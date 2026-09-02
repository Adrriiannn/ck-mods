using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating containers did.</summary>
    internal sealed class DimensionContainerGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>Drops the prefab could not carry, left for the loader to register.</summary>
        public readonly List<DimensionResolvedDrop> DropsNeedingALootTable = new List<DimensionResolvedDrop>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Containers: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Turns five plain questions about a container into the dozen components Core Keeper needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole point of this class is that it is the only place that knows a container is a dozen
    /// components. An author answers "how big, can it break, what fits, what does it look like, what
    /// happens when an item is put in"; the mapping from those to <c>InventoryAuthoring</c>,
    /// <c>PlaceableObjectAuthoring</c>, <c>HealthAuthoring</c>, <c>DamageReductionAuthoring</c>,
    /// <c>MineableAuthoring</c> and the rest lives here and nowhere else.
    /// </para>
    /// <para>
    /// The component set is copied from what Core Keeper's own chests carry, read out of
    /// <c>Assets/GameObject/ChestEntity.prefab</c> and its 80 siblings — <c>MineableAuthoring</c> plus
    /// <c>HealthAuthoring</c> plus <c>DamageReductionAuthoring</c> plus the four state components, and
    /// notably NOT <c>DestructibleObjectAuthoring</c>, which no chest in the game carries. Following
    /// vanilla's own composition is what makes a generated container behave like a chest rather than
    /// like a barrel that happens to hold things.
    /// </para>
    /// <para>
    /// Components are REMOVED when their answer is no, not merely left unconfigured. A container that
    /// stops being breakable must lose its damage components, or it keeps its old behaviour with
    /// nothing in the authoring asset still asking for it — the same rule the tileset and creature
    /// generators follow.
    /// </para>
    /// </remarks>
    internal static class DimensionContainerGenerator
    {
        /// <summary>The folder inside a mod where generated container prefabs live.</summary>
        public const string FolderName = "Containers";

        /// <summary>
        /// Damage a single hit may do, whatever swung it.
        /// </summary>
        /// <remarks>
        /// Core Keeper caps the hit rather than scaling the health: 1,166 of the 1,286 prefabs that
        /// carry <c>DamageReductionAuthoring</c> set <c>maxDamagePerHit</c> to 1, chests among them.
        /// That is what makes "hits to break" a number an author can reason about, because a better
        /// pickaxe does not change it.
        /// </remarks>
        private const int DamagePerHit = 1;

        public static DimensionContainerGenerationReport Generate(
            IEnumerable<DimensionContainerAsset> containers,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionDropPlan plan = null)
        {
            DimensionContainerGenerationReport report = new DimensionContainerGenerationReport();
            if (containers == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no containers were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);
            binder = new DimensionObjectBinder(naming);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionContainerAsset container in containers)
                {
                    GenerateOne(container, outputFolder, naming, report, plan);
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

        private static void GenerateOne(
            DimensionContainerAsset container,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionContainerGenerationReport report,
            DimensionDropPlan plan)
        {
            if (container == null || !container.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(container.ContainerId))
            {
                report.Skipped.Add("A container with no id was skipped.");
                return;
            }

            if (container.HasMoreSlotRulesThanSlots)
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' has " + container.EffectiveSlotRules.Length +
                    " slot rules but only " + container.TotalSlots + " slots. Core Keeper drops the " +
                    "overflow, so the last rules will not apply to anything.");
            }

            if (container.IndestructibleWasRefused)
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' is marked unbreakable, but it is crafted by " +
                    "players. A player could place it in their own base and never remove it, so it " +
                    "will generate breakable. Set it to be placed by the world instead, or untick " +
                    "unbreakable.");
            }

            if (container.ReactionDoesNothing)
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' reacts to '" + container.ReactsToItemId +
                    "' but nothing was set to happen. The game will check every item moved into it " +
                    "and the player will see no result.");
            }

            if (container.HasOrphanedReactionContents)
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' names loot for its reaction but nothing for it " +
                    "to become. Those contents are only ever put into the NEW object, so as written " +
                    "they are dropped.");
            }

            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(container.ContainerId, "Container") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(container.ContainerId);

            try
            {
                Configure(root, container, naming, report, plan);
                // drawsItself: a container is a thing a player looks at as much as a thing they
                // open, so its graphical prefab carries a renderer and is written even when
                // nothing uses it. Without this every generated container was invisible where it
                // stood — or, worse, wore whichever of the game's own chests happened to own the
                // shared Chest pool.
                DimensionInteractionVisualUtility.Apply(
                    root,
                    container.Interaction,
                    outputFolder,
                    DimensionGeneratedPrefabUtility.SanitizeAuthoredName(container.ContainerId, "Container"),
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + container.DisplayName + "' " + message);
                    },
                    true,
                    container.Sprite);
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
                report.Errors.Add(container.ContainerId + " failed to generate: " + exception.Message);
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
            DimensionContainerAsset container,
            DimensionNamingContext naming,
            DimensionContainerGenerationReport report,
            DimensionDropPlan plan)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(container.ContainerId);
            // Core Keeper has no "chest" type — a container is something you place, and what makes it
            // a container is the inventory on it. Typing it otherwise would keep it out of the
            // placement UI entirely.
            obj.objectType = ObjectType.PlaceablePrefab;
            obj.initialAmount = 1;

            Rarity rarity;
            if (!string.IsNullOrEmpty(container.RarityId) &&
                Enum.TryParse(container.RarityId, false, out rarity))
            {
                obj.rarity = rarity;
            }

            ApplyLook(root, obj, container, report);
            ApplyInventory(root, container, naming, report);
            ApplyPlacement(root, container, naming, report);
            ApplyBreaking(root, container);
            ApplyContentsReaction(root, container, naming, report);

            // What every vanilla chest carries and ours did not: animation, paint, facing, a
            // description, and the automation hook that lets conveyors feed it.
            DimensionObjectSpine.ApplyUniversal(root, false);

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                container.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add("'" + container.DisplayName + "' " + message);
                });
            DimensionObjectSpine.ApplyPlacedObject(root, true, container.FacesPlacementDirection);

            // The label that floats above it. ApplyPlacedObject has just made sure the store exists;
            // this is the only thing that puts anything IN it, and DescriptionConverter copies
            // initialText into the entity's DescriptionBuffer at bake time. Written every generate,
            // blank included, so clearing the field in the asset clears it on the object too.
            DescriptionAuthoring label = EnsureComponent<DescriptionAuthoring>(root);
            label.initialText = container.LabelItComesWith;

            EnsureComponent<Pug.Automation.AutomatedStorageAuthoring>(root);
            DimensionObjectSpine.ApplyBasics(
                root,
                container.Basics,
                delegate(string message)
                {
                    report.Warnings.Add("'" + container.DisplayName + "' " + message);
                });
            DimensionObjectSpine.ApplyInitialConditions(
                root,
                container.Conditions,
                delegate(string message)
                {
                    report.Warnings.Add("'" + container.DisplayName + "' " + message);
                });

            // A chest in a dungeon is a drop location like any creature: something an item can say
            // it comes out of. Routed through the same emitter so a chest and a slime disagree about
            // nothing, and so a range or a biome restriction is handed on rather than lost.
            DimensionDropsForSource dropsFromThis = plan == null
                ? null
                : plan.For(DimensionDropSourceKind.Container, container.ContainerId);

            // TAKEN BACK BEFORE ANYTHING IS DECIDED, and outside the guard below. Generation
            // reloads the prefab it wrote last time, so without this an author who deletes every
            // drop off this chest keeps last time's drops baked on it for ever, and a renamed mod
            // leaves it pointing at a table minted from the old name that the runtime never fills.
            DimensionDropEmitter.ClearAnyLootTheLastGenerateWrote(root);

            if (dropsFromThis != null && dropsFromThis.Drops.Count > 0)
            {
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

                // A chest with drops that only a loot table can carry needs a loot table. Given
                // none it carried no DropsLootFromLootTableCD at all, so there was nowhere to
                // register the drop at load and it never happened.
                if (needsATable.Count > 0)
                {
                    DimensionDropEmitter.EnsureALootTableToHangDropsOn(
                        root,
                        naming.QualifyGenerated(container.ContainerId),
                        delegate(string message) { report.Warnings.Add(message); });
                }
            }

            // The sweep, last. A chest is a placed object like any other, and the same answers on
            // it — reacting to what is nearby, wiring, being sat on — need the same companions the
            // game's own prefabs carry beside them.
            DimensionQueryCompanions.FinishAWorldObject(
                root,
                container.DisplayName,
                delegate(string message) { report.Warnings.Add(message); });
        }

        /// <summary>
        /// Gives the container a name, a tooltip, an icon and a picture in the world.
        /// </summary>
        /// <remarks>
        /// <para>
        /// FOUR SEPARATE PLACES, ONE ANSWER EACH, AND THREE OF THEM WERE EMPTY. The NAME and the
        /// TOOLTIP come from the mod's localization table, under <c>Items/&lt;qualified object
        /// name&gt;</c> and the same key with <c>Desc</c> appended — those rows are written by
        /// <c>DimensionLocalizationPlan</c> against this same qualified name, so all this side has
        /// to do is not change the name. The ICON is <c>InventoryItemAuthoring.icon</c>, which is
        /// what <c>ObjectInfo</c> reads and every inventory slot draws; without the component the
        /// container drew as a blank square in the hotbar and as nothing at all on the ground once
        /// broken. The PICTURE IN THE WORLD is <c>additionalSprites[0]</c>, read back at runtime by
        /// <see cref="ExpandNullforge.Containers.DimensionContainerView"/> — and by the game itself
        /// for a container held over a player's head, which is the field's existing meaning.
        /// </para>
        /// <para>
        /// The icon falls back to the world sprite on the asset, so a container authored with one
        /// picture is complete rather than half-drawn.
        /// </para>
        /// </remarks>
        private static void ApplyLook(
            GameObject root,
            ObjectAuthoring obj,
            DimensionContainerAsset container,
            DimensionContainerGenerationReport report)
        {
            obj.additionalSprites = new System.Collections.Generic.List<Sprite>();
            if (container.Sprite != null)
            {
                obj.additionalSprites.Add(container.Sprite);
            }

            InventoryItemAuthoring inventoryItem = EnsureComponent<InventoryItemAuthoring>(root);
            inventoryItem.icon = container.Icon;
            inventoryItem.smallIcon = container.Icon;
            // A container is one thing you place, not a pile you carry. Every chest in the game
            // agrees; a stackable one would let a player merge two chests into a count.
            inventoryItem.isStackable = false;
            if (inventoryItem.requiredObjectsToCraft == null)
            {
                inventoryItem.requiredObjectsToCraft =
                    new System.Collections.Generic.List<InventoryItemAuthoring.CraftingObject>();
            }

            if (container.Icon == null)
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' has no icon and no sprite, so it draws as an " +
                    "empty square in the inventory. Drop a picture into its Icon or Sprite field.");
            }

            if (string.IsNullOrEmpty(container.DisplayName))
            {
                report.Warnings.Add(
                    "'" + container.ContainerId + "' has no name, so players see its raw id. Give " +
                    "it a name.");
            }
        }

        /// <summary>
        /// Writes the slot grid and the rules about what fits.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT write <c>itemsInInventory</c> or <c>addLootFromTable</c>. A container
        /// definition is the kind of thing a chest is, and a crafted chest is empty; what a particular
        /// chest holds when it appears belongs to the scene that places it. Core Keeper does exactly
        /// this — 215 of its 216 containers author no contents at all, and the one that does is
        /// <c>MoldChestEntity_PuzzleGNature1T2Dynamos_55</c>, a per-scene variant of an ordinary chest.
        /// </remarks>
        private static void ApplyInventory(
            GameObject root,
            DimensionContainerAsset container,
            DimensionNamingContext naming,
            DimensionContainerGenerationReport report)
        {
            InventoryAuthoring inventory = EnsureInventory(root);
            inventory.sizeX = container.SlotsAcross;
            inventory.sizeY = container.SlotsDown;
            inventory.maxExtraSize = container.UpgradeableExtraSlots;
            inventory.canOnlyContainOneItemPerSlot = container.OneItemPerSlot;
            inventory.objectsGetLockedInPlace = container.ContentsAreLocked;
            inventory.cantAddObjectsToInventory = container.CannotAddItems;
            inventory.autoTransferEnabled = container.AutoTransfer;

            inventory.slotRequirements = BuildSlotRequirements(container, naming, report);
            inventory.itemsInInventory = new List<ObjectData>();
            inventory.addLootFromTable = LootTableID.Empty;

            // An upgradeable container needs the component that carries the upgrade, not just a
            // number: the number alone says how far it could go and nothing knows how to take it there.
            if (container.UpgradeableExtraSlots > 0 || container.WantsGrowingInventorySettings)
            {
                ExtraInventorySizeAuthoring extra =
                    EnsureComponent<ExtraInventorySizeAuthoring>(root);
                extra.isPouch = container.IsAPouch;
                extra.sameSizeForAllLevels = new Pug.UnityExtensions.OptionalValue<int>
                {
                    hasValue = container.HasAFixedSize,
                    value = container.SameSizeAtEveryLevel
                };

                extra.canOnlyContainObjectsWithCategoryTags =
                    new List<ObjectCategoryTag>();
                string[] tags = container.OnlyAcceptsCategoryTags;
                for (int i = 0; i < tags.Length; i++)
                {
                    ObjectCategoryTag tag;
                    if (Enum.TryParse(tags[i], false, out tag))
                    {
                        extra.canOnlyContainObjectsWithCategoryTags.Add(tag);
                    }
                    else
                    {
                        report.Warnings.Add(
                            "'" + container.DisplayName + "' only accepts items tagged '" +
                            tags[i] + "', which is not a category the game has, so that " +
                            "restriction does nothing.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<ExtraInventorySizeAuthoring>(root);
            }
        }

        private static List<SlotRequirement> BuildSlotRequirements(
            DimensionContainerAsset container,
            DimensionNamingContext naming,
            DimensionContainerGenerationReport report)
        {
            List<SlotRequirement> requirements = new List<SlotRequirement>();
            DimensionContainerSlotRule[] rules = container.EffectiveSlotRules;

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionContainerSlotRule rule = rules[i];

                // Core Keeper resolves both of these itself, in OnValidate, without telling a player
                // anything — so the only place they can be surfaced is here, before the build.
                if (rule.NamesBothItemsAndTags)
                {
                    report.Warnings.Add(
                        "'" + container.DisplayName + "' has a slot rule naming both items and " +
                        "categories. Core Keeper allows one or the other and drops the categories, so " +
                        "only the named items will be accepted.");
                }

                SlotRequirement requirement = new SlotRequirement
                {
                    requirementAppliesToAllSlots = rule.AppliesToAllSlots,
                    dontShowAnyHint = !rule.ShowHint,
                    showInfoText = rule.ShowHint,
                    denyLegendaryRarity = rule.DenyLegendary,
                    acceptsObjectsWithTags = new List<ObjectCategoryTag>(),
                    acceptsObjectIds = new List<ObjectID>()
                };

                string[] tags = rule.AcceptsCategoryTags;
                for (int t = 0; t < tags.Length; t++)
                {
                    ObjectCategoryTag tag;
                    if (Enum.TryParse(tags[t], false, out tag))
                    {
                        requirement.acceptsObjectsWithTags.Add(tag);
                    }
                    else
                    {
                        report.Warnings.Add(
                            "'" + container.DisplayName + "' has a slot rule naming category '" +
                            tags[t] + "', which the game does not have. That part of the rule was " +
                            "left out.");
                    }
                }

                string[] items = rule.AcceptsItemIds;
                int leftOutByTheCap = 0;
                for (int o = 0; o < items.Length; o++)
                {
                    ObjectID objectID = ResolveObject(items[o]);
                    bool usable = objectID != ObjectID.None || IsDeferred(items[o]);
                    if (!usable)
                    {
                        report.Warnings.Add(
                            "'" + container.DisplayName + "' has a slot rule naming item '" +
                            items[o] + "', which is neither one of this mod's items nor one the " +
                            "game has. That part of the rule was left out.");
                        continue;
                    }

                    // THE CAP IS OBEYED HERE RATHER THAN BY THE GAME. InventoryAuthoring.OnValidate
                    // truncates the list past seven, so entries written beyond it vanished after
                    // this pass had already counted them — and the link rows kept counting, so a
                    // deferred item at position eight was told at load that its rule had no such
                    // position and to "generate again", which could never help. Both sides stop in
                    // the same place now.
                    //
                    // COUNTED, NOT PREDICTED. Above the loop the warning would fire on how many
                    // names were TYPED, so nine names of which three are misspellings lose nothing
                    // to the cap and the author is still told the game dropped some. Only names
                    // that really would have gone in are counted here.
                    if (requirement.acceptsObjectIds.Count >=
                        DimensionContainerSlotRule.MaxAcceptedItemIds)
                    {
                        leftOutByTheCap++;
                        continue;
                    }

                    if (objectID != ObjectID.None)
                    {
                        requirement.acceptsObjectIds.Add(objectID);
                    }
                    else
                    {
                        // A PLACEHOLDER AT THE RIGHT POSITION, not a dropped entry. The list becomes
                        // a FixedList32Bytes inside InventorySlotRequirementBuffer, and the link
                        // hydration writes into it by position — dropping this entry would slide
                        // every later one along and the write would land on the wrong item.
                        requirement.acceptsObjectIds.Add(ObjectID.None);
                    }
                }

                if (leftOutByTheCap > 0)
                {
                    report.Warnings.Add(
                        "'" + container.DisplayName + "' has a slot rule that would accept " +
                        (requirement.acceptsObjectIds.Count + leftOutByTheCap) + " items. Core " +
                        "Keeper allows " + DimensionContainerSlotRule.MaxAcceptedItemIds +
                        ", so the last " + leftOutByTheCap + " are left out — use a category tag " +
                        "instead.");
                }

                requirements.Add(requirement);
            }

            return requirements;
        }

        /// <summary>
        /// Resolves a list of authored items into the game's own item records.
        /// </summary>
        /// <remarks>
        /// Used for what a container BECOMES, never for what it starts with — see
        /// <see cref="ApplyInventory"/> for why a container definition holds nothing.
        /// </remarks>
        private static List<ObjectData> BuildItems(
            DimensionSceneContainerItem[] source,
            DimensionContainerAsset container,
            DimensionNamingContext naming,
            DimensionContainerGenerationReport report)
        {
            List<ObjectData> items = new List<ObjectData>();
            if (source == null)
            {
                return items;
            }

            for (int i = 0; i < source.Length; i++)
            {
                DimensionSceneContainerItem item = source[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                ObjectID objectID = ResolveObject(item.ItemId);
                if (objectID == ObjectID.None)
                {
                    // TWO DIFFERENT PROBLEMS, TWO DIFFERENT SENTENCES. One of the mod's own items
                    // is not a mistake — it simply has no number yet, and what a container turns
                    // into carries its contents in the sealed property blob, so there is nothing
                    // for a load-time pass to write into. Telling the author it "is not a
                    // registered object" sent them looking for a typo in a name they had spelled
                    // correctly. The reference is still lost either way, so both say what to do.
                    string switchedOff = binder.ExplainIfSwitchedOff(
                        "'" + container.DisplayName + "' has one of its own items in what it " +
                        "becomes,",
                        item.ItemId);
                    if (switchedOff != null)
                    {
                        report.Warnings.Add(switchedOff);
                    }
                    else if (naming.Owns(item.ItemId))
                    {
                        report.Warnings.Add(
                            "'" + container.DisplayName + "' turns into something holding '" +
                            item.ItemId + "', one of your own items. What a container turns into " +
                            "is fixed when the game builds its objects, before your items have " +
                            "numbers, so that one cannot go in there. Use one of the game's items, " +
                            "or put yours in through a loot table instead.");
                    }
                    else
                    {
                        report.Warnings.Add(
                            "'" + container.DisplayName + "' turns into something holding '" +
                            item.ItemId + "', which is neither one of this mod's items nor one the " +
                            "game has, so that one was left out. Check the spelling.");
                    }

                    continue;
                }

                items.Add(new ObjectData
                {
                    objectID = objectID,
                    amount = item.Amount,
                    variation = 0
                });
            }

            return items;
        }

        private static void ApplyPlacement(
            GameObject root,
            DimensionContainerAsset container,
            DimensionNamingContext naming,
            DimensionContainerGenerationReport report)
        {
            PlaceableObjectAuthoring placeable = EnsureComponent<PlaceableObjectAuthoring>(root);
            DimensionObjectSpine.ApplyMelodyResponse(
                root,
                container.MelodyResponse,
                delegate(string objectId) { return ResolveObject(objectId); },
                delegate(string message)
                {
                    report.Warnings.Add("'" + container.DisplayName + "' " + message);
                },
                IsDeferred);

            DimensionObjectSpine.ApplyPlacementRules(
                root,
                container.PlacementRules,
                delegate(string message)
                {
                    report.Warnings.Add("'" + container.DisplayName + "' " + message);
                });
            placeable.prefabTileSize = container.TileSize;
            placeable.canBePlacedOnAnyWalkableTile = true;
            placeable.canBePlacedOnWater = container.CanBePlacedOnWater;

            if (container.FacesPlacementDirection)
            {
                // Facing is stored as the object's variation, which is how the game rotates anything
                // placeable — so this component IS the rotation, not a hint about it.
                EnsureComponent<DirectionBasedOnVariationAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<DirectionBasedOnVariationAuthoring>(root);
            }
        }

        /// <summary>
        /// Decides whether it can be broken, by what, in how many hits, and what falls out.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The component set here is Core Keeper's own for a chest: <c>MineableAuthoring</c>,
        /// <c>HealthAuthoring</c>, <c>DamageReductionAuthoring</c> and the four state components that
        /// make it flash when hit and die when emptied of health. <c>DestructibleObjectAuthoring</c> is
        /// deliberately absent unless a drill is demanded — no chest in the game carries it, and it is
        /// the component that turns a thing into a smashable prop rather than a container.
        /// </para>
        /// <para>
        /// Indestructible is expressed by adding the marker AND removing the damage components, not by
        /// one or the other. Leaving them on something marked indestructible gives a container that
        /// cannot be destroyed but still flashes when hit — which reads as a bug.
        /// </para>
        /// </remarks>
        private static void ApplyBreaking(GameObject root, DimensionContainerAsset container)
        {
            if (!container.IsBreakable)
            {
                EnsureComponent<IndestructibleAuthoring>(root);
                RemoveComponentIfPresent<DestructibleObjectAuthoring>(root);
                RemoveComponentIfPresent<DamageReductionAuthoring>(root);
                RemoveComponentIfPresent<HealthAuthoring>(root);
                RemoveComponentIfPresent<MineableAuthoring>(root);
                RemoveComponentIfPresent<TookDamageStateAuthoring>(root);
                RemoveComponentIfPresent<DeathStateAuthoring>(root);
                return;
            }

            RemoveComponentIfPresent<IndestructibleAuthoring>(root);
            EnsureComponent<MineableAuthoring>(root);

            // The state machine is what makes damage visible and death happen at all. A chest with
            // health but no death state absorbs hits forever.
            EnsureComponent<StateAuthoring>(root);
            EnsureComponent<IdleStateAuthoring>(root);
            EnsureComponent<TookDamageStateAuthoring>(root);
            EnsureComponent<DeathStateAuthoring>(root);

            // Hits-to-break is health, because damage is capped at one point per hit just below.
            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            health.dontCalculateHealthFromLevel = true;
            health.maxHealth = container.HitsToBreak;
            health.startHealth = container.HitsToBreak;
            health.maxHealthMultiplier = 1f;

            DamageReductionAuthoring reduction = EnsureComponent<DamageReductionAuthoring>(root);
            reduction.calculateReductionFromLevel = false;
            reduction.reduction = container.RequiredMiningDamage;
            reduction.reductionMultiplier = 1f;
            reduction.maxDamagePerHit = DamagePerHit;
            reduction.minDamagePerHit = 0;

            // Only ore boulders demand a drill in vanilla, and the component that carries the demand
            // is the destructible one — so it appears only when the demand is actually made.
            if (container.RequiresDrill)
            {
                DestructibleObjectAuthoring destructible = EnsureComponent<DestructibleObjectAuthoring>(root);
                destructible.requiresDrill = true;
            }
            else
            {
                RemoveComponentIfPresent<DestructibleObjectAuthoring>(root);
            }

            if (container.DropsContentsWhenBroken)
            {
                RemoveComponentIfPresent<DontDropContainedAuthoring>(root);
            }
            else
            {
                EnsureComponent<DontDropContainedAuthoring>(root);
            }

            if (container.DropsItselfWhenBroken)
            {
                RemoveComponentIfPresent<DontDropSelfAuthoring>(root);
                // Vanilla chests carry this so a broken one returns exactly one chest rather than a
                // stack sized from whatever the drop maths worked out.
                EnsureComponent<AlwaysDropOneAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<AlwaysDropOneAuthoring>(root);
                EnsureComponent<DontDropSelfAuthoring>(root);
            }
        }

        /// <summary>
        /// Wires what happens when the trigger item is put in.
        /// </summary>
        /// <remarks>
        /// This one component is the whole of Core Keeper's locked-chest mechanism, and it does far
        /// more than change a sprite: it can re-instantiate the object as a different one and fill
        /// that new object from a loot table or an exact item list. <c>LockedCopperChest</c> is a
        /// one-slot container accepting <c>CopperKey</c> that becomes <c>CopperChest</c> — nothing
        /// more than these fields. Wiring only the variation, as an earlier pass did, left five of the
        /// seven knobs unreachable and made locked chests impossible to author.
        /// </remarks>
        private static void ApplyContentsReaction(
            GameObject root,
            DimensionContainerAsset container,
            DimensionNamingContext naming,
            DimensionContainerGenerationReport report)
        {
            if (!container.ReactsToContents)
            {
                RemoveComponentIfPresent<ChangeVariationWhenContainingObjectAuthoring>(root);
                return;
            }

            ObjectID trigger = ResolveObject(container.ReactsToItemId);
            if (trigger == ObjectID.None && !IsDeferred(container.ReactsToItemId))
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' is set to react to '" + container.ReactsToItemId +
                    "', which is neither one of this mod's items nor one the game has. It will " +
                    "not react.");
                RemoveComponentIfPresent<ChangeVariationWhenContainingObjectAuthoring>(root);
                return;
            }

            // The component STAYS for one of the mod's own keys, holding None until the link
            // hydration writes the real id at load. ChangeVariationWhenContainingObjectConverter
            // writes its component whatever the id is, so there is something to write into.

            ChangeVariationWhenContainingObjectAuthoring reaction =
                EnsureComponent<ChangeVariationWhenContainingObjectAuthoring>(root);
            reaction.objectID = trigger;
            reaction.variationToChangeTo = container.ReactionVariation;
            reaction.alsoRemoveCollider = container.RemoveColliderOnReaction;
            reaction.addItemsToNewObject = new List<ObjectData>();
            reaction.reinstantiateToNewObjectId = ObjectID.None;
            reaction.addLootFromTableToNewObject = LootTableID.Empty;

            EffectID effect;
            if (!string.IsNullOrEmpty(container.ReactionEffectId) &&
                Enum.TryParse(container.ReactionEffectId, false, out effect))
            {
                reaction.playEffectOnReinstantiate = effect;
            }
            else if (!string.IsNullOrEmpty(container.ReactionEffectId))
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' names effect '" + container.ReactionEffectId +
                    "', which this version of the game does not have. It will change without one.");
            }

            if (!container.BecomesSomethingElse)
            {
                return;
            }

            ObjectID becomes = ResolveObject(container.BecomesContainerId);
            if (becomes == ObjectID.None && !IsDeferred(container.BecomesContainerId))
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' is set to become '" + container.BecomesContainerId +
                    "', which is neither one of this mod's objects nor one the game has. It will " +
                    "change its look but stay itself, so the item that opened it is consumed for " +
                    "nothing.");
                return;
            }

            reaction.reinstantiateToNewObjectId = becomes;
            reaction.addItemsToNewObject = BuildItems(container.BecomesContents, container, naming, report);

            LootTableID lootTable;
            // Vanilla names first, then the mod's own seeded tables (minted id).
            if (!string.IsNullOrEmpty(container.BecomesLootTableId) &&
                DimensionEditorLootTables.TryResolve(container.BecomesLootTableId, out lootTable))
            {
                reaction.addLootFromTableToNewObject = lootTable;
            }
            else if (!string.IsNullOrEmpty(container.BecomesLootTableId))
            {
                report.Warnings.Add(
                    "'" + container.DisplayName + "' names loot table '" + container.BecomesLootTableId +
                    "', which is neither the game's nor this mod's. What it becomes will be empty.");
            }
        }

        /// <summary>
        /// Resolves an item name, qualifying it only when the mod owns it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A container that accepts a vanilla item must name the vanilla object; qualifying it would
        /// point the rule at something that does not exist, and the slot would silently accept nothing.
        /// </para>
        /// <para>
        /// Vanilla names are parsed off the <c>ObjectID</c> enum rather than looked up through
        /// <c>API.Authoring.GetObjectID</c>, because this runs at GENERATION time and that lookup is a
        /// runtime dictionary — <c>ModAPIAuthoring.ObjectIDLookup</c> is filled by
        /// <c>RegisterAuthoringGameObject</c> as mods load, so in the editor it is empty and every
        /// vanilla name resolves to <c>None</c>. Parsing the enum is the same thing the recipe
        /// generator does, and it turns a typo into a warning while the author is still looking at the
        /// dashboard.
        /// </para>
        /// <para>
        /// The runtime lookup is not tried afterwards, though a name the mod owns looks as if it
        /// could only live there. It does not exist there either at generation time — that
        /// dictionary is empty — so the call can only ever answer <c>None</c> while looking as
        /// though it covered the mod's own objects. Ownership is asked of the binder instead.
        /// </para>
        /// </remarks>
        private static ObjectID ResolveObject(string itemId)
        {
            return DimensionObjectBinder.Vanilla(itemId);
        }

        /// <summary>
        /// The run's binder: the game's own numbers baked, this mod's own names left for the game.
        /// </summary>
        private static DimensionObjectBinder binder = new DimensionObjectBinder(default);

        /// <summary>True when the name is one of this mod's own and the runtime will fill it in.</summary>
        private static bool IsDeferred(string itemId)
        {
            return binder.IsDeferred(itemId);
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        /// <summary>
        /// Adds the inventory without the exception Core Keeper's own <c>OnValidate</c> throws on a
        /// brand-new one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>InventoryAuthoring</c> declares <c>slotRequirements</c> and <c>itemsInInventory</c> with
        /// no field initialiser, and its <c>OnValidate</c> walks <c>slotRequirements.Count</c>
        /// unconditionally. Unity runs <c>OnValidate</c> synchronously inside <c>AddComponent</c>, so a
        /// freshly added component always throws a <c>NullReferenceException</c> there — a line before
        /// we would have assigned the lists, and too early for any ordering on our side to help.
        /// </para>
        /// <para>
        /// Unity logs that exception rather than propagating it, so the component still comes back
        /// intact and the generated prefab is correct either way. What the quiet window suppresses is a
        /// console error printed for every container a modder generates, which reads as generation
        /// having failed when it has not. The window is exactly one call wide and the logger is
        /// restored in a <c>finally</c>, so nothing else can hide inside it.
        /// </para>
        /// </remarks>
        private static InventoryAuthoring EnsureInventory(GameObject root)
        {
            InventoryAuthoring existing = root.GetComponent<InventoryAuthoring>();
            if (existing != null)
            {
                return existing;
            }

            bool logging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            InventoryAuthoring added;
            try
            {
                added = root.AddComponent<InventoryAuthoring>();
            }
            finally
            {
                Debug.unityLogger.logEnabled = logging;
            }

            // Assigned straight away so every LATER OnValidate — on save, on domain reload, on a
            // modder opening the prefab in the inspector — sees real lists and behaves.
            added.slotRequirements = new List<SlotRequirement>();
            added.itemsInInventory = new List<ObjectData>();
            return added;
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
