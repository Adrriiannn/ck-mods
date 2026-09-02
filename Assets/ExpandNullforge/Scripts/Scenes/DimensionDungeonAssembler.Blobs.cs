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
    /// The blob assets a dungeon hands the game, and letting them go again.
    /// </summary>
    public static partial class DimensionDungeonAssembler
    {
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
    }
}
