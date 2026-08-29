using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Scenes;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Grows a dimension's authored dungeons inside it — the Structures phase of generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's dungeon placer runs only over Overworld spawn cells; a custom dimension never
    /// produces one, so a dungeon authored FOR a dimension could never appear in it. This pass
    /// closes that: it picks spots the way vanilla does, then births instances straight from
    /// the assembler's prototypes — <c>Instantiate</c> plus a positioned transform, exactly the
    /// two writes vanilla's own SpawnJob performs — and the game's dungeon pipeline does the
    /// rest, rooms and corridors and fills and scenes, in the same systems vanilla uses.
    /// </para>
    /// <para>
    /// Structures phase, deliberately between Terrain and Scenes: the dungeon carves its rooms
    /// into settled terrain, and standalone scenes placed afterwards see the dungeon's ground
    /// as occupied space in their own margins.
    /// </para>
    /// <para>
    /// A SCOPED STEP GROWS DUNGEONS ONLY INSIDE ITS RECTANGLE. Both the spot search and the
    /// margin score measure from the step's rectangle, not the whole area, so a dungeon asked to
    /// sit in one corner keeps its clearance inside that corner instead of drifting to the middle
    /// of the map where the scoring is better.
    /// </para>
    /// <para>
    /// COMPLETION IS A GRACE PERIOD, stated honestly. The pipeline's own "done" signal is a
    /// trigger component with an awkward lifecycle; the generator time-slices to roughly a
    /// second per dungeon, so this pass holds the ladder for a fixed settle after the last
    /// instance is born. A dungeon still filling when the pass reports Ready degrades to
    /// scenery appearing a moment late — never to a half-written save, because the game
    /// refuses to begin one while any dungeon marker lives.
    /// </para>
    /// </remarks>
    public sealed class DimensionDungeonPlacementPassProvider :
        IDimensionGenerationProvider,
        IDimensionGenerationPassProvider
    {
        private const double SettleSecondsPerDungeon = 1.5d;
        private const int HaltonSamplesPerAttempt = 100;
        private static readonly int[] AttemptsPerPass = { 4, 8, 32 };
        private static readonly int[] MinClearRadiusPerPass = { 44, 20, 0 };

        private readonly NullforgeDimensionService service;

        private sealed class PlacementJob
        {
            public bool Placed;
            public int InstanceCount;
            public double ReadyAt;
        }

        private readonly Dictionary<string, PlacementJob> jobs =
            new Dictionary<string, PlacementJob>();

        public DimensionDungeonPlacementPassProvider(NullforgeDimensionService service)
        {
            this.service = service;
        }

        public string ProviderId
        {
            get { return DimensionGenerationProviderIds.DungeonPlacement; }
        }

        public void ClearJobs()
        {
            jobs.Clear();
        }

        public bool CanGenerate(DimensionDefinition dimension, DimensionBounds localBounds)
        {
            if (string.IsNullOrEmpty(dimension.Id) ||
                dimension.Id == DimensionIds.Overworld ||
                !dimension.HasCapability(DimensionCapabilityFlags.Generation))
            {
                return false;
            }

            IReadOnlyList<DimensionDungeonDefinition> definitions = DimensionDungeonRegistry.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].DimensionId, dimension.Id, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public DimensionGenerationProviderResult TickGeneration(DimensionGenerationContext context)
        {
            // Single-provider mode: the whole area, which is what an unscoped pass resolves to.
            return Tick(context, context.Area.LocalBounds);
        }

        public DimensionGenerationProviderResult TickGenerationPass(
            DimensionGenerationPassContext context)
        {
            DimensionBounds bounds;
            if (!TryResolvePlacementBounds(context, out bounds))
            {
                return DimensionGenerationProviderResult.Ready(
                    "This step's rectangle does not reach the area being generated.");
            }

            return Tick(context.GenerationContext, bounds);
        }

        /// <summary>
        /// The local rectangle this step may grow dungeons in, or false when it reaches none of
        /// the area being generated.
        /// </summary>
        /// <remarks>
        /// Named and separate so the scoping can be checked without a world: it is the whole
        /// difference between a step scoped to a corner and one that quietly covers the map.
        /// </remarks>
        internal static bool TryResolvePlacementBounds(
            DimensionGenerationPassContext context,
            out DimensionBounds localBounds)
        {
            localBounds = DimensionGenerationPassBounds.Resolve(context);
            return DimensionGenerationPassBounds.HasArea(localBounds);
        }

        private DimensionGenerationProviderResult Tick(
            DimensionGenerationContext context,
            DimensionBounds passBounds)
        {
            if (context.ServerWorld == null || !context.ServerWorld.IsCreated)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating, 0.01f, "Waiting for the server world.");
            }

            // Prototypes first: the assembler is idempotent per world, and its retry contract
            // (false while the world's dungeon tables are absent) becomes ours.
            if (!DimensionDungeonAssembler.TryAssemble(context.ServerWorld))
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating, 0.02f,
                    "Waiting for the world's dungeon tables.");
            }

            // The pass rectangle is part of the key: two steps scoped to different halves of one
            // area are two jobs, and sharing one would let the second find the first's work done
            // and grow nothing.
            string key = context.Dimension.Id + "|" +
                         context.Area.LocalBounds.Min.x + "," + context.Area.LocalBounds.Min.y +
                         "|" + DimensionGenerationPassBounds.Key(passBounds);
            PlacementJob job;
            if (!jobs.TryGetValue(key, out job))
            {
                job = new PlacementJob();
                jobs[key] = job;
            }

            if (!job.Placed)
            {
                job.InstanceCount = PlaceAll(context, passBounds);
                job.Placed = true;
                job.ReadyAt = context.ElapsedSeconds +
                              math.max(1, job.InstanceCount) * SettleSecondsPerDungeon;
                if (job.InstanceCount == 0)
                {
                    jobs.Remove(key);
                    return DimensionGenerationProviderResult.Ready("No dungeons rolled for this area.");
                }
            }

            if (context.ElapsedSeconds < job.ReadyAt)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating,
                    0.5f,
                    "Growing " + job.InstanceCount + " dungeon(s).");
            }

            jobs.Remove(key);
            return DimensionGenerationProviderResult.Ready(
                "Grew " + job.InstanceCount + " dungeon(s).");
        }

        private int PlaceAll(DimensionGenerationContext context, DimensionBounds passBounds)
        {
            int placed = 0;
            List<PlacedSpot> spots = new List<PlacedSpot>();

            ulong seed = DimensionOverlayScatter.Hash(
                0UL, passBounds.Min, StableHash(context.Dimension.Id) ^ StableHash("dungeon-pass"));

            IReadOnlyList<DimensionDungeonDefinition> definitions = DimensionDungeonRegistry.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                DimensionDungeonDefinition definition = definitions[i];
                if (!string.Equals(definition.DimensionId, context.Dimension.Id, System.StringComparison.Ordinal))
                {
                    continue;
                }

                Entity prototype;
                if (!DimensionDungeonAssembler.TryGetPrototype(
                        context.ServerWorld, definition.DungeonId, out prototype) ||
                    !context.ServerWorld.EntityManager.Exists(prototype))
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' has no " +
                        "assembled prototype, so it cannot grow in '" + context.Dimension.Id + "'.");
                    continue;
                }

                int reservation = ReadPlacementRadius(context.ServerWorld, prototype);
                for (int copy = 0; copy < definition.CountPerArea; copy++)
                {
                    int2 local;
                    if (definition.DimensionPlacement == DimensionScenePlacementMode.ExactLocalPosition)
                    {
                        if (copy > 0)
                        {
                            break;
                        }

                        // The author's spot, in the dimension's OWN coordinates — local (0,0)
                        // is the centre players arrive at. Authored positions always win, but
                        // one outside the generated area lands in void and deserves saying so.
                        local = definition.ExactLocalPosition;

                        // A scoped step only owns its own rectangle. A pin outside it belongs
                        // to whichever step covers that ground, so this one leaves it alone —
                        // said out loud, because "my dungeon never appeared" is otherwise a
                        // silent scoping mistake.
                        if (!passBounds.Contains(local) &&
                            context.Area.LocalBounds.Contains(local))
                        {
                            DimensionFrameworkLog.Info(
                                "Dungeon '" + definition.DungeonId + "' is " +
                                "pinned at local " + local + ", outside this step's rectangle (" +
                                passBounds.Min + " to " + passBounds.MaxExclusive +
                                "). Another step has to cover that spot for it to grow.");
                            break;
                        }

                        if (!context.Area.LocalBounds.Contains(local))
                        {
                            DimensionFrameworkLog.Warning(
                                "Dungeon '" + definition.DungeonId + "' is " +
                                "pinned at local " + local + ", outside this generated area (" +
                                context.Area.LocalBounds.Min + " to " +
                                context.Area.LocalBounds.MaxExclusive + "). It will generate " +
                                "into ungenerated void — widen the dimension's generated area " +
                                "or move the pin.");
                        }
                    }
                    else if (!TryFindSpot(
                                 context, passBounds, seed, definition, reservation, spots, copy,
                                 out local))
                    {
                        DimensionFrameworkLog.Warning(
                            "Dungeon '" + definition.DungeonId + "' found no " +
                            "clear spot in '" + context.Dimension.Id + "' (copy " + (copy + 1) +
                            " of " + definition.CountPerArea + "). This step covers " +
                            "local " + passBounds.Min + " to " + passBounds.MaxExclusive +
                            (definition.DimensionPlacement == DimensionScenePlacementMode.RadialBand
                                ? ", and the ring asks for " + definition.MinRadiusTiles + " to " +
                                  (definition.MaxRadiusTiles > 0
                                      ? definition.MaxRadiusTiles.ToString()
                                      : "any") + " tiles from the centre"
                                : string.Empty) +
                            (definition.MinDistanceFromCentre > 0
                                ? ", keeping " + definition.MinDistanceFromCentre +
                                  " tiles clear of local (0, 0)"
                                : string.Empty) +
                            ". A ring this step's rectangle never reaches can never place.");
                        break;
                    }

                    int2 absolute = context.Area.AbsoluteBounds.Min +
                                    (local - context.Area.LocalBounds.Min);

                    // Vanilla's SpawnDungeon, line for line: instantiate the prototype (the
                    // Prefab tag strips, the init tag copies) and position it. The area ref
                    // stays default — the blocked-areas system tolerates Entity.Null with a
                    // standalone fallback.
                    Entity instance = context.ServerWorld.EntityManager.Instantiate(prototype);
                    context.ServerWorld.EntityManager.SetComponentData(
                        instance,
                        LocalTransform.FromPosition(absolute.x, 0f, absolute.y));

                    spots.Add(new PlacedSpot { Local = local, Radius = reservation });
                    placed++;
                }
            }

            return placed;
        }

        internal struct PlacedSpot
        {
            public int2 Local;
            public int Radius;
        }

        private int ReadPlacementRadius(World world, Entity prototype)
        {
            if (world.EntityManager.HasComponent<PugWorldGen.DungeonAreaCD>(prototype))
            {
                return math.max(
                    8,
                    world.EntityManager.GetComponentData<PugWorldGen.DungeonAreaCD>(prototype)
                        .placementRadius);
            }

            return 24;
        }

        /// <summary>
        /// Vanilla's spot ladder over the step's rectangle, with the dungeon's reservation as
        /// clearance.
        /// </summary>
        /// <remarks>
        /// EVERY SAMPLE COMES FROM <paramref name="bounds"/>, not from the whole area — that is
        /// what makes a scoped step real. The margin score is measured from the same rectangle's
        /// edges, so a dungeon asked to sit in a corner keeps its clearance inside the corner
        /// instead of drifting to the middle of the map to score better.
        /// </remarks>
        internal bool TryFindSpot(
            DimensionGenerationContext context,
            DimensionBounds bounds,
            ulong seed,
            DimensionDungeonDefinition definition,
            int reservation,
            List<PlacedSpot> spots,
            int copyIndex,
            out int2 spot)
        {
            spot = default;
            int2 size = bounds.Size;
            if (size.x <= 0 || size.y <= 0)
            {
                return false;
            }

            bool small = math.min(size.x, size.y) < 96;

            for (int pass = small ? 1 : 0; pass < MinClearRadiusPerPass.Length; pass++)
            {
                int wantClear = math.max(MinClearRadiusPerPass[pass], reservation);
                for (int attempt = 0; attempt < AttemptsPerPass[pass]; attempt++)
                {
                    int indexOffset = (int)(seed % 65536) +
                                      StableHash(definition.DungeonId) % 8192 +
                                      copyIndex * 40000 +
                                      attempt * HaltonSamplesPerAttempt + pass * 10000;
                    float bestScore = -1f;
                    int2 best = default;
                    for (int sample = 0; sample < HaltonSamplesPerAttempt; sample++)
                    {
                        int2 candidate = HaltonPoint(indexOffset + sample, bounds);
                        if (!PassesPlacementBands(candidate, definition))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(definition.BiomeId) &&
                            !SpotInBiome(context.Dimension.Id, candidate, definition.BiomeId))
                        {
                            continue;
                        }

                        float score = math.min(
                            math.min(candidate.x - bounds.Min.x, bounds.MaxExclusive.x - 1 - candidate.x),
                            math.min(candidate.y - bounds.Min.y, bounds.MaxExclusive.y - 1 - candidate.y));
                        for (int s = 0; s < spots.Count; s++)
                        {
                            float distance = math.length(new float2(
                                candidate.x - spots[s].Local.x,
                                candidate.y - spots[s].Local.y)) - spots[s].Radius - reservation;
                            score = math.min(score, distance);
                        }

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

        /// <summary>
        /// Whether a LOCAL position satisfies the dungeon's distance rules — every one of them
        /// measured from the dimension's own centre, local (0, 0), the spot players arrive at.
        /// </summary>
        /// <remarks>
        /// Never the world Core. A dimension sits thousands of tiles from the world's origin,
        /// and a distance measured from there would be one enormous unusable number; the whole
        /// point of local coordinates is that the author's "300 tiles out" means 300 tiles
        /// from THEIR centre. The minimum distance applies to every sampled placement; the
        /// ring adds its band on top when the placement mode asks for one.
        /// </remarks>
        internal static bool PassesPlacementBands(
            int2 localPosition,
            DimensionDungeonDefinition definition)
        {
            float distance = math.length(new float2(localPosition.x, localPosition.y));
            if (distance < definition.MinDistanceFromCentre)
            {
                return false;
            }

            if (definition.DimensionPlacement == DimensionScenePlacementMode.RadialBand &&
                (distance < definition.MinRadiusTiles ||
                 (definition.MaxRadiusTiles > 0 && distance >= definition.MaxRadiusTiles)))
            {
                return false;
            }

            return true;
        }

        private bool SpotInBiome(string dimensionId, int2 local, string biomeId)
        {
            DimensionZoneDefinition zone;
            return service != null &&
                   service.TryFindZoneDefinitionAtLocal(
                       dimensionId, new float2(local.x, local.y), out zone) &&
                   string.Equals(zone.Kind, biomeId, System.StringComparison.Ordinal);
        }

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

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619u;
                }

                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
