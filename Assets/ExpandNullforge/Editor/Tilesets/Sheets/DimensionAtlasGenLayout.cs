using ExpandNullforge.Tilesets;
using PugTilemap;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// <see cref="DimensionTilesetCompositor.IGenLayout"/> backed by the captured vanilla GEN packing for one
    /// layer — the canonical, shared-across-all-tilesets cell layout. Packing into it makes generated output a
    /// real Core Keeper tileset (same size/positions/variant counts), so it works in any tileset-using mod.
    /// </summary>
    internal sealed class DimensionAtlasGenLayout : DimensionTilesetCompositor.IGenLayout
    {
        private readonly LayerName layer;

        public int Width { get; }

        public int Height { get; }

        private DimensionAtlasGenLayout(LayerName layer, int width, int height)
        {
            this.layer = layer;
            Width = width;
            Height = height;
        }

        /// <summary>Null if vanilla's GEN packing wasn't captured for this layer (caller falls back to its own packing).</summary>
        public static DimensionAtlasGenLayout TryCreate(LayerName layer)
        {
            return DimensionTilesetAtlas.TryGetGenSize(layer, out int w, out int h)
                ? new DimensionAtlasGenLayout(layer, w, h)
                : null;
        }

        public int VariantCount(int mask)
        {
            return DimensionTilesetAtlas.GenVariantCount(layer, mask);
        }

        public bool TryGetCell(int mask, int variant, out int leftPx, out int topPx)
        {
            return DimensionTilesetAtlas.TryGetGenCell(layer, mask, variant, out leftPx, out topPx);
        }
    }
}
