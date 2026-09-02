using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Creatures;
using ExpandNullforge.Skills;
using ExpandNullforge.WorldRules;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the six of Core Keeper's shared tables the framework had never reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THESE ARE GUARDING. Each of the six is written at exactly one moment, and each has one
    /// thing about that moment which is easy to get wrong and impossible to see afterwards.
    /// </para>
    /// <para>
    /// Set bonuses: the game builds two lookups keyed by the ITEM and adds to both with methods that
    /// throw on a repeat, so a piece claimed by two sets is an exception thrown while a world is
    /// being built rather than a cosmetic problem. And numbers must come from the names, because a
    /// number that moves between loads is a number that ends up in a save meaning something else.
    /// </para>
    /// <para>
    /// World events: the game keys its bake by the EVENT and adds with a method that throws on a
    /// repeat, so one event must never end up with two rows however many times it is named; and an
    /// event with no row at all is the game's own way of saying it never happens.
    /// </para>
    /// <para>
    /// The game's own terrain: the generator plays a tile's matching rules back in REVERSE, so a
    /// rule appended at the end of the list would lose to every vanilla rule matching the same tile.
    /// The front of the list is the winning end and the tests say so.
    /// </para>
    /// <para>
    /// Backgrounds, skill pictures and pet colours are all answered where the game asks rather than
    /// written into its assets, so what they must get right is answering for what was claimed and
    /// leaving everything else exactly as the game had it.
    /// </para>
    /// <para>
    /// EVERY TEST HERE FAILS ON AN EMPTY SUBJECT. Each one either registers its own subject first,
    /// or is one of the four written specifically to prove that nothing was registered.
    /// </para>
    /// </remarks>
    public sealed class DimensionResourceTableReachTests
    {
        /// <summary>Stands in for a piece of gear this mod adds: a number far above the game's own.</summary>
        private const int ModdedHelm = 12345;

        private const int ModdedChest = 12346;

        [SetUp]
        public void Setup()
        {
            DimensionSetBonusRegistry.Clear();
            DimensionBackgroundRegistry.Clear();
            DimensionEnvironmentEventRegistry.Clear();
            DimensionWorldTerrainRuleRegistry.Clear();
            DimensionSkillIconRegistry.Clear();
            DimensionPetSkinRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DimensionSetBonusRegistry.Clear();
            DimensionBackgroundRegistry.Clear();
            DimensionEnvironmentEventRegistry.Clear();
            DimensionWorldTerrainRuleRegistry.Clear();
            DimensionSkillIconRegistry.Clear();
            DimensionPetSkinRegistry.Clear();
        }

        // ---- armour sets -------------------------------------------------------------------

        [Test]
        public void SetNumbersFollowTheNamesRatherThanTheOrderTheyArriveIn()
        {
            DimensionSetBonusRegistry.Register("zeta", new[] { "a" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));
            DimensionSetBonusRegistry.Register("alpha", new[] { "b" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));

            Assert.AreEqual(100, DimensionSetBonusRegistry.NumberFor("alpha", 100));
            Assert.AreEqual(101, DimensionSetBonusRegistry.NumberFor("zeta", 100));

            DimensionSetBonusRegistry.Clear();
            DimensionSetBonusRegistry.Register("alpha", new[] { "b" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));
            DimensionSetBonusRegistry.Register("zeta", new[] { "a" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));

            Assert.AreEqual(
                100,
                DimensionSetBonusRegistry.NumberFor("alpha", 100),
                "A set's number moved when the registration order changed. Numbers reach save " +
                "files, so a set that renumbers between loads means something else afterwards.");
            Assert.AreEqual(101, DimensionSetBonusRegistry.NumberFor("zeta", 100));
        }

        [Test]
        public void ASetIsNotQueuedAtAllWithoutBothPiecesAndEffects()
        {
            DimensionSetBonusRegistry.Register("nothing", new string[0], 30, 2, OneLine("X"));
            DimensionSetBonusRegistry.Register(
                "silent", new[] { "a" }, 30, 2, new DimensionSetBonusRegistry.LineRow[0]);

            Assert.IsFalse(
                DimensionSetBonusRegistry.HasAny,
                "A set with no pieces or no effects was queued. It could never give anything, and " +
                "it would take a set number that a real set could have had.");
        }

        [Test]
        public void APieceMayBelongToOneSetOnly()
        {
            List<int> rejected;
            List<int> kept = DimensionSetBonusRegistry.PiecesThatMayBeUsed(
                new List<int> { ModdedHelm, ModdedChest },
                new HashSet<int> { ModdedChest },
                out rejected);

            Assert.AreEqual(1, kept.Count, "A piece another set already holds was kept.");
            Assert.AreEqual(ModdedHelm, kept[0]);
            CollectionAssert.Contains(rejected, ModdedChest);
        }

        [Test]
        public void ASetReachesTheGamesTableWithItsPiecesEffectsTierAndRarity()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>();
                DimensionSetBonusRegistry.Register(
                    "mine",
                    new[] { "MyMod:Helm", "MyMod:Chest" },
                    (int)AreaLevel.Stone,
                    (int)Rarity.Rare,
                    OneLine("PhysicalMeleeDamageIncrease"));

                int appended = DimensionSetBonusRegistry.AppendTo(
                    table, 100, ResolveModdedPiece, ResolveEffect, null);

                Assert.AreEqual(1, appended, "The set did not reach the game's table.");
                Assert.AreEqual(1, table.setBonuses.Count);

                SetBonusInfo info = table.setBonuses[0];
                Assert.AreEqual(100, (int)info.setBonusID);
                Assert.AreEqual(AreaLevel.Stone, info.areaLevel);
                Assert.AreEqual(Rarity.Rare, info.rarity);
                Assert.AreEqual(2, info.availablePieces.Count);
                Assert.AreEqual(1, info.setBonusDatas.Count);
                Assert.AreEqual(2, info.setBonusDatas[0].requiredPieces);
                Assert.AreEqual(
                    0,
                    info.setBonusDatas[0].conditionData.value,
                    "A value was written into a set bonus. The game recomputes every value from " +
                    "the tier and the rarity in UpdateSetBonusDatas, so writing one is a number " +
                    "nobody reads pretending to be a number somebody does.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void ASetAlreadyAppendedIsNotAppendedTwiceByASecondWorld()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>();
                DimensionSetBonusRegistry.Register(
                    "mine", new[] { "MyMod:Helm" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));

                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);
                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);
                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);

                Assert.AreEqual(
                    1,
                    table.setBonuses.Count,
                    "The table is loaded once and kept for the session, and the system that reads " +
                    "it runs again for every world. Appending twice would give one set two rows, " +
                    "and the game's own item lookup throws on the second.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void AModReloadDoesNotLeaveTheGamesTableWithTwoCopiesOfOneSet()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>();
                DimensionSetBonusRegistry.Register(
                    "mine", new[] { "MyMod:Helm" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));
                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);

                // What a mod reload does: this framework's registry is emptied, Core Keeper's table
                // is not, because it is loaded once for the whole process.
                DimensionSetBonusRegistry.Clear();
                DimensionSetBonusRegistry.Register(
                    "mine", new[] { "MyMod:Helm" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));
                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);

                Assert.AreEqual(
                    1,
                    table.setBonuses.Count,
                    "A reload left two rows for one set. The game's item-to-set lookup is built " +
                    "with an add that throws on a repeated item, so the second row takes the whole " +
                    "condition system down as the next world is created.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void APieceTheGamesOwnSetAlreadyHoldsIsRefusedAndSaidOutLoud()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>
                {
                    new SetBonusInfo
                    {
                        setBonusID = SetBonusID.IronArmorSet,
                        availablePieces = new List<ObjectID> { (ObjectID)ModdedHelm },
                        setBonusDatas = new List<SetBonusData>()
                    }
                };

                DimensionSetBonusRegistry.Register(
                    "mine", new[] { "MyMod:Helm" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));

                List<string> said = new List<string>();
                int appended = DimensionSetBonusRegistry.AppendTo(
                    table, 100, ResolveModdedPiece, ResolveEffect, said.Add);

                Assert.AreEqual(
                    0,
                    appended,
                    "A set whose only piece is already claimed was added anyway. The game's own " +
                    "item lookup adds with a method that throws on a repeat, so that is an " +
                    "exception while a world loads.");
                Assert.IsNotEmpty(
                    said,
                    "The clash was refused in silence. Nothing else in the process would say why " +
                    "the set never appeared.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        /// <summary>
        /// The strip takes away this framework's own rows and leaves another mod's alone.
        /// </summary>
        /// <remarks>
        /// The strip used to remove every row carrying a set number this framework had handed out.
        /// The first free number is the highest <c>SetBonusID</c> plus one, which is the number any
        /// other mod appending a set picks by exactly the same reasoning — so a second mod's set,
        /// written at the same number after ours, was deleted by our next append, and the comment
        /// beside the strip said another mod's sets were safe.
        /// </remarks>
        [Test]
        public void TheStripTakesAwayOurOwnRowsAndNotAnotherModsWithTheSameNumber()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>();
                DimensionSetBonusRegistry.Register(
                    "mine", new[] { "MyMod:Helm" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));
                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);
                Assert.AreEqual(1, table.setBonuses.Count);

                // Another mod appends after us and reasons about the free number exactly as we do,
                // so it lands on the same one.
                SetBonusInfo theirs = new SetBonusInfo
                {
                    setBonusID = (SetBonusID)100,
                    areaLevel = AreaLevel.Stone,
                    rarity = Rarity.Rare,
                    availablePieces = new List<ObjectID> { (ObjectID)999999 },
                    setBonusDatas = new List<SetBonusData>()
                };
                table.setBonuses.Add(theirs);

                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);

                CollectionAssert.Contains(
                    table.setBonuses,
                    theirs,
                    "Another mod's set was deleted because it had picked the same free number we " +
                    "did. Nothing about that row is ours.");
                Assert.AreEqual(
                    2,
                    table.setBonuses.Count,
                    "Our own row was written twice, or theirs went missing.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        /// <summary>
        /// Switching the armour-set block off and loading again takes the sets away.
        /// </summary>
        /// <remarks>
        /// The strip used to sit behind an early return on an empty row list, and the patch behind
        /// an earlier one on "nothing is registered", so the one load that has nothing to append is
        /// the load that could not clean up. A creator who unticked "Add sets" kept them, in a
        /// table nothing in the registry could still explain.
        /// </remarks>
        [Test]
        public void SwitchingTheBlockOffAndLoadingAgainTakesTheSetsAway()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>();
                DimensionSetBonusRegistry.Register(
                    "mine", new[] { "MyMod:Helm" }, 30, 2, OneLine("PhysicalMeleeDamageIncrease"));
                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);
                Assert.AreEqual(1, table.setBonuses.Count);
                Assert.IsTrue(DimensionSetBonusRegistry.AnythingWasWrittenBefore);

                // The reload with the block switched off: the registry empties and nothing is
                // registered in its place.
                DimensionSetBonusRegistry.Clear();
                DimensionSetBonusRegistry.AppendTo(table, 100, ResolveModdedPiece, ResolveEffect, null);

                Assert.AreEqual(
                    0,
                    table.setBonuses.Count,
                    "The sets stayed in the game's table after the block that made them was " +
                    "switched off. Nothing left in the mod explains them and nothing takes them " +
                    "away for the rest of the session.");
                Assert.IsFalse(
                    DimensionSetBonusRegistry.AnythingWasWrittenBefore,
                    "The framework still thinks it has rows in the table, so a plain game would " +
                    "keep loading the table to strip nothing.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        // ---- backgrounds -------------------------------------------------------------------

        [Test]
        public void AStarterKitStopsAtTheTwoLinesTheCreationScreenHas()
        {
            Assert.AreEqual(2, DimensionBackgroundRegistry.ItemsThatWillFit(3));
            Assert.AreEqual(2, DimensionBackgroundRegistry.ItemsThatWillFit(2));
            Assert.AreEqual(1, DimensionBackgroundRegistry.ItemsThatWillFit(1));
            Assert.AreEqual(0, DimensionBackgroundRegistry.ItemsThatWillFit(0));
        }

        [Test]
        public void ChangingABackgroundAnswersForItAndLeavesEveryOtherAlone()
        {
            DimensionBackgroundRegistry.Register(
                (int)DimensionBackground.Miner,
                "Crafting",
                new[] { "MyMod:Helm" },
                new[] { 4 },
                new[] { 0 });

            RolePerksTable.Perks ours;
            Assert.IsTrue(
                DimensionBackgroundRegistry.TryAnswer(
                    (int)DimensionBackground.Miner,
                    TheGamesOwnChef(),
                    ResolveModdedPiece,
                    null,
                    out ours),
                "The background this mod changed was not answered for.");
            Assert.AreEqual(SkillID.Crafting, ours.starterSkill);
            Assert.AreEqual(1, ours.starterItems.Count);
            Assert.AreEqual(4, ours.starterItems[0].amount);

            RolePerksTable.Perks untouched;
            Assert.IsFalse(
                DimensionBackgroundRegistry.TryAnswer(
                    (int)DimensionBackground.Chef,
                    TheGamesOwnChef(),
                    ResolveModdedPiece,
                    null,
                    out untouched),
                "A background nobody named was answered for, so the game's own kit would be " +
                "replaced by an empty one.");
        }

        /// <summary>
        /// A row that fills in one half of a background keeps the game's other half.
        /// </summary>
        /// <remarks>
        /// THE ANSWER REPLACES CORE KEEPER'S WHOLE PERKS STRUCT, so a half nobody filled in used to
        /// come back as the default rather than as the game's. <c>default(SkillID)</c> is Mining
        /// and an empty <c>starterItems</c> is an empty bag, so giving Chef a new bag also moved
        /// Chef to Mining, and giving Chef a new skill also took the cooking pot away — in the
        /// granted kit and in the creation-screen preview, with nothing said either way. Both
        /// halves are asserted here because the loss was symmetrical.
        /// </remarks>
        [Test]
        public void ARowThatFillsInOneHalfOfABackgroundKeepsTheGamesOther()
        {
            DimensionBackgroundRegistry.Register(
                (int)DimensionBackground.Chef,
                string.Empty,
                new[] { "MyMod:Helm" },
                new[] { 1 },
                new[] { 0 });

            List<string> said = new List<string>();
            RolePerksTable.Perks bagOnly;
            DimensionBackgroundRegistry.TryAnswer(
                (int)DimensionBackground.Chef, TheGamesOwnChef(), ResolveModdedPiece, said.Add, out bagOnly);

            Assert.AreEqual(
                SkillID.Cooking,
                bagOnly.starterSkill,
                "Changing only the bag moved the background to another skill. Chef starts you in " +
                "Cooking and nothing in the row said otherwise.");
            Assert.AreEqual(1, bagOnly.starterItems.Count, "The listed bag was not used.");
            Assert.AreEqual((int)(ObjectID)ModdedHelm, (int)bagOnly.starterItems[0].objectID);

            DimensionBackgroundRegistry.Clear();
            DimensionBackgroundRegistry.Register(
                (int)DimensionBackground.Chef,
                "Melee",
                new string[0],
                new int[0],
                new int[0]);

            RolePerksTable.Perks skillOnly;
            DimensionBackgroundRegistry.TryAnswer(
                (int)DimensionBackground.Chef, TheGamesOwnChef(), ResolveModdedPiece, said.Add, out skillOnly);

            Assert.AreEqual(SkillID.Melee, skillOnly.starterSkill, "The listed skill was not used.");
            Assert.AreEqual(
                2,
                skillOnly.starterItems.Count,
                "Changing only the skill emptied the bag. Chef starts with a cooking pot and " +
                "eight mushrooms and nothing in the row said otherwise.");
        }

        /// <summary>
        /// Chef as Core Keeper has it: Cooking, a cooking pot and eight mushrooms.
        /// </summary>
        /// <remarks>
        /// Read off <c>MonoBehaviour/RolePerksTable.asset</c> row 3 in the ripped assets — skill 8
        /// (Cooking), object 4013 and eight of 5500 — which is also what
        /// <c>wiki-research/life-skills.md</c> says Chef starts with. It stands in for the answer
        /// <c>RolePerksTable.GetPerks</c> hands the postfix.
        /// </remarks>
        private static RolePerksTable.Perks TheGamesOwnChef()
        {
            return new RolePerksTable.Perks
            {
                role = CharacterRole.Chef,
                starterSkill = SkillID.Cooking,
                starterItems = new List<ObjectData>
                {
                    new ObjectData { objectID = (ObjectID)4013, amount = 1, variation = 0 },
                    new ObjectData { objectID = (ObjectID)5500, amount = 8, variation = 0 }
                }
            };
        }

        /// <summary>
        /// The game's own kit is copied, not handed back, so nothing this mod does writes into it.
        /// </summary>
        /// <remarks>
        /// <c>RolePerksTable</c> is one asset reached as an inspector reference on five prefabs and
        /// never reloaded, so a list added to here would carry the addition into every later
        /// character in the session.
        /// </remarks>
        [Test]
        public void TheGamesOwnStarterKitIsCopiedRatherThanAddedTo()
        {
            RolePerksTable.Perks theirs = TheGamesOwnChef();

            DimensionBackgroundRegistry.Register(
                (int)DimensionBackground.Chef,
                "Melee",
                new[] { "MyMod:Helm" },
                new[] { 1 },
                new[] { 0 });

            RolePerksTable.Perks ours;
            DimensionBackgroundRegistry.TryAnswer(
                (int)DimensionBackground.Chef, theirs, ResolveModdedPiece, null, out ours);

            Assert.AreEqual(
                2,
                theirs.starterItems.Count,
                "The game's own list was written into. It belongs to an asset the game keeps for " +
                "the whole session, so anything put in it stays there.");
            Assert.AreNotSame(theirs.starterItems, ours.starterItems);
        }

        [Test]
        public void AThirdStarterItemIsDroppedRatherThanHandedToAScreenThatCannotDrawIt()
        {
            DimensionBackgroundRegistry.Register(
                (int)DimensionBackground.Fighter,
                "Melee",
                new[] { "MyMod:Helm", "MyMod:Chest", "MyMod:Helm" },
                new[] { 1, 1, 1 },
                new[] { 0, 0, 0 });

            List<string> said = new List<string>();
            RolePerksTable.Perks ours;
            DimensionBackgroundRegistry.TryAnswer(
                (int)DimensionBackground.Fighter,
                TheGamesOwnChef(),
                ResolveModdedPiece,
                said.Add,
                out ours);

            Assert.AreEqual(
                2,
                ours.starterItems.Count,
                "A third starter item survived. The creation screen writes each item into " +
                "roleItemDescs[i] with no bounds check and that list has two slots, so the third " +
                "throws as a player skims onto the background.");
            Assert.IsNotEmpty(said, "The third item was dropped in silence.");
        }

        [Test]
        public void AnUnansweredItemNameIsRetriedRatherThanRememberedAsAbsent()
        {
            DimensionBackgroundRegistry.Register(
                (int)DimensionBackground.Ranger,
                "Range",
                new[] { "MyMod:NotLoadedYet" },
                new[] { 1 },
                new[] { 0 });

            RolePerksTable.Perks first;
            DimensionBackgroundRegistry.TryAnswer(
                (int)DimensionBackground.Ranger,
                TheGamesOwnChef(),
                name => ObjectID.None,
                null,
                out first);
            Assert.AreEqual(0, first.starterItems.Count);

            RolePerksTable.Perks second;
            DimensionBackgroundRegistry.TryAnswer(
                (int)DimensionBackground.Ranger,
                TheGamesOwnChef(),
                name => (ObjectID)ModdedHelm,
                null,
                out second);
            Assert.AreEqual(
                1,
                second.starterItems.Count,
                "A kit built before this mod's content had loaded was remembered. The " +
                "character-creation screen opens long before a world exists, so the first ask can " +
                "always be too early.");
        }

        // ---- the world's own events --------------------------------------------------------

        [Test]
        public void OneEventNeverEndsUpWithTwoRows()
        {
            DimensionEnvironmentEventRegistry.Register(
                (int)EnvironmentEventType.CaveIn, true, new[] { "Stone" },
                0f, 0, 0, false, false, false, 0f, 0f);
            DimensionEnvironmentEventRegistry.Register(
                (int)EnvironmentEventType.CaveIn, true, new[] { "Larva" },
                0f, 0, 0, false, false, false, 0f, 0f);

            List<EnvironmentEventParams> edited =
                DimensionEnvironmentEventRegistry.BuildEditedList(
                    new List<EnvironmentEventParams> { Vanilla(EnvironmentEventType.CaveIn) },
                    ResolveBiome,
                    null);

            Assert.AreEqual(
                1,
                edited.Count,
                "An event got two rows. The game bakes this list into a NativeParallelHashMap " +
                "keyed by the event, and that add throws on a repeat, so the second row is an " +
                "exception while a world is being built.");
            Assert.AreEqual(Biome.Larva, edited[0].biomes[0], "The last row named did not win.");
        }

        [Test]
        public void AnEventNobodyNamedKeepsEveryRequirementTheGameGaveIt()
        {
            DimensionEnvironmentEventRegistry.Register(
                (int)EnvironmentEventType.CaveIn, true, new[] { "Stone" },
                0f, 0, 0, false, false, false, 0f, 0f);

            List<EnvironmentEventParams> edited =
                DimensionEnvironmentEventRegistry.BuildEditedList(
                    new List<EnvironmentEventParams>
                    {
                        Vanilla(EnvironmentEventType.CaveIn),
                        Vanilla(EnvironmentEventType.SpawnLarvas)
                    },
                    ResolveBiome,
                    null);

            Assert.AreEqual(2, edited.Count);
            Assert.AreEqual(EnvironmentEventType.SpawnLarvas, edited[1].eventType);
            Assert.AreEqual(
                90f,
                edited[1].minDistanceFromCore,
                "An event nobody named lost the game's own requirements.");
        }

        [Test]
        public void SwitchingAnEventOffLeavesNoRowAtAll()
        {
            DimensionEnvironmentEventRegistry.Register(
                (int)EnvironmentEventType.CaveIn, false, new[] { "Stone" },
                0f, 0, 0, false, false, false, 0f, 0f);

            List<EnvironmentEventParams> edited =
                DimensionEnvironmentEventRegistry.BuildEditedList(
                    new List<EnvironmentEventParams> { Vanilla(EnvironmentEventType.CaveIn) },
                    ResolveBiome,
                    null);

            Assert.IsEmpty(
                edited,
                "The game's own way of saying an event never happens is to have no row for it: " +
                "the requirement check looks it up first and returns false when it is missing.");
        }

        [Test]
        public void AnEventLeftOnWithNoBiomeSaysSoRatherThanGoingQuietlyDead()
        {
            DimensionEnvironmentEventRegistry.Register(
                (int)EnvironmentEventType.CaveIn, true, new string[0],
                0f, 0, 0, false, false, false, 0f, 0f);

            List<string> said = new List<string>();
            DimensionEnvironmentEventRegistry.BuildEditedList(
                new List<EnvironmentEventParams> { Vanilla(EnvironmentEventType.CaveIn) },
                ResolveBiome,
                said.Add);

            Assert.IsNotEmpty(
                said,
                "An event with an empty biome list can never happen, because the check is a loop " +
                "over that list. Nothing else would tell the creator why their cave-ins stopped.");
        }

        [Test]
        public void ABiomeThisModAddedIsRefusedRatherThanShippedAsARuleThatNeverMatches()
        {
            DimensionEnvironmentEventRegistry.Register(
                (int)EnvironmentEventType.CaveIn, true, new[] { "mymod:emberdeep" },
                0f, 0, 0, false, false, false, 0f, 0f);

            List<string> said = new List<string>();
            List<EnvironmentEventParams> edited =
                DimensionEnvironmentEventRegistry.BuildEditedList(
                    new List<EnvironmentEventParams> { Vanilla(EnvironmentEventType.CaveIn) },
                    DimensionEnvironmentEventRegistry.ResolveBiomeId,
                    said.Add);

            Assert.IsEmpty(
                edited[0].biomes,
                "A biome this mod added was written into a world event's rule. The biome the check " +
                "compares against comes from BiomeLookup.GetBiome, which reads a byte per sampled " +
                "tile or a fixed list indexed by the biome, and nothing in the framework writes " +
                "either — so it could never match, silently.");
            Assert.IsNotEmpty(said, "The refusal was silent.");
        }

        [Test]
        public void OnlyTheGamesOwnBiomesAreVisibleToTheEventCheck()
        {
            Assert.IsTrue(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("Stone"));
            Assert.IsTrue(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("Excavation"));
            Assert.IsFalse(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("None"),
                "Biome.None matches no player standing anywhere, so offering it as a biome would " +
                "be offering a rule that never fires.");
            Assert.IsFalse(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("mymod:emberdeep"));
            Assert.IsFalse(DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome(""));
        }

        /// <summary>
        /// A biome written as a number, or as one of the names that is not a biome, is refused too.
        /// </summary>
        /// <remarks>
        /// <c>Enum.TryParse</c> takes a number as readily as a name, so the refusal that the whole
        /// custom-biome correction rests on was walked past by typing 1000 instead of a name — and
        /// 1000 is the shape a framework biome id takes. It also accepted the enum's own end marker
        /// and the two names Core Keeper marks obsolete and its generator never lays.
        /// </remarks>
        [Test]
        public void ABiomeWrittenAsANumberOrAsAnObsoleteNameIsRefusedLikeAnyOtherStranger()
        {
            Assert.IsFalse(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("1000"),
                "A biome id written as a number walked past the refusal. That is exactly the " +
                "shape a biome this mod added has, and the rule it produces never matches.");
            Assert.IsFalse(DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("3"));
            Assert.IsFalse(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("__MAX_VALUE__"),
                "The enum's own end marker is not a biome a player can stand in.");
            Assert.IsFalse(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("Obsidian"),
                "Obsidian is marked 'Not used in full release world generation' in the game's own " +
                "Biome enum, so a rule aimed at it can never fire.");
            Assert.IsFalse(
                DimensionEnvironmentEventRegistry.TheEventCheckCanSeeThisBiome("GreatWall"),
                "GreatWall carries the same obsolete note as Obsidian.");

            Assert.AreEqual(
                Biome.None,
                DimensionEnvironmentEventRegistry.ResolveBiomeId("1000"),
                "A number came back as a biome rather than as nothing, so it would be written " +
                "into the rule and compared against a value BiomeLookup can never return.");

            // The nine that are real still answer, which is what stops this from being a refusal
            // of everything.
            string[] real =
            {
                "Slime", "Larva", "Stone", "Nature", "Sea", "Desert", "Crystal", "Passage",
                "Excavation"
            };
            for (int i = 0; i < real.Length; i++)
            {
                Assert.AreNotEqual(
                    Biome.None,
                    DimensionEnvironmentEventRegistry.ResolveBiomeId(real[i]),
                    real[i] + " is one of the game's own nine and was refused.");
            }
        }

        // ---- the game's own terrain --------------------------------------------------------

        [Test]
        public void AModsTerrainRuleIsWrittenInFrontOfTheGamesOwn()
        {
            DimensionWorldTerrainRuleRegistry.Register(
                (int)PugWorldGen.CoreKeeper.Biome.Stone,
                0,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                0,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                (int)WorldGen.TileTypeMapping.ResourceIndex.Resource4,
                (int)PugTilemap.TileType.ore,
                1234567);

            List<WorldGen.TileTypeMapping.MappingRule> edited =
                DimensionWorldTerrainRuleRegistry.BuildEditedList(
                    new List<WorldGen.TileTypeMapping.MappingRule>
                    {
                        new WorldGen.TileTypeMapping.MappingRule
                        {
                            outputTile = new WorldGen.TileTypeMapping.MappingResult
                            {
                                tileType = PugTilemap.TileType.ore,
                                tileset = (PugTilemap.Tileset)1
                            }
                        }
                    });

            Assert.AreEqual(2, edited.Count);
            Assert.AreEqual(
                1234567,
                (int)edited[0].outputTile.tileset,
                "A mod's rule was not put in front. The generator pushes matching rules into a " +
                "NativeParallelMultiHashMap in list order and that map enumerates a key's values " +
                "in reverse, so the earliest matching rule is written last and wins. At the back " +
                "of the list a mod's rule loses to every vanilla rule that matched.");
            Assert.AreEqual(1, (int)edited[1].outputTile.tileset);
        }

        [Test]
        public void ATerrainRuleKeepsEveryPartOfItsQuestion()
        {
            DimensionWorldTerrainRuleRegistry.Register(
                (int)PugWorldGen.CoreKeeper.Biome.Clay,
                (int)PugWorldGen.CoreKeeper.TileType.Resource,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                (int)WorldGen.TileTypeMapping.FlagState.True,
                (int)WorldGen.TileTypeMapping.FlagState.False,
                (int)WorldGen.TileTypeMapping.ResourceIndex.Resource5,
                (int)PugTilemap.TileType.wall,
                4242);

            WorldGen.TileTypeMapping.MappingRule rule =
                DimensionWorldTerrainRuleRegistry.BuildEditedList(null)[0];

            Assert.AreEqual(PugWorldGen.CoreKeeper.Biome.Clay, rule.biome);
            Assert.AreEqual(PugWorldGen.CoreKeeper.TileType.Resource, rule.proceduralTileType);
            Assert.AreEqual(WorldGen.TileTypeMapping.FlagState.False, rule.floorFlag);
            Assert.AreEqual(WorldGen.TileTypeMapping.FlagState.True, rule.roofHoleFlag);
            Assert.AreEqual(WorldGen.TileTypeMapping.FlagState.False, rule.greatWallFlag);
            Assert.AreEqual(WorldGen.TileTypeMapping.ResourceIndex.Resource5, rule.resourceIndex);
            Assert.AreEqual(PugTilemap.TileType.wall, rule.outputTile.tileType);
            Assert.AreEqual(4242, (int)rule.outputTile.tileset);
        }

        /// <summary>
        /// A rule registered word for word twice is queued once; one that differs is queued again.
        /// </summary>
        /// <remarks>
        /// The generated consumer's <c>Shutdown()</c> clears only its own "already registered"
        /// flag, so a consumer reloaded without the framework reloading re-runs every
        /// <c>Register</c> against a registry nothing emptied. The other five tables replace by key
        /// and survive that; this one appended, so every reload put another copy of every rule at
        /// the front of Core Keeper's table and into the native array the generator keeps per
        /// world. The second half of this test is the objection the old comment raised: two rules
        /// differing only in the block they lay are two rules, and both are kept.
        /// </remarks>
        [Test]
        public void TheSameTerrainRuleTwiceIsOneRuleAndADifferentOneIsTwo()
        {
            for (int i = 0; i < 3; i++)
            {
                DimensionWorldTerrainRuleRegistry.Register(
                    (int)PugWorldGen.CoreKeeper.Biome.Stone, 0, 0, 0, 0,
                    (int)WorldGen.TileTypeMapping.ResourceIndex.Resource4,
                    (int)PugTilemap.TileType.ore,
                    1234567);
            }

            Assert.AreEqual(
                1,
                DimensionWorldTerrainRuleRegistry.PendingCount,
                "Three loads of the same rule left three copies. Nothing looks wrong in the world " +
                "— the same block is laid twice — so the list simply grows for the session.");

            DimensionWorldTerrainRuleRegistry.Register(
                (int)PugWorldGen.CoreKeeper.Biome.Stone, 0, 0, 0, 0,
                (int)WorldGen.TileTypeMapping.ResourceIndex.Resource4,
                (int)PugTilemap.TileType.ore,
                7654321);

            Assert.AreEqual(
                2,
                DimensionWorldTerrainRuleRegistry.PendingCount,
                "Two rules that lay different blocks were treated as one rule. They are not: " +
                "every matching rule puts its answer down.");
        }

        // ---- skill pictures ----------------------------------------------------------------

        [Test]
        public void HalfAClaimKeepsTheGamesOtherHalf()
        {
            Sprite mine = Sprite.Create(
                new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            Sprite theirGold = Sprite.Create(
                new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                DimensionSkillIconRegistry.Register(SkillID.Mining, mine, null);

                SkillIcon theirs = new SkillIcon
                {
                    skillID = SkillID.Mining,
                    icon = null,
                    goldIcon = theirGold
                };

                SkillIcon ours;
                Assert.IsTrue(DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, theirs, out ours));
                Assert.AreSame(mine, ours.icon);
                Assert.AreSame(
                    theirGold,
                    ours.goldIcon,
                    "A mod that repainted only the ordinary picture blanked the game's gold one.");

                SkillIcon untouched;
                Assert.IsFalse(
                    DimensionSkillIconRegistry.TryAnswer(SkillID.Fishing, theirs, out untouched),
                    "A skill nobody claimed was answered for.");
            }
            finally
            {
                Object.DestroyImmediate(mine);
                Object.DestroyImmediate(theirGold);
            }
        }

        /// <summary>
        /// The answer for one skill is built once rather than on every ask.
        /// </summary>
        /// <remarks>
        /// <c>SkillUIElement.LateUpdate</c> asks <c>GetIcon</c> once or twice per element per
        /// frame, so a fresh <c>SkillIcon</c> per answer was an allocation per claimed skill per
        /// frame for as long as the skill window was open. The game's answer for one skill is the
        /// same object every time — it comes out of a serialized list — so the built answer is kept
        /// beside it.
        /// </remarks>
        [Test]
        public void TheAnswerForOneSkillIsBuiltOnceRatherThanOnEveryAsk()
        {
            Sprite mine = Sprite.Create(
                new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                DimensionSkillIconRegistry.Register(SkillID.Mining, mine, null);
                SkillIcon theirs = new SkillIcon { skillID = SkillID.Mining };

                SkillIcon first;
                SkillIcon second;
                DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, theirs, out first);
                DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, theirs, out second);

                Assert.AreSame(
                    first,
                    second,
                    "A second ask built a second answer. This is asked from LateUpdate, so that " +
                    "is one allocation per claimed skill per frame while the window is open.");

                // A different icon from the game is a different question and is answered afresh.
                SkillIcon other = new SkillIcon { skillID = SkillID.Mining };
                SkillIcon third;
                DimensionSkillIconRegistry.TryAnswer(SkillID.Mining, other, out third);
                Assert.AreNotSame(first, third);
            }
            finally
            {
                Object.DestroyImmediate(mine);
            }
        }

        /// <summary>
        /// A skill written as a number is refused rather than registered against NUM_SKILLS.
        /// </summary>
        /// <remarks>
        /// <c>Enum.TryParse("12")</c> on <c>SkillID</c> comes back as <c>NUM_SKILLS</c>, which is
        /// the count of the skills rather than one of them, so the picture registered against it
        /// was one nothing would ever ask for and the spelling complaint was never printed. Checked
        /// through <c>AttachFrom</c> because that is the only caller that turns a written name into
        /// a skill.
        /// </remarks>
        [Test]
        public void ASkillWrittenAsANumberIsRefusedRatherThanRegisteredAgainstTheCount()
        {
            // NUM_SKILLS is 12 and is not a skill. Nothing ever asks GetIcon for it, so a picture
            // registered against it is a control that reaches nothing, and the spelling complaint
            // that would have told the creator so is never printed.
            Assert.AreEqual(
                12,
                (int)SkillID.NUM_SKILLS,
                "The game's skill count moved, so the number this test uses has to move too.");

            SkillID parsed;
            Assert.IsFalse(
                DimensionSkillIconRegistry.TheGamesOwnSkill("12", out parsed),
                "A skill written as a number was taken as a skill. Enum.TryParse answers 12 with " +
                "NUM_SKILLS, which is the count of the skills rather than one of them.");
            Assert.IsFalse(
                DimensionSkillIconRegistry.TheGamesOwnSkill("NUM_SKILLS", out parsed),
                "NUM_SKILLS is the count, not a skill.");
            Assert.IsFalse(DimensionSkillIconRegistry.TheGamesOwnSkill("Woodcutting", out parsed));
            Assert.IsFalse(DimensionSkillIconRegistry.TheGamesOwnSkill("", out parsed));

            string[] real =
            {
                "Mining", "Running", "Melee", "Vitality", "Crafting", "Range",
                "Gardening", "Fishing", "Cooking", "Magic", "Summoning", "Explosives"
            };
            for (int i = 0; i < real.Length; i++)
            {
                Assert.IsTrue(
                    DimensionSkillIconRegistry.TheGamesOwnSkill(real[i], out parsed),
                    real[i] + " is one of the game's own twelve and was refused.");
                Assert.AreEqual(i, (int)parsed, real[i] + " answered the wrong skill.");
            }
        }

        [Test]
        public void ARowWithNoPictureAtAllClaimsNothing()
        {
            DimensionSkillIconRegistry.Register(SkillID.Cooking, null, null);
            Assert.IsFalse(
                DimensionSkillIconRegistry.HasAny,
                "An empty row claimed a skill, which would draw the square blank rather than " +
                "leave the game's own picture on it.");
        }

        // ---- pet colours -------------------------------------------------------------------

        [Test]
        public void APetWithNoColoursIsNotClaimed()
        {
            DimensionPetSkinRegistry.Register("MyMod:Sprout", new GradientMapDataBlock[0]);
            DimensionPetSkinRegistry.Register("MyMod:Sprout", null);

            Assert.IsFalse(
                DimensionPetSkinRegistry.HasAny,
                "A pet with no colours was claimed, which would answer the converter with an " +
                "empty skin list instead of letting the game's own null through.");
        }

        /// <summary>
        /// A pet id written without the mod in front of it still finds the pet.
        /// </summary>
        /// <remarks>
        /// The generator stamps a creature's object name with the mod in front of it, and that is
        /// the only key <c>API.Authoring.GetObjectID</c> answers to. Every other reader of a mob id
        /// qualifies it while generating; this one is read off the template at load. The wizard
        /// seeds the id already qualified, so the ordinary flow worked — and a creator who typed a
        /// plain id, which every other feature accepts, got a pet that still converted with no
        /// skins, which is the bug this table was opened up to fix.
        /// </remarks>
        [Test]
        public void APetIdWrittenWithoutTheModInFrontOfItStillFindsThePet()
        {
            GradientMapDataBlock colour = ScriptableObject.CreateInstance<GradientMapDataBlock>();
            try
            {
                DimensionPetSkinRegistry.Register("fluff", new[] { colour }, "MyMod");

                PetInfosTable.PetSkinInfo info;
                Assert.IsTrue(
                    DimensionPetSkinRegistry.TryAnswer(
                        (ObjectID)ModdedHelm,
                        name => string.Equals(name, "MyMod:fluff", System.StringComparison.Ordinal)
                            ? (ObjectID)ModdedHelm
                            : ObjectID.None,
                        out info),
                    "A pet whose id was written without the mod in front of it was not found, so " +
                    "it converts with no skins at all — silently, with the patch reporting that " +
                    "it fired.");
                Assert.AreEqual(1, info.skins.Count);

                // A name that was already qualified is asked for exactly as written and nothing is
                // stuck in front of it twice.
                DimensionPetSkinRegistry.Clear();
                DimensionPetSkinRegistry.Register("MyMod:fluff", new[] { colour }, "MyMod");

                PetInfosTable.PetSkinInfo already;
                Assert.IsTrue(
                    DimensionPetSkinRegistry.TryAnswer(
                        (ObjectID)ModdedHelm,
                        name => string.Equals(name, "MyMod:fluff", System.StringComparison.Ordinal)
                            ? (ObjectID)ModdedHelm
                            : ObjectID.None,
                        out already),
                    "An id that already carried the mod's name was mangled.");
            }
            finally
            {
                Object.DestroyImmediate(colour);
            }
        }

        [Test]
        public void RegisteringOnePetTwiceReplacesRatherThanStacks()
        {
            GradientMapDataBlock one = ScriptableObject.CreateInstance<GradientMapDataBlock>();
            GradientMapDataBlock two = ScriptableObject.CreateInstance<GradientMapDataBlock>();
            try
            {
                DimensionPetSkinRegistry.Register("MyMod:Sprout", new[] { one });
                DimensionPetSkinRegistry.Register("MyMod:Sprout", new[] { one, two });

                Assert.AreEqual(
                    1,
                    DimensionPetSkinRegistry.Count,
                    "One pet ended up with two claims, so which colours it came in would depend " +
                    "on which claim was found first.");
            }
            finally
            {
                Object.DestroyImmediate(one);
                Object.DestroyImmediate(two);
            }
        }

        // ---- silence is a defect: these fail on an empty subject ------------------------------

        [Test]
        public void AnEmptySetRegistryHandsOutNoNumbersAndAppendsNothing()
        {
            SetBonusesTable table = ScriptableObject.CreateInstance<SetBonusesTable>();
            try
            {
                table.setBonuses = new List<SetBonusInfo>();
                Assert.IsFalse(DimensionSetBonusRegistry.HasAny);
                Assert.AreEqual(-1, DimensionSetBonusRegistry.NumberFor("anything", 100));
                Assert.AreEqual(
                    0,
                    DimensionSetBonusRegistry.AppendTo(
                        table, 100, ResolveModdedPiece, ResolveEffect, null));
                Assert.IsEmpty(table.setBonuses);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void AnEmptyWorldEventRegistryHandsTheGamesOwnListStraightBack()
        {
            List<EnvironmentEventParams> game = new List<EnvironmentEventParams>
            {
                Vanilla(EnvironmentEventType.CaveIn),
                Vanilla(EnvironmentEventType.SpawnLarvas)
            };

            List<EnvironmentEventParams> edited =
                DimensionEnvironmentEventRegistry.BuildEditedList(game, ResolveBiome, null);

            Assert.AreEqual(2, edited.Count);
            Assert.AreEqual(EnvironmentEventType.CaveIn, edited[0].eventType);
            Assert.AreEqual(EnvironmentEventType.SpawnLarvas, edited[1].eventType);
        }

        [Test]
        public void AnEmptyTerrainRegistryAddsNothingInFrontOfTheGamesRules()
        {
            List<WorldGen.TileTypeMapping.MappingRule> game =
                new List<WorldGen.TileTypeMapping.MappingRule>
                {
                    new WorldGen.TileTypeMapping.MappingRule()
                };

            Assert.IsFalse(DimensionWorldTerrainRuleRegistry.HasAny);
            Assert.AreEqual(1, DimensionWorldTerrainRuleRegistry.BuildEditedList(game).Count);
        }

        [Test]
        public void AnEmptyBackgroundRegistryAnswersForNothing()
        {
            Assert.IsFalse(DimensionBackgroundRegistry.HasAny);
            for (int background = 0; background <= 10; background++)
            {
                RolePerksTable.Perks unused;
                Assert.IsFalse(
                    DimensionBackgroundRegistry.TryAnswer(
                        background, TheGamesOwnChef(), ResolveModdedPiece, null, out unused),
                    "Background " + background + " was answered for by an empty registry, so " +
                    "every one of the game's own kits would be replaced by an empty one.");
            }
        }

        // ---- fixtures ------------------------------------------------------------------------

        private static DimensionSetBonusRegistry.LineRow[] OneLine(string effect)
        {
            return new[]
            {
                new DimensionSetBonusRegistry.LineRow
                {
                    EffectName = effect,
                    RequiredPieces = 2,
                    Strength = 1f,
                    Seconds = 0f
                }
            };
        }

        /// <summary>Stands in for the mod's own objects having been given numbers.</summary>
        private static ObjectID ResolveModdedPiece(string name)
        {
            if (string.Equals(name, "MyMod:Helm", System.StringComparison.Ordinal))
            {
                return (ObjectID)ModdedHelm;
            }

            if (string.Equals(name, "MyMod:Chest", System.StringComparison.Ordinal))
            {
                return (ObjectID)ModdedChest;
            }

            return ObjectID.None;
        }

        private static ConditionID ResolveEffect(string name)
        {
            ConditionID parsed;
            return System.Enum.TryParse(name, false, out parsed) ? parsed : ConditionID.None;
        }

        private static Biome ResolveBiome(string id)
        {
            Biome parsed;
            return System.Enum.TryParse(id, false, out parsed) ? parsed : Biome.None;
        }

        /// <summary>
        /// One of Core Keeper's own rows, with the distance from the core its cave-in row carries.
        /// </summary>
        /// <remarks>
        /// Measured on <c>Resources/EnvironmentEventsTable.asset</c>: the cave-in row is 90 tiles
        /// from the core. The number is here so a test that claims a row survived untouched is
        /// comparing against something the game really has.
        /// </remarks>
        private static EnvironmentEventParams Vanilla(EnvironmentEventType type)
        {
            return new EnvironmentEventParams
            {
                eventType = type,
                biomes = new List<Biome> { Biome.Stone },
                minDistanceFromCore = 90f,
                tileRequirements = new List<EnvironmentEventTilesRequirement>()
            };
        }
    }
}
