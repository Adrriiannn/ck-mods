using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.WorldRules;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the four rules a mod can change about the game itself, rather than add to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO OF THE FOUR ARE FILES THE GAME READS BEFORE ANY OF OUR CODE RUNS, so the file's exact
    /// text is the whole contract — Core Keeper reads it with Unity's own JSON reader, which
    /// matches field names letter for letter and understands enums only as numbers. A renamed key
    /// or a name written where a number belongs is not an error at load: the field silently stays
    /// at zero, which for a loot table means "catches nothing". So the tests assert the exact text
    /// rather than that a file exists.
    /// </para>
    /// <para>
    /// THE OTHER TWO EDIT SOMETHING THE GAME OWNS AND KEEPS. The upgrade cost table is a single
    /// shared asset that lives for the whole session, so the test that matters most is the one
    /// proving the original comes back untouched — a mod that edited it in place would carry its
    /// prices into a vanilla world opened later in the same sitting, with nothing left holding the
    /// real numbers.
    /// </para>
    /// </remarks>
    public sealed class DimensionWorldRulesTests
    {
        private const string TestRoot = "Assets/NullforgeWorldRuleTests";

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);
        }

        [TearDown]
        public void Cleanup()
        {
            DimensionUpgradeCostRegistry.Clear();
            DimensionPlayerOverrideRegistry.Clear();
            DimensionTestScratchFolder.Remove(TestRoot);
        }

        // -------------------------------------------------------- the settings files ---

        private static DimensionWorldRulesGenerationReport Run(
            string id,
            System.Action<SerializedObject> write)
        {
            DimensionGameSetupAsset asset =
                ScriptableObject.CreateInstance<DimensionGameSetupAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty("setupIdentifier").stringValue = id;
                serialized.FindProperty("displayName").stringValue = id;
                serialized.FindProperty("enabled").boolValue = true;
                write(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                return DimensionWorldRulesGenerator.Generate(
                    new List<DimensionGameSetupAsset> { asset },
                    TestRoot);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static string ReadFile(string relativePath)
        {
            TextAsset written =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TestRoot + "/" + relativePath);
            return written == null ? null : written.text;
        }

        [Test]
        public void FishingByBiomeIsWrittenInTheGamesOwnFileShape()
        {
            Run("rules", serialized =>
            {
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;
                SerializedProperty biomes = serialized.FindProperty("fishing.biomes");
                biomes.arraySize = 1;
                SerializedProperty row = biomes.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("biome").stringValue = "Nature";
                row.FindPropertyRelative("fishCaught").stringValue = "NatureFishes";
                row.FindPropertyRelative("junkCaught").stringValue = "NatureFishingLoot";
            });

            string json = ReadFile("Conf/Loot/Fishing/Biome/rules_Nature.json");
            Assert.That(json, Is.Not.Null, "No biome fishing file was written.");
            Assert.That(
                json,
                Is.EqualTo(
                    "{\"biome\":" + (int)Biome.Nature +
                    ",\"junkLoot\":" + (int)LootTableID.NatureFishingLoot +
                    ",\"fishLoot\":" + (int)LootTableID.NatureFishes + "}"),
                "The game reads this file field by field with Unity's own JSON reader; the keys " +
                "and the numeric ids are the contract.");
        }

        [Test]
        public void FishingByWaterIsWrittenInTheGamesOwnFileShape()
        {
            Run("rules", serialized =>
            {
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;
                SerializedProperty waters = serialized.FindProperty("fishing.waters");
                waters.arraySize = 1;
                SerializedProperty row = waters.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("waterGround").stringValue = "Sea";
                row.FindPropertyRelative("fishCaught").stringValue = "SeaFishes";
                row.FindPropertyRelative("junkCaught").stringValue = "SeaFishingLoot";
            });

            string json = ReadFile("Conf/Loot/Fishing/Water/rules_Sea.json");
            Assert.That(json, Is.Not.Null, "No water fishing file was written.");
            Assert.That(
                json,
                Is.EqualTo(
                    "{\"waterType\":" + (int)PugTilemap.Tileset.Sea +
                    ",\"junkLoot\":" + (int)LootTableID.SeaFishingLoot +
                    ",\"fishLoot\":" + (int)LootTableID.SeaFishes + "}"));
        }

        [Test]
        public void ARuleWithNoJunkTableIsRefusedBecauseTheGameWouldReadItAsNoRule()
        {
            // GetFishingStats asks whether a rule exists by testing its JUNK table against Empty,
            // so a rule naming only fish is passed over and the water goes on catching what it
            // always did — the exact shape of failure this framework refuses to ship.
            DimensionWorldRulesGenerationReport report = Run("rules", serialized =>
            {
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;
                SerializedProperty waters = serialized.FindProperty("fishing.waters");
                waters.arraySize = 1;
                SerializedProperty row = waters.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("waterGround").stringValue = "Sea";
                row.FindPropertyRelative("fishCaught").stringValue = "SeaFishes";
                row.FindPropertyRelative("junkCaught").stringValue = string.Empty;
            });

            Assert.That(ReadFile("Conf/Loot/Fishing/Water/rules_Sea.json"), Is.Null);
            Assert.That(report.Warnings.Count, Is.GreaterThan(0));
            Assert.That(report.Warnings[0], Does.Contain("junk"),
                "The warning did not name the missing junk table as the fix.");
        }

        [Test]
        public void AFishFightIsWrittenAsItsTurnsInOrder()
        {
            Run("rules", serialized =>
            {
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;
                SerializedProperty fights = serialized.FindProperty("fishing.fishFights");
                fights.arraySize = 2;

                SerializedProperty pull = fights.GetArrayElementAtIndex(0);
                pull.FindPropertyRelative("fish").stringValue = "AngleFish";
                pull.FindPropertyRelative("pulls").boolValue = true;
                pull.FindPropertyRelative("seconds").floatValue = 1.5f;

                SerializedProperty rest = fights.GetArrayElementAtIndex(1);
                rest.FindPropertyRelative("fish").stringValue = "AngleFish";
                rest.FindPropertyRelative("pulls").boolValue = false;
                rest.FindPropertyRelative("seconds").floatValue = 0.5f;
            });

            string json = ReadFile("Conf/Fishing/rules_AngleFish.json");
            Assert.That(json, Is.Not.Null, "No fish fight file was written.");
            Assert.That(
                json,
                Is.EqualTo(
                    "{\"fish\":" + (int)ObjectID.AngleFish +
                    ",\"struggleData\":[{\"isStruggling\":true,\"time\":1.5}," +
                    "{\"isStruggling\":false,\"time\":0.5}]}"));
        }

        [Test]
        public void AFightWithNoRestingTurnIsRefusedRatherThanWritten()
        {
            DimensionWorldRulesGenerationReport report = Run("rules", serialized =>
            {
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;
                SerializedProperty fights = serialized.FindProperty("fishing.fishFights");
                fights.arraySize = 1;
                SerializedProperty pull = fights.GetArrayElementAtIndex(0);
                pull.FindPropertyRelative("fish").stringValue = "AngleFish";
                pull.FindPropertyRelative("pulls").boolValue = true;
                pull.FindPropertyRelative("seconds").floatValue = 1f;
            });

            Assert.That(ReadFile("Conf/Fishing/rules_AngleFish.json"), Is.Null);
            Assert.That(report.Warnings.Count, Is.GreaterThan(0));
        }

        [Test]
        public void TalentsAreWrittenPerSkillInTheOrderTheyWereListed()
        {
            Run("rules", serialized =>
            {
                serialized.FindProperty("talents.changesWhatTalentsGive").boolValue = true;
                SerializedProperty talents = serialized.FindProperty("talents.talents");
                talents.arraySize = 2;

                SerializedProperty first = talents.GetArrayElementAtIndex(0);
                first.FindPropertyRelative("skill").stringValue = "Mining";
                first.FindPropertyRelative("talentName").stringValue = "Digger";
                first.FindPropertyRelative("gives").stringValue = "MiningIncrease";
                first.FindPropertyRelative("perPoint").intValue = 2;

                SerializedProperty second = talents.GetArrayElementAtIndex(1);
                second.FindPropertyRelative("skill").stringValue = "Mining";
                second.FindPropertyRelative("talentName").stringValue = "Deeper";
                second.FindPropertyRelative("gives").stringValue = "MiningSpeedIncrease";
                second.FindPropertyRelative("perPoint").intValue = 3;
            });

            string json = ReadFile("Conf/Talents/rules_Mining.json");
            Assert.That(json, Is.Not.Null, "No talent file was written.");
            Assert.That(
                json,
                Is.EqualTo(
                    "{\"skill\":" + (int)SkillID.Mining + ",\"talents\":[" +
                    "{\"name\":\"Digger\",\"givesCondition\":" + (int)ConditionID.MiningIncrease +
                    ",\"conditionValuePerPoint\":2}," +
                    "{\"name\":\"Deeper\",\"givesCondition\":" + (int)ConditionID.MiningSpeedIncrease +
                    ",\"conditionValuePerPoint\":3}]}"),
                "The game merges a talent file into its own tree by position, so the order of the " +
                "entries is the contract as much as their contents.");
        }

        [Test]
        public void ASkillTheGameDoesNotHaveIsRefusedRatherThanWritten()
        {
            DimensionWorldRulesGenerationReport report = Run("rules", serialized =>
            {
                serialized.FindProperty("talents.changesWhatTalentsGive").boolValue = true;
                SerializedProperty talents = serialized.FindProperty("talents.talents");
                talents.arraySize = 1;
                SerializedProperty only = talents.GetArrayElementAtIndex(0);
                only.FindPropertyRelative("skill").stringValue = "Woodcutting";
                only.FindPropertyRelative("talentName").stringValue = "Chopper";
                only.FindPropertyRelative("gives").stringValue = "MiningIncrease";
                only.FindPropertyRelative("perPoint").intValue = 1;
            });

            Assert.That(ReadFile("Conf/Talents/rules_Woodcutting.json"), Is.Null);
            Assert.That(report.Warnings.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ARuleSwitchedOffTakesItsSettingsFileAway()
        {
            Run("rules", serialized =>
            {
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;
                SerializedProperty biomes = serialized.FindProperty("fishing.biomes");
                biomes.arraySize = 1;
                SerializedProperty row = biomes.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("biome").stringValue = "Nature";
                row.FindPropertyRelative("fishCaught").stringValue = "NatureFishes";
                row.FindPropertyRelative("junkCaught").stringValue = "NatureFishingLoot";
            });

            Assert.That(
                ReadFile("Conf/Loot/Fishing/Biome/rules_Nature.json"),
                Is.Not.Null,
                "The file has to exist before switching the rule off can be shown to remove it.");

            Run("rules", serialized =>
            {
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = false;
            });

            Assert.That(
                ReadFile("Conf/Loot/Fishing/Biome/rules_Nature.json"),
                Is.Null,
                "A settings file nobody asks for any more would go on changing the game forever.");
        }

        // ------------------------------------------------------- the upgrade prices ---

        private static UpgradeCostsTable MakeTable()
        {
            UpgradeCostsTable table = ScriptableObject.CreateInstance<UpgradeCostsTable>();
            table.upgradeCosts = new List<UpgradeCosts>
            {
                new UpgradeCosts
                {
                    upgradeCost = new List<UpgradeCost>
                    {
                        new UpgradeCost { item = ObjectID.CopperBar, amount = 4 }
                    }
                },
                new UpgradeCosts
                {
                    upgradeCost = new List<UpgradeCost>
                    {
                        new UpgradeCost { item = ObjectID.CopperBar, amount = 8 }
                    }
                }
            };

            return table;
        }

        [Test]
        public void PricingALevelLeavesTheGamesOwnTableUntouched()
        {
            DimensionUpgradeCostRegistry.Clear();
            DimensionUpgradeCostRegistry.Register(1, "IronBar", 12);

            UpgradeCostsTable original = MakeTable();
            UpgradeCostsTable copy = null;
            try
            {
                copy = DimensionUpgradeCostRegistry.BuildEditedCopy(
                    original,
                    DimensionUpgradeCostRegistry.ResolveItemName,
                    null);

                Assert.That(copy, Is.Not.Null);
                Assert.That(copy, Is.Not.SameAs(original));
                Assert.That(
                    copy.upgradeCosts[1].upgradeCost[0].item,
                    Is.EqualTo(ObjectID.IronBar));
                Assert.That(copy.upgradeCosts[1].upgradeCost[0].amount, Is.EqualTo(12));

                Assert.That(
                    original.upgradeCosts[1].upgradeCost.Count,
                    Is.EqualTo(1),
                    "The game's own table is shared for the whole session and never reloaded.");
                Assert.That(
                    original.upgradeCosts[1].upgradeCost[0].item,
                    Is.EqualTo(ObjectID.CopperBar));
                Assert.That(original.upgradeCosts[1].upgradeCost[0].amount, Is.EqualTo(8));
            }
            finally
            {
                Object.DestroyImmediate(original);
                if (copy != null)
                {
                    Object.DestroyImmediate(copy);
                }
            }
        }

        [Test]
        public void ALevelNobodyPricedKeepsTheGamesOwnPrice()
        {
            DimensionUpgradeCostRegistry.Clear();
            DimensionUpgradeCostRegistry.Register(1, "IronBar", 12);

            UpgradeCostsTable original = MakeTable();
            UpgradeCostsTable copy = null;
            try
            {
                copy = DimensionUpgradeCostRegistry.BuildEditedCopy(
                    original,
                    DimensionUpgradeCostRegistry.ResolveItemName,
                    null);

                Assert.That(copy.upgradeCosts[0].upgradeCost[0].item, Is.EqualTo(ObjectID.CopperBar));
                Assert.That(copy.upgradeCosts[0].upgradeCost[0].amount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(original);
                if (copy != null)
                {
                    Object.DestroyImmediate(copy);
                }
            }
        }

        [Test]
        public void EveryIngredientOfOneLevelBecomesThatLevelsWholePrice()
        {
            DimensionUpgradeCostRegistry.Clear();
            DimensionUpgradeCostRegistry.Register(0, "IronBar", 2);
            DimensionUpgradeCostRegistry.Register(0, "AncientCoin", 5);

            UpgradeCostsTable original = MakeTable();
            UpgradeCostsTable copy = null;
            try
            {
                copy = DimensionUpgradeCostRegistry.BuildEditedCopy(
                    original,
                    DimensionUpgradeCostRegistry.ResolveItemName,
                    null);

                Assert.That(copy.upgradeCosts[0].upgradeCost.Count, Is.EqualTo(2));
                Assert.That(copy.upgradeCosts[0].upgradeCost[0].item, Is.EqualTo(ObjectID.IronBar));
                Assert.That(
                    copy.upgradeCosts[0].upgradeCost[1].item,
                    Is.EqualTo(ObjectID.AncientCoin));
            }
            finally
            {
                Object.DestroyImmediate(original);
                if (copy != null)
                {
                    Object.DestroyImmediate(copy);
                }
            }
        }

        [Test]
        public void AnIngredientNothingAnswersToIsDroppedRatherThanPricedAtZero()
        {
            DimensionUpgradeCostRegistry.Clear();
            DimensionUpgradeCostRegistry.Register(0, "NotAThingTheGameHas", 3);

            List<string> reported = new List<string>();
            UpgradeCostsTable original = MakeTable();
            UpgradeCostsTable copy = null;
            try
            {
                copy = DimensionUpgradeCostRegistry.BuildEditedCopy(
                    original,
                    DimensionUpgradeCostRegistry.ResolveItemName,
                    reported.Add);

                Assert.That(reported.Count, Is.EqualTo(1));
                Assert.That(
                    copy.upgradeCosts[0].upgradeCost[0].item,
                    Is.EqualTo(ObjectID.CopperBar),
                    "A level whose only ingredient could not be found keeps the game's price.");
            }
            finally
            {
                Object.DestroyImmediate(original);
                if (copy != null)
                {
                    Object.DestroyImmediate(copy);
                }
            }
        }

        [Test]
        public void NothingIsCopiedWhenNoLevelWasPriced()
        {
            DimensionUpgradeCostRegistry.Clear();

            UpgradeCostsTable original = MakeTable();
            try
            {
                Assert.That(DimensionUpgradeCostRegistry.HasAny, Is.False);
                Assert.That(
                    DimensionUpgradeCostRegistry.BuildEditedCopy(
                        original,
                        DimensionUpgradeCostRegistry.ResolveItemName,
                        null),
                    Is.Null,
                    "With nothing priced the converter must be left pointing at the game's own " +
                    "table, so it costs one boolean check in every world.");
            }
            finally
            {
                Object.DestroyImmediate(original);
            }
        }

        // ------------------------------------------------------ the player overrides ---

        [Test]
        public void ADriftCurveWithNoPointsIsRefused()
        {
            DimensionPlayerOverrideRegistry.Clear();
            DimensionPlayerOverrideRegistry.RegisterVehicleDrift(new float[0], new float[0]);

            Assert.That(
                DimensionPlayerOverrideRegistry.OverridesMovement,
                Is.False,
                "An empty curve answers zero forever, which would stop vehicles drifting rather " +
                "than change how they drift.");
        }

        [Test]
        public void RegisteringATurningDelayIsWhatMakesTheOverrideRun()
        {
            DimensionPlayerOverrideRegistry.Clear();
            Assert.That(DimensionPlayerOverrideRegistry.OverridesMovement, Is.False);

            DimensionPlayerOverrideRegistry.RegisterTurningDelay(0.25f);
            Assert.That(DimensionPlayerOverrideRegistry.OverridesMovement, Is.True);
        }

        [Test]
        public void TheGamesPlayerNumbersComeBackAfterTheWorldHasReadThem()
        {
            DimensionPlayerOverrideRegistry.Clear();
            DimensionPlayerOverrideRegistry.RegisterTurningDelay(0.5f);

            GameObject stand_in = new GameObject("PlayerStandIn");
            try
            {
                PlayerAuthoring authoring = stand_in.AddComponent<PlayerAuthoring>();
                authoring.walkingReorientationDelay = 0.0666667f;

                DimensionPlayerOverrideRegistry.PlayerMovementBackup backup =
                    DimensionPlayerOverrideRegistry.ApplyMovement(authoring);
                Assert.That(authoring.walkingReorientationDelay, Is.EqualTo(0.5f).Within(0.0001f));

                DimensionPlayerOverrideRegistry.RestoreMovement(authoring, backup);
                Assert.That(
                    authoring.walkingReorientationDelay,
                    Is.EqualTo(0.0666667f).Within(0.0001f),
                    "The game's player object stays loaded for the whole session, so leaving it " +
                    "edited would carry this into a vanilla world opened afterwards.");
            }
            finally
            {
                Object.DestroyImmediate(stand_in);
            }
        }

        [Test]
        public void AnythingThatIsNotTheGamesPlayerIsLeftAlone()
        {
            GameObject notThePlayer = new GameObject("NotThePlayer");
            try
            {
                PlayerAuthoring authoring = notThePlayer.AddComponent<PlayerAuthoring>();
                Assert.That(
                    DimensionPlayerOverrideRegistry.IsTheGamesPlayer(authoring),
                    Is.False,
                    "The guard is the object's own id, not the presence of the component.");
            }
            finally
            {
                Object.DestroyImmediate(notThePlayer);
            }
        }
    }
}
