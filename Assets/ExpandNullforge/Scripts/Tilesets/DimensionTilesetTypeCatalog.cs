using System;
using System.Collections.Generic;
using PugTilemap;

namespace ExpandNullforge.Tilesets
{
    /// <summary>How a block type is grouped in the wizard's type picker.</summary>
    public enum DimensionBlockRole
    {
        /// <summary>The standard placeable material: ground + wall, with the full state set.</summary>
        Terrain,
        /// <summary>Player-built surfaces and structures (floor, rug, bridge, fence, rail…).</summary>
        Built,
        /// <summary>Liquid / chasm base tiles (water, pit).</summary>
        Liquid,
        /// <summary>Wiring and special plates.</summary>
        Special,
        /// <summary>
        /// World overlays (big roots, chrysalis). Not standalone blocks — their texture on a tile
        /// always comes from that tile's own tileset, so they are authored as a Terrain state or
        /// used to reskin a vanilla tileset. The wizard routes them accordingly.
        /// </summary>
        Overlay
    }

    /// <summary>How this block's tileset reaches the game: through its own item, or by reskinning vanilla.</summary>
    public enum DimensionTilesetItemMode
    {
        /// <summary>Generate the block's own placeable inventory item (types that support one).</summary>
        CreateItem = 0,
        /// <summary>No item — the tileset exists for worldgen/scene use only.</summary>
        None = 1,
        /// <summary>
        /// Reskin a vanilla tileset: while this block is enabled, the chosen vanilla tileset index
        /// renders with this tileset's textures instead (render-only, global, save-safe).
        /// </summary>
        ReskinVanilla = 2
    }

    /// <summary>
    /// One primary block type a modder can create — verified against the engine's layer taxonomy
    /// (17 primaries; only ground/wall carry states; fronts/shadows/lights are auto companions).
    /// </summary>
    public sealed class DimensionTilesetType
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly DimensionBlockRole Role;
        /// <summary>The layers the modder actually authors for this type (companions excluded).</summary>
        public readonly LayerName[] AuthoredLayers;
        /// <summary>True only for Terrain — the sole type with a state picker.</summary>
        public readonly bool HasStates;
        /// <summary>Whether the framework can generate a placeable inventory item for this type yet.</summary>
        public readonly bool SupportsItem;
        public readonly string Blurb;

