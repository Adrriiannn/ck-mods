using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers how a creature arrives, idles, answers a bigger party, and leaves.
    /// </summary>
    /// <remarks>
    /// The party scaling is the one that matters most in play. Every vanilla prefab measured
    /// carrying it is a boss or a serious enemy, and a custom boss without it has the same health
    /// for one player as for four — the single most common complaint about modded bosses. It is
    /// opt-in with a real number rather than a tick, because vanilla does use partial scaling.
    /// </remarks>
    public sealed class DimensionCreatureLifecycleTests
    {
        private const string TestRoot = "Assets/NullforgeLifecycleTests";

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
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject serialized = new SerializedObject(host);
            write(serialized.FindProperty("combat").FindPropertyRelative("lifecycle"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureCombatTemplate built = host.Combat;
            Object.DestroyImmediate(host);
            return built;
        }

        private static DimensionCreatureStatsTemplate Stats()
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            holder.FindProperty("creatureStats").FindPropertyRelative("maxHealth").intValue = 30;
            holder.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureStatsTemplate stats = host.CreatureStats;
            Object.DestroyImmediate(host);
            return stats;
        }

        private static DimensionCreatureGenerationReport Run(DimensionCreatureCombatTemplate combat)
        {
            return DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = "testbeast",
                        DisplayName = "Test Beast",
                        Stats = Stats(),
                        Combat = combat,
                        IsEnemy = true
                    }
                },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject Load()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testbeast.prefab");
        }

        [Test]
        public void ABossThatScalesWithThePartyGetsTheComponent()
        {
            Run(Combat(delegate(SerializedProperty lifecycle)
            {
                lifecycle.FindPropertyRelative("healthScalesWithPlayers").floatValue = 1f;
            }));

            ScaleHealthByPlayerCountAuthoring scaling =
                Load().GetComponent<ScaleHealthByPlayerCountAuthoring>();
            Assert.IsNotNull(scaling);
            Assert.AreEqual(1f, scaling.scalingFactor);
        }

        [Test]
        public void ACreatureThatDoesNotScaleCarriesNoScaling()
        {
            Run(Combat(delegate(SerializedProperty lifecycle)
            {
                lifecycle.FindPropertyRelative("healthScalesWithPlayers").floatValue = 0f;
            }));

            Assert.IsNull(Load().GetComponent<ScaleHealthByPlayerCountAuthoring>());
        }

        [Test]
        public void ABossBurstsOutOfTheGroundWhenItArrives()
        {
            Run(Combat(delegate(SerializedProperty lifecycle)
            {
                lifecycle.FindPropertyRelative("makesAnEntrance").boolValue = true;
                lifecycle.FindPropertyRelative("entranceAnimation").stringValue = "emerge";
                lifecycle.FindPropertyRelative("entranceSeconds").floatValue = 2f;
                lifecycle.FindPropertyRelative("clearsTheGroundAsItArrives").boolValue = true;
                lifecycle.FindPropertyRelative("clearedRadius").floatValue = 3f;
            }));

            SpawnStateAuthoring entrance = Load().GetComponent<SpawnStateAuthoring>();
            Assert.IsNotNull(entrance);
            Assert.AreEqual("emerge", entrance.animId);
            Assert.AreEqual(2f, entrance.duration);
            Assert.IsTrue(entrance.removeTilesOnSpawn);
            Assert.AreEqual(3f, entrance.radiusToRemoveTilesWithin);
        }

        [Test]
        public void AnEntranceWithNoAnimationIsReported()
        {
            DimensionCreatureGenerationReport report = Run(Combat(delegate(SerializedProperty lifecycle)
            {
                lifecycle.FindPropertyRelative("makesAnEntrance").boolValue = true;
            }));

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("no animation to make it with")));
        }

        [Test]
        public void IdleAnimationsReachTheGameWithTheirOwnTiming()
        {
            Run(Combat(delegate(SerializedProperty lifecycle)
            {
                SerializedProperty emotes = lifecycle.FindPropertyRelative("idleEmotes");
                emotes.arraySize = 1;
                SerializedProperty first = emotes.GetArrayElementAtIndex(0);
                first.FindPropertyRelative("animation").stringValue = "scratch";
                first.FindPropertyRelative("seconds").floatValue = 1.5f;
                lifecycle.FindPropertyRelative("minimumIdleGap").floatValue = 2f;
                lifecycle.FindPropertyRelative("maximumIdleGap").floatValue = 6f;
            }));

            IdleEmoteStateAuthoring emote = Load().GetComponent<IdleEmoteStateAuthoring>();
            Assert.IsNotNull(emote);
            Assert.AreEqual(1, emote.emoteAnimations.Count);
            Assert.AreEqual("scratch", emote.emoteAnimations[0].animation);
            Assert.AreEqual(2f, emote.minCooldown);
            Assert.AreEqual(6f, emote.maxCooldown);
        }

        [Test]
        public void AnEmoteNamingNoAnimationIsDroppedRatherThanWrittenBlank()
        {
            Run(Combat(delegate(SerializedProperty lifecycle)
            {
                SerializedProperty emotes = lifecycle.FindPropertyRelative("idleEmotes");
                emotes.arraySize = 1;
                emotes.GetArrayElementAtIndex(0).FindPropertyRelative("animation").stringValue =
                    string.Empty;
            }));

            Assert.IsNull(
                Load().GetComponent<IdleEmoteStateAuthoring>(),
                "one blank emote is no emotes, not an empty list");
        }

        [Test]
        public void ABackwardsWaitRangeIsCorrectedRatherThanWrittenBackwards()
        {
            DimensionCreatureCombatTemplate combat = Combat(delegate(SerializedProperty lifecycle)
            {
                lifecycle.FindPropertyRelative("minimumIdleGap").floatValue = 8f;
                lifecycle.FindPropertyRelative("maximumIdleGap").floatValue = 2f;
            });

            Assert.AreEqual(8f, combat.Lifecycle.MaximumIdleGap);
        }

        [Test]
        public void DespawningAndDeathClearingAreAddedAndRemovedWithTheirSettings()
        {
            Run(Combat(delegate(SerializedProperty lifecycle)
            {
                lifecycle.FindPropertyRelative("despawnsWhenNobodyIsWithin").floatValue = 60f;
                lifecycle.FindPropertyRelative("deathClearsThingsNearby").boolValue = true;
            }));

            Assert.IsNotNull(Load().GetComponent<DestroyWhenNoNearbyPlayerAuthoring>());
            Assert.IsNotNull(Load().GetComponent<DestroyNearbyOnDeathAuthoring>());

            Run(Combat(delegate(SerializedProperty lifecycle)
            {
                lifecycle.FindPropertyRelative("despawnsWhenNobodyIsWithin").floatValue = 0f;
                lifecycle.FindPropertyRelative("deathClearsThingsNearby").boolValue = false;
            }));

            Assert.IsNull(Load().GetComponent<DestroyWhenNoNearbyPlayerAuthoring>());
            Assert.IsNull(Load().GetComponent<DestroyNearbyOnDeathAuthoring>());
        }
    }
}
