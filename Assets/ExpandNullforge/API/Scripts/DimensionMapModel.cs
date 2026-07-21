using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// The stacked layer a painted block occupies. Ground is the walkable/floor base (or water,
    /// or a void pit); Wall sits on top of it. Keeping them separate lets a cell carry both a
    /// floor and a wall the way Core Keeper does (mine a wall, the ground under it remains).
    /// </summary>
    public enum DimensionMapLayer
    {
        Ground = 0,
        Wall = 1
    }

    /// <summary>Where a block's tileset (its material/biome skin) comes from.</summary>
    public enum DimensionBlockTilesetSource
    {
        /// <summary>A built-in Core Keeper tileset selected by index.</summary>
        Vanilla = 0,

        /// <summary>A tileset the creator registered through the Dimensions API tileset registry.</summary>
        Custom = 1
    }

    /// <summary>Maps a tile role to the map layer it paints into.</summary>
    public static class DimensionMapLayerRules
    {
        public const int LayerCount = 2;

        /// <summary>
        /// Walls and ore veins are the solid layer; everything else (ground, water, pit, floor
        /// decoration) is the base layer. The map creator paints into these two layers.
        /// </summary>
        public static DimensionMapLayer LayerForRole(DimensionTileRole role)
        {
            switch (role)
            {
                case DimensionTileRole.Wall:
                case DimensionTileRole.Vein:
                    return DimensionMapLayer.Wall;
                default:
                    return DimensionMapLayer.Ground;
            }
        }
    }

    /// <summary>
    /// A positionless, resolved block: the role that becomes a Core Keeper <c>TileType</c> and
    /// the tileset that becomes the material/biome skin. Produced by the authoring map model and
    /// consumed by the generation system, which maps role → TileType and resolves the tileset to
    /// a Core Keeper tileset index. Deliberately free of any Core Keeper type so it lives in the
    /// API contract layer.
    /// </summary>
    public readonly struct DimensionCompiledBlock
    {
        public DimensionCompiledBlock(
            DimensionTileRole role,
            DimensionBlockTilesetSource tilesetSource,
            int vanillaTilesetIndex,
            string customTilesetId)
        {
            Role = role;
            TilesetSource = tilesetSource;
            VanillaTilesetIndex = vanillaTilesetIndex < 0 ? 0 : vanillaTilesetIndex;
            CustomTilesetId = customTilesetId ?? string.Empty;
        }

        public DimensionTileRole Role { get; }

        public DimensionBlockTilesetSource TilesetSource { get; }

        public int VanillaTilesetIndex { get; }

        public string CustomTilesetId { get; }

        /// <summary>The layer this block paints into, derived from its role.</summary>
        public DimensionMapLayer Layer => DimensionMapLayerRules.LayerForRole(Role);

        /// <summary>
        /// True when the block references a custom tileset but names none — a generation-time
        /// error the validator should catch, never a silent fallback to tileset 0.
        /// </summary>
        public bool HasUnresolvedCustomTileset =>
            TilesetSource == DimensionBlockTilesetSource.Custom &&
            string.IsNullOrEmpty(CustomTilesetId);
    }

    /// <summary>One placed tile: a resolved block at a dimension-local tile position.</summary>
    public readonly struct DimensionTilePlacement
    {
        public DimensionTilePlacement(int2 localPosition, DimensionCompiledBlock block)
        {
            LocalPosition = localPosition;
            Block = block;
        }

        public int2 LocalPosition { get; }

        public DimensionCompiledBlock Block { get; }
    }
}
