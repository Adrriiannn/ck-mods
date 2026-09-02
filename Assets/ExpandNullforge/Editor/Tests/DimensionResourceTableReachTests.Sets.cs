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
    /// Equipment sets reaching the game table, and being taken back out of it.
    /// </summary>
    public sealed partial class DimensionResourceTableReachTests
    {
        /// <summary>Stands in for a piece of gear this mod adds: a number far above the game's own.</summary>
        private const int ModdedHelm = 12345;

        private const int ModdedChest = 12346;

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
    }
}
