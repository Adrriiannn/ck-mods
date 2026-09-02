using ExpandNullforge.Zones;
using NUnit.Framework;
using PugTilemap;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers a creature's claim on where it appears.
    /// </summary>
    /// <remarks>
    /// The placement itself is Core Keeper's — the chance rolls, the tile checks, the clustering, the
    /// respawn behaviour. What this mod supplies is the row describing what to place and where, and
    /// the failures worth guarding are the quiet ones: a duplicate row doubling a creature's density,
    /// or a re-registration losing the biome it was meant to be restricted to.
    /// </remarks>
    public sealed class DimensionCreatureSpawnTests
    {
        [SetUp]
        [TearDown]
        public void Reset()
        {
            DimensionCreatureSpawnRegistry.Clear();
            DimensionBiomeIdentity.Clear();
        }

        private static void Register(string obj, string biome, float chance = 0.01f)
        {
            DimensionCreatureSpawnRegistry.Register(
                obj, biome, new[] { 1001 }, TileType.ground, chance, 1, 1, false, false);
        }

        [Test]
        public void ACreatureCanClaimSeveralBiomesWithDifferentFrequencies()
        {
            // A bat common in caves and rare in the open is two claims, not a contradiction.
            Register("mod:bat", "caves", 0.05f);
            Register("mod:bat", "open", 0.001f);

            Assert.AreEqual(2, DimensionCreatureSpawnRegistry.All.Count);
        }

        [Test]
        public void ReRegisteringTheSameCreatureInTheSameBiomeReplacesIt()
        {
            // A reload re-registers everything. Two identical rows would double the creature's density
            // in game with nothing to explain it.
            Register("mod:bat", "caves", 0.05f);
            Register("mod:bat", "caves", 0.01f);

            Assert.AreEqual(1, DimensionCreatureSpawnRegistry.All.Count);
            Assert.AreEqual(0.01f, DimensionCreatureSpawnRegistry.All[0].SpawnChance, 0.00001f);
        }

        [Test]
        public void ACreatureWithNoBiomeIsAClaimOnAnywhere()
        {
            Register("mod:mote", string.Empty);

            Assert.AreEqual(1, DimensionCreatureSpawnRegistry.All.Count);
            Assert.IsEmpty(
                DimensionCreatureSpawnRegistry.All[0].BiomeId,
                "An empty biome is Core Keeper's own 'any biome', not a missing value.");
        }

        [Test]
        public void AnEmptyRegistryReportsSoSoTheHookCanSkipEntirely()
        {
            Assert.IsFalse(DimensionCreatureSpawnRegistry.HasAny);
            Register("mod:bat", "caves");
            Assert.IsTrue(DimensionCreatureSpawnRegistry.HasAny);
        }

        [Test]
        public void ACreatureWithNoNameIsRefusedRatherThanStoredNameless()
        {
            DimensionCreatureSpawnRegistry.Register(
                string.Empty, "caves", new[] { 1001 }, TileType.ground, 0.01f, 1, 1, false, false);

            Assert.IsFalse(DimensionCreatureSpawnRegistry.HasAny);
        }

        [Test]
        public void EverythingTheAuthorChoseSurvivesRegistration()
        {
            DimensionCreatureSpawnRegistry.Register(
                "mod:swarm", "caves", new[] { 1001, 1002 }, TileType.water, 0.25f, 2, 6, true, true);

            DimensionCreatureSpawnDefinition definition = DimensionCreatureSpawnRegistry.All[0];
            Assert.AreEqual(TileType.water, definition.Surface);
            Assert.AreEqual(2, definition.TilesetIds.Count);
            Assert.AreEqual(6, definition.MaxAmount);
            Assert.IsTrue(definition.Clustered);
            Assert.IsTrue(
                definition.CanSpawnInBlockedArea,
                "An author who deliberately allowed dungeon spawning must get it.");
        }

        [Test]
        public void ABiomeUsedForSpawningGetsTheSameIdentityItUsesEverywhereElse()
        {
            // Spawning, the title card and the music all resolve the biome the same way. If they
            // disagreed, a creature would spawn in a place whose name and music belonged to another.
            Biome first = DimensionBiomeIdentity.GetOrAssign("caves");
            Register("mod:bat", "caves");
            Biome second = DimensionBiomeIdentity.GetOrAssign("caves");

            Assert.AreEqual(first, second);
        }
    }
}
