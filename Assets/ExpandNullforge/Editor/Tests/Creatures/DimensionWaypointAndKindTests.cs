using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers waypoints, conveyor push, potions and scanners.
    /// </summary>
    /// <remarks>
    /// Four small ones with nothing in common except size. The conveyor push is the interesting
    /// one: it is a separate component from the mover, so a belt that carries items and a belt that
    /// shoves the player standing on it are two different things a modder has to ask for, and one
    /// direction per variation is how a four-rotation belt is one object rather than four.
    /// </remarks>
    public sealed class DimensionWaypointAndKindTests
    {
        private const string TestRoot = "Assets/NullforgeWaypointTests";

        private DimensionWorldObjectAsset worldObject;
        private DimensionItemAsset item;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeWaypointTests");
            }

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("objectIdentifier").stringValue = "teststatue";
            serialized.FindProperty("displayName").stringValue = "Test Statue";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            SerializedObject other = new SerializedObject(item);
            other.FindProperty("itemId").stringValue = "testtonic";
            other.FindProperty("displayName").stringValue = "Test Tonic";
            other.FindProperty("iconId").stringValue = "1";
            other.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void SetObject(System.Action<SerializedObject> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetItem(System.Action<SerializedObject> write)
        {
            SerializedObject serialized = new SerializedObject(item);
            write(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionWorldObjectGenerationReport RunObject()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        private DimensionItemGenerationReport RunItem()
        {
            return DimensionItemGenerator.Generate(
                new List<DimensionItemAsset> { item },
                TestRoot);
        }

        private static GameObject LoadObject()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/teststatue.prefab");
        }

        private static GameObject LoadItem()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testtonic.prefab");
        }

        [Test]
        public void AWaypointIsAddedAndRemovedWithItsTick()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("playersCanTravelToIt").boolValue = true;
                serialized.FindProperty("activateWithin").floatValue = 1.5f;
                serialized.FindProperty("isTheCoreWaypoint").boolValue = true;
            });
            RunObject();

            WayPointAuthoring waypoint = LoadObject().GetComponent<WayPointAuthoring>();
            Assert.IsNotNull(waypoint);
            Assert.AreEqual(1.5f, waypoint.distanceToActivate);
            Assert.IsTrue(waypoint.isCoreWaypoint);

            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("playersCanTravelToIt").boolValue = false;
            });
            RunObject();
            Assert.IsNull(LoadObject().GetComponent<WayPointAuthoring>());
        }

        [Test]
        public void TheCoreFlagOnlyCountsWhenItIsActuallyAWaypoint()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("playersCanTravelToIt").boolValue = false;
                serialized.FindProperty("isTheCoreWaypoint").boolValue = true;
            });

            Assert.IsFalse(worldObject.IsTheCoreWaypoint);
        }

        [Test]
        public void AConveyorPushesTheWayItWasTold()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                SerializedProperty automation = serialized.FindProperty("automation");
                automation.FindPropertyRelative("pushesWhatStandsOnIt").boolValue = true;
                SerializedProperty directions = automation.FindPropertyRelative("pushDirections");
                directions.arraySize = 2;
                directions.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(1, 0);
                directions.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(0, 1);
                automation.FindPropertyRelative("pushPriority").intValue = 3;
            });
            RunObject();

            VelocityAffectorAuthoring push = LoadObject().GetComponent<VelocityAffectorAuthoring>();
            Assert.IsNotNull(push);
            Assert.AreEqual(2, push.moveForceOptions.Count, "one direction per variation");
            Assert.AreEqual(3, push.priority);
        }

        [Test]
        public void ABeltToldNoDirectionIsReported()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("automation")
                    .FindPropertyRelative("pushesWhatStandsOnIt").boolValue = true;
            });

            DimensionWorldObjectGenerationReport report = RunObject();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("runs and moves nothing")));
            Assert.IsTrue(worldObject.Automation.PushesNowhere);
        }

        [Test]
        public void APotionIsAMarkerAddedAndRemovedWithItsTick()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("isAPotion").boolValue = true;
            });
            RunItem();
            Assert.IsNotNull(LoadItem().GetComponent<PotionAuthoring>());

            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("isAPotion").boolValue = false;
            });
            RunItem();
            Assert.IsNull(LoadItem().GetComponent<PotionAuthoring>());
        }

        [Test]
        public void AScannerFindsWhatItWasPointedAt()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("scansForObjectId").stringValue = "Wood";
                serialized.FindProperty("summonsInsteadOfScanning").boolValue = true;
            });
            RunItem();

            ScannerAuthoring scanner = LoadItem().GetComponent<ScannerAuthoring>();
            Assert.IsNotNull(scanner);
            Assert.AreEqual(ObjectID.Wood, scanner.objectToScan);
            Assert.IsTrue(scanner.summonInsteadOfScan);
        }

        [Test]
        public void AScannerPointedAtNothingRealIsReported()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("scansForObjectId").stringValue = "NotAnObject";
            });

            DimensionItemGenerationReport report = RunItem();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotAnObject")));
            Assert.IsNull(LoadItem().GetComponent<ScannerAuthoring>());
        }

        [Test]
        public void ScannerAnswersReadBackEmptyWhenItIsNotAScanner()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("summonsInsteadOfScanning").boolValue = true;
                serialized.FindProperty("scannerOnlyInBiome").stringValue = "Dirt";
            });

            Assert.IsFalse(item.IsAScanner);
            Assert.IsFalse(item.SummonsInsteadOfScanning);
            Assert.IsEmpty(item.ScannerOnlyInBiome);
        }
    }
}
