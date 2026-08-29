using System.Collections.Generic;
using PugTilemap;

namespace ExpandNullforge.Zones
{
    /// <summary>One creature's claim on where it appears.</summary>
    public sealed class DimensionCreatureSpawnDefinition
    {
        public DimensionCreatureSpawnDefinition(
            string objectName,
            string biomeId,
            IReadOnlyList<int> tilesetIds,
            TileType surface,
            float spawnChance,
            int minAmount,
            int maxAmount,
            bool clustered,
            bool canSpawnInBlockedArea)
        {
            ObjectName = objectName ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            TilesetIds = tilesetIds ?? new int[0];
            Surface = surface;
            SpawnChance = spawnChance;
            MinAmount = minAmount;
            MaxAmount = maxAmount;
            Clustered = clustered;
            CanSpawnInBlockedArea = canSpawnInBlockedArea;
        }

        public readonly string ObjectName;

        /// <summary>
        /// The biome it belongs to, or empty for "anywhere the tilesets appear".
        /// </summary>
        /// <remarks>
        /// Empty is a real choice, not a gap: a creature that lives on a material rather than in a
        /// place — something that nests in a particular ore, say — should follow the material wherever
        /// the author put it.
        /// </remarks>
        public readonly string BiomeId;

        /// <summary>
        /// The tilesets it will stand on. Empty means any.
        /// </summary>
        /// <remarks>
        /// Core Keeper treats an empty tileset list as "no restriction", which is worth preserving
        /// rather than defaulting to something — an author who names a biome and no tileset means the
        /// whole biome, and filling in a guess would quietly shrink that.
        /// </remarks>
        public readonly IReadOnlyList<int> TilesetIds;

        /// <summary>Which surface it stands on — ground for most things, water for swimmers.</summary>
        public readonly TileType Surface;

        /// <summary>Chance per candidate tile, on the same scale Core Keeper's own entries use.</summary>
        public readonly float SpawnChance;

        public readonly int MinAmount;
        public readonly int MaxAmount;

        /// <summary>Whether it appears in groups rather than singly.</summary>
        public readonly bool Clustered;

        /// <summary>Whether it may appear inside dungeons and placed scenes.</summary>
        /// <remarks>
        /// Off by default, matching vanilla: a hand-built room is authored to contain what it contains,
        /// and wandering wildlife appearing inside it is almost never what anyone wanted.
        /// </remarks>
        public readonly bool CanSpawnInBlockedArea;

        /// <summary>
        /// Whether this comes back after it is taken, like a plant rather than a rock.
        /// </summary>
        /// <remarks>
        /// The distinction Core Keeper draws between its two spawn tables: one places things when a
        /// piece of world is first made, the other keeps replenishing them. A harvestable is simply an
        /// object on the second table — no growth timer to write, no regrowth system to maintain.
        /// </remarks>
        public bool Renewable;

        /// <summary>
        /// How much rarer each respawn gets as more already exist nearby.
        /// </summary>
        /// <remarks>
        /// Vanilla's own decay, and the thing that stops a renewable resource carpeting a biome. The
        /// chance is multiplied by <c>(1 - decay)^(existing count)</c>, so a patch of mushrooms thins
        /// out its own regrowth as it fills in.
        /// </remarks>
        public float RespawnDecay;
    }

    /// <summary>
    /// Every creature this mod wants the world to place, waiting to be added to Core Keeper's own
    /// spawn table.
    /// </summary>
    /// <remarks>
    /// Filled by generated bootstrap code at load, drained by
    /// <see cref="DimensionCreatureSpawnHook"/> at the one moment the table is open.
    /// </remarks>
    public static class DimensionCreatureSpawnRegistry
    {
        private static readonly List<DimensionCreatureSpawnDefinition> Definitions =
            new List<DimensionCreatureSpawnDefinition>();

        public static bool HasAny
        {
            get { return Definitions.Count > 0; }
        }

        public static IReadOnlyList<DimensionCreatureSpawnDefinition> All
        {
            get { return Definitions; }
        }

        /// <summary>
        /// Registers a spawn claim, replacing any earlier one for the same object in the same biome.
        /// </summary>
        /// <remarks>
        /// Keyed on object AND biome rather than object alone, because one creature legitimately
        /// appears in several biomes with different frequencies — a bat that is common in caves and
        /// rare in the open is two claims, not a contradiction.
        /// </remarks>
        public static void Register(
            string objectName,
            string biomeId,
            IReadOnlyList<int> tilesetIds,
            TileType surface,
            float spawnChance,
            int minAmount,
            int maxAmount,
            bool clustered,
            bool canSpawnInBlockedArea,
            bool renewable = false,
            float respawnDecay = 0.5f)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return;
            }

            DimensionCreatureSpawnDefinition definition = new DimensionCreatureSpawnDefinition(
                objectName,
                biomeId,
                tilesetIds,
                surface,
                spawnChance,
                minAmount,
                maxAmount,
                clustered,
                canSpawnInBlockedArea)
            {
                Renewable = renewable,
                RespawnDecay = respawnDecay
            };

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(Definitions[i].ObjectName, objectName, System.StringComparison.Ordinal) &&
                    string.Equals(Definitions[i].BiomeId, biomeId ?? string.Empty, System.StringComparison.Ordinal))
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
