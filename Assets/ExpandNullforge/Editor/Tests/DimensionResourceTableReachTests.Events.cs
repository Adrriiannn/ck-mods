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
    /// World events, and the biomes they are allowed to name.
    /// </summary>
    public sealed partial class DimensionResourceTableReachTests
    {
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
    }
}
