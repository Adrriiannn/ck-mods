using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// A compositor tile-source backed by Core Keeper's captured Dirt tables (<see cref="DimensionTilesetAtlas"/>)
    /// sampling the real <c>dirt_tileset.png</c> pixels. Its purpose is VALIDATION: recompositing Dirt through
    /// <see cref="DimensionTilesetCompositor"/> must reproduce Core Keeper's shipped GEN sheet. It is also the
    /// worked example the modder-sheet source (Phase 2) mirrors — same interface, different pixel origin.
    ///
    /// Variant 0 only (the golden test proves the composition; full variant matching is a later refinement).
    /// All pixel work is Unity space (row 0 = bottom); UV rects from the atlas are y-up, so no flips.
    /// </summary>
    internal sealed class DimensionDirtTileSource : DimensionTilesetCompositor.ITileSource
    {
        private readonly LayerName layer;
        private readonly Color32[] src;
        private readonly int w;
        private readonly int h;

        public DimensionDirtTileSource(LayerName layer, Color32[] srcPixels, int width, int height)
        {
            this.layer = layer;
            this.src = srcPixels;
            this.w = width;
            this.h = height;
        }

        public bool IsAuthored(int mask)
        {
            return DimensionTilesetAtlas.IsAuthoredAdaptive(layer, mask);
        }

        // Core Keeper's baked GEN uses FEW variants per mask (~1.25 avg): authored masks keep their own
        // 9-way variants; composited (corner/edge/end-cap) masks get a single baked version. The STD/SUB
        // SOURCE tables carry up to ~10 pieces, but the bake does not expand every combination — emitting
        // them all would invent thousands of tiles vanilla never ships. So: authored → its variant count;
        // composited → 1. (Custom tilesets get their variety from however many variants the modder authors.)
        public int VariantCount(int mask)
        {
            return IsAuthored(mask)
                ? Mathf.Max(1, DimensionTilesetAtlas.AdaptiveVariantCount(layer, mask))
                : 1;
        }

        public Color32[] GetAuthoredTile(int mask, int variant)
        {
            return DimensionTilesetAtlas.TryGetAdaptiveUV(layer, mask, variant, out Rect uv)
                ? Extract(uv, DimensionTilesetCompositor.Tile)
                : null;
        }

        public Color32[] GetQuarter(int subMask, int variant)
        {
            return DimensionTilesetAtlas.TryGetSubtileUV(layer, subMask, variant, out Rect uv)
                ? Extract(uv, DimensionTilesetCompositor.Quarter)
                : null;
        }

        // The learned recipe's UVs are template-relative, so this samples the CURRENT sheet (dirt for the
        // golden test, the modder's sheet for generation) at vanilla's exact quarter positions — custom
        // tilesets inherit vanilla's per-mask quarter picks with their own art.
        public Color32[] GetLearnedQuarter(int mask, int variant, int corner)
        {
            return DimensionTilesetAtlas.TryGetLearnedQuarterUV(layer, mask, variant, corner, out Rect uv)
                ? Extract(uv, DimensionTilesetCompositor.Quarter)
                : null;
        }

        // A size×size RGBA block from the source, read as a PLAIN size-pixel cell in IMAGE ORDER (row 0 =
        // top). The GEN cells are 16px (quarters 8px); we DON'T resample the fractional (~15.75px) UV width —
        // Core Keeper's bake, and our proven Node path, copy `size` consecutive pixels from the sprite's
        // rounded top-left. Resampling by the fractional width duplicated column 0 and smeared every tile
        // (~30 RMSE, 0 matches). Top-left = (round(uv.x*w), round((1-(uv.y+uv.h))*h)).
        private Color32[] Extract(Rect uv, int size)
        {
            var outPx = new Color32[size * size];
            int x0 = Mathf.RoundToInt(uv.x * w);
            int y0 = Mathf.RoundToInt((1f - (uv.y + uv.height)) * h);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int sx = Mathf.Clamp(x0 + x, 0, w - 1);
                    int sy = Mathf.Clamp(y0 + y, 0, h - 1);
                    outPx[y * size + x] = src[sy * w + sx];
                }
            }

            return outPx;
        }
    }
}
