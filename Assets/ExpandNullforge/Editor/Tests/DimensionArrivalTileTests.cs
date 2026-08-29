using System.Collections.Generic;
using ExpandNullforge.Portals;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Guards the order a portal arrival searches for open ground in.
    /// </summary>
    /// <remarks>
    /// The classification itself needs a live world's tiles and is verified in game, but the search
    /// order is pure data and is exactly where a silent mistake would hide: a wrong ring filter
    /// skips cells that look covered, and the player lands in a wall next to perfectly good floor
    /// with nothing in the log to say why.
    /// </remarks>
    public sealed class DimensionArrivalTileTests
    {
        [Test]
        public void SearchCoversEveryCellInRangeExceptTheOriginItself()
        {
            int span = DimensionArrivalTile.SearchRadius * 2 + 1;
            Assert.AreEqual(
                span * span - 1,
                DimensionArrivalTile.SearchOffsets.Length,
                "The search must consider every cell in the square around the destination, and the " +
                "destination itself is excluded because it was already rejected.");
        }

        [Test]
        public void NoCellIsSearchedTwice()
        {
            HashSet<int2> seen = new HashSet<int2>();
            foreach (int2 offset in DimensionArrivalTile.SearchOffsets)
            {
                Assert.IsTrue(
                    seen.Add(offset),
                    "Offset " + offset + " appears more than once; the ring filter is letting an " +
                    "inner ring through a second time.");
            }
        }

        [Test]
        public void TheOriginIsNeverACandidate()
        {
            foreach (int2 offset in DimensionArrivalTile.SearchOffsets)
            {
                Assert.IsFalse(
                    offset.x == 0 && offset.y == 0,
                    "The destination tile was already found unstandable; searching it again would " +
                    "return it and move nobody.");
            }
        }

        [Test]
        public void CloserTilesAreAlwaysTriedFirst()
        {
            int previous = 0;
            foreach (int2 offset in DimensionArrivalTile.SearchOffsets)
            {
                int ring = math.max(math.abs(offset.x), math.abs(offset.y));
                Assert.GreaterOrEqual(
                    ring,
                    previous,
                    "Ring " + ring + " is searched after ring " + previous + ". A player must land " +
                    "on the nearest open tile, not merely on some open tile.");
                previous = ring;
            }
        }

        [Test]
        public void EveryCandidateIsWithinTheSearchRadius()
        {
            foreach (int2 offset in DimensionArrivalTile.SearchOffsets)
            {
                Assert.LessOrEqual(
                    math.max(math.abs(offset.x), math.abs(offset.y)),
                    DimensionArrivalTile.SearchRadius,
                    "Offset " + offset + " is further than the documented radius. Players would be " +
                    "moved further from where the dimension author aimed them than the design allows.");
            }
        }

        [Test]
        public void TheFirstCellTriedIsAdjacentToTheDestination()
        {
            int2 first = DimensionArrivalTile.SearchOffsets[0];
            Assert.AreEqual(
                1,
                math.max(math.abs(first.x), math.abs(first.y)),
                "The search must start in the ring touching the destination.");
        }
    }
}
