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
    /// The in-game proof is only meaningful if the debug shape actually contains all four tile
    /// kinds and a non-rectangular outline, so that is verified here before anyone builds.
    /// </summary>
    internal sealed class DimensionTileMapDebugTests
    {
        [Test]
        public void ProofShape_ContainsGroundWallWaterAndVoid()
        {
            DimensionTileMapModel map = DimensionTileMapDebug.BuildProofShape();

            HashSet<TileType> types = new HashSet<TileType>();
            DimensionBounds bounds = map.LocalBounds;
            foreach (DimensionTilePlacement placement in map.EnumeratePlacements())
            {
                types.Add(DimensionBlockTileMapping.ToTileType(placement.Block.Role));
            }

            Assert.That(types, Does.Contain(TileType.ground));
            Assert.That(types, Does.Contain(TileType.wall));
            Assert.That(types, Does.Contain(TileType.water));
            Assert.That(types, Does.Contain(TileType.pit));
        }

        [Test]
        public void ProofShape_IsADiscNotAFilledSquare()
        {
            DimensionTileMapModel map = DimensionTileMapDebug.BuildProofShape();

            // The four corners of the bounding square are outside the disc, so they must be empty
            // — that is what makes this an arbitrary shape rather than the old flat rectangle.
            DimensionBounds bounds = map.LocalBounds;
            int2 corner = new int2(bounds.Min.x, bounds.Min.y);
            Assert.That(map.GetBlockIndex(corner, DimensionMapLayer.Ground), Is.EqualTo(-1));

            // The centre is inside the disc, so it has ground.
            Assert.That(
                map.GetBlockIndex(int2.zero, DimensionMapLayer.Ground),
                Is.Not.EqualTo(-1));
        }

        [Test]
        public void ProofShape_CompilesToWritesAtAbsoluteCoordinates()
        {
            DimensionTileMapModel map = DimensionTileMapDebug.BuildProofShape();
            DimensionBounds local = map.LocalBounds;
            DimensionBounds absolute = new DimensionBounds(
                local.Min + new int2(5000, 0), local.MaxExclusive + new int2(5000, 0));

            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), local, absolute);

            Assert.That(result.WriteCount, Is.GreaterThan(0));
            Assert.That(result.Skipped, Is.Empty, "Vanilla tileset 0 should always resolve.");
        }
    }
}
#endif
