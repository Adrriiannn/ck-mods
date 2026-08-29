using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// The Ore-phase pass for dimensions whose terrain is NOT a painted map: reads the
    /// generated walls back and grows the same veins the painted path appends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PAINTED DIMENSIONS ARE REFUSED, twice over. Their veins already ride the tile-map
    /// provider's write list — accepting them here would double every vein. Worse, the
    /// planned-pass builder drops providers that cannot run passes, so an Ore pass accepting
    /// a painted dimension could become the plan's ONLY pass and skip the terrain entirely.
    /// </para>
    /// <para>
    /// THE READ-BACK GATE IS MANDATORY. This pass can start the same service tick the terrain
    /// pass finished, before the tile flush lands; reading then sees "no walls" and veins
    /// silently never spawn. The pass reports progress until the accessor says the area is
    /// initialized, and only then walks it.
    /// </para>
    /// <para>
    /// A SCOPED STEP READS ONLY ITS OWN RECTANGLE. The scan used to walk the whole area whatever
    /// the step said, so an author who scoped ore to one cavern grew veins in every wall of the
    /// dimension — and, because veins ARE the visible result, saw nothing that looked like a
    /// mistake.
    /// </para>
    /// </remarks>
    public sealed class DimensionOreScatterPassProvider :
        IDimensionGenerationProvider,
        IDimensionGenerationPassProvider
    {
        private const int MaxTilesPerTick = 384;
        private const int ReadyDelayFrames = 2;

        private sealed class ScatterJob
        {
            public List<DimensionResolvedTileWrite> OreWrites;
            public int NextIndex;
            public bool WaitingForSettle;
            public int ReadyAfterFrame;
        }

        private readonly Dictionary<string, ScatterJob> jobs =
            new Dictionary<string, ScatterJob>();

        public string ProviderId
        {
            get { return DimensionGenerationProviderIds.OreScatter; }
        }

        public void ClearJobs()
        {
            jobs.Clear();
        }

        public bool CanGenerate(DimensionDefinition dimension, DimensionBounds localBounds)
        {
            return !string.IsNullOrEmpty(dimension.Id) &&
                   dimension.Id != DimensionIds.Overworld &&
                   dimension.HasCapability(DimensionCapabilityFlags.Generation) &&
                   DimensionOreVeinRuleRegistry.Any &&
                   !DimensionTileMapRegistry.Has(dimension.Id);
        }

        public DimensionGenerationProviderResult TickGeneration(DimensionGenerationContext context)
        {
            return Tick(context, context.Area.AbsoluteBounds, true);
        }

        public DimensionGenerationProviderResult TickGenerationPass(
            DimensionGenerationPassContext context)
        {
            DimensionBounds scanBounds;
            if (!TryResolveScanBounds(context, out scanBounds))
            {
                return DimensionGenerationProviderResult.Ready(
                    "This step's rectangle does not reach the area being generated.");
            }

            return Tick(
                context.GenerationContext,
                scanBounds,
                context.PassIndex + 1 >= context.PassCount);
        }

        /// <summary>
        /// The WORLD rectangle this step reads walls out of, or false when the step reaches none
        /// of the area being generated.
        /// </summary>
        /// <remarks>
        /// Walls are read back in world coordinates, so the step's authored rectangle has to make
        /// the same trip the tiles did. Scanning the whole area and calling it scoped is how a
        /// step meant for one cavern grew veins across an entire dimension.
        /// </remarks>
        internal static bool TryResolveScanBounds(
            DimensionGenerationPassContext context,
            out DimensionBounds absoluteBounds)
        {
            DimensionBounds local = DimensionGenerationPassBounds.Resolve(context);
            if (!DimensionGenerationPassBounds.HasArea(local))
            {
                absoluteBounds = default(DimensionBounds);
                return false;
            }

            absoluteBounds =
                DimensionGenerationPassBounds.ToAbsolute(context.GenerationContext.Area, local);
            return true;
        }

        private DimensionGenerationProviderResult Tick(
            DimensionGenerationContext context,
            DimensionBounds scanBounds,
            bool waitForSettle)
        {
            if (context.ServerWorld == null || !context.ServerWorld.IsCreated)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating, 0.01f,
                    "Waiting for the server world.");
            }

            // The scanned rectangle is part of the key: two steps scoped to different halves of
            // one area are two jobs, and sharing one would leave the second with nothing to do.
            string key = context.Dimension.Id + "|" +
                         context.Area.LocalBounds.Min.x + "," + context.Area.LocalBounds.Min.y +
                         "|" + DimensionGenerationPassBounds.Key(scanBounds);

            ScatterJob job;
            if (!jobs.TryGetValue(key, out job))
            {
                List<DimensionResolvedTileWrite> writes;
                DimensionGenerationProviderResult scan =
                    TryScanWalls(context, scanBounds, out writes);
                if (writes == null)
                {
                    return scan;
                }

                int baseCount = writes.Count;
                if (baseCount == 0)
                {
                    return DimensionGenerationProviderResult.Ready("No walls to grow veins in.");
                }

                int2 localShift = context.Area.LocalBounds.Min - context.Area.AbsoluteBounds.Min;
                string dimensionId = context.Dimension.Id;
                DimensionOreVeinScatter.AppendVeins(
                    dimensionId,
                    writes,
                    (absolutePosition, oreItemId) => DimensionOreBiomeGate.Allows(
                        dimensionId,
                        absolutePosition + localShift,
                        oreItemId));

                job = new ScatterJob
                {
                    OreWrites = writes.GetRange(baseCount, writes.Count - baseCount),
                    NextIndex = 0
                };
                jobs[key] = job;

                if (job.OreWrites.Count == 0)
                {
                    jobs.Remove(key);
                    return DimensionGenerationProviderResult.Ready("No veins rolled for this area.");
                }
            }

            if (job.WaitingForSettle)
            {
                if (UnityEngine.Time.frameCount < job.ReadyAfterFrame)
                {
                    return DimensionGenerationProviderResult.Progress(
                        DimensionGenerationState.Populating, 0.99f, "Vein tiles queued.");
                }

                jobs.Remove(key);
                return DimensionGenerationProviderResult.Ready(
                    "Grew " + job.OreWrites.Count + " vein tile(s).");
            }

            EntityQuery bufferQuery = context.ServerWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<TileUpdateBuffer>());
            if (bufferQuery.IsEmptyIgnoreFilter)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating, 0.05f,
                    "Waiting for the tile update buffer.");
            }

            DynamicBuffer<TileUpdateBuffer> tileUpdates =
                context.ServerWorld.EntityManager.GetBuffer<TileUpdateBuffer>(
                    bufferQuery.GetSingletonEntity());

            int written = 0;
            while (job.NextIndex < job.OreWrites.Count && written < MaxTilesPerTick)
            {
                DimensionResolvedTileWrite write = job.OreWrites[job.NextIndex];
                EntityUtility.AddTile(
                    write.Tileset,
                    write.TileType,
                    write.AbsolutePosition,
                    true,
                    tileUpdates);
                job.NextIndex++;
                written++;
            }

            if (job.NextIndex < job.OreWrites.Count)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating,
                    0.1f + 0.8f * (job.NextIndex / (float)job.OreWrites.Count),
                    "Growing veins: " + job.NextIndex + " of " + job.OreWrites.Count + " tiles.");
            }

            if (waitForSettle)
            {
                job.WaitingForSettle = true;
                job.ReadyAfterFrame = UnityEngine.Time.frameCount + ReadyDelayFrames;
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating, 0.99f, "Vein tiles queued.");
            }

            jobs.Remove(key);
            return DimensionGenerationProviderResult.Ready(
                "Grew " + job.OreWrites.Count + " vein tile(s).");
        }

        /// <summary>
        /// Reads every wall in the given world rectangle into a pseudo write list, or reports
        /// why not yet.
        /// </summary>
        private static DimensionGenerationProviderResult TryScanWalls(
            DimensionGenerationContext context,
            DimensionBounds bounds,
            out List<DimensionResolvedTileWrite> writes)
        {
            writes = null;
            PugQuerySystem querySystem =
                context.ServerWorld.GetExistingSystemManaged<PugQuerySystem>();
            if (querySystem == null)
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating, 0.02f,
                    "Waiting for the world's tile data.");
            }

            TileAccessor tiles = new TileAccessor(querySystem);

            // The gate: an uninitialized corner means the terrain flush has not landed. Probe
            // the corners and centre rather than every cell — initialization is per submap.
            int2 centre = (bounds.Min + bounds.MaxExclusive) / 2;
            if (!tiles.IsInitialized(bounds.Min) ||
                !tiles.IsInitialized(bounds.MaxExclusive - new int2(1, 1)) ||
                !tiles.IsInitialized(centre))
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.Populating, 0.03f,
                    "Waiting for the terrain to land.");
            }

            List<DimensionResolvedTileWrite> found = new List<DimensionResolvedTileWrite>();
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            {
                for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                {
                    int2 position = new int2(x, y);
                    if (!tiles.IsInitialized(position))
                    {
                        continue;
                    }

                    NativeArray<TileCD> layers = tiles.Get(position, Allocator.Temp);
                    for (int t = 0; t < layers.Length; t++)
                    {
                        if (layers[t].tileType == TileType.wall)
                        {
                            found.Add(new DimensionResolvedTileWrite(
                                position, TileType.wall, layers[t].tileset));
                        }
                        else if (layers[t].tileType == TileType.ore ||
                                 layers[t].tileType == TileType.ancientCrystal)
                        {
                            found.Add(new DimensionResolvedTileWrite(
                                position, layers[t].tileType, layers[t].tileset));
                        }
                    }

                    layers.Dispose();
                }
            }

            writes = found;
            return DimensionGenerationProviderResult.Progress(
                DimensionGenerationState.Populating, 0.05f, "Walls scanned.");
        }
    }
}
