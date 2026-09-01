using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating world objects did.</summary>
    internal sealed class DimensionWorldObjectGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>Drops needing a loot-table registration rather than per-object custom loot.</summary>
        public readonly List<DimensionResolvedDrop> DropsNeedingALootTable =
            new List<DimensionResolvedDrop>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "World objects: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Turns a world object into the placed prefab Core Keeper expects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the long tail — doors, lights, beds, trophies and the great mass of decoration. Their
    /// component sets are nearly identical: the spine every placed object has, plus one or two small
    /// markers. Modelled from <c>TorchEntity</c> and the 34 door prefabs.
    /// </para>
    /// <para>
    /// The three lighting components are written independently, because they are genuinely three
    /// different things and a torch carries two of them and not the third. See the asset for why.
    /// </para>
    /// </remarks>
    internal static class DimensionWorldObjectGenerator
    {
        /// <summary>The folder inside a mod where generated world-object prefabs live.</summary>
        public const string FolderName = "WorldObjects";

        /// <summary>Damage a single hit may do, matching the rest of the framework.</summary>
        private const int DamagePerHit = 1;

        /// <param name="plan">
        /// What every source drops, from the drop-location inversion. Optional: when absent nothing
        /// treats these objects as a drop source, which is how they behaved before.
        /// </param>
        public static DimensionWorldObjectGenerationReport Generate(
            IEnumerable<DimensionWorldObjectAsset> objects,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionDropPlan plan = null,
            IEnumerable<DimensionTilesetAsset> tilesets = null)
        {
            DimensionWorldObjectGenerationReport report = new DimensionWorldObjectGenerationReport();
            if (objects == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no world objects were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);
            binder = new DimensionObjectBinder(naming);

            // A tileset an object can leave behind is looked up by name, custom first. Built once
            // here rather than per object, because the answer cannot change mid-generate.
            System.Func<string, int> resolveTileset = BuildTilesetResolver(tilesets);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionWorldObjectAsset worldObject in objects)
                {
                    GenerateOne(
                        worldObject,
                        outputFolder,
                        naming,
                        report,
                        plan == null || worldObject == null
                            ? null
                            : plan.For(
                                DimensionDropSourceKind.Destructible,
                                worldObject.ObjectIdentifier),
                        resolveTileset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }

        /// <summary>
        /// Builds the name-to-tileset lookup an object uses to name the tile it leaves behind.
        /// </summary>
        /// <remarks>
        /// The mod's own tilesets are checked first, so an author who names a block after a vanilla
        /// one gets their own rather than the game's. Vanilla names still resolve, because leaving
        /// ordinary dirt or stone behind is a perfectly reasonable thing to want.
        /// </remarks>
        private static System.Func<string, int> BuildTilesetResolver(
            IEnumerable<DimensionTilesetAsset> tilesets)
        {
            Dictionary<string, int> custom = new Dictionary<string, int>();
            if (tilesets != null)
            {
                foreach (DimensionTilesetAsset tileset in tilesets)
                {
                    if (tileset == null)
                    {
                        continue;
                    }

                    string name = tileset.TilesetName;
                    if (!string.IsNullOrEmpty(name) && !custom.ContainsKey(name))
                    {
                        custom[name] = tileset.TilesetId;
                    }
                }
            }

            return delegate(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return -1;
                }

                int id;
                if (custom.TryGetValue(name, out id))
                {
                    return id;
                }

                PugTilemap.Tileset vanilla;
                if (System.Enum.TryParse(name, false, out vanilla))
                {
                    return (int)vanilla;
                }

                return -1;
            };
        }

        private static void GenerateOne(
            DimensionWorldObjectAsset worldObject,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionWorldObjectGenerationReport report,
            DimensionDropsForSource dropsFromThis,
            System.Func<string, int> resolveTileset)
        {
            if (worldObject == null || !worldObject.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(worldObject.ObjectIdentifier))
            {
                report.Skipped.Add("A world object with no id was skipped.");
                return;
            }

            Warn(worldObject, report);

            string prefabPath =
                outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(worldObject.ObjectIdentifier, "WorldObject") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(worldObject.ObjectIdentifier);

            try
            {
                Configure(root, worldObject, naming, report, dropsFromThis, resolveTileset);
                // drawsItself: an object standing in the world is something a player looks at, so
                // it gets a body prefab whether or not anything uses it. Without this an authored
                // sprite reached nothing and the object was invisible where it stood.
                DimensionInteractionVisualUtility.Apply(
                    root,
                    worldObject.Interaction,
                    outputFolder,
                    DimensionGeneratedPrefabUtility.SanitizeAuthoredName(worldObject.ObjectIdentifier, "WorldObject"),
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                    },
                    true,
                    worldObject.Sprite,
                    // The light a placed object gives off is not an entity component — Core Keeper
                    // keeps it in the graphical prefab — so it is answered here rather than in
                    // ApplyLight below, which writes the entity's three lighting components. The
                    // two halves write different files and cannot overwrite each other.
                    worldObject.EmittedLight);
                ApplyShopStock(root, worldObject, naming, report);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                if (updating)
                {
                    report.Updated.Add(prefabPath);
                }
                else
                {
                    report.Created.Add(prefabPath);
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    worldObject.ObjectIdentifier + " failed to generate: " + exception.Message + "\n" +
                    exception.StackTrace);
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void Configure(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            DimensionNamingContext naming,
            DimensionWorldObjectGenerationReport report,
            DimensionDropsForSource dropsFromThis,
            System.Func<string, int> resolveTileset)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(worldObject.ObjectIdentifier);
            obj.objectType = ObjectType.PlaceablePrefab;
            obj.initialAmount = 1;

            Rarity rarity;
            if (!string.IsNullOrEmpty(worldObject.RarityId) &&
                Enum.TryParse(worldObject.RarityId, false, out rarity))
            {
                obj.rarity = rarity;
            }

            // The same three places a container's look lives, for the same reasons: the picture on
            // the object's own record (which the body reads back per entity, and which the game
            // itself uses for a carried object), and the icon on the inventory component, which is
            // the only field ObjectInfo takes an icon from. Both were authored and read by nothing.
            obj.additionalSprites = new List<Sprite>();
            if (worldObject.Sprite != null)
            {
                obj.additionalSprites.Add(worldObject.Sprite);
            }

            InventoryItemAuthoring inventoryItem = EnsureComponent<InventoryItemAuthoring>(root);
            inventoryItem.icon = worldObject.Icon;
            inventoryItem.smallIcon = worldObject.Icon;
            if (inventoryItem.requiredObjectsToCraft == null)
            {
                inventoryItem.requiredObjectsToCraft =
                    new List<InventoryItemAuthoring.CraftingObject>();
            }

            if (worldObject.Icon == null)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' has no picture and no icon, so nothing is " +
                    "drawn where it stands and its item is an empty square. Drop a sprite into its " +
                    "Artwork or Icon field.");
            }

            ApplyKind(root, worldObject, naming, report);
            ApplyPlacement(root, worldObject, resolveTileset, report, naming);
            ApplyLight(root, worldObject);
            ApplyBreaking(root, worldObject, report);

            DimensionObjectSpine.ApplyUniversal(root, false);

            DimensionObjectSpine.ApplyNativeWorldPlacement(
                root,
                worldObject.NativeWorldPlacement,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                worldObject.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });
            DimensionObjectSpine.ApplyPlacedObject(
                root,
                worldObject.Paintable,
                worldObject.FacesPlacementDirection);
            DimensionObjectSpine.ApplyWiring(root, worldObject.Wiring);

            // WHAT IT SHEDS, WHAT IT GIVES WHEN USED, WHAT IT GIVES IN SEASON. The "Everything
            // else" foldout has drawn these three for world objects all along and nothing wrote
            // them — a pot's shed loot, filled in and thrown away with no word said. It runs before
            // the collected drops so the shared DropLootAuthoring is settled by the pass that owns
            // each of its channels: this one owns the three extra channels, that one owns custom
            // loot and the table.
            DimensionObjectSpine.ApplyExtraLoot(
                root,
                worldObject.ExtraLoot,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred);

            ApplyCollectedDrops(root, dropsFromThis, worldObject.ObjectIdentifier, report);
            DimensionObjectSpine.ApplyContinuousAttack(
                root,
                worldObject.ContinuousAttack,
                worldObject.Wiring,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyImmunityZone(
                root,
                worldObject.KeepsThingsSafeNearby,
                worldObject.SafeRadius,
                worldObject.SafeAreaIsRectangular,
                worldObject.SafeWidth,
                worldObject.SafeHeight,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyMusicArea(
                root,
                worldObject.Music,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyFacingAndText(
                root,
                worldObject.InitialFacing,
                worldObject.ItsColliderTurnsToo,
                worldObject.RotationIconOffset,
                worldObject.TextItComesWith,
                worldObject.UntouchableForOneFrameOnly,
                worldObject.CannotBeAttacked,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyBasics(
                root,
                worldObject.Basics,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });
            DimensionObjectSpine.ApplyInitialConditions(
                root,
                worldObject.Conditions,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyPlacedKinds(
                root,
                worldObject.IsAFenceGate,
                worldObject.FlowerOfPlantId,
                worldObject.FlowerVariation,
                worldObject.SpawnsEnemyId,
                delegate(string objectId) { return ResolveObject(objectId); },
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred);

            DimensionObjectSpine.ApplyWaypoint(
                root,
                worldObject.PlayersCanTravelToIt,
                worldObject.ActivateWithin,
                worldObject.IsTheCoreWaypoint);

            DimensionObjectSpine.ApplyAutomation(
                root,
                worldObject.Automation,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyObjectRules(
                root,
                worldObject.Rules,
                delegate(string objectId) { return ResolveObject(objectId); },
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred,
                QualifyReference);

            DimensionObjectSpine.ApplyLeavesBehind(
                root,
                worldObject.LeavesBehind,
                delegate(string objectId) { return ResolveObject(objectId); },
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyImmunities(root, false, false, worldObject.AlwaysDropsLoot);

            DimensionObjectSpine.ApplyTileOutcomes(
                root,
                worldObject.TileOutcome,
                resolveTileset,
                delegate(string message) { report.Warnings.Add(message); });

            DimensionObjectSpine.ApplyImpactFeedback(
                root,
                worldObject.ImpactFeedback,
                delegate(string unknown)
                {
                    report.Warnings.Add(
                        "'" + worldObject.DisplayName + "' throws particle burst '" + unknown +
                        "', which the game does not have. Nothing will come off it there.");
                });
            // IsDeferred goes in, like the item generator's call. Without it a world object set to
            // summon one of the mod's OWN creatures was told it named nothing, and no link row was
            // written — so the summon could never be filled in at load either.
            DimensionObjectSpine.ApplySecondaryUse(
                root,
                worldObject.SecondaryUse,
                delegate(string itemId) { return ResolveObject(itemId); },
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred);
            // The stored number, not a new control. A world object is something you set down: not
            // one of the game's 1,281 placeable prefabs carries a cooldown component, and the slots
            // that read one only read it for an item in hand. The field that used to sit inside the
            // effects block is no longer drawn anywhere, and this keeps whatever an existing world
            // object had in it rather than dropping the value on the next generate.
            DimensionObjectSpine.ApplyItemEffects(
                root,
                worldObject.Effects,
                worldObject.Effects.LegacyCooldownSeconds,
                delegate(string unknown)
                {
                    report.Warnings.Add(
                        "'" + worldObject.DisplayName + "' grants '" + unknown +
                        "', which is not an effect the game has. It will grant nothing for that entry.");
                },
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            // LAST. Every answer above writes the one component that obviously belongs to it; this
            // is the sweep that adds what the game's own systems ALSO demand beside it — the
            // tracker a reacting object needs to see anything at all, the slot a spawner platform
            // needs something put into it, the shelves a trader stocks, the wiring a powered answer
            // reads. It only adds, so nothing above it can be undone; and where the gap is one only
            // the author can close, it says so in the report rather than shipping a control that
            // does nothing.
            // THE TWO ANSWERS NOTHING ON THE OBJECT CAN SUPPLY. Every vanilla door prefab is one
            // tile by one tile whichever way it faces, and a torch carries the same components as
            // a statue, so the sweep cannot read either off what has already been written. Both
            // are controls with those words on them, and both are handed in here.
            DimensionQueryCompanions.WhatTheAuthorSaidAboutItsBody saidAboutItsBody =
                new DimensionQueryCompanions.WhatTheAuthorSaidAboutItsBody();
            saidAboutItsBody.ADoorwayThatRunsAlongTheWall =
                worldObject.Rules.DoorwayRunsAlongTheWall;
            saidAboutItsBody.PlayersWalkThroughIt = worldObject.Rules.PlayersWalkThroughIt;

            if (worldObject.Rules.WalkThroughWasOverruledByGroundCover)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' is ticked both as ground cover and as " +
                    "something a player walks through, and ground cover was used. Ground cover " +
                    "sits on the layer nothing casts at, so the thing cannot be hit and nothing " +
                    "notices it when something is placed on its tile; the walk-through answer " +
                    "keeps both of those. Untick one of the two.");
            }

            if (worldObject.Rules.DoorwayRunsAlongTheWall && !ItIsADoor(worldObject))
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' says its doorway runs along the wall and " +
                    "it is not a door, so nothing was done with that answer.");
            }

            DimensionQueryCompanions.FinishAWorldObject(
                root,
                worldObject.DisplayName,
                saidAboutItsBody,
                delegate(string message)
                {
                    report.Warnings.Add(message);
                });
        }

        /// <summary>Whether the author called this world object a door.</summary>
        private static bool ItIsADoor(DimensionWorldObjectAsset worldObject)
        {
            return worldObject != null && worldObject.Kind == DimensionWorldObjectKind.Door;
        }

        /// <summary>
        /// The one or two markers that make it a door, a bed or a trophy rather than decoration.
        /// </summary>
        /// <summary>
        /// Stamps or clears the shop's stock. The names ride to runtime and hydrate into the
        /// game's own vending buffer, so the buy window and pricing are entirely vanilla's.
        /// </summary>
        private static void ApplyShopStock(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            DimensionNamingContext naming,
            DimensionWorldObjectGenerationReport report)
        {
            DimensionInteractionTemplate interaction = worldObject.Interaction;
            bool sells = interaction != null &&
                         interaction.WhatUsingItDoes == DimensionUseBehaviour.SellsLikeAShop;
            if (!sells)
            {
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionShopStockAuthoring>(root);
                return;
            }

            string[] soldIds = interaction.SoldItemIds;
            if (soldIds.Length == 0)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' sells like a shop but stocks nothing, " +
                    "so its window opens empty.");
            }

            ExpandNullforge.Creatures.DimensionShopStockAuthoring stock =
                EnsureComponent<ExpandNullforge.Creatures.DimensionShopStockAuthoring>(root);
            stock.itemNames = new System.Collections.Generic.List<string>();
            for (int i = 0; i < soldIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(soldIds[i]))
                {
                    stock.itemNames.Add(naming.QualifyReference(soldIds[i]));
                }
            }

            stock.sizeX = interaction.ShopGrid.x;
            stock.sizeY = interaction.ShopGrid.y;
        }

        /// <remarks>
        /// All three are removed first, so changing a door into a decoration actually stops it
        /// being a door — the marker the pathfinder reads goes with it — rather than leaving it
        /// carrying the old kind with nothing asking.
        /// </remarks>
        private static void ApplyKind(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            DimensionNamingContext naming,
            DimensionWorldObjectGenerationReport report)
        {
            RemoveComponentIfPresent<DoorAuthoring>(root);
            RemoveComponentIfPresent<BedAuthoring>(root);
            RemoveComponentIfPresent<TrophyAuthoring>(root);

            switch (worldObject.Kind)
            {
                case DimensionWorldObjectKind.Door:
                {
                    // FALSE, AND THAT IS THE FIX FOR A CRASH THIS FRAMEWORK CREATED. With it true,
                    // DoorConverter adds ColliderVariationCD, and ColliderVariationSystem's query
                    // is ObjectDataCD + PhysicsCollider + ColliderVariationCD. A generated door
                    // used to carry no PhysicsCollider, so it never matched; now that every placed
                    // object has one, it matches.
                    //
                    // IT IS NO LONGER A CRASH. Every generated object now says its look is not
                    // part of its identity (DimensionQueryCompanions.ItsLookIsNotPartOfItsIdentity)
                    // so the lookup returns this door's own prefab rather than Entity.Null. What
                    // the system would then copy is the collider the door already has, because
                    // this framework writes one prefab per object. Staying out of the query says
                    // the same thing without spending a job on it, and it keeps the door's active
                    // variation from being tracked as if a swap were happening.
                    EnsureComponent<DoorAuthoring>(root).changesColliderByVariation = false;

                    report.Warnings.Add(
                        "'" + worldObject.DisplayName + "' is a door, and it keeps one body and " +
                        "one look whether it is open or shut, so a player cannot walk through the " +
                        "doorway and the door does not appear to move. Core Keeper builds an open " +
                        "door as a second object with its own picture and its own shape, and this " +
                        "framework writes one object per door. Its shut body lies " +
                        (worldObject.Rules.DoorwayRunsAlongTheWall
                            ? "along the wall, blocking a doorway that runs north to south."
                            : "across the corridor, blocking a doorway that runs east to west. " +
                              "If the wall it sits in runs north to south, tick the doorway " +
                              "answer on its rules, or the hitbox lies at right angles to the " +
                              "door and a player is stopped beside the doorway instead of in it."));

                    // A DOOR WITH ONLY THIS NEVER OPENS. DoorConverter produces a marker the
                    // pathfinder reads and nothing that swings. Opening is
                    // TriggerSetVariationSystem, and the component that feeds it is added by the
                    // sweep at the end of Configure, which is the only point where the door's body
                    // exists and can be checked. Half of the fix belongs here, though: the number
                    // the system toggles the look TO.
                    //
                    // It sets the object's variation to this number when used. Left at zero, a door
                    // that opened would toggle to the look it already had. One is the second look —
                    // closed is the first — which is how the game's two-look doors are built.
                    ObjectAuthoring identity = root.GetComponent<ObjectAuthoring>();
                    if (identity != null && identity.variationToToggleTo == 0)
                    {
                        identity.variationToToggleTo = 1;
                    }

                    break;
                }

                case DimensionWorldObjectKind.Bed:
                    // Lying down needs an occupiable slot beside this, which the sweep at the end
                    // of Configure checks — it cannot be checked here, because the pass that adds
                    // the slot runs after this one.
                    EnsureComponent<BedAuthoring>(root);
                    break;

                case DimensionWorldObjectKind.Trophy:
                {
                    TrophyAuthoring trophy = EnsureComponent<TrophyAuthoring>(root);
                    ObjectID enemy = ResolveObject(worldObject.SummonsEnemyId);

                    // Written every time, the None included. TrophyConverter writes TrophyCD
                    // whatever the id is, so for one of the mod's own creatures the None is the
                    // placeholder the link hydration overwrites at load — and writing it also
                    // stops a creature changed to a typo from quietly staying the old one.
                    trophy.enemyToSpawnFromSpawnerPlatform = enemy;
                    if (enemy == ObjectID.None &&
                        !IsDeferred(worldObject.SummonsEnemyId) &&
                        !string.IsNullOrEmpty(worldObject.SummonsEnemyId))
                    {
                        report.Warnings.Add(
                            "'" + worldObject.DisplayName + "' is a trophy for '" +
                            worldObject.SummonsEnemyId + "', which is neither one of this mod's " +
                            "creatures nor one the game has. It will summon nothing.");
                    }

                    break;
                }
            }
        }

        private static void ApplyPlacement(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            System.Func<string, int> resolveTileset,
            DimensionWorldObjectGenerationReport report,
            DimensionNamingContext naming)
        {
            PlaceableObjectAuthoring placeable = EnsureComponent<PlaceableObjectAuthoring>(root);
            DimensionObjectSpine.ApplyWorldRoles(
                root,
                worldObject.WorldRoles,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred,
                QualifyReference);

            DimensionObjectSpine.ApplyEventTerminal(
                root,
                worldObject.EventTerminal,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyFinalTouches(
                root,
                worldObject.FinalTouches,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyBeamAndAmbience(
                root,
                worldObject.BeamAndAmbience,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyHidingAndHatching(
                root,
                worldObject.HidingAndHatching,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyManaAndAura(
                root,
                worldObject.ManaAndAura,
                ResolveObject,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplySpawnerAndOrb(
                root,
                worldObject.SpawnerAndOrb,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyChainReaction(
                root,
                worldObject.ChainReaction,
                ResolveObject,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred);

            DimensionObjectSpine.ApplyMachineRoles(
                root,
                worldObject.MachineRoles,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyTerrainEffects(
                root,
                worldObject.TerrainEffects,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyLooksAtItsSurroundings(
                root,
                worldObject.AdaptsToSurroundings,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyNest(
                root,
                worldObject.Nest,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyTrader(
                root,
                worldObject.Trader,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred);

            DimensionObjectSpine.ApplyObjectRoles(
                root,
                worldObject.Roles,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyReactsToNearby(
                root,
                worldObject.ReactsToNearby,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplySummoningCircle(
                root,
                worldObject.SummoningCircle,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                delegate(string ownId)
                {
                    // The mod's own creatures have no ids until runtime; their qualified name
                    // is what hydration resolves.
                    return naming.QualifyGenerated(ownId);
                });

            // The reacts-to-nearby block above writes two of the same components this one does, so
            // this one is told what it claimed. Without that, an object that only reacts to
            // something nearby had its reaction removed here, one call later, in silence.
            DimensionObjectSpine.ApplyGates(
                root,
                worldObject.Gate,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                worldObject.ReactsToNearby);

            DimensionObjectSpine.ApplyKeepsItsFloor(
                root,
                worldObject.KeepsItsFloor,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyExtractable(
                root,
                worldObject.Extractable,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyPoweredMachine(
                root,
                worldObject.PoweredMachine,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            // The gate block goes in with the melody block: they write the same component, and
            // handing this pass both is what keeps a door that opens to a tune working. It runs
            // after ApplyGates for the same reason it always did — the gate's other two openings
            // are separate components.
            DimensionObjectSpine.ApplyMelodyResponse(
                root,
                worldObject.MelodyResponse,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred,
                worldObject.Gate);

            DimensionObjectSpine.ApplyPlacementRules(
                root,
                worldObject.PlacementRules,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });
            placeable.prefabTileSize = worldObject.TileSize;

            // NOTHING EVER WROTE THIS BEFORE. The body calculation has carried a corner-offset
            // term since the pass that measured PlanterBoxEntity, and the only writer in the tree
            // set it to zero, so the term could not do anything on any path that existed. It is
            // the author's answer now, and it moves the hitbox and the placement together.
            placeable.prefabCornerOffset = worldObject.StandsOnTile;
            placeable.canBePlacedOnAnyWalkableTile = true;
            placeable.canBePlacedOnWater = worldObject.CanBePlacedOnWater;

            if (worldObject.SurfacePriority != 0)
            {
                EnsureComponent<SurfacePriorityAuthoring>(root).Value = worldObject.SurfacePriority;
            }
            else
            {
                RemoveComponentIfPresent<SurfacePriorityAuthoring>(root);
            }
        }

        /// <summary>
        /// The three separate lighting mechanisms that live on the entity, written independently.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A torch carries <c>ActAsLightSourceWhenHeldInHand</c> and <c>TableItemLightSource</c> and
        /// NOT <c>GlowLight</c>. Collapsing them into one answer would produce lamps that glow and
        /// light nothing.
        /// </para>
        /// <para>
        /// THE FOURTH ONE IS NOT HERE, AND CANNOT BE. The light a placed object throws on the floor
        /// around it has no component on the entity at all — Core Keeper keeps it as a subtree of
        /// nodes in the graphical prefab — so it is written by
        /// <c>DimensionInteractionVisualUtility</c>, from the answers on the asset's own light
        /// block. Whoever comes looking for it here should look there.
        /// </para>
        /// </remarks>
        private static void ApplyLight(GameObject root, DimensionWorldObjectAsset worldObject)
        {
            if (worldObject.LightsTheRoomWhenPlaced)
            {
                EnsureComponent<TableItemLightSourceAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<TableItemLightSourceAuthoring>(root);
            }

            if (worldObject.LightsTheRoomWhenHeld)
            {
                ActAsLightSourceWhenHeldInHandAuthoring held =
                    EnsureComponent<ActAsLightSourceWhenHeldInHandAuthoring>(root);
                held.color = worldObject.HeldLightColor;
                held.range = worldObject.HeldLightRange;
            }
            else
            {
                RemoveComponentIfPresent<ActAsLightSourceWhenHeldInHandAuthoring>(root);
            }

            if (worldObject.ObjectItselfGlows)
            {
                GlowLightAuthoring glow = EnsureComponent<GlowLightAuthoring>(root);
                glow.color = worldObject.GlowColor;
                glow.intensity = worldObject.GlowIntensity;
            }
            else
            {
                RemoveComponentIfPresent<GlowLightAuthoring>(root);
            }
        }

        private static void ApplyBreaking(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            DimensionWorldObjectGenerationReport report)
        {
            if (worldObject.CannotBeAttacked)
            {
                // THE ONE-FRAME FLAG SURVIVED A REGENERATE. This reuses whatever the reloaded
                // prefab had, and the pass that owns the one-frame answer returns without clearing
                // it on exactly this branch — so an object that was once "untouchable for one frame
                // only" and is now "cannot be attacked" kept the stale true, the game stripped its
                // protection a frame later, and the generation report said in plain words that the
                // permanent rule had won. It is written here because this is the branch that makes
                // that claim.
                EnsureComponent<CantBeAttackedAuthoring>(root).removeAfterFirstFrame = false;

                RemoveComponentIfPresent<MineableAuthoring>(root);
                RemoveComponentIfPresent<HealthAuthoring>(root);
                RemoveComponentIfPresent<DamageReductionAuthoring>(root);

                // FIRST, OR THE REMOVAL BELOW IS REFUSED AND SAYS SO ON EVERY GENERATE. An earlier
                // pass writes "changes look when hit", and that component requires the took-damage
                // state — so trying to take the state off logs a warning naming the object, every
                // time, and the look-change could never fire anyway because nothing can damage an
                // object that cannot be attacked.
                if (root.GetComponent<ChangeVariationWhenTookDamageAuthoring>() != null)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenTookDamageAuthoring>(root);
                    report.Warnings.Add(
                        "'" + worldObject.DisplayName + "' changes its look when it is hit and " +
                        "also cannot be attacked. Nothing can hit it, so the look never changes; " +
                        "that answer was left off.");
                }

                RemoveComponentIfPresent<TookDamageStateAuthoring>(root);

                // NOT DAMAGEABLE IS NOT THE SAME AS NOT ALIVE. Three separate features on an
                // unbreakable object are gated on it having a state at all: music near it only
                // switches between its combat and its quiet track for something the game sees as
                // idle (MusicAreaSystem asks for StateInfoCD and IdleStateCD), a trader's shelves
                // are only stocked for something with a state (MerchantBuyInventorySystem), and a
                // trap that attacks continuously writes its state every tick
                // (AttackContinuouslyStateSystem). A decorative, unbreakable music source is the
                // natural setting for every one of those, and it used to switch them all off.
                // Idle and the state root cost an unbreakable object nothing — they are how it says
                // "nothing is happening" — while death and took-damage stay off, which is what
                // "cannot be attacked" actually means.
                EnsureComponent<StateAuthoring>(root);
                EnsureComponent<IdleStateAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<CantBeAttackedAuthoring>(root);
                EnsureComponent<MineableAuthoring>(root);
                DimensionObjectSpine.ApplyDamageableStates(root);

                HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
                health.dontCalculateHealthFromLevel = true;
                health.maxHealth = worldObject.HitsToBreak;
                health.startHealth = worldObject.HitsToBreak;
                health.maxHealthMultiplier = 1f;

                DamageReductionAuthoring reduction = EnsureComponent<DamageReductionAuthoring>(root);
                reduction.calculateReductionFromLevel = false;
                reduction.reductionMultiplier = 1f;
                reduction.reduction = 0;
                reduction.maxDamagePerHit = DamagePerHit;
                reduction.minDamagePerHit = 0;
            }

            if (worldObject.DisappearsOnItsOwn)
            {
                // PlatformDependentValue, not a float: Core Keeper can give a different lifetime per
                // console. The one-argument constructor is how it says "the same everywhere", which
                // is what an author who typed one number means.
                EnsureComponent<DestroyTimerAuthoring>(root).lifetime =
                    new Pug.UnityExtensions.PlatformDependentValue<float>(
                        worldObject.DisappearsAfterSeconds);
            }
            else
            {
                RemoveComponentIfPresent<DestroyTimerAuthoring>(root);
            }
        }

        private static void Warn(
            DimensionWorldObjectAsset worldObject,
            DimensionWorldObjectGenerationReport report)
        {
            if (worldObject.GlowsButLightsNothing)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' glows but lights nothing. In Core Keeper those " +
                    "are separate components — a glow tints the object, it does not light the room. " +
                    "If it is meant to be a lamp, tick that it lights the room when placed.");
            }

            if (worldObject.IsATrophyThatSummonsNothing)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' is a trophy with no enemy named, so using it " +
                    "will summon nothing.");
            }

            if (worldObject.CanNeverBeRemoved)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' cannot be attacked and never expires, so a " +
                    "player who places one can never take it back.");
            }
        }

        /// <summary>
        /// Writes what other items said drops from this object.
        /// </summary>
        /// <remarks>
        /// A world object can be a drop source too — breaking rubble or a pot is exactly the
        /// <c>Destructible</c> kind. Anything needing an amount range or a biome is left for the
        /// bootstrap to register against a loot table, since per-object custom loot carries neither.
        /// </remarks>
        private static void ApplyCollectedDrops(
            GameObject root,
            DimensionDropsForSource dropsFromThis,
            string objectIdentifier,
            DimensionWorldObjectGenerationReport report)
        {
            // TAKEN BACK BEFORE THE EARLY RETURN, and that is the point. Generation reloads the
            // prefab it wrote last time, so deleting every drop off this object used to leave last
            // time's drops baked on it with no way to take them off, and a renamed mod left it
            // pointing at a loot table minted from the old name that nothing ever fills.
            DimensionDropEmitter.ClearAnyLootTheLastGenerateWrote(root);

            if (dropsFromThis == null || dropsFromThis.Drops.Count == 0)
            {
                return;
            }

            List<DimensionResolvedDrop> needsATable = DimensionDropEmitter.ApplyCustomLoot(
                root,
                dropsFromThis,
                delegate(string itemId) { return ResolveObject(itemId); },
                delegate(string message) { report.Warnings.Add(message); },
                IsDeferred);

            for (int i = 0; i < needsATable.Count; i++)
            {
                report.DropsNeedingALootTable.Add(needsATable[i]);
            }

            // Something that breaks and gives an item needs a loot table for anything custom loot
            // cannot carry. Most world objects are authored without one, and given none there was
            // nowhere for the drop to be registered at load, so it never happened.
            if (needsATable.Count > 0)
            {
                DimensionDropEmitter.EnsureALootTableToHangDropsOn(
                    root,
                    binder.Naming.QualifyGenerated(objectIdentifier),
                    delegate(string message) { report.Warnings.Add(message); });
            }
        }

        /// <summary>
        /// The run's binder: the game's own numbers baked, this mod's own names left for the game.
        /// </summary>
        private static DimensionObjectBinder binder = new DimensionObjectBinder(default);

        /// <summary>Resolves a name to one of the GAME's own numbers, and nothing else.</summary>
        /// <remarks>
        /// THE RULE, and it holds for every generator: at generation time the game is not running,
        /// so the only names with numbers are the game's own. <c>API.Authoring.GetObjectID</c> reads
        /// a runtime dictionary that is empty here, so asking it can only ever answer <c>None</c>
        /// while reading as though it covered the mod's own objects. A name of this mod's comes
        /// back <c>None</c> on purpose, and <see cref="IsDeferred"/> is what the caller asks next.
        /// </remarks>
        private static ObjectID ResolveObject(string itemId)
        {
            return DimensionObjectBinder.Vanilla(itemId);
        }

        /// <summary>True when the name is one of this mod's own and the runtime will fill it in.</summary>
        private static bool IsDeferred(string itemId)
        {
            return binder.IsDeferred(itemId);
        }

        /// <summary>The full name the game registers one of this mod's own objects under.</summary>
        private static string QualifyReference(string itemId)
        {
            return binder.Qualify(itemId);
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }

    }
}
