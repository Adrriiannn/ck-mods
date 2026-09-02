using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers what attacking sounds like, and what an object leaves on the tile it stood on.
    /// </summary>
    /// <remarks>
    /// Both are the same shape of quiet failure. Attack sounds left at zero do not go silent — they
    /// fall back to the game's, which is right, so the thing to guard is that authoring one writes
    /// it and clearing it removes the component rather than writing five zeroes. The tile outcome
    /// guards the opposite: a tileset name that resolves to nothing must be reported, because the
    /// symptom is an object that breaks and leaves bare ground.
    /// </remarks>
    public sealed class DimensionAttackAndTileOutcomeTests
    {
        private const string TestRoot = "Assets/NullforgeAttackTests";

        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeAttackTests");
            }

            // In memory, not as an asset: a SerializedObject write made right after CreateAsset
            // does not stick, and the test would then run against an item full of defaults.
            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SetString("objectIdentifier", "testpot");
            SetString("displayName", "Test Pot");
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

        private void SetOutcome(string leavesTileset, float chance)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            SerializedProperty outcome = serialized.FindProperty("tileOutcome");
            outcome.FindPropertyRelative("leavesTilesetId").stringValue = leavesTileset;
            outcome.FindPropertyRelative("leavesChance").floatValue = chance;
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testpot.prefab");
        }

        // ---- what it leaves on the tile ----

        [Test]
        public void AVanillaTilesetNameLeavesThatTileBehind()
        {
            SetOutcome("Dirt", 1f);
            Run();

            SpawnTileOnDeathAuthoring spawn = Load().GetComponent<SpawnTileOnDeathAuthoring>();
            Assert.IsNotNull(spawn);
            Assert.AreEqual(PugTilemap.Tileset.Dirt, spawn.tileset);
            Assert.AreEqual(1f, spawn.spawnChance);
        }

        [Test]
        public void ATilesetNothingDefinesIsReportedRatherThanLeavingBareGround()
        {
            SetOutcome("NotATileset", 1f);

            DimensionWorldObjectGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(warning => warning.Contains("NotATileset")));
            Assert.IsNull(Load().GetComponent<SpawnTileOnDeathAuthoring>());
        }

        [Test]
        public void AnObjectThatLeavesNothingCarriesNeitherComponent()
        {
            // Generation is authoritative: clearing the fields has to take the components off.
            SetOutcome("Dirt", 1f);
            Run();
            Assert.IsNotNull(Load().GetComponent<SpawnTileOnDeathAuthoring>());

            SetOutcome(string.Empty, 1f);
            Run();
            Assert.IsNull(Load().GetComponent<SpawnTileOnDeathAuthoring>());
            Assert.IsNull(Load().GetComponent<CrackableTileAuthoring>());
        }

        [Test]
        public void ATileNamedButNeverLeftIsDetectableWithoutGenerating()
        {
            SetOutcome("Dirt", 0f);
            Assert.IsTrue(worldObject.TileOutcome.NamesATileItWillNeverLeave);
            Assert.IsFalse(worldObject.TileOutcome.LeavesATileBehind);
        }

        // ---- what attacking sounds like ----

        [Test]
        public void AuthoredAttackSoundsReachTheGame()
        {
            GameObject root = new GameObject("sounded");
            try
            {
                DimensionAttackSoundsTemplate sounds = new DimensionAttackSoundsTemplate();
                SerializedObject holder = new SerializedObject(worldObject);
                Assert.IsFalse(sounds.HasAnySound, "an untouched template asks for nothing");

                DimensionObjectSpine.ApplyAttackSounds(root, sounds);
                Assert.IsNull(
                    root.GetComponent<CustomAttackSoundAuthoring>(),
                    "silence is the game's own sound, not five zeroes");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AWindUpWithoutASwingIsDetectable()
        {
            // Legal, and almost always an oversight: the sound before the attack was chosen and the
            // attack itself left as the default, so the two do not match.
            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(item);
                serialized
                    .FindProperty("attackSounds")
                    .FindPropertyRelative("windUpSound")
                    .stringValue = "hydraBossBiteAnticipation";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(item.AttackSounds.HasAnySound);
                Assert.IsTrue(item.AttackSounds.WindsUpButSwingsWithTheDefault);
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }
    }
}
