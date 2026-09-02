#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The painted-map model is the shared foundation for the Biome/Map Creator and the
    /// generation system, so its behaviour is pinned here: ground and wall coexist per cell,
    /// out-of-bounds paints are refused, enumeration yields floors before walls, and a resize
    /// keeps the tiles that still fall inside the new region.
    /// </summary>
    internal sealed class DimensionTileMapModelTests
    {
        [Test]
        public void GroundAndWall_CoexistAtTheSameCell()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 4, 4);
            int ground = map.AddBlock(Block("dirt_ground", DimensionTileRole.Ground));
            int wall = map.AddBlock(Block("dirt_wall", DimensionTileRole.Wall));

            Assert.That(map.SetBlock(new int2(1, 1), ground), Is.True);
            Assert.That(map.SetBlock(new int2(1, 1), wall), Is.True);

            // Painting the wall must not erase the ground beneath it.
            Assert.That(
                map.GetBlockIndex(new int2(1, 1), DimensionMapLayer.Ground), Is.EqualTo(ground));
            Assert.That(
                map.GetBlockIndex(new int2(1, 1), DimensionMapLayer.Wall), Is.EqualTo(wall));
            Assert.That(map.PaintedTileCount(), Is.EqualTo(2));
        }

        [Test]
        public void PaintingOutsideBounds_IsRefused()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(10, 10), 4, 4);
            int ground = map.AddBlock(Block("g", DimensionTileRole.Ground));

            Assert.That(map.SetBlock(new int2(9, 10), ground), Is.False, "west of origin");
            Assert.That(map.SetBlock(new int2(14, 10), ground), Is.False, "east of extent");
            Assert.That(map.InBounds(new int2(10, 10)), Is.True);
            Assert.That(map.InBounds(new int2(13, 13)), Is.True);
            Assert.That(map.InBounds(new int2(14, 13)), Is.False);
            Assert.That(map.PaintedTileCount(), Is.EqualTo(0));
        }

        [Test]
        public void OriginIsRespected_LocalCoordinatesAreAbsoluteNotZeroBased()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(100, -50), 3, 3);
            int ground = map.AddBlock(Block("g", DimensionTileRole.Ground));
            Assert.That(map.SetBlock(new int2(101, -49), ground), Is.True);

            List<DimensionTilePlacement> placements = Placements(map);
            Assert.That(placements.Count, Is.EqualTo(1));
            Assert.That(placements[0].LocalPosition, Is.EqualTo(new int2(101, -49)));
        }

        [Test]
        public void Enumerate_YieldsGroundBeforeWall()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 2, 2);
            int wall = map.AddBlock(Block("w", DimensionTileRole.Wall));
            int ground = map.AddBlock(Block("g", DimensionTileRole.Ground));
            map.SetBlock(new int2(0, 0), wall);
            map.SetBlock(new int2(0, 0), ground);

            List<DimensionTilePlacement> placements = Placements(map);
            Assert.That(placements.Count, Is.EqualTo(2));
            // Floors must be written before the walls that sit on them.
            Assert.That(placements[0].Block.Layer, Is.EqualTo(DimensionMapLayer.Ground));
            Assert.That(placements[1].Block.Layer, Is.EqualTo(DimensionMapLayer.Wall));
        }

        [Test]
        public void ClearBlock_RemovesOnlyTheNamedLayer()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 2, 2);
            int ground = map.AddBlock(Block("g", DimensionTileRole.Ground));
            int wall = map.AddBlock(Block("w", DimensionTileRole.Wall));
            map.SetBlock(new int2(0, 0), ground);
            map.SetBlock(new int2(0, 0), wall);

            Assert.That(map.ClearBlock(new int2(0, 0), DimensionMapLayer.Wall), Is.True);
            Assert.That(map.GetBlockIndex(new int2(0, 0), DimensionMapLayer.Wall), Is.EqualTo(-1));
            Assert.That(
                map.GetBlockIndex(new int2(0, 0), DimensionMapLayer.Ground),
                Is.EqualTo(ground),
                "Clearing the wall must leave the ground.");
        }

        [Test]
        public void WaterAndPit_ArePaintableGroundLayerBlocks()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 3, 1);
            int water = map.AddBlock(Block("water", DimensionTileRole.Liquid));
            int pit = map.AddBlock(Block("void", DimensionTileRole.Pit));

            Assert.That(map.SetBlock(new int2(0, 0), water), Is.True);
            Assert.That(map.SetBlock(new int2(1, 0), pit), Is.True);

            Assert.That(
                map.GetBlockIndex(new int2(0, 0), DimensionMapLayer.Ground), Is.EqualTo(water));
            Assert.That(
                map.GetBlockIndex(new int2(1, 0), DimensionMapLayer.Ground), Is.EqualTo(pit));
        }

        [Test]
        public void Resize_KeepsTilesInsideTheNewRegionAndDropsTheRest()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 4, 4);
            int ground = map.AddBlock(Block("g", DimensionTileRole.Ground));
            map.SetBlock(new int2(0, 0), ground); // stays
            map.SetBlock(new int2(3, 3), ground); // dropped by the shrink

            map.Resize(int2.zero, 2, 2);

            Assert.That(map.GetBlockIndex(new int2(0, 0), DimensionMapLayer.Ground), Is.EqualTo(ground));
            Assert.That(map.InBounds(new int2(3, 3)), Is.False);
            Assert.That(map.PaintedTileCount(), Is.EqualTo(1));
        }

        [Test]
        public void CompiledBlock_CarriesTilesetSource()
        {
            DimensionMapBlock vanilla = new DimensionMapBlock(
                "stone", "Stone", DimensionTileRole.Wall,
                DimensionBlockTilesetSource.Vanilla, 3, string.Empty);
            DimensionMapBlock custom = new DimensionMapBlock(
                "mod_crystal", "Crystal", DimensionTileRole.Ground,
                DimensionBlockTilesetSource.Custom, 0, "mod:crystal");

            DimensionCompiledBlock v = vanilla.ToCompiled();
            Assert.That(v.TilesetSource, Is.EqualTo(DimensionBlockTilesetSource.Vanilla));
            Assert.That(v.VanillaTilesetIndex, Is.EqualTo(3));
            Assert.That(v.Layer, Is.EqualTo(DimensionMapLayer.Wall));

            DimensionCompiledBlock c = custom.ToCompiled();
            Assert.That(c.TilesetSource, Is.EqualTo(DimensionBlockTilesetSource.Custom));
            Assert.That(c.CustomTilesetId, Is.EqualTo("mod:crystal"));
            Assert.That(c.HasUnresolvedCustomTileset, Is.False);
        }

        [Test]
        public void ACustomBlockWithNoTilesetId_IsFlaggedNotSilentlyTileset0()
        {
            DimensionMapBlock block = new DimensionMapBlock(
                "broken", "Broken", DimensionTileRole.Ground,
                DimensionBlockTilesetSource.Custom, 0, string.Empty);

            Assert.That(block.ToCompiled().HasUnresolvedCustomTileset, Is.True);
        }

        [Test]
        public void SetBlock_WithAnInvalidPaletteIndex_IsRefused()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 2, 2);
            Assert.That(map.SetBlock(new int2(0, 0), 0), Is.False, "empty palette");
            Assert.That(map.SetBlock(new int2(0, 0), -1), Is.False);
            Assert.That(map.PaintedTileCount(), Is.EqualTo(0));
        }

        private static DimensionMapBlock Block(string id, DimensionTileRole role)
        {
            return new DimensionMapBlock(
                id, id, role, DimensionBlockTilesetSource.Vanilla, 0, string.Empty);
        }

        private static List<DimensionTilePlacement> Placements(DimensionTileMapModel map)
        {
            List<DimensionTilePlacement> list = new List<DimensionTilePlacement>();
            foreach (DimensionTilePlacement placement in map.EnumeratePlacements())
            {
                list.Add(placement);
            }

            return list;
        }
    }
}
#endif
