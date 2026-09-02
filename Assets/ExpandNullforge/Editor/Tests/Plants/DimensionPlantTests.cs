using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.Plants;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers turning one plant into the family Core Keeper expects.
    /// </summary>
    /// <remarks>
    /// The failures worth guarding are the ones that still generate: a seed that grows nothing, a
    /// plant that yields nothing, a ripe prefab that is not actually ripe, a better version with no
    /// prefab behind it. None of them error, and all of them only show up when somebody plants the
    /// thing in a real world.
    /// </remarks>
    public sealed class DimensionPlantTests
    {
        private const string TestRoot = "Assets/NullforgePlantTests";

        private DimensionPlantAsset plant;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgePlantTests");
            }

            plant = ScriptableObject.CreateInstance<DimensionPlantAsset>();
            Set("plantId", "testcrop");
            Set("displayName", "Test Crop");
            Set("produceItemId", "Carrock");
        }

        [TearDown]
        public void Cleanup()
        {
            if (plant != null)
            {
                Object.DestroyImmediate(plant);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void Set(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetFloat(string field, float value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetEnum(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            // intValue, not enumValueIndex — see the container tests for why.
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds one better version, filled in through the serialized list the Studio writes.
        /// </summary>
        private void AddVersion(
            string versionName,
            float chancePercent,
            bool usesTheGamesGoldenChance,
            string produceItemId,
            string extraItemId)
        {
            SerializedObject serialized = new SerializedObject(plant);
            SerializedProperty list = serialized.FindProperty("versions");
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty row = list.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("versionName").stringValue = versionName;
            row.FindPropertyRelative("chancePercent").floatValue = chancePercent;
            row.FindPropertyRelative("usesTheGamesGoldenChance").boolValue = usesTheGamesGoldenChance;
            row.FindPropertyRelative("produceItemId").stringValue = produceItemId ?? string.Empty;
            row.FindPropertyRelative("harvestAmount").intValue = 0;
            row.FindPropertyRelative("chanceToGetThingsBackPercent").floatValue = 0f;
            row.FindPropertyRelative("enabled").boolValue = true;

            SerializedProperty extras = row.FindPropertyRelative("extraDrops");
            extras.arraySize = 0;
            if (!string.IsNullOrEmpty(extraItemId))
            {
                extras.InsertArrayElementAtIndex(0);
                SerializedProperty extra = extras.GetArrayElementAtIndex(0);
                extra.FindPropertyRelative("itemId").stringValue = extraItemId;
                extra.FindPropertyRelative("amount").intValue = 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionPlantGenerationReport Run()
        {
            return DimensionPlantGenerator.Generate(
                new List<DimensionPlantAsset> { plant },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject Load(string suffix)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                TestRoot + "/testcrop" + suffix + ".prefab");
        }

        // ---- one asset, the whole family ----

        [Test]
        public void OnePlantMakesTheSeedTheGrowingPlantAndTheRipeOne()
        {
            DimensionPlantGenerationReport report = Run();

            Assert.IsEmpty(report.Errors, string.Join("; ", report.Errors));
            Assert.IsNotNull(Load(DimensionPlantGenerator.PlantSuffix), "no growing plant");
            Assert.IsNotNull(Load(DimensionPlantGenerator.RipeSuffix), "no ripe plant");
            Assert.IsNotNull(Load(DimensionPlantGenerator.SeedSuffix), "no seed");
        }

        [Test]
        public void TheRipePrefabIsTheSameObjectJustFinishedGrowing()
        {
            // Vanilla's Complete…PlantEntity shares its ObjectID with the growing one and differs in
            // its stage and its variation. Minting a second object here would break the seed's own
            // reference; leaving it at variation 0 would give one object two prefabs at variation 0.
            Run();

            GameObject growing = Load(DimensionPlantGenerator.PlantSuffix);
            GameObject ripe = Load(DimensionPlantGenerator.RipeSuffix);

            Assert.AreEqual(
                growing.GetComponent<ObjectAuthoring>().objectName,
                ripe.GetComponent<ObjectAuthoring>().objectName,
                "The ripe plant is the same object, not a second one.");
            Assert.AreEqual(0, growing.GetComponent<ObjectAuthoring>().variation);
            Assert.AreEqual(
                DimensionPlantAsset.CompletePlantVariation,
                ripe.GetComponent<ObjectAuthoring>().variation,
                "Two prefabs of one object cannot both sit at variation 0.");

            Assert.AreEqual(
                0,
                growing.GetComponent<DimensionPlantProduceAuthoring>().growingSettings.currentStage);
            Assert.AreEqual(
                plant.GrowthStages,
                ripe.GetComponent<DimensionPlantProduceAuthoring>().growingSettings.currentStage,
                "Ripe means currentStage has reached highestStage.");
        }

        [Test]
        public void TheSeedNamesThePlantItGrowsIntoRatherThanAnIdItCannotKnow()
        {
            // The bug this whole component exists for: an ObjectID resolved in the editor is None
            // for anything the mod itself makes, and a seed with None grows nothing, silently.
            Run();

            GameObject seedPrefab = Load(DimensionPlantGenerator.SeedSuffix);
            GameObject plantPrefab = Load(DimensionPlantGenerator.PlantSuffix);

            Assert.IsNull(
                seedPrefab.GetComponent<SeedAuthoring>(),
                "Vanilla's seed component bakes an id that cannot resolve here.");
            Assert.AreEqual(
                plantPrefab.GetComponent<ObjectAuthoring>().objectName,
                seedPrefab.GetComponent<DimensionSeedAuthoring>().turnsIntoPlantName);
        }

        [Test]
        public void ThePlantNamesItsProduceRatherThanBakingAnId()
        {
            Run();

            GameObject plantPrefab = Load(DimensionPlantGenerator.PlantSuffix);
            Assert.IsNull(plantPrefab.GetComponent<PlantAuthoring>());
            Assert.AreEqual(
                "Carrock",
                plantPrefab.GetComponent<DimensionPlantProduceAuthoring>().produceName);
        }

        [Test]
        public void HowManyYouGetPerHarvestReachesThePlant()
        {
            // Core Keeper's own PlantConverter writes 1 here and offers no way to say otherwise.
            SetInt("harvestAmount", 3);
            Run();

            Assert.AreEqual(
                3,
                Load(DimensionPlantGenerator.PlantSuffix)
                    .GetComponent<DimensionPlantProduceAuthoring>().numberOfPlantsToDrop);
        }

        [Test]
        public void PickingAPlantGivesItsSeedBackAsOftenAsAVanillaCropDoes()
        {
            Run();

            DimensionPlantDropsAuthoring drops =
                Load(DimensionPlantGenerator.PlantSuffix).GetComponent<DimensionPlantDropsAuthoring>();
            Assert.AreEqual(1, drops.itemNames.Length);
            Assert.AreEqual(
                Load(DimensionPlantGenerator.SeedSuffix).GetComponent<ObjectAuthoring>().objectName,
                drops.itemNames[0]);
            Assert.AreEqual(0.75f, drops.chance, 0.0001f, "Every vanilla plant returns its seed at 0.75.");
        }

        [Test]
        public void TheSeedCarriesTheIconAPlayerSeesInTheirBag()
        {
            Run();

            InventoryItemAuthoring inventory =
                Load(DimensionPlantGenerator.SeedSuffix).GetComponent<InventoryItemAuthoring>();
            Assert.IsNotNull(inventory, "the seed is the half of a crop a player carries");
            Assert.IsTrue(inventory.isStackable);
        }

        // ---- growing ----

        [Test]
        public void TheTotalGrowingTimeIsDividedIntoStagesTheWayTheGameStoresIt()
        {
            // A designer balances the total; the game reads per-stage. Asking for both would let them
            // disagree.
            SetInt("growthStages", 4);
            SetFloat("minutesToGrow", 8f);

            Assert.AreEqual(120f, plant.SecondsBetweenStages, 0.001f);

            Run();
            GrowingSettings settings = Load(DimensionPlantGenerator.PlantSuffix)
                .GetComponent<DimensionPlantProduceAuthoring>().growingSettings;
            Assert.AreEqual(4, settings.highestStage);
            Assert.AreEqual(120f, settings.timeBetweenStages, 0.001f);
        }

        [Test]
        public void APlantDefaultsToTheEightMinutesAVanillaCropTakes()
        {
            Assert.AreEqual(DimensionPlantAsset.VanillaGrowthStages, plant.GrowthStages);
            Assert.AreEqual(240f, plant.SecondsBetweenStages, 0.001f, "Carrock's own number.");
        }

        // ---- better versions, which are variations and not objects ----

        [Test]
        public void AGoldenVersionSitsWhereTheGamesOwnGoldenCropsSit()
        {
            AddVersion("Golden", 3f, true, "CarrockRare", null);
            Run();

            DimensionSeedAuthoring seed =
                Load(DimensionPlantGenerator.SeedSuffix).GetComponent<DimensionSeedAuthoring>();
            Assert.AreEqual(DimensionPlantAsset.RareSeedVariation, seed.rareSeedVariation);
            Assert.AreEqual(DimensionPlantAsset.RarePlantVariation, seed.rarePlantVariation);
        }

        [Test]
        public void EveryVersionGetsItsOwnSeedAndPlantPrefab()
        {
            // Without them the roll lands on a variation nothing is registered for, the game logs an
            // error and spawns nothing, and roughly that version's share of plantings vanishes.
            AddVersion("Golden", 3f, true, "CarrockRare", null);
            AddVersion("Platinum", 1f, false, "GoldOre", "GoldBar");
            Run();

            Assert.IsNotNull(Load("SeedGolden"), "no golden seed");
            Assert.IsNotNull(Load("PlantGolden"), "no golden plant");
            Assert.IsNotNull(Load("SeedPlatinum"), "no platinum seed");
            Assert.IsNotNull(Load("PlantPlatinum"), "no platinum plant");

            Assert.AreEqual(1, Load("SeedGolden").GetComponent<ObjectAuthoring>().variation);
            Assert.AreEqual(2, Load("PlantGolden").GetComponent<ObjectAuthoring>().variation);
            Assert.AreEqual(2, Load("SeedPlatinum").GetComponent<ObjectAuthoring>().variation);
            Assert.AreEqual(3, Load("PlantPlatinum").GetComponent<ObjectAuthoring>().variation);
        }

        [Test]
        public void AnOrdinaryPlantingGetsAVariationOfItsOwnSoItIsNeverRolledTwice()
        {
            // Variation 0 is the only mark the roll has for "not decided yet", so a planting that
            // came up ordinary has to be moved off it — otherwise reloading the world rolls it again
            // and a player can reload until a rare version comes up.
            AddVersion("Golden", 3f, true, "CarrockRare", null);
            Run();

            GameObject plain = Load(DimensionPlantGenerator.PlainSeedSuffix);
            Assert.IsNotNull(plain, "no prefab for a planting that came up ordinary");
            Assert.AreEqual(
                DimensionPlantGenerator.PlainSeedVariationFor(1),
                plain.GetComponent<ObjectAuthoring>().variation);
            Assert.AreEqual(
                DimensionPlantGenerator.PlainSeedVariationFor(1),
                Load(DimensionPlantGenerator.SeedSuffix)
                    .GetComponent<DimensionCropTierSeedAuthoring>().plainSeedVariation);
        }

        [Test]
        public void TheVersionTableRidesOnEverySeedVariation()
        {
            AddVersion("Golden", 3f, true, "CarrockRare", null);
            AddVersion("Platinum", 1f, false, "GoldOre", null);
            Run();

            DimensionCropTierSeedAuthoring tiers =
                Load("SeedPlatinum").GetComponent<DimensionCropTierSeedAuthoring>();
            Assert.AreEqual(new[] { 1, 2 }, tiers.seedVariations);
            Assert.AreEqual(new[] { 2, 3 }, tiers.plantVariations);
            Assert.AreEqual(new[] { 3f, 1f }, tiers.chancePercents);
            Assert.AreEqual(new[] { true, false }, tiers.usesTheGamesGoldenRoll);
        }

        [Test]
        public void AVersionCarriesItsOwnProduceAndItsOwnExtras()
        {
            AddVersion("Platinum", 1f, false, "GoldOre", "GoldBar");
            Run();

            GameObject versionPlant = Load("PlantPlatinum");
            Assert.AreEqual(
                "GoldOre", versionPlant.GetComponent<DimensionPlantProduceAuthoring>().produceName);

            DimensionPlantDropsAuthoring drops =
                versionPlant.GetComponent<DimensionPlantDropsAuthoring>();
            Assert.AreEqual(2, drops.itemNames.Length, "its seed and the bonus");
            Assert.AreEqual("GoldBar", drops.itemNames[1]);
        }

        [Test]
        public void AVersionWithoutItsOwnProduceGivesTheOrdinaryHarvest()
        {
            AddVersion("Golden", 3f, true, string.Empty, null);
            Run();

            Assert.AreEqual(
                "Carrock", Load("PlantGolden").GetComponent<DimensionPlantProduceAuthoring>().produceName);
        }

        [Test]
        public void OnlyTheFirstVersionMayHandItsRollBackToTheGame()
        {
            // The game has one golden roll and places one variation, so a second version claiming it
            // would simply never come up.
            AddVersion("Golden", 3f, true, string.Empty, null);
            AddVersion("Platinum", 1f, true, string.Empty, null);

            DimensionPlantGenerationReport report = Run();

            Assert.IsNotEmpty(report.Warnings);
            Assert.IsFalse(
                Load(DimensionPlantGenerator.SeedSuffix)
                    .GetComponent<DimensionCropTierSeedAuthoring>().usesTheGamesGoldenRoll[1],
                "The framework has to roll it, or nobody does.");
        }

        [Test]
        public void RemovingAVersionTakesItsPrefabsAwayAgain()
        {
            // Left behind, the old prefab keeps claiming a variation of the same seed, and a version
            // added later would land on the same number.
            AddVersion("Golden", 3f, true, string.Empty, null);
            AddVersion("Platinum", 1f, false, string.Empty, null);
            Run();
            Assert.IsNotNull(Load("SeedPlatinum"));

            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty("versions").arraySize = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Run();

            Assert.IsNull(Load("SeedPlatinum"), "the removed version still has a prefab");
            Assert.IsNull(Load("PlantPlatinum"));
            Assert.IsNotNull(Load("SeedGolden"), "the version that stayed lost its prefab");
        }

        [Test]
        public void ACropWithNoVersionsCarriesNoneOfTheVersionMachinery()
        {
            Run();

            Assert.IsNull(
                Load(DimensionPlantGenerator.SeedSuffix).GetComponent<DimensionCropTierSeedAuthoring>());
            Assert.IsNull(
                Load(DimensionPlantGenerator.PlantSuffix).GetComponent<DimensionCropTierPlantAuthoring>());
            Assert.IsNull(Load(DimensionPlantGenerator.PlainSeedSuffix));
            Assert.AreEqual(
                0,
                Load(DimensionPlantGenerator.SeedSuffix)
                    .GetComponent<DimensionSeedAuthoring>().rareSeedVariation,
                "A seed with nothing to roll for must not keep rolling.");
        }

        // ---- where it grows ----

        [Test]
        public void APlantThatSpreadsOnItsOwnHasNoSeedToPlant()
        {
            // Nobody plants a root plant, so a seed for one is an item that does nothing.
            SetEnum("ground", (int)DimensionPlantGround.SpreadsOnItsOwn);
            Run();

            Assert.IsNull(Load(DimensionPlantGenerator.SeedSuffix));
            Assert.IsNotNull(Load(DimensionPlantGenerator.PlantSuffix));
        }

        [Test]
        public void SwitchingBackToBeingPlantedRemovesTheSpreading()
        {
            SetEnum("ground", (int)DimensionPlantGround.SpreadsOnItsOwn);
            Run();

            SetEnum("ground", (int)DimensionPlantGround.PlantedInFarmedSoil);
            Run();

            Assert.IsNull(
                Load(DimensionPlantGenerator.PlantSuffix).GetComponent<RootPlantAuthoring>(),
                "Left behind, it keeps spreading with nothing in the asset asking it to.");
        }

        [Test]
        public void BeingWashedAwayByWaterIsAddedAndRemoved()
        {
            SetBool("washedAwayByWater", true);
            Run();
            Assert.IsNotNull(
                Load(DimensionPlantGenerator.PlantSuffix).GetComponent<CanBeRemovedByWaterAuthoring>());

            SetBool("washedAwayByWater", false);
            Run();
            Assert.IsNull(
                Load(DimensionPlantGenerator.PlantSuffix).GetComponent<CanBeRemovedByWaterAuthoring>());
        }

        // ---- the quiet mistakes ----

        [Test]
        public void APlantWithNothingToHarvestIsReported()
        {
            Set("produceItemId", string.Empty);
            Assert.IsTrue(plant.HarvestGivesNothing);

            Assert.IsNotEmpty(Run().Warnings);
        }

        [Test]
        public void SpreadingWithNowhereToSpreadIsReported()
        {
            SetEnum("ground", (int)DimensionPlantGround.SpreadsOnItsOwn);
            Assert.IsTrue(plant.SpreadsNowhere);

            Assert.IsNotEmpty(Run().Warnings);
        }

        [Test]
        public void ACropThatIsRipeInstantlyIsReported()
        {
            SetFloat("minutesToGrow", 0f);
            Assert.IsTrue(plant.IsReadyImmediately);

            Assert.IsNotEmpty(Run().Warnings);
        }

        [Test]
        public void AProduceNameThatCanNeverResolveIsReportedRatherThanSilentlyDropped()
        {
            // A bare word that is not one of the game's own items is a mod item missing its mod
            // name, and nothing downstream can tell the difference between that and a typo.
            Set("produceItemId", "NotARealItem");

            DimensionPlantGenerationReport report = Run();
            Assert.IsNotEmpty(report.Warnings);
            Assert.IsNotNull(
                Load(DimensionPlantGenerator.PlantSuffix),
                "It still generates — the plant just yields nothing.");
        }

        [Test]
        public void AFullyQualifiedModItemIsNotReportedAsMissing()
        {
            Set("produceItemId", "MyMod:Amber");

            DimensionPlantGenerationReport report = Run();
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Assert.IsFalse(
                    report.Warnings[i].Contains("MyMod:Amber"),
                    "A mod's own item only exists once the mod loads: " + report.Warnings[i]);
            }
        }

        [Test]
        public void APlantWithNoIdIsSkippedRatherThanGeneratedNameless()
        {
            Set("plantId", string.Empty);

            DimensionPlantGenerationReport report = Run();
            Assert.IsEmpty(report.Created);
            Assert.AreEqual(1, report.Skipped.Count);
        }

        [Test]
        public void GeneratingTwiceUpdatesTheSamePrefabs()
        {
            DimensionPlantGenerationReport first = Run();
            Assert.AreEqual(3, first.Created.Count, "seed, plant, ripe plant");

            DimensionPlantGenerationReport second = Run();
            Assert.IsEmpty(second.Created);
            Assert.AreEqual(3, second.Updated.Count);
        }

        [Test]
        public void APlantCarriesTheStateMachineThatLetsItBeRemoved()
        {
            Run();
            GameObject growing = Load(DimensionPlantGenerator.PlantSuffix);

            Assert.IsNotNull(growing.GetComponent<StateAuthoring>());
            Assert.IsNotNull(growing.GetComponent<DeathStateAuthoring>());
            Assert.IsNotNull(growing.GetComponent<MineableAuthoring>());
        }
    }
}
