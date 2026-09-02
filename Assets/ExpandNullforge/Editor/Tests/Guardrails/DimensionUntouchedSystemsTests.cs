using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the systems the framework had never touched at all — the ocarina, nests, traders,
    /// instruments, segmented creatures, and the small roles that make an object part of a base.
    /// </summary>
    /// <remarks>
    /// These are not field-coverage tests. Every one of them pins a behaviour that is invisible
    /// until it is wrong in-game: a listener that hears a melody the game does not have, a nest with
    /// no cap producing forever, a music sheet that plays silence on five of the six instruments.
    /// </remarks>
    public sealed class DimensionUntouchedSystemsTests
    {
        private const string TestRoot = "Assets/NullforgeUntouchedTests";

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

        private static GameObject BuildWorldObject(
            string id,
            System.Action<SerializedObject> write,
            out DimensionWorldObjectGenerationReport report)
        {
            DimensionWorldObjectAsset asset =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty("objectIdentifier").stringValue = id;
                serialized.FindProperty("displayName").stringValue = id;
                write(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                report = DimensionWorldObjectGenerator.Generate(
                    new List<DimensionWorldObjectAsset> { asset },
                    TestRoot,
                    default(DimensionNamingContext));

                return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // ---- the ocarina ----

        [Test]
        public void AnObjectCanListenForAMelodyAndChangeWhenItHearsIt()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testlistener", serialized =>
            {
                SerializedProperty melody = serialized.FindProperty("melodyResponse");
                SerializedProperty tunes = melody.FindPropertyRelative("melodies");
                tunes.arraySize = 1;
                tunes.GetArrayElementAtIndex(0).stringValue = "Dirt2";
                melody.FindPropertyRelative("hearingRange").floatValue = 5f;
                melody.FindPropertyRelative("becomesVariation").intValue = 1;
                melody.FindPropertyRelative("weakensWhenItHears").boolValue = true;
            }, out report);

            AffectObjectWhenMelodyPlayedAuthoring listener =
                prefab.GetComponent<AffectObjectWhenMelodyPlayedAuthoring>();
            Assert.IsNotNull(listener);
            Assert.AreEqual(1, listener.melodyIDList.Count);
            Assert.AreEqual(MelodyID.Dirt2, listener.melodyIDList[0]);
            Assert.AreEqual(5f, listener.hearRange);
            Assert.IsTrue(listener.weakenWhenAffected);
        }

        [Test]
        public void AnObjectListeningForNoRealMelodyIsLeftOrdinary()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testdeaf", serialized =>
            {
                SerializedProperty tunes = serialized
                    .FindProperty("melodyResponse")
                    .FindPropertyRelative("melodies");
                tunes.arraySize = 1;
                tunes.GetArrayElementAtIndex(0).stringValue = "NotATune";
            }, out report);

            Assert.IsNull(
                prefab.GetComponent<AffectObjectWhenMelodyPlayedAuthoring>(),
                "an object that listens for nothing must not be left polling forever");
            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotATune")));
        }

        // ---- nests ----

        [Test]
        public void ANestWithNoCapIsClampedRatherThanLeftToRunForever()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testnest", serialized =>
            {
                SerializedProperty nest = serialized.FindProperty("nest");
                nest.FindPropertyRelative("isANest").boolValue = true;
                SerializedProperty broods = nest.FindPropertyRelative("broods");
                broods.arraySize = 1;
                SerializedProperty brood = broods.GetArrayElementAtIndex(0);
                brood.FindPropertyRelative("objectId").stringValue = "Wood";
                brood.FindPropertyRelative("atMostAtOnce").intValue = 0;
                brood.FindPropertyRelative("minWait").floatValue = 1f;
                brood.FindPropertyRelative("maxWait").floatValue = 2f;
            }, out report);

            SpawnAroundObjectAuthoring spawner =
                prefab.GetComponent<SpawnAroundObjectAuthoring>();
            Assert.IsNotNull(spawner);
            Assert.AreEqual(1, spawner.spawnEntries.Count);
            Assert.GreaterOrEqual(
                spawner.spawnEntries[0].limitNumberSpawned,
                1,
                "a nest with a cap of zero produces until the world cannot hold any more");
        }

        // ---- traders ----

        [Test]
        public void ATraderStocksWhatItWasGivenWithItsUnlockRules()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testtrader", serialized =>
            {
                SerializedProperty trader = serialized.FindProperty("trader");
                trader.FindPropertyRelative("isATrader").boolValue = true;
                SerializedProperty stock = trader.FindPropertyRelative("stock");
                stock.arraySize = 1;
                SerializedProperty good = stock.GetArrayElementAtIndex(0);
                good.FindPropertyRelative("objectId").stringValue = "Wood";
                good.FindPropertyRelative("amount").intValue = 5;
                good.FindPropertyRelative("appearsAfter").enumValueIndex = 3;
            }, out report);

            MerchantAuthoring merchant = prefab.GetComponent<MerchantAuthoring>();
            Assert.IsNotNull(merchant);
            Assert.AreEqual(1, merchant.items.Count);
            Assert.AreEqual(ObjectID.Wood, merchant.items[0].objectID);
            Assert.AreEqual(5, merchant.items[0].amount);
            Assert.AreEqual(
                MerchantItemRequirement.CoreActivated,
                merchant.items[0].requirementToBeAvailable,
                "stock gates on world progress, per item, so a shop grows as the run does");
        }

        [Test]
        public void ATraderWithAnEmptyShelfIsReported()
        {
            DimensionWorldObjectGenerationReport report;
            BuildWorldObject("testemptyshop", serialized =>
            {
                serialized.FindProperty("trader")
                    .FindPropertyRelative("isATrader").boolValue = true;
            }, out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("nothing on its shelf")));
        }

        // ---- the small roles ----

        /// <remarks>
        /// A CHAIR WITH NO PICTURE IS NOT SITTABLE, and this chair has no picture. Core
        /// Keeper's step that makes an object sittable reaches into the object's picture for the
        /// marker saying where the sitter's body goes and takes the first one by index without
        /// checking there is one — so on a chair with no seat in its picture the whole generate
        /// throws, rather than the chair being unsittable. All thirty of the game's chairs,
        /// stools, thrones, boats and minecarts carry that marker. The answer is now refused on
        /// anything that cannot take it, with a sentence, and this pins that: the tick without a
        /// seat produces no component and does produce a sentence.
        /// </remarks>
        [Test]
        public void AChairWithNoSeatInItsPictureIsToldSoRatherThanBeingMadeSittable()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testchair", serialized =>
            {
                SerializedProperty roles = serialized.FindProperty("roles");
                roles.FindPropertyRelative("canBeSatOn").boolValue = true;
                roles.FindPropertyRelative("canBePainted").boolValue = true;
                roles.FindPropertyRelative("startingPaint").intValue = 3;
                roles.FindPropertyRelative("isADiscovery").boolValue = true;
                roles.FindPropertyRelative("discoveredWithin").floatValue = 8f;
            }, out report);

            Assert.IsNull(
                prefab.GetComponent<SittableAuthoring>(),
                "a chair with nowhere to sit in its picture stops the generate dead when the " +
                "game tries to build the seat, so the answer is refused instead");
            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("can be sat on")),
                "a refused answer has to say so; silence is the defect");
            Assert.AreEqual(3, prefab.GetComponent<PaintToolAuthoring>().paintIndex);
            Assert.AreEqual(8f, prefab.GetComponent<CanBeDiscoveredAuthoring>().distanceToDiscover);
        }

        // ---- machines ----

        [Test]
        public void AnAutomatedMinerChewsTheShapeItWasGiven()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testminer", serialized =>
            {
                SerializedProperty machine = serialized.FindProperty("machineRoles");
                machine.FindPropertyRelative("isAnAutomatedMiner").boolValue = true;
                machine.FindPropertyRelative("bitesFor").intValue = 25;
                machine.FindPropertyRelative("secondsBetweenBites").floatValue = 2f;
                SerializedProperty bite = machine.FindPropertyRelative("chewsTilesAt");
                bite.arraySize = 2;
                bite.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(1, 0);
                bite.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(2, 0);
            }, out report);

            Pug.Automation.AutomatedMinerAuthoring miner =
                prefab.GetComponent<Pug.Automation.AutomatedMinerAuthoring>();
            Assert.IsNotNull(miner);
            Assert.AreEqual(25, miner.damage);
            Assert.AreEqual(
                2,
                miner.damagePositions.Count,
                "the bite is a SHAPE, not a radius — a miner can chew a seam or an L");
            Assert.AreEqual(2, miner.damagePositions[1].x);
        }

        [Test]
        public void AMinerWithNothingToChewIsReported()
        {
            DimensionWorldObjectGenerationReport report;
            BuildWorldObject("testidleminer", serialized =>
            {
                serialized.FindProperty("machineRoles")
                    .FindPropertyRelative("isAnAutomatedMiner").boolValue = true;
            }, out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("never digs")));
        }

        // ---- adaptive looks ----

        [Test]
        public void ALooserRuleAboveAFussierOneIsReported()
        {
            DimensionWorldObjectGenerationReport report;
            BuildWorldObject("testbridge", serialized =>
            {
                SerializedProperty adapts = serialized.FindProperty("adaptsToSurroundings");
                adapts.FindPropertyRelative("changesWithItsSurroundings").boolValue = true;
                SerializedProperty rules = adapts.FindPropertyRelative("rules");
                rules.arraySize = 2;
                rules.GetArrayElementAtIndex(0).FindPropertyRelative("matchesNeeded").intValue = 1;
                rules.GetArrayElementAtIndex(1).FindPropertyRelative("matchesNeeded").intValue = 4;
            }, out report);

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("fussiest rules first")),
                "the first match wins, so a loose rule above a fussy one hides it entirely");
        }
    }
}
