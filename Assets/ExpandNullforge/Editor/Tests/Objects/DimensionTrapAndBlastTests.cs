using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers traps, safe zones, and the blast a bomb turns into.
    /// </summary>
    /// <remarks>
    /// The explosion asset closes a loop the explosive work left open: a mod could make a bomb and
    /// could not make its blast, so the only answers to "what does this explode into" were vanilla's.
    /// The trap's own trap is the power requirement — 24 of the game's 31 continuous attackers need
    /// power, so it defaults on, and a trap that needs power and was never wired never fires once
    /// while looking completely correct on its own.
    /// </remarks>
    public sealed class DimensionTrapAndBlastTests
    {
        private const string TestRoot = "Assets/NullforgeTrapTests";

        private DimensionWorldObjectAsset worldObject;
        private DimensionExplosionAsset explosion;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeTrapTests");
            }

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("objectIdentifier").stringValue = "testspike";
            serialized.FindProperty("displayName").stringValue = "Test Spike";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            explosion = ScriptableObject.CreateInstance<DimensionExplosionAsset>();
            SerializedObject other = new SerializedObject(explosion);
            other.FindProperty("explosionId").stringValue = "testblast";
            other.FindProperty("displayName").stringValue = "Test Blast";
            other.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (explosion != null)
            {
                Object.DestroyImmediate(explosion);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void SetAttack(System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized.FindProperty("continuousAttack"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionWorldObjectGenerationReport RunObject()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject LoadObject()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testspike.prefab");
        }

        // ---- traps ----

        [Test]
        public void SomethingThatHurtsNothingCarriesNoAttack()
        {
            RunObject();
            Assert.IsNull(LoadObject().GetComponent<AttackContinuouslyAuthoring>());
        }

        [Test]
        public void TheTrapDialsReachTheGame()
        {
            SetAttack(delegate(SerializedProperty attack)
            {
                attack.FindPropertyRelative("hurtsWhatComesNear").boolValue = true;
                attack.FindPropertyRelative("damage").intValue = 25;
                attack.FindPropertyRelative("reach").floatValue = 0.6f;
                attack.FindPropertyRelative("needsPower").boolValue = false;
                attack.FindPropertyRelative("sparesThingsOnWalls").boolValue = true;
            });
            RunObject();

            AttackContinuouslyAuthoring trap = LoadObject().GetComponent<AttackContinuouslyAuthoring>();
            Assert.IsNotNull(trap);
            Assert.AreEqual(25, trap.damage);
            Assert.AreEqual(0.6f, trap.hitRadius);
            Assert.IsFalse(trap.requiresElectricity);
            Assert.IsTrue(trap.cantDamageObjectsHangingOnWalls);
        }

        [Test]
        public void ATrapThatNeedsPowerAndIsNotWiredIsReported()
        {
            // It looks completely correct on its own — the answer lives on a different template.
            SetAttack(delegate(SerializedProperty attack)
            {
                attack.FindPropertyRelative("hurtsWhatComesNear").boolValue = true;
                attack.FindPropertyRelative("needsPower").boolValue = true;
            });

            DimensionWorldObjectGenerationReport report = RunObject();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("never fire")));
            Assert.IsTrue(
                worldObject.ContinuousAttack.NeedsPowerButIsNotWired(worldObject.Wiring));
        }

        [Test]
        public void ATrapWiredForPowerIsNotReported()
        {
            SetAttack(delegate(SerializedProperty attack)
            {
                attack.FindPropertyRelative("hurtsWhatComesNear").boolValue = true;
                attack.FindPropertyRelative("needsPower").boolValue = true;
            });

            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("wiring").FindPropertyRelative("role").intValue =
                (int)DimensionWiringRole.PoweredDevice;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionWorldObjectGenerationReport report = RunObject();

            Assert.IsFalse(report.Warnings.Exists(w => w.Contains("never fire")));
        }

        [Test]
        public void ASafeZoneIsAddedAndRemovedWithItsTick()
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("keepsThingsSafeNearby").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RunObject();
            Assert.IsNotNull(LoadObject().GetComponent<ImmunityZoneAuthoring>());

            serialized = new SerializedObject(worldObject);
            serialized.FindProperty("keepsThingsSafeNearby").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RunObject();
            Assert.IsNull(LoadObject().GetComponent<ImmunityZoneAuthoring>());
        }

        // ---- the blast a bomb turns into ----

        [Test]
        public void AnExplosionIsGeneratedWithTheFourThingsItNeeds()
        {
            DimensionExplosionGenerator.Generate(
                new List<DimensionExplosionAsset> { explosion },
                TestRoot,
                default(DimensionNamingContext));

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testblast.prefab");
            Assert.IsNotNull(prefab.GetComponent<ExplosionAuthoring>());
            Assert.IsNotNull(
                prefab.GetComponent<DestroyTimerAuthoring>(),
                "an explosion with no timer keeps doing its damage over an area, forever");
            Assert.IsNotNull(prefab.GetComponent<DontSerializeAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<CantBeAttackedAuthoring>());

            // The one whose absence is invisible: without behaviour tags the blast falls outside
            // ExplosionDamageSystem's query and does nothing at all.
            Assert.IsNotNull(
                prefab.GetComponent<BehaviourTagsAuthoring>(),
                "a blast with no behaviour tags is never seen by the game's damage pass");
        }

        [Test]
        public void TheBlastReachReachesTheGame()
        {
            SerializedObject serialized = new SerializedObject(explosion);
            serialized.FindProperty("radius").floatValue = 3.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionExplosionGenerator.Generate(
                new List<DimensionExplosionAsset> { explosion },
                TestRoot,
                default(DimensionNamingContext));

            ExplosionAuthoring blast = AssetDatabase
                .LoadAssetAtPath<GameObject>(TestRoot + "/testblast.prefab")
                .GetComponent<ExplosionAuthoring>();
            Assert.AreEqual(3.5f, blast.radius);

            // Deliberately zero. Every path that spawns a blast writes its own damage over the
            // prefab's before the blast acts, so a number here would be a promise nothing keeps.
            Assert.AreEqual(0, blast.damage);
            Assert.AreEqual(0, blast.tileDamage);
        }

        [Test]
        public void ABlastThatCatchesNothingIsReported()
        {
            SerializedObject serialized = new SerializedObject(explosion);
            serialized.FindProperty("radius").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionExplosionGenerationReport report = DimensionExplosionGenerator.Generate(
                new List<DimensionExplosionAsset> { explosion },
                TestRoot,
                default(DimensionNamingContext));

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("catches nothing")));
            Assert.IsTrue(explosion.ReachesNothing);
        }
    }
}
