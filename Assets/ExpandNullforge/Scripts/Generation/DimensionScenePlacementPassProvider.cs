using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Scenes;
using ExpandNullforge.Zones;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// The generation pass that puts a dimension's authored structures into its world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider exists because nobody else will do it: Core Keeper's own placer
    /// (<c>SpawnDungeonAndSceneSystem</c>) only runs over Overworld spawn cells, which our
    /// provider-painted areas never produce — so in a custom dimension, every scene a modder
    /// authored simply never appeared, with nothing logged. This pass runs after terrain in the
    /// generation ladder and stamps them itself, through the same <c>SpawnCustomSceneCD</c>
    /// machinery the game uses.
    /// </para>
    /// <para>
    /// The spot search mirrors vanilla's shape on purpose: three sub-passes of decreasing
    /// clear-radius ambition ({44, 20, 0} tiles across {4, 8, 32} attempts), each attempt taking
    /// the best of 100 Halton samples by largest minimum margin. Big landmarks claim open ground
    /// early; small decorations tuck into what is left. The one deliberate improvement over
    /// vanilla is the pick: authored <c>Weight</c> drives a roulette rather than a uniform roll,
    /// because the weight field exists and a uniform pick would make it a lie.
    /// </para>
    /// <para>
    /// Determinism: everything rolls from the world seed mixed with the dimension id, so one
    /// world always lays out the same way and two worlds differ — matching vanilla, and
    /// deliberately unlike the overlay scatter (which is dimension-only so the same cavern
    /// greets every world).
    /// </para>
    /// </remarks>
    public sealed class DimensionScenePlacementPassProvider :
        IDimensionGenerationProvider,
        IDimensionGenerationPassProvider
    {
        /// <summary>Vanilla's ladder: demand a big clearing first, settle for any free tile last.</summary>
        private static readonly int[] MinClearRadiusPerPass = { 44, 20, 0 };

        private static readonly int[] AttemptsPerPass = { 4, 8, 32 };

        private const int HaltonSamplesPerAttempt = 100;

        /// <summary>Stamps queued per tick; the same order of magnitude as the terrain providers' write budget.</summary>
        private const int MaxStampsPerTick = 8;

        /// <summary>
        /// Extra copies of repeatable scenes per area, after every scene has placed once.
        /// Mirrors vanilla's last sub-pass budget (32 attempts per cell).
        /// </summary>
        private const int FillBudget = 32;

        /// <summary>Consecutive fill misses before the area is declared full.</summary>
        private const int FillFailureLimit = 8;

        /// <summary>
        /// How many ticks a not-yet-loaded footprint is retried before the scene is dropped.
        /// </summary>
        /// <remarks>
        /// The refusal is the common transient right after terrain settles — the chunk simply has
        /// not initialized yet — so it must be a retry, not a failure. But a chunk that never
        /// loads would otherwise wedge generation forever, so the retry has a ceiling.
        /// </remarks>
        private const int MaxAreaNotLoadedRetries = 120;

        private readonly NullforgeDimensionService service;

        private readonly Dictionary<string, PlacementJob> jobs =
            new Dictionary<string, PlacementJob>();

        public DimensionScenePlacementPassProvider(NullforgeDimensionService service)
        {
            this.service = service;
        }

        public string ProviderId
        {
            get { return DimensionGenerationProviderIds.ScenePlacement; }
        }

        public void ClearJobs()
        {
            jobs.Clear();
        }

        public bool CanGenerate(DimensionDefinition dimension, DimensionBounds localBounds)
        {
            // The content checks keep this provider out of dimensions with nothing to place:
            // the plan synthesizer consults CanGenerate before appending the auto-scenes pass,
            // so a scene-less dimension never carries a scenes pass at all.
            return !string.IsNullOrEmpty(dimension.Id) &&
                   dimension.Id != DimensionIds.Overworld &&
                   dimension.HasCapability(DimensionCapabilityFlags.Generation) &&
                   service != null &&
                   (DimensionScenePoolRegistry.Has(dimension.Id) ||
                    HasPlannedScenes(dimension.Id));
        }

        public DimensionGenerationProviderResult TickGeneration(DimensionGenerationContext context)
        {
            // Single-provider mode: whole area, same behavior as a full-area pass.
            return TickPass(context, context.Area.LocalBounds);
        }

        public DimensionGenerationProviderResult TickGenerationPass(
            DimensionGenerationPassContext context)
        {
            // The shared resolution, so all five providers scope a step the same way.
            return TickPass(
                context.GenerationContext,
                DimensionGenerationPassBounds.Resolve(context));
        }

        private DimensionGenerationProviderResult TickPass(
            DimensionGenerationContext context,
            DimensionBounds passBounds)
        {
            if (context.ServerWorld == null || !context.ServerWorld.IsCreated)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.StampingScenes,
                    0.01f,
                    "Waiting for the server world.");
            }

            string key = CreateKey(context.Dimension.Id, context.Area.LocalBounds, passBounds);
            PlacementJob job;
            if (!jobs.TryGetValue(key, out job))
            {
                job = new PlacementJob();
                jobs[key] = job;
            }

            if (job.Completed)
            {
                return job.Failed
                    ? DimensionGenerationProviderResult.Failed(job.CompletionMessage)
                    : DimensionGenerationProviderResult.Ready(job.CompletionMessage);
            }

            // THE INJECTION BELT. The scene table normally swaps when the game's own placer
            // starts, but on a save whose Overworld is already fully generated that system may
            // never start — and a stamp against the un-swapped table is silently destroyed by
            // the name miss. Idempotent per world, so this costs one reference compare once done.
            if (!DimensionCustomSceneTableInjector.TryInject(context.ServerWorld) &&
                DimensionCustomSceneRegistry.Count > 0)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.StampingScenes,
                    0.02f,
                    "Waiting for the scene table.");
            }

            if (!job.SeedResolved)
            {
                uint worldSeed;
                if (!TryReadWorldSeed(context.ServerWorld, out worldSeed))
                {
                    return DimensionGenerationProviderResult.Progress(
                        DimensionGenerationState.StampingScenes,
                        0.02f,
                        "Waiting for the world seed.");
                }

                job.Seed = DimensionOverlayScatter.Hash(
                    worldSeed,
                    context.Area.LocalBounds.Min,
                    StableHash(context.Dimension.Id) ^ StableHash("scene-pass"));
                job.SeedResolved = true;
            }

            if (!job.Planned)
            {
                PlanJob(job, context, passBounds);
                job.Planned = true;
            }

            int stamps = 0;
            while (stamps < MaxStampsPerTick && job.Pending.Count > 0)
            {
                PendingPlacement pending = job.Pending[0];
                PlacementOutcome outcome = TryPlaceOne(context, passBounds, job, pending);
                if (outcome == PlacementOutcome.RetryNextTick)
                {
                    // The world has to load a chunk before this can move; burning the rest of
                    // the stamp budget on the same closed door helps nobody.
                    break;
                }

                job.Pending.RemoveAt(0);
                if (outcome == PlacementOutcome.Placed)
                {
                    job.PlacedCount++;
                    stamps++;
                    continue;
                }

                if (pending.Required)
                {
                    job.Completed = true;
                    job.Failed = true;
                    job.CompletionMessage =
                        "Required scene '" + pending.SceneId + "' found no valid spot in " +
                        context.Dimension.Id + ". The area is marked failed because the " +
                        "dimension would be missing something its author declared essential.";
                    DimensionFrameworkLog.Warning(job.CompletionMessage);
                    return DimensionGenerationProviderResult.Failed(job.CompletionMessage);
                }

                DimensionFrameworkLog.Warning(
                    "Scene '" + pending.SceneId + "' was skipped in " +
                    context.Dimension.Id + ": " + pending.LastError);
            }

            if (job.Pending.Count > 0)
            {
                float done = job.TotalPlanned <= 0
                    ? 0.5f
                    : (float)(job.TotalPlanned - job.Pending.Count) / job.TotalPlanned;
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.StampingScenes,
                    math.clamp(0.05f + 0.85f * done, 0.05f, 0.9f),
                    "Placing scenes: " + (job.TotalPlanned - job.Pending.Count) + " of " +
                    job.TotalPlanned + ".");
            }

            // THE FILL PHASE — where authored Weight becomes real. Every scene has now placed
            // once; repeatable ones (Unique false) keep landing until the area is full or the
            // budget runs out, each copy chosen by weighted roulette among the scenes whose
            // policy admits the sampled spot. Without this, Weight would be an authored field
            // nothing reads — the identity-table bug class in miniature.
            if (!TickFillPhase(context, passBounds, job, ref stamps))
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.StampingScenes,
                    0.95f,
                    "Filling with repeatable scenes: " + (FillBudget - job.FillRemaining) +
                    " of up to " + FillBudget + ".");
            }

            job.Completed = true;
            job.CompletionMessage = job.PlacedCount == 0
                ? "No scenes needed placing."
                : "Placed " + job.PlacedCount + " scene" + (job.PlacedCount == 1 ? "" : "s") + ".";
            return DimensionGenerationProviderResult.Ready(job.CompletionMessage);
        }

        /// <summary>Runs fill stamps inside this tick's budget. True when the phase is finished.</summary>
        private bool TickFillPhase(
            DimensionGenerationContext context,
            DimensionBounds passBounds,
            PlacementJob job,
            ref int stamps)
        {
            if (job.FillPool.Count == 0)
            {
                return true;
            }

            while (stamps < MaxStampsPerTick &&
                   job.FillRemaining > 0 &&
                   job.FillFailureStreak < FillFailureLimit)
            {
                int fillIndex = FillBudget - job.FillRemaining;
                int2 spot;
                if (!TryFindFillSpot(job, passBounds, fillIndex, out spot))
                {
                    job.FillRemaining--;
                    job.FillFailureStreak++;
                    continue;
                }

                FillCandidate choice = PickFillCandidate(job, spot, context.Dimension.Id);
                if (choice == null)
                {
                    job.FillRemaining--;
                    job.FillFailureStreak++;
                    continue;
                }

                int2 absolute = context.Area.AbsoluteBounds.Min +
                                (spot - context.Area.LocalBounds.Min);
                uint instanceSeed = (uint)DimensionOverlayScatter.Hash(job.Seed, absolute, 3);

                DimensionScenePlacementResult result;
                string error;
                if (!DimensionScenePlacement.TryPlace(
                        context.ServerWorld,
                        choice.Entry.RegisteredSceneName,
                        absolute,
                        instanceSeed,
                        out result,
                        out error))
                {
                    if (result == DimensionScenePlacementResult.AreaNotLoaded)
                    {
                        // Do not burn budget on a chunk that is still loading — come back.
                        return false;
                    }

                    job.Placed.Add(new PlacedFootprint
                    {
                        LocalPosition = spot,
                        Radius = FillFootprintRadius(choice.Entry)
                    });
                    job.FillRemaining--;
                    job.FillFailureStreak++;
                    continue;
                }

                int radius = FillFootprintRadius(choice.Entry);
                job.Placed.Add(new PlacedFootprint { LocalPosition = spot, Radius = radius });
                choice.PlacedAt.Add(spot);
                RegisterSceneTriggers(context.Dimension.Id, choice.Entry.RegisteredSceneName, absolute);
                RecordFillPlacement(context.Dimension.Id, choice, spot);
                job.PlacedCount++;
                job.FillRemaining--;
                job.FillFailureStreak = 0;
                stamps++;
            }

            return job.FillRemaining <= 0 || job.FillFailureStreak >= FillFailureLimit;
        }

        /// <summary>A fill spot only needs open ground — per-scene policy is applied at the pick.</summary>
        private static bool TryFindFillSpot(
            PlacementJob job,
            DimensionBounds bounds,
            int fillIndex,
            out int2 spot)
        {
            spot = default;
            int2 size = bounds.Size;
            if (size.x <= 0 || size.y <= 0)
            {
                return false;
            }

            int indexOffset = (int)(job.Seed % 65536) + 20000 + fillIndex * HaltonSamplesPerAttempt;
            float bestScore = -1f;
            int2 best = default;
            for (int sample = 0; sample < HaltonSamplesPerAttempt; sample++)
            {
                int2 candidate = HaltonPoint(indexOffset + sample, bounds);
                float score = math.min(
                    math.min(candidate.x - bounds.Min.x, bounds.MaxExclusive.x - 1 - candidate.x),
                    math.min(candidate.y - bounds.Min.y, bounds.MaxExclusive.y - 1 - candidate.y));
                for (int i = 0; i < job.Placed.Count; i++)
                {
                    PlacedFootprint placed = job.Placed[i];
                    float distance = math.length(new float2(
                        candidate.x - placed.LocalPosition.x,
                        candidate.y - placed.LocalPosition.y)) - placed.Radius;
                    score = math.min(score, distance);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (bestScore < 2f)
            {
                return false;
            }

            spot = best;
            return true;
        }

        /// <summary>
        /// Weighted roulette among the repeatable scenes whose policy admits this spot —
        /// deterministic, seeded from the spot itself.
        /// </summary>
        private FillCandidate PickFillCandidate(PlacementJob job, int2 spot, string dimensionId)
        {
            int totalWeight = 0;
            for (int i = 0; i < job.FillPool.Count; i++)
            {
                FillCandidate candidate = job.FillPool[i];
                candidate.EligibleThisRoll = !candidate.Exhausted &&
                                             IsFillEligible(candidate, spot, dimensionId);
                if (candidate.EligibleThisRoll)
                {
                    totalWeight += candidate.Entry.Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            uint roll = (uint)(DimensionOverlayScatter.Hash(job.Seed, spot, 2) % (ulong)totalWeight);
            for (int i = 0; i < job.FillPool.Count; i++)
            {
                FillCandidate candidate = job.FillPool[i];
                if (!candidate.EligibleThisRoll)
                {
                    continue;
                }

                if (roll < (uint)candidate.Entry.Weight)
                {
                    return candidate;
                }

                roll -= (uint)candidate.Entry.Weight;
            }

            return null;
        }

        private bool IsFillEligible(FillCandidate candidate, int2 spot, string dimensionId)
        {
            DimensionScenePoolEntry entry = candidate.Entry;

            // Same-scene spacing: copies of one template keep two footprints of air between
            // them, so a heavy weight makes a scene common rather than carpeted.
            int minSelfDistance = 2 * math.max(entry.FootprintSize.x, entry.FootprintSize.y);
            for (int i = 0; i < candidate.PlacedAt.Count; i++)
            {
                if (math.length(new float2(
                        spot.x - candidate.PlacedAt[i].x,
                        spot.y - candidate.PlacedAt[i].y)) < minSelfDistance)
                {
                    return false;
                }
            }

            if (entry.Mode == DimensionScenePlacementMode.PreferredBounds &&
                entry.HasPreferredLocalBounds &&
                !entry.PreferredLocalBounds.Contains(spot))
            {
                return false;
            }

            return IsEligibleSpot(candidate.Probe, dimensionId, spot);
        }

        private void RecordFillPlacement(string dimensionId, FillCandidate candidate, int2 spot)
        {
            DimensionOperationResult opResult;
            string copyId = candidate.Entry.SceneId + "#" + candidate.PlacedAt.Count;
            int2 half = candidate.Entry.FootprintSize / 2;
            DimensionSceneDefinition record = new DimensionSceneDefinition(
                copyId,
                candidate.Entry.SceneId,
                dimensionId,
                new DimensionBounds(spot - half, spot + half + new int2(1, 1)),
                string.IsNullOrEmpty(candidate.Entry.BiomeId) ? "scene" : candidate.Entry.BiomeId,
                candidate.Entry.Priority,
                DimensionSceneState.Planned);
            service.TryRegisterScene(record, out opResult);
            service.TrySetSceneState(copyId, DimensionSceneState.Ready, "scene-placement-fill", out opResult);
        }

        private static int FillFootprintRadius(DimensionScenePoolEntry entry)
        {
            return math.max(
                1,
                (int)math.ceil(0.5f * math.length(
                    new float2(entry.FootprintSize.x, entry.FootprintSize.y))));
        }

        /// <summary>
        /// Builds the ordered work list once: authored positions first (they own their ground),
        /// then the pool's free placements filling in around them.
        /// </summary>
        private void PlanJob(PlacementJob job, DimensionGenerationContext context, DimensionBounds passBounds)
        {
            string dimensionId = context.Dimension.Id;
            IReadOnlyList<DimensionSceneDefinition> records = service.GetScenes(dimensionId, true);
            IReadOnlyList<DimensionScenePoolEntry> pool = DimensionScenePoolRegistry.For(dimensionId);

            Dictionary<string, DimensionScenePoolEntry> poolById =
                new Dictionary<string, DimensionScenePoolEntry>(System.StringComparer.Ordinal);
            for (int i = 0; i < pool.Count; i++)
            {
                poolById[pool[i].SceneId] = pool[i];
            }

            // Scenes already Ready were stamped by an earlier run of this area — re-stamping
            // would duplicate their chests and re-clear their tiles, so Ready is a hard skip.
            // Their triggered tiles, though, live in an in-memory registry and must be re-armed
            // every time the pass revisits the area, or a saved world's traps would fall silent.
            int2 worldOffset = context.Area.AbsoluteBounds.Min - context.Area.LocalBounds.Min;
            HashSet<string> alreadyPlaced = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].State == DimensionSceneState.Ready)
                {
                    alreadyPlaced.Add(records[i].SceneId);
                    if (Intersects(records[i].LocalBounds, passBounds))
                    {
                        RearmPlacedSceneTriggers(records[i], poolById, worldOffset, dimensionId);
                    }
                }
            }

            // 1. Compiled Planned records: the author said WHERE, so these go first and the
            //    compiled rectangle wins over any pool duplicate of the same scene.
            for (int i = 0; i < records.Count; i++)
            {
                DimensionSceneDefinition record = records[i];
                if (record.State != DimensionSceneState.Planned ||
                    alreadyPlaced.Contains(record.SceneId) ||
                    !Intersects(record.LocalBounds, passBounds))
                {
                    continue;
                }

                DimensionScenePoolEntry policy;
                poolById.TryGetValue(record.SceneId, out policy);

                DimensionCustomSceneDefinition tileData;
                string sceneName = policy != null
                    ? policy.RegisteredSceneName
                    : (DimensionCustomSceneRegistry.TryGet(record.SceneId, out tileData)
                        ? record.SceneId
                        : null);
                if (sceneName == null)
                {
                    DimensionFrameworkLog.Warning(
                        "Planned scene '" + record.SceneId + "' has no " +
                        "registered tile data under any known name, so it cannot be placed.");
                    continue;
                }

                int2 size = record.LocalBounds.Size;
                bool exact = math.all(size <= new int2(1, 1)) ||
                             (policy != null &&
                              policy.Mode == DimensionScenePlacementMode.ExactLocalPosition);

                job.Pending.Add(new PendingPlacement
                {
                    SceneId = record.SceneId,
                    RegisteredSceneName = sceneName,
                    Policy = policy,
                    Kind = exact ? PendingKind.Exact : PendingKind.Preferred,
                    // The compiled record carries the authored ranking; the pool entry is the
                    // fallback for a scene the compiler never produced a rectangle for.
                    Priority = policy != null ? policy.Priority : record.Priority,
                    Required = policy != null && policy.Required,
                    HasSceneRecord = true
                });
            }

            // 2. Pool entries with no compiled rectangle: Automatic and RadialBand, plus any
            //    Exact/Preferred whose compiled record the compiler dropped.
            for (int i = 0; i < pool.Count; i++)
            {
                DimensionScenePoolEntry entry = pool[i];
                if (alreadyPlaced.Contains(entry.SceneId) || HasPending(job, entry.SceneId))
                {
                    continue;
                }

                PendingKind kind;
                DimensionBounds authored = default;
                switch (entry.Mode)
                {
                    case DimensionScenePlacementMode.ExactLocalPosition:
                        kind = PendingKind.Exact;
                        authored = new DimensionBounds(
                            entry.ExactLocalPosition,
                            entry.ExactLocalPosition + new int2(1, 1));
                        break;
                    case DimensionScenePlacementMode.PreferredBounds:
                        kind = PendingKind.Preferred;
                        authored = entry.HasPreferredLocalBounds
                            ? entry.PreferredLocalBounds
                            : passBounds;
                        break;
                    default:
                        kind = PendingKind.Free;
                        break;
                }

                job.Pending.Add(new PendingPlacement
                {
                    SceneId = entry.SceneId,
                    RegisteredSceneName = entry.RegisteredSceneName,
                    Policy = entry,
                    Kind = kind,
                    Priority = entry.Priority,
                    AuthoredBounds = authored,
                    Required = entry.Required,
                    HasSceneRecord = false
                });
            }

            // Authored positions before free ones, big footprints before small: the landmark
            // claims its clearing before the decorations spend the open ground.
            job.Pending.Sort(ComparePending);
            job.TotalPlanned = job.Pending.Count;

            // 3. The fill pool: repeatable scenes that may land again after everything has
            //    placed once. Exact positions are one-of by nature, so they never fill.
            for (int i = 0; i < pool.Count; i++)
            {
                DimensionScenePoolEntry entry = pool[i];
                if (entry.Unique ||
                    entry.Mode == DimensionScenePlacementMode.ExactLocalPosition)
                {
                    continue;
                }

                FillCandidate candidate = new FillCandidate
                {
                    Entry = entry,
                    Probe = new PendingPlacement
                    {
                        SceneId = entry.SceneId,
                        RegisteredSceneName = entry.RegisteredSceneName,
                        Policy = entry
                    }
                };

                // Regeneration guard: copies from an earlier run of this area are already in
                // the world. Placing more would double the density every regeneration, so a
                // scene with surviving copies sits the fill out entirely.
                string copyPrefix = entry.SceneId + "#";
                for (int r = 0; r < records.Count; r++)
                {
                    if (records[r].State == DimensionSceneState.Ready &&
                        records[r].SceneId.StartsWith(copyPrefix, System.StringComparison.Ordinal))
                    {
                        candidate.Exhausted = true;
                        break;
                    }
                }

                job.FillPool.Add(candidate);
            }
        }

        /// <summary>
        /// The order scenes get first refusal on ground: authored spots, then the author's own
        /// ranking, then the biggest, then the id.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Kind stays ahead of priority on purpose, and this is a deliberate departure from the
        /// triage note that asked for priority first. Kind is not a preference — an Exact or
        /// Preferred scene was given a position by its author, and letting a merely
        /// higher-priority free-floating scene claim that ground first would break a promise the
        /// author made explicitly. Priority ranks the scenes that are genuinely competing for
        /// the same open ground, which is where it means something.
        /// </para>
        /// <para>
        /// Higher priority sorts first. Everything after it is the previous ordering, kept as
        /// tiebreakers so placement stays deterministic for a given seed.
        /// </para>
        /// </remarks>
        private static int ComparePending(PendingPlacement left, PendingPlacement right)
        {
            int kind = ((int)left.Kind).CompareTo((int)right.Kind);
            if (kind != 0)
            {
                return kind;
            }

            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int leftSize = left.Policy != null
                ? math.max(left.Policy.FootprintSize.x, left.Policy.FootprintSize.y)
                : math.max(left.AuthoredBounds.Size.x, left.AuthoredBounds.Size.y);
            int rightSize = right.Policy != null
                ? math.max(right.Policy.FootprintSize.x, right.Policy.FootprintSize.y)
                : math.max(right.AuthoredBounds.Size.x, right.AuthoredBounds.Size.y);
            int size = rightSize.CompareTo(leftSize);
            return size != 0
                ? size
                : string.CompareOrdinal(left.SceneId, right.SceneId);
        }

        private PlacementOutcome TryPlaceOne(
            DimensionGenerationContext context,
            DimensionBounds passBounds,
            PlacementJob job,
            PendingPlacement pending)
        {
            int2 localSpot;
            switch (pending.Kind)
            {
                case PendingKind.Exact:
                    // The anchor is the scene's CENTRE (scenes register an explicit middle
                    // pivot), so an authored rectangle is honoured by anchoring at its centre —
                    // the same floor((min+max)/2) the pivot itself uses, so a rectangle sized
                    // like the scene is filled edge to edge. A pool point is a 1x1 rectangle
                    // and comes out unchanged.
                    localSpot = new int2(
                        (pending.AuthoredBounds.Min.x + pending.AuthoredBounds.MaxExclusive.x - 1) >> 1,
                        (pending.AuthoredBounds.Min.y + pending.AuthoredBounds.MaxExclusive.y - 1) >> 1);
                    break;
                case PendingKind.Preferred:
                {
                    DimensionBounds search = Intersect(pending.AuthoredBounds, passBounds);
                    if (!TryFindSpot(job, search, pending, context.Dimension.Id, out localSpot))
                    {
                        pending.LastError = "no clear spot inside its preferred area.";
                        return PlacementOutcome.NoSpot;
                    }

                    break;
                }
                default:
                {
                    if (!TryFindSpot(job, passBounds, pending, context.Dimension.Id, out localSpot))
                    {
                        pending.LastError = "no clear spot in the generated area.";
                        return PlacementOutcome.NoSpot;
                    }

                    break;
                }
            }

            int2 absolute = context.Area.AbsoluteBounds.Min +
                            (localSpot - context.Area.LocalBounds.Min);
            uint instanceSeed = (uint)DimensionOverlayScatter.Hash(job.Seed, absolute, 1);

            DimensionScenePlacementResult result;
            string error;
            if (!DimensionScenePlacement.TryPlace(
                    context.ServerWorld,
                    pending.RegisteredSceneName,
                    absolute,
                    instanceSeed,
                    out result,
                    out error))
            {
                if (result == DimensionScenePlacementResult.AreaNotLoaded)
                {
                    pending.AreaNotLoadedRetries++;
                    if (pending.AreaNotLoadedRetries <= MaxAreaNotLoadedRetries)
                    {
                        return PlacementOutcome.RetryNextTick;
                    }

                    pending.LastError =
                        "its footprint never finished loading (" + MaxAreaNotLoadedRetries +
                        " ticks): " + error;
                    return PlacementOutcome.NoSpot;
                }

                // Refused on player builds. A free placement can look for other ground; an
                // authored position cannot — the author picked that exact spot.
                if (pending.Kind == PendingKind.Free && pending.RefusedSpots < 3)
                {
                    pending.RefusedSpots++;
                    job.Placed.Add(new PlacedFootprint
                    {
                        // Poison the refused spot so the retry does not sample it again.
                        LocalPosition = localSpot,
                        Radius = FootprintRadius(pending)
                    });
                    return PlacementOutcome.RetryNextTick;
                }

                pending.LastError = error;
                return PlacementOutcome.NoSpot;
            }

            job.Placed.Add(new PlacedFootprint
            {
                LocalPosition = localSpot,
                Radius = FootprintRadius(pending)
            });

            RegisterSceneTriggers(context.Dimension.Id, pending.RegisteredSceneName, absolute);
            RecordPlacement(context.Dimension.Id, pending, localSpot);
            return PlacementOutcome.Placed;
        }

        /// <summary>
        /// Arms a placed scene's triggered tiles, now that the scene finally has an anchor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the only moment the framework knows where a scene landed, which is why the
        /// registrations happen here rather than in the bootstrap: a trigger authored in
        /// scene-local coordinates becomes a set of world-coordinate cells only once
        /// <c>TryPlace</c> has succeeded. The stamp uses anchor + (tile − centre), so the same
        /// arithmetic maps each covered cell.
        /// </para>
        /// <para>
        /// One landing shares one trigger id across its whole patch, so a multi-tile plate has a
        /// single cooldown and a once-only trap really fires once — while two placed copies of
        /// the same scene stay independent traps.
        /// </para>
        /// <para>
        /// KNOWN LIMIT. A scene embedded in a generated dungeon room is stamped by Core Keeper's
        /// own machinery, which reports no position — those copies never pass through here, so
        /// their triggers stay silent.
        /// </para>
        /// </remarks>
        private static void RegisterSceneTriggers(
            string dimensionId,
            string registeredSceneName,
            int2 absoluteAnchor)
        {
            DimensionCustomSceneDefinition definition;
            if (string.IsNullOrEmpty(registeredSceneName) ||
                !DimensionCustomSceneRegistry.TryGet(registeredSceneName, out definition) ||
                definition.Triggers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < definition.Triggers.Count; i++)
            {
                DimensionSceneTrigger trigger = definition.Triggers[i];
                string triggerId = registeredSceneName + ":" + trigger.TriggerId + "@" +
                    absoluteAnchor.x + "," + absoluteAnchor.y;
                for (int y = trigger.LocalMin.y; y < trigger.LocalMaxExclusive.y; y++)
                {
                    for (int x = trigger.LocalMin.x; x < trigger.LocalMaxExclusive.x; x++)
                    {
                        int2 world = absoluteAnchor + (new int2(x, y) - definition.CenterPosition);
                        DimensionTriggeredTileRegistry.Register(new DimensionTriggeredTileDefinition(
                            triggerId,
                            dimensionId,
                            world,
                            trigger.Kind,
                            trigger.CarriedItemName,
                            trigger.Action,
                            trigger.ActionTarget,
                            trigger.Amount,
                            trigger.ConditionSeconds,
                            0f, // radius: unauthored on purpose — the system's own 2-tile default applies.
                            trigger.OnceOnly,
                            trigger.CooldownSeconds));
                    }
                }
            }
        }

        /// <summary>
        /// Re-arms the triggers of a scene an earlier run already stamped, recovering the anchor
        /// from the recorded rectangle.
        /// </summary>
        /// <remarks>
        /// The recorded bounds were built as anchor ± half, so the rectangle's centre — the same
        /// floor((min+max)/2) the stamp pivot uses — is the anchor that was used. Registration is
        /// idempotent per trigger id, so revisiting an area costs nothing but the loop.
        /// </remarks>
        private static void RearmPlacedSceneTriggers(
            DimensionSceneDefinition record,
            Dictionary<string, DimensionScenePoolEntry> poolById,
            int2 worldOffset,
            string dimensionId)
        {
            // A fill copy is recorded as "scene#N"; its registered tile data lives under the base id.
            string baseId = record.SceneId;
            int copyMarker = baseId.IndexOf('#');
            if (copyMarker >= 0)
            {
                baseId = baseId.Substring(0, copyMarker);
            }

            DimensionScenePoolEntry entry;
            string sceneName = poolById.TryGetValue(baseId, out entry)
                ? entry.RegisteredSceneName
                : baseId;

            int2 anchor = new int2(
                (record.LocalBounds.Min.x + record.LocalBounds.MaxExclusive.x - 1) >> 1,
                (record.LocalBounds.Min.y + record.LocalBounds.MaxExclusive.y - 1) >> 1);
            RegisterSceneTriggers(dimensionId, sceneName, anchor + worldOffset);
        }

        /// <summary>
        /// Marks the scene Ready in the runtime records, registering a record first when the
        /// placement came from the pool. Ready is what makes re-generation idempotent.
        /// </summary>
        private void RecordPlacement(string dimensionId, PendingPlacement pending, int2 localSpot)
        {
            DimensionOperationResult opResult;
            if (!pending.HasSceneRecord)
            {
                int2 half = pending.Policy != null
                    ? pending.Policy.FootprintSize / 2
                    : new int2(8, 8);
                DimensionSceneDefinition record = new DimensionSceneDefinition(
                    pending.SceneId,
                    pending.SceneId,
                    dimensionId,
                    new DimensionBounds(localSpot - half, localSpot + half + new int2(1, 1)),
                    pending.Policy != null && !string.IsNullOrEmpty(pending.Policy.BiomeId)
                        ? pending.Policy.BiomeId
                        : "scene",
                    pending.Policy != null ? pending.Policy.Priority : 0,
                    DimensionSceneState.Planned);
                service.TryRegisterScene(record, out opResult);
            }

            service.TrySetSceneState(
                pending.SceneId,
                DimensionSceneState.Ready,
                "scene-placement-pass",
                out opResult);
        }

        /// <summary>
        /// Vanilla's spot search: sub-passes of falling ambition, best-of-100 Halton samples per
        /// attempt, scored by the smallest margin to anything already placed or to the edge.
        /// </summary>
        private bool TryFindSpot(
            PlacementJob job,
            DimensionBounds searchBounds,
            PendingPlacement pending,
            string dimensionId,
            out int2 spot)
        {
            spot = default;
            int2 size = searchBounds.Size;
            if (size.x <= 0 || size.y <= 0)
            {
                return false;
            }

            int footprint = FootprintRadius(pending);
            bool small = math.min(size.x, size.y) < 96;

            for (int pass = small ? 1 : 0; pass < MinClearRadiusPerPass.Length; pass++)
            {
                int wantClear = math.max(MinClearRadiusPerPass[pass], footprint);
                for (int attempt = 0; attempt < AttemptsPerPass[pass]; attempt++)
                {
                    int indexOffset =
                        (int)(job.Seed % 65536) + attempt * HaltonSamplesPerAttempt + pass * 10000;
                    float bestScore = -1f;
                    int2 best = default;
                    for (int sample = 0; sample < HaltonSamplesPerAttempt; sample++)
                    {
                        int2 candidate = HaltonPoint(indexOffset + sample, searchBounds);
                        if (!IsEligibleSpot(pending, dimensionId, candidate))
                        {
                            continue;
                        }

                        float score = ScoreSpot(job, searchBounds, candidate, pending);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = candidate;
                        }
                    }

                    if (bestScore >= wantClear)
                    {
                        spot = best;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The policy gates that are about the SPOT, not about clearance.</summary>
        private bool IsEligibleSpot(PendingPlacement pending, string dimensionId, int2 candidate)
        {
            DimensionScenePoolEntry policy = pending.Policy;
            if (policy == null)
            {
                return true;
            }

            if (policy.Mode == DimensionScenePlacementMode.RadialBand)
            {
                // Local (0,0) is the dimension's centre by the fallback-bounds convention, so
                // the band is a plain distance check.
                float distance = math.length(new float2(candidate.x, candidate.y));
                if (distance < policy.MinRadiusTiles || distance >= policy.MaxRadiusTiles)
                {
                    return false;
                }
            }

            bool wantsBiome =
                !string.IsNullOrEmpty(policy.BiomeId) ||
                !string.IsNullOrEmpty(policy.RadialBiomeId) ||
                policy.AllowedBiomeIds.Count > 0;
            if (!wantsBiome)
            {
                return true;
            }

            DimensionZoneDefinition zone;
            if (!service.TryFindZoneDefinitionAtLocal(
                    dimensionId,
                    new float2(candidate.x, candidate.y),
                    out zone))
            {
                // A biome-restricted scene outside every biome region has no claim to the spot.
                return false;
            }

            string biomeAtSpot = zone.Kind ?? string.Empty;
            if (!string.IsNullOrEmpty(policy.RadialBiomeId) &&
                string.Equals(policy.RadialBiomeId, biomeAtSpot, System.StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(policy.BiomeId) &&
                string.Equals(policy.BiomeId, biomeAtSpot, System.StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < policy.AllowedBiomeIds.Count; i++)
            {
                if (string.Equals(policy.AllowedBiomeIds[i], biomeAtSpot, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Smallest margin to the bounds edge or to any prior placement — bigger is safer.</summary>
        private static float ScoreSpot(
            PlacementJob job,
            DimensionBounds bounds,
            int2 candidate,
            PendingPlacement pending)
        {
            float score = math.min(
                math.min(candidate.x - bounds.Min.x, bounds.MaxExclusive.x - 1 - candidate.x),
                math.min(candidate.y - bounds.Min.y, bounds.MaxExclusive.y - 1 - candidate.y));

            int ownFootprint = FootprintRadius(pending);
            for (int i = 0; i < job.Placed.Count; i++)
            {
                PlacedFootprint placed = job.Placed[i];
                float distance =
                    math.length(new float2(
                        candidate.x - placed.LocalPosition.x,
                        candidate.y - placed.LocalPosition.y)) -
                    placed.Radius - ownFootprint;
                score = math.min(score, distance);
            }

            return score;
        }

        private static int FootprintRadius(PendingPlacement pending)
        {
            int2 size = pending.Policy != null
                ? pending.Policy.FootprintSize
                : math.max(pending.AuthoredBounds.Size, new int2(8, 8));
            return math.max(1, (int)math.ceil(0.5f * math.length(new float2(size.x, size.y))));
        }

        /// <summary>Low-discrepancy point n of the (2,3) Halton sequence, mapped over the bounds.</summary>
        private static int2 HaltonPoint(int index, DimensionBounds bounds)
        {
            int2 size = bounds.Size;
            return new int2(
                bounds.Min.x + (int)(RadicalInverse(index, 2) * size.x),
                bounds.Min.y + (int)(RadicalInverse(index, 3) * size.y));
        }

        private static float RadicalInverse(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;
            int n = index < 0 ? -index : index;
            while (n > 0)
            {
                result += (n % radix) * fraction;
                n /= radix;
                fraction /= radix;
            }

            return math.clamp(result, 0f, 0.9999f);
        }

        private bool HasPlannedScenes(string dimensionId)
        {
            IReadOnlyList<DimensionSceneDefinition> scenes = service.GetScenes(dimensionId, false);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].State == DimensionSceneState.Planned)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPending(PlacementJob job, string sceneId)
        {
            for (int i = 0; i < job.Pending.Count; i++)
            {
                if (string.Equals(job.Pending[i].SceneId, sceneId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryReadWorldSeed(World world, out uint seed)
        {
            seed = 0;
            EntityQuery query = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ServerSeedCD>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            seed = query.GetSingleton<ServerSeedCD>().Value;
            return true;
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                // FNV-1a, the framework's standard stable string hash (see the tileset ids).
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619u;
                }

                return (int)hash;
            }
        }

        private static DimensionBounds Intersect(DimensionBounds left, DimensionBounds right)
        {
            return new DimensionBounds(
                math.max(left.Min, right.Min),
                math.min(left.MaxExclusive, right.MaxExclusive));
        }

        private static bool Intersects(DimensionBounds left, DimensionBounds right)
        {
            return left.Min.x < right.MaxExclusive.x && right.Min.x < left.MaxExclusive.x &&
                   left.Min.y < right.MaxExclusive.y && right.Min.y < left.MaxExclusive.y;
        }

        private static string CreateKey(
            string dimensionId,
            DimensionBounds area,
            DimensionBounds pass)
        {
            return dimensionId + "|" +
                   area.Min.x + "," + area.Min.y + "|" +
                   pass.Min.x + "," + pass.Min.y + "," +
                   pass.MaxExclusive.x + "," + pass.MaxExclusive.y;
        }

        private enum PendingKind
        {
            // The order IS the placement order.
            Exact = 0,
            Preferred = 1,
            Free = 2
        }

        private enum PlacementOutcome
        {
            Placed,
            RetryNextTick,
            NoSpot
        }

        private sealed class PendingPlacement
        {
            public string SceneId;
            public string RegisteredSceneName;
            public DimensionScenePoolEntry Policy;
            public PendingKind Kind;

            /// <summary>The author's own ranking, higher first. Read by the placement order.</summary>
            public int Priority;

            public DimensionBounds AuthoredBounds;
            public bool Required;
            public bool HasSceneRecord;
            public int AreaNotLoadedRetries;
            public int RefusedSpots;
            public string LastError;
        }

        private struct PlacedFootprint
        {
            public int2 LocalPosition;
            public int Radius;
        }

        private sealed class FillCandidate
        {
            public DimensionScenePoolEntry Entry;

            /// <summary>A policy carrier for the shared spot-eligibility check.</summary>
            public PendingPlacement Probe;

            public readonly List<int2> PlacedAt = new List<int2>();
            public bool Exhausted;
            public bool EligibleThisRoll;
        }

        private sealed class PlacementJob
        {
            public readonly List<PendingPlacement> Pending = new List<PendingPlacement>();
            public readonly List<PlacedFootprint> Placed = new List<PlacedFootprint>();
            public readonly List<FillCandidate> FillPool = new List<FillCandidate>();
            public bool SeedResolved;
            public ulong Seed;
            public bool Planned;
            public int TotalPlanned;
            public int PlacedCount;
            public int FillRemaining = FillBudget;
            public int FillFailureStreak;
            public bool Completed;
            public bool Failed;
            public string CompletionMessage = string.Empty;
        }
    }
}
