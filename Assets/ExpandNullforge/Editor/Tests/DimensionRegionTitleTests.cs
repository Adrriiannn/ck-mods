using ExpandNullforge.Zones;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the identity rules behind custom biome title cards.
    /// </summary>
    /// <remarks>
    /// The title itself is Core Keeper's — the fade, the sound, the music duck, the discovery record.
    /// What this mod supplies is the answer to "which biome is this", and every way of getting that
    /// wrong is quiet: a value that shifts between sessions un-discovers a biome the player already
    /// found, a value that collides makes two biomes share one name, and a tileset claimed by the wrong
    /// biome announces a place the player is not standing in.
    /// </remarks>
    public sealed class DimensionRegionTitleTests
    {
        [SetUp]
        public void Reset()
        {
            DimensionBiomeIdentity.Clear();
            DimensionRegionTitleRegistry.Clear();
        }

        [TearDown]
        public void Cleanup()
        {
            DimensionBiomeIdentity.Clear();
            DimensionRegionTitleRegistry.Clear();
        }

        [Test]
        public void ABiomeKeepsTheSameValueEveryTimeItIsAskedFor()
        {
            Biome first = DimensionBiomeIdentity.GetOrAssign("frostlands");
            Biome second = DimensionBiomeIdentity.GetOrAssign("frostlands");
            Assert.AreEqual(first, second);
        }

        [Test]
        public void ABiomeKeepsItsValueAcrossAFreshStart()
        {
            // The value is written into the player's discovery list, which persists. If it were
            // assigned by counting registrations it would change the moment an author added a biome
            // above it in a list, and every player would lose the biomes they had found.
            Biome before = DimensionBiomeIdentity.GetOrAssign("frostlands");

            DimensionBiomeIdentity.Clear();
            DimensionBiomeIdentity.GetOrAssign("somewhere-else");
            DimensionBiomeIdentity.GetOrAssign("another-place");
            Biome after = DimensionBiomeIdentity.GetOrAssign("frostlands");

            Assert.AreEqual(
                before,
                after,
                "The value must come from the id alone, not from the order ids were seen in.");
        }

        [Test]
        public void DifferentBiomesGetDifferentValues()
        {
            Biome a = DimensionBiomeIdentity.GetOrAssign("frostlands");
            Biome b = DimensionBiomeIdentity.GetOrAssign("ashfields");
            Assert.AreNotEqual(a, b, "Two biomes sharing a value would share one discovery record.");
        }

        [Test]
        public void CustomValuesStayWellClearOfCoreKeepersOwn()
        {
            // Core Keeper keeps adding biomes. Sitting just past the current end of its enum would
            // start colliding the first time the game shipped a new one.
            Biome biome = DimensionBiomeIdentity.GetOrAssign("frostlands");
            Assert.GreaterOrEqual((int)biome, DimensionBiomeIdentity.FirstCustomBiomeValue);
            Assert.Greater(
                DimensionBiomeIdentity.FirstCustomBiomeValue,
                (int)Biome.__MAX_VALUE__,
                "Custom values must start beyond every biome the game currently defines.");
        }

        [Test]
        public void AnEmptyBiomeIdIsNoBiomeRatherThanAValueOfItsOwn()
        {
            Assert.AreEqual(Biome.None, DimensionBiomeIdentity.GetOrAssign(string.Empty));
            Assert.AreEqual(Biome.None, DimensionBiomeIdentity.GetOrAssign(null));
        }

        [Test]
        public void AValueCanBeTurnedBackIntoTheBiomeItCameFrom()
        {
            Biome biome = DimensionBiomeIdentity.GetOrAssign("frostlands");

            string id;
            Assert.IsTrue(DimensionBiomeIdentity.TryGetBiomeId(biome, out id));
            Assert.AreEqual("frostlands", id);
            Assert.IsTrue(DimensionBiomeIdentity.IsCustom(biome));
            Assert.IsFalse(DimensionBiomeIdentity.IsCustom(Biome.Nature));
        }

        [Test]
        public void StandingOnABiomesOwnTilesetIdentifiesThatBiome()
        {
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.cyan, new[] { 1001, 1002 }, string.Empty);

            string biomeId;
            Assert.IsTrue(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(1001, out biomeId));
            Assert.AreEqual("frostlands", biomeId);
            Assert.IsTrue(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(1002, out biomeId));
            Assert.AreEqual("frostlands", biomeId);
        }

        [Test]
        public void AVanillaTilesetBelongsToNoCustomBiome()
        {
            // This is what lets a player walk out of a custom biome and get their real one back. If an
            // unclaimed tileset resolved to anything, the biome would never end.
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.cyan, new[] { 1001 }, string.Empty);

            string biomeId;
            Assert.IsFalse(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(0, out biomeId));
            Assert.IsFalse(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(8, out biomeId));
        }

        [Test]
        public void RegisteringABiomeAgainReplacesItRatherThanStackingIt()
        {
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.cyan, new[] { 1001 }, string.Empty);
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.red, new[] { 1003 }, "Torch");

            Assert.AreEqual(1, DimensionRegionTitleRegistry.All.Count);
            Assert.AreEqual(Color.red, DimensionRegionTitleRegistry.All[0].Color);
            Assert.AreEqual("Torch", DimensionRegionTitleRegistry.All[0].IconObjectName);

            // The tilesets the old registration claimed must be released, or the biome would still be
            // reported for ground it no longer owns.
            string biomeId;
            Assert.IsFalse(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(1001, out biomeId));
            Assert.IsTrue(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(1003, out biomeId));
        }

        [Test]
        public void ATilesetClaimedTwiceKeepsItsFirstOwner()
        {
            // Two mods sharing a tileset is a conflict this framework cannot arbitrate. Keeping the
            // first claim at least makes the outcome the same on every machine, rather than depending
            // on which mod happened to load first.
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.cyan, new[] { 1001 }, string.Empty);
            DimensionRegionTitleRegistry.Register(
                "ashfields", "Biomes/ashfields", Color.red, new[] { 1001 }, string.Empty);

            string biomeId;
            Assert.IsTrue(DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(1001, out biomeId));
            Assert.AreEqual("frostlands", biomeId);
        }

        [Test]
        public void ARegistryWithNothingInItReportsSoSoTheSystemCanSkipEntirely()
        {
            // The per-frame biome system early-outs on this. A mod that ships no custom biome must
            // cost nothing at all.
            Assert.IsFalse(DimensionRegionTitleRegistry.HasAny);
            DimensionRegionTitleRegistry.Register(
                "frostlands", "Biomes/frostlands", Color.cyan, new[] { 1001 }, string.Empty);
            Assert.IsTrue(DimensionRegionTitleRegistry.HasAny);
        }
    }
}
