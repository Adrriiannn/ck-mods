#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Generation;
using NUnit.Framework;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The compile core is what places tiles in the world, so its coordinate translation, area
    /// clipping, and unresolved-tileset handling are pinned here — a mistake in any of them
    /// silently mis-places or drops terrain in-game.
    /// </summary>
    internal sealed class DimensionTileMapCompilerTests
    {
        [Test]
        public void LocalPositionsTranslateToAbsoluteByTheAreaOrigin()
        {
            // Dimension local (0,0) sits at absolute (5000,0) — the first radial slot.
            DimensionBounds local = Bounds(0, 0, 4, 4);
            DimensionBounds absolute = Bounds(5000, 0, 5004, 4);

            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 4, 4);
            int ground = map.AddBlock(Block(DimensionTileRole.Ground, 2));
            map.SetBlock(new int2(1, 2), ground);

            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), local, absolute);

            Assert.That(result.WriteCount, Is.EqualTo(1));
            Assert.That(result.Writes[0].AbsolutePosition, Is.EqualTo(new int2(5001, 2)));
            Assert.That(result.Writes[0].TileType, Is.EqualTo(TileType.ground));
            Assert.That(result.Writes[0].Tileset, Is.EqualTo(2));
        }

        [Test]
        public void PlacementsOutsideTheArea_AreSkipped()
        {
            // The area being generated is only the lower-left 2x2 of a 4x4 painted map.
            DimensionBounds local = Bounds(0, 0, 2, 2);
            DimensionBounds absolute = Bounds(0, 0, 2, 2);

            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 4, 4);
            int ground = map.AddBlock(Block(DimensionTileRole.Ground, 0));
            map.SetBlock(new int2(0, 0), ground); // inside the area
            map.SetBlock(new int2(3, 3), ground); // outside the area

            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), local, absolute);

            Assert.That(result.WriteCount, Is.EqualTo(1));
            Assert.That(result.Writes[0].AbsolutePosition, Is.EqualTo(new int2(0, 0)));
        }

        [Test]
        public void GroundAndWallAtTheSameCell_ProduceTwoWrites_FloorFirst()
        {
            DimensionBounds bounds = Bounds(0, 0, 2, 2);
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 2, 2);
            int ground = map.AddBlock(Block(DimensionTileRole.Ground, 0));
            int wall = map.AddBlock(Block(DimensionTileRole.Wall, 1));
            map.SetBlock(new int2(0, 0), ground);
            map.SetBlock(new int2(0, 0), wall);

            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), bounds, bounds);

            Assert.That(result.WriteCount, Is.EqualTo(2));
            Assert.That(result.Writes[0].TileType, Is.EqualTo(TileType.ground));
            Assert.That(result.Writes[1].TileType, Is.EqualTo(TileType.wall));
        }

        [Test]
        public void WaterAndPit_CompileToTheirTileTypes()
        {
            DimensionBounds bounds = Bounds(0, 0, 2, 1);
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 2, 1);
            int water = map.AddBlock(Block(DimensionTileRole.Liquid, 0));
            int pit = map.AddBlock(Block(DimensionTileRole.Pit, 0));
            map.SetBlock(new int2(0, 0), water);
            map.SetBlock(new int2(1, 0), pit);

            List<DimensionResolvedTileWrite> writes =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), bounds, bounds).Writes;

            Assert.That(TypeAt(writes, new int2(0, 0)), Is.EqualTo(TileType.water));
            Assert.That(TypeAt(writes, new int2(1, 0)), Is.EqualTo(TileType.pit));
        }

        [Test]
        public void UnresolvedCustomTileset_IsSkippedAndReportedOnce()
        {
            DimensionBounds bounds = Bounds(0, 0, 3, 1);
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 3, 1);
            DimensionMapBlock custom = new DimensionMapBlock(
                "mod_crystal", "Crystal", DimensionTileRole.Ground,
                DimensionBlockTilesetSource.Custom, 0, "mod:crystal");
            int index = map.AddBlock(custom);
            map.SetBlock(new int2(0, 0), index);
            map.SetBlock(new int2(1, 0), index);
            map.SetBlock(new int2(2, 0), index);

            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), bounds, bounds);

            Assert.That(result.WriteCount, Is.EqualTo(0), "Unresolved tiles must not be written.");
            Assert.That(
                result.Skipped.Count,
                Is.EqualTo(1),
                "Three tiles of one missing tileset should report once, not three times.");
            Assert.That(result.Skipped[0], Does.Contain("mod:crystal"));
        }

        [Test]
        public void NullPlacements_ProduceAnEmptyResult()
        {
            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(null, Bounds(0, 0, 1, 1), Bounds(0, 0, 1, 1));
            Assert.That(result.WriteCount, Is.EqualTo(0));
            Assert.That(result.Skipped, Is.Empty);
        }

        private static DimensionBounds Bounds(int minX, int minY, int maxX, int maxY)
        {
            return new DimensionBounds(new int2(minX, minY), new int2(maxX, maxY));
        }

        private static DimensionMapBlock Block(DimensionTileRole role, int tileset)
        {
            return new DimensionMapBlock(
                role.ToString(), role.ToString(), role,
                DimensionBlockTilesetSource.Vanilla, tileset, string.Empty);
        }

        private static TileType TypeAt(List<DimensionResolvedTileWrite> writes, int2 pos)
        {
            for (int i = 0; i < writes.Count; i++)
            {
                if (writes[i].AbsolutePosition.Equals(pos))
                {
                    return writes[i].TileType;
                }
            }

            Assert.Fail("No write at " + pos);
            return TileType.none;
        }
    }
}
#endif
