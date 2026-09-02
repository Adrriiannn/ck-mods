using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the artillery mode of a projectile — the falling rock, the boss bomb.
    /// </summary>
    /// <remarks>
    /// The rule these exist to hold is that artillery is a different COMPONENT, not a different
    /// setting. A mortar carries <c>MortarProjectileAuthoring</c> instead of
    /// <c>ProjectileAuthoring</c> — measured on the falling-rock and bomb prefabs, which have no
    /// straight-shot component at all — so switching a projectile to artillery has to take the
    /// straight-shot half off, or the shell gets two ways to hit the same thing.
    /// </remarks>
    public sealed class DimensionArtilleryTests
    {
        private const string TestRoot = "Assets/NullforgeArtilleryTests";

        private DimensionProjectileAsset projectile;

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            projectile = ScriptableObject.CreateInstance<DimensionProjectileAsset>();
            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty("projectileId").stringValue = "testshell";
            serialized.FindProperty("displayName").stringValue = "Test Shell";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void Cleanup()
        {
            if (projectile != null)
            {
                Object.DestroyImmediate(projectile);
            }

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private void Set(System.Action<SerializedObject> write)
        {
            SerializedObject serialized = new SerializedObject(projectile);
            write(serialized);
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testshell.prefab");
        }

        [Test]
        public void AProjectileCarriesOneFlightComponentOrTheOther()
        {
            Run();
            Assert.IsNotNull(Load().GetComponent<ProjectileAuthoring>());
            Assert.IsNull(Load().GetComponent<MortarProjectileAuthoring>());

            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flight").intValue =
                    (int)DimensionProjectileFlight.ArcsLikeArtillery;
            });
            Run();

            Assert.IsNotNull(Load().GetComponent<MortarProjectileAuthoring>());
            Assert.IsNull(
                Load().GetComponent<ProjectileAuthoring>(),
                "a shell that also flies straight has two ways to hit the same thing");
        }

        [Test]
        public void TheArcTimingsReachTheGame()
        {
            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flight").intValue =
                    (int)DimensionProjectileFlight.ArcsLikeArtillery;
                serialized.FindProperty("useTheGamesOwnTimings").boolValue = false;
                serialized.FindProperty("goUpSeconds").floatValue = 0.5f;
                serialized.FindProperty("airSeconds").floatValue = 0.6f;
                serialized.FindProperty("goDownSeconds").floatValue = 1f;
                serialized.FindProperty("breaksTerrainWhereItLands").boolValue = true;
            });
            Run();

            MortarProjectileAuthoring mortar = Load().GetComponent<MortarProjectileAuthoring>();
            Assert.IsFalse(mortar.useDefaultTimings);
            Assert.AreEqual(0.5f, mortar.goUpTime);
            Assert.AreEqual(0.6f, mortar.airTime);
            Assert.AreEqual(1f, mortar.goDownTime);
            Assert.IsTrue(mortar.hitTiles);
        }

        [Test]
        public void AShellWithItsOwnTimingsAllSetToZeroIsReported()
        {
            // It completes the whole arc in no time and goes off where it was fired.
            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flight").intValue =
                    (int)DimensionProjectileFlight.ArcsLikeArtillery;
                serialized.FindProperty("useTheGamesOwnTimings").boolValue = false;
                serialized.FindProperty("goUpSeconds").floatValue = 0f;
                serialized.FindProperty("airSeconds").floatValue = 0f;
                serialized.FindProperty("goDownSeconds").floatValue = 0f;
            });

            DimensionProjectileGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("no time")));
            Assert.IsTrue(projectile.ArcsInstantly);
        }

        [Test]
        public void ScatteringAVanillaTilesetOnTheWayDownReachesTheGame()
        {
            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flight").intValue =
                    (int)DimensionProjectileFlight.ArcsLikeArtillery;
                serialized.FindProperty("scattersTilesOnTheWayDown").boolValue = true;
                serialized.FindProperty("scatteredTilesetId").stringValue = "Dirt";
                serialized.FindProperty("scatterExtraRadius").floatValue = 1.5f;
            });

            DimensionProjectileGenerator.Generate(
                new List<DimensionProjectileAsset> { projectile },
                TestRoot,
                default(DimensionNamingContext));

            MortarProjectileAuthoring mortar = Load().GetComponent<MortarProjectileAuthoring>();
            Assert.IsTrue(mortar.spawnTilesOnGoingDown);
            Assert.AreEqual(PugTilemap.Tileset.Dirt, mortar.tilesetToSpawnOnGoingDown);
            Assert.AreEqual(1.5f, mortar.spawnTilesOnGoingDownExtraRadius);
        }

        [Test]
        public void ATilesetNothingDefinesTurnsTheScatteringOffRatherThanLeavingBareGround()
        {
            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flight").intValue =
                    (int)DimensionProjectileFlight.ArcsLikeArtillery;
                serialized.FindProperty("scattersTilesOnTheWayDown").boolValue = true;
                serialized.FindProperty("scatteredTilesetId").stringValue = "NotATileset";
            });

            DimensionProjectileGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotATileset")));
            Assert.IsFalse(Load().GetComponent<MortarProjectileAuthoring>().spawnTilesOnGoingDown);
        }

        [Test]
        public void ScatteringWithNoTilesetNamedIsReported()
        {
            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flight").intValue =
                    (int)DimensionProjectileFlight.ArcsLikeArtillery;
                serialized.FindProperty("scattersTilesOnTheWayDown").boolValue = true;
            });

            DimensionProjectileGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("bare ground")));
            Assert.IsTrue(projectile.ScattersOrLandsWithoutNamingATileset);
        }

        [Test]
        public void ArtilleryFieldsReadBackEmptyOnAStraightShot()
        {
            // A straight shot that once arced must not keep answering artillery questions.
            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("scattersTilesOnTheWayDown").boolValue = true;
                serialized.FindProperty("scatteredTilesetId").stringValue = "Dirt";
            });

            Assert.IsFalse(projectile.ArcsLikeArtillery);
            Assert.IsFalse(projectile.ScattersTilesOnTheWayDown);
            Assert.IsFalse(projectile.ScattersOrLandsWithoutNamingATileset);
        }

        [Test]
        public void ArtilleryStillCarriesTheFourThingsEveryProjectileNeeds()
        {
            Set(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flight").intValue =
                    (int)DimensionProjectileFlight.ArcsLikeArtillery;
            });
            Run();

            GameObject prefab = Load();
            Assert.IsNotNull(prefab.GetComponent<DestroyTimerAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DontSerializeAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DontDropSelfAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<CantBeAttackedAuthoring>());
        }
    }
}
