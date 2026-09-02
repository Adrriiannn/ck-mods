using System.Collections.Generic;
using ExpandNullforge.EditorTools.Generation;
using ExpandNullforge.Tilesets;
using NUnit.Framework;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Proves the tileset compositor still reproduces Core Keeper's own bake, exactly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the guarantee every custom tileset rests on. The compositor does not decorate vanilla's
    /// output — it replaces it: a custom tileset's ground and wall render from a sheet this code bakes,
    /// through the 256-neighbour-mask atlas the engine samples. If the bake drifts, every custom block
    /// in every mod built afterwards renders subtly wrong, in ways that show up as "the corners look
    /// off" screenshots rather than as an error.
    /// </para>
    /// <para>
    /// The test regenerates Dirt — the one tileset whose source art, captured tables and shipped result
    /// are all available offline — and requires that every tile it produces is byte-identical to a tile
    /// Core Keeper actually shipped. Vanilla is the oracle: matching it is the whole claim.
    /// </para>
    /// <para>
    /// The counts below are measured, not aspirational. A change to them is a real change in what the
    /// compositor emits and deserves a look, not a number bump.
    /// </para>
    /// <para>
    /// The sweep covers every vanilla tileset whose source art and shipped result are both on disk —
    /// 24 of them — all regenerated from Dirt's one captured template. That the template generalises is
    /// itself part of what is being asserted.
    /// </para>
    /// <para>
    /// A MISSING CAPTURE IS A FAILURE, NOT A SKIP. A precondition that calls
    /// <c>Assert.Ignore</c> reports Ignored on a machine with no captured tables and no shipped
    /// sheets, and the suite reads green — the project's strongest claim proving nothing
    /// while looking proven. The preconditions here fail instead, and say what to fetch.
    /// </para>
    /// </remarks>
    public sealed class DimensionTilesetGenerationTests
    {
        /// <summary>What to do when the captured Dirt tables are not on this machine.</summary>
        private const string MissingCaptureInstructions =
            "The tables come from DimensionTilesetAtlasCapture, which writes them the first time the " +
            "mod runs in game: build and install the mod, load any world once, then run these tests " +
            "again. Until then this claim is unproven, which is what the red is for.";

        /// <summary>What to do when a shipped Core Keeper sheet is not on this machine.</summary>
        private const string MissingArtInstructions =
            "The sheets are looked up by name through the project and then the Core Keeper assets " +
            "package (DimensionTilesetGoldenAssets.FindFile); install that package, or point it at a " +
            "copy, so vanilla is available as the oracle.";

        /// <summary>Tiles Dirt's ground layer bakes to — 256 masks plus their extra variants.</summary>
        private const int GroundTileCount = 484;

        /// <summary>Tiles Dirt's wall layer bakes to.</summary>
        private const int WallTileCount = 479;

        [Test]
        public void RegeneratedDirtGroundIsByteIdenticalToTheShippedSheet()
        {
            AssertLayerMatchesVanilla(LayerName.ground, "tileset0_ground_state0", GroundTileCount);
        }

        [Test]
        public void RegeneratedDirtWallIsByteIdenticalToTheShippedSheet()
        {
            AssertLayerMatchesVanilla(LayerName.wall, "tileset0_wall_state0", WallTileCount);
        }

        [Test]
        public void EveryNeighbourMaskGetsATileToDrawWith()
        {
            foreach (LayerName layer in new[] { LayerName.ground, LayerName.wall })
            {
                DimensionTilesetCompositor.GenLayer gen = Regenerate(layer);
                for (int mask = 0; mask < 256; mask++)
                {
                    Assert.Greater(
                        gen.Uvs[mask].Count,
                        0,
                        layer + " mask " + mask + " has no tile. The engine samples the atlas by " +
                        "neighbour mask with no fallback, so that arrangement of blocks would render " +
                        "as a hole in the world.");
                }
            }
        }

        [Test]
        public void NoGeneratedTileIsBlank()
        {
            foreach (LayerName layer in new[] { LayerName.ground, LayerName.wall })
            {
                DimensionTilesetCompositor.GenLayer gen = Regenerate(layer);
                for (int mask = 0; mask < 256; mask++)
                {
                    for (int variant = 0; variant < gen.Uvs[mask].Count; variant++)
                    {
                        Color32[] tile = DimensionTilesetGoldenAssets.CellFromUv(
                            gen.Pixels, gen.Width, gen.Height, gen.Uvs[mask][variant]);

                        bool anyOpaque = false;
                        for (int i = 0; i < tile.Length; i++)
                        {
                            if (tile[i].a >= 128)
                            {
                                anyOpaque = true;
                                break;
                            }
                        }

                        Assert.IsTrue(
                            anyOpaque,
                            layer + " mask " + mask + " variant " + variant + " is fully transparent. " +
                            "A UV that points at empty atlas renders as nothing, which reads in game as " +
                            "a missing block rather than as a bug.");
                    }
                }
            }
        }

        [Test]
        public void EveryUvPointsInsideTheAtlas()
        {
            foreach (LayerName layer in new[] { LayerName.ground, LayerName.wall })
            {
                DimensionTilesetCompositor.GenLayer gen = Regenerate(layer);
                for (int mask = 0; mask < 256; mask++)
                {
                    for (int variant = 0; variant < gen.Uvs[mask].Count; variant++)
                    {
                        Rect uv = gen.Uvs[mask][variant];
                        int left = Mathf.RoundToInt(uv.x * gen.Width);
                        int bottom = Mathf.RoundToInt(uv.y * gen.Height);

                        Assert.GreaterOrEqual(left, 0, layer + " mask " + mask + " starts left of the atlas.");
                        Assert.GreaterOrEqual(bottom, 0, layer + " mask " + mask + " starts below the atlas.");
                        Assert.LessOrEqual(
                            left + DimensionTilesetGoldenAssets.Tile,
                            gen.Width,
                            layer + " mask " + mask + " runs off the right edge of the atlas; the tile " +
                            "would sample whatever is packed at the far left of the same row.");
                        Assert.LessOrEqual(
                            bottom + DimensionTilesetGoldenAssets.Tile,
                            gen.Height,
                            layer + " mask " + mask + " runs off the top edge of the atlas.");
                    }
                }
            }
        }

        /// <summary>
        /// The mint guarantee: the same tileset must build to the same bytes every time.
        /// </summary>
        /// <remarks>
        /// A modder builds, ships, then rebuilds to change one unrelated thing. If the bake is not
        /// deterministic, the second build's art differs from the first for no reason anyone can see or
        /// explain — and the usual causes (hash-ordered iteration, reused scratch buffers, floating-point
        /// accumulation) produce differences small enough to survive review and large enough to notice
        /// in game.
        /// </remarks>
        [Test]
        public void RegeneratingTheSameLayerTwiceProducesIdenticalBytes()
        {
            foreach (LayerName layer in new[] { LayerName.ground, LayerName.wall })
            {
                DimensionTilesetCompositor.GenLayer first = Regenerate(layer);
                DimensionTilesetCompositor.GenLayer second = Regenerate(layer);

                Assert.AreEqual(first.Width, second.Width, layer + " atlas width changed between runs.");
                Assert.AreEqual(first.Height, second.Height, layer + " atlas height changed between runs.");
                Assert.AreEqual(
                    first.Pixels.Length,
                    second.Pixels.Length,
                    layer + " atlas pixel count changed between runs.");

                for (int i = 0; i < first.Pixels.Length; i++)
                {
                    Color32 a = first.Pixels[i];
                    Color32 b = second.Pixels[i];
                    if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a)
                    {
                        Assert.Fail(
                            layer + " atlas pixel " + i + " differs between two runs of the same input (" +
                            a + " then " + b + "). Generation must be reproducible, or a rebuild silently " +
                            "changes a shipped mod's art.");
                    }
                }

                for (int mask = 0; mask < 256; mask++)
                {
                    Assert.AreEqual(
                        first.Uvs[mask].Count,
                        second.Uvs[mask].Count,
                        layer + " mask " + mask + " produced a different number of variants between runs.");

                    for (int variant = 0; variant < first.Uvs[mask].Count; variant++)
                    {
                        Assert.AreEqual(
                            first.Uvs[mask][variant],
                            second.Uvs[mask][variant],
                            layer + " mask " + mask + " variant " + variant + " landed at a different " +
                            "place in the atlas between runs.");
                    }
                }
            }
        }

        /// <summary>
        /// Every vanilla tileset, paired with the index its shipped GEN sheets are named by.
        /// </summary>
        /// <remarks>
        /// Discovered by regenerating each source sheet and finding which shipped atlas it reproduces —
        /// so the pairing is measured from the art itself, not read off a table that could go stale.
        /// The recolour sheets (paintable_*) and the two sheets with no shipped GEN of their own are
        /// deliberately absent: there is nothing to compare them against.
        /// </remarks>
        private static readonly (string Source, int Index)[] VanillaTilesets =
        {
            ("alien", 56), ("city", 24), ("clay", 11), ("crystal", 55), ("darkstone", 54),
            ("desertcity", 27), ("desert", 26), ("dirt", 0), ("excavation_void", 74),
            ("industrial_border", 72), ("industrial_puzzle", 70), ("industrial_rock", 71),
            ("industrial_wall_red", 69), ("larva_hive", 6), ("lava", 3), ("mold_dungeon", 9),
            ("nature", 8), ("oasis", 66), ("passage", 60), ("sand", 12), ("sea", 10),
            ("snow", 31), ("stone", 1), ("turf", 13)
        };

        /// <summary>
        /// The whole architecture in one assertion: Dirt's captured tables reproduce EVERY vanilla
        /// tileset's ground, not just Dirt's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what makes a single captured template enough. If the tables only described Dirt,
        /// every custom tileset would need its own in-game capture before it could render, and a modder
        /// drawing art that did not happen to match Dirt's cell layout would get silent drift at the
        /// corners. That all 24 vanilla tilesets bake byte-identically from one capture is the evidence
        /// that the layout is shared and the compositor reads it correctly.
        /// </para>
        /// <para>
        /// Losing this is the single worst regression possible in the tileset pipeline, and it would
        /// otherwise only show up as odd-looking corners in someone's screenshot.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryVanillaTilesetsGroundRegeneratesFromTheOneCapturedTemplate()
        {
            AssertEveryVanillaTilesetMatches(LayerName.ground, "ground", GroundTileCount, VanillaTilesets);
        }

        /// <summary>
        /// The same guarantee for walls, which took a different recipe to reach.
        /// </summary>
        /// <remarks>
        /// A quarter recipe mined from Dirt's own bake reproduces only 4 of the 24 walls: Dirt is
        /// the outlier, and 20 other tilesets keep some wall quarters at
        /// different places in their sheets. The recipe is chosen per slot to satisfy every tileset
        /// at once, which is exactly solvable because a composited tile is four independent quarters.
        /// </remarks>
        [Test]
        public void EveryVanillaTilesetsWallRegeneratesFromTheOneCapturedTemplate()
        {
            AssertEveryVanillaTilesetMatches(LayerName.wall, "wall", WallTileCount, VanillaTilesets);
        }

        private static void AssertEveryVanillaTilesetMatches(
            LayerName layer,
            string genSuffix,
            int expectedTiles,
            (string Source, int Index)[] tilesets)
        {
            int checkedCount = 0;
            List<string> failures = new List<string>();

            foreach ((string source, int index) in tilesets)
            {
                Color32[] src;
                int sw, sh;
                if (!DimensionTilesetGoldenAssets.TryLoadPixels(source + "_tileset", out src, out sw, out sh))
                {
                    continue;
                }

                string shippedName = "tileset" + index + "_" + genSuffix + "_state0";
                Color32[] shipped;
                int gw, gh;
                if (!DimensionTilesetGoldenAssets.TryLoadPixels(shippedName, out shipped, out gw, out gh))
                {
                    continue;
                }

                checkedCount++;
                DimensionTilesetCompositor.GenLayer gen =
                    DimensionTilesetGoldenAssets.RegenerateVanillaLayer(layer, src, sw, sh);

                int total = 0;
                int identical = 0;
                float worst = 0f;
                for (int mask = 0; mask < 256; mask++)
                {
                    for (int variant = 0; variant < gen.Uvs[mask].Count; variant++)
                    {
                        total++;
                        Color32[] tile = DimensionTilesetGoldenAssets.CellFromUv(
                            gen.Pixels, gen.Width, gen.Height, gen.Uvs[mask][variant]);
                        float best = DimensionTilesetGoldenAssets.BestRmseAnywhere(tile, shipped, gw, gh);
                        if (best <= 0f)
                        {
                            identical++;
                        }
                        else if (best > worst)
                        {
                            worst = best;
                        }
                    }
                }

                if (total != expectedTiles || identical != total)
                {
                    failures.Add(
                        source + " -> " + shippedName + ": " + identical + "/" + total +
                        " tiles byte-identical (worst RMSE " + worst.ToString("F2") + ")");
                }
            }

            if (checkedCount == 0)
            {
                Assert.Fail(
                    "None of Core Keeper's shipped " + genSuffix + " sheets were found, so this test " +
                    "compared nothing and would otherwise have reported green. " + MissingArtInstructions);
            }

            Assert.IsEmpty(
                failures,
                "Regenerating " + layer + " from the captured template no longer reproduces vanilla for " +
                failures.Count + " of " + checkedCount + " tilesets:\n  " + string.Join("\n  ", failures));
        }

        private static void AssertLayerMatchesVanilla(LayerName layer, string shippedName, int expectedTiles)
        {
            DimensionTilesetCompositor.GenLayer gen = Regenerate(layer);

            Color32[] shipped;
            int sw, sh;
            if (!DimensionTilesetGoldenAssets.TryLoadPixels(shippedName, out shipped, out sw, out sh))
            {
                Assert.Fail(
                    "Core Keeper's shipped " + shippedName + ".png was not found, so this test compared " +
                    "nothing. " + MissingArtInstructions);
            }

            int total = 0;
            int identical = 0;
            float worst = 0f;
            int worstMask = -1;
            int worstVariant = -1;

            for (int mask = 0; mask < 256; mask++)
            {
                for (int variant = 0; variant < gen.Uvs[mask].Count; variant++)
                {
                    total++;
                    Color32[] tile = DimensionTilesetGoldenAssets.CellFromUv(
                        gen.Pixels, gen.Width, gen.Height, gen.Uvs[mask][variant]);
                    float best = DimensionTilesetGoldenAssets.BestRmseAnywhere(tile, shipped, sw, sh);
                    if (best <= 0f)
                    {
                        identical++;
                    }
                    else if (best > worst)
                    {
                        worst = best;
                        worstMask = mask;
                        worstVariant = variant;
                    }
                }
            }

            Assert.AreEqual(
                expectedTiles,
                total,
                layer + " baked " + total + " tiles where " + expectedTiles + " were expected. The atlas " +
                "layout or the variant counts changed; confirm that is intended before updating the number.");

            Assert.AreEqual(
                total,
                identical,
                layer + ": " + (total - identical) + " of " + total + " generated tiles are not " +
                "byte-identical to any tile Core Keeper shipped (worst is mask " + worstMask + " variant " +
                worstVariant + " at RMSE " + worst.ToString("F2") + "). Run " +
                "Dimensions API ▸ Debug ▸ Golden Test to write the regenerated atlas out and compare it " +
                "against " + shippedName + ".png.");
        }

        private static DimensionTilesetCompositor.GenLayer Regenerate(LayerName layer)
        {
            if (!DimensionTilesetAtlas.IsReady)
            {
                Assert.Fail(
                    "The captured Dirt tables are not loaded (" + DimensionTilesetAtlas.Status +
                    "), so this test regenerated nothing. " + MissingCaptureInstructions);
            }

            Color32[] dirt;
            int dw, dh;
            if (!DimensionTilesetGoldenAssets.TryLoadPixels("dirt_tileset", out dirt, out dw, out dh))
            {
                Assert.Fail(
                    "dirt_tileset.png was not found, so this test regenerated nothing. " +
                    MissingArtInstructions);
            }

            return DimensionTilesetGoldenAssets.RegenerateVanillaLayer(layer, dirt, dw, dh);
        }
    }
}
