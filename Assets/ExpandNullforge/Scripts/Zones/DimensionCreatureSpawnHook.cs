using System.Collections.Generic;
using ExpandNullforge.Foundation;
using HarmonyLib;
using Pug.UnityExtensions;
using PugMod;
using PugTilemap;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Adds this mod's creatures to Core Keeper's own world-spawning table, so they appear the way
    /// everything else in the world appears.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PugMods.SpawnTable.Init</c> is the game's OWN mod hook for this table — it is called once,
    /// on a fresh copy of the spawn data, immediately before the converter turns it into the buffer
    /// world generation reads. Appending there means custom creatures go through exactly the same
    /// placement pass as vanilla ones: the same chance rolls, the same tile checks, the same clustering,
    /// the same respawn behaviour.
    /// </para>
    /// <para>
    /// WHY NOT SPAWN THEM ANOTHER WAY. Writing our own spawner would have to reproduce all of that,
    /// and would get it subtly wrong in the places that matter — density near a base, not spawning on
    /// top of the player, not filling in a dungeon that was authored to be empty.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(PugMods.SpawnTable), nameof(PugMods.SpawnTable.Init))]
    public static class DimensionCreatureSpawnHook
    {
        /// <summary>How many creature spawn rules the last run added.</summary>
        public static int LastAddedCount { get; private set; }

        private static void Postfix(EnvironmentSpawnObjectsTable spawnTable)
        {
            if (spawnTable == null || !DimensionCreatureSpawnRegistry.HasAny)
            {
                return;
            }

            if (spawnTable.spawnObjects == null)
            {
                spawnTable.spawnObjects = new List<EnvironmentSpawnData>();
            }

            if (spawnTable.respawnObjects == null)
            {
                spawnTable.respawnObjects = new List<RespawnData>();
            }

            LastAddedCount = 0;
            IReadOnlyList<DimensionCreatureSpawnDefinition> definitions = DimensionCreatureSpawnRegistry.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (TryAdd(spawnTable, definitions[i]))
                {
                    LastAddedCount++;
                }
            }

            if (LastAddedCount > 0)
            {
                DimensionFrameworkLog.Info(
                    "Added " + LastAddedCount +
                    " creature(s) to the world's own spawn table.");
            }
        }

        private static bool TryAdd(
            EnvironmentSpawnObjectsTable spawnTable,
            DimensionCreatureSpawnDefinition definition)
        {
            ObjectID objectID = API.Authoring.GetObjectID(definition.ObjectName);
            if (objectID == ObjectID.None)
            {
                // The creature's own prefab failed to register, which is a different problem reported
                // elsewhere. Saying so here too is worth it: "my creature never spawns" is the symptom
                // an author will actually notice, and this is where they will look.
                DimensionFrameworkLog.Warning(
                    "'" + definition.ObjectName + "' is set to spawn in the world, " +
                    "but no object of that name is registered, so it will never appear. Check that its " +
                    "prefab was generated into the mod folder.");
                return false;
            }

            // Courtesy, not a refusal: the spawn table is world-global with no per-dimension
            // field, and the ambient gate suppresses placement inside a gated dimension. The
            // author who listed a Dungeon's biome here deserves to hear where the row went.
            Api.IDimensionService gateService;
            if (!string.IsNullOrEmpty(definition.BiomeId) &&
                Api.DimensionApi.TryGetService(out gateService) && gateService != null)
            {
                IReadOnlyList<Api.DimensionDefinition> dimensions = gateService.GetDimensions();
                for (int d = 0; d < dimensions.Count; d++)
                {
                    if (DimensionTypePolicy.For(dimensions[d].Type).BlocksAmbientSpawns &&
                        BiomeBelongsToDimension(gateService, definition.BiomeId, dimensions[d].Id))
                    {
                        DimensionFrameworkLog.Warning(
                            "'" + definition.ObjectName + "' spawns in biome '" +
                            definition.BiomeId + "', which belongs to " + dimensions[d].Type +
                            " '" + dimensions[d].Id + "' — a type that keeps ambient spawning " +
                            "out. It will only appear there through spawn rules or scenes.");
                        break;
                    }
                }
            }

            List<SpawnObjectData> spawns = new List<SpawnObjectData>
            {
                new SpawnObjectData
                {
                    spawnType = definition.Clustered
                        ? EnvironmentSpawnType.Cluster
                        : EnvironmentSpawnType.Spot,
                    objectID = objectID,
                    variation = new Pug.UnityExtensions.RangeInt { min = 0, max = 0 },
                    amount = definition.MaxAmount < 1 ? 1 : definition.MaxAmount
                }
            };

            if (definition.Renewable)
            {
                // The game's own second table. Everything on it is replenished as the world is
                // revisited, which is the whole of what "harvestable" means — no growth timer of ours,
                // no regrowth system to keep working.
                spawnTable.respawnObjects.Add(new RespawnData
                {
                    name = definition.BiomeId + "/" + definition.ObjectName,
                    spawnCheck = BuildRespawnCheck(definition),
                    spawns = spawns
                });

                return true;
            }

            EnvironmentSpawnData entry = new EnvironmentSpawnData
            {
                // The game rewrites this in OnValidate from the biome and object; setting something
                // readable keeps it legible if anyone inspects the table before that runs.
                name = definition.BiomeId + "/" + definition.ObjectName,
                spawnCheck = BuildCheck(definition),
                spawns = spawns
            };

            spawnTable.spawnObjects.Add(entry);
            return true;
        }

        /// <summary>
        /// The respawn table's own version of the same conditions.
        /// </summary>
        /// <remarks>
        /// A near-twin of the spawn check, and Core Keeper keeps them as separate types rather than
        /// sharing one — respawning cares about decay and density in a way first placement does not.
        /// Building both from one definition is what keeps a harvestable appearing in the same places
        /// it regrows in.
        /// </remarks>
        private static RespawnData.SpawnCheck BuildRespawnCheck(DimensionCreatureSpawnDefinition definition)
        {
            List<Tileset> tilesets = new List<Tileset>();
            for (int i = 0; i < definition.TilesetIds.Count; i++)
            {
                tilesets.Add((Tileset)definition.TilesetIds[i]);
            }

            return new RespawnData.SpawnCheck
            {
                spawnChance = new EnvironmentalSpawnChance
                {
                    source = EnvironmentalSpawnChance.Source.Constant,
                    constantValue = new PlatformDependentValue<float>(definition.SpawnChance)
                },
                biome = string.IsNullOrEmpty(definition.BiomeId)
                    ? Biome.None
                    : DimensionBiomeIdentity.GetOrAssign(definition.BiomeId),
                spawnChanceDecay = definition.RespawnDecay,
                maxSpawnPerTile = new PlatformDependentValue<float>(1f),
                maxSpawnsPerRespawn = definition.MaxAmount < 1 ? 1 : definition.MaxAmount,
                minTilesRequired = 0f,
                tileType = definition.Surface,
                tilesets = tilesets,
                adjacentTiles = new RespawnData.SpawnCheck.AdjacentTileList
                {
                    list = new List<TileRequirement>()
                }
            };
        }

        private static EnvironmentSpawnData.SpawnCheck BuildCheck(DimensionCreatureSpawnDefinition definition)
        {
            List<Tileset> tilesets = new List<Tileset>();
            for (int i = 0; i < definition.TilesetIds.Count; i++)
            {
                // Raw cast: world generation compares the tile's own tileset number, so a custom id
                // matches here exactly as a vanilla one does.
                tilesets.Add((Tileset)definition.TilesetIds[i]);
            }

            return new EnvironmentSpawnData.SpawnCheck
            {
                spawnChance = new EnvironmentalSpawnChance
                {
                    source = EnvironmentalSpawnChance.Source.Constant,
                    constantValue = new PlatformDependentValue<float>(definition.SpawnChance)
                },

                // Biome.None is Core Keeper's own "any biome", which is exactly what an author means
                // by leaving the biome empty.
                biome = string.IsNullOrEmpty(definition.BiomeId)
                    ? Biome.None
                    : DimensionBiomeIdentity.GetOrAssign(definition.BiomeId),
                tileType = definition.Surface,
                tilesets = tilesets,
                adjacentTiles = new EnvironmentSpawnData.SpawnCheck.AdjacentTileList
                {
                    list = new List<TileRequirement>()
                },
                canSpawnInBlockedArea = definition.CanSpawnInBlockedArea,
                skipSpawnForPartialMaps = false
            };
        }

        /// <summary>Whether a biome id names one of a dimension's zones — biome identity rides Kind.</summary>
        private static bool BiomeBelongsToDimension(
            Api.IDimensionService service,
            string biomeId,
            string dimensionId)
        {
            Api.IDimensionZoneCatalogService zones = service as Api.IDimensionZoneCatalogService;
            if (zones == null)
            {
                return false;
            }

            IReadOnlyList<Api.DimensionZoneDefinition> definitions =
                zones.GetZoneDefinitions(dimensionId, false);
            for (int i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].Kind, biomeId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
