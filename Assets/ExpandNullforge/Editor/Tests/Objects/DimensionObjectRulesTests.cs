using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the small rules the world applies to a placed object, and critters.
    /// </summary>
    /// <remarks>
    /// Every one of these is a tick, and the rule they share is that unticking has to take the
    /// component off. Half of them are bare markers whose whole meaning is their presence, so an
    /// "add only" generator would leave an object behaving as it did three edits ago with nothing
    /// in the inspector to say why.
    /// </remarks>
    public sealed class DimensionObjectRulesTests
    {
        private const string TestRoot = "Assets/NullforgeRulesTests";

        private DimensionWorldObjectAsset worldObject;
        private DimensionCritterAsset critter;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeRulesTests");
            }

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("objectIdentifier").stringValue = "testrock";
            serialized.FindProperty("displayName").stringValue = "Test Rock";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            critter = ScriptableObject.CreateInstance<DimensionCritterAsset>();
            SerializedObject critterObject = new SerializedObject(critter);
            critterObject.FindProperty("critterId").stringValue = "testbug";
            critterObject.FindProperty("displayName").stringValue = "Test Bug";
            critterObject.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (critter != null)
            {
                Object.DestroyImmediate(critter);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void SetRules(System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized.FindProperty("rules"));
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testrock.prefab");
        }

        [Test]
        public void TheBareMarkersAreAddedAndRemovedWithTheirTicks()
        {
            SetRules(delegate(SerializedProperty rules)
            {
                rules.FindPropertyRelative("alwaysDropsItsFirstVariation").boolValue = true;
                rules.FindPropertyRelative("isGroundCover").boolValue = true;
                rules.FindPropertyRelative("aScannerFindsIt").boolValue = true;
            });
            Run();

            GameObject prefab = Load();
            Assert.IsNotNull(prefab.GetComponent<AlwaysDropVariationZeroAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<GroundDecorationAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<CanBeScannedAuthoring>());

            SetRules(delegate(SerializedProperty rules)
            {
                rules.FindPropertyRelative("alwaysDropsItsFirstVariation").boolValue = false;
                rules.FindPropertyRelative("isGroundCover").boolValue = false;
                rules.FindPropertyRelative("aScannerFindsIt").boolValue = false;
            });
            Run();

            prefab = Load();
            Assert.IsNull(prefab.GetComponent<AlwaysDropVariationZeroAuthoring>());
            Assert.IsNull(prefab.GetComponent<GroundDecorationAuthoring>());
            Assert.IsNull(prefab.GetComponent<CanBeScannedAuthoring>());
        }

        [Test]
        public void StayingEnabledIsOnlyWrittenWhenItIsBeingTurnedOff()
        {
            // The component's own default is true, so writing it for the ordinary case would put a
            // component on every object saying what would have happened anyway.
            Run();
            Assert.IsNull(Load().GetComponent<CustomDisableAuthoring>());

            SetRules(delegate(SerializedProperty rules)
            {
                rules.FindPropertyRelative("alwaysStaysEnabled").boolValue = false;
            });
            Run();

            CustomDisableAuthoring disable = Load().GetComponent<CustomDisableAuthoring>();
            Assert.IsNotNull(disable);
            Assert.IsFalse(disable.alwaysEnabled);
        }

        [Test]
        public void ANetworkRangeIsOnlyWrittenWhenOneWasChosen()
        {
            Run();
            Assert.IsNull(Load().GetComponent<OverrideNetworkSyncDistanceAuthoring>());

            SetRules(delegate(SerializedProperty rules)
            {
                rules.FindPropertyRelative("networkRange").floatValue = 40f;
            });
            Run();

            Assert.AreEqual(
                40f,
                Load().GetComponent<OverrideNetworkSyncDistanceAuthoring>().distance);
        }

        [Test]
        public void APlacementRuleThatMatchesNothingIsReported()
        {
            // Naming nothing to stand on and ticking no surface means it destroys itself the moment
            // it is placed, which reads as an object that will not place at all.
            SetRules(delegate(SerializedProperty rules)
            {
                rules.FindPropertyRelative("mustStandOnObjectId").stringValue = "NotAnObject";
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotAnObject")));
        }

        [Test]
        public void AMarkerTheGameDoesNotHaveIsReportedRatherThanShownWrong()
        {
            SetRules(delegate(SerializedProperty rules)
            {
                rules.FindPropertyRelative("mapMarker").stringValue = "NotAMarker";
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotAMarker")));
            Assert.IsNull(Load().GetComponent<MapMarkerAuthoring>());
        }

        [Test]
        public void AKnownMarkerReachesTheGame()
        {
            SetRules(delegate(SerializedProperty rules)
            {
                rules.FindPropertyRelative("mapMarker").stringValue = "Portal";
                rules.FindPropertyRelative("markerGoesOnceFound").boolValue = true;
            });
            Run();

            MapMarkerAuthoring marker = Load().GetComponent<MapMarkerAuthoring>();
            Assert.IsNotNull(marker);
            Assert.AreEqual(MapMarkerType.Portal, marker.mapMarkerType);
            Assert.IsTrue(marker.hideWhenDiscovered);
        }

        // ---- critters, which nothing generated at all until now ----

        [Test]
        public void ACritterIsGeneratedAtAll()
        {
            DimensionCritterGenerationReport report = DimensionCritterGenerator.Generate(
                new List<DimensionCritterAsset> { critter },
                TestRoot,
                default(DimensionNamingContext));

            Assert.AreEqual(1, report.Created.Count);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testbug.prefab");
            Assert.IsNotNull(prefab.GetComponent<CritterAuthoring>());
        }

        [Test]
        public void ACritterWithNowhereToLiveIsReported()
        {
            // It generates cleanly, is counted on the dashboard, and never appears in the world.
            DimensionCritterGenerationReport report = DimensionCritterGenerator.Generate(
                new List<DimensionCritterAsset> { critter },
                TestRoot,
                default(DimensionNamingContext));

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("names nowhere to live")));
            Assert.IsTrue(critter.LivesNowhere);
        }

        [Test]
        public void BothSpawnListsAreAlwaysAssignedEvenTheUnusedOne()
        {
            // The converter reads whichever matches the spawn type, and a null list throws before
            // it gets there.
            DimensionCritterGenerator.Generate(
                new List<DimensionCritterAsset> { critter },
                TestRoot,
                default(DimensionNamingContext));

            CritterAuthoring authored = AssetDatabase
                .LoadAssetAtPath<GameObject>(TestRoot + "/testbug.prefab")
                .GetComponent<CritterAuthoring>();

            Assert.IsNotNull(authored.biomesToSpawnIn);
            Assert.IsNotNull(authored.tilesetsToSpawnIn);
        }
    }
}
