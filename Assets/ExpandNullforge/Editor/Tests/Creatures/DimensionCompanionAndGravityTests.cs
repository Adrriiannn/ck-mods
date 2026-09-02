using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers creatures that arrive with company, and gravity wells that bend a wanderer.
    /// </summary>
    /// <remarks>
    /// Companions are the one that needed real thought. The component holds prefab references, not
    /// names, so wiring them while generating each creature would mean a creature listed before its
    /// companion finds nothing — the order of a list in the inspector deciding whether a boss
    /// arrives with its guards. It is a second pass for that reason, and that is what these pin.
    /// </remarks>
    public sealed class DimensionCompanionAndGravityTests
    {
        private const string TestRoot = "Assets/NullforgeCompanyTests";

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);
        }

        [TearDown]
        public void Cleanup()
        {
            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private static DimensionCreatureCombatTemplate Combat(System.Action<SerializedProperty> write)
        {
            // A world object is a serialization host; any asset with a combat template would do.
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject serialized = new SerializedObject(host);
            SerializedProperty combat = serialized.FindProperty("combat");
            write(combat);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureCombatTemplate built = host.Combat;
            Object.DestroyImmediate(host);
            return built;
        }

        /// <summary>
        /// The stats every creature request needs.
        /// </summary>
        /// <remarks>
        /// A request with no stats is skipped by design, so leaving them off makes these tests
        /// pass or fail for the wrong reason: nothing generated at all.
        /// </remarks>
        private static DimensionCreatureStatsTemplate Stats()
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty root = holder.FindProperty("creatureStats");
            root.FindPropertyRelative("maxHealth").intValue = 30;
            holder.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureStatsTemplate stats = host.CreatureStats;
            Object.DestroyImmediate(host);
            return stats;
        }

        private static DimensionCreatureGenerationReport Run(List<DimensionCreatureGenerator.Request> requests)
        {
            return DimensionCreatureGenerator.Generate(
                requests,
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject Load(string id)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
        }

        [Test]
        public void ABossArrivesWithItsGuardsEvenWhenListedFirst()
        {
            // The ordering case. The boss is generated before the guard exists.
            DimensionCreatureCombatTemplate withCompany = Combat(delegate(SerializedProperty combat)
            {
                SerializedProperty company = combat.FindPropertyRelative("arrivesWith");
                company.arraySize = 1;
                company.GetArrayElementAtIndex(0).stringValue = "testguard";
            });

            Run(new List<DimensionCreatureGenerator.Request>
            {
                new DimensionCreatureGenerator.Request
                {
                    CreatureId = "testboss",
                    DisplayName = "Test Boss",
                    Stats = Stats(),
                    Combat = withCompany,
                    IsEnemy = true,
                    IsBoss = true
                },
                new DimensionCreatureGenerator.Request
                {
                    CreatureId = "testguard",
                    DisplayName = "Test Guard",
                    Stats = Stats(),
                    IsEnemy = true
                }
            });

            SpawnCompanionsAuthoring companions = Load("testboss").GetComponent<SpawnCompanionsAuthoring>();
            Assert.IsNotNull(companions, "the guard is generated after the boss and must still be found");
            Assert.AreEqual(1, companions.companions.Count);
            Assert.IsTrue(companions.follow);
        }

        [Test]
        public void ACompanionTheModDoesNotGenerateIsReported()
        {
            DimensionCreatureCombatTemplate withGhost = Combat(delegate(SerializedProperty combat)
            {
                SerializedProperty company = combat.FindPropertyRelative("arrivesWith");
                company.arraySize = 1;
                company.GetArrayElementAtIndex(0).stringValue = "nosuchcreature";
            });

            DimensionCreatureGenerationReport report = Run(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = "testloner",
                        DisplayName = "Test Loner",
                        Stats = Stats(),
                        Combat = withGhost,
                        IsEnemy = true
                    }
                });

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("nosuchcreature")));
            Assert.IsNull(
                Load("testloner").GetComponent<SpawnCompanionsAuthoring>(),
                "no company at all is better than an empty list saying there is some");
        }

        [Test]
        public void ACreatureWithNoCompanyCarriesNoComponent()
        {
            Run(new List<DimensionCreatureGenerator.Request>
            {
                new DimensionCreatureGenerator.Request
                {
                    CreatureId = "testalone",
                    DisplayName = "Test Alone",
                    Stats = Stats(),
                    IsEnemy = true
                }
            });

            Assert.IsNull(Load("testalone").GetComponent<SpawnCompanionsAuthoring>());
        }

        [Test]
        public void GravityWellsAreOnlyWrittenWhenSomethingCanBeCaught()
        {
            GameObject root = new GameObject("wanderer");
            try
            {
                DimensionObjectSpine.ApplyGravityWells(root, 0f, 2f, 10f, 45f, 0);
                Assert.IsNull(
                    root.GetComponent<RandomWalkGravityAuthoring>(),
                    "a zero chance means wells never apply, so the component is not written");

                DimensionObjectSpine.ApplyGravityWells(root, 0.5f, 2f, 12f, 30f, 0);
                RandomWalkGravityAuthoring gravity = root.GetComponent<RandomWalkGravityAuthoring>();
                Assert.IsNotNull(gravity);
                Assert.AreEqual(0.5f, gravity.chanceToBeAffectedByGravityWell);
                Assert.AreEqual(2f, gravity.strength);
                Assert.AreEqual(12f, gravity.maxDistanceToBeAffected);
                Assert.AreEqual(30f, gravity.maxAngleDeviation);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GravityTunedButNeverAppliedIsDetectable()
        {
            DimensionCreatureCombatTemplate tuned = Combat(delegate(SerializedProperty combat)
            {
                combat.FindPropertyRelative("gravityWellStrength").floatValue = 5f;
                combat.FindPropertyRelative("gravityWellChance").floatValue = 0f;
            });

            Assert.IsTrue(tuned.TunedGravityThatNeverApplies);
            Assert.IsFalse(tuned.GravityWellsAffectIt);
        }
    }
}
