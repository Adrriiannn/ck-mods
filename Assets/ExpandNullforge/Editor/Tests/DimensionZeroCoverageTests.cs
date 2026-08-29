using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the rest of the components that were attached with nothing set on them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The durability one is the clearest thing the field audit found. The framework already knew
    /// the game recomputes a written <c>maxDurability</c> — it warned about it — and it had never
    /// exposed <c>durabilityMultiplier</c>, which is the number the recomputation is built from. So
    /// the one dial that works was the one nobody could reach.
    /// </para>
    /// <para>
    /// The boss chest is the same shape of gap with a bigger consequence: <c>BossAuthoring</c> was
    /// on every generated boss and empty, so a custom boss could not leave a treasure chest.
    /// </para>
    /// </remarks>
    public sealed class DimensionZeroCoverageTests
    {
        private const string TestRoot = "Assets/NullforgeZeroTests";

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeZeroTests");
            }
        }

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private static DimensionCreatureStatsTemplate Stats()
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            holder.FindProperty("creatureStats").FindPropertyRelative("maxHealth").intValue = 500;
            holder.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureStatsTemplate stats = host.CreatureStats;
            Object.DestroyImmediate(host);
            return stats;
        }

        private static DimensionBossChestTemplate Chest(System.Action<SerializedProperty> write)
        {
            DimensionBossAsset host = ScriptableObject.CreateInstance<DimensionBossAsset>();
            SerializedObject serialized = new SerializedObject(host);
            write(serialized.FindProperty("bossChest"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            DimensionBossChestTemplate built = host.BossChest;
            Object.DestroyImmediate(host);
            return built;
        }

        // ---- the boss chest ----

        [Test]
        public void ABossLeavesTheChestItNames()
        {
            DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = "testboss",
                        DisplayName = "Test Boss",
                        Stats = Stats(),
                        IsEnemy = true,
                        IsBoss = true,
                        BossChest = Chest(delegate(SerializedProperty chest)
                        {
                            chest.FindPropertyRelative("chestObjectId").stringValue = "Wood";
                            chest.FindPropertyRelative("chestAmount").intValue = 1;
                            chest.FindPropertyRelative("isAMainStoryBoss").boolValue = true;
                        })
                    }
                },
                TestRoot,
                default(DimensionNamingContext));

            BossAuthoring boss = AssetDatabase
                .LoadAssetAtPath<GameObject>(TestRoot + "/testboss.prefab")
                .GetComponent<BossAuthoring>();
            Assert.IsNotNull(boss);
            Assert.AreEqual(ObjectID.Wood, boss.chestToSpawn.objectID);
            Assert.AreEqual(1, boss.chestToSpawn.amount);
            Assert.IsTrue(boss.isMainStoryBoss);
        }

        [Test]
        public void ABossNamingAChestTheGameDoesNotHaveIsReported()
        {
            DimensionCreatureGenerationReport report = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = "testboss2",
                        DisplayName = "Test Boss Two",
                        Stats = Stats(),
                        IsEnemy = true,
                        IsBoss = true,
                        BossChest = Chest(delegate(SerializedProperty chest)
                        {
                            chest.FindPropertyRelative("chestObjectId").stringValue = "NotAChest";
                        })
                    }
                },
                TestRoot,
                default(DimensionNamingContext));

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotAChest")));

            BossAuthoring boss = AssetDatabase
                .LoadAssetAtPath<GameObject>(TestRoot + "/testboss2.prefab")
                .GetComponent<BossAuthoring>();
            Assert.AreEqual(
                0,
                boss.chestToSpawn.amount,
                "a chest of nothing must not be left with an amount saying it exists");
        }

        [Test]
        public void ABossWithNoChestLeavesNone()
        {
            DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = "testboss3",
                        DisplayName = "Test Boss Three",
                        Stats = Stats(),
                        IsEnemy = true,
                        IsBoss = true
                    }
                },
                TestRoot,
                default(DimensionNamingContext));

            BossAuthoring boss = AssetDatabase
                .LoadAssetAtPath<GameObject>(TestRoot + "/testboss3.prefab")
                .GetComponent<BossAuthoring>();
            Assert.AreEqual(ObjectID.None, boss.chestToSpawn.objectID);
        }

        // ---- durability, the dial that actually works ----

        [Test]
        public void TheDurabilityMultipliersSurviveTheImport()
        {
            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(item);
                serialized.FindProperty("itemId").stringValue = "testpick";
                serialized.FindProperty("displayName").stringValue = "Test Pick";
                serialized.FindProperty("iconId").stringValue = "1";
                serialized.FindProperty("archetype").intValue = (int)Api.DimensionItemArchetype.Tool;
                serialized.FindProperty("durabilityMultiplier").floatValue = 1.5f;
                serialized.FindProperty("repairMultiplier").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionItemGenerationReport report = DimensionItemGenerator.Generate(
                    new List<DimensionItemAsset> { item },
                    TestRoot);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testpick.prefab");
                Assert.IsNotNull(
                    prefab,
                    "nothing generated. created=" + string.Join(",", report.Created.ToArray()) +
                    " skipped=" + string.Join(",", report.Skipped.ToArray()) +
                    " errors=" + string.Join(",", report.Errors.ToArray()));

                DurabilityAuthoring durability = prefab.GetComponent<DurabilityAuthoring>();
                Assert.IsNotNull(durability);
                Assert.AreEqual(
                    1.5f,
                    durability.durabilityMultiplier,
                    "the multiplier is the half of durability that is not recomputed away");
                Assert.AreEqual(0.5f, durability.repairMultiplier);
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        // ---- pouches and growing inventories ----

        [Test]
        public void APouchIsAContainerThatGrowsAndIsCarried()
        {
            DimensionContainerAsset container =
                ScriptableObject.CreateInstance<DimensionContainerAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(container);
                serialized.FindProperty("containerId").stringValue = "testpouch";
                serialized.FindProperty("displayName").stringValue = "Test Pouch";
                serialized.FindProperty("isAPouch").boolValue = true;
                SerializedProperty tags = serialized.FindProperty("onlyAcceptsCategoryTags");
                tags.arraySize = 1;
                tags.GetArrayElementAtIndex(0).stringValue = "Wood";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionContainerGenerationReport report = DimensionContainerGenerator.Generate(
                    new List<DimensionContainerAsset> { container },
                    TestRoot,
                    default(DimensionNamingContext));

                string path = report.Created.Count > 0 ? report.Created[0] : report.Updated[0];
                ExtraInventorySizeAuthoring extra = AssetDatabase
                    .LoadAssetAtPath<GameObject>(path)
                    .GetComponent<ExtraInventorySizeAuthoring>();

                Assert.IsNotNull(
                    extra,
                    "a pouch needs the component even when it has no upgradeable slots");
                Assert.IsTrue(extra.isPouch);
                Assert.AreEqual(1, extra.canOnlyContainObjectsWithCategoryTags.Count);
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void AnOrdinaryChestCarriesNoGrowingInventoryComponent()
        {
            DimensionContainerAsset container =
                ScriptableObject.CreateInstance<DimensionContainerAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(container);
                serialized.FindProperty("containerId").stringValue = "testplain";
                serialized.FindProperty("displayName").stringValue = "Test Plain";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionContainerGenerationReport report = DimensionContainerGenerator.Generate(
                    new List<DimensionContainerAsset> { container },
                    TestRoot,
                    default(DimensionNamingContext));

                string path = report.Created.Count > 0 ? report.Created[0] : report.Updated[0];
                Assert.IsNull(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path)
                        .GetComponent<ExtraInventorySizeAuthoring>());
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        // ---- facing, sign text, one-frame invulnerability ----

        [Test]
        public void ASignComesWithItsText()
        {
            DimensionWorldObjectAsset worldObject =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(worldObject);
                serialized.FindProperty("objectIdentifier").stringValue = "testsign";
                serialized.FindProperty("displayName").stringValue = "Test Sign";
                serialized.FindProperty("textItComesWith").stringValue = "Beware";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionWorldObjectGenerator.Generate(
                    new List<DimensionWorldObjectAsset> { worldObject },
                    TestRoot,
                    default(DimensionNamingContext));

                DescriptionAuthoring description = AssetDatabase
                    .LoadAssetAtPath<GameObject>(TestRoot + "/testsign.prefab")
                    .GetComponent<DescriptionAuthoring>();
                Assert.AreEqual("Beware", description.initialText);
            }
            finally
            {
                Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void OneFrameInvulnerabilityOnTopOfPermanentIsReported()
        {
            DimensionWorldObjectAsset worldObject =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(worldObject);
                serialized.FindProperty("objectIdentifier").stringValue = "testrock2";
                serialized.FindProperty("displayName").stringValue = "Test Rock Two";
                serialized.FindProperty("cannotBeAttacked").boolValue = true;
                serialized.FindProperty("untouchableForOneFrameOnly").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionWorldObjectGenerationReport report =
                    DimensionWorldObjectGenerator.Generate(
                        new List<DimensionWorldObjectAsset> { worldObject },
                        TestRoot,
                        default(DimensionNamingContext));

                Assert.IsTrue(report.Warnings.Exists(w => w.Contains("permanent one wins")));
                Assert.IsTrue(worldObject.OneFrameRuleIsPointless);
            }
            finally
            {
                Object.DestroyImmediate(worldObject);
            }
        }
        // ---- placement rules, the widest-reaching component in the game ----

        [Test]
        public void AWallTorchGetsTheWholeWallSideSet()
        {
            DimensionWorldObjectAsset worldObject =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(worldObject);
                serialized.FindProperty("objectIdentifier").stringValue = "testtorch";
                serialized.FindProperty("displayName").stringValue = "Test Torch";
                SerializedProperty rules = serialized.FindProperty("placementRules");
                rules.FindPropertyRelative("canGoOnTheSideOfAWall").boolValue = true;
                rules.FindPropertyRelative("hasAWallFacingLook").boolValue = true;
                rules.FindPropertyRelative("wallLookStartsAtVariationOne").boolValue = true;
                rules.FindPropertyRelative("claimsTheWallItIsOn").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionWorldObjectGenerator.Generate(
                    new List<DimensionWorldObjectAsset> { worldObject },
                    TestRoot,
                    default(DimensionNamingContext));

                PlaceableObjectAuthoring placeable = AssetDatabase
                    .LoadAssetAtPath<GameObject>(TestRoot + "/testtorch.prefab")
                    .GetComponent<PlaceableObjectAuthoring>();
                Assert.IsTrue(placeable.canPlaceOnSideOfWall);
                Assert.IsTrue(placeable.hasVariationsThatCanBePlacedOnWalls);
                Assert.IsTrue(placeable.wallSideVariationStartsOnIndex1);
                Assert.IsTrue(placeable.blocksHangingWallObjects);
            }
            finally
            {
                Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void AWallObjectWithNoWallLookIsReported()
        {
            DimensionWorldObjectAsset worldObject =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(worldObject);
                serialized.FindProperty("objectIdentifier").stringValue = "testbadwall";
                serialized.FindProperty("displayName").stringValue = "Test Bad Wall";
                serialized.FindProperty("placementRules")
                    .FindPropertyRelative("canGoOnTheSideOfAWall").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionWorldObjectGenerationReport report =
                    DimensionWorldObjectGenerator.Generate(
                        new List<DimensionWorldObjectAsset> { worldObject },
                        TestRoot,
                        default(DimensionNamingContext));

                Assert.IsTrue(
                    report.Warnings.Exists(w => w.Contains("draw its floor sprite")),
                    "the wall-decoration bug has to be caught at generation, not in-game");
            }
            finally
            {
                Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void ARugCanBePlacedOverThingsThatWouldNormallyBlockIt()
        {
            DimensionWorldObjectAsset worldObject =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(worldObject);
                serialized.FindProperty("objectIdentifier").stringValue = "testrug";
                serialized.FindProperty("displayName").stringValue = "Test Rug";
                SerializedProperty rules = serialized.FindProperty("placementRules");
                rules.FindPropertyRelative("canGoOverBlockingObjects").boolValue = true;
                rules.FindPropertyRelative("rootsStillGrowThrough").boolValue = true;
                rules.FindPropertyRelative("aBadPlacementDoesNotDestroyIt").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionWorldObjectGenerator.Generate(
                    new List<DimensionWorldObjectAsset> { worldObject },
                    TestRoot,
                    default(DimensionNamingContext));

                PlaceableObjectAuthoring placeable = AssetDatabase
                    .LoadAssetAtPath<GameObject>(TestRoot + "/testrug.prefab")
                    .GetComponent<PlaceableObjectAuthoring>();
                Assert.IsTrue(placeable.canBePlacedOnBlockingObjects);
                Assert.IsTrue(placeable.dontBlockRoots);
                Assert.IsTrue(placeable.dontDestroyObjectIfInvalidPlacement);
            }
            finally
            {
                Object.DestroyImmediate(worldObject);
            }
        }
    }
}
