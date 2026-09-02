using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the container generator: five plain questions turning into a dozen components.
    /// </summary>
    /// <remarks>
    /// The interesting failures are the ones where a component is left behind. A container that stops
    /// being breakable but keeps its destructible component still shows damage and cannot die; one
    /// that stops reacting to its contents keeps reacting. Neither errors — they just behave like the
    /// version the author already changed their mind about.
    /// </remarks>
    public sealed class DimensionContainerTests
    {
        private const string TestRoot = "Assets/NullforgeContainerTests";

        private DimensionContainerAsset container;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeContainerTests");
            }

            container = ScriptableObject.CreateInstance<DimensionContainerAsset>();
            Set("containerId", "testchest");
            Set("displayName", "Test Chest");
        }

        [TearDown]
        public void Cleanup()
        {
            if (container != null)
            {
                Object.DestroyImmediate(container);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void Set(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(container);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetEnum(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(container);
            // intValue, NOT enumValueIndex. enumValueIndex is the ordinal position in the enum's value
            // list, so a member with an explicit value — Custom = 100 — sits at index 3, and writing
            // 100 is out of range and silently leaves the field on its default. intValue writes the
            // underlying value, which is what the member actually means.
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(container);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(container);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private GameObject Run(out DimensionContainerGenerationReport report)
        {
            report = DimensionContainerGenerator.Generate(
                new List<DimensionContainerAsset> { container },
                TestRoot,
                default(DimensionNamingContext));

            string path = report.Created.Count > 0
                ? report.Created[0]
                : (report.Updated.Count > 0 ? report.Updated[0] : null);

            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ---- the sizes, which are the whole point of naming them ----

        [Test]
        public void NamedSizesResolveToTheGridsTheGameActuallyUses()
        {
            // Measured from every chest prefab in Assets/GameObject: all 26 ordinary chests are 6x3
            // and all 42 boss chests are 12x3. The wiki says the same in player words — 18 and 36.
            SetEnum("size", (int)DimensionContainerSize.SingleWide);
            Assert.AreEqual(6, container.SlotsAcross);
            Assert.AreEqual(3, container.SlotsDown);
            Assert.AreEqual(18, container.TotalSlots);

            SetEnum("size", (int)DimensionContainerSize.DoubleWide);
            Assert.AreEqual(12, container.SlotsAcross);
            Assert.AreEqual(3, container.SlotsDown, "Core Keeper widens a chest, it does not deepen it.");
            Assert.AreEqual(36, container.TotalSlots);

            SetEnum("size", (int)DimensionContainerSize.SingleSlot);
            Assert.AreEqual(1, container.TotalSlots);
        }

        [Test]
        public void ACustomSizeUsesTheTypedGrid()
        {
            SetEnum("size", (int)DimensionContainerSize.Custom);
            SetInt("customSlotsAcross", 5);
            SetInt("customSlotsDown", 4);

            Assert.AreEqual(20, container.TotalSlots);
        }

        [Test]
        public void TheSlotGridReachesThePrefab()
        {
            SetEnum("size", (int)DimensionContainerSize.DoubleWide);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(prefab, string.Join("; ", report.Errors));
            InventoryAuthoring inventory = prefab.GetComponent<InventoryAuthoring>();
            Assert.IsNotNull(inventory, "Without an inventory it is not a container at all.");
            Assert.AreEqual(12, inventory.sizeX);
            Assert.AreEqual(3, inventory.sizeY);
        }

        [Test]
        public void AContainerDefinitionHoldsNothingBecauseACraftedChestIsEmpty()
        {
            // 215 of Core Keeper's 216 containers author no contents. The exception is a per-scene
            // variant, which is exactly the point: contents belong to the placement, not the kind.
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            InventoryAuthoring inventory = prefab.GetComponent<InventoryAuthoring>();
            Assert.IsEmpty(inventory.itemsInInventory);
            Assert.AreEqual(LootTableID.Empty, inventory.addLootFromTable);
        }

        // ---- breaking it ----

        [Test]
        public void AnIndestructibleContainerLosesItsDamageComponents()
        {
            // Left behind, they give a container that cannot be destroyed but still flashes when hit,
            // which reads as a bug rather than as a decision.
            SetEnum("origin", (int)DimensionContainerOrigin.PlacedByTheWorld);
            SetBool("indestructible", false);
            DimensionContainerGenerationReport first;
            Run(out first);

            SetBool("indestructible", true);
            DimensionContainerGenerationReport second;
            GameObject prefab = Run(out second);

            Assert.IsNotNull(prefab.GetComponent<IndestructibleAuthoring>());
            Assert.IsNull(prefab.GetComponent<DamageReductionAuthoring>());
            Assert.IsNull(prefab.GetComponent<HealthAuthoring>());
            Assert.IsNull(prefab.GetComponent<MineableAuthoring>());
        }

        [Test]
        public void UnbreakableIsOnlyAllowedOnAContainerTheWorldPlaces()
        {
            // A player who crafts an unbreakable chest and places it can never take it back — it holds
            // that tile in their base forever. A dungeon's chest has no such problem.
            SetEnum("origin", (int)DimensionContainerOrigin.CraftedByPlayers);
            SetBool("indestructible", true);

            Assert.IsTrue(container.IndestructibleWasRefused);
            Assert.IsTrue(container.IsBreakable, "The tick must not be honoured on a craftable chest.");

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNull(prefab.GetComponent<IndestructibleAuthoring>());
            Assert.IsNotEmpty(report.Warnings, "Refusing it silently would be worse than obeying it.");
        }

        [Test]
        public void UnbreakableIsHonouredOnAContainerTheWorldPlaces()
        {
            SetEnum("origin", (int)DimensionContainerOrigin.PlacedByTheWorld);
            SetBool("indestructible", true);

            Assert.IsFalse(container.IndestructibleWasRefused);
            Assert.IsFalse(container.IsBreakable);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<IndestructibleAuthoring>());
        }

        [Test]
        public void HitsToBreakIsHealthWithDamageCappedAtOnePerHit()
        {
            // This is how every breakable object in the game does it, chests included: cap the hit
            // rather than scale the health, so a better pickaxe does not change the count.
            SetInt("hitsToBreak", 5);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.AreEqual(5, prefab.GetComponent<HealthAuthoring>().maxHealth);
            Assert.AreEqual(1, prefab.GetComponent<DamageReductionAuthoring>().maxDamagePerHit);
        }

        [Test]
        public void AContainerDefaultsToTheTwoHitsAVanillaChestTakes()
        {
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.AreEqual(
                DimensionContainerAsset.VanillaChestHitsToBreak,
                prefab.GetComponent<HealthAuthoring>().maxHealth,
                "Every chest prefab in the game authors maxHealth 2.");
        }

        [Test]
        public void AMiningThresholdIsWrittenAsTheGamesOwnReduction()
        {
            SetInt("requiredMiningDamage", 55);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            DamageReductionAuthoring reduction = prefab.GetComponent<DamageReductionAuthoring>();
            Assert.IsNotNull(reduction);
            Assert.AreEqual(55, reduction.reduction, "Stone walls use exactly this number.");
            Assert.IsFalse(
                reduction.calculateReductionFromLevel,
                "The threshold is the author's choice, not something derived from where it sat.");
        }

        [Test]
        public void AContainerNeedsNoParticularToolUnlessAskedTo()
        {
            // Every chest in the game leaves reduction at 0 — any tool opens it. Defaulting otherwise
            // would make custom chests quietly stricter than the ones beside them.
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.AreEqual(0, prefab.GetComponent<DamageReductionAuthoring>().reduction);
        }

        [Test]
        public void TheDestructibleComponentAppearsOnlyWhenADrillIsDemanded()
        {
            // No chest in Core Keeper carries it; the twelve prefabs that set requiresDrill are all
            // ore boulders. Adding it unasked makes a container behave like a smashable prop.
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);
            Assert.IsNull(prefab.GetComponent<DestructibleObjectAuthoring>());

            SetBool("requiresDrill", true);
            prefab = Run(out report);
            Assert.IsTrue(prefab.GetComponent<DestructibleObjectAuthoring>().requiresDrill);

            SetBool("requiresDrill", false);
            prefab = Run(out report);
            Assert.IsNull(
                prefab.GetComponent<DestructibleObjectAuthoring>(),
                "Turning it back off has to remove the component, not just stop setting the flag.");
        }

        [Test]
        public void ABreakableContainerCarriesTheStateMachineThatLetsItDie()
        {
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<StateAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<TookDamageStateAuthoring>());
            Assert.IsNotNull(
                prefab.GetComponent<DeathStateAuthoring>(),
                "With health but no death state it absorbs hits forever.");
        }

        [Test]
        public void KeepingContentsOnBreakIsExpressedByTheMarkerBeingAbsent()
        {
            SetBool("dropsContentsWhenBroken", false);
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);
            Assert.IsNotNull(prefab.GetComponent<DontDropContainedAuthoring>());

            SetBool("dropsContentsWhenBroken", true);
            prefab = Run(out report);
            Assert.IsNull(
                prefab.GetComponent<DontDropContainedAuthoring>(),
                "Turning the option back on has to remove the marker, not just stop adding it.");
        }

        // ---- what fits ----

        [Test]
        public void ASlotRuleThatRestrictsNothingIsNotWritten()
        {
            // An empty rule still costs a check on every item moved, and shows the player a
            // restriction hint on a container that has none.
            SerializedObject serialized = new SerializedObject(container);
            SerializedProperty rules = serialized.FindProperty("slotRules");
            rules.arraySize = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0, container.EffectiveSlotRules.Length);
        }

        [Test]
        public void ARuleThatDeniesLegendaryCountsAsRestricting()
        {
            SerializedObject serialized = new SerializedObject(container);
            SerializedProperty rules = serialized.FindProperty("slotRules");
            rules.arraySize = 1;
            rules.GetArrayElementAtIndex(0).FindPropertyRelative("denyLegendary").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(1, container.EffectiveSlotRules.Length);
        }

        // ---- what happens when an item is put in ----

        [Test]
        public void ClearingTheReactionRemovesTheComponent()
        {
            Set("reactsToItemId", "SomethingUnresolvable");
            DimensionContainerGenerationReport first;
            Run(out first);

            Set("reactsToItemId", string.Empty);
            DimensionContainerGenerationReport second;
            GameObject prefab = Run(out second);

            Assert.IsNull(prefab.GetComponent<ChangeVariationWhenContainingObjectAuthoring>());
        }

        [Test]
        public void ALockedChestIsAKeySlotThatBecomesTheOpenOne()
        {
            // This is Core Keeper's own mechanism, not an invention: LockedCopperChest is a one-slot
            // container accepting CopperKey that re-instantiates as CopperChest.
            SetEnum("size", (int)DimensionContainerSize.SingleSlot);
            Set("reactsToItemId", "CopperKey");
            Set("becomesContainerId", "CopperChest");

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            ChangeVariationWhenContainingObjectAuthoring reaction =
                prefab.GetComponent<ChangeVariationWhenContainingObjectAuthoring>();
            Assert.IsNotNull(reaction, string.Join("; ", report.Warnings));
            Assert.AreEqual(ObjectID.CopperKey, reaction.objectID);
            Assert.AreEqual(
                ObjectID.CopperChest,
                reaction.reinstantiateToNewObjectId,
                "Without this the key is consumed and the chest stays shut.");
        }

        [Test]
        public void WhatItBecomesCanBeFilledFromATableOrFromExactItems()
        {
            // The game offers both on the same component and honours both, so this is not two names
            // for one thing: a table is a random draw, a list is a guarantee.
            SetEnum("size", (int)DimensionContainerSize.SingleSlot);
            Set("reactsToItemId", "CopperKey");
            Set("becomesContainerId", "CopperChest");

            SerializedObject serialized = new SerializedObject(container);
            SerializedProperty contents = serialized.FindProperty("becomesContents");
            contents.arraySize = 1;
            contents.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "Wood";
            contents.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            ChangeVariationWhenContainingObjectAuthoring reaction =
                prefab.GetComponent<ChangeVariationWhenContainingObjectAuthoring>();
            Assert.AreEqual(1, reaction.addItemsToNewObject.Count);
            Assert.AreEqual(ObjectID.Wood, reaction.addItemsToNewObject[0].objectID);
            Assert.AreEqual(4, reaction.addItemsToNewObject[0].amount);
        }

        [Test]
        public void ReactionContentsWithNothingToBecomeAreReported()
        {
            // The game reads them only when it re-instantiates, so as written they are silently lost.
            Set("reactsToItemId", "CopperKey");
            Set("becomesLootTableId", "Chest");

            Assert.IsTrue(container.HasOrphanedReactionContents);

            DimensionContainerGenerationReport report;
            Run(out report);
            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void AReactionThatDoesNothingIsReported()
        {
            Set("reactsToItemId", "CopperKey");

            Assert.IsTrue(container.ReactionDoesNothing);

            DimensionContainerGenerationReport report;
            Run(out report);
            Assert.IsNotEmpty(report.Warnings);
        }

        // ---- the spine every vanilla object of this kind carries ----

        [Test]
        public void AContainerCarriesWhatEveryVanillaChestCarries()
        {
            // Measured from ChestEntity: animation, paint, facing, a description and the automation
            // hook that lets conveyors feed it. A generated chest had none of them, so it sat oddly
            // beside vanilla ones in the same base.
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<AnimationAuthoring>(), "no animation");
            Assert.IsNotNull(prefab.GetComponent<PaintableObjectAuthoring>(), "not paintable");
            Assert.IsNotNull(prefab.GetComponent<DescriptionAuthoring>(), "no description");
            Assert.IsNotNull(
                prefab.GetComponent<Pug.Automation.AutomatedStorageAuthoring>(),
                "conveyors cannot feed it");
        }

        [Test]
        public void FacingIsRemovedWhenTheContainerStopsFacingAnywhere()
        {
            SetBool("facesPlacementDirection", true);
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);
            Assert.IsNotNull(prefab.GetComponent<RotationAuthoring>());

            SetBool("facesPlacementDirection", false);
            prefab = Run(out report);
            Assert.IsNull(prefab.GetComponent<RotationAuthoring>());
        }

        // ---- guardrails worth surfacing before a build ----

        [Test]
        public void MoreSlotRulesThanSlotsIsReported()
        {
            // Core Keeper drops the overflow in OnValidate without telling anybody, so before the
            // build is the only place it can be said.
            SetEnum("size", (int)DimensionContainerSize.SingleSlot);

            SerializedObject serialized = new SerializedObject(container);
            SerializedProperty rules = serialized.FindProperty("slotRules");
            rules.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                rules.GetArrayElementAtIndex(i).FindPropertyRelative("denyLegendary").boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(container.HasMoreSlotRulesThanSlots);

            DimensionContainerGenerationReport report;
            Run(out report);
            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void ARuleNamingBothItemsAndCategoriesIsReported()
        {
            // InventoryAuthoring.OnValidate clears the tags and logs to the console — invisible to a
            // player, and to an author who is not watching Unity's log.
            SerializedObject serialized = new SerializedObject(container);
            SerializedProperty rules = serialized.FindProperty("slotRules");
            rules.arraySize = 1;

            SerializedProperty rule = rules.GetArrayElementAtIndex(0);
            SerializedProperty items = rule.FindPropertyRelative("acceptsItemIds");
            items.arraySize = 1;
            items.GetArrayElementAtIndex(0).stringValue = "Wood";

            SerializedProperty tags = rule.FindPropertyRelative("acceptsCategoryTags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).stringValue = "CanBeSalvaged";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(container.EffectiveSlotRules[0].NamesBothItemsAndTags);

            DimensionContainerGenerationReport report;
            Run(out report);
            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void AnUpgradeableContainerCarriesTheComponentThatDoesTheUpgrading()
        {
            SetInt("upgradeableExtraSlots", 8);
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(
                prefab.GetComponent<ExtraInventorySizeAuthoring>(),
                "The number alone says how far it could go; nothing knows how to take it there.");

            SetInt("upgradeableExtraSlots", 0);
            prefab = Run(out report);
            Assert.IsNull(prefab.GetComponent<ExtraInventorySizeAuthoring>());
        }

        [Test]
        public void AContainerWithNoIdIsSkippedRatherThanGeneratedNameless()
        {
            Set("containerId", string.Empty);

            DimensionContainerGenerationReport report;
            Run(out report);

            Assert.IsEmpty(report.Created);
            Assert.AreEqual(1, report.Skipped.Count);
        }

        [Test]
        public void GeneratingTwiceUpdatesTheSamePrefab()
        {
            DimensionContainerGenerationReport first;
            Run(out first);
            Assert.AreEqual(1, first.Created.Count);

            DimensionContainerGenerationReport second;
            Run(out second);
            Assert.IsEmpty(second.Created);
            Assert.AreEqual(1, second.Updated.Count);
        }

        [Test]
        public void APlacedContainerIsTypedSoTheGameLetsYouPlaceIt()
        {
            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.AreEqual(
                ObjectType.PlaceablePrefab,
                prefab.GetComponent<ObjectAuthoring>().objectType,
                "Core Keeper has no chest type — what makes it a container is the inventory, and " +
                "typing it otherwise keeps it out of the placement UI.");
            Assert.IsNotNull(prefab.GetComponent<PlaceableObjectAuthoring>());
        }

        // ---- a chest is a drop location like any other ----

        [Test]
        public void AChestCarriesWhatItemsSayDropsFromIt()
        {
            // The other half of the inversion. Containers were the one source kind the plan knew
            // about and no generator ever asked for, so an item saying it came out of a chest
            // generated cleanly and dropped nothing.
            Set("containerId", "desertchest");

            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = "Wood";
            SerializedProperty sources = serialized.FindProperty("dropsFrom");
            sources.arraySize = 1;
            SerializedProperty source = sources.GetArrayElementAtIndex(0);
            source.FindPropertyRelative("kind").intValue = (int)DimensionDropSourceKind.Container;
            source.FindPropertyRelative("sourceId").stringValue = "desertchest";
            source.FindPropertyRelative("chance").floatValue = 1f;
            source.FindPropertyRelative("minAmount").intValue = 1;
            source.FindPropertyRelative("maxAmount").intValue = 1;
            source.FindPropertyRelative("enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionDropPlan plan = DimensionDropPlan.Build(
                new List<DimensionItemAsset> { item },
                null);

            DimensionContainerGenerationReport report = DimensionContainerGenerator.Generate(
                new List<DimensionContainerAsset> { container },
                TestRoot,
                default(DimensionNamingContext),
                plan);

            string path = report.Created.Count > 0 ? report.Created[0] : report.Updated[0];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            DropLootAuthoring loot = prefab.GetComponent<DropLootAuthoring>();

            Assert.IsNotNull(loot, "the chest should carry the loot the item named");
            Assert.IsTrue(loot.hasCustomLoot);
            Assert.AreEqual(ObjectID.Wood, loot.customLoot.Values[0].lootDropID);

            Object.DestroyImmediate(item);
        }

        [Test]
        public void AChestNothingNamesGetsNoLootComponent()
        {
            Set("containerId", "plainchest");

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNull(
                prefab.GetComponent<DropLootAuthoring>(),
                "generation is authoritative: an empty plan must not leave loot behind");
        }

        // ---- what it looks like, which is what a player sees first ----

        [Test]
        public void TheIconReachesTheComponentTheGameActuallyReads()
        {
            // ObjectInfo takes its icon from InventoryItemAuthoring and nowhere else
            // (ObjectAuthoring.ObjectAuthoringToObjectInfo). Without the component the chest drew as
            // an empty square in every slot it ever appeared in.
            Sprite art = MakeSprite("world");
            Sprite badge = MakeSprite("badge");
            SetObject("sprite", art);
            SetObject("icon", badge);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);
            InventoryItemAuthoring inventory = prefab.GetComponent<InventoryItemAuthoring>();

            Assert.IsNotNull(inventory, "a chest with no inventory item component has no icon");
            Assert.AreSame(badge, inventory.icon);
            Assert.AreSame(badge, inventory.smallIcon);
            Assert.IsFalse(inventory.isStackable, "a chest is one thing placed, never a stack");
        }

        [Test]
        public void AChestWithOnlyOnePictureUsesItForBoth()
        {
            Sprite art = MakeSprite("only");
            SetObject("sprite", art);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.AreSame(art, prefab.GetComponent<InventoryItemAuthoring>().icon);
        }

        [Test]
        public void TheWorldPictureTravelsOnTheObjectsOwnRecord()
        {
            // additionalSprites is where Core Keeper already keeps an object's own picture for
            // whoever has to draw it outside the world — PlayerController reads [0] for a carried
            // object — and it is what DimensionContainerView reads back per entity.
            Sprite art = MakeSprite("carried");
            SetObject("sprite", art);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);
            System.Collections.Generic.List<Sprite> extra =
                prefab.GetComponent<ObjectAuthoring>().additionalSprites;

            Assert.IsNotNull(extra);
            Assert.AreEqual(1, extra.Count);
            Assert.AreSame(art, extra[0]);
        }

        [Test]
        public void AChestGetsTheFrameworksOwnBodyRatherThanTheGamesChest()
        {
            // Core Keeper pools graphical objects by component TYPE and its own pools are built
            // first, so a mod prefab carrying the vanilla Chest component is never the one drawn.
            // A type of ours wins its own pool; that is the whole reason this class exists.
            SetObject("sprite", MakeSprite("chestbody"));
            SetInteraction(DimensionUseBehaviour.OpensLikeAChest);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);
            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;

            Assert.IsNotNull(visual, "a chest with no graphical prefab is invisible where it stands");
            ExpandNullforge.Containers.DimensionContainerView view =
                visual.GetComponent<ExpandNullforge.Containers.DimensionContainerView>();
            Assert.IsNotNull(view, "the body must be the framework's own type, not the game's Chest");
            Assert.IsNotNull(view.body, "the view needs a renderer to point at the entity's picture");
        }

        [Test]
        public void AChestNobodyOpensIsStillVisible()
        {
            // The other branch of GraphicalObjectConversion: a graphical prefab with no
            // EntityMonoBehaviour is Instantiated per entity instead of pooled, so this one keeps
            // the picture baked into it.
            Sprite art = MakeSprite("silent");
            SetObject("sprite", art);

            DimensionContainerGenerationReport report;
            GameObject prefab = Run(out report);
            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;

            Assert.IsNotNull(visual, "a container nothing opens still has to be seen");
            SpriteRenderer renderer = visual.GetComponentInChildren<SpriteRenderer>(true);
            Assert.IsNotNull(renderer);
            Assert.AreSame(art, renderer.sprite);
            Assert.IsNull(
                visual.GetComponent<EntityMonoBehaviour>(),
                "an unused container must take the un-pooled branch, or it shares one body with " +
                "every other container");
        }

        [Test]
        public void AChestWithNoPictureAtAllSaysSo()
        {
            DimensionContainerGenerationReport report;
            Run(out report);

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("no icon")),
                "a chest that would draw as an empty square must say so before the build");
        }

        private void SetObject(string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(container);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInteraction(DimensionUseBehaviour behaviour)
        {
            SerializedObject serialized = new SerializedObject(container);
            serialized.FindProperty("interaction.whatUsingItDoes").intValue = (int)behaviour;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A real sprite asset on disk, because a prefab can only serialize a reference to one.
        /// </summary>
        private static Sprite MakeSprite(string name)
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.name = name + "Texture";
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, TestRoot + "/" + name + "Texture.asset");

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                16f,
                1u,
                SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, TestRoot + "/" + name + ".asset");
            AssetDatabase.ImportAsset(
                TestRoot + "/" + name + ".asset",
                ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(TestRoot + "/" + name + ".asset");
        }
    }
}
