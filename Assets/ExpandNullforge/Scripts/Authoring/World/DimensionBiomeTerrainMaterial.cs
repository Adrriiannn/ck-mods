using ExpandNullforge.Tilesets;
using PugTilemap;

namespace ExpandNullforge.Authoring
{
    /// <summary>What a Biome page "Ground" or "Walls" entry turned out to name.</summary>
    public enum DimensionBiomeTerrainSource
    {
        /// <summary>Nothing was named — the list is empty.</summary>
        Empty,

        /// <summary>One of this mod's own blocks, by the item id its tileset generates.</summary>
        ModBlock,

        /// <summary>One of the game's own blocks, picked from the vanilla list.</summary>
        VanillaBlock,

        /// <summary>Something was named and it matches nothing. The world would lay dirt there.</summary>
        Unknown
    }

    /// <summary>
    /// Turns the block a biome names for its ground or its walls into the tileset the world
    /// actually writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One resolver, read from three places — the export that emits the registration, the
    /// validation that tells the author when an entry names nothing, and the Biome page's picker,
    /// which writes the ids this reads back. Two copies of this rule would be two chances for the
    /// picker to write something the export cannot resolve.
    /// </para>
    /// <para>
    /// THE FIRST ENTRY IS THE ONE THAT BUILDS TERRAIN. A cell has one ground and one wall, so the
    /// list has to collapse to one number, and taking the first match is the rule the framework
    /// already set for ore ("the game finds a vein by taking the first match"). The rest of the
    /// list keeps its existing job of saying what the biome is made of.
    /// </para>
    /// </remarks>
    public static class DimensionBiomeTerrainMaterial
    {
        /// <summary>
        /// What a picked vanilla block is written as in the biome's list: "vanilla:" and the name
        /// the block is called in the picker ("vanilla:Stone").
        /// </summary>
        public const string VanillaPrefix = "vanilla:";

        /// <summary>The block id that stands for one of the game's own blocks.</summary>
        public static string VanillaId(string displayName)
        {
            return VanillaPrefix + (displayName ?? string.Empty);
        }

        /// <summary>
        /// The tileset the first entry of a biome's Ground or Walls list resolves to.
        /// </summary>
        /// <param name="objectIds">The biome's Ground or Walls list, as authored.</param>
        /// <param name="tilesets">The dimension's own blocks.</param>
        /// <param name="tilesetId">
        /// The tileset the world should build from. Meaningful only when the result is
        /// <see cref="DimensionBiomeTerrainSource.ModBlock"/> or
        /// <see cref="DimensionBiomeTerrainSource.VanillaBlock"/>.
        /// </param>
        /// <param name="named">The entry that was looked at, for a message that names it.</param>
        /// <param name="hasGround">
        /// Whether the resolved block has a ground surface at all. A wall-only block named as a
        /// biome's Ground builds nothing a player can stand on.
        /// </param>
        public static DimensionBiomeTerrainSource ResolveFirst(
            string[] objectIds,
            DimensionTilesetAsset[] tilesets,
            out int tilesetId,
            out string named,
            out bool hasGround)
        {
            tilesetId = 0;
            named = string.Empty;
            hasGround = false;

            if (objectIds == null)
            {
                return DimensionBiomeTerrainSource.Empty;
            }

            for (int i = 0; i < objectIds.Length; i++)
            {
                string candidate = objectIds[i];
                if (!string.IsNullOrEmpty(candidate))
                {
                    named = candidate;
                    return Resolve(candidate, tilesets, out tilesetId, out hasGround);
                }
            }

            return DimensionBiomeTerrainSource.Empty;
        }

        /// <summary>The tileset one block id resolves to.</summary>
        public static DimensionBiomeTerrainSource Resolve(
            string objectId,
            DimensionTilesetAsset[] tilesets,
            out int tilesetId,
            out bool hasGround)
        {
            tilesetId = 0;
            hasGround = false;
            if (string.IsNullOrEmpty(objectId))
            {
                return DimensionBiomeTerrainSource.Empty;
            }

            if (objectId.StartsWith(VanillaPrefix, System.StringComparison.Ordinal))
            {
                string name = objectId.Substring(VanillaPrefix.Length);
                System.Collections.Generic.IReadOnlyList<DimensionVanillaTilesetEntry> all =
                    DimensionVanillaTilesetCatalog.All;
                for (int i = 0; i < all.Count; i++)
                {
                    if (!string.Equals(all[i].DisplayName, name, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    tilesetId = all[i].TilesetIndex;
                    hasGround = all[i].HasGround;
                    return DimensionBiomeTerrainSource.VanillaBlock;
                }

                return DimensionBiomeTerrainSource.Unknown;
            }

            if (tilesets != null)
            {
                for (int i = 0; i < tilesets.Length; i++)
                {
                    DimensionTilesetAsset tileset = tilesets[i];
                    if (tileset == null || !tileset.Enabled)
                    {
                        continue;
                    }

                    if (!string.Equals(objectId, tileset.GroundBlockItemId, System.StringComparison.Ordinal) &&
                        !string.Equals(objectId, tileset.WallBlockItemId, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    tilesetId = tileset.TilesetId;
                    hasGround = TypeHasGround(tileset);
                    return DimensionBiomeTerrainSource.ModBlock;
                }
            }

            return DimensionBiomeTerrainSource.Unknown;
        }

        /// <summary>Whether a block's type authors a ground surface at all.</summary>
        private static bool TypeHasGround(DimensionTilesetAsset tileset)
        {
            DimensionTilesetType type = tileset.BlockType;
            LayerName[] authored = type == null ? null : type.AuthoredLayers;
            if (authored == null)
            {
                return false;
            }

            for (int i = 0; i < authored.Length; i++)
            {
                if (authored[i] == LayerName.ground)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
