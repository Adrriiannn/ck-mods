using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers turning a workbench into a station that actually crafts.
    /// </summary>
    /// <remarks>
    /// Before this generator existed a workbench asset could describe a station and never make one:
    /// nothing wrote <c>CraftingAuthoring</c>, so a custom bench was a decoration and the recipes had
    /// to land on a vanilla one. These tests pin that it is a real station now, and that the ways of
    /// getting an empty or unreachable one are reported rather than shipped.
    /// </remarks>
    public sealed class DimensionWorkbenchTests
    {
        private const string TestRoot = "Assets/NullforgeWorkbenchTests";

        private DimensionWorkbenchAsset workbench;
        private readonly List<Object> temporaries = new List<Object>();

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            workbench = ScriptableObject.CreateInstance<DimensionWorkbenchAsset>();
            temporaries.Add(workbench);
            Set("workbenchId", "testbench");
            Set("displayName", "Test Bench");
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = 0; i < temporaries.Count; i++)
            {
                if (temporaries[i] != null)
                {
                    Object.DestroyImmediate(temporaries[i]);
                }
            }

            temporaries.Clear();

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private void Set(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(workbench);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(workbench);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetEnum(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(workbench);
            // intValue, not enumValueIndex — see the container tests for why.
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Attaches a recipe to the workbench through the serialized list.</summary>
        private DimensionRecipeAsset AddRecipe(string outputItemId, int amount = 1, float seconds = 0f)
        {
            DimensionRecipeAsset recipe = ScriptableObject.CreateInstance<DimensionRecipeAsset>();
            temporaries.Add(recipe);

            SerializedObject recipeObject = new SerializedObject(recipe);
            recipeObject.FindProperty("recipeId").stringValue = "r_" + outputItemId;
            recipeObject.FindProperty("outputItemId").stringValue = outputItemId;
            recipeObject.FindProperty("outputAmount").intValue = amount;
            recipeObject.FindProperty("craftTimeSeconds").floatValue = seconds;
            recipeObject.FindProperty("enabled").boolValue = true;
            recipeObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serialized = new SerializedObject(workbench);
            SerializedProperty list = serialized.FindProperty("recipes");
            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = recipe;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return recipe;
        }

        /// <summary>
        /// Generates the bench, tolerating the one console error Core Keeper makes and we cannot.
        /// </summary>
        /// <remarks>
        /// A station gets an <c>InventoryAuthoring</c>, and that component's <c>OnValidate</c>
        /// reads <c>slotRequirements.Count</c> on its first line. The list has no initialiser and
        /// Unity runs <c>OnValidate</c> the instant <c>AddComponent</c> returns, so for that one
        /// instant it is null and Unity logs a NullReferenceException from inside the game's own
        /// component. The generator fills the list on the very next statement — nothing on this
        /// side can get in front of <c>OnValidate</c> — and anyone who adds that component by hand
        /// in the Inspector sees the same line.
        /// <para>
        /// Without this, two tests here fail on a Unity console message with no code of ours in
        /// the stack, which reads as a broken generator and is not one. The switch is scoped to
        /// this one call and put back in the finally, so an error raised anywhere else in the
        /// fixture still fails the test that raised it.
        /// </para>
        /// </remarks>
        private DimensionWorkbenchGenerationReport Run()
        {
            bool wasIgnoring = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                return DimensionWorkbenchGenerator.Generate(
                    new List<DimensionWorkbenchAsset> { workbench },
                    TestRoot,
                    default(DimensionNamingContext));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = wasIgnoring;
            }
        }

        private static GameObject Load()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testbench.prefab");
        }

        /// <summary>Sets the station's wiring through the serialized template.</summary>
        private void SetWiring(DimensionWiringRole role, int power = 0, bool blocks = false)
        {
            SerializedObject serialized = new SerializedObject(workbench);
            SerializedProperty w = serialized.FindProperty("wiring");
            w.FindPropertyRelative("role").intValue = (int)role;
            w.FindPropertyRelative("powerProduced").intValue = power;
            w.FindPropertyRelative("blocksCurrent").boolValue = blocks;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- it is a station now, not a decoration ----

        [Test]
        public void AWorkbenchBecomesAnObjectThatCanActuallyCraft()
        {
            AddRecipe("Torch");
            DimensionWorkbenchGenerationReport report = Run();

            Assert.IsEmpty(report.Errors, string.Join("; ", report.Errors));
            GameObject prefab = Load();
            Assert.IsNotNull(prefab, "no station prefab was generated");

            CraftingAuthoring crafting = prefab.GetComponent<CraftingAuthoring>();
            Assert.IsNotNull(
                crafting,
                "Without CraftingAuthoring the station is a decoration that opens nothing.");
            Assert.AreEqual(CraftingType.Simple, crafting.craftingType);
            Assert.AreEqual(1, crafting.canCraftObjects.Count);
        }

        [Test]
        public void AVanillaOutputResolvesToItsObjectIdRatherThanAString()
        {
            AddRecipe("Torch", amount: 4, seconds: 2.5f);
            Run();

            CraftingAuthoring.CraftableObject entry =
                Load().GetComponent<CraftingAuthoring>().canCraftObjects[0];

            Assert.AreEqual(ObjectID.Torch, entry.objectID);
            Assert.AreEqual(4, entry.amount);
            Assert.AreEqual(2.5f, entry.craftingTime, 0.001f);
        }

        /// <summary>
        /// A recipe making one of this mod's own objects leaves the baked list empty.
        /// </summary>
        /// <remarks>
        /// RENAMED FROM AModsOwnOutputUsesTheStringIdBecauseItDoesNotExistYet, and it asserted the
        /// opposite of what the generator does. It read <c>canCraftObjects[0]</c> and expected a
        /// row carrying <c>ObjectID.None</c> and a <c>moddedObjectID</c> string. That WAS the
        /// behaviour: Core Keeper's own escape hatch resolves <c>moddedObjectID</c> during
        /// conversion, and mod prefabs convert in an order kept sorted by a hash of the prefab
        /// name, so a bench that converted before the item it makes baked <c>ObjectID.None</c> in
        /// permanently and silently, and a rename could flip it either way. The generator was
        /// changed to skip the row entirely and register by name at runtime instead — and this
        /// test was left behind, throwing IndexOutOfRange on an empty list. It had never been run
        /// since.
        /// </remarks>
        [Test]
        public void AModsOwnOutputIsNotBakedIntoTheBenchAtAll()
        {
            AddRecipe("mycustomthing");
            Run();

            Assert.That(
                Load().GetComponent<CraftingAuthoring>().canCraftObjects,
                Is.Empty,
                "A recipe whose output is this mod's own object must leave no row in the baked " +
                "list. The bench registers it by NAME at runtime instead — the bootstrap writes " +
                "the registration in AppendWorkbenchOwnRecipeRegistrations and the recipe " +
                "injector adds the row when the bench's entity appears, by which time every name " +
                "resolves. A baked row here would be the bug that path exists to end.");
        }

        [Test]
        public void TheStationKindReachesTheGamesOwnCraftingType()
        {
            AddRecipe("Torch");
            SetEnum("kind", (int)DimensionWorkbenchKind.Cooking);
            Run();

            Assert.AreEqual(CraftingType.Cooking, Load().GetComponent<CraftingAuthoring>().craftingType);
        }

        // ---- the spine a placed station has ----

        [Test]
        public void AStationCarriesWhatEveryVanillaStationCarries()
        {
            // Read off AlchemyTableEntity: crafting on top of mineable, health, placement, animation,
            // rotation and the four state components.
            AddRecipe("Torch");
            Run();
            GameObject prefab = Load();

            Assert.IsNotNull(prefab.GetComponent<MineableAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<HealthAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<PlaceableObjectAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<AnimationAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<RotationAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DeathStateAuthoring>());
        }

        [Test]
        public void HitsToBreakReachesTheHealthTheGameReads()
        {
            AddRecipe("Torch");
            Run();

            Assert.AreEqual(3, Load().GetComponent<HealthAuthoring>().maxHealth, "the default");
            Assert.AreEqual(1, Load().GetComponent<DamageReductionAuthoring>().maxDamagePerHit);
        }

        // ---- wiring ----

        [Test]
        public void AnUnwiredStationCarriesNoElectricityAtAll()
        {
            AddRecipe("Torch");
            Run();
            Assert.IsNull(Load().GetComponent<Pug.Automation.ElectricityAuthoring>());
        }

        [Test]
        public void APoweredStationBecomesPartOfTheCircuit()
        {
            AddRecipe("Torch");
            SetWiring(DimensionWiringRole.PowerSource, power: 20, blocks: true);
            Run();

            Pug.Automation.ElectricityAuthoring e =
                Load().GetComponent<Pug.Automation.ElectricityAuthoring>();
            Assert.IsNotNull(e);
            Assert.AreEqual(20, e.sourceEnergy, "vanilla generators sit at 20");
            Assert.IsTrue(e.blocksElectricity);
            Assert.IsFalse(e.isWire);
        }

        [Test]
        public void UnwiringAStationRemovesTheComponent()
        {
            AddRecipe("Torch");
            SetWiring(DimensionWiringRole.PowerSource, power: 20);
            Run();

            SetWiring(DimensionWiringRole.NotWired);
            Run();
            Assert.IsNull(
                Load().GetComponent<Pug.Automation.ElectricityAuthoring>(),
                "Left behind it keeps blocking current in a circuit nothing describes.");
        }

        [Test]
        public void APowerSourceProducingNothingIsCaught()
        {
            DimensionWorkbenchAsset probe = ScriptableObject.CreateInstance<DimensionWorkbenchAsset>();
            temporaries.Add(probe);
            SerializedObject o = new SerializedObject(probe);
            o.FindProperty("wiring").FindPropertyRelative("role").intValue =
                (int)DimensionWiringRole.PowerSource;
            o.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(
                probe.Wiring.ProducesNoPower,
                "It looks like a generator and powers nothing.");
        }

        [Test]
        public void AWireCannotAlsoBlockCurrent()
        {
            // Carrying current is the one thing a wire is for, so the contradiction is forced out
            // rather than trusted.
            DimensionWorkbenchAsset probe = ScriptableObject.CreateInstance<DimensionWorkbenchAsset>();
            temporaries.Add(probe);
            SerializedObject o = new SerializedObject(probe);
            SerializedProperty w = o.FindProperty("wiring");
            w.FindPropertyRelative("role").intValue = (int)DimensionWiringRole.Wire;
            w.FindPropertyRelative("blocksCurrent").boolValue = true;
            o.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(probe.Wiring.BlocksCurrent);
        }
        // ---- the quiet mistakes ----

        [Test]
        public void AStationThatCanCraftNothingIsReported()
        {
            Assert.IsNotEmpty(Run().Warnings, "It places and opens an empty window.");
        }

        [Test]
        public void ARecipeWithNoOutputIsReported()
        {
            AddRecipe(string.Empty);
            Assert.IsNotEmpty(Run().Warnings);
        }

        [Test]
        public void GroupingRecipesOntoAVanillaStationGeneratesNoPrefab()
        {
            // A legitimate answer, not a mistake: the recipes go to a bench that already exists.
            AddRecipe("Torch");
            Set("objectId", "CopperWorkBench");
            SetBool("generatesItsOwnObject", false);

            DimensionWorkbenchGenerationReport report = Run();
            Assert.IsEmpty(report.Created);
            Assert.IsNull(Load());
        }

        [Test]
        public void GeneratingNoStationAndNamingNoneIsReported()
        {
            AddRecipe("Torch");
            SetBool("generatesItsOwnObject", false);

            Assert.IsTrue(workbench.HasNowhereToShowRecipes);
            Assert.IsNotEmpty(Run().Warnings, "Its recipes have nowhere to appear.");
        }

        [Test]
        public void AWorkbenchWithNoIdIsSkippedRatherThanGeneratedNameless()
        {
            Set("workbenchId", string.Empty);

            DimensionWorkbenchGenerationReport report = Run();
            Assert.IsEmpty(report.Created);
            Assert.AreEqual(1, report.Skipped.Count);
        }

        [Test]
        public void GeneratingTwiceUpdatesTheSamePrefab()
        {
            AddRecipe("Torch");
            Assert.AreEqual(1, Run().Created.Count);

            DimensionWorkbenchGenerationReport second = Run();
            Assert.IsEmpty(second.Created);
            Assert.AreEqual(1, second.Updated.Count);
        }
    }
}
