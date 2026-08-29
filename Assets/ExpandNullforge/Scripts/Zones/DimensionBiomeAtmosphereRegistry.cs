using System.Collections.Generic;

namespace ExpandNullforge.Zones
{
    /// <summary>What a custom biome sounds like.</summary>
    public sealed class DimensionBiomeAtmosphereDefinition
    {
        public DimensionBiomeAtmosphereDefinition(
            string biomeId,
            string ambienceSoundKey,
            float ambienceVolume,
            string musicRosterName,
            IReadOnlyList<int> tilesetIds)
        {
            BiomeId = biomeId ?? string.Empty;
            AmbienceSoundKey = ambienceSoundKey ?? string.Empty;
            AmbienceVolume = ambienceVolume;
            MusicRosterName = musicRosterName ?? string.Empty;
            TilesetIds = tilesetIds ?? new int[0];
        }

        public readonly string BiomeId;

        /// <summary>
        /// The looping ambience clip, as a Sound Library key.
        /// </summary>
        /// <remarks>
        /// Empty means "no ambience of its own", which is a real choice rather than an omission — a
        /// small cave biome inside a larger one often reads better carrying the sound of its host.
        /// </remarks>
        public readonly string AmbienceSoundKey;

        /// <summary>
        /// How loud this ambience is at its fullest, relative to the game's own.
        /// </summary>
        /// <remarks>
        /// Multiplies the volume Core Keeper computes from nearby tiles rather than replacing it, so
        /// the ambience still fades in and out with how much of the biome is around the player. A flat
        /// volume would make the sound snap on at the boundary.
        /// </remarks>
        public readonly float AmbienceVolume;

        /// <summary>
        /// The name of the Core Keeper music roster this biome plays, or empty for none.
        /// </summary>
        /// <remarks>
        /// A roster name rather than a track, because Core Keeper's music is a playlist with its own
        /// shuffling, cooldowns and cross-fades. Naming a roster hands all of that over; naming a
        /// track would mean rebuilding it.
        /// </remarks>
        public readonly string MusicRosterName;

        /// <summary>The tilesets that mean "the player is here".</summary>
        public readonly IReadOnlyList<int> TilesetIds;

        public bool HasAmbience
        {
            get { return !string.IsNullOrEmpty(AmbienceSoundKey); }
        }

        public bool HasMusic
        {
            get { return !string.IsNullOrEmpty(MusicRosterName); }
        }
    }

    /// <summary>
    /// The sound of every custom biome, waiting to be handed to Core Keeper's own audio handlers.
    /// </summary>
    /// <remarks>
    /// Filled by generated bootstrap code at load. Nothing here plays anything — the installers hand
    /// these to the game's ambience and music handlers, which then do the mixing, the distance
    /// falloff, the streaming in and out, and the cross-fades. That is the whole point: a mod's biome
    /// should sound like part of the game, not like a mod playing a sound over it.
    /// </remarks>
    public static class DimensionBiomeAtmosphereRegistry
    {
        private static readonly List<DimensionBiomeAtmosphereDefinition> Definitions =
            new List<DimensionBiomeAtmosphereDefinition>();

        public static bool HasAny
        {
            get { return Definitions.Count > 0; }
        }

        public static IReadOnlyList<DimensionBiomeAtmosphereDefinition> All
        {
            get { return Definitions; }
        }

        /// <summary>Registers a biome's atmosphere, replacing any earlier one for the same biome.</summary>
        public static void Register(
            string biomeId,
            string ambienceSoundKey,
            float ambienceVolume,
            string musicRosterName,
            IReadOnlyList<int> tilesetIds)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return;
            }

            DimensionBiomeAtmosphereDefinition definition = new DimensionBiomeAtmosphereDefinition(
                biomeId,
                ambienceSoundKey,
                ambienceVolume,
                musicRosterName,
                tilesetIds);

            for (int i = 0; i < tilesetIds.Count; i++)
            {
                DimensionBiomeTilesetIndex.Claim(tilesetIds[i], biomeId);
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(Definitions[i].BiomeId, biomeId, System.StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    return;
                }
            }

            Definitions.Add(definition);
        }

        public static void Clear()
        {
            Definitions.Clear();
        }
    }
}
