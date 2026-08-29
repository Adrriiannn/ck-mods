using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers what a thing leaves standing when it dies, and what it shrugs off.
    /// </summary>
    /// <remarks>
    /// The one worth guarding hardest is the crowd limit. A thing that leaves two of itself and has
    /// no limit on how many may gather is an unbounded chain — kill one, get two, kill those, get
    /// four — and nothing about it errors. It is only obvious once a world is full of them, which is
    /// far too late for the person who shipped the mod.
    /// </remarks>
    public sealed class DimensionLeavesBehindTests
    {
        private const string TestRoot = "Assets/NullforgeLeavesTests";

        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeLeavesTests");
            }

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SetString("objectIdentifier", "testcocoon");
            SetString("displayName", "Test Cocoon");
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void SetString(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetLeaves(System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized.FindProperty("leavesBehind"));
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testcocoon.prefab");
        }

        [Test]
        public void WhatItLeavesBehindReachesTheGame()
        {
            SetLeaves(delegate(SerializedProperty leaves)
            {
                leaves.FindPropertyRelative("objectId").stringValue = "Wood";
                leaves.FindPropertyRelative("chance").floatValue = 0.5f;
                leaves.FindPropertyRelative("minimumAmount").intValue = 2;
                leaves.FindPropertyRelative("maximumAmount").intValue = 3;
                leaves.FindPropertyRelative("crowdLimit").intValue = 6;
            });
            Run();

            SpawnOnDeathAuthoring spawn = Load().GetComponent<SpawnOnDeathAuthoring>();
            Assert.IsNotNull(spawn);
            Assert.AreEqual(ObjectID.Wood, spawn.objectToSpawn);
            Assert.AreEqual(0.5f, spawn.spawnChance);
            Assert.AreEqual(2, spawn.amount.min);
            Assert.AreEqual(3, spawn.amount.max);
            Assert.AreEqual(6, spawn.maxAmountAllowedWithinRadius);
        }

        [Test]
        public void SomethingThatLeavesNothingCarriesNoComponent()
        {
            SetLeaves(delegate(SerializedProperty leaves)
            {
                leaves.FindPropertyRelative("objectId").stringValue = "Wood";
            });
            Run();
            Assert.IsNotNull(Load().GetComponent<SpawnOnDeathAuthoring>());

            SetLeaves(delegate(SerializedProperty leaves)
            {
                leaves.FindPropertyRelative("objectId").stringValue = string.Empty;
            });
            Run();
            Assert.IsNull(Load().GetComponent<SpawnOnDeathAuthoring>());
        }

        [Test]
        public void AnObjectTheGameDoesNotHaveIsReportedRatherThanLeavingNothingQuietly()
        {
            SetLeaves(delegate(SerializedProperty leaves)
            {
                leaves.FindPropertyRelative("objectId").stringValue = "NotAnObject";
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(warning => warning.Contains("NotAnObject")));
            Assert.IsNull(Load().GetComponent<SpawnOnDeathAuthoring>());
        }

        [Test]
        public void AnUnboundedChainIsReportedBeforeItShips()
        {
            SetLeaves(delegate(SerializedProperty leaves)
            {
                leaves.FindPropertyRelative("objectId").stringValue = "Wood";
                leaves.FindPropertyRelative("maximumAmount").intValue = 2;
                leaves.FindPropertyRelative("crowdLimit").intValue = 0;
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(
                report.Warnings.Exists(warning => warning.Contains("unbounded chain")),
                "kill one, get two, kill those, get four");
            Assert.IsTrue(worldObject.LeavesBehind.CanGrowWithoutLimit);
        }

        [Test]
        public void AMaximumBelowTheMinimumIsRaisedToMeetIt()
        {
            // Rather than writing a range the game reads backwards.
            SetLeaves(delegate(SerializedProperty leaves)
            {
                leaves.FindPropertyRelative("objectId").stringValue = "Wood";
                leaves.FindPropertyRelative("minimumAmount").intValue = 5;
                leaves.FindPropertyRelative("maximumAmount").intValue = 2;
            });

            Assert.AreEqual(5, worldObject.LeavesBehind.MaximumAmount);
        }

        [Test]
        public void ImmunitiesAreAddedAndRemovedRatherThanOnlyAdded()
        {
            GameObject root = new GameObject("immune");
            try
            {
                DimensionObjectSpine.ApplyImmunities(root, true, true, true);
                Assert.IsNotNull(root.GetComponent<ImmuneToPushBackAuthoring>());
                Assert.IsNotNull(root.GetComponent<ImmuneToRangeDamageAuthoring>());
                Assert.IsNotNull(root.GetComponent<ImmuneToSkipLootDropAuthoring>());

                DimensionObjectSpine.ApplyImmunities(root, false, false, false);
                Assert.IsNull(root.GetComponent<ImmuneToPushBackAuthoring>());
                Assert.IsNull(root.GetComponent<ImmuneToRangeDamageAuthoring>());
                Assert.IsNull(root.GetComponent<ImmuneToSkipLootDropAuthoring>());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
