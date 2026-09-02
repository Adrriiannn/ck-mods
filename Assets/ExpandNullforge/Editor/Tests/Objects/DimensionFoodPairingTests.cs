using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.Food;
using NUnit.Framework;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Locks the framework's pairing arithmetic to the game's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS A GOLDEN TEST, NOT A SANITY CHECK. The combiner window promises a creator that the
    /// dish it shows is the dish the pot makes, and a framework that quietly disagreed with the
    /// game would be worse than no preview — a wrong answer given confidently is believed. So every
    /// assertion here compares <see cref="DimensionFoodPairing"/> against <c>CookedFoodCD</c>, the
    /// game's own implementation, over the real ingredient ids rather than invented ones.
    /// </para>
    /// <para>
    /// The truncation guard is tested from the other direction: not that the framework packs the
    /// same way, but that it REFUSES an id the packing would corrupt. That failure is silent in
    /// game, so a test is the only place it can be made loud.
    /// </para>
    /// </remarks>
    public sealed class DimensionFoodPairingTests
    {
        /// <summary>
        /// A spread of real ingredient ids: ordinary plants, meat, fish, both always-leaders, and
        /// the two ends of the golden band.
        /// </summary>
        private static readonly int[] SampleIngredients =
        {
            1645, 5500, 5773, 7900, 7901, 7902, 8003, 8006, 8009, 8012,
            8024, 8033, 8100, 8108, 8110, 9618, 9700, 9703, 9707, 9733,
            9741, 9743,
        };

        [Test]
        public void EveryPairPicksTheSameLeadAsTheGame()
        {
            for (int a = 0; a < SampleIngredients.Length; a++)
            {
                for (int b = 0; b < SampleIngredients.Length; b++)
                {
                    int first = SampleIngredients[a];
                    int second = SampleIngredients[b];
                    Assert.AreEqual(
                        (int)CookedFoodCD.GetPrimaryIngredient((ObjectID)first, (ObjectID)second),
                        DimensionFoodPairing.Primary(first, second),
                        "lead for " + first + " with " + second);
                    Assert.AreEqual(
                        (int)CookedFoodCD.GetSecondaryIngredient((ObjectID)first, (ObjectID)second),
                        DimensionFoodPairing.Secondary(first, second),
                        "second for " + first + " with " + second);
                }
            }
        }

        [Test]
        public void EveryPairPacksTheSameWayAsTheGame()
        {
            for (int a = 0; a < SampleIngredients.Length; a++)
            {
                for (int b = 0; b < SampleIngredients.Length; b++)
                {
                    int first = SampleIngredients[a];
                    int second = SampleIngredients[b];
                    int variation = DimensionFoodPairing.Variation(first, second);
                    Assert.AreEqual(
                        CookedFoodCD.GetFoodVariation((ObjectID)first, (ObjectID)second),
                        variation,
                        "packed pair for " + first + " with " + second);
                    Assert.AreEqual(
                        (int)CookedFoodCD.GetPrimaryIngredientFromVariation(variation),
                        DimensionFoodPairing.PrimaryFromVariation(variation),
                        "lead read back out of " + variation);
                    Assert.AreEqual(
                        (int)CookedFoodCD.GetSecondaryIngredientFromVariation(variation),
                        DimensionFoodPairing.SecondaryFromVariation(variation),
                        "second read back out of " + variation);
                }
            }
        }

        [Test]
        public void APairAlwaysResolvesTheSameWay()
        {
            // The whole preview rests on this: the game seeds two streams from the pair and
            // compares one draw from each, so the answer never varies between worlds or machines.
            for (int i = 0; i < SampleIngredients.Length; i++)
            {
                int first = SampleIngredients[i];
                int second = SampleIngredients[(i + 3) % SampleIngredients.Length];
                int once = DimensionFoodPairing.Primary(first, second);
                for (int again = 0; again < 5; again++)
                {
                    Assert.AreEqual(once, DimensionFoodPairing.Primary(first, second));
                }
            }
        }

        [Test]
        public void OneOfThePairAlwaysLeadsAndTheOtherAlwaysFollows()
        {
            for (int a = 0; a < SampleIngredients.Length; a++)
            {
                for (int b = 0; b < SampleIngredients.Length; b++)
                {
                    int first = SampleIngredients[a];
                    int second = SampleIngredients[b];
                    int lead = DimensionFoodPairing.Primary(first, second);
                    int follow = DimensionFoodPairing.Secondary(first, second);
                    Assert.IsTrue(
                        lead == first || lead == second,
                        "the lead has to be one of the two");
                    if (first != second)
                    {
                        Assert.AreNotEqual(lead, follow, "the same food cannot be both");
                    }
                }
            }
        }

        [Test]
        public void OnlyTheGamesOwnGoldensAlwaysLead()
        {
            Assert.IsTrue(DimensionFoodPairing.AlwaysLeads(8100), "the first golden plant");
            Assert.IsTrue(DimensionFoodPairing.AlwaysLeads(8149), "the last of the golden band");
            Assert.IsTrue(DimensionFoodPairing.AlwaysLeads(9733), "Starlight Nautilus");
            Assert.IsFalse(DimensionFoodPairing.AlwaysLeads(8099), "just below the band");
            Assert.IsFalse(DimensionFoodPairing.AlwaysLeads(8150), "just above the band");

            // The honest half: a mod's objects are numbered from 32768 up, so nothing a mod adds
            // can ever land in the band. This is the wall the combiner window documents rather
            // than pretends around.
            Assert.IsFalse(
                DimensionFoodPairing.AlwaysLeads(32768),
                "the first number a mod can be given");
            Assert.IsFalse(
                DimensionFoodPairing.AlwaysLeads(65535),
                "the last number a dish can remember");
        }

        [Test]
        public void AnIdTooBigToBeRememberedIsRefused()
        {
            Assert.IsTrue(DimensionFoodPairing.FitsInAVariation(0));
            Assert.IsTrue(DimensionFoodPairing.FitsInAVariation(32768), "an ordinary mod object");
            Assert.IsTrue(DimensionFoodPairing.FitsInAVariation(65535), "the last one that fits");
            Assert.IsFalse(DimensionFoodPairing.FitsInAVariation(65536), "one past the wall");
            Assert.IsFalse(DimensionFoodPairing.FitsInAVariation(70000));
        }

        [Test]
        public void AnIdTooBigToBeRememberedComesBackAsSomethingElse()
        {
            // Why the guard has to exist at all: nothing throws, nothing logs — the pair simply
            // comes back naming a different object, and every dish made from it is wrong.
            const int TooBig = 65536 + 8003;
            int variation = DimensionFoodPairing.Variation(TooBig, 5500);
            int readBackFirst = DimensionFoodPairing.HighHalf(variation);
            int readBackSecond = DimensionFoodPairing.LowHalf(variation);
            Assert.IsTrue(
                readBackFirst != TooBig || readBackSecond != TooBig,
                "an id above the wall cannot survive the round trip");
            Assert.IsFalse(DimensionFoodPairing.FitsInAVariation(TooBig));
        }

        [Test]
        public void TheGamesIngredientTableAgreesWithTheGamesObjectIds()
        {
            // The table is measured off the game's prefabs, so every id in it has to be an object
            // the game actually has and every dish it names has to be a cooked food.
            for (int i = 0; i < DimensionFoodCatalog.Count; i++)
            {
                int ingredient = DimensionFoodCatalog.IngredientIdAt(i);
                int dish = DimensionFoodCatalog.MakesDishAt(i);
                Assert.IsTrue(
                    System.Enum.IsDefined(typeof(ObjectID), ingredient),
                    "ingredient " + ingredient + " is not an object the game has");
                Assert.IsTrue(
                    dish >= 9500 && dish <= 9599,
                    "ingredient " + ingredient + " makes " + dish +
                    ", which is not in the game's cooked-food range");
            }
        }

        [Test]
        public void ADishTierIdCarriesTheSuffixTheGameStripsOff()
        {
            // Load-bearing: the game removes a trailing "Rare" or "Epic" from an object's name
            // before it looks up the dish's term, which is how all three qualities share one name.
            Assert.AreEqual("mod:pieRare", DimensionDishAsset.RareItemIdFor("mod:pie"));
            Assert.AreEqual("mod:pieEpic", DimensionDishAsset.EpicItemIdFor("mod:pie"));
            Assert.AreEqual("mod:berryRare", DimensionCookingTemplate.GoldenItemIdFor("mod:berry"));
        }

        [Test]
        public void AGoldenIngredientBorrowsItsWordsRatherThanClaimingItsOwn()
        {
            // The name terms are what turn a dish into "Bombastic Heart Berry Pudding". A golden
            // version must NOT write its own pair, because the game strips the suffix and looks up
            // the base ingredient's words; rows for the golden id would be rows nothing reads.
            List<DimensionLocalizationCsv.Row> rows = new List<DimensionLocalizationCsv.Row>();
            DimensionLocalizationCsv.AddFoodIngredientNameRows(rows, "MyMod:berry", "Berry");
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("FoodAdjectives/MyMod_berry", rows[0].Key);
            Assert.AreEqual("FoodNouns/MyMod_berry", rows[1].Key);

            rows.Clear();
            DimensionLocalizationCsv.AddFoodIngredientNameRows(rows, "MyMod:berryRare", "Golden Berry");
            Assert.AreEqual(0, rows.Count, "a golden version shares its base's words");
        }
    }
}
