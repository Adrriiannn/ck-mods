using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{

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
    internal static partial class DimensionWorldObjectGenerator
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
            // that read one only read it for an item in hand. The field inside the effects block is
            // not drawn anywhere, and this keeps whatever an existing world object holds in it
            // rather than dropping the value on the next generate.
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
