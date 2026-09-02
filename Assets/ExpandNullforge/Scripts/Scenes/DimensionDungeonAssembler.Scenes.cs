using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Zones;
using PugTilemap;
using Pug.UnityExtensions;
using PugWorldGen;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// Placing one scene inside a dungeon, and checking its tiles resolve.
    /// </summary>
    public static partial class DimensionDungeonAssembler
    {
        /// <summary>
        /// The one-handmade-room dungeon: vanilla's SingleCustomSceneDungeon, reproduced.
        /// </summary>
        /// <remarks>
        /// The absences here are as deliberate as the presences. No DungeonFillCD keeps the fill
        /// system from prepending a shell room — the scene IS the floor. No path rules, no
        /// templates. One Center room sized to the scene, one scene group that must place
        /// exactly once, and a reserved radius so other dungeons keep their distance.
        /// </remarks>
        private static bool TryAssembleSingleScene(
            EntityManager entityManager,
            DynamicBuffer<DungeonBiomeSpawnTableBuffer> biomeTable,
            DimensionDungeonDefinition definition)
        {
            string sceneName = definition.SingleScene.SceneName;
            int2 size;
            if (!DimensionCustomSceneRegistry.TryGetSize(sceneName, out size))
            {
                DimensionFrameworkLog.Warning(
                    "Dungeon '" + definition.DungeonId + "' is a single " +
                    "handmade room named '" + sceneName + "', which is not registered. It was " +
                    "skipped.");
                return false;
            }

            int sceneRadius = (int)math.ceil(0.5f * math.length(new float2(size.x, size.y)));
            // Vanilla clamps single-scene dungeons to 120 tiles of radius at authoring time.
            sceneRadius = math.min(sceneRadius, 120);
            int reserved = math.max(definition.SingleScene.ReservedRadius, sceneRadius) + 2;

            Entity dungeon = entityManager.CreateEntity();
            entityManager.AddComponentData(dungeon, new DungeonAreaCD
            {
                seed = 0,
                radius = reserved,
                placementRadius = reserved,
                shapeFreq = 1f,
                shapeAmp = 0f,
                // The same one control the generated path reads. A single-scene dungeon used
                // to hardcode true, so clearing "Keep Creatures Out" did nothing for exactly
                // the dungeons small enough for a wandering creature to matter in.
                blockSpawns = definition.BlockOtherSpawns
            });

            entityManager.AddComponent<Prefab>(dungeon);
            entityManager.AddComponentData(dungeon, LocalTransform.Identity);
            entityManager.AddComponentData(dungeon, default(SpawnAreaEntityRefCD));
            entityManager.AddComponent<DungeonGenerationInitializationCD>(dungeon);
            entityManager.AddComponent<BlockSaveCD>(dungeon);
            entityManager.AddBuffer<DungeonRoomBuffer>(dungeon);
            entityManager.AddBuffer<DungeonPathBuffer>(dungeon);
            entityManager.AddBuffer<DungeonSpawnedObjectBuffer>(dungeon);

            entityManager.AddBuffer<DungeonRoomPlacementBuffer>(dungeon).Add(
                new DungeonRoomPlacementBuffer
                {
                    Value = new DungeonRoomPlacement
                    {
                        algorithm = RoomPlacementAlgorithm.Center,
                        roomType = RoomFlags.Main,
                        amount = new RangeInt { min = 1, max = 1 },
                        size = new RangeInt { min = sceneRadius, max = sceneRadius },
                        canIntersectRooms = RoomFlags.None,
                        angleMin = 0f,
                        angleMax = math.PI * 2f
                    }
                });

            FixedList4096Bytes<DungeonCustomScene> scenes = new FixedList4096Bytes<DungeonCustomScene>();
            scenes.Add(new DungeonCustomScene
            {
                name = new FixedString64Bytes(sceneName),
                size = size
            });
            entityManager.AddBuffer<DungeonCustomSceneGroupBuffer>(dungeon).Add(
                new DungeonCustomSceneGroupBuffer
                {
                    roomType = RoomFlags.CustomScene | RoomFlags.Main,
                    minSpawns = 1,
                    maxSpawns = 1,
                    customScenes = scenes
                });

            Entity spawnTable = entityManager.CreateEntity();
            entityManager.AddBuffer<DungeonSpawnTableBuffer>(spawnTable).Add(
                new DungeonSpawnTableBuffer
                {
                    name = new FixedString32Bytes(Shorten(definition.DungeonId)),
                    prefabEntity = dungeon,
                    spawnValue = 0f,
                    spawnChance = definition.SpawnChance
                });

            PrototypesById[definition.DungeonId] = dungeon;
            biomeTable.Add(new DungeonBiomeSpawnTableBuffer
            {
                tableEntity = spawnTable,
                biome = ForBothWorldTypes(ResolveBindingBiome(definition.BiomeId)),
                minDistanceFromCoreInClassicWorlds = definition.MinDistanceFromCentre
            });

            return true;
        }

        /// <summary>
        /// The identity-gate preflight: warns about every tile and block the dungeon generator
        /// would silently drop, while the author can still do something about it.
        /// </summary>
        /// <remarks>
        /// Two gates, both linear scans of the object database. Room scene tiles resolve
        /// (tileset, tileType) to an object — a tileset used purely as decoration, with its
        /// block objects switched off, has no entry and its tiles vanish from dungeon rooms
        /// while looking perfect in the open world. Outline blocks resolve the other way:
        /// an object that is neither a tile nor a prefab stamps as nothing. Warn, never
        /// refuse — a dropped tile degrades one room; refusing drops the dungeon.
        /// </remarks>
        private static void VerifyDungeonTilesResolve(
            EntityManager entityManager,
            DimensionDungeonDefinition definition)
        {
            EntityQuery databaseQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            if (databaseQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            BlobAssetReference<PugDatabase.PugDatabaseBank> bank =
                databaseQuery.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;

            HashSet<long> warned = new HashSet<long>();
            for (int g = 0; g < definition.RoomGroups.Count; g++)
            {
                IReadOnlyList<string> sceneNames = definition.RoomGroups[g].SceneNames;
                for (int s = 0; s < sceneNames.Count; s++)
                {
                    DimensionCustomSceneDefinition scene;
                    if (!DimensionCustomSceneRegistry.TryGet(sceneNames[s], out scene))
                    {
                        continue;
                    }

                    for (int t = 0; t < scene.Tiles.Count; t++)
                    {
                        DimensionSceneTile tile = scene.Tiles[t];
                        long key = ((long)tile.Tileset << 8) | (long)tile.TileType;
                        if (!warned.Add(key))
                        {
                            continue;
                        }

                        if (PugDatabase.GetObjectData(tile.Tileset, tile.TileType, bank).objectID ==
                            ObjectID.None)
                        {
                            DimensionFrameworkLog.Warning(
                                "Dungeon '" + definition.DungeonId +
                                "' room scene '" + sceneNames[s] + "' uses tileset " +
                                tile.Tileset + " as " + tile.TileType + ", which no object " +
                                "claims — the dungeon generator will drop those tiles. If the " +
                                "tileset's block items are switched off, that is why.");
                        }
                    }
                }
            }

            for (int i = 0; i < definition.OutlineBlockIds.Count; i++)
            {
                ObjectID objectID = PugMod.API.Authoring.GetObjectID(definition.OutlineBlockIds[i]);
                if (objectID == ObjectID.None || !PugDatabase.HasObject(objectID, bank))
                {
                    continue;
                }

                ref PugDatabase.EntityObjectInfo info = ref PugDatabase.GetEntityObjectInfo(objectID, bank);
                if (info.tileType == TileType.none && info.prefabEntities.Length == 0)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' outline " +
                        "block '" + definition.OutlineBlockIds[i] + "' is neither a tile nor " +
                        "a placeable object, so the shell would stamp nothing for that layer.");
                }
            }
        }
    }
}
