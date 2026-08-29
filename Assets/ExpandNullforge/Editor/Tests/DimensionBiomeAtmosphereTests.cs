using ExpandNullforge.Zones;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the shared "where is the player standing" index that a biome's title, ambience and music
    /// all read.
    /// </summary>
    /// <remarks>
    /// These three used to be able to disagree, and the disagreement is the interesting failure: the
    /// title says one biome, the music plays another's, and the ambience belongs to neither. Keeping
    /// one index is what prevents it, so the tests are about the index rather than about sound.
    /// </remarks>
    public sealed class DimensionBiomeAtmosphereTests
    {
        [SetUp]
        public void Reset()
        {
            DimensionBiomeIdentity.Clear();
            DimensionRegionTitleRegistry.Clear();
            DimensionBiomeAtmosphereRegistry.Clear();
            DimensionBiomeTilesetIndex.Clear();
        }

        [TearDown]
        public void Cleanup()
        {
            DimensionBiomeIdentity.Clear();
            DimensionRegionTitleRegistry.Clear();
            DimensionBiomeAtmosphereRegistry.Clear();
            DimensionBiomeTilesetIndex.Clear();
        }

        [Test]
        public void AtmosphereAndTitleAgreeAboutWhichBiomeATilesetBelongsTo()
        {
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.cyan, new[] { 1001 }, string.Empty);
            DimensionBiomeAtmosphereRegistry.Register(
                "frostlands", "assets/audio/wind.ogg", 1f, "SEA_BIOME", new[] { 1001, 1002 });

            string biomeId;
            Assert.IsTrue(DimensionBiomeTilesetIndex.TryGetBiomeId(1001, out biomeId));
            Assert.AreEqual("frostlands", biomeId);

            // The second tileset was introduced by the atmosphere alone, and the title registry sees
            // it too — they are reading one index, not two copies.
            Assert.IsTrue(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(1002, out biomeId));
            Assert.AreEqual("frostlands", biomeId);
        }

        [Test]
        public void ABiomeCanHaveAmbienceWithoutATitleCard()
        {
            // Wanting a place to sound different without announcing itself is an ordinary thing to
            // want — a cave that belongs to the biome above it.
            DimensionBiomeAtmosphereRegistry.Register(
                "underhollow", "assets/audio/drip.ogg", 0.6f, string.Empty, new[] { 1010 });

            string biomeId;
            Assert.IsTrue(DimensionBiomeTilesetIndex.TryGetBiomeId(1010, out biomeId));
            Assert.AreEqual("underhollow", biomeId);
            Assert.IsFalse(DimensionRegionTitleRegistry.HasAny);
        }

        [Test]
        public void AnUnclaimedTilesetBelongsToNoBiome()
        {
            // This is what lets a player walk out of a custom biome. If an unclaimed tileset resolved
            // to anything, the ambience and music would never hand back to the real biome.
            DimensionBiomeAtmosphereRegistry.Register(
                "frostlands", "assets/audio/wind.ogg", 1f, string.Empty, new[] { 1001 });

            string biomeId;
            Assert.IsFalse(DimensionBiomeTilesetIndex.TryGetBiomeId(0, out biomeId));
            Assert.IsFalse(DimensionBiomeTilesetIndex.TryGetBiomeId(1002, out biomeId));
        }

        [Test]
        public void ATilesetClaimedTwiceKeepsItsFirstOwnerWhicheverRegistryClaimedIt()
        {
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.cyan, new[] { 1001 }, string.Empty);
            DimensionBiomeAtmosphereRegistry.Register(
                "ashfields", "assets/audio/fire.ogg", 1f, string.Empty, new[] { 1001 });

            string biomeId;
            Assert.IsTrue(DimensionBiomeTilesetIndex.TryGetBiomeId(1001, out biomeId));
            Assert.AreEqual(
                "frostlands",
                biomeId,
                "First claim must win regardless of which registry made it, or the answer would " +
                "depend on which mod loaded first.");
        }

        [Test]
        public void ReleasingATilesetOnlyWorksForTheBiomeHoldingIt()
        {
            DimensionBiomeTilesetIndex.Claim(1001, "frostlands");
            DimensionBiomeTilesetIndex.Release(1001, "ashfields");

            string biomeId;
            Assert.IsTrue(
                DimensionBiomeTilesetIndex.TryGetBiomeId(1001, out biomeId),
                "A biome must not be able to release ground it never owned.");

            DimensionBiomeTilesetIndex.Release(1001, "frostlands");
            Assert.IsFalse(DimensionBiomeTilesetIndex.TryGetBiomeId(1001, out biomeId));
        }

        [Test]
        public void RegisteringABiomesAtmosphereAgainReplacesIt()
        {
            DimensionBiomeAtmosphereRegistry.Register(
                "frostlands", "assets/audio/wind.ogg", 1f, "SEA_BIOME", new[] { 1001 });
            DimensionBiomeAtmosphereRegistry.Register(
                "frostlands", "assets/audio/storm.ogg", 0.5f, "DESERT_BIOME", new[] { 1001 });

            Assert.AreEqual(1, DimensionBiomeAtmosphereRegistry.All.Count);
            Assert.AreEqual("assets/audio/storm.ogg", DimensionBiomeAtmosphereRegistry.All[0].AmbienceSoundKey);
            Assert.AreEqual(0.5f, DimensionBiomeAtmosphereRegistry.All[0].AmbienceVolume, 0.0001f);
            Assert.AreEqual("DESERT_BIOME", DimensionBiomeAtmosphereRegistry.All[0].MusicRosterName);
        }

        [Test]
        public void ABiomeWithNoSoundOfItsOwnSaysSoRatherThanPretending()
        {
            DimensionBiomeAtmosphereRegistry.Register("plain", string.Empty, 1f, string.Empty, new[] { 1001 });

            DimensionBiomeAtmosphereDefinition definition = DimensionBiomeAtmosphereRegistry.All[0];
            Assert.IsFalse(definition.HasAmbience);
            Assert.IsFalse(definition.HasMusic);
        }

        [Test]
        public void EveryMusicRosterABiomeCanNameIsOneTheGameActuallyHas()
        {
            // The installer parses these names against the game's own enum and warns on a miss. This
            // just pins that the names an author would reasonably reach for are real, so the warning
            // means "you typed it wrong" rather than "this never worked".
            string[] plausible =
            {
                "SLIME_BIOME", "LARVA_BIOME", "STONE_BIOME", "NATURE_BIOME",
                "SEA_BIOME", "DESERT_BIOME", "CRYSTAL_BIOME", "PASSAGE_BIOME", "EXCAVATION_BIOME"
            };

            for (int i = 0; i < plausible.Length; i++)
            {
                MusicRosterType roster;
                Assert.IsTrue(
                    System.Enum.TryParse(plausible[i], false, out roster),
                    plausible[i] + " is no longer a music roster in this version of the game.");
            }
        }

        [Test]
        public void AnEmptyRegistryReportsSoSoTheInstallersCanSkipEntirely()
        {
            Assert.IsFalse(DimensionBiomeAtmosphereRegistry.HasAny);
            Assert.IsFalse(DimensionBiomeTilesetIndex.HasAny);

            DimensionBiomeAtmosphereRegistry.Register(
                "frostlands", "assets/audio/wind.ogg", 1f, string.Empty, new[] { 1001 });

            Assert.IsTrue(DimensionBiomeAtmosphereRegistry.HasAny);
            Assert.IsTrue(DimensionBiomeTilesetIndex.HasAny);
        }
    }
}
