#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Generation;
using ExpandNullforge.Tilesets;
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

        /// <summary>
        /// A named custom tileset generates even when nothing has registered it. Its id is derived
        /// from the name, so the terrain is right and only the appearance waits on the mod —
        /// whereas refusing to write would leave holes (missing ground is a pit) that outlive the
        /// missing mod.
        /// </summary>
        [Test]
        public void AnUninstalledCustomTileset_StillGeneratesAndIsNotedOnce()
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

            Assert.That(result.WriteCount, Is.EqualTo(3), "The terrain must not be left with holes.");
            Assert.That(result.Skipped, Is.Empty, "Nothing failed — this is a note, not a skip.");
            Assert.That(
                result.Notes.Count,
                Is.EqualTo(1),
                "Three tiles of one uninstalled tileset should report once, not three times.");
            Assert.That(result.Notes[0], Does.Contain("mod:crystal"));

            // Every tile carries the id the name derives to, so installing the mod later fixes all
            // of them at once with no migration.
            int expected = DimensionTilesetRegistry.ComputeTilesetId("mod:crystal");
            foreach (DimensionResolvedTileWrite write in result.Writes)
            {
                Assert.That(write.Tileset, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// The one case that must still refuse: a block flagged custom that names no tileset. There
        /// is no identity to be correct about, and falling back to tileset 0 would quietly generate
        /// dirt where the author asked for something else.
        /// </summary>
        [Test]
        public void ACustomBlockNamingNoTileset_IsSkippedAndReported()
        {
            DimensionBounds bounds = Bounds(0, 0, 2, 1);
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 2, 1);
            DimensionMapBlock broken = new DimensionMapBlock(
                "broken", "Broken", DimensionTileRole.Ground,
                DimensionBlockTilesetSource.Custom, 0, string.Empty);
            int index = map.AddBlock(broken);
            map.SetBlock(new int2(0, 0), index);
            map.SetBlock(new int2(1, 0), index);

            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), bounds, bounds);

            Assert.That(result.WriteCount, Is.EqualTo(0));
            Assert.That(result.Skipped.Count, Is.EqualTo(1));
        }

        [Test]
        public void NullPlacements_ProduceAnEmptyResult()
        {
            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(null, Bounds(0, 0, 1, 1), Bounds(0, 0, 1, 1));
            Assert.That(result.WriteCount, Is.EqualTo(0));
            Assert.That(result.Skipped, Is.Empty);
        }

        /// <summary>
        /// A cell carrying a wall grows nothing.
        /// </summary>
        /// <remarks>
        /// An overlay pass that tests only the write in front of it misses a painted cell holding
        /// a ground AND a wall — the wall written EARLIER in the same list — so the grass lands
        /// on top of the wall, which the pass's own comment calls nonsense.
        /// </remarks>
        [Test]
        public void OverlaysSkipACellThatAlsoTookAWall()
        {
            const int Tileset = 7;
            DimensionOverlayRuleRegistry.Clear();
            try
            {
                // Density 1 means every eligible cell, so anything skipped was skipped on purpose.
                DimensionOverlayRuleRegistry.Register(Tileset, new List<DimensionOverlayRule>
                {
                    new DimensionOverlayRule(LayerName.smallGrass, TileType.smallGrass, 1f)
                });

                DimensionBounds area = Bounds(0, 0, 4, 4);
                DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 4, 4);
                int ground = map.AddBlock(Block(DimensionTileRole.Ground, Tileset));
                int wall = map.AddBlock(Block(DimensionTileRole.Wall, Tileset));

                map.SetBlock(new int2(1, 1), ground);   // open floor
                map.SetBlock(new int2(2, 2), ground);   // floor with a wall standing on it
                map.SetBlock(new int2(2, 2), wall);

                DimensionTileMapCompileResult result =
                    DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), area, area);
                DimensionTileMapGenerationProvider.AppendScatteredOverlays("mod:cavern", result.Writes);

                Assert.That(
                    CountWrites(result.Writes, new int2(1, 1), TileType.smallGrass),
                    Is.EqualTo(1),
                    "Open floor should grow its block's grass.");
                Assert.That(
                    CountWrites(result.Writes, new int2(2, 2), TileType.smallGrass),
                    Is.EqualTo(0),
                    "A cell with a wall on it should grow nothing.");
            }
            finally
            {
                DimensionOverlayRuleRegistry.Clear();
            }
        }

        private static int CountWrites(
            List<DimensionResolvedTileWrite> writes,
            int2 position,
            TileType tileType)
        {
            int count = 0;
            for (int i = 0; i < writes.Count; i++)
            {
                if (writes[i].AbsolutePosition.Equals(position) && writes[i].TileType == tileType)
                {
                    count++;
                }
            }

            return count;
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
