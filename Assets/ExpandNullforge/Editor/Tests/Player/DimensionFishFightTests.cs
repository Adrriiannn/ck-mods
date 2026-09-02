using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.WorldRules;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the one thing about fishing that a settings file cannot carry: a fight for a fish
    /// this mod adds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THESE TESTS ARE GUARDING. A fight reaches a modder's own fish by editing the list Core
    /// Keeper is about to bake, at the single moment between the mod's objects getting their
    /// numbers and the fishing table being frozen. Three things about that moment are easy to get
    /// wrong and impossible to see afterwards, so each has a test of its own: the list belongs to
    /// the mod loader and is kept for the WHOLE SESSION, so writing to it twice must not leave two
    /// fights; a fish already in the list must be replaced rather than shadowed, because the baked
    /// lookup stops at the first match; and a name nothing answers must leave the table exactly as
    /// it was rather than write a fight onto object zero.
    /// </para>
    /// <para>
    /// The split between the two roads is tested from the authoring side as well — one list of
    /// turns in the editor, and each fish going down whichever road can carry it — because a fish
    /// that went down both would have its fight written twice and a fish that went down neither
    /// would silently fight like everything else.
    /// </para>
    /// </remarks>
    public sealed class DimensionFishFightTests
    {
        private const string TestRoot = "Assets/NullforgeFishFightTests";

        /// <summary>
        /// Stands in for a fish this mod adds: a number far above Core Keeper's own, the way a mod
        /// object's number really arrives.
        /// </summary>
        private const ObjectID ModdedFish = (ObjectID)12345;

        [SetUp]
        public void Setup()
        {
            DimensionFishFightRegistry.Clear();
            DimensionTestScratchFolder.Ensure(TestRoot);
        }

        [TearDown]
        public void Cleanup()
        {
            DimensionFishFightRegistry.Clear();
            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private static FishingTable NewTable()
        {
            FishingTable table = ScriptableObject.CreateInstance<FishingTable>();
            table.fishingInfos = new List<FishingTable.FishingInfo>();
            table.fishStruggleInfos = new List<FishingTable.FishStruggleInfo>();
            return table;
        }

        private static System.Func<string, ObjectID> Answers(string name, ObjectID id)
        {
            return candidate => candidate == name ? id : ObjectID.None;
        }

        private static List<DimensionFishFightRegistry.FightTurn> PullThenRest()
        {
            return new List<DimensionFishFightRegistry.FightTurn>
            {
                new DimensionFishFightRegistry.FightTurn { Pulls = true, Seconds = 3f },
                new DimensionFishFightRegistry.FightTurn { Pulls = false, Seconds = 1f }
            };
        }

        // ------------------------------------------------------------ the registry ---

        [Test]
        public void AFishThisModAddsReachesTheTableTheConverterIsAboutToBake()
        {
            FishingTable table = NewTable();
            try
            {
                DimensionFishFightRegistry.Register("MyMod:GlowFish", PullThenRest());
                DimensionFishFightRegistry.ApplyToFishingTable(
                    table, Answers("MyMod:GlowFish", ModdedFish), null);

                Assert.That(table.fishStruggleInfos.Count, Is.EqualTo(1),
                    "The fish's fight did not reach the table.");
                Assert.That(table.fishStruggleInfos[0].fishID, Is.EqualTo(ModdedFish));
                Assert.That(table.fishStruggleInfos[0].struggleData.Count, Is.EqualTo(2));
                Assert.That(table.fishStruggleInfos[0].struggleData[0].isStruggling, Is.True,
                    "The turns did not stay in the order they were written.");
                Assert.That(table.fishStruggleInfos[0].struggleData[0].time, Is.EqualTo(3f));
                Assert.That(table.fishStruggleInfos[0].struggleData[1].isStruggling, Is.False);
                Assert.That(table.fishStruggleInfos[0].struggleData[1].time, Is.EqualTo(1f));
                Assert.That(DimensionFishFightRegistry.Applied,
                    Is.EqualTo(new[] { "MyMod:GlowFish" }),
                    "The log's record of which fish arrived does not match what arrived.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void LoadingASecondWorldInOneSittingDoesNotGiveTheFishTwoFights()
        {
            FishingTable table = NewTable();
            try
            {
                DimensionFishFightRegistry.Register("MyMod:GlowFish", PullThenRest());
                System.Func<string, ObjectID> resolve = Answers("MyMod:GlowFish", ModdedFish);

                DimensionFishFightRegistry.ApplyToFishingTable(table, resolve, null);
                DimensionFishFightRegistry.ApplyToFishingTable(table, resolve, null);
                DimensionFishFightRegistry.ApplyToFishingTable(table, resolve, null);

                Assert.That(table.fishStruggleInfos.Count, Is.EqualTo(1),
                    "The table the mod loader keeps for the whole session grew a fight per world.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void AFightForAFishTheTableAlreadyHasReplacesItRatherThanHidingBehindIt()
        {
            FishingTable table = NewTable();
            try
            {
                // The baked lookup stops at the first entry whose number matches, so a second entry
                // for the same fish would never be reached and the authored fight would be a
                // surface that silently does nothing.
                table.fishStruggleInfos.Add(new FishingTable.FishStruggleInfo
                {
                    fishID = ObjectID.AngleFish,
                    struggleData = new List<FishingTable.FishStruggleData>
                    {
                        new FishingTable.FishStruggleData { isStruggling = true, time = 99f }
                    }
                });

                DimensionFishFightRegistry.Register("AngleFish", PullThenRest());
                DimensionFishFightRegistry.ApplyToFishingTable(
                    table, Answers("AngleFish", ObjectID.AngleFish), null);

                Assert.That(table.fishStruggleInfos.Count, Is.EqualTo(1));
                Assert.That(table.fishStruggleInfos[0].struggleData.Count, Is.EqualTo(2),
                    "The authored fight did not replace the one already in the table.");
                Assert.That(table.fishStruggleInfos[0].struggleData[0].time, Is.EqualTo(3f));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void AFishNothingAnswersIsReportedByNameAndLeavesTheTableAlone()
        {
            FishingTable table = NewTable();
            List<string> reported = new List<string>();
            try
            {
                DimensionFishFightRegistry.Register("MyMod:Typo", PullThenRest());
                DimensionFishFightRegistry.ApplyToFishingTable(
                    table, name => ObjectID.None, reported.Add);

                Assert.That(table.fishStruggleInfos.Count, Is.EqualTo(0),
                    "A fight was written for a fish that does not exist.");
                Assert.That(reported.Count, Is.EqualTo(1));
                Assert.That(reported[0], Does.Contain("MyMod:Typo"),
                    "The report did not name the fish that could not be found.");
                Assert.That(DimensionFishFightRegistry.Applied, Is.Empty,
                    "A fish that never arrived was recorded as having arrived.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void TheTablesOwnLookupIsKeptInStepWithTheListBesideIt()
        {
            FishingTable table = NewTable();
            try
            {
                table.fishStruggleInfosLookUp =
                    new Dictionary<ObjectID, FishingTable.FishStruggleInfo>();

                DimensionFishFightRegistry.Register("MyMod:GlowFish", PullThenRest());
                DimensionFishFightRegistry.ApplyToFishingTable(
                    table, Answers("MyMod:GlowFish", ModdedFish), null);

                Assert.That(table.fishStruggleInfosLookUp.ContainsKey(ModdedFish), Is.True);
                Assert.That(table.fishStruggleInfosLookUp[ModdedFish].struggleData.Count,
                    Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void AFightThatIsAllPullingIsRefusedSoNoFishCanBeHookedForever()
        {
            DimensionFishFightRegistry.Register("MyMod:GlowFish",
                new List<DimensionFishFightRegistry.FightTurn>
                {
                    new DimensionFishFightRegistry.FightTurn { Pulls = true, Seconds = 2f },
                    new DimensionFishFightRegistry.FightTurn { Pulls = true, Seconds = 1f }
                });

            Assert.That(DimensionFishFightRegistry.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void RegisteringOneFishTwiceKeepsTheLastFightRatherThanBoth()
        {
            DimensionFishFightRegistry.Register("MyMod:GlowFish", PullThenRest());
            DimensionFishFightRegistry.Register("MyMod:GlowFish",
                new List<DimensionFishFightRegistry.FightTurn>
                {
                    new DimensionFishFightRegistry.FightTurn { Pulls = true, Seconds = 7f },
                    new DimensionFishFightRegistry.FightTurn { Pulls = false, Seconds = 7f }
                });

            Assert.That(DimensionFishFightRegistry.PendingCount, Is.EqualTo(1));

            FishingTable table = NewTable();
            try
            {
                DimensionFishFightRegistry.ApplyToFishingTable(
                    table, Answers("MyMod:GlowFish", ModdedFish), null);
                Assert.That(table.fishStruggleInfos[0].struggleData[0].time, Is.EqualTo(7f));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void TheTurnsArriveAsTwoRowsOfNumbersTheWayGeneratedCodeCanWriteThem()
        {
            // Generated C# can write numbers and nothing else, so the bootstrap calls the overload
            // taking two plain arrays. It must produce the same fight as the list overload.
            DimensionFishFightRegistry.Register(
                "MyMod:GlowFish", new bool[] { true, false }, new float[] { 3f, 1f });

            FishingTable table = NewTable();
            try
            {
                DimensionFishFightRegistry.ApplyToFishingTable(
                    table, Answers("MyMod:GlowFish", ModdedFish), null);

                Assert.That(table.fishStruggleInfos.Count, Is.EqualTo(1));
                Assert.That(table.fishStruggleInfos[0].struggleData[0].isStruggling, Is.True);
                Assert.That(table.fishStruggleInfos[0].struggleData[1].time, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void TheDifficultyNumberIsDerivedTheWayTheGameDerivesIt()
        {
            // FishingTable.OnValidate: each turn contributes time/turnCount to its side, and the
            // ratio is the larger side over the smaller, negative when resting is the larger.
            FishingTable.FishStruggleInfo info = DimensionFishFightRegistry.BuildStruggleInfo(
                ModdedFish, PullThenRest());

            Assert.That(info.difficultyRatio, Is.EqualTo(3f).Within(0.0001f));

            FishingTable.FishStruggleInfo gentler = DimensionFishFightRegistry.BuildStruggleInfo(
                ModdedFish,
                new List<DimensionFishFightRegistry.FightTurn>
                {
                    new DimensionFishFightRegistry.FightTurn { Pulls = true, Seconds = 1f },
                    new DimensionFishFightRegistry.FightTurn { Pulls = false, Seconds = 4f }
                });

            Assert.That(gentler.difficultyRatio, Is.EqualTo(-4f).Within(0.0001f));
        }

        [Test]
        public void NothingIsWrittenWhenNoFishWasGivenAFight()
        {
            FishingTable table = NewTable();
            try
            {
                DimensionFishFightRegistry.ApplyToFishingTable(
                    table, name => ModdedFish, null);
                Assert.That(table.fishStruggleInfos.Count, Is.EqualTo(0));
                Assert.That(DimensionFishFightRegistry.HasAny, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        // ------------------------------------------ the split between the two roads ---

        private static DimensionGameSetupAsset SetupWithFights(
            string id,
            params string[] fishNames)
        {
            DimensionGameSetupAsset asset = ScriptableObject.CreateInstance<DimensionGameSetupAsset>();
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("setupIdentifier").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("enabled").boolValue = true;
            serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;

            SerializedProperty fights = serialized.FindProperty("fishing.fishFights");
            fights.arraySize = fishNames.Length * 2;
            for (int i = 0; i < fishNames.Length; i++)
            {
                SerializedProperty pull = fights.GetArrayElementAtIndex(i * 2);
                pull.FindPropertyRelative("fish").stringValue = fishNames[i];
                pull.FindPropertyRelative("pulls").boolValue = true;
                pull.FindPropertyRelative("seconds").floatValue = 1.5f;

                SerializedProperty rest = fights.GetArrayElementAtIndex(i * 2 + 1);
                rest.FindPropertyRelative("fish").stringValue = fishNames[i];
                rest.FindPropertyRelative("pulls").boolValue = false;
                rest.FindPropertyRelative("seconds").floatValue = 0.5f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static string ReadFile(string relativePath)
        {
            string path = TestRoot + "/" + relativePath;
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            return asset == null ? null : asset.text;
        }

        [Test]
        public void OnlyTheGamesOwnFishGetASettingsFile()
        {
            DimensionGameSetupAsset asset = SetupWithFights("rules", "AngleFish", "MyModGlowFish");
            try
            {
                DimensionWorldRulesGenerator.Generate(
                    new List<DimensionGameSetupAsset> { asset }, TestRoot);

                Assert.That(ReadFile("Conf/Fishing/rules_AngleFish.json"), Is.Not.Null,
                    "One of the game's own fish lost its settings file.");
                Assert.That(ReadFile("Conf/Fishing/rules_MyModGlowFish.json"), Is.Null,
                    "A fish this mod adds was written to a file that names fish by number, " +
                    "where its number does not exist yet.");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void OnlyTheModsOwnFishAreRegisteredInTheBootstrap()
        {
            DimensionGameSetupAsset asset = SetupWithFights("rules", "AngleFish", "GlowFish");
            DimensionTemplateAsset template = ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            DimensionItemAsset fishItem = ScriptableObject.CreateInstance<DimensionItemAsset>();
            try
            {
                SerializedObject item = new SerializedObject(fishItem);
                item.FindProperty("itemId").stringValue = "GlowFish";
                item.FindProperty("enabled").boolValue = true;
                item.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serialized = new SerializedObject(template);
                SerializedProperty setups = serialized.FindProperty("globalGameSetups");
                setups.arraySize = 1;
                setups.GetArrayElementAtIndex(0).objectReferenceValue = asset;
                SerializedProperty items = serialized.FindProperty("globalItems");
                items.arraySize = 1;
                items.GetArrayElementAtIndex(0).objectReferenceValue = fishItem;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                StringBuilder builder = new StringBuilder();
                DimensionRuntimeConsumerBootstrapUtility.AppendWorldRulesRegistrations(
                    builder, template, "MyMod");
                string emitted = builder.ToString();

                Assert.That(emitted, Does.Contain(
                    "DimensionFishFightRegistry.Register(\"MyMod:GlowFish\", " +
                    "new bool[] { true, false }, new float[] { 1.5f, 0.5f });"),
                    "The mod's own fish was not registered, or not in the turns' own order.");
                Assert.That(emitted, Does.Not.Contain("AngleFish"),
                    "One of the game's own fish was registered as well as written to its file, " +
                    "which would give it two fights.");
            }
            finally
            {
                Object.DestroyImmediate(fishItem);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AModsFishWithAnUnlandableFightIsNotRegistered()
        {
            DimensionGameSetupAsset asset = ScriptableObject.CreateInstance<DimensionGameSetupAsset>();
            DimensionTemplateAsset template = ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty("setupIdentifier").stringValue = "rules";
                serialized.FindProperty("displayName").stringValue = "rules";
                serialized.FindProperty("enabled").boolValue = true;
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = true;
                SerializedProperty fights = serialized.FindProperty("fishing.fishFights");
                fights.arraySize = 1;
                SerializedProperty pull = fights.GetArrayElementAtIndex(0);
                pull.FindPropertyRelative("fish").stringValue = "GlowFish";
                pull.FindPropertyRelative("pulls").boolValue = true;
                pull.FindPropertyRelative("seconds").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject templateSerialized = new SerializedObject(template);
                SerializedProperty setups = templateSerialized.FindProperty("globalGameSetups");
                setups.arraySize = 1;
                setups.GetArrayElementAtIndex(0).objectReferenceValue = asset;
                templateSerialized.ApplyModifiedPropertiesWithoutUndo();

                StringBuilder builder = new StringBuilder();
                DimensionRuntimeConsumerBootstrapUtility.AppendWorldRulesRegistrations(
                    builder, template, "MyMod");

                Assert.That(builder.ToString(), Does.Not.Contain("DimensionFishFightRegistry"),
                    "A fight that never resolves was registered, so the fish could be hooked " +
                    "and never landed.");
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NothingIsRegisteredWhenTheFishingBlockIsSwitchedOff()
        {
            DimensionGameSetupAsset asset = SetupWithFights("rules", "GlowFish");
            DimensionTemplateAsset template = ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty("fishing.changesWhatFishingCatches").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject templateSerialized = new SerializedObject(template);
                SerializedProperty setups = templateSerialized.FindProperty("globalGameSetups");
                setups.arraySize = 1;
                setups.GetArrayElementAtIndex(0).objectReferenceValue = asset;
                templateSerialized.ApplyModifiedPropertiesWithoutUndo();

                StringBuilder builder = new StringBuilder();
                DimensionRuntimeConsumerBootstrapUtility.AppendWorldRulesRegistrations(
                    builder, template, "MyMod");

                Assert.That(builder.ToString(), Does.Not.Contain("DimensionFishFightRegistry"));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
