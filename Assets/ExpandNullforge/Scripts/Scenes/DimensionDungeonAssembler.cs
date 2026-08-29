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
    public static class DimensionDungeonAssembler
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
                // ONE control, on the dungeon itself. The shape template used to carry an
                // inverted twin of this bit, and because a shape template is present whenever
                // the generator is switched on, the twin always won — an author who cleared
                // "Keep Creatures Out" on the dungeon watched wildlife stay out anyway, with
                // nothing said. The twin was removed; this field is the only writer.
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
        /// Maps the Studio's authored room rules onto the game's placement structs, honoring the
        /// Studio's inclusive-range promise against the game's max-exclusive rolls.
        /// </summary>
        /// <remarks>
        /// Two authored conveniences are resolved here rather than at authoring time, because
        /// only the runtime knows the game's quirks: a fixed-spot rule is refused with a warning
        /// (the game's switch has no case for it — it would silently place nothing), and a rule
        /// set with no centre room gets a synthetic one, since FromOtherRooms errors out on an
        /// empty room buffer.
        /// </remarks>
        private static List<DungeonRoomPlacement> MapAuthoredRoomRules(
            DimensionDungeonDefinition definition,
            out int maxRoomRadius)
        {
            List<DungeonRoomPlacement> rules = new List<DungeonRoomPlacement>();
            maxRoomRadius = 0;
            bool hasCentre = false;
            bool growsFromOthers = false;

            for (int i = 0; i < definition.RoomRules.Count; i++)
            {
                DimensionDungeonRoomRule rule = definition.RoomRules[i];
                if (rule.Placement == DimensionRoomPlacement.NotPlaced)
                {
                    continue;
                }

                if (rule.Placement == DimensionRoomPlacement.AtAFixedSpot)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' has a room " +
                        "rule placed 'at a fixed spot', which Core Keeper never implemented — " +
                        "the game's room placer has no code for it and would silently place " +
                        "nothing. The rule was skipped.");
                    continue;
                }

                RoomPlacementAlgorithm algorithm = (RoomPlacementAlgorithm)(int)rule.Placement;
                hasCentre |= algorithm == RoomPlacementAlgorithm.Center;
                growsFromOthers |= algorithm == RoomPlacementAlgorithm.FromOtherRooms;

                int minRadius = math.max(1, rule.MinRadius);
                int maxRadius = math.max(minRadius, rule.MaxRadius);
                maxRoomRadius = math.max(maxRoomRadius, maxRadius);

                // Vanilla's own restriction: overlap is only honored for the two algorithms
                // whose placement can actually retry around it.
                bool canOverlap = rule.MayOverlapOtherRooms &&
                                  (algorithm == RoomPlacementAlgorithm.Random ||
                                   algorithm == RoomPlacementAlgorithm.FromOtherRooms);

                // (0,0) angles mean the author never touched the field; a literal zero would
                // pin every room due east, so unset means anywhere.
                float angleMin = rule.AngleMinDegrees;
                float angleMax = rule.AngleMaxDegrees;
                if (angleMax <= angleMin)
                {
                    angleMin = 0f;
                    angleMax = 360f;
                }

                rules.Add(new DungeonRoomPlacement
                {
                    algorithm = algorithm,
                    roomType = rule.Kind == DimensionRoomKind.None
                        ? RoomFlags.Main
                        : (RoomFlags)(int)rule.Kind,
                    amount = new RangeInt
                    {
                        min = math.max(0, rule.MinCount),
                        max = math.max(rule.MinCount, rule.MaxCount) + 1
                    },
                    size = new RangeInt
                    {
                        min = minRadius,
                        // FromOtherRooms is the one algorithm the game rolls max-INCLUSIVE, so
                        // adding one there would grow rooms past what was authored.
                        max = algorithm == RoomPlacementAlgorithm.FromOtherRooms
                            ? maxRadius
                            : maxRadius + 1
                    },
                    fromExistingSpacing = new RangeInt
                    {
                        min = math.max(0, rule.MinSpacing),
                        max = math.max(rule.MinSpacing, rule.MaxSpacing)
                    },
                    fromExistingPerpendicular = rule.StraightPaths,
                    canIntersectRooms = canOverlap
                        ? (RoomFlags)(int)rule.MayOverlapKinds
                        : RoomFlags.None,
                    angleMin = math.radians(angleMin),
                    angleMax = math.radians(angleMax),
                    alignAngleWithCoreRadial = rule.AlignedWithTheCore
                });
            }

            if (rules.Count == 0)
            {
                // Every authored rule was refused or disabled; fall back to synthesis so the
                // dungeon still generates instead of matching the pipeline with nothing to do.
                return BuildRoomRules(definition, out maxRoomRadius);
            }

            if (!hasCentre && growsFromOthers)
            {
                rules.Insert(0, NewRule(RoomPlacementAlgorithm.Center, 1, 1, 4, 6));
                maxRoomRadius = math.max(maxRoomRadius, 6);
            }

            WarnWhenAuthoredRoomsCannotHoldTheirScenes(definition, rules);

            return rules;
        }

        /// <summary>
        /// Says so when an authored room rule can roll smaller than a scene it may have to hold.
        /// </summary>
        /// <remarks>
        /// A warning, not a clamp, on vanilla's own precedent: the game never checks fit (its
        /// City quarters overflow rooms half their size and live with the consequences), and an
        /// author who chose a number gets that number. What the overflow actually costs is
        /// spelled out so the author can decide: the overhang escapes the room's spawn-block
        /// circle, and spacing and corridor routing measure the too-small radius, so neighbours
        /// and corridors may legally stamp over the scene's edge.
        /// </remarks>
        private static void WarnWhenAuthoredRoomsCannotHoldTheirScenes(
            DimensionDungeonDefinition definition,
            List<DungeonRoomPlacement> rules)
        {
            for (int g = 0; g < definition.RoomGroups.Count; g++)
            {
                DimensionDungeonRoomGroup group = definition.RoomGroups[g];
                RoomFlags groupFlags = ToRoomFlags(group.Role);

                int groupFit = 0;
                string biggestScene = null;
                for (int s = 0; s < group.SceneNames.Count; s++)
                {
                    if (DimensionCustomSceneRegistry.TryGetFitRadius(group.SceneNames[s], out int fit) &&
                        fit > groupFit)
                    {
                        groupFit = fit;
                        biggestScene = group.SceneNames[s];
                    }
                }

                if (groupFit == 0)
                {
                    continue;
                }

                for (int r = 0; r < rules.Count; r++)
                {
                    // Rooms are claimed by ANY shared flag bit, so any intersecting rule can
                    // produce a room this group's scenes land in.
                    if ((rules[r].roomType & groupFlags) == RoomFlags.None)
                    {
                        continue;
                    }

                    // size.max is stored max-exclusive for most algorithms; min is the honest
                    // floor either way, and the floor is what a guarantee needs.
                    if (rules[r].size.min < groupFit)
                    {
                        DimensionFrameworkLog.Warning(
                            "Dungeon '" + definition.DungeonId + "': a " +
                            group.Role + " room can roll radius " + rules[r].size.min +
                            ", but '" + biggestScene + "' needs " + groupFit +
                            " to fit. The overhang will poke past the room's protected circle, " +
                            "and neighbouring rooms or corridors may stamp over its edge. Raise " +
                            "the room rule's size to " + groupFit + " to guarantee the fit.");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The corridor band the game will actually carve, as the stored width.
        /// </summary>
        /// <remarks>
        /// The carve test covers integer offsets <c>|z| ≤ width/2 + 0.1</c> around the
        /// room-to-room centre line, so every corridor is an odd tile count and an even request
        /// silently gets the next odd band. Storing the effective count keeps authored numbers
        /// truthful; the log line teaches the rule the one time it changes something.
        /// </remarks>
        private static float NormalizedCorridorWidth(
            DimensionDungeonDefinition definition,
            float authoredWidth)
        {
            int effective = DimensionSceneGeometry.EffectiveCorridorTiles(authoredWidth);
            if (math.abs(authoredWidth - effective) > 0.05f)
            {
                DimensionFrameworkLog.Info(
                    "Dungeon '" + definition.DungeonId + "': corridors are " +
                    "always an odd number of tiles wide, centred on the line between rooms — " +
                    "the authored width " + authoredWidth + " carves " + effective + " tiles.");
            }

            return effective;
        }

        /// <summary>
        /// Gives the dungeon its floor-and-wall shell, built from the authored outline blocks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The shell is the space between the rooms: the fill system prepends Fill rooms and
        /// paths, and the template built here is what stamps tiles into them. Layer order is the
        /// authored order, bottom first — each path layer spawns on the one before it, exactly
        /// the chaining vanilla's synthetic template uses.
        /// </para>
        /// <para>
        /// STRICTLY BINARY: authored outline blocks produce both template buffers; none produce
        /// neither. The game's template-assignment query requires both buffers, so an empty pair
        /// would satisfy the query and then stamp nothing — the silent kind of nothing.
        /// </para>
        /// </remarks>
        private static void AddFillShell(
            EntityManager entityManager,
            Entity dungeon,
            DimensionDungeonDefinition definition,
            DimensionDungeonShape shape)
        {
            if (definition.OutlineBlockIds.Count == 0)
            {
                return;
            }

            List<ObjectID> layers = new List<ObjectID>();
            for (int i = 0; i < definition.OutlineBlockIds.Count; i++)
            {
                string blockId = definition.OutlineBlockIds[i];
                if (string.IsNullOrEmpty(blockId))
                {
                    continue;
                }

                ObjectID objectID = PugMod.API.Authoring.GetObjectID(blockId);
                if (objectID == ObjectID.None)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' outlines " +
                        "with block '" + blockId + "', which is not a known object. That " +
                        "layer was left out of the shell.");
                    continue;
                }

                layers.Add(objectID);
            }

            if (layers.Count == 0)
            {
                DimensionFrameworkLog.Warning(
                    "Dungeon '" + definition.DungeonId + "' authored outline " +
                    "blocks but none resolved, so it generates without a shell — rooms will " +
                    "stamp onto raw cave.");
                return;
            }

            bool rect = shape != null && shape.IsRectangular;

            // THE CARVE ROWS, and why a dungeon is sealed without them. The widened Fill twins
            // stamp the outline layers as solid wall mass, and they carry ONLY the Fill flag.
            // A REAL corridor carries its endpoints' flags and matches templates by ALL bits —
            // so with nothing but the shell's Fill row, a real corridor matches no row, stamps
            // nothing, and the wall mass poured over its band simply stays: every room sealed
            // in rock. Vanilla never hits this because its dungeons always author path
            // templates (PathCityDungeon and kin). These two rows are that authored floor,
            // synthesized: ground carved down the corridor band and across unclaimed rooms,
            // allowed onto empty tiles and the shell's own wall blocks — the City pattern, which
            // is also what carves each doorway dead-centred on the corridor for free. They sit
            // AFTER any authored fillings (an author's row wins) and BEFORE the Fill rows (fill
            // twins skip them — no Fill bit — and still find their wall).
            RoomFlags everyRealFlag = RoomFlags.Main | RoomFlags.Entrance | RoomFlags.End |
                                      RoomFlags.Connecting | RoomFlags.CustomScene;

            BlobAssetReference<SpawnTemplateBlob> carvePathBlob = BuildCarveBlob(layers, true);
            OwnedTemplateBlobs.Add(carvePathBlob);
            Entity carvePathHolder = entityManager.CreateEntity();
            entityManager.AddBuffer<DungeonPathSpawnTemplateBuffer>(carvePathHolder).Add(
                new DungeonPathSpawnTemplateBuffer { Value = carvePathBlob });
            EnsureBuffer<DungeonPathTemplateBuffer>(entityManager, dungeon).Add(
                new DungeonPathTemplateBuffer
                {
                    spawnTemplateBufferEntity = carvePathHolder,
                    flags = everyRealFlag,
                    minimumSizeRequirement = 0
                });

            BlobAssetReference<SpawnTemplateBlob> carveRoomBlob = BuildCarveBlob(layers, false);
            OwnedTemplateBlobs.Add(carveRoomBlob);
            Entity carveRoomHolder = entityManager.CreateEntity();
            entityManager.AddBuffer<DungeonNodeSpawnTemplateBuffer>(carveRoomHolder).Add(
                new DungeonNodeSpawnTemplateBuffer { Value = carveRoomBlob });
            EnsureBuffer<DungeonNodeTemplateBuffer>(entityManager, dungeon).Add(
                new DungeonNodeTemplateBuffer
                {
                    spawnTemplateBufferEntity = carveRoomHolder,
                    flags = everyRealFlag,
                    minimumSizeRequirement = 0
                });

            BlobAssetReference<SpawnTemplateBlob> roomBlob = BuildFillBlob(layers, false, rect);
            OwnedTemplateBlobs.Add(roomBlob);
            Entity roomHolder = entityManager.CreateEntity();
            entityManager.AddBuffer<DungeonNodeSpawnTemplateBuffer>(roomHolder).Add(
                new DungeonNodeSpawnTemplateBuffer { Value = roomBlob });
            // Ensure, never re-Add: AddBuffer on an entity that already has one RESETS it,
            // and the authored filling rows land in this buffer before the shell's row does.
            EnsureBuffer<DungeonNodeTemplateBuffer>(entityManager, dungeon).Add(
                new DungeonNodeTemplateBuffer
                {
                    spawnTemplateBufferEntity = roomHolder,
                    flags = RoomFlags.Fill,
                    minimumSizeRequirement = 0
                });

            BlobAssetReference<SpawnTemplateBlob> pathBlob = BuildFillBlob(layers, true, rect);
            OwnedTemplateBlobs.Add(pathBlob);
            Entity pathHolder = entityManager.CreateEntity();
            entityManager.AddBuffer<DungeonPathSpawnTemplateBuffer>(pathHolder).Add(
                new DungeonPathSpawnTemplateBuffer { Value = pathBlob });
            EnsureBuffer<DungeonPathTemplateBuffer>(entityManager, dungeon).Add(
                new DungeonPathTemplateBuffer
                {
                    spawnTemplateBufferEntity = pathHolder,
                    flags = RoomFlags.Fill,
                    minimumSizeRequirement = 0
                });
        }

        /// <summary>
        /// The authored room fillings — the procedural content layer where a dungeon's
        /// character lives.
        /// </summary>
        /// <remarks>
        /// <para>
        /// AUTHORED ROWS BEFORE THE SHELL'S, on purpose. The game takes the FIRST template row
        /// whose flags match a room — even a row with an empty template list eats the room.
        /// Vanilla's own converter ordering puts the shell's Fill row first by accident of
        /// component order; ours puts authored content first so an author's Main-room filling
        /// can never be shadowed by the shell, and the shell's Fill twins still match their own
        /// Fill row because authored rows only carry the Filler bit when the author chose it.
        /// </para>
        /// <para>
        /// Corridors match by ALL their flag bits, not any — the classic silent trap — so a
        /// corridor filling's mask is broadened to every flag a path can accumulate unless the
        /// author's mask already covers more.
        /// </para>
        /// </remarks>
        private static void AddAuthoredFillings(
            EntityManager entityManager,
            Entity dungeon,
            DimensionDungeonDefinition definition)
        {
            if (definition.Fillings.Count == 0)
            {
                return;
            }

            for (int i = 0; i < definition.Fillings.Count; i++)
            {
                DimensionDungeonFillingRule filling = definition.Fillings[i];
                if (filling.Entries.Count == 0)
                {
                    // An empty filling row would still match rooms and BLOCK later rows —
                    // the game's own first-match-wins rule. Never emit one.
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' filling '" +
                        filling.FillingId + "' places nothing, so it was left out — an empty " +
                        "filling would eat its rooms and block every filling after it.");
                    continue;
                }

                BlobAssetReference<SpawnTemplateBlob> blob =
                    BuildAuthoredFillingBlob(definition, filling);
                if (!blob.IsCreated)
                {
                    continue;
                }

                OwnedTemplateBlobs.Add(blob);

                Entity holder = entityManager.CreateEntity();
                if (filling.FillsCorridors)
                {
                    entityManager.AddBuffer<DungeonPathSpawnTemplateBuffer>(holder).Add(
                        new DungeonPathSpawnTemplateBuffer { Value = blob });
                    RoomFlags mask = BroadenCorridorMask(filling.FillsRooms);
                    EnsureBuffer<DungeonPathTemplateBuffer>(entityManager, dungeon).Add(
                        new DungeonPathTemplateBuffer
                        {
                            spawnTemplateBufferEntity = holder,
                            flags = mask,
                            minimumSizeRequirement = filling.OnlyIfAtLeastThisBig
                        });
                }
                else
                {
                    entityManager.AddBuffer<DungeonNodeSpawnTemplateBuffer>(holder).Add(
                        new DungeonNodeSpawnTemplateBuffer { Value = blob });
                    EnsureBuffer<DungeonNodeTemplateBuffer>(entityManager, dungeon).Add(
                        new DungeonNodeTemplateBuffer
                        {
                            spawnTemplateBufferEntity = holder,
                            flags = filling.FillsRooms == Authoring.DimensionRoomKind.None
                                ? RoomFlags.Main
                                : (RoomFlags)(int)filling.FillsRooms,
                            minimumSizeRequirement = filling.OnlyIfAtLeastThisBig
                        });
                }
            }
        }

        /// <summary>
        /// One authored filling as the game's own spawn-template blob, entry for entry in
        /// paint order, with the chaining law: an empty may-land-on list means bare ground.
        /// </summary>
        private static BlobAssetReference<SpawnTemplateBlob> BuildAuthoredFillingBlob(
            DimensionDungeonDefinition definition,
            DimensionDungeonFillingRule filling)
        {
            BlobBuilder builder = new BlobBuilder(Allocator.Temp);
            ref SpawnTemplateBlob root = ref builder.ConstructRoot<SpawnTemplateBlob>();
            root.shapeAmp = filling.FillsCorridors ? 0f : 0.5f;
            root.shapeFreq = filling.FillsCorridors ? 1f : 2f;

            BlobBuilderArray<SpawnEntryCD> blobEntries =
                builder.Allocate(ref root.entries, filling.Entries.Count);
            for (int i = 0; i < filling.Entries.Count; i++)
            {
                DimensionDungeonFillingEntry source = filling.Entries[i];
                ref SpawnEntryCD entry = ref blobEntries[i];

                ObjectID objectID = PugMod.API.Authoring.GetObjectID(source.ObjectId);
                if (objectID == ObjectID.None)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' filling '" +
                        filling.FillingId + "' places '" + source.ObjectId + "', which is not " +
                        "a known object. That layer stamps nothing.");
                    entry.disabled = true;
                }

                entry.objectToSpawn.objectID = objectID;
                entry.objectToSpawn.amount = new RangeInt
                {
                    min = source.StackCount,
                    max = source.StackCount
                };
                BlobBuilderArray<int> variations =
                    builder.Allocate(ref entry.objectToSpawn.variations, 1);
                variations[0] = source.Look;
                BlobBuilderArray<float> probabilities =
                    builder.Allocate(ref entry.objectToSpawn.accumulatedVariationProbability, 1);
                probabilities[0] = 1f;

                entry.chanceToAppearAtAll = source.ChanceToAppear;
                entry.randomChance = source.Density;
                entry.amount = new RangeInt { min = source.MinPatches, max = source.MaxPatches };
                entry.algorithm = (SpawnAlgorithm)source.PatchShape;
                entry.shapeSize = new Range
                {
                    min = math.max(0.02f, source.PatchSize),
                    max = math.max(0.02f, source.PatchSize)
                };

                // The chaining law: nothing named means bare ground (the empty-cell marker);
                // named ids mean it must sit on one of them, exactly as vanilla chains layers.
                if (source.MayLandOn == null || source.MayLandOn.Length == 0)
                {
                    ObjectID none = ObjectID.None;
                    entry.canSpawnOn.Add(in none);
                }
                else
                {
                    AddResolvedIds(
                        ref entry.canSpawnOn, source.MayLandOn, definition, filling, "may land on");
                }

                if (source.NeverOn != null && source.NeverOn.Length > 0)
                {
                    AddResolvedIds(
                        ref entry.canNotSpawnOn, source.NeverOn, definition, filling, "never on");
                }

                if (!string.IsNullOrEmpty(source.ChestLoot))
                {
                    LootTableID lootTable;
                    // Vanilla names first, then the mod's own registered tables — the same
                    // resolver every loot field uses, so a chest can roll a table the author
                    // invented as easily as AncientChest.
                    if (Loot.DimensionLootTableRegistry.TryResolve(source.ChestLoot, out lootTable))
                    {
                        entry.containLoot = lootTable;
                    }
                    else
                    {
                        DimensionFrameworkLog.Warning(
                            "Dungeon '" + definition.DungeonId + "' filling '" +
                            filling.FillingId + "' names chest loot '" + source.ChestLoot +
                            "', which is neither one of the game's loot tables nor one of this " +
                            "mod's. The chest places with its ordinary contents.");
                    }
                }
            }

            BlobAssetReference<SpawnTemplateBlob> blob =
                builder.CreateBlobAssetReference<SpawnTemplateBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        /// <summary>Resolves ids into the entry's fixed list, honoring the game's 15-id cap.</summary>
        private static void AddResolvedIds(
            ref FixedList64Bytes<ObjectID> target,
            string[] ids,
            DimensionDungeonDefinition definition,
            DimensionDungeonFillingRule filling,
            string fieldName)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                {
                    continue;
                }

                if (target.Length >= 15)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' filling '" +
                        filling.FillingId + "' lists more than 15 '" + fieldName + "' objects; " +
                        "the game's field holds 15, so the rest were dropped.");
                    return;
                }

                ObjectID id = PugMod.API.Authoring.GetObjectID(ids[i]);
                if (id == ObjectID.None)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' filling '" +
                        filling.FillingId + "' " + fieldName + " '" + ids[i] + "', which is not " +
                        "a known object; that condition was dropped.");
                    continue;
                }

                target.Add(in id);
            }
        }

        /// <summary>
        /// The flag mask an authored corridor filling matches paths with.
        /// </summary>
        /// <remarks>
        /// Paths carry BOTH endpoints' flags and match by ALL bits, so a narrow authored mask
        /// would never apply — it broadens to every flag a REAL path can wear. What it must
        /// never do is add Fill on its own: the widened fill twins carry only that bit, and a
        /// corridor row covering it steals them from the shell, replacing the wall mass around
        /// every corridor with more corridor floor. Fill rides through exactly when the author
        /// chose the Filler kind themselves.
        /// </remarks>
        internal static RoomFlags BroadenCorridorMask(Authoring.DimensionRoomKind fillsRooms)
        {
            return (RoomFlags)(int)fillsRooms |
                   RoomFlags.Main | RoomFlags.Entrance | RoomFlags.End |
                   RoomFlags.Connecting | RoomFlags.CustomScene;
        }

        private static DynamicBuffer<T> EnsureBuffer<T>(EntityManager entityManager, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            return entityManager.HasBuffer<T>(entity)
                ? entityManager.GetBuffer<T>(entity)
                : entityManager.AddBuffer<T>(entity);
        }

        /// <summary>
        /// The game's template assignment queries for BOTH buffers at once: a dungeon with
        /// node rows but no path buffer (or the reverse) matches nothing and fills nothing,
        /// silently. Whenever either side has rows, the other must at least exist.
        /// </summary>
        private static void EnsureTemplateBufferPair(EntityManager entityManager, Entity dungeon)
        {
            bool hasNode = entityManager.HasBuffer<DungeonNodeTemplateBuffer>(dungeon);
            bool hasPath = entityManager.HasBuffer<DungeonPathTemplateBuffer>(dungeon);
            if (hasNode && !hasPath)
            {
                entityManager.AddBuffer<DungeonPathTemplateBuffer>(dungeon);
            }
            else if (hasPath && !hasNode)
            {
                entityManager.AddBuffer<DungeonNodeTemplateBuffer>(dungeon);
            }
        }

        /// <summary>
        /// The floor a real corridor or unclaimed room lays through the wall mass: one entry,
        /// the outline's ground layer, allowed onto empty tiles and the outline's wall blocks.
        /// </summary>
        /// <remarks>
        /// The spawn-rule grammar carries the whole doorway story: a leading None admits empty
        /// tiles, every other listed id is a block this floor may REPLACE. Listing the shell's
        /// own wall layers means the corridor eats through the wall band exactly where its own
        /// centred, odd-width band runs — a doorway that cannot be off-centre because it is
        /// computed in the corridor's frame. Anything not listed (a scene's authored interior)
        /// stops the carve dead, which is the polite half of the same rule.
        /// </remarks>
        private static BlobAssetReference<SpawnTemplateBlob> BuildCarveBlob(
            List<ObjectID> layers,
            bool forPaths)
        {
            BlobBuilder builder = new BlobBuilder(Allocator.Temp);
            ref SpawnTemplateBlob root = ref builder.ConstructRoot<SpawnTemplateBlob>();
            root.shapeAmp = forPaths ? 0f : 0.5f;
            root.shapeFreq = forPaths ? 1f : 2f;

            BlobBuilderArray<SpawnEntryCD> entries = builder.Allocate(ref root.entries, 1);
            ref SpawnEntryCD entry = ref entries[0];
            entry.objectToSpawn.objectID = layers[0];
            entry.objectToSpawn.amount = new RangeInt { min = 1, max = 1 };

            BlobBuilderArray<int> variations =
                builder.Allocate(ref entry.objectToSpawn.variations, 1);
            variations[0] = 0;
            BlobBuilderArray<float> probabilities =
                builder.Allocate(ref entry.objectToSpawn.accumulatedVariationProbability, 1);
            probabilities[0] = 1f;

            entry.chanceToAppearAtAll = 1f;
            entry.randomChance = 1f;
            entry.amount = new RangeInt { min = 1, max = 1 };
            entry.algorithm = forPaths ? SpawnAlgorithm.Rect : SpawnAlgorithm.Circle;
            entry.shapeSize = new Range { min = 1f, max = 1f };
            entry.rotateTowardsCenter = forPaths;

            ObjectID none = ObjectID.None;
            entry.canSpawnOn.Add(in none);
            for (int i = 1; i < layers.Count && entry.canSpawnOn.Length < 15; i++)
            {
                ObjectID wall = layers[i];
                entry.canSpawnOn.Add(in wall);
            }

            BlobAssetReference<SpawnTemplateBlob> blob =
                builder.CreateBlobAssetReference<SpawnTemplateBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        /// <summary>
        /// Vanilla's own synthetic fill template, field for field: circles (or rects) of each
        /// layer for rooms; full-width rects chained layer-on-layer, rotated along the path,
        /// for corridors.
        /// </summary>
        private static BlobAssetReference<SpawnTemplateBlob> BuildFillBlob(
            List<ObjectID> layers,
            bool forPaths,
            bool rectShaped)
        {
            BlobBuilder builder = new BlobBuilder(Allocator.Temp);
            ref SpawnTemplateBlob root = ref builder.ConstructRoot<SpawnTemplateBlob>();
            root.shapeAmp = forPaths ? 0f : 0.5f;
            root.shapeFreq = forPaths ? 1f : 2f;

            BlobBuilderArray<SpawnEntryCD> entries = builder.Allocate(ref root.entries, layers.Count);
            for (int i = 0; i < layers.Count; i++)
            {
                ref SpawnEntryCD entry = ref entries[i];
                entry.objectToSpawn.objectID = layers[i];
                entry.objectToSpawn.amount = new RangeInt { min = 1, max = 1 };

                // One variation, probability one — the game walks the accumulated array, and an
                // unallocated BlobArray is a bad pointer, not an empty list.
                BlobBuilderArray<int> variations =
                    builder.Allocate(ref entry.objectToSpawn.variations, 1);
                variations[0] = 0;
                BlobBuilderArray<float> probabilities =
                    builder.Allocate(ref entry.objectToSpawn.accumulatedVariationProbability, 1);
                probabilities[0] = 1f;

                entry.chanceToAppearAtAll = 1f;
                entry.randomChance = 1f;
                entry.amount = new RangeInt { min = 1, max = 1 };

                if (forPaths)
                {
                    entry.algorithm = SpawnAlgorithm.Rect;
                    entry.shapeSize = new Range { min = 1f, max = 1f };
                    entry.rotateTowardsCenter = true;
                    ObjectID beneath = i == 0 ? ObjectID.None : layers[i - 1];
                    entry.canSpawnOn.Add(in beneath);
                }
                else
                {
                    entry.algorithm = rectShaped ? SpawnAlgorithm.Rect : SpawnAlgorithm.Circle;
                    entry.shapeSize = rectShaped
                        // Vanilla's constant: the inscribed square of the room circle.
                        ? new Range { min = 0.65710676f, max = 0.65710676f }
                        : new Range { min = 1f, max = 1f };
                }
            }

            BlobAssetReference<SpawnTemplateBlob> blob =
                builder.CreateBlobAssetReference<SpawnTemplateBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        /// <summary>
        /// The theme pass: objects swapped for others as the dungeon builds, so one layout can
        /// wear many dressings without redrawing a room.
        /// </summary>
        private static void AddSwaps(
            EntityManager entityManager,
            Entity dungeon,
            DimensionDungeonDefinition definition)
        {
            if (definition.Swaps.Count == 0)
            {
                return;
            }

            DynamicBuffer<DungeonReplaceObjectsBuffer> swaps =
                entityManager.AddBuffer<DungeonReplaceObjectsBuffer>(dungeon);
            for (int i = 0; i < definition.Swaps.Count; i++)
            {
                DimensionDungeonSwapRule rule = definition.Swaps[i];
                ObjectID replaceId = PugMod.API.Authoring.GetObjectID(rule.ReplaceId);
                ObjectID withId = PugMod.API.Authoring.GetObjectID(rule.WithId);
                if (replaceId == ObjectID.None || withId == ObjectID.None)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' swaps '" +
                        rule.ReplaceId + "' for '" + rule.WithId + "', but " +
                        (replaceId == ObjectID.None ? "the first" : "the second") +
                        " is not a known object. The swap was skipped.");
                    continue;
                }

                int minLook = math.max(0, rule.MinReplacementLook);
                int maxLook = math.max(minLook, rule.MaxReplacementLook);
                int lookCount = maxLook - minLook + 1;

                BlobBuilder intBuilder = new BlobBuilder(Allocator.Temp);
                BlobBuilderArray<int> looks = intBuilder.Allocate(
                    ref intBuilder.ConstructRoot<BlobArray<int>>(), lookCount);
                BlobBuilder floatBuilder = new BlobBuilder(Allocator.Temp);
                BlobBuilderArray<float> accumulated = floatBuilder.Allocate(
                    ref floatBuilder.ConstructRoot<BlobArray<float>>(), lookCount);
                for (int look = 0; look < lookCount; look++)
                {
                    looks[look] = minLook + look;
                    accumulated[look] = (look + 1) / (float)lookCount;
                }

                BlobAssetReference<BlobArray<int>> looksBlob =
                    intBuilder.CreateBlobAssetReference<BlobArray<int>>(Allocator.Persistent);
                BlobAssetReference<BlobArray<float>> weightsBlob =
                    floatBuilder.CreateBlobAssetReference<BlobArray<float>>(Allocator.Persistent);
                intBuilder.Dispose();
                floatBuilder.Dispose();
                OwnedIntBlobs.Add(looksBlob);
                OwnedFloatBlobs.Add(weightsBlob);

                swaps.Add(new DungeonReplaceObjectsBuffer
                {
                    replaceID = replaceId,
                    replaceVariation = rule.OnlyOneLook
                        ? new OptionalValue<int>(rule.TheLook)
                        : default,
                    replaceWithID = withId,
                    replaceWithVariations = looksBlob,
                    accumulatedVariationProbability = weightsBlob
                });
            }
        }

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

        private static void DisposeOwnedBlobs()
        {
            for (int i = 0; i < OwnedTemplateBlobs.Count; i++)
            {
                if (OwnedTemplateBlobs[i].IsCreated)
                {
                    OwnedTemplateBlobs[i].Dispose();
                }
            }

            for (int i = 0; i < OwnedIntBlobs.Count; i++)
            {
                if (OwnedIntBlobs[i].IsCreated)
                {
                    OwnedIntBlobs[i].Dispose();
                }
            }

            for (int i = 0; i < OwnedFloatBlobs.Count; i++)
            {
                if (OwnedFloatBlobs[i].IsCreated)
                {
                    OwnedFloatBlobs[i].Dispose();
                }
            }

            OwnedTemplateBlobs.Clear();
            OwnedIntBlobs.Clear();
            OwnedFloatBlobs.Clear();
        }

        /// <summary>
        /// Synthesizes the room placement rules the game's pipeline needs from the asset's
        /// room groups, sized so each group's scenes fit their rooms corner to corner.
        /// </summary>
        /// <remarks>
        /// The shape template will replace these defaults when it is wired; until then the
        /// rules mirror the most common vanilla dungeon: one room at the centre, the rest
        /// grown outward from rooms already placed, entrances pinned to the outline. The
        /// FromOtherRooms algorithm errors on an empty room buffer, so a Center rule must
        /// always come first — which is also why an entrance-only dungeon gets a synthetic
        /// centre room to grow from.
        /// </remarks>
        private static List<DungeonRoomPlacement> BuildRoomRules(
            DimensionDungeonDefinition definition,
            out int maxRoomRadius)
        {
            List<DungeonRoomPlacement> rules = new List<DungeonRoomPlacement>();
            maxRoomRadius = 0;
            bool centrePlaced = false;

            for (int i = 0; i < definition.RoomGroups.Count; i++)
            {
                DimensionDungeonRoomGroup group = definition.RoomGroups[i];
                int maxRadius = 4;
                for (int sceneIndex = 0; sceneIndex < group.SceneNames.Count; sceneIndex++)
                {
                    if (!DimensionCustomSceneRegistry.TryGetFitRadius(
                            group.SceneNames[sceneIndex],
                            out int radius))
                    {
                        continue;
                    }

                    maxRadius = math.max(maxRadius, radius);
                }

                // Every room in the group rolls at the largest scene's fit radius, because which
                // scene a room claims is a separate roll: a small room can claim the big scene,
                // and a room that cannot hold its scene loses the overhang to neighbours and
                // corridors. Guaranteed fit is what a synthesized default owes its author.
                int minRadius = maxRadius;

                maxRoomRadius = math.max(maxRoomRadius, maxRadius);

                bool entrance = group.Role == DimensionDungeonRoomRole.Entrance;
                RoomPlacementAlgorithm algorithm;
                int minCount = math.max(1, group.MinRooms);
                int maxCount = math.max(minCount, group.MaxRooms);
                if (entrance)
                {
                    algorithm = RoomPlacementAlgorithm.Entrance;
                }
                else if (!centrePlaced)
                {
                    // Center places exactly one and ignores the amount; the remainder of this
                    // group grows from it below.
                    algorithm = RoomPlacementAlgorithm.Center;
                    centrePlaced = true;
                }
                else
                {
                    algorithm = RoomPlacementAlgorithm.FromOtherRooms;
                }

                rules.Add(NewRule(algorithm, minCount, maxCount, minRadius, maxRadius));

                if (algorithm == RoomPlacementAlgorithm.Center && maxCount > 1)
                {
                    rules.Add(NewRule(
                        RoomPlacementAlgorithm.FromOtherRooms,
                        math.max(0, minCount - 1),
                        maxCount - 1,
                        minRadius,
                        maxRadius));
                }
            }

            if (!centrePlaced)
            {
                // Every dungeon needs one room at its heart for the others to grow from and
                // the corridors to reach. Small and plain; the fill shell covers it.
                rules.Insert(0, NewRule(RoomPlacementAlgorithm.Center, 1, 1, 4, 6));
                maxRoomRadius = math.max(maxRoomRadius, 6);
            }

            return rules;
        }

        private static DungeonRoomPlacement NewRule(
            RoomPlacementAlgorithm algorithm,
            int minCount,
            int maxCount,
            int minRadius,
            int maxRadius)
        {
            return new DungeonRoomPlacement
            {
                algorithm = algorithm,
                roomType = RoomFlags.Main,
                // The game rolls NextInt(min, max) — max-exclusive — so authored inclusive
                // counts and sizes get one added here, exactly like vanilla's converter.
                amount = new RangeInt { min = minCount, max = maxCount + 1 },
                size = new RangeInt { min = minRadius, max = maxRadius + 1 },
                canIntersectRooms = RoomFlags.None,
                fromExistingSpacing = new RangeInt { min = 3, max = 7 },
                fromExistingPerpendicular = false,
                angleMin = 0f,
                angleMax = math.PI * 2f,
                alignAngleWithCoreRadial = false
            };
        }

        /// <summary>
        /// The biome a table binding matches against — a VANILLA biome by its game name, or
        /// one of the mod's own by its authored id.
        /// </summary>
        /// <remarks>
        /// The vanilla parse comes FIRST, and the order is the bug this fixes: an author
        /// writing "Desert" means the game's Desert, but the identity mint happily coins a
        /// custom int for any string — so the binding matched a biome no Overworld cell ever
        /// reports and the dungeon silently never rolled there. A custom biome id (anything
        /// that is not a vanilla name) still mints as before and matches inside the author's
        /// own dimension, where the framework's biome sampling reports it.
        /// </remarks>
        internal static Biome ResolveBindingBiome(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return Biome.None;
            }

            Biome vanilla;
            if (System.Enum.TryParse(biomeId, false, out vanilla))
            {
                return vanilla;
            }

            return DimensionBiomeIdentity.GetOrAssign(biomeId);
        }

        private static WorldGenerationTypeDependentValue<Biome> ForBothWorldTypes(Biome biome)
        {
            return new WorldGenerationTypeDependentValue<Biome>
            {
                classic = biome,
                fullRelease = biome
            };
        }

        private static RoomFlags ToRoomFlags(DimensionDungeonRoomRole role)
        {
            switch (role)
            {
                case DimensionDungeonRoomRole.Entrance:
                    return RoomFlags.CustomScene | RoomFlags.Entrance;
                case DimensionDungeonRoomRole.End:
                    return RoomFlags.CustomScene | RoomFlags.End;
                case DimensionDungeonRoomRole.Connecting:
                    return RoomFlags.CustomScene | RoomFlags.Connecting;
                default:
                    return RoomFlags.CustomScene | RoomFlags.Main;
            }
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
