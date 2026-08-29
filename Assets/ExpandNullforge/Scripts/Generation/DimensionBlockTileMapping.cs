using ExpandNullforge.Api;
using ExpandNullforge.Tilesets;
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
                    // Core Keeper draws the solid cave roof implicitly everywhere; the only
                    // paintable roof-family tile is the opening cut into it (TileType.roof is
                    // obsolete and unused by the engine). A Ceiling-role block therefore paints
                    // a skylight, not a solid overhead tile.
                    return TileType.roofHole;
                case DimensionTileRole.Vein:
                    return TileType.ore;
                case DimensionTileRole.Decoration:
                case DimensionTileRole.Custom:
                default:
                    return TileType.ground;
            }
        }

        /// <summary>
        /// Resolves a block's tileset to a Core Keeper tileset index. Vanilla blocks carry the index
        /// directly; a custom block's index is derived from its tileset NAME. False means the block
        /// names no tileset at all — never a silent fall back to tileset 0, which would generate
        /// dirt where the author asked for something else.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS USED TO ALWAYS FAIL for custom blocks, because nothing could turn a name into an
        /// index. That is no longer true: a custom tileset's id is a pure function of its name
        /// (<see cref="DimensionTilesetRegistry.ComputeTilesetId"/>), so it resolves without the
        /// tileset being installed, or registered, or even existing.
        /// </para>
        /// <para>
        /// WHY IT RESOLVES EVEN WHEN THE TILESET IS NOT INSTALLED. The alternative is refusing to
        /// write the tile, which does not produce "nothing" — it produces a HOLE. Missing ground is
        /// a pit, missing wall is open space, and a dimension generated that way is structurally
        /// wrong in a way that outlives the missing mod. Writing the tile instead keeps the terrain
        /// correct and costs only appearance: an unregistered id renders through the framework's
        /// missing-tileset placeholder, and because the id is derived from the name rather than
        /// handed out, installing the mod later makes every one of those tiles correct with no
        /// migration. The caller still reports the situation; it just does not corrupt the world
        /// over it.
        /// </para>
        /// </remarks>
        public static bool TryResolveTileset(DimensionCompiledBlock block, out int tileset)
        {
            if (block.TilesetSource == DimensionBlockTilesetSource.Vanilla)
            {
                tileset = block.VanillaTilesetIndex;
                return true;
            }

            string name = block.CustomTilesetId;
            if (string.IsNullOrEmpty(name))
            {
                // A block flagged custom that names nothing is an authoring error, and the one case
                // where refusing to write is right: there is no identity to be correct about.
                tileset = 0;
                return false;
            }

            tileset = DimensionTilesetRegistry.ComputeTilesetId(name);
            return true;
        }

        /// <summary>
        /// True when a resolved custom tileset has no registered visuals in this session, so its
        /// tiles will render as the missing-tileset placeholder. Diagnostic only — the tiles are
        /// still correct, and become correct-looking as soon as the owning mod is installed.
        /// </summary>
        public static bool IsCustomTilesetMissing(DimensionCompiledBlock block)
        {
            return block.TilesetSource == DimensionBlockTilesetSource.Custom &&
                   !string.IsNullOrEmpty(block.CustomTilesetId) &&
                   !DimensionTilesetRegistry.TryGetByName(block.CustomTilesetId, out _);
        }
    }
}
