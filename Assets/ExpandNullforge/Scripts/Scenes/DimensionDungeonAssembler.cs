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
    /// Builds this mod's dungeons into the world's own dungeon tables, so world generation grows them
    /// the way it grows Core Keeper's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dungeon in Core Keeper is not a special kind of content — it is a handful of ECS components
    /// describing a radius, a room size, and which scenes may fill which kind of room. Everything after
    /// that is the game's: choosing a spot, laying out rooms, carving paths between them, filling the
    /// gaps, keeping wildlife out. So a custom dungeon is an arrangement of things the game can already
    /// place, and this assembler writes that arrangement rather than performing it.
    /// </para>
    /// <para>
    /// THE SCENES ARE REFERENCED BY NAME, which is what makes this possible at all. Rooms are named
    /// custom scenes, and the framework has already put its scenes into the game's scene table by the
    /// time this runs — so a dungeon room is a scene the game can find, and the dungeon generator
    /// stamps it exactly as it stamps a vanilla room.
    /// </para>
    /// <para>
    /// ORDER MATTERS: this must run after the scene table is injected, or a dungeon would name rooms
    /// the game cannot find and generate as empty caves.
    /// </para>
    /// </remarks>
    public static partial class DimensionDungeonAssembler
    {
        private static World assembledWorld;

        /// <summary>
        /// Every blob this assembler built, so a new world or a domain reload can dispose them.
        /// </summary>
        /// <remarks>
        /// The fill and swap blobs are allocated Persistent and live outside any BlobAssetStore,
        /// so nothing disposes them for us. Vanilla's converter parks its blobs in the store the
        /// world owns; ours must keep the handles themselves or leak one set per world load.
        /// </remarks>
        private static readonly List<BlobAssetReference<SpawnTemplateBlob>> OwnedTemplateBlobs =
            new List<BlobAssetReference<SpawnTemplateBlob>>();

        private static readonly List<BlobAssetReference<BlobArray<int>>> OwnedIntBlobs =
            new List<BlobAssetReference<BlobArray<int>>>();

        private static readonly List<BlobAssetReference<BlobArray<float>>> OwnedFloatBlobs =
            new List<BlobAssetReference<BlobArray<float>>>();

        /// <summary>How many dungeons the last assembly added.</summary>
        public static int LastAssembledCount { get; private set; }

        /// <summary>
        /// The assembled prototype entities by dungeon id — what the dimension placement pass
        /// instantiates. Valid only for the assembled world.
        /// </summary>
        private static readonly Dictionary<string, Entity> PrototypesById =
            new Dictionary<string, Entity>(System.StringComparer.Ordinal);

        /// <summary>The assembled prototype for a dungeon id, when the current world has one.</summary>
        public static bool TryGetPrototype(World world, string dungeonId, out Entity prototype)
        {
            prototype = Entity.Null;
            return ReferenceEquals(assembledWorld, world) &&
                   !string.IsNullOrEmpty(dungeonId) &&
                   PrototypesById.TryGetValue(dungeonId, out prototype);
        }

        /// <summary>
        /// Adds this mod's dungeons to the world's spawn tables, once per world.
        /// </summary>
        /// <returns>True when the tables now contain them.</returns>
        public static bool TryAssemble(World world)
        {
            if (world == null || !world.IsCreated || !DimensionDungeonRegistry.HasAny)
            {
                return false;
            }

            if (ReferenceEquals(assembledWorld, world))
            {
                return true;
            }

            EntityManager entityManager = world.EntityManager;

            EntityQuery biomeTableQuery =
                entityManager.CreateEntityQuery(ComponentType.ReadWrite<DungeonBiomeSpawnTableBuffer>());
            if (biomeTableQuery.IsEmptyIgnoreFilter)
            {
                // The world has not built its dungeon tables yet. Not an error — the caller retries.
                return false;
            }

            try
            {
                // A second world in one session means the first world's blobs are unreferenced.
                DisposeOwnedBlobs();

                Entity tableHolder = biomeTableQuery.GetSingletonEntity();
                DynamicBuffer<DungeonBiomeSpawnTableBuffer> biomeTable =
                    entityManager.GetBuffer<DungeonBiomeSpawnTableBuffer>(tableHolder);

                LastAssembledCount = 0;
                IReadOnlyList<DimensionDungeonDefinition> definitions = DimensionDungeonRegistry.All;
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (TryAssembleOne(entityManager, biomeTable, definitions[i]))
                    {
                        LastAssembledCount++;
                    }
                }

                assembledWorld = world;

                if (LastAssembledCount > 0)
                {
                    DimensionFrameworkLog.Info(
                        "Added " + LastAssembledCount +
                        " dungeon(s) to the world's own generation tables.");
                }

                return true;
            }
            catch (System.Exception exception)
            {
                DimensionFrameworkLog.Warning(
                    "Could not add dungeons to the world: " + exception.Message +
                    ". Everything else about the dimension still generates.");
                return false;
            }
        }

        private static bool TryAssembleOne(
            EntityManager entityManager,
            DynamicBuffer<DungeonBiomeSpawnTableBuffer> biomeTable,
            DimensionDungeonDefinition definition)
        {
            if (definition.SingleScene != null)
            {
                return TryAssembleSingleScene(entityManager, biomeTable, definition);
            }

            if (definition.RoomGroups.Count == 0)
            {
                DimensionFrameworkLog.Warning(
                    "Dungeon '" + definition.DungeonId + "' has no rooms, so it was " +
                    "skipped. A dungeon with nothing to place generates as an empty cave.");
                return false;
            }

            VerifyDungeonTilesResolve(entityManager, definition);

            Entity dungeon = entityManager.CreateEntity();

            // Room rules first: the largest room radius feeds the placement radius below, the
            // way vanilla's converter computes it. Authored rules win; the synthesized set is
            // only for a dungeon whose author never opened the shape template.
            List<DungeonRoomPlacement> roomRules = definition.RoomRules.Count > 0
                ? MapAuthoredRoomRules(definition, out int maxRoomRadius)
                : BuildRoomRules(definition, out maxRoomRadius);

            DimensionDungeonShape shape = definition.Shape;
            int radius = shape != null && shape.Radius > 0 ? shape.Radius : definition.Radius;

            // Vanilla refuses rooms further than 200 tiles from a dungeon's centre, so a
            // combination that reaches past that quietly loses its outer rooms.
            if (radius + maxRoomRadius > 195)
            {
                DimensionFrameworkLog.Warning(
                    "Dungeon '" + definition.DungeonId + "' is " +
                    radius + " tiles across with rooms up to " + maxRoomRadius +
                    " — the game refuses rooms past 200 tiles from centre, so its far rooms " +
                    "may be dropped.");
            }

            // The shape template's fields, or the defaults that reproduce the pre-template
            // behavior exactly. The outline wobble only applies when the outline is its own
            // drawn thing — rooms-defined outlines ignore amplitude, as vanilla's do.
            bool defineByRooms = shape == null || shape.OutlineFollowsTheRooms;
            float roomFill = shape != null ? shape.RoomFillSize : definition.RoomSize;
            if (shape != null && defineByRooms && roomFill <= 0f)
            {
                // Vanilla's own quirk (DungeonConverter): rooms-defined outline with no fill
                // size borrows the wobble amplitude, so the shell still hugs the rooms.
                roomFill = shape.OutlineWobble;
            }

            entityManager.AddComponentData(dungeon, new DungeonAreaCD
            {
                // The seed is per-dungeon-kind, not per-instance: world generation reseeds each
                // placement from the world seed, and a constant here only shapes the outline.
                seed = shape != null && shape.Seed > 0
                    ? (uint)shape.Seed
                    : (uint)definition.DungeonId.GetHashCode(),
                radius = radius,
                // Vanilla's own formula: room space plus a margin, so the free-spot scorer
                // reserves enough clearance for the whole dungeon rather than just its core.
                placementRadius = radius + maxRoomRadius + 2,
                shapeAmp = shape != null && shape.HasAShapedOutline ? shape.OutlineWobble : 0.15f,
                shapeFreq = shape != null && shape.HasAShapedOutline
                    ? math.max(0.01f, shape.OutlineBusyness)
                    : 3f,
                // ONE control, on the dungeon itself. An inverted twin on the shape template
                // wins whenever the generator is switched on, because a shape template is always
                // present then — so an author who clears
                // "Keep Creatures Out" on the dungeon watches wildlife stay out anyway, with
                // nothing said. This field is the only writer.
                blockSpawns = definition.BlockOtherSpawns,
                defineShapeByRooms = defineByRooms
            });

            entityManager.AddComponentData(dungeon, new DungeonFillCD
            {
                defineShapeByRooms = defineByRooms,
                roomSize = roomFill,
                pathSize = shape != null ? shape.PathFillSize : definition.PathSize
            });

            // THE FULL PIPELINE ARCHETYPE. Every dungeon system queries a precise component
            // set, and the game's SpawnJob SetComponents LocalTransform and the area ref onto
            // each instance — on an entity without them, that faults the command buffer AND
            // stalls the whole map cell's procedural pass behind a PendingSpawns counter that
            // can never reach zero. Before these lines, an assembled dungeon matched no query
            // at all: it entered the tables, was chosen, and generated nothing.
            //
            // The Prefab tag is load-bearing, not hygiene: without it the completed prototype
            // is itself a live dungeon at the world origin — it generates once there, the
            // pipeline consumes its initialization tag, and every instance cloned afterwards
            // is born tagless and inert.
            entityManager.AddComponent<Prefab>(dungeon);
            entityManager.AddComponentData(dungeon, LocalTransform.Identity);
            entityManager.AddComponentData(dungeon, default(SpawnAreaEntityRefCD));
            entityManager.AddComponent<DungeonGenerationInitializationCD>(dungeon);
            entityManager.AddComponent<BlockSaveCD>(dungeon);
            entityManager.AddBuffer<DungeonRoomBuffer>(dungeon);
            entityManager.AddBuffer<DungeonPathBuffer>(dungeon);
            entityManager.AddBuffer<DungeonSpawnedObjectBuffer>(dungeon);

            DynamicBuffer<DungeonRoomPlacementBuffer> roomPlacements =
                entityManager.AddBuffer<DungeonRoomPlacementBuffer>(dungeon);
            for (int i = 0; i < roomRules.Count; i++)
            {
                roomPlacements.Add(new DungeonRoomPlacementBuffer { Value = roomRules[i] });
            }

            // Corridors. Authored path rules win; the default is one minimum spanning tree over
            // every room — it joins everything with the least corridor, and it is the pass that
            // promotes rooms to End and Connecting, without which scene groups for those roles
            // could never match.
            DynamicBuffer<DungeonPathPlacementBuffer> pathPlacements =
                entityManager.AddBuffer<DungeonPathPlacementBuffer>(dungeon);
            if (definition.PathRules.Count > 0)
            {
                for (int i = 0; i < definition.PathRules.Count; i++)
                {
                    DimensionDungeonPathRule rule = definition.PathRules[i];
                    if (rule.Placement == DimensionPathPlacement.NoPaths)
                    {
                        continue;
                    }

                    pathPlacements.Add(new DungeonPathPlacementBuffer
                    {
                        Value = new DungeonPathPlacement
                        {
                            // The Studio enums are value-aligned with the game's on purpose,
                            // which is what makes this cast honest rather than lucky.
                            placement = (PathPlacementAlgorithm)(int)rule.Placement,
                            // Vanilla strips the Filler bit here: fill rooms are the shell, and
                            // corridors that target the shell would tunnel through everything.
                            roomType = ((RoomFlags)(int)rule.Kind) & ~RoomFlags.Fill,
                            amount = new RangeInt
                            {
                                min = rule.MinCount,
                                // The game rolls max-exclusive; the Studio promises inclusive.
                                max = math.max(rule.MinCount, rule.MaxCount) + 1
                            },
                            canIntersectPaths = rule.MayCrossPaths
                                ? (RoomFlags)(int)rule.MayCrossPathKinds
                                : RoomFlags.None,
                            canIntersectRooms = rule.MayCrossRooms
                                ? (RoomFlags)(int)rule.MayCrossRoomKinds
                                : RoomFlags.None,
                            straightPaths = rule.Straight,
                            pathStartRoomType = (RoomFlags)(int)rule.StartsFrom,
                            pathEndRoomType = (RoomFlags)(int)rule.EndsAt,
                            width = NormalizedCorridorWidth(definition, rule.Width)
                        }
                    });
                }
            }

            if (pathPlacements.Length == 0)
            {
                pathPlacements.Add(new DungeonPathPlacementBuffer
                {
                    Value = new DungeonPathPlacement
                    {
                        placement = PathPlacementAlgorithm.MinSpanningTree,
                        roomType = RoomFlags.Main | RoomFlags.Entrance,
                        amount = new RangeInt { min = 0, max = 0 },
                        canIntersectPaths = RoomFlags.None,
                        canIntersectRooms = RoomFlags.None,
                        straightPaths = false,
                        pathStartRoomType = RoomFlags.Entrance,
                        pathEndRoomType = RoomFlags.End,
                        // 5, not 4: the carve test covers integer offsets |z| ≤ w/2 + 0.1, so 4
                        // always carved five tiles anyway. Storing the odd number keeps the
                        // width the code says equal to the corridor the player walks.
                        width = 5f
                    }
                });
            }

            DynamicBuffer<DungeonCustomSceneGroupBuffer> groups =
                entityManager.AddBuffer<DungeonCustomSceneGroupBuffer>(dungeon);

            for (int i = 0; i < definition.RoomGroups.Count; i++)
            {
                DimensionDungeonRoomGroup group = definition.RoomGroups[i];
                if (group.SceneNames.Count == 0)
                {
                    continue;
                }

                FixedList4096Bytes<DungeonCustomScene> scenes = new FixedList4096Bytes<DungeonCustomScene>();
                for (int s = 0; s < group.SceneNames.Count; s++)
                {
                    string sceneName = group.SceneNames[s];
                    int2 size;
                    if (!DimensionCustomSceneRegistry.TryGetSize(sceneName, out size))
                    {
                        DimensionFrameworkLog.Warning(
                            "Dungeon '" + definition.DungeonId + "' names room scene '" +
                            sceneName + "', which is not registered. That room will not appear.");
                        continue;
                    }

                    // The list is fixed-size; overflowing it would throw and cost the whole dungeon,
                    // so stop and say so instead.
                    if (scenes.Length >= scenes.Capacity)
                    {
                        DimensionFrameworkLog.Warning(
                            "Dungeon '" + definition.DungeonId + "' has more room " +
                            "scenes than the game can hold in one group; the rest were left out.");
                        break;
                    }

                    scenes.Add(new DungeonCustomScene
                    {
                        name = new FixedString64Bytes(sceneName),
                        size = size
                    });
                }

                if (scenes.Length == 0)
                {
                    continue;
                }

                groups.Add(new DungeonCustomSceneGroupBuffer
                {
                    roomType = ToRoomFlags(group.Role),
                    minSpawns = group.MinRooms,
                    maxSpawns = group.MaxRooms,
                    customScenes = scenes
                });
            }

            if (groups.Length == 0)
            {
                entityManager.DestroyEntity(dungeon);
                return false;
            }

            AddAuthoredFillings(entityManager, dungeon, definition);
            AddFillShell(entityManager, dungeon, definition, shape);
            EnsureTemplateBufferPair(entityManager, dungeon);
            AddSwaps(entityManager, dungeon, definition);

            Entity spawnTable = entityManager.CreateEntity();
            DynamicBuffer<DungeonSpawnTableBuffer> entries =
                entityManager.AddBuffer<DungeonSpawnTableBuffer>(spawnTable);

            entries.Add(new DungeonSpawnTableBuffer
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
                // The same biome for both world types: a dimension is its own world with its own
                // layout, so the classic/full-release split the base game makes does not apply to it.
                biome = ForBothWorldTypes(ResolveBindingBiome(definition.BiomeId)),
                minDistanceFromCoreInClassicWorlds = definition.MinDistanceFromCentre
            });

            return true;
        }

        /// <summary>
        /// A name short enough for the fixed-size field the game stores it in.
        /// </summary>
        /// <remarks>
        /// The name is only used for diagnostics, so truncating loses nothing that matters — whereas
        /// overflowing a FixedString32 throws and costs the dungeon.
        /// </remarks>
        private static string Shorten(string value)
        {
            const int maxBytes = 29;
            if (string.IsNullOrEmpty(value) || value.Length <= maxBytes)
            {
                return value ?? string.Empty;
            }

            return value.Substring(value.Length - maxBytes);
        }

        /// <summary>Forgets which world was assembled and releases its blobs. Tests and shutdown.</summary>
        public static void ResetForNewWorld()
        {
            assembledWorld = null;
            PrototypesById.Clear();
            LastAssembledCount = 0;
            DisposeOwnedBlobs();
        }
    }
}
