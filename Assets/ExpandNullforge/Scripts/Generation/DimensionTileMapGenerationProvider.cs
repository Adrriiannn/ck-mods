using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Generates a dimension from its painted tile map. It paints through Core Keeper's
    /// <see cref="EntityUtility.AddTile"/> + <see cref="TileUpdateBuffer"/> path — the same one the
    /// safe-platform provider uses — because that path <em>requests the target chunk</em> when it is
    /// not loaded yet, which a fresh dimension area always is. (An earlier Burst
    /// <c>TileAccessor.Set</c> fast-path could only write into already-loaded chunks, so every tile
    /// of a just-created area deferred forever and generation never completed.) Writes are throttled
    /// per tick so a full-scale biome does not spike the frame, then a short settle delay lets the
    /// tile-update commands flush before the area is reported Ready.
    ///
    /// The map comes from <see cref="DimensionTileMapRegistry"/>, populated when the dimension's
    /// content loads. A dimension with no registered map is not this provider's job.
    /// </summary>
    public sealed class DimensionTileMapGenerationProvider :
        IDimensionGenerationProvider,
        IDimensionGenerationPassProvider
    {
        /// <summary>
        /// The pass-ladder face of the same paint. Without it, any generation plan naming a
        /// terrain pass silently dropped this provider and ran the later passes over bare
        /// void — a dimension that is empty except its one placed ruin, with nothing logged.
        /// </summary>
        /// <remarks>
        /// A scoped step paints only its own rectangle. Both bounds are clipped together and
        /// handed on as a pair, because the compiler maps local to world by the distance between
        /// the two corners it is given — clipping only the local half would shift every painted
        /// tile by the width of the clip.
        /// </remarks>
        public DimensionGenerationProviderResult TickGenerationPass(
            DimensionGenerationPassContext context)
        {
            DimensionBounds local;
            DimensionBounds absolute;
            if (!TryResolvePaintBounds(context, out local, out absolute))
            {
                return DimensionGenerationProviderResult.Ready(
                    "This step's rectangle does not reach the area being generated.");
            }

            return TickPaint(context.GenerationContext, local, absolute);
        }

        /// <summary>
        /// The rectangle this step paints, in both coordinate spaces, or false when the step
        /// reaches none of the area being generated.
        /// </summary>
        /// <remarks>
        /// The pair travels together because the compiler maps local to world by the distance
        /// between the two corners it is handed — clipping only the local half would shift every
        /// painted tile by the width of the clip.
        /// </remarks>
        internal static bool TryResolvePaintBounds(
            DimensionGenerationPassContext context,
            out DimensionBounds localBounds,
            out DimensionBounds absoluteBounds)
        {
            localBounds = DimensionGenerationPassBounds.Resolve(context);
            if (!DimensionGenerationPassBounds.HasArea(localBounds))
            {
                absoluteBounds = default(DimensionBounds);
                return false;
            }

            absoluteBounds =
                DimensionGenerationPassBounds.ToAbsolute(context.GenerationContext.Area, localBounds);
            return true;
        }

        private const int MaxTilesPerTick = 384;
        private const int ReadyDelayFrames = 2;

        private readonly Dictionary<string, PaintJob> jobs = new Dictionary<string, PaintJob>();

        private World cachedTileUpdateWorld;
        private Entity cachedTileUpdateEntity;

        public string ProviderId => DimensionGenerationProviderIds.TileMap;

        public bool CanGenerate(DimensionDefinition dimension, DimensionBounds localBounds)
        {
            if (string.IsNullOrEmpty(dimension.Id) ||
                dimension.Id == DimensionIds.Overworld ||
                !dimension.HasCapability(DimensionCapabilityFlags.Generation))
            {
                return false;
            }

            // Only when a painted map exists for this dimension.
            return DimensionTileMapRegistry.Has(dimension.Id);
        }

        public DimensionGenerationProviderResult TickGeneration(DimensionGenerationContext context)
        {
            // Single-provider mode: the whole area, which is what an unscoped step resolves to.
            return TickPaint(context, context.Area.LocalBounds, context.Area.AbsoluteBounds);
        }

        private DimensionGenerationProviderResult TickPaint(
            DimensionGenerationContext context,
            DimensionBounds localBounds,
            DimensionBounds absoluteBounds)
        {
            if (context.ServerWorld == null || !context.ServerWorld.IsCreated)
            {
                return DimensionGenerationProviderResult.Failed(
                    "The server world is not available for tile-map generation.");
            }

            if (!DimensionTileMapRegistry.TryGet(context.Dimension.Id, out DimensionTileMapModel map) ||
                map == null)
            {
                return DimensionGenerationProviderResult.Failed(
                    "No tile map is registered for dimension '" + context.Dimension.Id + "'.");
            }

            // The painted rectangle is the key, not the area's: two steps scoped to different
            // halves of one area are two jobs.
            string key = BuildKey(context.Dimension.Id, localBounds);

            if (!jobs.TryGetValue(key, out PaintJob job))
            {
                DimensionTileMapCompileResult compiled = DimensionTileMapCompiler.Compile(
                    map.EnumeratePlacements(),
                    localBounds,
                    absoluteBounds);

                for (int i = 0; i < compiled.Skipped.Count; i++)
                {
                    DimensionFrameworkLog.Warning(compiled.Skipped[i]);
                }

                if (compiled.WriteCount == 0)
                {
                    // Nothing painted inside this rectangle (all outside it, or all unresolved).
                    return DimensionGenerationProviderResult.Ready(
                        "No tiles to generate for this area.");
                }

                AppendScatteredOverlays(context.Dimension.Id, compiled.Writes);

                // Veins after decoration and after the full base list exists: the append rides
                // the same in-order paint, so every vein cell's wall is queued before its ore —
                // the order the game's server demands, on pain of minted loose ore items.
                Unity.Mathematics.int2 localShift = context.Area.LocalBounds.Min - context.Area.AbsoluteBounds.Min;
                string veinDimensionId = context.Dimension.Id;
                DimensionOreVeinScatter.AppendVeins(
                    veinDimensionId,
                    compiled.Writes,
                    (absolutePosition, oreItemId) => DimensionOreBiomeGate.Allows(
                        veinDimensionId,
                        absolutePosition + localShift,
                        oreItemId));

                job = new PaintJob(compiled.Writes);
                jobs[key] = job;
            }

            if (job.WaitingForTileUpdate)
            {
                if (Time.frameCount < job.ReadyAfterFrame)
                {
                    return DimensionGenerationProviderResult.Progress(
                        DimensionGenerationState.GeneratingTerrain, 0.99f, "Tile-map tiles queued.");
                }

                jobs.Remove(key);
                return DimensionGenerationProviderResult.Ready("Tile-map terrain generated.");
            }

            if (!TryGetTileUpdateBuffer(context.ServerWorld, out DynamicBuffer<TileUpdateBuffer> tileUpdates))
            {
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.GeneratingTerrain, job.Progress01,
                    "Waiting for the tile update buffer.");
            }

            int painted = 0;
            while (job.NextIndex < job.Writes.Count && painted < MaxTilesPerTick)
            {
                DimensionResolvedTileWrite write = job.Writes[job.NextIndex];
                EntityUtility.AddTile(
                    write.Tileset,
                    write.TileType,
                    write.AbsolutePosition,
                    true,
                    tileUpdates);
                job.NextIndex++;
                painted++;
            }

            if (job.NextIndex >= job.Writes.Count)
            {
                // All tiles queued; give the tile-update commands a couple of frames to flush before
                // the area is reported Ready so travel does not land on half-written terrain.
                job.WaitingForTileUpdate = true;
                job.ReadyAfterFrame = Time.frameCount + ReadyDelayFrames;
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.GeneratingTerrain, 0.99f, "Tile-map tiles queued.");
            }

            return DimensionGenerationProviderResult.Progress(
                DimensionGenerationState.GeneratingTerrain, job.Progress01, "Writing tile-map terrain.");
        }

        /// <summary>Drops any tracked jobs, e.g. when the framework tears down generation state.</summary>
        public void ClearJobs()
        {
            jobs.Clear();
            cachedTileUpdateWorld = null;
            cachedTileUpdateEntity = Entity.Null;
        }

        private bool TryGetTileUpdateBuffer(
            World serverWorld,
            out DynamicBuffer<TileUpdateBuffer> tileUpdates)
        {
            if (serverWorld == null || !serverWorld.IsCreated)
            {
                tileUpdates = default(DynamicBuffer<TileUpdateBuffer>);
                return false;
            }

            EntityManager entityManager = serverWorld.EntityManager;
            if (cachedTileUpdateWorld == serverWorld &&
                cachedTileUpdateEntity != Entity.Null &&
                entityManager.Exists(cachedTileUpdateEntity) &&
                entityManager.HasBuffer<TileUpdateBuffer>(cachedTileUpdateEntity))
            {
                tileUpdates = entityManager.GetBuffer<TileUpdateBuffer>(cachedTileUpdateEntity);
                return true;
            }

            cachedTileUpdateWorld = serverWorld;
            cachedTileUpdateEntity = Entity.Null;
            using (EntityQuery query =
                entityManager.CreateEntityQuery(ComponentType.ReadWrite<TileUpdateBuffer>()))
            {
                if (query.IsEmpty)
                {
                    Entity entity = entityManager.CreateEntity();
                    cachedTileUpdateEntity = entity;
                    tileUpdates = entityManager.AddBuffer<TileUpdateBuffer>(entity);
                    return true;
                }

                using (NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp))
                {
                    if (entities.Length == 0)
                    {
                        tileUpdates = default(DynamicBuffer<TileUpdateBuffer>);
                        return false;
                    }

                    cachedTileUpdateEntity = entities[0];
                    tileUpdates = entityManager.GetBuffer<TileUpdateBuffer>(cachedTileUpdateEntity);
                    return true;
                }
            }
        }

        private static string BuildKey(string dimensionId, DimensionBounds bounds)
        {
            return (dimensionId ?? string.Empty) +
                   "|" + bounds.Min.x + "," + bounds.Min.y +
                   "|" + bounds.MaxExclusive.x + "," + bounds.MaxExclusive.y;
        }

        /// <summary>
        /// Grows each painted ground tile's own decoration on top of it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Runs once, on the compiled write list, before any painting starts. Doing it here rather
        /// than inside the paint loop means the throttling, the progress figure and the settle delay
        /// all account for the overlays without knowing they exist.
        /// </para>
        /// <para>
        /// Only ground is decorated. Grass on a wall would be nonsense, and the overlay layers are
        /// ground-family in the engine anyway — writing one over a wall produces a tile the renderer
        /// has nowhere to draw. A painted cell can carry a ground AND a wall, and the wall is
        /// written earlier in this same list, so testing the write's own type is not enough: the
        /// cells that took a wall anywhere in this compile are collected first and skipped.
        /// </para>
        /// <para>
        /// The scatter is seeded from the DIMENSION, not the save. An authored dimension's decoration
        /// is part of its design — the author who paints a mossy cavern should get the same cavern in
        /// every world, and every player in a multiplayer world must see the same one.
        /// </para>
        /// </remarks>
        /// <remarks>
        /// Internal rather than private so a test can drive the rule that a cell carrying a wall
        /// grows nothing, instead of grepping for the call.
        /// </remarks>
        internal static void AppendScatteredOverlays(string dimensionId, List<DimensionResolvedTileWrite> writes)
        {
            if (!DimensionOverlayRuleRegistry.Any || writes == null || writes.Count == 0)
            {
                return;
            }

            ulong seed = DimensionOverlayScatter.Hash(
                0UL, default, DimensionOverlayScatter.StableHash(dimensionId));

            // Snapshot the count: the loop appends to the same list, and re-reading Count would walk
            // over the overlays it just added and decorate the decoration.
            int groundCount = writes.Count;

            HashSet<Unity.Mathematics.int2> walled = new HashSet<Unity.Mathematics.int2>();
            for (int i = 0; i < groundCount; i++)
            {
                if (writes[i].TileType == TileType.wall)
                {
                    walled.Add(writes[i].AbsolutePosition);
                }
            }

            List<DimensionOverlayRule> chosen = new List<DimensionOverlayRule>();

            for (int i = 0; i < groundCount; i++)
            {
                DimensionResolvedTileWrite write = writes[i];
                if (write.TileType != TileType.ground || walled.Contains(write.AbsolutePosition))
                {
                    continue;
                }

                IReadOnlyList<DimensionOverlayRule> rules = DimensionOverlayRuleRegistry.For(write.Tileset);
                if (rules.Count == 0)
                {
                    continue;
                }

                chosen.Clear();
                DimensionOverlayScatter.Collect(seed, write.AbsolutePosition, rules, chosen);
                for (int r = 0; r < chosen.Count; r++)
                {
                    writes.Add(new DimensionResolvedTileWrite(
                        write.AbsolutePosition,
                        chosen[r].TileType,
                        write.Tileset));
                }
            }
        }

        private sealed class PaintJob
        {
            public readonly List<DimensionResolvedTileWrite> Writes;
            public int NextIndex;
            public bool WaitingForTileUpdate;
            public int ReadyAfterFrame;

            public PaintJob(List<DimensionResolvedTileWrite> writes)
            {
                Writes = writes ?? new List<DimensionResolvedTileWrite>();
            }

            public float Progress01
            {
                get
                {
                    return Writes.Count <= 0
                        ? 0f
                        : Mathf.Clamp((float)NextIndex / Writes.Count, 0f, 0.98f);
                }
            }
        }
    }
}
