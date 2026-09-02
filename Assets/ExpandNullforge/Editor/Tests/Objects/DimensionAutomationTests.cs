using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers what Core Keeper's automation can do with a thing.
    /// </summary>
    /// <remarks>
    /// Six components, five of them bare markers, and they were being asked about in two different
    /// places — one tick lived with the world's general rules and the rest did not exist. They are
    /// one question a builder asks, so they are one template now, and these pin that the mover's
    /// settings are written only on the thing that actually does the moving.
    /// </remarks>
    public sealed class DimensionAutomationTests
    {
        private const string TestRoot = "Assets/NullforgeAutomationTests";

        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("objectIdentifier").stringValue = "testbelt";
            serialized.FindProperty("displayName").stringValue = "Test Belt";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private void SetAutomation(System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized.FindProperty("automation"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionWorldObjectGenerationReport Run()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject Load()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testbelt.prefab");
        }

        [Test]
        public void TheFiveMarkersAreAddedAndRemovedWithTheirTicks()
        {
            SetAutomation(delegate(SerializedProperty automation)
            {
                automation.FindPropertyRelative("automationMayActOnIt").boolValue = true;
                automation.FindPropertyRelative("aDrillCanMineIt").boolValue = true;
                automation.FindPropertyRelative("aSeederCanPlantIt").boolValue = true;
                automation.FindPropertyRelative("automationCanCraftAtIt").boolValue = true;
                automation.FindPropertyRelative("itMovesThings").boolValue = true;
            });
            Run();

            GameObject prefab = Load();
            Assert.IsNotNull(prefab.GetComponent<Pug.Automation.AffectedByAutomationAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<Pug.Automation.AutomatedMineableAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<Pug.Automation.AutomatedPlantableSeedAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<Pug.Automation.AutomatedCrafterAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<Pug.Automation.AutomatedMoverAuthoring>());

            SetAutomation(delegate(SerializedProperty automation)
            {
                automation.FindPropertyRelative("automationMayActOnIt").boolValue = false;
                automation.FindPropertyRelative("aDrillCanMineIt").boolValue = false;
                automation.FindPropertyRelative("aSeederCanPlantIt").boolValue = false;
                automation.FindPropertyRelative("automationCanCraftAtIt").boolValue = false;
                automation.FindPropertyRelative("itMovesThings").boolValue = false;
            });
            Run();

            prefab = Load();
            Assert.IsNull(prefab.GetComponent<Pug.Automation.AffectedByAutomationAuthoring>());
            Assert.IsNull(prefab.GetComponent<Pug.Automation.AutomatedMineableAuthoring>());
            Assert.IsNull(prefab.GetComponent<Pug.Automation.AutomatedPlantableSeedAuthoring>());
            Assert.IsNull(prefab.GetComponent<Pug.Automation.AutomatedCrafterAuthoring>());
            Assert.IsNull(prefab.GetComponent<Pug.Automation.AutomatedMoverAuthoring>());
        }

        [Test]
        public void MoverSettingsAreOnlyWrittenOnTheThingThatMoves()
        {
            // A chest carrying conveyor timings would be a component the game reads and nothing
            // acts on.
            SetAutomation(delegate(SerializedProperty automation)
            {
                automation.FindPropertyRelative("automationMayActOnIt").boolValue = true;
                automation.FindPropertyRelative("itMovesThings").boolValue = false;
            });
            Run();
            Assert.IsNull(Load().GetComponent<AutomatedMoverSharedAuthoring>());

            SetAutomation(delegate(SerializedProperty automation)
            {
                automation.FindPropertyRelative("itMovesThings").boolValue = true;
                automation.FindPropertyRelative("moveSeconds").floatValue = 0.25f;
                automation.FindPropertyRelative("restSeconds").floatValue = 0.75f;
                automation.FindPropertyRelative("splitsStacks").boolValue = true;
            });
            Run();

            AutomatedMoverSharedAuthoring shared = Load().GetComponent<AutomatedMoverSharedAuthoring>();
            Assert.IsNotNull(shared);
            Assert.AreEqual(0.25f, shared.moveTime);
            Assert.AreEqual(0.75f, shared.cooldownTime);
            Assert.IsTrue(shared.splitOnMove);
        }

        [Test]
        public void AConveyorThatCanNeverFinishAMoveIsReported()
        {
            // Zero is not "instant" — there is no interval to run over, so nothing completes.
            SetAutomation(delegate(SerializedProperty automation)
            {
                automation.FindPropertyRelative("itMovesThings").boolValue = true;
                automation.FindPropertyRelative("moveSeconds").floatValue = 0f;
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("no move ever completes")));
            Assert.IsTrue(worldObject.Automation.MovesNothingBecauseAMoveTakesNoTime);
        }
    }
}
