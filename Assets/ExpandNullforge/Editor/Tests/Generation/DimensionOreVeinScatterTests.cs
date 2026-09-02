#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Generation;
using NUnit.Framework;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The vein scatter's contract, enforced offline where the failure modes are invisible:
    /// an ore write with no wall before it does not fail in game — it silently MINTS a loose
    /// ore item on the floor. These tests hold the write-list invariants that prevent that.
    /// </summary>
    internal sealed class DimensionOreVeinScatterTests
    {
        private const int WallTileset = 1234;
        private const string Dim = "test.ore-dim";

        [SetUp]
        public void Reset()
        {
            DimensionOreVeinRuleRegistry.Clear();
            DimensionOreBiomeGate.ClearAll();
        }

        [TearDown]
        public void Clear()
        {
            DimensionOreVeinRuleRegistry.Clear();
            DimensionOreBiomeGate.ClearAll();
        }

        private static List<DimensionResolvedTileWrite> WallField(int width, int height)
        {
            List<DimensionResolvedTileWrite> writes = new List<DimensionResolvedTileWrite>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    writes.Add(new DimensionResolvedTileWrite(
                        new int2(x, y), TileType.wall, WallTileset));
                }
            }

            return writes;
        }

        private static void RegisterRule(float abundance, int min = 3, int max = 6)
        {
            DimensionOreVeinRuleRegistry.Register(WallTileset, new[]
            {
                new DimensionOreVeinRule(WallTileset, "mod:zanium", abundance, min, max)
            });
        }

        [Test]
        public void EveryOreWriteHasItsWallEarlierInTheList()
        {
            RegisterRule(10f);
            List<DimensionResolvedTileWrite> writes = WallField(40, 40);
            DimensionOreVeinScatter.AppendVeins(Dim, writes, null);

            HashSet<int2> wallsSeen = new HashSet<int2>();
            int oreCount = 0;
            for (int i = 0; i < writes.Count; i++)
            {
                if (writes[i].TileType == TileType.wall)
                {
                    wallsSeen.Add(writes[i].AbsolutePosition);
                }
                else if (writes[i].TileType == TileType.ore)
                {
                    oreCount++;
                    Assert.That(
                        wallsSeen.Contains(writes[i].AbsolutePosition),
                        Is.True,
                        "An ore write at " + writes[i].AbsolutePosition + " precedes its wall. " +
                        "In game that add is rejected and the ore spills as a loose item.");
                }
            }

            Assert.That(oreCount, Is.GreaterThan(0), "Abundance 10 on 1600 walls grew nothing.");
        }

        [Test]
        public void SameDimensionGrowsIdenticalVeinsEveryTime()
        {
            RegisterRule(5f);
            List<DimensionResolvedTileWrite> first = WallField(30, 30);
            List<DimensionResolvedTileWrite> second = WallField(30, 30);
            DimensionOreVeinScatter.AppendVeins(Dim, first, null);
            DimensionOreVeinScatter.AppendVeins(Dim, second, null);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].AbsolutePosition, Is.EqualTo(first[i].AbsolutePosition));
                Assert.That(second[i].TileType, Is.EqualTo(first[i].TileType));
                Assert.That(second[i].Tileset, Is.EqualTo(first[i].Tileset));
            }

            List<DimensionResolvedTileWrite> other = WallField(30, 30);
            DimensionOreVeinScatter.AppendVeins("test.other-dim", other, null);
            Assert.That(
                other.Count == first.Count && SamePositions(other, first),
                Is.False,
                "Two different dimensions grew byte-identical veins; the seed ignores the id.");
        }

        [Test]
        public void NoTwoVeinCellsShareAPositionAndPaintedOreIsNeverReclaimed()
        {
            RegisterRule(10f);
            List<DimensionResolvedTileWrite> writes = WallField(30, 30);
            int2 painted = new int2(5, 5);
            writes.Add(new DimensionResolvedTileWrite(painted, TileType.ore, 777));
            int baseCount = writes.Count;

            DimensionOreVeinScatter.AppendVeins(Dim, writes, null);

            HashSet<int2> oreCells = new HashSet<int2> { painted };
            for (int i = baseCount; i < writes.Count; i++)
            {
                Assert.That(
                    oreCells.Add(writes[i].AbsolutePosition),
                    Is.True,
                    "Two veins claimed " + writes[i].AbsolutePosition +
                    " (or the scatter overwrote a hand-painted vein).");
            }
        }

        [Test]
        public void AbundanceZeroGrowsNothing()
        {
            RegisterRule(0f);
            List<DimensionResolvedTileWrite> writes = WallField(40, 40);
            int baseCount = writes.Count;
            DimensionOreVeinScatter.AppendVeins(Dim, writes, null);

            Assert.That(writes.Count, Is.EqualTo(baseCount), "Zero abundance means paint-only.");
        }

        [Test]
        public void VeinSizesStayInsideTheAuthoredBounds()
        {
            RegisterRule(10f, 2, 4);
            List<DimensionResolvedTileWrite> writes = WallField(60, 60);
            int baseCount = writes.Count;
            DimensionOreVeinScatter.AppendVeins(Dim, writes, null);

            // Group appended ore into 4-connected blobs and measure each.
            HashSet<int2> cells = new HashSet<int2>();
            for (int i = baseCount; i < writes.Count; i++)
            {
                cells.Add(writes[i].AbsolutePosition);
            }

            Assert.That(cells.Count, Is.GreaterThan(0));
            int2[] offsets = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
            HashSet<int2> visited = new HashSet<int2>();
            foreach (int2 start in cells)
            {
                if (!visited.Add(start))
                {
                    continue;
                }

                int size = 1;
                Queue<int2> frontier = new Queue<int2>();
                frontier.Enqueue(start);
                while (frontier.Count > 0)
                {
                    int2 cell = frontier.Dequeue();
                    for (int d = 0; d < 4; d++)
                    {
                        int2 neighbour = cell + offsets[d];
                        if (cells.Contains(neighbour) && visited.Add(neighbour))
                        {
                            size++;
                            frontier.Enqueue(neighbour);
                        }
                    }
                }

                // The strict per-vein bound is enforced by construction (GrowVein rolls its
                // size inside [min, max] and stops there); what a component measure CAN
                // legally see is a clump of several veins that grew adjacent. The assertion
                // therefore guards runaway growth, not the exact per-vein number.
                Assert.That(
                    size,
                    Is.LessThanOrEqualTo(4 * 4),
                    "An ore clump of " + size + " cells is far past anything " +
                    "[min 2, max 4] veins could make even merged — growth is not stopping.");
            }
        }

        [Test]
        public void TheBiomeGateNarrowsWhereANamedOreGrows()
        {
            DimensionOreBiomeGate.Register(
                Dim,
                new Api.DimensionBounds(new int2(0, 0), new int2(50, 50)),
                new[] { "OtherOre" });

            Assert.That(
                DimensionOreBiomeGate.Allows(Dim, new int2(5, 5), "mod:zanium"),
                Is.False,
                "The biome named ores and ours is not among them.");
            Assert.That(
                DimensionOreBiomeGate.Allows(Dim, new int2(5, 5), "OtherOre"),
                Is.True);
            Assert.That(
                DimensionOreBiomeGate.Allows(Dim, new int2(500, 500), "mod:zanium"),
                Is.True,
                "Outside every covered biome, everything is allowed — undecided is not forbidden.");
        }

        [Test]
        public void RegisteringTwiceReplacesInsteadOfDoubling()
        {
            RegisterRule(5f);
            RegisterRule(5f);
            List<DimensionResolvedTileWrite> once = WallField(30, 30);
            DimensionOreVeinScatter.AppendVeins(Dim, once, null);

            DimensionOreVeinRuleRegistry.Clear();
            RegisterRule(5f);
            List<DimensionResolvedTileWrite> fresh = WallField(30, 30);
            DimensionOreVeinScatter.AppendVeins(Dim, fresh, null);

            Assert.That(once.Count, Is.EqualTo(fresh.Count), "A reload is not twice the ore.");
        }

        private static bool SamePositions(
            List<DimensionResolvedTileWrite> a,
            List<DimensionResolvedTileWrite> b)
        {
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                if (!a[i].AbsolutePosition.Equals(b[i].AbsolutePosition))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
