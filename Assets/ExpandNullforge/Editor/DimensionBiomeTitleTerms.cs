namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The localization term a biome's title card is drawn from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One place decides this, because two do not stay in step: the generated runtime registers the
    /// term and the generated CSV fills it, and if either side spelled it differently the title would
    /// render as the raw key — visible in game, invisible in every build log.
    /// </para>
    /// <para>
    /// Namespaced by mod for the same reason every other generated id is: two mods with a "Frostlands"
    /// would otherwise share one title, and whichever loaded last would name both.
    /// </para>
    /// </remarks>
    internal static class DimensionBiomeTitleTerms
    {
        /// <summary>The term for a biome, before the lookup's colon replacement is applied.</summary>
        public static string ForBiome(string modName, string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return string.Empty;
            }

            return "Biomes/" + DimensionObjectNamespace.Qualify(modName, biomeId);
        }
    }
}
