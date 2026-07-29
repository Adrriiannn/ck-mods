using System.Collections.Generic;

namespace ExpandNullforge.Tilesets
{
    /// <summary>One vanilla tileset a modder can reskin, presented by its player-facing block.</summary>
    public sealed class DimensionVanillaTilesetEntry
    {
        /// <summary>The vanilla Tileset enum index (0-74) — the render key the reskin overrides.</summary>
        public readonly int TilesetIndex;
        /// <summary>Player-facing name of the block/material ("Dirt", "Stone", …).</summary>
        public readonly string DisplayName;
        /// <summary>True when the tileset has both a ground and a wall block (reskin affects both).</summary>
        public readonly bool HasGround;

        public DimensionVanillaTilesetEntry(int tilesetIndex, string displayName, bool hasGround)
        {
            TilesetIndex = tilesetIndex;
            DisplayName = displayName;
            HasGround = hasGround;
        }
    }

    /// <summary>
    /// The vanilla tilesets a modder can pick as reskin targets, verified against the decompiled
    /// Tileset enum and the shipped block prefabs (a biome's wall + ground share one index, so a
    /// reskin covers both). Paint/glass color variants are listed once by their family.
    /// </summary>
    public static class DimensionVanillaTilesetCatalog
    {
        public static readonly IReadOnlyList<DimensionVanillaTilesetEntry> All = new List<DimensionVanillaTilesetEntry>
        {
            new DimensionVanillaTilesetEntry(0, "Dirt", true),
            new DimensionVanillaTilesetEntry(1, "Stone", true),
            new DimensionVanillaTilesetEntry(2, "Obsidian", true),
            new DimensionVanillaTilesetEntry(3, "Lava", true),
            new DimensionVanillaTilesetEntry(5, "Wood (base building)", false),
            new DimensionVanillaTilesetEntry(6, "Larva Hive", true),
            new DimensionVanillaTilesetEntry(7, "Decorative Stone", false),
            new DimensionVanillaTilesetEntry(8, "Nature / Grass", true),
            new DimensionVanillaTilesetEntry(9, "Mold", true),
            new DimensionVanillaTilesetEntry(10, "Sea / Limestone", true),
            new DimensionVanillaTilesetEntry(11, "Clay", true),
            new DimensionVanillaTilesetEntry(12, "Sand", true),
            new DimensionVanillaTilesetEntry(13, "Turf", true),
            new DimensionVanillaTilesetEntry(14, "Paintable (unpainted)", false),
            new DimensionVanillaTilesetEntry(23, "Scarlet", false),
            new DimensionVanillaTilesetEntry(24, "City", true),
            new DimensionVanillaTilesetEntry(25, "Coral", false),
            new DimensionVanillaTilesetEntry(26, "Desert", true),
            new DimensionVanillaTilesetEntry(27, "Desert Temple", true),
            new DimensionVanillaTilesetEntry(28, "Galaxite", false),
            new DimensionVanillaTilesetEntry(31, "Snow", true),
            new DimensionVanillaTilesetEntry(34, "Glass", false),
            new DimensionVanillaTilesetEntry(35, "Meadow", true),
            new DimensionVanillaTilesetEntry(36, "Explosive", false),
            new DimensionVanillaTilesetEntry(53, "Straw (meadow building)", false),
            new DimensionVanillaTilesetEntry(54, "Dark Stone", true),
            new DimensionVanillaTilesetEntry(55, "Crystal", true),
            new DimensionVanillaTilesetEntry(56, "Alien", true),
            new DimensionVanillaTilesetEntry(57, "Gleam Wood", false),
            new DimensionVanillaTilesetEntry(58, "Eerie", false),
            new DimensionVanillaTilesetEntry(59, "Natural Explosive", false),
            new DimensionVanillaTilesetEntry(60, "Passage", true),
            new DimensionVanillaTilesetEntry(66, "Oasis", true),
            new DimensionVanillaTilesetEntry(67, "Forest Explosive", false),
            new DimensionVanillaTilesetEntry(68, "Desert Explosive", false),
            new DimensionVanillaTilesetEntry(69, "Excavation", true),
            new DimensionVanillaTilesetEntry(70, "Excavation Dungeon", true),
            new DimensionVanillaTilesetEntry(71, "Excavation Rock", true),
            new DimensionVanillaTilesetEntry(72, "Excavation Border", true),
            new DimensionVanillaTilesetEntry(74, "Excavation Void", true),
        };

        /// <summary>Display name for a vanilla tileset index, or a numeric fallback.</summary>
        public static string NameOf(int tilesetIndex)
        {
            foreach (DimensionVanillaTilesetEntry entry in All)
            {
                if (entry.TilesetIndex == tilesetIndex)
                {
                    return entry.DisplayName;
                }
            }

            return "Tileset " + tilesetIndex;
        }
    }
}
