#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Generation;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a dimension's ground and walls are made of, per cell.
    /// </summary>
    /// <remarks>
    /// The failure this guards is not a crash. It is a dimension that generates a dirt platform
    /// while its Biome page shows a carefully chosen block — which is exactly what the framework
    /// shipped before this registry existed, and which nothing in a build would have reported.
    /// </remarks>
    internal sealed class DimensionTerrainMaterialRegistryTests
    {
        private const string Dimension = "mymod:cavern";

        [SetUp]
        [TearDown]
        public void Reset()
        {
            DimensionTerrainMaterialRegistry.ClearAll();
        }

        [Test]
        public void NothingRegistered_ResolvesToNothing_AndHandsBackDirt()
        {
            int ground;
            int wall;
            bool resolved = DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(4, 4), out ground, out wall);

            Assert.That(resolved, Is.False);
            Assert.That(ground, Is.EqualTo(DimensionTerrainMaterialRegistry.DefaultTileset));
            Assert.That(wall, Is.EqualTo(DimensionTerrainMaterialRegistry.DefaultTileset));
        }

        [Test]
        public void ABoundBiome_AnswersInsideItsBounds()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "cave", 1000, 1001);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "cave", Bounds(0, 0, 10, 10));

            int ground;
            int wall;
            bool resolved = DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(5, 5), out ground, out wall);

            Assert.That(resolved, Is.True);
            Assert.That(ground, Is.EqualTo(1000));
            Assert.That(wall, Is.EqualTo(1001));
        }

        [Test]
        public void OutsideTheBoundZone_NothingCoversTheCell()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "cave", 1000, 1001);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "cave", Bounds(0, 0, 10, 10));

            int ground;
            int wall;
            bool resolved = DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(40, 40), out ground, out wall);

            Assert.That(resolved, Is.False);
            Assert.That(ground, Is.EqualTo(DimensionTerrainMaterialRegistry.DefaultTileset));
        }

        [Test]
        public void AZoneOfAnotherBiome_BindsNothing()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "cave", 1000, 1001);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "meadow", Bounds(0, 0, 10, 10));

            int ground;
            int wall;
            Assert.That(
                DimensionTerrainMaterialRegistry.TryResolve(
                    Dimension, new int2(5, 5), out ground, out wall),
                Is.False);
        }

        [Test]
        public void TwoBiomesOverOneCell_ResolveToTheOneBoundLast()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "first", 10, 11);
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "second", 20, 21);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "first", Bounds(0, 0, 10, 10));
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "second", Bounds(5, 5, 15, 15));

            int ground;
            int wall;
            DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(7, 7), out ground, out wall);

            Assert.That(ground, Is.EqualTo(20));
            Assert.That(wall, Is.EqualTo(21));
        }

        /// <summary>
        /// Zone update fires repeatedly on one zone, so a rebind must replace the biome's row where
        /// it stands. If it appended instead, the biome would move to the end of the list and take
        /// every overlapping cell from whoever legitimately owned it — so which biome wins an
        /// overlap would depend on how often a zone happened to be updated.
        /// </summary>
        [Test]
        public void RebindingAZone_KeepsItsPlaceInTheOrder()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "first", 10, 11);
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "second", 20, 21);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "first", Bounds(0, 0, 10, 10));
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "second", Bounds(0, 0, 10, 10));

            // The same zone comes round again, exactly as a repeated zone update produces.
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "first", Bounds(0, 0, 10, 10));

            int ground;
            int wall;
            DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(2, 2), out ground, out wall);

            Assert.That(ground, Is.EqualTo(20), "The rebind must not push 'first' past 'second'.");
        }

        [Test]
        public void RebindingAZone_TakesTheBiomesNewestMaterial()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "cave", 10, 11);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "cave", Bounds(0, 0, 10, 10));
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "cave", 30, 31);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "cave", Bounds(0, 0, 10, 10));

            int ground;
            int wall;
            DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(2, 2), out ground, out wall);

            Assert.That(ground, Is.EqualTo(30));
            Assert.That(wall, Is.EqualTo(31));
        }

        /// <summary>
        /// The one-entry memo remembers the row that answered last. Two overlapping rows make that
        /// unsafe — a remembered row can contain a cell that a later row wins — so the memo turns
        /// itself off for a dimension whose rows overlap. Asking about the shared cell after asking
        /// about a cell only the first row covers is precisely the order that catches it.
        /// </summary>
        [Test]
        public void TheMemoDoesNotHandBackTheLoserOfAnOverlap()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "first", 10, 11);
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "second", 20, 21);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "first", Bounds(0, 0, 10, 10));
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "second", Bounds(5, 5, 15, 15));

            int ground;
            int wall;

            // Only 'first' covers this one, so it is what a memo would remember.
            DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(1, 1), out ground, out wall);
            Assert.That(ground, Is.EqualTo(10));

            // 'first' also contains this one, but 'second' was bound later and wins it.
            DimensionTerrainMaterialRegistry.TryResolve(
                Dimension, new int2(7, 7), out ground, out wall);
            Assert.That(ground, Is.EqualTo(20));
        }

        [Test]
        public void Clear_DropsOneDimensionAndLeavesTheOther()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "cave", 10, 11);
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "cave", Bounds(0, 0, 10, 10));
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial("other:place", "cave", 30, 31);
            DimensionTerrainMaterialRegistry.BindZone("other:place", "cave", Bounds(0, 0, 10, 10));

            DimensionTerrainMaterialRegistry.Clear(Dimension);

            int ground;
            int wall;
            Assert.That(
                DimensionTerrainMaterialRegistry.TryResolve(
                    Dimension, new int2(2, 2), out ground, out wall),
                Is.False);
            Assert.That(
                DimensionTerrainMaterialRegistry.TryResolve(
                    "other:place", new int2(2, 2), out ground, out wall),
                Is.True);
            Assert.That(ground, Is.EqualTo(30));
        }

        [Test]
        public void Clear_AlsoForgetsTheWaitingRegistration()
        {
            DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(Dimension, "cave", 10, 11);
            DimensionTerrainMaterialRegistry.Clear(Dimension);

            // A zone arriving after the clear has nothing waiting for it any more, which is what
            // stops one world's materials leaking into the next world loaded in the same session.
            DimensionTerrainMaterialRegistry.BindZone(Dimension, "cave", Bounds(0, 0, 10, 10));

            int ground;
            int wall;
            Assert.That(
                DimensionTerrainMaterialRegistry.TryResolve(
                    Dimension, new int2(2, 2), out ground, out wall),
                Is.False);
        }

        private static DimensionBounds Bounds(int minX, int minY, int maxX, int maxY)
        {
            return new DimensionBounds(new int2(minX, minY), new int2(maxX, maxY));
        }
    }
}
#endif
