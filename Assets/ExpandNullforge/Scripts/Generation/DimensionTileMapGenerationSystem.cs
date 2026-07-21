using System.Collections.Generic;
using ExpandNullforge.Foundation;
using PugTilemap;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Server system that writes painted tile batches into the world on Core Keeper's own
    /// terrain path — <see cref="TileAccessor.Set"/> in a Burst job, off the main thread. A
    /// generation provider submits a batch of resolved writes; the system schedules the job this
    /// frame and completes it the next (near-free, it has run in between), so no frame carries the
    /// per-tile cost on the main thread.
    ///
    /// Tiles whose submap has not streamed in yet are deferred, so a batch is re-run until every
    /// tile has landed or an attempt cap is reached — this is what lets a biome be written as its
    /// chunks load, the same way vanilla terrain fills in. Re-writing an already-placed tile is
    /// idempotent, so the retry is safe if wasteful; compacting deferred tiles is a later
    /// optimization.
    ///
    /// This is an ECS system whose behaviour can only be verified in-game; the API shapes it uses
    /// are compile-checked against the SDK.
    /// </summary>
    public sealed partial class DimensionTileMapGenerationSystem : PugSimulationSystemBase
    {
        private const int MaxAttempts = 600;

        private sealed class Batch
        {
            public NativeArray<int2> Positions;
            public NativeArray<TileType> TileTypes;
            public NativeArray<int> Tilesets;
            public NativeArray<int> Counters;
            public JobHandle Handle;
            public bool JobInFlight;
            public bool Done;
            public int Attempts;
            public int LastWritten;
            public int LastDeferred;
        }

        private readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();

        /// <summary>
        /// Submits a batch of resolved writes for a generation key (dimension + area). Copies the
        /// writes into persistent native memory the job owns; returns false if there is nothing to
        /// write. Re-submitting a key that is still running is ignored so a polling provider does
        /// not stack duplicate work.
        /// </summary>
        public bool Submit(string key, IReadOnlyList<DimensionResolvedTileWrite> writes)
        {
            if (string.IsNullOrEmpty(key) || writes == null || writes.Count == 0)
            {
                return false;
            }

            if (batches.ContainsKey(key))
            {
                return true;
            }

            int count = writes.Count;
            Batch batch = new Batch
            {
                Positions = new NativeArray<int2>(count, Allocator.Persistent),
                TileTypes = new NativeArray<TileType>(count, Allocator.Persistent),
                Tilesets = new NativeArray<int>(count, Allocator.Persistent),
                Counters = new NativeArray<int>(2, Allocator.Persistent)
            };

            for (int i = 0; i < count; i++)
            {
                DimensionResolvedTileWrite write = writes[i];
                batch.Positions[i] = write.AbsolutePosition;
                batch.TileTypes[i] = write.TileType;
                batch.Tilesets[i] = write.Tileset;
            }

            batches[key] = batch;
            return true;
        }

        /// <summary>
        /// Reports a submitted batch's progress. <paramref name="known"/> is false if the key was
        /// never submitted (or has been collected). <paramref name="done"/> is true once every
        /// tile has landed or the attempt cap was hit.
        /// </summary>
        public bool TryGetStatus(string key, out bool done, out int deferred)
        {
            done = false;
            deferred = 0;
            if (string.IsNullOrEmpty(key) || !batches.TryGetValue(key, out Batch batch))
            {
                return false;
            }

            done = batch.Done;
            deferred = batch.LastDeferred;
            return true;
        }

        /// <summary>Forgets a completed batch. Safe to call once the provider has observed done.</summary>
        public void Release(string key)
        {
            if (!string.IsNullOrEmpty(key) && batches.TryGetValue(key, out Batch batch))
            {
                DisposeBatch(batch);
                batches.Remove(key);
            }
        }

        protected override void OnUpdate()
        {
            if (batches.Count == 0)
            {
                return;
            }

            // A fresh accessor each frame: its component lookups must be current, and it is only
            // valid for jobs scheduled this same frame.
            TileAccessor tileAccessor = CreateTileAccessor(false);

            foreach (KeyValuePair<string, Batch> pair in batches)
            {
                Batch batch = pair.Value;
                if (batch.Done)
                {
                    continue;
                }

                if (batch.JobInFlight)
                {
                    // Scheduled last frame; it has run in between, so Complete is near-free.
                    batch.Handle.Complete();
                    batch.JobInFlight = false;
                    batch.LastWritten = batch.Counters[0];
                    batch.LastDeferred = batch.Counters[1];

                    if (batch.LastDeferred == 0 || batch.Attempts >= MaxAttempts)
                    {
                        batch.Done = true;
                        continue;
                    }
                }

                DimensionTileMapWriteJob job = new DimensionTileMapWriteJob
                {
                    TileAccessor = tileAccessor,
                    Positions = batch.Positions,
                    TileTypes = batch.TileTypes,
                    Tilesets = batch.Tilesets,
                    Counters = batch.Counters
                };

                Dependency = job.Schedule(Dependency);
                batch.Handle = Dependency;
                batch.JobInFlight = true;
                batch.Attempts++;
            }
        }

        protected override void OnDestroy()
        {
            foreach (KeyValuePair<string, Batch> pair in batches)
            {
                DisposeBatch(pair.Value);
            }

            batches.Clear();
            base.OnDestroy();
        }

        private static void DisposeBatch(Batch batch)
        {
            if (batch == null)
            {
                return;
            }

            if (batch.JobInFlight)
            {
                batch.Handle.Complete();
                batch.JobInFlight = false;
            }

            if (batch.Positions.IsCreated)
            {
                batch.Positions.Dispose();
            }

            if (batch.TileTypes.IsCreated)
            {
                batch.TileTypes.Dispose();
            }

            if (batch.Tilesets.IsCreated)
            {
                batch.Tilesets.Dispose();
            }

            if (batch.Counters.IsCreated)
            {
                batch.Counters.Dispose();
            }
        }
    }
}
