using System;
using System.Collections.Generic;
using ExpandNullforge.Foundation;
using Pug.UnityExtensions;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// Puts this framework's scenes into Core Keeper's scene table, so the engine's own placement
    /// machinery can stamp them like any hand-authored room.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY REBUILD RATHER THAN APPEND. The table is a blob asset: one immutable
    /// <c>BlobAssetReference&lt;CustomSceneTableBlob&gt;</c> held by a singleton component. Blobs cannot
    /// grow, so "adding" a scene means building a new table containing the existing scenes plus ours
    /// and writing the singleton back. That is safe precisely because <b>every</b> consumer resolves a
    /// scene by NAME — the apply system, the legacy spawn system, the dungeon room generator — so
    /// appending never disturbs anything that was already there.
    /// </para>
    /// <para>
    /// TIMING IS THE WHOLE PROBLEM, AND THERE ARE TWO REGIMES.
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     Explicit placement re-reads the singleton on every update, so for that path the table can be
    ///     swapped at any moment and it simply works.
    ///   </description></item>
    ///   <item><description>
    ///     Natural world generation does not. It reads the singleton once when its system starts and
    ///     immediately snapshots the scene list out of it. Swap after that and the new scenes exist in
    ///     the table but will never be chosen — the failure is total and completely silent. So the
    ///     injection has to land before that system starts, which is what
    ///     <see cref="DimensionSpawnDungeonAndScenePatch"/> arranges.
    ///   </description></item>
    /// </list>
    /// <para>
    /// OWNERSHIP. Vanilla's table lives in a <c>BlobAssetStore</c> that disposes it with the world.
    /// Ours is allocated <c>Persistent</c> and is not in that store, so this class keeps the handle and
    /// disposes the previous one itself. Without that, every world load would leak a table.
    /// </para>
    /// </remarks>
    public static class DimensionCustomSceneTableInjector
    {
        private static BlobAssetReference<CustomSceneTableBlob> owned;

        /// <summary>
        /// The world the current table was built for.
        /// </summary>
        /// <remarks>
        /// Keyed on the world rather than a "have I run yet" flag on purpose. A player who leaves to the
        /// menu and loads a second save gets a brand new world with a brand new vanilla table, and a
        /// flag would remember the first one and skip — leaving every structure missing from the second
        /// world with nothing to explain it. Comparing worlds makes the retry automatic.
        /// </remarks>
        private static World injectedWorld;

        /// <summary>How many scenes the last successful injection added.</summary>
        public static int LastInjectedCount { get; private set; }

        /// <summary>
        /// Rebuilds the scene table as (whatever is already there) + (our scenes), once per world.
        /// </summary>
        /// <returns>True when the table now contains our scenes.</returns>
        public static bool TryInject(World world)
        {
            if (world == null || !world.IsCreated || DimensionCustomSceneRegistry.Count == 0)
            {
                return false;
            }

            if (ReferenceEquals(injectedWorld, world))
            {
                return true;
            }

            EntityManager entityManager = world.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<CustomSceneTableCD>());
            if (query.IsEmptyIgnoreFilter)
            {
                // The table has not been built yet. Not an error — the caller retries.
                return false;
            }

            try
            {
                Entity singleton = query.GetSingletonEntity();
                CustomSceneTableCD existing = entityManager.GetComponentData<CustomSceneTableCD>(singleton);

                // The object database is the reason this cannot happen at authoring time: a name only
                // becomes a prefab entity once the world has loaded its objects.
                PugDatabase.DatabaseBankCD databaseBank;
                bool hasDatabase = TryGetDatabaseBank(entityManager, out databaseBank);

                BlobAssetReference<CustomSceneTableBlob> rebuilt = Build(existing.Value, hasDatabase, databaseBank);
                entityManager.SetComponentData(singleton, new CustomSceneTableCD { Value = rebuilt });

                // Only now is the old table unreferenced. Disposing before the write would leave the
                // singleton pointing at freed memory for the width of this method.
                if (owned.IsCreated)
                {
                    owned.Dispose();
                }

                owned = rebuilt;
                injectedWorld = world;
                LastInjectedCount = DimensionCustomSceneRegistry.Count;

                DimensionFrameworkLog.Info(
                    "Added " + LastInjectedCount + " scene(s) to the world's scene table.");
                return true;
            }
            catch (Exception exception)
            {
                DimensionFrameworkLog.Warning(
                    "Could not add scenes to the world's scene table: " + exception.Message +
                    ". Structures from this mod will not appear.");
                return false;
            }
        }

        /// <summary>
        /// Builds a table holding every scene in <paramref name="source"/> followed by every registered
        /// one.
        /// </summary>
        /// <remarks>
        /// Existing scenes are copied field by field rather than memcpy'd: a blob array's contents live
        /// outside the struct, so copying the struct alone would produce entries whose arrays still
        /// point into the old table — which is exactly the table this operation is about to dispose.
        /// </remarks>
        private static BlobAssetReference<CustomSceneTableBlob> Build(
            BlobAssetReference<CustomSceneTableBlob> source,
            bool hasDatabase,
            PugDatabase.DatabaseBankCD databaseBank)
        {
            IReadOnlyList<DimensionCustomSceneDefinition> ours = DimensionCustomSceneRegistry.All;
            int existingCount = source.IsCreated ? source.Value.scenes.Length : 0;

            using (BlobBuilder builder = new BlobBuilder(Allocator.Temp))
            {
                ref CustomSceneTableBlob root = ref builder.ConstructRoot<CustomSceneTableBlob>();
                BlobBuilderArray<CustomSceneBlob> scenes =
                    builder.Allocate(ref root.scenes, existingCount + ours.Count);

                for (int i = 0; i < existingCount; i++)
                {
                    CopyExisting(builder, ref source.Value.scenes[i], ref scenes[i]);
                }

                for (int i = 0; i < ours.Count; i++)
                {
                    WriteOurs(builder, ours[i], ref scenes[existingCount + i], hasDatabase, databaseBank);
                }

                return builder.CreateBlobAssetReference<CustomSceneTableBlob>(Allocator.Persistent);
            }
        }

        /// <summary>
        /// The object database, if the world has one yet.
        /// </summary>
        /// <remarks>
        /// Returns false rather than throwing when it is missing. A scene with no objects does not
        /// need it at all, so a world that has not built its database yet should still get its
        /// terrain rather than losing the whole scene over a dependency it never used.
        /// </remarks>
        private static bool TryGetDatabaseBank(EntityManager entityManager, out PugDatabase.DatabaseBankCD databaseBank)
        {
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            if (query.IsEmptyIgnoreFilter)
            {
                databaseBank = default;
                return false;
            }

            databaseBank = query.GetSingleton<PugDatabase.DatabaseBankCD>();
            return true;
        }

        private static void CopyExisting(BlobBuilder builder, ref CustomSceneBlob from, ref CustomSceneBlob to)
        {
            to.sceneName = from.sceneName;
            to.centerPosition = from.centerPosition;
            to.canFlipX = from.canFlipX;
            to.canFlipY = from.canFlipY;
            to.maxOccurrences = from.maxOccurrences;
            to.replacedByContentBundle = from.replacedByContentBundle;
            to.minDistanceFromCoreInClassicWorlds = from.minDistanceFromCoreInClassicWorlds;
            to.hasCenter = from.hasCenter;
            to.center = from.center;
            to.boundsSize = from.boundsSize;
            to.radius = from.radius;

            // The biome lists MUST survive the copy. The game's scene picker is an exact-match
            // scan over biomesToSpawnIn with no wildcard, so a vanilla scene whose list came
            // through as an empty BlobArray stops natural-spawning in every cell generated
            // after our injection — a regression that would read as "the world feels emptier"
            // with nothing logged anywhere.
            CopyArray(builder, ref from.biomesToSpawnIn.classic, ref to.biomesToSpawnIn.classic);
            CopyArray(builder, ref from.biomesToSpawnIn.fullRelease, ref to.biomesToSpawnIn.fullRelease);

            CopyArray(builder, ref from.tiles, ref to.tiles);
            CopyArray(builder, ref from.tilePositions, ref to.tilePositions);
            CopyArray(builder, ref from.prefabs, ref to.prefabs);
            CopyArray(builder, ref from.prefabPositions, ref to.prefabPositions);
            CopyArray(builder, ref from.prefabDirections, ref to.prefabDirections);
            CopyArray(builder, ref from.prefabColors, ref to.prefabColors);
            CopyArray(builder, ref from.prefabInventoryOverrides, ref to.prefabInventoryOverrides);
            CopyArray(builder, ref from.prefabSizes, ref to.prefabSizes);
            CopyArray(builder, ref from.prefabCornerOffsets, ref to.prefabCornerOffsets);
            CopyArray(builder, ref from.prefabObjectDatas, ref to.prefabObjectDatas);
        }

        private static void CopyArray<T>(BlobBuilder builder, ref BlobArray<T> from, ref BlobArray<T> to)
            where T : unmanaged
        {
            BlobBuilderArray<T> copy = builder.Allocate(ref to, from.Length);
            for (int i = 0; i < from.Length; i++)
            {
                copy[i] = from[i];
            }
        }

        /// <summary>The tile footprint, max minus min plus one on each axis.</summary>
        private static int2 ComputeTileExtent(IReadOnlyList<DimensionSceneTile> tiles)
        {
            if (tiles == null || tiles.Count == 0)
            {
                return new int2(1, 1);
            }

            int2 minimum = tiles[0].LocalPosition;
            int2 maximum = tiles[0].LocalPosition;
            for (int i = 1; i < tiles.Count; i++)
            {
                minimum = math.min(minimum, tiles[i].LocalPosition);
                maximum = math.max(maximum, tiles[i].LocalPosition);
            }

            return maximum - minimum + new int2(1, 1);
        }

        private static void WriteOurs(
            BlobBuilder builder,
            DimensionCustomSceneDefinition definition,
            ref CustomSceneBlob blob,
            bool hasDatabase,
            PugDatabase.DatabaseBankCD databaseBank)
        {
            blob.sceneName = definition.SceneName;
            blob.centerPosition = definition.CenterPosition;
            blob.canFlipX = definition.CanFlipX;
            blob.canFlipY = definition.CanFlipY;
            blob.maxOccurrences = definition.MaxOccurrences;

            // Always allocate the biome lists, even empty: an unallocated BlobArray is a bad
            // pointer, not an empty list. A scene that opts into Overworld growth carries the
            // vanilla biomes it named; everything else carries an honest empty list.
            IReadOnlyList<Biome> overworldBiomes = definition.OverworldBiomes;
            BlobBuilderArray<Biome> classicBiomes = builder.Allocate(
                ref blob.biomesToSpawnIn.classic,
                overworldBiomes.Count);
            BlobBuilderArray<Biome> fullReleaseBiomes = builder.Allocate(
                ref blob.biomesToSpawnIn.fullRelease,
                overworldBiomes.Count);
            for (int b = 0; b < overworldBiomes.Count; b++)
            {
                classicBiomes[b] = overworldBiomes[b];
                fullReleaseBiomes[b] = overworldBiomes[b];
            }

            blob.minDistanceFromCoreInClassicWorlds =
                definition.MinDistanceFromCoreInClassicWorlds;
            blob.hasCenter = false;
            blob.center = default;
            int2 sceneExtent = ComputeTileExtent(definition.Tiles);
            blob.boundsSize = sceneExtent;
            // Used by the placer as a clearance requirement only: larger means rarer, never
            // wrong. Half the diagonal keeps corners clear at any flip.
            blob.radius = 0.5f * math.length(new float2(sceneExtent.x, sceneExtent.y));

            IReadOnlyList<DimensionSceneTile> tiles = definition.Tiles;
            BlobBuilderArray<TileCD> tileData = builder.Allocate(ref blob.tiles, tiles.Count);
            BlobBuilderArray<int2> tilePositions = builder.Allocate(ref blob.tilePositions, tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
            {
                DimensionSceneTile tile = tiles[i];
                tileData[i] = new TileCD { tileset = tile.Tileset, tileType = tile.TileType };
                tilePositions[i] = tile.LocalPosition;
            }

            WriteObjects(builder, definition, ref blob, hasDatabase, databaseBank);
        }

        /// <summary>
        /// Resolves the scene's named objects into real prefab entities and writes them, with their
        /// per-copy overrides, into the blob.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS WHY OBJECTS COULD NOT SHIP BEFORE. A scene stores its objects as prefab
        /// <em>entities</em>, and those only exist once the world has converted its object database.
        /// Authoring time is far too early. Injection is exactly the right moment: the database is
        /// loaded, the world is real, and the table is about to be handed to the engine.
        /// </para>
        /// <para>
        /// A name that does not resolve is <strong>dropped, loudly</strong>, and the rest of the scene
        /// still places. Losing one chest to a typo should not cost the author their whole room, and a
        /// silently missing object is far harder to diagnose than a warning naming it.
        /// </para>
        /// <para>
        /// Every array must be allocated even when empty. An unallocated <c>BlobArray</c> has no valid
        /// offset — reading one is not an empty list, it is a bad pointer.
        /// </para>
        /// </remarks>
        private static void WriteObjects(
            BlobBuilder builder,
            DimensionCustomSceneDefinition definition,
            ref CustomSceneBlob blob,
            bool hasDatabase,
            PugDatabase.DatabaseBankCD databaseBank)
        {
            List<ResolvedObject> resolved = new List<ResolvedObject>();

            if (hasDatabase)
            {
                IReadOnlyList<DimensionSceneObject> objects = definition.Objects;
                for (int i = 0; i < objects.Count; i++)
                {
                    DimensionSceneObject candidate = objects[i];
                    ObjectID objectID = API.Authoring.GetObjectID(candidate.ObjectName);
                    if (objectID == ObjectID.None)
                    {
                        DimensionFrameworkLog.Warning(
                            "Scene '" + definition.SceneName + "' wanted to place '" +
                            candidate.ObjectName + "' at " + candidate.LocalPosition +
                            ", but no object of that name is registered. Everything else in the scene " +
                            "still places.");
                        continue;
                    }

                    Entity prefab = PugDatabase.GetPrimaryPrefabEntity(objectID, databaseBank.databaseBankBlob);
                    if (prefab == Entity.Null)
                    {
                        DimensionFrameworkLog.Warning(
                            "Scene '" + definition.SceneName + "': '" +
                            candidate.ObjectName + "' resolved to an id but has no prefab in this " +
                            "world, so it was skipped.");
                        continue;
                    }

                    resolved.Add(new ResolvedObject
                    {
                        Source = candidate,
                        Prefab = prefab,
                        ObjectID = objectID,
                        Info = PugDatabase.GetObjectInfo(objectID)
                    });
                }
            }
            else if (definition.Objects.Count > 0)
            {
                DimensionFrameworkLog.Warning(
                    "Scene '" + definition.SceneName + "' has objects, but the " +
                    "world's object database is not available yet, so its terrain will place without " +
                    "them.");
            }

            int count = resolved.Count;
            BlobBuilderArray<Entity> prefabs = builder.Allocate(ref blob.prefabs, count);
            BlobBuilderArray<float3> positions = builder.Allocate(ref blob.prefabPositions, count);
            BlobBuilderArray<OptionalValue<float3>> directions = builder.Allocate(ref blob.prefabDirections, count);
            BlobBuilderArray<OptionalValue<PaintableColor>> colors = builder.Allocate(ref blob.prefabColors, count);
            BlobBuilderArray<InventoryOverrideData> inventories =
                builder.Allocate(ref blob.prefabInventoryOverrides, count);
            BlobBuilderArray<int2> sizes = builder.Allocate(ref blob.prefabSizes, count);
            BlobBuilderArray<int2> cornerOffsets = builder.Allocate(ref blob.prefabCornerOffsets, count);
            BlobBuilderArray<ObjectDataCD> objectDatas = builder.Allocate(ref blob.prefabObjectDatas, count);

            for (int i = 0; i < count; i++)
            {
                ResolvedObject entry = resolved[i];
                prefabs[i] = entry.Prefab;

                // Ground-plane placement: X across, Z into the screen, Y untouched.
                positions[i] = new float3(entry.Source.LocalPosition.x, 0f, entry.Source.LocalPosition.y);

                float3 direction;
                directions[i] = DimensionSceneFacings.TryGetDirection(entry.Source.Facing, out direction)
                    ? new OptionalValue<float3>(direction)
                    : default;

                // Unpainted is both the game's first palette entry and our "no override", so the two
                // collapse into one check rather than needing a separate flag.
                colors[i] = entry.Source.Paint != DimensionScenePaintChoice.Unpainted
                    ? new OptionalValue<PaintableColor>((PaintableColor)entry.Source.Paint)
                    : default;

                // Written field by field, straight into the blob element, and never through a local
                // or a helper. A struct holding a BlobArray may not be copied around: its array's
                // contents live outside it, so an assignment would replace a real allocation offset
                // with a default one and the game would read a bad pointer. Unity's own analyzer
                // enforces this, which is how the rule was found.
                if (entry.Source.HasInventoryOverride)
                {
                    inventories[i].hasAnyInventoryOverride = true;

                    LootTableID lootTable;
                    // Vanilla names first, then the mod's own registered tables — the shared
                    // resolver, so a placed chest can roll an authored table by name.
                    if (!string.IsNullOrEmpty(entry.Source.LootTableName) &&
                        Loot.DimensionLootTableRegistry.TryResolve(entry.Source.LootTableName, out lootTable))
                    {
                        inventories[i].hasLootTableOverride = true;
                        inventories[i].lootTableOverride = lootTable;
                    }
                    else if (!string.IsNullOrEmpty(entry.Source.LootTableName))
                    {
                        DimensionFrameworkLog.Warning(
                            "Container loot table '" + entry.Source.LootTableName +
                            "' is neither one of the game's loot tables nor one of this mod's, so " +
                            "that container was left to its own contents.");
                    }

                    List<InitialInventoryItem> contents = ResolveContents(entry.Source);
                    if (contents.Count > 0)
                    {
                        inventories[i].hasItemsOverride = true;

                        // Clear whatever the object would have held: authored contents are a statement
                        // about what is in this chest, not an addition to a default nobody saw.
                        inventories[i].itemsToRemove = contents.Count;

                        BlobBuilderArray<InitialInventoryItem> written =
                            builder.Allocate(ref inventories[i].itemsOverride, contents.Count);
                        for (int c = 0; c < contents.Count; c++)
                        {
                            written[c] = contents[c];
                        }
                    }
                }

                // Footprint, amount and variation all come from the object's own definition, which is
                // the same place Core Keeper's converter reads them for a hand-authored scene. Asserting
                // 1x1 here instead would make a two-tile bed claim one tile and overlap its neighbour,
                // and would reset every object that ships with a non-default variation.
                if (entry.Info != null)
                {
                    sizes[i] = new int2(entry.Info.prefabTileSize.x, entry.Info.prefabTileSize.y);
                    cornerOffsets[i] = new int2(entry.Info.prefabCornerOffset.x, entry.Info.prefabCornerOffset.y);
                    objectDatas[i] = new ObjectDataCD
                    {
                        objectID = entry.ObjectID,
                        amount = entry.Info.initialAmount,
                        variation = entry.Info.variation
                    };
                }
                else
                {
                    // Vanilla's own fallback for a prefab with no object info: one tile, no offset.
                    sizes[i] = new int2(1, 1);
                    cornerOffsets[i] = default;
                    objectDatas[i] = new ObjectDataCD
                    {
                        objectID = entry.ObjectID,
                        amount = 1,
                        variation = 0
                    };
                }
            }
        }

        /// <summary>
        /// Fills a placed container with what its author asked for.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A loot table and exact items are not alternatives — both may be set, and Core Keeper
        /// applies both. The table is the room's texture and the items are its point: a chest that
        /// always holds the key and also holds something rolled is a normal thing to want.
        /// </para>
        /// <para>
        /// The items array must be allocated whenever <c>hasItemsOverride</c> is set, and must NOT be
        /// touched otherwise — an unallocated BlobArray has no valid offset, so reading one is a bad
        /// pointer rather than an empty list.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Resolves a container's authored items into what the blob stores.
        /// </summary>
        /// <remarks>
        /// Returns a plain list rather than writing into the blob, because an element of blob storage
        /// cannot be passed by reference — the allocation has to happen at the call site. Splitting it
        /// this way also keeps the name resolution, which can warn, out of the blob-building loop.
        /// </remarks>
        private static List<InitialInventoryItem> ResolveContents(DimensionSceneObject source)
        {
            List<InitialInventoryItem> items = new List<InitialInventoryItem>();
            if (source.Contents == null || source.Contents.Count == 0)
            {
                return items;
            }

            for (int i = 0; i < source.Contents.Count; i++)
            {
                DimensionSceneContent content = source.Contents[i];
                ObjectID itemId = API.Authoring.GetObjectID(content.ItemName);
                if (itemId == ObjectID.None)
                {
                    DimensionFrameworkLog.Warning(
                        "Container item '" + content.ItemName +
                        "' is not a registered object, so it was left out. The container still places.");
                    continue;
                }

                items.Add(new InitialInventoryItem
                {
                    item = new ObjectData
                    {
                        objectID = itemId,
                        amount = content.Amount,
                        variation = 0
                    }
                });
            }

            return items;
        }

        private struct ResolvedObject
        {
            public DimensionSceneObject Source;
            public Entity Prefab;
            public ObjectID ObjectID;

            /// <summary>The object's own definition, or null if the database has no entry for it.</summary>
            public ObjectInfo Info;
        }

        /// <summary>
        /// Forgets the per-world state and releases the table this one owned. The runtime does not need
        /// to call it — a new world re-injects on its own — but tests do.
        /// </summary>
        public static void ResetForNewWorld()
        {
            injectedWorld = null;
            LastInjectedCount = 0;
            if (owned.IsCreated)
            {
                owned.Dispose();
                owned = default;
            }
        }
    }
}
