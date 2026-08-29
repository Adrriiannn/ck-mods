using System.Collections.Generic;
using HarmonyLib;
using Pug.UnityExtensions;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// The mechanism that keeps a place ALIVE: "this kind of tile keeps producing this creature",
    /// which is exactly how vanilla dungeons repopulate after their garrison dies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MEASURED VANILLA TRUTH this rides on. The game's periodic respawn sweep
    /// (<c>SpawnEnvironmentObjectsPeriodicallySystem</c>) walks the world every ~15 minutes,
    /// only where a player is within 200 tiles, and rolls each respawn row against every tile
    /// whose surface matches the row's <c>(tileType, tilesets)</c> key. The rows come from
    /// <c>Manager.mod.SpawnTable.respawnObjects</c> — a plain managed list read by a plain
    /// managed converter (<c>EnvironmentSpawnObjectsTableConverter.Convert</c>) — and the
    /// tileset check is a list-contains over ints, so a CUSTOM tileset id keys a row exactly
    /// like a vanilla one. This is how "chrysalis on Stone breeds Cavelings" works, and it is
    /// open to us verbatim.
    /// </para>
    /// <para>
    /// Two facts make this the honest dungeon-repopulation surface rather than a hack. First,
    /// the periodic sweep's checks run against the transient blocked-area set, NOT the spawn
    /// cell's persistent buffer where a dungeon's keep-out circles live — so a dungeon that
    /// blocks other spawns still respawns its own tile-bred creatures, by the game's own design.
    /// Second, the JSON route can't name mod creatures (ids are literal ints in the files), but
    /// this registry appends AFTER objects exist and resolves by name, so custom creatures ride
    /// through.
    /// </para>
    /// <para>
    /// Registered per creature, mirroring vanilla's own files: each species is its own row with
    /// its own chance (Caveling 0.3 and its Shaman 0.01 are separate rows in the shipped Conf).
    /// </para>
    /// </remarks>
    public static class DimensionRespawnRegistry
    {
        public struct RespawnRule
        {
            /// <summary>Unique row name; replaces an earlier row of the same name.</summary>
            public string RuleName;
            public string CreatureObjectName;
            /// <summary>The tile surface it appears on.</summary>
            public TileType TileType;
            /// <summary>Full-width tileset ids, custom ids included. Empty means any tileset.</summary>
            public int[] Tilesets;
            /// <summary>Per-sweep chance, 0 to 1.</summary>
            public float Chance;
            /// <summary>Chance is multiplied by (1 - this)^existing, so crowds self-limit.</summary>
            public float ChanceDecayPerExisting;
            public float MaxPerTile;
            public int MaxPerSweep;
            public float MinTilesRequired;
            /// <summary>A vanilla biome name to confine the row to, or empty for anywhere.</summary>
            public string OnlyInBiome;
        }

        private static readonly List<RespawnRule> Rules = new List<RespawnRule>();
        private static bool applied;

        /// <summary>Queues a rule. Call from the generated bootstrap; names resolve at load.</summary>
        public static void Register(RespawnRule rule)
        {
            if (string.IsNullOrEmpty(rule.RuleName) || string.IsNullOrEmpty(rule.CreatureObjectName))
            {
                return;
            }

            for (int i = 0; i < Rules.Count; i++)
            {
                if (string.Equals(Rules[i].RuleName, rule.RuleName, System.StringComparison.Ordinal))
                {
                    Rules.RemoveAt(i);
                    break;
                }
            }

            Rules.Add(rule);
        }

        /// <summary>Clears queued rules (mod reload).</summary>
        public static void Clear()
        {
            Rules.Clear();
            applied = false;
        }

        internal static int PendingCount
        {
            get { return Rules.Count; }
        }

        /// <summary>
        /// Appends every queued rule to the game's own respawn table, just before it is baked.
        /// Idempotent per load.
        /// </summary>
        internal static void ApplyToSpawnTable()
        {
            if (applied || Rules.Count == 0)
            {
                return;
            }

            EnvironmentSpawnObjectsTable table = Manager.mod == null ? null : Manager.mod.SpawnTable;
            List<RespawnData> respawns = table == null ? null : table.respawnObjects;
            if (respawns == null)
            {
                return;
            }

            int added = 0;
            for (int i = 0; i < Rules.Count; i++)
            {
                RespawnRule rule = Rules[i];
                ObjectID creatureId = PugMod.API.Authoring.GetObjectID(rule.CreatureObjectName);
                if (creatureId == ObjectID.None)
                {
                    Foundation.DimensionFrameworkLog.Warning(
                        "Respawn rule '" + rule.RuleName + "' names creature '" +
                        rule.CreatureObjectName + "', which is not a known object. The rule was " +
                        "left out.");
                    continue;
                }

                RespawnData data = new RespawnData
                {
                    name = rule.RuleName,
                    spawnCheck = new RespawnData.SpawnCheck
                    {
                        spawnChance = new EnvironmentalSpawnChance
                        {
                            source = EnvironmentalSpawnChance.Source.Constant,
                            constantValue = new PlatformDependentValue<float>(
                                Mathf.Clamp01(rule.Chance))
                        },
                        spawnChanceDecay = Mathf.Clamp01(rule.ChanceDecayPerExisting),
                        maxSpawnPerTile = new PlatformDependentValue<float>(
                            Mathf.Max(0f, rule.MaxPerTile)),
                        maxSpawnsPerRespawn = Mathf.Max(1, rule.MaxPerSweep),
                        minTilesRequired = Mathf.Max(0f, rule.MinTilesRequired),
                        tileType = rule.TileType,
                        tilesets = new List<Tileset>(),
                        adjacentTiles = new RespawnData.SpawnCheck.AdjacentTileList
                        {
                            // The converter foreaches this list unconditionally; a null here is
                            // a load-time crash that would take the whole table with it.
                            list = new List<TileRequirement>()
                        }
                    },
                    spawns = new List<SpawnObjectData>
                    {
                        new SpawnObjectData
                        {
                            spawnType = EnvironmentSpawnType.Spot,
                            objectID = creatureId,
                            variation = new Pug.UnityExtensions.RangeInt { min = 0, max = 0 },
                            // Zero means "the object's own initial amount", the converter's rule.
                            amount = 0
                        }
                    }
                };

                if (rule.Tilesets != null)
                {
                    for (int t = 0; t < rule.Tilesets.Length; t++)
                    {
                        // Custom ids ride through: the runtime check is List.Contains over the
                        // int-backed enum, never an array indexed by it.
                        data.spawnCheck.tilesets.Add((Tileset)rule.Tilesets[t]);
                    }
                }

                if (!string.IsNullOrEmpty(rule.OnlyInBiome) &&
                    System.Enum.TryParse(rule.OnlyInBiome, false, out Biome biome))
                {
                    data.spawnCheck.biome = biome;
                }

                respawns.Add(data);
                added++;
            }

            if (added > 0)
            {
                Foundation.DimensionFrameworkLog.Info(
                    "Added " + added +
                    " creature respawn rule(s) to the game's own spawn table.");
            }

            applied = true;
        }
    }

    /// <summary>
    /// The one moment the managed spawn table still matters: the converter reads
    /// <c>Manager.mod.SpawnTable</c> when the game is playing, and everything after runs from
    /// the baked buffer. Appending in a prefix here is the whole injection.
    /// </summary>
    [HarmonyPatch(typeof(EnvironmentSpawnObjectsTableConverter), "Convert")]
    internal static class DimensionRespawnTableConverterPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        [HarmonyPrefix]
        private static void Prefix()
        {
            Fired++;

            DimensionRespawnRegistry.ApplyToSpawnTable();
        }
    }
}
