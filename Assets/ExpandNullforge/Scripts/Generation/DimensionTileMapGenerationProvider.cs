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
    public sealed class DimensionTileMapGenerationProvider : IDimensionGenerationProvider
    {
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

            string key = BuildKey(context.Dimension.Id, context.Area.LocalBounds);

            if (!jobs.TryGetValue(key, out PaintJob job))
            {
                DimensionTileMapCompileResult compiled = DimensionTileMapCompiler.Compile(
                    map.EnumeratePlacements(),
                    context.Area.LocalBounds,
                    context.Area.AbsoluteBounds);

                for (int i = 0; i < compiled.Skipped.Count; i++)
                {
                    DimensionFrameworkLog.Warning("[ExpandNullforge] " + compiled.Skipped[i]);
                }

                if (compiled.WriteCount == 0)
                {
                    // Nothing to write for this area (all outside it, or all unresolved). Done.
                    return DimensionGenerationProviderResult.Ready(
                        "No tiles to generate for this area.");
                }

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
