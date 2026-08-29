using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the two components that were attached to everything with nothing set on them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the pair the field-coverage audit found first, and they are the clearest case for
    /// why the component count was the wrong measure. Both were present on every generated object,
    /// so everything downstream assumed they were configured, and every field on them sat at its
    /// default.
    /// </para>
    /// <para>
    /// The tier is the one that changes numbers: <c>AreaLevelAuthoring</c> is what the health and
    /// damage curves read, so a default of Slime made a Crystal-biome boss compute starting-area
    /// stats. The facing is why a creature with four-way art would never turn.
    /// </para>
    /// </remarks>
    public sealed class DimensionObjectBasicsTests
    {
        private const string TestRoot = "Assets/NullforgeBasicsTests";

        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeBasicsTests");
            }

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("objectIdentifier").stringValue = "testthing";
            serialized.FindProperty("displayName").stringValue = "Test Thing";
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private void Set(string template, System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized.FindProperty(template));
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testthing.prefab");
        }

        [Test]
        public void TheWorldTierReachesTheComponentThatDrivesTheStatCurves()
        {
            Set("basics", delegate(SerializedProperty basics)
            {
                basics.FindPropertyRelative("areaTier").intValue = (int)DimensionAreaTier.Crystal;
                basics.FindPropertyRelative("scalesWithItsTier").boolValue = true;
            });
            Run();

            AreaLevelAuthoring level = Load().GetComponent<AreaLevelAuthoring>();
            Assert.IsNotNull(level);
            Assert.AreEqual(
                AreaLevel.Crystal,
                level.areaLevel,
                "a Crystal-biome thing computing starting-area stats is the bug this closes");
        }

        [Test]
        public void TheTierMappingIsExactRatherThanOrdinal()
        {
            // AreaLevel is not sequential: Clay is 10, Stone 30, Crystal 70. Treating our enum as an
            // ordinal would land on a different biome entirely.
            Set("basics", delegate(SerializedProperty basics)
            {
                basics.FindPropertyRelative("areaTier").intValue = (int)DimensionAreaTier.Stone;
                basics.FindPropertyRelative("scalesWithItsTier").boolValue = true;
            });
            Run();

            Assert.AreEqual(AreaLevel.Stone, Load().GetComponent<AreaLevelAuthoring>().areaLevel);
            Assert.AreEqual(30, (int)DimensionAreaTier.Stone);
        }

        [Test]
        public void SomethingThatTurnsGetsOrientationSupport()
        {
            Set("basics", delegate(SerializedProperty basics)
            {
                basics.FindPropertyRelative("facing").intValue = (int)DimensionFacing.AllFourWays;
            });
            Run();

            AnimationAuthoring animation = Load().GetComponent<AnimationAuthoring>();
            Assert.IsNotNull(animation);
            Assert.AreEqual(
                AnimationAuthoring.OrientationSupport.Horizontal |
                    AnimationAuthoring.OrientationSupport.Vertical,
                animation.orientationSupport,
                "four-way art with no orientation support never turns");
        }

        [Test]
        public void SceneryKeepsTheDefaultOfNotTurning()
        {
            Run();

            Assert.AreEqual(
                AnimationAuthoring.OrientationSupport.None,
                Load().GetComponent<AnimationAuthoring>().orientationSupport,
                "1,234 vanilla prefabs leave this at None, so it is the right default");
        }

        [Test]
        public void ATierChosenWithoutOptingIntoScalingIsReportedRatherThanIgnored()
        {
            // The component that reads a tier is the level trap, so it is never added on a hunch.
            // A tier that nothing will read is exactly the kind of dead setting this audit exists
            // to remove, so it says so rather than sitting there looking configured.
            Set("basics", delegate(SerializedProperty basics)
            {
                basics.FindPropertyRelative("areaTier").intValue = (int)DimensionAreaTier.Lava;
                basics.FindPropertyRelative("scalesWithItsTier").boolValue = false;
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("does nothing")));
            Assert.IsNull(
                Load().GetComponent<AreaLevelAuthoring>(),
                "opting out must not leave the scaling component behind");
        }

        [Test]
        public void NotChoosingATierSaysNothingAtAll()
        {
            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsFalse(report.Warnings.Exists(w => w.Contains("does nothing")));
        }

        // ---- conditions ----

        [Test]
        public void ACertainConditionAndAChancedOneGoIntoDifferentLists()
        {
            // Core Keeper keeps them apart. A creator says "40% of the time" once and the sorting
            // happens in the generator.
            Set("conditions", delegate(SerializedProperty conditions)
            {
                SerializedProperty starts = conditions.FindPropertyRelative("startsWith");
                starts.arraySize = 2;

                SerializedProperty certain = starts.GetArrayElementAtIndex(0);
                certain.FindPropertyRelative("conditionId").stringValue = "ArmorIncrease";
                certain.FindPropertyRelative("chance").floatValue = 1f;

                SerializedProperty chanced = starts.GetArrayElementAtIndex(1);
                chanced.FindPropertyRelative("conditionId").stringValue = "CritChance";
                chanced.FindPropertyRelative("chance").floatValue = 0.4f;
            });
            Run();

            SupportsConditionsAuthoring supports =
                Load().GetComponent<SupportsConditionsAuthoring>();
            Assert.IsNotNull(supports);
            Assert.AreEqual(1, supports.initialConditions.Count);
            Assert.AreEqual(1, supports.initialConditionsWithRandomChance.Count);
            Assert.AreEqual(0.4f, supports.initialConditionsWithRandomChance[0].chance);
        }

        [Test]
        public void AConditionTheGameDoesNotHaveIsReportedRatherThanStartedWith()
        {
            Set("conditions", delegate(SerializedProperty conditions)
            {
                SerializedProperty starts = conditions.FindPropertyRelative("startsWith");
                starts.arraySize = 1;
                starts.GetArrayElementAtIndex(0).FindPropertyRelative("conditionId").stringValue =
                    "NotACondition";
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotACondition")));
            Assert.IsEmpty(Load().GetComponent<SupportsConditionsAuthoring>().initialConditions);
        }

        [Test]
        public void TheImmunitiesReachTheGame()
        {
            Set("conditions", delegate(SerializedProperty conditions)
            {
                conditions.FindPropertyRelative("aurasDoNotReachIt").boolValue = true;
                conditions.FindPropertyRelative("theEnvironmentDoesNotAffectIt").boolValue = true;
                conditions.FindPropertyRelative("healingDoesNotTouchIt").boolValue = true;
            });
            Run();

            SupportsConditionsAuthoring supports =
                Load().GetComponent<SupportsConditionsAuthoring>();
            Assert.IsTrue(supports.cantBeAffectedByAuras);
            Assert.IsTrue(supports.cantBeAffectedByEnvironment);
            Assert.IsTrue(supports.cantBeAffectedByHealing);
        }

        [Test]
        public void TheImmunitiesAreClearedAgainWhenUnticked()
        {
            Set("conditions", delegate(SerializedProperty conditions)
            {
                conditions.FindPropertyRelative("aurasDoNotReachIt").boolValue = true;
            });
            Run();
            Assert.IsTrue(Load().GetComponent<SupportsConditionsAuthoring>().cantBeAffectedByAuras);

            Set("conditions", delegate(SerializedProperty conditions)
            {
                conditions.FindPropertyRelative("aurasDoNotReachIt").boolValue = false;
            });
            Run();
            Assert.IsFalse(Load().GetComponent<SupportsConditionsAuthoring>().cantBeAffectedByAuras);
        }

        [Test]
        public void AConditionThatCanNeverHappenIsReported()
        {
            Set("conditions", delegate(SerializedProperty conditions)
            {
                SerializedProperty starts = conditions.FindPropertyRelative("startsWith");
                starts.arraySize = 1;
                SerializedProperty only = starts.GetArrayElementAtIndex(0);
                only.FindPropertyRelative("conditionId").stringValue = "ArmorIncrease";
                only.FindPropertyRelative("chance").floatValue = 0f;
            });

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("can never happen")));
        }
    }
}
