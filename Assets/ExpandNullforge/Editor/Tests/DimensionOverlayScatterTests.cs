using System.Collections.Generic;
using ExpandNullforge.Generation;
using NUnit.Framework;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Guards the properties that make scattered decoration safe to ship.
    /// </summary>
    /// <remarks>
    /// Two of these are correctness, not polish. Determinism is what lets a host and a joining client
    /// generate the same terrain independently — a scatter that varied would put a mod's grass in
    /// different places for different players in the same world. Channel independence is what stops
    /// every overlay choosing the same cells and piling onto one patch while the rest stays bare.
    /// </remarks>
    public sealed class DimensionOverlayScatterTests
    {
        private static DimensionOverlayRule Grass(float density)
        {
            return new DimensionOverlayRule(LayerName.smallGrass, TileType.smallGrass, density);
        }

        private static DimensionOverlayRule Pebbles(float density)
        {
            return new DimensionOverlayRule(LayerName.smallStones, TileType.smallStones, density);
        }

        [Test]
        public void TheSameCellAlwaysAnswersTheSame()
        {
            DimensionOverlayRule rule = Grass(0.5f);
            for (int i = 0; i < 200; i++)
            {
                int2 cell = new int2(i * 7 - 400, i * 13 - 900);
                bool first = DimensionOverlayScatter.ShouldPlace(1234UL, cell, rule);
                bool second = DimensionOverlayScatter.ShouldPlace(1234UL, cell, rule);
                Assert.AreEqual(
                    first,
                    second,
                    "A host and a joining client generate terrain independently; a cell that answered " +
                    "differently would put decoration in different places for different players.");
            }
        }

        [Test]
        public void ADifferentSeedGrowsADifferentWorld()
        {
            DimensionOverlayRule rule = Grass(0.5f);
            int differences = 0;
            for (int i = 0; i < 400; i++)
            {
                int2 cell = new int2(i % 20, i / 20);
                if (DimensionOverlayScatter.ShouldPlace(1UL, cell, rule) !=
                    DimensionOverlayScatter.ShouldPlace(2UL, cell, rule))
                {
                    differences++;
                }
            }

            Assert.Greater(
                differences,
                100,
                "Two worlds should not grow identical decoration; the seed is barely reaching the hash " +
                "if they do.");
        }

        [Test]
        public void TwoOverlaysDoNotChooseTheSameCells()
        {
            int both = 0;
            int either = 0;
            for (int i = 0; i < 2000; i++)
            {
                int2 cell = new int2(i % 50, i / 50);
                bool grass = DimensionOverlayScatter.ShouldPlace(99UL, cell, Grass(0.3f));
                bool pebbles = DimensionOverlayScatter.ShouldPlace(99UL, cell, Pebbles(0.3f));
                if (grass && pebbles) both++;
                if (grass || pebbles) either++;
            }

            // Independent at 0.3 each gives ~9% overlap of 2000 cells; identical choices would give
            // both == either. Anything close to that means the overlays share a hash.
            Assert.Less(
                both,
                either / 2,
                "Every overlay picking the same cells stacks them all on one patch of ground and " +
                "leaves the rest bare.");
        }

        [Test]
        public void DensityRoughlyMatchesTheShareOfCellsChosen()
        {
            const int cells = 4000;
            foreach (float density in new[] { 0.1f, 0.25f, 0.5f, 0.75f })
            {
                int hits = 0;
                for (int i = 0; i < cells; i++)
                {
                    if (DimensionOverlayScatter.ShouldPlace(7UL, new int2(i % 80, i / 80), Grass(density)))
                    {
                        hits++;
                    }
                }

                float actual = hits / (float)cells;
                Assert.AreEqual(
                    density,
                    actual,
                    0.05f,
                    "Density is the share of ground that gets the overlay; at " + density +
                    " the scatter produced " + actual.ToString("F3") + ".");
            }
        }

        [Test]
        public void ZeroDensityPlacesNothingAndFullDensityPlacesEverywhere()
        {
            for (int i = 0; i < 100; i++)
            {
                int2 cell = new int2(i, -i);
                Assert.IsFalse(DimensionOverlayScatter.ShouldPlace(5UL, cell, Grass(0f)));
                Assert.IsTrue(DimensionOverlayScatter.ShouldPlace(5UL, cell, Grass(1f)));
            }
        }

        [Test]
        public void DensityIsClampedRatherThanTrusted()
        {
            Assert.IsFalse(DimensionOverlayScatter.ShouldPlace(5UL, new int2(3, 3), Grass(-2f)));
            Assert.IsTrue(DimensionOverlayScatter.ShouldPlace(5UL, new int2(3, 3), Grass(4f)));
        }

        [Test]
        public void NegativeCoordinatesAreScatteredJustLikePositiveOnes()
        {
            // A dimension centred on the origin has as much terrain in negative space as positive; a
            // sign-handling mistake would leave one quadrant bare or solid.
            int hits = 0;
            const int cells = 2000;
            for (int i = 0; i < cells; i++)
            {
                if (DimensionOverlayScatter.ShouldPlace(11UL, new int2(-(i % 50) - 1, -(i / 50) - 1), Grass(0.5f)))
                {
                    hits++;
                }
            }

            Assert.AreEqual(0.5f, hits / (float)cells, 0.06f);
        }

        [Test]
        public void CollectReturnsEveryOverlayThatBelongsOnACell()
        {
            List<DimensionOverlayRule> rules = new List<DimensionOverlayRule> { Grass(1f), Pebbles(1f) };
            List<DimensionOverlayRule> got = new List<DimensionOverlayRule>();

            DimensionOverlayScatter.Collect(3UL, new int2(0, 0), rules, got);

            Assert.AreEqual(
                2,
                got.Count,
                "A cell can carry grass and pebbles at once, exactly as vanilla ground does.");
        }

        [Test]
        public void CollectAddsNothingWhenNoOverlayApplies()
        {
            List<DimensionOverlayRule> rules = new List<DimensionOverlayRule> { Grass(0f), Pebbles(0f) };
            List<DimensionOverlayRule> got = new List<DimensionOverlayRule>();

            DimensionOverlayScatter.Collect(3UL, new int2(0, 0), rules, got);

            Assert.IsEmpty(got);
        }
    }
}
