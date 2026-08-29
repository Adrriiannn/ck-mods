#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Generation;
using NUnit.Framework;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The parts of the Paint tab that are decidable without a window: the canvas coordinate
    /// round-trip, the size cap, and the two model behaviours the canvas leans on.
    /// </summary>
    /// <remarks>
    /// A canvas whose screen-to-tile map is off by one paints the wrong cell, and nothing about
    /// that looks wrong until a dimension generates. It is the one part of a painting tool worth
    /// pinning from outside the editor.
    /// </remarks>
    internal sealed class DimensionMapPaintPanelModelTests
    {
        private static readonly Rect Canvas = new Rect(20f, 40f, 320f, 320f);

        [Test]
        public void ScreenToTileIsTheExactInverseOfTileToScreen()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(-16, -16), 32, 32);

            int2[] probes =
            {
                new int2(-16, -16),  // bottom left
                new int2(15, -16),   // bottom right
                new int2(-16, 15),   // top left
                new int2(15, 15),    // top right
                new int2(0, 0)       // the local origin, near the middle
            };

            for (int i = 0; i < probes.Length; i++)
            {
                Vector2 point = DimensionMapPaintPanel.ToCanvas(Canvas, map, probes[i]);
                int2 back = DimensionMapPaintPanel.ToLocalTile(Canvas, map, point);
                Assert.That(back, Is.EqualTo(probes[i]), "Tile " + probes[i]);
            }
        }

        [Test]
        public void TileToScreenFlipsY_SoTheTopOfTheMapDrawsAtTheTopOfTheCanvas()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(0, 0), 8, 8);

            Vector2 bottom = DimensionMapPaintPanel.ToCanvas(Canvas, map, new int2(0, 0));
            Vector2 top = DimensionMapPaintPanel.ToCanvas(Canvas, map, new int2(0, 7));

            // Canvas space grows downward, so the higher tile has the smaller y.
            Assert.That(top.y, Is.LessThan(bottom.y));
        }

        [Test]
        public void APointOutsideTheCanvasClampsOntoTheMap()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(0, 0), 8, 8);

            int2 tile = DimensionMapPaintPanel.ToLocalTile(
                Canvas, map, new Vector2(Canvas.xMin - 500f, Canvas.yMin - 500f));

            Assert.That(map.InBounds(tile), Is.True);
        }

        [Test]
        public void ASizeWithinTheCapIsAccepted()
        {
            string refusal;
            Assert.That(
                DimensionMapPaintPanel.TryPlanSize(
                    DimensionMapPaintPanel.MaxSide, DimensionMapPaintPanel.MaxSide, out refusal),
                Is.True);
            Assert.That(refusal, Is.Empty);
        }

        [Test]
        public void ASizePastTheCapIsRefusedAndSaysWhy()
        {
            string refusal;
            bool ok = DimensionMapPaintPanel.TryPlanSize(
                DimensionMapPaintPanel.MaxSide + 1, 32, out refusal);

            Assert.That(ok, Is.False);
            Assert.That(refusal, Is.Not.Empty);
            Assert.That(refusal, Does.Contain(DimensionMapPaintPanel.MaxSide.ToString()));
        }

        [Test]
        public void AnEmptySizeIsRefused()
        {
            string refusal;
            Assert.That(DimensionMapPaintPanel.TryPlanSize(0, 32, out refusal), Is.False);
            Assert.That(refusal, Is.Not.Empty);
        }

        /// <summary>
        /// The Resize button promises the painting survives. It does, for everything still inside
        /// the new region — which is the whole reason the panel calls Resize and not SetBounds.
        /// </summary>
        [Test]
        public void ShrinkingTheMapKeepsWhatIsStillInsideItAndDropsTheRest()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(0, 0), 16, 16);
            int ground = map.AddBlock(Block(DimensionTileRole.Ground, 0));
            map.SetBlock(new int2(2, 2), ground);
            map.SetBlock(new int2(12, 12), ground);

            map.Resize(new int2(0, 0), 8, 8);

            Assert.That(map.GetBlockIndex(new int2(2, 2), DimensionMapLayer.Ground), Is.EqualTo(ground));
            Assert.That(map.GetBlockIndex(new int2(12, 12), DimensionMapLayer.Ground), Is.EqualTo(-1));
        }

        /// <summary>
        /// Painting a wall must not erase the floor under it — a mined-out wall has to leave ground
        /// behind, the way Core Keeper's own does. The model keeps them on separate layers; this
        /// pins the behaviour the canvas relies on when it draws walls inset over their ground.
        /// </summary>
        [Test]
        public void PaintingAWallOverGroundLeavesTheGroundReadable()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(0, 0), 8, 8);
            int ground = map.AddBlock(Block(DimensionTileRole.Ground, 0));
            int wall = map.AddBlock(Block(DimensionTileRole.Wall, 1));

            map.SetBlock(new int2(3, 3), ground);
            map.SetBlock(new int2(3, 3), wall);

            Assert.That(map.GetBlockIndex(new int2(3, 3), DimensionMapLayer.Ground), Is.EqualTo(ground));
            Assert.That(map.GetBlockIndex(new int2(3, 3), DimensionMapLayer.Wall), Is.EqualTo(wall));
        }

        /// <summary>
        /// A painted ore vein has to bring its own wall, and the wall has to be queued first: the
        /// ore tile type needs a wall at its cell, and the server drops a rejected ore as a loose
        /// item. This is the authoring side of that rule.
        /// </summary>
        [Test]
        public void AVeinCompilesToItsWallBeforeItsOre()
        {
            DimensionBounds local = new DimensionBounds(new int2(0, 0), new int2(8, 8));
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(0, 0), 8, 8);
            int vein = map.AddBlock(Block(DimensionTileRole.Vein, 3));
            map.SetBlock(new int2(4, 4), vein);

            DimensionTileMapCompileResult result =
                DimensionTileMapCompiler.Compile(map.EnumeratePlacements(), local, local);

            Assert.That(result.WriteCount, Is.EqualTo(2));
            Assert.That(result.Writes[0].TileType, Is.EqualTo(TileType.wall));
            Assert.That(result.Writes[1].TileType, Is.EqualTo(TileType.ore));
        }

        private static DimensionMapBlock Block(DimensionTileRole role, int vanillaTileset)
        {
            return new DimensionMapBlock(
                "b" + (int)role,
                "Block " + (int)role,
                role,
                DimensionBlockTilesetSource.Vanilla,
                vanillaTileset,
                string.Empty);
        }
    }
}
#endif
