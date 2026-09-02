using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers what a weapon or a creature fires.
    /// </summary>
    /// <remarks>
    /// The components worth pinning hardest are the ones nobody would think to ask for. A projectile
    /// without <c>DestroyTimerAuthoring</c> means every missed shot in the mod stays in the world
    /// forever; without <c>DontSerializeAuthoring</c> they are written into the save and come back on
    /// load. Neither shows up while playing for the first minute, and both are ruinous after an hour.
    /// </remarks>
    public sealed class DimensionProjectileTests
    {
        private const string TestRoot = "Assets/NullforgeProjectileTests";

        private DimensionProjectileAsset projectile;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeProjectileTests");
            }

            projectile = ScriptableObject.CreateInstance<DimensionProjectileAsset>();
            SetString("projectileId", "testbolt");
            SetString("displayName", "Test Bolt");
        }

        [TearDown]
        public void Cleanup()
        {
            if (projectile != null)
            {
                Object.DestroyImmediate(projectile);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void SetString(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetFloat(string field, float value)
        {
            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionProjectileGenerationReport Run()
        {
            return DimensionProjectileGenerator.Generate(
                new List<DimensionProjectileAsset> { projectile },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject Load()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testbolt.prefab");
        }

        [Test]
        public void EveryProjectileCarriesTheFourThingsThatStopItRuiningTheWorld()
        {
            Run();
            GameObject prefab = Load();

            Assert.IsNotNull(
                prefab.GetComponent<DestroyTimerAuthoring>(),
                "without a timer every missed shot stays in the world forever");
            Assert.IsNotNull(
                prefab.GetComponent<DontSerializeAuthoring>(),
                "without this they are written into the save and come back on load");
            Assert.IsNotNull(
                prefab.GetComponent<DontDropSelfAuthoring>(),
                "a projectile that dies should not leave itself on the floor");
            Assert.IsNotNull(
                prefab.GetComponent<CantBeAttackedAuthoring>(),
                "nothing should target a shot in flight");
        }

        [Test]
        public void TheOrdinaryDialsReachTheGame()
        {
            SetFloat("hitRadius", 0.5f);
            SetFloat("speed", 14f);
            SetBool("goesThroughEnemies", true);
            SetInt("bounces", 4);
            Run();

            ProjectileAuthoring shot = Load().GetComponent<ProjectileAuthoring>();
            Assert.AreEqual(0.5f, shot.damageRadius);
            Assert.AreEqual(0.5f, shot.damageRadiusClient, "the client copy has to agree");
            Assert.IsTrue(shot.piercesEnemies);
            Assert.AreEqual(4, shot.maxBounceCount);
            Assert.AreEqual(14f, Load().GetComponent<MovementSpeedAuthoring>().speed);
        }

        [Test]
        public void TerrainDamageIsNotWrittenWhenItDoesNotDamageTerrain()
        {
            // Asking for a radius and leaving the tick off is the ordinary way to get a projectile
            // that quietly chews through walls.
            SetFloat("terrainHitRadius", 2f);
            SetBool("damagesTerrain", false);
            Run();

            Assert.AreEqual(0f, Load().GetComponent<ProjectileAuthoring>().tileDamageRadius);
        }

        [Test]
        public void AShotThatCanNeverHitAnythingIsReported()
        {
            SetFloat("hitRadius", 0f);

            DimensionProjectileGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(warning => warning.Contains("hit radius of zero")));
        }

        [Test]
        public void AShotThatNeverMovesIsReported()
        {
            SetFloat("speed", 0f);

            DimensionProjectileGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(warning => warning.Contains("no speed")));
        }

        [Test]
        public void ShardsWithNothingToBreakIntoAreReported()
        {
            SetInt("shards", 4);

            DimensionProjectileGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(warning => warning.Contains("breaks into nothing")));
            Assert.IsTrue(projectile.ShattersIntoNothing);
        }

        [Test]
        public void GeneratingTwiceUpdatesTheSamePrefab()
        {
            Assert.AreEqual(1, Run().Created.Count);

            DimensionProjectileGenerationReport second = Run();
            Assert.IsEmpty(second.Created);
            Assert.AreEqual(1, second.Updated.Count);
        }
    }
}
