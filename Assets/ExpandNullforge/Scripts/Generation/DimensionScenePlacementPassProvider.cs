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
    public sealed partial class DimensionScenePlacementPassProvider :
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
    }
}
