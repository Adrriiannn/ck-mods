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
    /// What a new character starts with, and the backgrounds offered.
    /// </summary>
    public sealed partial class DimensionResourceTableReachTests
    {
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
    }
}
