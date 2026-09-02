#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.Explosives;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Pins the two objects a bomb is made of, and the ways one can be built that look right and do
    /// nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERY FAILURE THIS COVERS IS SILENT IN GAME. A blast with no behaviour tags falls outside the
    /// damage pass's query and does nothing. A bomb whose trigger was swapped but kept its old fuse
    /// goes off on the countdown instead of when something walks past. A bomb with no
    /// <c>DontDropSelf</c> drops itself back on the floor when it explodes and is free forever.
    /// None of the three logs anything.
    /// </para>
    /// </remarks>
    public sealed class DimensionExplosiveArchetypeTests
    {
        private const string TestRoot = "Assets/NullforgeExplosiveTests";
        private const string BombId = "testcharge";

        private DimensionItemAsset item;

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = BombId;
            serialized.FindProperty("displayName").stringValue = "Test Charge";
            serialized.FindProperty("iconId").stringValue = "1";
            serialized.FindProperty("archetype").enumValueIndex =
                EnumIndexOf(DimensionItemArchetype.Explosive);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("explodes").boolValue = true;
            });
        }

        [TearDown]
        public void Cleanup()
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        // ---- the contract ----

        [Test]
        public void TheBombArchetypeAsksForTheThingsABombNeeds()
        {
            Assert.IsTrue(DimensionItemArchetypeRules.Requires(
                DimensionItemArchetype.Explosive,
                DimensionItemAuthoringComponents.Explosive));
            Assert.IsTrue(DimensionItemArchetypeRules.Requires(
                DimensionItemArchetype.Explosive,
                DimensionItemAuthoringComponents.InventoryItem),
                "a bomb is carried before it is placed");
            Assert.IsTrue(DimensionItemArchetypeRules.Requires(
                DimensionItemArchetype.Explosive,
                DimensionItemAuthoringComponents.Placement),
                "a bomb is placed in the world");

            // Loot would be wrong: vanilla bombs drop nothing when they go off, and Breakable is
            // what drags a loot table in.
            Assert.IsFalse(DimensionItemArchetypeRules.Requires(
                DimensionItemArchetype.Explosive,
                DimensionItemAuthoringComponents.Loot));
            Assert.IsTrue(DimensionItemArchetypeRules.IsWorldPlaced(
                DimensionItemArchetype.Explosive));
        }

        // ---- the bomb ----

        [Test]
        public void ABombIsGeneratedWithEverythingItNeedsToGoOff()
        {
            RunItem();
            GameObject bomb = LoadBomb();

            Assert.IsNotNull(bomb.GetComponent<ExplosiveAuthoring>());
            Assert.IsNotNull(
                bomb.GetComponent<DestroyTimerAuthoring>(),
                "the countdown is what sets a fused bomb off");
            Assert.IsNotNull(
                bomb.GetComponent<HealthAuthoring>(),
                "every way of setting a bomb off runs through its health reaching zero");
            Assert.IsNotNull(
                bomb.GetComponent<DontDropSelfAuthoring>(),
                "without this a bomb drops itself back on the floor when it explodes");
            Assert.IsNotNull(
                bomb.GetComponent<MineableAuthoring>(),
                "without this a pickaxe cannot hit the placed bomb");
            Assert.IsNotNull(bomb.GetComponent<PlaceableObjectAuthoring>());
            Assert.IsNotNull(bomb.GetComponent<InventoryItemAuthoring>());
        }

        [Test]
        public void TheBombNumbersReachTheGame()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("hurtsCreaturesBy").intValue = 437;
                explosive.FindPropertyRelative("breaksTerrainBy").intValue = 1155;
                explosive.FindPropertyRelative("secondsBeforeItGoesOff").floatValue = 4f;
                explosive.FindPropertyRelative("howToughItIs").intValue = 10;
                explosive.FindPropertyRelative("pushback").enumValueIndex =
                    (int)DimensionExplosionPushback.Small;
                explosive.FindPropertyRelative("sparesWhoeverSetItOff").boolValue = true;
                explosive.FindPropertyRelative("theBombTakesTheirSideToo").boolValue = true;
                explosive.FindPropertyRelative("otherBlastsDoNotSetItOff").boolValue = true;
            });
            RunItem();

            GameObject bomb = LoadBomb();
            ExplosiveAuthoring authored = bomb.GetComponent<ExplosiveAuthoring>();
            Assert.AreEqual(437, authored.damage);
            Assert.AreEqual(1155, authored.miningDamage);
            Assert.AreEqual(ExplosionPushbackLevel.Small, authored.explosionPushback);
            Assert.IsTrue(authored.explosionInheritsFaction);
            Assert.IsTrue(authored.bombInheritsFaction);
            Assert.IsTrue(authored.ignoreExploding);
            Assert.AreEqual(
                4f,
                bomb.GetComponent<DestroyTimerAuthoring>().lifetime.GetValueForCurrentPlatform());
            Assert.AreEqual(10, bomb.GetComponent<HealthAuthoring>().maxHealth);
        }

        [Test]
        public void ChangingTheTriggerTakesTheOldOneAway()
        {
            RunItem();
            Assert.IsNotNull(
                LoadBomb().GetComponent<DestroyTimerAuthoring>(),
                "it starts on a countdown");

            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("setOffBy").enumValueIndex =
                    (int)DimensionExplosiveTrigger.SomethingComingClose;
                explosive.FindPropertyRelative("howCloseSomethingHasToCome").floatValue = 1f;
                explosive.FindPropertyRelative("secondsAfterItIsTriggered").floatValue = 0.5f;
            });
            RunItem();

            GameObject bomb = LoadBomb();
            ProximityTriggerAuthoring trigger = bomb.GetComponent<ProximityTriggerAuthoring>();
            Assert.IsNotNull(trigger);
            Assert.AreEqual(1f, trigger.radius);
            Assert.AreEqual(0.5f, trigger.delayTime);
            Assert.IsNull(
                bomb.GetComponent<DestroyTimerAuthoring>(),
                "a leftover countdown would set it off before anything came near");
        }

        [Test]
        public void AWiredBombWaitsForPowerAndNothingElse()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("setOffBy").enumValueIndex =
                    (int)DimensionExplosiveTrigger.GettingPower;
            });
            RunItem();

            GameObject bomb = LoadBomb();
            Assert.IsNotNull(bomb.GetComponent<Pug.Automation.ElectricityAuthoring>());
            Assert.IsNull(bomb.GetComponent<DestroyTimerAuthoring>());
            Assert.IsNull(bomb.GetComponent<ProximityTriggerAuthoring>());
        }

        [Test]
        public void AnItemThatStopsExplodingKeepsNothingOfIt()
        {
            RunItem();
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("explodes").boolValue = false;
            });
            RunItem();

            GameObject bomb = LoadBomb();
            Assert.IsNull(bomb.GetComponent<ExplosiveAuthoring>());
            Assert.IsNull(
                bomb.GetComponent<DestroyTimerAuthoring>(),
                "a leftover countdown would delete the placed item a few seconds later");
        }

        // ---- the blast ----

        [Test]
        public void TheBlastCarriesTheFourThingsTheGameQueriesFor()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("blastReach").floatValue = 2f;
            });
            RunItem();

            GameObject blast = LoadBlast();
            Assert.IsNotNull(blast, "the bomb's blast is written beside it");
            Assert.IsNotNull(blast.GetComponent<ExplosionAuthoring>());
            Assert.IsNotNull(
                blast.GetComponent<BehaviourTagsAuthoring>(),
                "without behaviour tags the blast is never seen by the damage pass");
            Assert.IsNotNull(blast.GetComponent<ObjectAuthoring>());
            Assert.IsNotNull(
                blast.GetComponent<DestroyTimerAuthoring>(),
                "a blast with no lifetime never stops existing");
            Assert.IsNotNull(blast.GetComponent<DontSerializeAuthoring>());
            Assert.IsNotNull(blast.GetComponent<DontDropSelfAuthoring>());
            Assert.AreEqual(2f, blast.GetComponent<ExplosionAuthoring>().radius);
        }

        /// <summary>
        /// Reach is the ONLY number on the blast, because it is the only one nothing overwrites.
        /// </summary>
        [Test]
        public void TheBlastCarriesNoDamageNumbersOfItsOwn()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("hurtsCreaturesBy").intValue = 437;
                explosive.FindPropertyRelative("breaksTerrainBy").intValue = 1155;
            });
            RunItem();

            ExplosionAuthoring blast = LoadBlast().GetComponent<ExplosionAuthoring>();
            Assert.AreEqual(0, blast.damage);
            Assert.AreEqual(0, blast.tileDamage);
        }

        [Test]
        public void ABombThatNamesItsOwnBlastGetsNoneMade()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("explosionObjectId").stringValue = "Explosion";
            });
            RunItem();

            Assert.IsNull(LoadBlast(), "the author named a blast, so none is generated");
            Assert.AreEqual(
                ObjectID.Explosion,
                LoadBomb().GetComponent<ExplosiveAuthoring>().explosionID);
        }

        // ---- fire ----

        [Test]
        public void AShortPatchOfFireIsMarkedOnBothHalves()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("leavesBehind").enumValueIndex =
                    (int)DimensionBlastLeavesBehind.AShortPatchOfFire;
            });
            RunItem();

            DimensionBlastFireAuthoring fire =
                LoadBlast().GetComponent<DimensionBlastFireAuthoring>();
            Assert.IsNotNull(fire, "the blast is what spawns the fire");
            Assert.AreEqual(1, fire.napalmVariation, "1 is the game's short patch");

            // The bomb's own flag only matters when the PLAYER's gear rolled the napalm chance, and
            // the two must not be able to disagree about which patch this bomb leaves.
            Assert.IsTrue(LoadBomb().GetComponent<ExplosiveAuthoring>().useSmallNapalmVariant);
        }

        [Test]
        public void ALongPatchOfFireIsTheOtherVariation()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("leavesBehind").enumValueIndex =
                    (int)DimensionBlastLeavesBehind.ALongPatchOfFire;
            });
            RunItem();

            Assert.AreEqual(
                0,
                LoadBlast().GetComponent<DimensionBlastFireAuthoring>().napalmVariation);
            Assert.IsFalse(LoadBomb().GetComponent<ExplosiveAuthoring>().useSmallNapalmVariant);
        }

        [Test]
        public void ABlastThatLeavesNothingIsNotMarked()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("leavesBehind").enumValueIndex =
                    (int)DimensionBlastLeavesBehind.ALongPatchOfFire;
            });
            RunItem();
            Assert.IsNotNull(LoadBlast().GetComponent<DimensionBlastFireAuthoring>());

            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("leavesBehind").enumValueIndex =
                    (int)DimensionBlastLeavesBehind.Nothing;
            });
            RunItem();
            Assert.IsNull(
                LoadBlast().GetComponent<DimensionBlastFireAuthoring>(),
                "taking the fire off has to actually stop it burning");
        }

        // ---- what the author is told ----

        [Test]
        public void ABombThatDoesNotExplodeIsRefused()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("explodes").boolValue = false;
            });

            Assert.IsFalse(DimensionItemArchetypeValidator.CanGenerate(item));
        }

        [Test]
        public void ABlastThatReachesNothingIsRefused()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("blastReach").floatValue = 0f;
            });

            Assert.IsFalse(DimensionItemArchetypeValidator.CanGenerate(item));
            Assert.IsTrue(item.Explosive.ExplodesAndReachesNothing);
        }

        [Test]
        public void ACountdownOfZeroSecondsIsRefused()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("secondsBeforeItGoesOff").floatValue = 0f;
            });

            Assert.IsFalse(DimensionItemArchetypeValidator.CanGenerate(item));
        }

        /// <summary>
        /// Reaching further than terrain damage can travel is legal, and worth saying out loud
        /// because nothing in the inspector hints at the game's fixed 4-tile digging block.
        /// </summary>
        [Test]
        public void ReachingFurtherThanItCanDigIsAWarningNotAnError()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("blastReach").floatValue = 12f;
                explosive.FindPropertyRelative("breaksTerrainBy").intValue = 500;
            });

            Assert.IsTrue(item.Explosive.DigsLessFarThanItReaches);
            Assert.IsTrue(DimensionItemArchetypeValidator.CanGenerate(item));

            DimensionItemGenerationReport report = RunItem();
            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("breaks terrain within")));
        }

        [Test]
        public void AProximityBombSaysItIsAlwaysUsedUp()
        {
            SetExplosive(explosive =>
            {
                explosive.FindPropertyRelative("setOffBy").enumValueIndex =
                    (int)DimensionExplosiveTrigger.SomethingComingClose;
            });

            DimensionItemGenerationReport report = RunItem();
            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("always used up")));
        }

        // ---- the wiring nothing else can see ----

        /// <summary>
        /// A bomb's blast id is a name until something turns it into a number, and this holds the
        /// mod entry to naming the system that does.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHAT THIS PROVES IS NARROWER THAN IT LOOKS, and the opposite reading —
        /// "mod-assembly systems are not auto-created" — is wrong. They are. The game builds its worlds after
        /// the mod assembly is in memory and creates every system it finds, into the group its
        /// <c>[UpdateInGroup]</c> names. So this test is not what keeps the feature alive; it keeps
        /// the mod entry's list of what the framework runs complete, which is worth something on
        /// its own and is not the same claim.
        /// </para>
        /// <para>
        /// What does keep it alive is checked in <c>DimensionSystemLivenessTests</c>, off the
        /// class's own attributes, and in game by <c>DimensionSelfAudit</c>. The stakes are worth
        /// restating either way: an unresolved blast is <c>ObjectID.None</c>, and
        /// <c>CreateExplosion</c> returns silently when the object it spawned has no
        /// <c>ExplosionCD</c> (<c>ExplosiveSystem.cs:88-91</c>), so every bomb in the mod fizzles.
        /// </para>
        /// </remarks>
        [Test]
        public void TheSystemThatLinksABombToItsBlastIsActuallyCreated()
        {
            string source = ReadModEntry();
            StringAssert.Contains(
                "GetOrCreateSystemManaged<ExpandNullforge.Explosives." +
                "DimensionExplosiveHydrationSystem>",
                source,
                "Every generated bomb names its blast by name and nothing turns that name into an " +
                "ObjectID. Add world.GetOrCreateSystemManaged<ExpandNullforge.Explosives." +
                "DimensionExplosiveHydrationSystem>(); to BOTH the server and the client blocks of " +
                "ExpandNullforgeModEntry. Until then every custom bomb goes off and does nothing.");
        }

        [Test]
        public void TheSystemThatMakesABlastLeaveFireIsActuallyCreated()
        {
            string source = ReadModEntry();
            StringAssert.Contains(
                "GetOrCreateSystemManaged<ExpandNullforge.Explosives.DimensionBlastFireSystem>",
                source,
                "Blasts marked to leave fire carry DimensionBlastFireAuthoring and nothing reads " +
                "it. Add world.GetOrCreateSystemManaged<ExpandNullforge.Explosives." +
                "DimensionBlastFireSystem>(); to BOTH the server and the client blocks of " +
                "ExpandNullforgeModEntry. Until then 'leaves a patch of fire' does nothing.");
        }

        [Test]
        public void TheBombRegistrationsAreActuallyEmitted()
        {
            StringAssert.Contains(
                "AppendExplosiveRegistrations(builder, template, modName);",
                DimensionFrameworkSourceScanner.ReadPartials("DimensionRuntimeConsumerBootstrapUtility"),
                "DimensionRuntimeConsumerBootstrapUtility.Explosives.cs writes the bomb-to-blast " +
                "rows and nothing calls it. Add AppendExplosiveRegistrations(builder, template, " +
                "modName); beside AppendFoodRegistrations in the shared bootstrap utility.");
        }

        private static string ReadModEntry()
        {
            return DimensionFrameworkSourceScanner.ReadByName("ExpandNullforgeModEntry.cs");
        }

        // ---- helpers ----

        private void SetExplosive(System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(item);
            write(serialized.FindProperty("explosive"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionItemGenerationReport RunItem()
        {
            return DimensionItemGenerator.Generate(
                new List<DimensionItemAsset> { item },
                TestRoot);
        }

        private static GameObject LoadBomb()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + BombId + ".prefab");
        }

        private static GameObject LoadBlast()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                TestRoot + "/" + DimensionExplosiveBlast.BlastIdFor(BombId) + ".prefab");
        }

        /// <summary>
        /// The index Unity's serialized enum uses, which is a POSITION in the enum's value list and
        /// not the value itself. The archetype enum skips numbers, so casting would write the wrong
        /// archetype and the test would pass for the wrong reason.
        /// </summary>
        private static int EnumIndexOf(DimensionItemArchetype archetype)
        {
            System.Array values = System.Enum.GetValues(typeof(DimensionItemArchetype));
            for (int i = 0; i < values.Length; i++)
            {
                if ((DimensionItemArchetype)values.GetValue(i) == archetype)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
#endif
