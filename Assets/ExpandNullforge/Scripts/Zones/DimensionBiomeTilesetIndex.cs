using System.Collections.Generic;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Which custom biome each custom tileset belongs to — the single answer to "where is the player
    /// standing".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every part of a biome's presentation asks this same question: the title card, the ambience mix,
    /// the music roster, and the biome the game itself reports for the player. They have to agree, and
    /// the only way to guarantee that is for there to be one place that answers.
    /// </para>
    /// <para>
    /// Keyed on tilesets rather than on zone rectangles because that is the question Core Keeper
    /// itself asks — its ambience, music and title systems all count tilesets in a box around the
    /// player. A rectangle would be a second, differently-shaped answer, and the two would disagree
    /// exactly at the edges, which is where players notice.
    /// </para>
    /// </remarks>
    public static class DimensionBiomeTilesetIndex
    {
        private static readonly Dictionary<int, string> BiomeByTileset = new Dictionary<int, string>();

        public static bool HasAny
        {
            get { return BiomeByTileset.Count > 0; }
        }

        /// <summary>
        /// Claims a tileset for a biome, if no biome has claimed it already.
        /// </summary>
        /// <remarks>
        /// First claim wins, deliberately. Two mods claiming one tileset is a conflict this framework
        /// cannot arbitrate — but letting the last writer win would make the answer depend on load
        /// order, so the same two mods would behave differently on two players' machines.
        /// </remarks>
        public static void Claim(int tilesetId, string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId) || BiomeByTileset.ContainsKey(tilesetId))
            {
                return;
            }

            BiomeByTileset.Add(tilesetId, biomeId);
        }

        /// <summary>Releases a tileset, but only if the biome giving it up is the one holding it.</summary>
        public static void Release(int tilesetId, string biomeId)
        {
            string owner;
            if (BiomeByTileset.TryGetValue(tilesetId, out owner) &&
                string.Equals(owner, biomeId, System.StringComparison.Ordinal))
            {
                BiomeByTileset.Remove(tilesetId);
            }
        }

        /// <summary>
        /// The biome a tileset belongs to, if any.
        /// </summary>
        /// <remarks>
        /// Returning false for an unclaimed tileset is what lets a player walk out of a custom biome
        /// and get their real one back. If an unclaimed tileset resolved to anything, the biome would
        /// never end.
        /// </remarks>
        public static bool TryGetBiomeId(int tilesetId, out string biomeId)
        {
            return BiomeByTileset.TryGetValue(tilesetId, out biomeId);
        }

        public static void Clear()
        {
            BiomeByTileset.Clear();
        }
    }
}
