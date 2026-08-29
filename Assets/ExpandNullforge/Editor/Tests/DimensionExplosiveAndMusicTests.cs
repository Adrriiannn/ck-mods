using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers things that go off, and music that plays near a thing.
    /// </summary>
    /// <remarks>
    /// Both have one mistake that is invisible in the inspector and obvious in play. An explosive
    /// with no explosion object still does its damage, in silence, with nothing to see. A music area
    /// whose start distance is larger than its stop distance can never begin, because a player
    /// walking towards it crosses the stop line first.
    /// </remarks>
    public sealed class DimensionExplosiveAndMusicTests
    {
        private const string TestRoot = "Assets/NullforgeBoomTests";

        private DimensionItemAsset item;
        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeBoomTests");
            }

            item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = "testbomb";
            serialized.FindProperty("displayName").stringValue = "Test Bomb";
            serialized.FindProperty("iconId").stringValue = "1";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject other = new SerializedObject(worldObject);
            other.FindProperty("objectIdentifier").stringValue = "testshrine";
            other.FindProperty("displayName").stringValue = "Test Shrine";
            other.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void Cleanup()
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void SetExplosive(System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(item);
            write(serialized.FindProperty("explosive"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetMusic(System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized.FindProperty("music"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionItemGenerationReport RunItem()
        {
            return DimensionItemGenerator.Generate(
                new List<DimensionItemAsset> { item },
                TestRoot);
        }

        private DimensionWorldObjectGenerationReport RunObject()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        [Test]
        public void SomethingThatDoesNotExplodeCarriesNoExplosive()
        {
            RunItem();
            Assert.IsNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testbomb.prefab")
                    .GetComponent<ExplosiveAuthoring>());
        }

        [Test]
        public void TheExplosiveDialsReachTheGame()
        {
            SetExplosive(delegate(SerializedProperty explosive)
            {
                explosive.FindPropertyRelative("explodes").boolValue = true;
                explosive.FindPropertyRelative("explosionObjectId").stringValue = "Wood";
                explosive.FindPropertyRelative("hurtsCreaturesBy").intValue = 45;
                explosive.FindPropertyRelative("pushback").intValue =
                    (int)DimensionExplosionPushback.Small;
                explosive.FindPropertyRelative("sparesWhoeverSetItOff").boolValue = true;
            });
            RunItem();

            ExplosiveAuthoring boom = AssetDatabase
                .LoadAssetAtPath<GameObject>(TestRoot + "/testbomb.prefab")
                .GetComponent<ExplosiveAuthoring>();
            Assert.IsNotNull(boom);
            Assert.AreEqual(ObjectID.Wood, boom.explosionID);
            Assert.AreEqual(45, boom.damage);
            Assert.AreEqual(ExplosionPushbackLevel.Small, boom.explosionPushback);
            Assert.IsTrue(boom.explosionInheritsFaction);
        }

        /// <summary>
        /// A bomb that names no blast used to be reported as broken. It is now the ordinary case:
        /// the framework writes the blast beside it.
        /// </summary>
        [Test]
        public void ABombThatNamesNoBlastGetsOneMade()
        {
            SetExplosive(delegate(SerializedProperty explosive)
            {
                explosive.FindPropertyRelative("explodes").boolValue = true;
            });

            DimensionItemGenerationReport report = RunItem();

            Assert.IsTrue(item.Explosive.MakesItsOwnBlast);
            Assert.IsFalse(
                report.Warnings.Exists(w => w.Contains("nothing appears")),
                "The blast is generated, so there is nothing to warn about.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testbombBlast.prefab"),
                "The bomb's blast should have been written beside it.");
        }

        [Test]
        public void MusicNearAThingReachesTheGame()
        {
            SetMusic(delegate(SerializedProperty music)
            {
                music.FindPropertyRelative("musicId").stringValue = "BOSS";
                music.FindPropertyRelative("startsWithin").floatValue = 15f;
                music.FindPropertyRelative("stopsBeyond").floatValue = 25f;
            });
            RunObject();

            MusicAreaAuthoring area = AssetDatabase
                .LoadAssetAtPath<GameObject>(TestRoot + "/testshrine.prefab")
                .GetComponent<MusicAreaAuthoring>();
            Assert.IsNotNull(area);
            Assert.AreEqual(MusicRosterType.BOSS, area.musicRosterType);
            Assert.AreEqual(15f, area.startAtDistance);
            Assert.AreEqual(25f, area.stopAtDistance);
        }

        [Test]
        public void MusicThatCanNeverStartIsReported()
        {
            // A player walking towards it crosses the stop line before the start line.
            SetMusic(delegate(SerializedProperty music)
            {
                music.FindPropertyRelative("musicId").stringValue = "BOSS";
                music.FindPropertyRelative("startsWithin").floatValue = 40f;
                music.FindPropertyRelative("stopsBeyond").floatValue = 10f;
            });

            DimensionWorldObjectGenerationReport report = RunObject();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("never begins")));
            Assert.IsTrue(worldObject.Music.CanNeverStart);
        }

        [Test]
        public void MusicTheGameDoesNotHaveIsReportedRatherThanSilentlyIgnored()
        {
            SetMusic(delegate(SerializedProperty music)
            {
                music.FindPropertyRelative("musicId").stringValue = "NotAnyMusic";
            });

            DimensionWorldObjectGenerationReport report = RunObject();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotAnyMusic")));
            Assert.IsNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testshrine.prefab")
                    .GetComponent<MusicAreaAuthoring>());
        }
    }
}
