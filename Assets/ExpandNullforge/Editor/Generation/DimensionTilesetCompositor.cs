using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// Core Keeper's own adaptive-tile compositor, reproduced from the decompiled bake
    /// (<c>QuadGeneratorExtensions.ResolveQuad</c> + <c>AdjacentDir.Get*SubCornerBitMask</c>). Given a
    /// per-mask tile source, it bakes the full 256-neighbour-mask "GEN" atlas one layer at a time — the
    /// exact madness sheet the game samples at runtime — plus the UV buckets that index it.
    ///
    /// The rule (validated offline against Core Keeper's shipped GEN sheets — with the learned recipe the
    /// full regenerated Dirt GEN is byte-identical, 484/484 ground and 479/479 wall cells): for each mask,
    /// if the 9-way table authored it, use that whole tile; otherwise composite four 8×8 quarters
    /// (TL/TR/BL/BR, NO mirroring) — preferring the LEARNED per-(mask,variant) recipe mined from vanilla's
    /// own bake, falling back to the derived sub-corner-mask rule. The raw mask is first
    /// restricted to the layer's connectivity so an orthogonal-only layer never invents diagonal combos.
    ///
    /// Everything here works in IMAGE ORDER (row 0 = TOP, y down) — the same order the raw PNG and the
    /// vanilla bake use. This matters: the source sprites are ~15.75px, and resampling to 16px with floor
    /// duplicates a pixel at the sampling start, so the sample DIRECTION must match vanilla (top-first) or
    /// nothing lines up. Callers feed pixels in image order (flip <c>GetPixels32</c> once on load) and flip
    /// back once when building the final <c>Texture2D</c>. Output is plain pixels + UV rects.
    /// </summary>
    internal static class DimensionTilesetCompositor
    {
        internal const int Tile = 16;
        internal const int Quarter = 8;

        // AdjacentDir bit layout: E=1 SE=2 S=4 SW=8 W=16 NW=32 N=64 NE=128.
        // Per-quadrant canonical sub-corner masks (AdjacentDir.Get*SubCornerBitMask), verbatim.
        internal static int SubTL(int d) { int n = 7; if ((d & 16) != 0) { n |= 16; n |= 8; } if ((d & 64) != 0) { n |= 64; n |= 128; } if ((d & 32) != 0) { n |= 32; } return n; }
        internal static int SubTR(int d) { int n = 28; if ((d & 1) != 0) { n |= 1; n |= 2; } if ((d & 64) != 0) { n |= 64; n |= 32; } if ((d & 128) != 0) { n |= 128; } return n; }
        internal static int SubBL(int d) { int n = 193; if ((d & 16) != 0) { n |= 16; n |= 32; } if ((d & 4) != 0) { n |= 4; n |= 2; } if ((d & 8) != 0) { n |= 8; } return n; }
        internal static int SubBR(int d) { int n = 112; if ((d & 1) != 0) { n |= 1; n |= 128; } if ((d & 4) != 0) { n |= 4; n |= 8; } if ((d & 2) != 0) { n |= 2; } return n; }

        /// <summary>
        /// Per-mask tile provider. WHERE the pixels come from (captured Dirt tables, or a modder's authored
        /// sheet at template positions) is the implementation's concern; the compositor stays layout-agnostic.
        /// All returned pixel arrays are row-major, row 0 = bottom (Unity <c>GetPixels32</c> order).
        /// </summary>
        internal interface ITileSource
        {
            /// <summary>Whether the 9-way table authored a full tile for this exact (connectivity-masked) mask.</summary>
            bool IsAuthored(int mask);

            /// <summary>Number of variants a tile of this mask should emit (≥1). Drives the GEN variant list.</summary>
            int VariantCount(int mask);

            /// <summary><see cref="Tile"/>²  RGBA of the authored full tile (only called when <see cref="IsAuthored"/>).</summary>
            Color32[] GetAuthoredTile(int mask, int variant);

            /// <summary><see cref="Quarter"/>²  RGBA of the sub-corner quarter for <paramref name="subMask"/>, or null.</summary>
            Color32[] GetQuarter(int subMask, int variant);

            /// <summary>
            /// <see cref="Quarter"/>²  RGBA of the LEARNED quarter for a composited (mask, variant) at
            /// <paramref name="corner"/> (0 TL, 1 TR, 2 BL, 3 BR) — the exact source rect vanilla's own bake
            /// picked, mined from its shipped GEN sheets. Null when no recipe was learned; the compositor
            /// then falls back to the derived sub-corner-mask rule. Recipes are stored all-or-nothing per
            /// (mask, variant), so corner 0 non-null implies all four are.
            /// </summary>
            Color32[] GetLearnedQuarter(int mask, int variant, int corner);
        }

        internal sealed class GenLayer
        {
            public Color32[] Pixels;
            public int Width;
            public int Height;
            /// <summary>256 buckets; bucket[mask] = that mask's variant UV rects (0-1, y-up) into the atlas.</summary>
            public List<List<Rect>> Uvs;
        }

        /// <summary>
        /// Vanilla's canonical output layout for a layer: the fixed texture size, per-mask variant count, and
        /// each variant's exact 16px cell (image order, row 0 = top). Packing into this makes the output a real
        /// Core Keeper tileset (portable to other mods), not a framework-only sheet.
        /// </summary>
        internal interface IGenLayout
        {
            int Width { get; }
            int Height { get; }
            int VariantCount(int mask);
            bool TryGetCell(int mask, int variant, out int leftPx, out int topPx);
        }

        /// <summary>
        /// Bake all 256 neighbour masks (each with its variants) for one layer into a packed atlas.
        /// <paramref name="connectBits"/> restricts each raw mask to the layer's real connectivity (e.g.
        /// slime = 85) before resolving — matching the game's <c>num &amp;= dirBits</c>.
        /// </summary>
        internal static GenLayer Composite(ITileSource src, int connectBits, IGenLayout layout = null)
        {
            if (layout != null)
            {
                return PackToLayout(src, connectBits, layout);
            }

            var tiles = new List<Color32[]>();          // each Tile² , unique baked tiles
            var perMask = new List<List<int>>(256);     // perMask[mask] = tile indices for its variants
            for (int i = 0; i < 256; i++)
            {
                perMask.Add(new List<int>());
            }

            Color32[] interior = null; // mask 255 fallback so no mask is ever empty/invisible

            for (int mask = 0; mask < 256; mask++)
            {
                int mm = mask & connectBits & 0xFF;
                int variants = Mathf.Max(1, src.VariantCount(mm));
                for (int v = 0; v < variants; v++)
                {
                    Color32[] tile = src.IsAuthored(mm) ? src.GetAuthoredTile(mm, v) : CompositeQuarters(src, mm, v);
                    if (tile == null)
                    {
                        continue;
                    }

                    if (mm == 0xFF && interior == null)
                    {
                        interior = tile;
                    }

                    perMask[mask].Add(tiles.Count);
                    tiles.Add(tile);
                }
            }

            // Guarantee every mask has at least one entry (fall back to the fully-surrounded interior tile).
            if (interior == null && tiles.Count > 0)
            {
                interior = tiles[0];
            }

            for (int mask = 0; mask < 256; mask++)
            {
                if (perMask[mask].Count == 0 && interior != null)
                {
                    perMask[mask].Add(tiles.Count);
                    tiles.Add(interior);
                }
            }

            return Pack(tiles, perMask);
        }

        private static Color32[] MakeTile(ITileSource src, int mask, int variant)
        {
            return src.IsAuthored(mask) ? src.GetAuthoredTile(mask, variant) : CompositeQuarters(src, mask, variant);
        }

        // Pack every (mask, variant) tile into VANILLA'S exact cell positions on its canonical sheet — the
        // output is then a real Core Keeper tileset (same size, layout, variant counts), portable to any mod.
        private static GenLayer PackToLayout(ITileSource src, int connectBits, IGenLayout layout)
        {
            int w = layout.Width;
            int h = layout.Height;
            var px = new Color32[w * h];
            var uvs = new List<List<Rect>>(256);
            for (int i = 0; i < 256; i++)
            {
                uvs.Add(new List<Rect>());
            }

            for (int mask = 0; mask < 256; mask++)
            {
                int mm = mask & connectBits & 0xFF;
                int n = layout.VariantCount(mask);
                for (int v = 0; v < n; v++)
                {
                    if (!layout.TryGetCell(mask, v, out int lx, out int ty))
                    {
                        continue;
                    }

                    Color32[] tile = MakeTile(src, mm, v);
                    if (tile == null)
                    {
                        continue;
                    }

                    for (int y = 0; y < Tile; y++)
                    {
                        for (int x = 0; x < Tile; x++)
                        {
                            int dx = lx + x;
                            int dy = ty + y;
                            if (dx >= 0 && dy >= 0 && dx < w && dy < h)
                            {
                                px[dy * w + dx] = tile[y * Tile + x];
                            }
                        }
                    }

                    uvs[mask].Add(new Rect(lx / (float)w, ty / (float)h, Tile / (float)w, Tile / (float)h));
                }
            }

            return new GenLayer { Pixels = px, Width = w, Height = h, Uvs = uvs };
        }

        // NW/NE/SW/SE ← TL/TR/BL/BR quarters. In image order (row 0 = top) north is the top rows.
        // The LEARNED per-(mask,variant) recipe — vanilla's own quarter picks, mined byte-identical from its
        // shipped GEN sheets — wins when present; the derived sub-corner-mask rule remains the fallback for
        // unlearned masks/layers. The derived rule is VALID but picks different (visibly mismatched) source
        // quarters than vanilla for thin runs like mask 68 (N|S) / 17 (E|W).
        private static Color32[] CompositeQuarters(ITileSource src, int mask, int variant)
        {
            Color32[] nw = src.GetLearnedQuarter(mask, variant, 0);
            Color32[] ne, sw, se;
            if (nw != null)
            {
                ne = src.GetLearnedQuarter(mask, variant, 1);
                sw = src.GetLearnedQuarter(mask, variant, 2);
                se = src.GetLearnedQuarter(mask, variant, 3);
            }
            else
            {
                nw = src.GetQuarter(SubTL(mask), variant);
                ne = src.GetQuarter(SubTR(mask), variant);
                sw = src.GetQuarter(SubBL(mask), variant);
                se = src.GetQuarter(SubBR(mask), variant);
            }

            if (nw == null && ne == null && sw == null && se == null)
            {
                return null;
            }

            var tile = new Color32[Tile * Tile];
            PlaceQuarter(tile, nw, 0, 0);              // north-west: left half, TOP half (rows 0-7)
            PlaceQuarter(tile, ne, Quarter, 0);        // north-east: right half, top half
            PlaceQuarter(tile, sw, 0, Quarter);        // south-west: left half, BOTTOM half (rows 8-15)
            PlaceQuarter(tile, se, Quarter, Quarter);  // south-east: right half, bottom half
            return tile;
        }

        private static void PlaceQuarter(Color32[] tile, Color32[] q, int x0, int y0)
        {
            if (q == null)
            {
                return;
            }

            for (int y = 0; y < Quarter; y++)
            {
                for (int x = 0; x < Quarter; x++)
                {
                    tile[(y0 + y) * Tile + (x0 + x)] = q[y * Quarter + x];
                }
            }
        }

        private static GenLayer Pack(List<Color32[]> tiles, List<List<int>> perMask)
        {
            int count = Mathf.Max(1, tiles.Count);
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.CeilToInt(count / (float)cols);
            int w = cols * Tile;
            int h = rows * Tile;
            var px = new Color32[w * h];

            for (int i = 0; i < tiles.Count; i++)
            {
                int cx = (i % cols) * Tile;
                int cy = (i / cols) * Tile; // row 0 = bottom
                Color32[] t = tiles[i];
                for (int y = 0; y < Tile; y++)
                {
                    for (int x = 0; x < Tile; x++)
                    {
                        px[(cy + y) * w + (cx + x)] = t[y * Tile + x];
                    }
                }
            }

            var uvs = new List<List<Rect>>(256);
            for (int mask = 0; mask < 256; mask++)
            {
                var bucket = new List<Rect>();
                foreach (int ti in perMask[mask])
                {
                    int cx = (ti % cols) * Tile;
                    int cy = (ti / cols) * Tile;
                    bucket.Add(new Rect(cx / (float)w, cy / (float)h, Tile / (float)w, Tile / (float)h));
                }

                uvs.Add(bucket);
            }

            return new GenLayer { Pixels = px, Width = w, Height = h, Uvs = uvs };
        }
    }
}
