using System.Collections.Generic;
using ExpandNullforge.Plants;
using NUnit.Framework;
using Pug.Properties;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the two decisions Core Keeper does not leave open: which version of a crop a planting
    /// comes up as, and which plant that version's seed has to sprout into.
    /// </summary>
    /// <remarks>
    /// Both are pure functions on purpose. Neither can be watched in a running world without waiting
    /// two watered minutes for a sprout, and a roll that quietly favours the wrong band would take a
    /// few thousand plantings to notice.
    /// </remarks>
    public sealed class DimensionCropTierTests
    {
        private static List<DimensionCropTier> GoldenThenPlatinum()
        {
            // Golden is left to the game, so it takes no share of the framework's own draw; platinum
            // is the framework's, at five in a hundred.
            return new List<DimensionCropTier>
            {
                new DimensionCropTier
                {
                    SeedVariation = 1,
                    PlantVariation = 2,
                    ChancePercent = 3f,
                    UsesTheGamesGoldenRoll = true
                },
                new DimensionCropTier
                {
                    SeedVariation = 2,
                    PlantVariation = 3,
                    ChancePercent = 5f,
                    UsesTheGamesGoldenRoll = false
                }
            };
        }

        // ---- the roll ----

        [Test]
        public void TheSameDrawAlwaysGivesTheSameVersion()
        {
            List<DimensionCropTier> tiers = GoldenThenPlatinum();

            Assert.AreEqual(1, DimensionCropTierRoll.Choose(tiers, 0.01f));
            Assert.AreEqual(1, DimensionCropTierRoll.Choose(tiers, 0.01f));
            Assert.AreEqual(
                DimensionCropTierRoll.NoTier, DimensionCropTierRoll.Choose(tiers, 0.5f));
        }

        [Test]
        public void AVersionsChanceIsItsShareOfAHundredPlantings()
        {
            List<DimensionCropTier> tiers = GoldenThenPlatinum();

            // Five in a hundred means the draw wins below 0.05 and loses at it.
            Assert.AreEqual(1, DimensionCropTierRoll.Choose(tiers, 0.0499f));
            Assert.AreEqual(
                DimensionCropTierRoll.NoTier, DimensionCropTierRoll.Choose(tiers, 0.05f));
        }

        [Test]
        public void VersionsSitEndToEndOnOneDrawRatherThanRollingSeparately()
        {
            // Rolling each one on its own would let two win at once, and would make each printed
            // chance quietly depend on what else is in the list.
            List<DimensionCropTier> tiers = new List<DimensionCropTier>
            {
                new DimensionCropTier { SeedVariation = 1, PlantVariation = 2, ChancePercent = 10f },
                new DimensionCropTier { SeedVariation = 2, PlantVariation = 3, ChancePercent = 10f }
            };

            Assert.AreEqual(0, DimensionCropTierRoll.Choose(tiers, 0.05f));
            Assert.AreEqual(1, DimensionCropTierRoll.Choose(tiers, 0.15f));
            Assert.AreEqual(
                DimensionCropTierRoll.NoTier, DimensionCropTierRoll.Choose(tiers, 0.25f));
        }

        [Test]
        public void AVersionTheGameRollsIsNeverRolledHereAsWell()
        {
            List<DimensionCropTier> tiers = GoldenThenPlatinum();

            // Golden would occupy the first three parts in a hundred if this rolled it, so a draw of
            // 0.01 landing on platinum is the proof that it does not.
            Assert.AreEqual(1, DimensionCropTierRoll.Choose(tiers, 0.01f));
            Assert.AreEqual(5f, DimensionCropTierRoll.TotalChancePercent(tiers), 0.0001f);
        }

        [Test]
        public void AVersionWithNoChanceNeverComesUp()
        {
            List<DimensionCropTier> tiers = new List<DimensionCropTier>
            {
                new DimensionCropTier { SeedVariation = 1, PlantVariation = 2, ChancePercent = 0f }
            };

            Assert.AreEqual(
                DimensionCropTierRoll.NoTier, DimensionCropTierRoll.Choose(tiers, 0f));
        }

        [Test]
        public void NothingToRollForAnswersAnOrdinaryPlanting()
        {
            Assert.AreEqual(DimensionCropTierRoll.NoTier, DimensionCropTierRoll.Choose(null, 0f));
            Assert.AreEqual(
                DimensionCropTierRoll.NoTier,
                DimensionCropTierRoll.Choose(new List<DimensionCropTier>(), 0f));
        }

        // ---- the seed to plant mapping ----

        [Test]
        public void EachVersionsSeedMapsToThatVersionsPlant()
        {
            List<DimensionCropTier> tiers = GoldenThenPlatinum();

            Assert.AreEqual(3, DimensionCropTierRoll.PlantVariationForSeedVariation(tiers, 2));
        }

        [Test]
        public void TheVersionTheGameHandlesIsLeftAlone()
        {
            // PlantsGrowJob already promotes that one pair by itself. Answering a variation here
            // would have the sprout system destroy and rebuild a plant that was already correct.
            List<DimensionCropTier> tiers = GoldenThenPlatinum();

            Assert.AreEqual(0, DimensionCropTierRoll.PlantVariationForSeedVariation(tiers, 1));
        }

        [Test]
        public void AnOrdinaryPlantingAndAnUnknownVariationBothMeanNoReplacement()
        {
            List<DimensionCropTier> tiers = GoldenThenPlatinum();

            Assert.AreEqual(0, DimensionCropTierRoll.PlantVariationForSeedVariation(tiers, 0));
            Assert.AreEqual(
                0,
                DimensionCropTierRoll.PlantVariationForSeedVariation(tiers, 3),
                "The variation an ordinary planting lands on is not a version.");
        }

        // ---- what the converters write ----

        [Test]
        public void TheSeedWritesTheSamePropertiesCoreKeepersOwnSeedConverterWrites()
        {
            Assert.AreEqual(
                new[]
                {
                    "isSeed",
                    "Seed/turnsIntoPlantID",
                    "Seed/rarePlantVariation",
                    "Seed/rareSeedVariation",
                    "Growing/highestStage",
                    "Growing/timeBetweenStages",
                    "Growing/keepDamageReductionWhenRipe"
                },
                DimensionPlantProperties.Seed);
        }

        [Test]
        public void ThePlantWritesTheSamePropertiesCoreKeepersOwnPlantConverterWrites()
        {
            Assert.AreEqual(
                new[]
                {
                    "Growing/highestStage",
                    "Growing/timeBetweenStages",
                    "Growing/keepDamageReductionWhenRipe"
                },
                DimensionPlantProperties.Plant);
        }

        [Test]
        public void EveryPropertyNameHashesToTheNumberTheGrowingJobLooksFor()
        {
            // PlantsGrowJob is Burst-compiled with these as literal integers, so it cannot be
            // patched and it cannot be told a different name. A name spelled one character
            // differently hashes to something else, the job finds nothing, and the seed destroys
            // itself when it ripens without a word in the log.
            Assert.AreEqual(
                -1534320058, Property.StringToHash(DimensionPlantProperties.TurnsIntoPlantId));
            Assert.AreEqual(
                151743395, Property.StringToHash(DimensionPlantProperties.RarePlantVariation));
            Assert.AreEqual(
                1273594437, Property.StringToHash(DimensionPlantProperties.RareSeedVariation));
            Assert.AreEqual(
                1963487001, Property.StringToHash(DimensionPlantProperties.HighestStage));
            Assert.AreEqual(
                -965595447, Property.StringToHash(DimensionPlantProperties.TimeBetweenStages));
            Assert.AreEqual(
                -2115300463,
                Property.StringToHash(DimensionPlantProperties.KeepDamageReductionWhenRipe));
        }

        [Test]
        public void TheSproutSystemLooksThePlantUpByTheSameProperty()
        {
            Assert.AreEqual(
                Property.StringToHash(DimensionPlantProperties.TurnsIntoPlantId),
                DimensionPlantNames.TurnsIntoPlantPropertyId);
        }
    }
}
