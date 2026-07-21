using ExpandNullforge.Api;
using PugTilemap;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Bridges a painted block to the Core Keeper tile it becomes: role → <see cref="TileType"/>
    /// and tileset source → a Core Keeper tileset index. This is the one place that knows Core
    /// Keeper's tile vocabulary, so the map model and dashboard stay engine-free. Every
    /// <see cref="TileType"/> named here is checked by the compiler against the shipped enum —
    /// a wrong name fails the build rather than producing a silently wrong tile in-game.
    /// </summary>
    public static class DimensionBlockTileMapping
    {
        /// <summary>
        /// The Core Keeper <see cref="TileType"/> a role generates. Roles the tile map does not
        /// model as a distinct structural tile (Decoration, Custom) fall back to ground, which is
        /// the safe base layer; genuinely custom structural tiles are a later concern.
        /// </summary>
        public static TileType ToTileType(DimensionTileRole role)
        {
            switch (role)
            {
                case DimensionTileRole.Ground:
                    return TileType.ground;
                case DimensionTileRole.Wall:
                    return TileType.wall;
                case DimensionTileRole.Pit:
                    return TileType.pit;
                case DimensionTileRole.Liquid:
                    return TileType.water;
                case DimensionTileRole.Ceiling:
                    return TileType.roof;
                case DimensionTileRole.Vein:
                    return TileType.ore;
                case DimensionTileRole.Decoration:
                case DimensionTileRole.Custom:
                default:
                    return TileType.ground;
            }
        }

        /// <summary>
        /// Resolves a block's tileset to a Core Keeper tileset index. Vanilla blocks carry the
        /// index directly. Custom blocks require a tileset provider that does not ship yet, so
        /// they resolve to false rather than defaulting to tileset 0 — the caller must report the
        /// gap instead of generating the wrong material.
        /// </summary>
        public static bool TryResolveTileset(DimensionCompiledBlock block, out int tileset)
        {
            if (block.TilesetSource == DimensionBlockTilesetSource.Vanilla)
            {
                tileset = block.VanillaTilesetIndex;
                return true;
            }

            tileset = 0;
            return false;
        }
    }
}
