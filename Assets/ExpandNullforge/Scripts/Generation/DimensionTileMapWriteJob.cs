using PugTilemap;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Writes resolved tiles into the world with <see cref="TileAccessor.Set"/> — the same path
    /// Core Keeper's own worldgen uses, run off the main thread so a full-scale biome does not
    /// spike the frame. One entry per tile.
    ///
    /// A tile whose submap has not been initialized yet is <em>deferred</em>, not written: the
    /// count of deferred tiles is reported back so the caller can retry those positions on a
    /// later frame once the chunk streams in, rather than silently dropping them or writing into
    /// a non-existent submap.
    ///
    /// The API shapes here (<c>TileAccessor</c> as a job field, <c>Set</c>, <c>IsInitialized</c>,
    /// <c>TileCD</c>) are compile-verified against the shipped SDK.
    /// </summary>
    [BurstCompile]
    public struct DimensionTileMapWriteJob : IJob
    {
        public TileAccessor TileAccessor;

        [ReadOnly] public NativeArray<int2> Positions;
        [ReadOnly] public NativeArray<TileType> TileTypes;
        [ReadOnly] public NativeArray<int> Tilesets;

        /// <summary>Two counters: [0] = tiles written, [1] = tiles deferred (submap not ready).</summary>
        public NativeArray<int> Counters;

        public void Execute()
        {
            int written = 0;
            int deferred = 0;

            for (int i = 0; i < Positions.Length; i++)
            {
                int2 position = Positions[i];
                if (!TileAccessor.IsInitialized(position))
                {
                    deferred++;
                    continue;
                }

                TileAccessor.Set(
                    position,
                    new TileCD { tileType = TileTypes[i], tileset = Tilesets[i] });
                written++;
            }

            if (Counters.Length > 0)
            {
                Counters[0] = written;
            }

            if (Counters.Length > 1)
            {
                Counters[1] = deferred;
            }
        }
    }
}
