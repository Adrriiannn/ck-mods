using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Generation;
using ExpandNullforge.Tilesets;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.Scenes
{
    /// <summary>One authored scene tile, already reduced to the two things that matter.</summary>
    public readonly struct DimensionSceneTileRequest
    {
        public DimensionSceneTileRequest(int2 localPosition, string blockId, DimensionTileRole role)
        {
            LocalPosition = localPosition;
            BlockId = blockId;
            Role = role;
        }

        public readonly int2 LocalPosition;
        public readonly string BlockId;
        public readonly DimensionTileRole Role;
    }

    /// <summary>What compiling a scene's tiles produced.</summary>
    public sealed class DimensionSceneTileCompileResult
    {
        public readonly List<DimensionSceneTile> Tiles = new List<DimensionSceneTile>();

        /// <summary>Tiles that could not be produced, each with a reason. Never dropped silently.</summary>
        public readonly List<string> Skipped = new List<string>();
    }

    /// <summary>
    /// Turns a scene's authored tiles into the concrete (tileset, tile type, position) triples the
    /// scene blob holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure logic — no ECS, no world, no asset database — so the parts that are easy to get quietly
    /// wrong (tileset resolution, duplicate cells, the roof-hole special case) are testable offline,
    /// and the runtime only has to hand over the result.
    /// </para>
    /// <para>
    /// A tileset resolves from the block's NAME, the same way terrain does, so a scene and the terrain
    /// around it cannot disagree about what <c>MyMod:obsidian</c> is.
    /// </para>
    /// </remarks>
    public static class DimensionSceneTileCompiler
    {
        /// <summary>
        /// Core Keeper's own limit on how large one scene may be, in tiles per side.
        /// </summary>
        /// <remarks>
        /// Enforced here rather than discovered later: a scene that exceeds it is not rejected by the
        /// engine with a message, it simply misbehaves at placement time.
        /// </remarks>
        public const int MaxSceneSize = 64;

        public static DimensionSceneTileCompileResult Compile(
            string sceneName,
            IEnumerable<DimensionSceneTileRequest> requests)
        {
            DimensionSceneTileCompileResult result = new DimensionSceneTileCompileResult();
            if (requests == null)
            {
                return result;
            }

            // A cell may legitimately carry more than one tile — ground under a wall is the normal
            // case — so the key is the cell AND the tile type, not the cell alone. Two tiles of the
            // same type in one cell is an authoring mistake: whichever is written second wins, and
            // which one that is depends on list order, so it is caught rather than silently resolved.
            HashSet<long> seen = new HashSet<long>();

            int2 min = new int2(int.MaxValue, int.MaxValue);
            int2 max = new int2(int.MinValue, int.MinValue);

            foreach (DimensionSceneTileRequest request in requests)
            {
                if (string.IsNullOrEmpty(request.BlockId))
                {
                    result.Skipped.Add(
                        "A tile at " + request.LocalPosition + " names no block, so there is nothing to place.");
                    continue;
                }

                int tileset;
                if (!TryResolveTileset(request.BlockId, out tileset))
                {
                    result.Skipped.Add(
                        "Tile at " + request.LocalPosition + " uses block '" + request.BlockId +
                        "', whose tileset could not be resolved.");
                    continue;
                }

                TileType tileType = DimensionBlockTileMapping.ToTileType(request.Role);
                long key = ((long)request.LocalPosition.x << 40) ^
                           ((long)request.LocalPosition.y << 16) ^
                           (long)tileType;
                if (!seen.Add(key))
                {
                    result.Skipped.Add(
                        "Two " + tileType + " tiles are painted at " + request.LocalPosition +
                        "; only one can exist there, and which one would win depends on ordering.");
                    continue;
                }

                min = math.min(min, request.LocalPosition);
                max = math.max(max, request.LocalPosition);
                result.Tiles.Add(new DimensionSceneTile(request.LocalPosition, tileset, tileType));
            }

            if (result.Tiles.Count > 0)
            {
                int width = max.x - min.x + 1;
                int height = max.y - min.y + 1;
                if (width > MaxSceneSize || height > MaxSceneSize)
                {
                    result.Skipped.Add(
                        "Scene '" + sceneName + "' spans " + width + "x" + height + " tiles, over Core " +
                        "Keeper's " + MaxSceneSize + "x" + MaxSceneSize + " limit for a single scene. " +
                        "Split it into several smaller scenes.");
                    result.Tiles.Clear();
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a block name to a tileset id: a plain number for a vanilla tileset, otherwise the
        /// id the custom tileset's name derives to.
        /// </summary>
        private static bool TryResolveTileset(string blockId, out int tileset)
        {
            int vanilla;
            if (int.TryParse(blockId, out vanilla) && vanilla >= 0)
            {
                tileset = vanilla;
                return true;
            }

            tileset = DimensionTilesetRegistry.ComputeTilesetId(blockId);
            return true;
        }
    }
}