        public DimensionTilesetType(
            string key,
            string displayName,
            DimensionBlockRole role,
            LayerName[] authoredLayers,
            bool hasStates,
            bool supportsItem,
            string blurb)
        {
            Key = key;
            DisplayName = displayName;
            Role = role;
            AuthoredLayers = authoredLayers ?? Array.Empty<LayerName>();
            HasStates = hasStates;
            SupportsItem = supportsItem;
            Blurb = blurb;
        }
    }

    /// <summary>
    /// The complete set of primary block types (from the decompiled game's own layer graph). The
    /// wizard presents these grouped by role; the chosen type decides which layers are authored,
    /// whether the state picker appears, and whether an inventory item can be generated.
    /// </summary>
    public static class DimensionTilesetTypeCatalog
    {
        public static readonly IReadOnlyList<DimensionTilesetType> All = new List<DimensionTilesetType>
        {
            // ---- The terrain block: the only type with states, and the only item-generating one (v1).
            new DimensionTilesetType("terrain", "Terrain block", DimensionBlockRole.Terrain,
                new[] { LayerName.ground, LayerName.wall }, true, true,
                "A full material: ground surface + solid wall in one item, placed exactly like vanilla blocks. Carries every optional state (tilled, slime, ore…)."),

            // ---- Built surfaces & structures (stateless; item generation arrives later).
            new DimensionTilesetType("floor", "Floor", DimensionBlockRole.Built,
                new[] { LayerName.floor }, false, false,
                "A built floor surface laid over ground or bridge."),
            new DimensionTilesetType("litFloor", "Lit floor", DimensionBlockRole.Built,
                new[] { LayerName.litFloor }, false, false,
                "A glowing floor — its emissive pass is drawn automatically."),
            new DimensionTilesetType("looseFlooring", "Loose flooring", DimensionBlockRole.Built,
                new[] { LayerName.looseFlooring }, false, false,
                "Loose planks / scatter flooring."),
            new DimensionTilesetType("rug", "Rug", DimensionBlockRole.Built,
                new[] { LayerName.rug }, false, false,
                "A soft rug surface."),
            new DimensionTilesetType("bridge", "Bridge", DimensionBlockRole.Built,
                new[] { LayerName.bridge }, false, false,
                "A walkway built over water or pits."),
            new DimensionTilesetType("rail", "Rail", DimensionBlockRole.Built,
                new[] { LayerName.rail }, false, false,
                "Minecart rails."),
            new DimensionTilesetType("fence", "Fence", DimensionBlockRole.Built,
                new[] { LayerName.fence }, false, false,
                "A thin fence — its shadow and bounced light are drawn automatically."),
            new DimensionTilesetType("thinWall", "Thin wall", DimensionBlockRole.Built,
                new[] { LayerName.thinWall }, false, false,
                "A thin partition wall — its front face and shadow are drawn automatically."),
            new DimensionTilesetType("greatWall", "Great wall", DimensionBlockRole.Built,
                new[] { LayerName.greatWall }, false, false,
                "The indestructible boundary wall."),

            // ---- Liquids. (No "Pit" type: verified in the decompile that the pit tileset layer only
            // renders in Pugstorm's own editor — in the shipped game the chasm visual is the
            // neighboring ground's cross-section plus the PitFog planes, so a pit tileset would
            // author invisible data. Stalagmites-in-pits are sprite OBJECTS, not tiles.)
            new DimensionTilesetType("water", "Water", DimensionBlockRole.Liquid,
                new[] { LayerName.water }, false, false,
                "A liquid base tile — its front face and shimmer are drawn automatically."),

            // ---- Special plates.
            new DimensionTilesetType("circuitPlate", "Circuit plate", DimensionBlockRole.Special,
                new[] { LayerName.circuitPlate }, false, false,
                "Electrical wiring plate."),
            new DimensionTilesetType("ancientCircuitPlate", "Ancient circuit plate", DimensionBlockRole.Special,
                new[] { LayerName.ancientCircuitPlate }, false, false,
                "Ancient-tech wiring plate."),

            // ---- Overlays: routed, not standalone (their texture rides the underlying tile's tileset).
            new DimensionTilesetType("bigRoot", "Big roots", DimensionBlockRole.Overlay,
                new[] { LayerName.bigRoot }, false, false,
                "Roots always take their look from the tileset of the ground they grow on — author them as a state of a Terrain block, or reskin a vanilla tileset's roots."),
            new DimensionTilesetType("chrysalis", "Chrysalis", DimensionBlockRole.Overlay,
                new[] { LayerName.chrysalis }, false, false,
                "The hive membrane takes its look from the tileset it spreads on — author it as a state of a Terrain block, or reskin a vanilla tileset."),
        };

        private static readonly Dictionary<string, DimensionTilesetType> ByKey = BuildIndex();

        private static Dictionary<string, DimensionTilesetType> BuildIndex()
        {
            Dictionary<string, DimensionTilesetType> map =
                new Dictionary<string, DimensionTilesetType>(StringComparer.Ordinal);
            foreach (DimensionTilesetType type in All)
            {
                map[type.Key] = type;
            }

            return map;
        }

        public static bool TryGet(string key, out DimensionTilesetType type)
        {
            type = null;
            return !string.IsNullOrEmpty(key) && ByKey.TryGetValue(key, out type);
        }

        /// <summary>The terrain type — the default, and what legacy assets (no stored type) resolve to.</summary>
        public static DimensionTilesetType Terrain
        {
            get
            {
                TryGet("terrain", out DimensionTilesetType type);
                return type;
            }
        }

        /// <summary>Resolve an asset's stored type key, falling back to Terrain for legacy/empty keys.</summary>
        public static DimensionTilesetType Resolve(string key)
        {
            return TryGet(key, out DimensionTilesetType type) ? type : Terrain;
        }
    }
}
