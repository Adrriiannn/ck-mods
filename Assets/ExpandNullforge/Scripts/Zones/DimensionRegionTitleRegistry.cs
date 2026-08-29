using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// The title card a custom biome shows the first time a player walks into it.
    /// </summary>
    public sealed class DimensionRegionTitleDefinition
    {
        public DimensionRegionTitleDefinition(
            string biomeId,
            string titleTerm,
            Color color,
            IReadOnlyList<int> requiredTilesetIds,
            string iconObjectName)
        {
            BiomeId = biomeId ?? string.Empty;
            TitleTerm = titleTerm ?? string.Empty;
            Color = color;
            RequiredTilesetIds = requiredTilesetIds ?? new int[0];
            IconObjectName = iconObjectName ?? string.Empty;
        }

        public readonly string BiomeId;

        /// <summary>
        /// The localization term whose text is drawn, not the text itself.
        /// </summary>
        /// <remarks>
        /// Core Keeper renders the term, so a title given as literal text would appear correctly in
        /// English and as that same English in every other language. Handing over the term is what lets
        /// a translated mod actually translate.
        /// </remarks>
        public readonly string TitleTerm;

        /// <summary>The title's colour, which also becomes the gamepad light while inside.</summary>
        public readonly Color Color;

        /// <summary>
        /// The tilesets that have to be underfoot before an undiscovered biome announces itself.
        /// </summary>
        /// <remarks>
        /// Vanilla's own gate, and a good one: it stops a title firing because the player clipped the
        /// corner of a region on the way past. Core Keeper counts tiles in a 20x20 box and wants more
        /// than 100 of them, so the biome has to genuinely be where the player is standing.
        /// </remarks>
        public readonly IReadOnlyList<int> RequiredTilesetIds;

        /// <summary>The object whose icon flanks the title, or empty for none.</summary>
        public readonly string IconObjectName;
    }

    /// <summary>
    /// Every custom biome that has a title card, waiting to be handed to Core Keeper's own handler.
    /// </summary>
    /// <remarks>
    /// Filled by generated bootstrap code at load and read once per scene by
    /// <see cref="DimensionRegionTitleInstaller"/>. Kept separate from the installer so registration
    /// does not depend on a scene existing yet — mods load long before the game scene does.
    /// </remarks>
    public static class DimensionRegionTitleRegistry
    {
        private static readonly List<DimensionRegionTitleDefinition> Titles =
            new List<DimensionRegionTitleDefinition>();


        public static bool HasAny
        {
            get { return Titles.Count > 0; }
        }

        public static IReadOnlyList<DimensionRegionTitleDefinition> All
        {
            get { return Titles; }
        }

        /// <summary>
        /// Registers a title, replacing any earlier one for the same biome.
        /// </summary>
        /// <remarks>
        /// Replacing rather than appending keeps a reloaded mod from stacking duplicate titles, which
        /// vanilla would resolve by whichever it found first — making the visible title depend on load
        /// order.
        /// </remarks>
        public static void Register(
            string biomeId,
            string titleTerm,
            Color color,
            IReadOnlyList<int> requiredTilesetIds,
            string iconObjectName)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return;
            }

            DimensionRegionTitleDefinition definition = new DimensionRegionTitleDefinition(
                biomeId,
                titleTerm,
                color,
                requiredTilesetIds,
                iconObjectName);

            for (int i = 0; i < Titles.Count; i++)
            {
                if (string.Equals(Titles[i].BiomeId, biomeId, System.StringComparison.Ordinal))
                {
                    RemoveTilesetsOf(Titles[i]);
                    Titles[i] = definition;
                    IndexTilesetsOf(definition);
                    return;
                }
            }

            Titles.Add(definition);
            IndexTilesetsOf(definition);
        }

        /// <summary>
        /// Which custom biome a tileset belongs to, if any.
        /// </summary>
        /// <remarks>
        /// Delegates to <see cref="DimensionBiomeTilesetIndex"/>, which is shared with ambience and
        /// music so all three always name the same place.
        /// </remarks>
        public static bool TryGetBiomeIdForTileset(int tilesetId, out string biomeId)
        {
            return DimensionBiomeTilesetIndex.TryGetBiomeId(tilesetId, out biomeId);
        }

        public static void Clear()
        {
            Titles.Clear();
            DimensionBiomeTilesetIndex.Clear();
        }

        private static void IndexTilesetsOf(DimensionRegionTitleDefinition definition)
        {
            for (int i = 0; i < definition.RequiredTilesetIds.Count; i++)
            {
                DimensionBiomeTilesetIndex.Claim(definition.RequiredTilesetIds[i], definition.BiomeId);
            }
        }

        private static void RemoveTilesetsOf(DimensionRegionTitleDefinition definition)
        {
            for (int i = 0; i < definition.RequiredTilesetIds.Count; i++)
            {
                DimensionBiomeTilesetIndex.Release(definition.RequiredTilesetIds[i], definition.BiomeId);
            }
        }
    }
}
