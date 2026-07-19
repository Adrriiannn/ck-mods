using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Dimension Layout Template")]
    public sealed class DimensionLayoutTemplateAsset : ScriptableObject
    {
        [SerializeField] private string layoutId = "layout";
        [SerializeField] private string displayName = "Dimension Layout";
        [SerializeField] private DimensionLayoutKind layoutKind = DimensionLayoutKind.ManualRegions;
        [SerializeField] private DimensionLayoutRegionDefinition[] regions = new DimensionLayoutRegionDefinition[0];
        [SerializeField] private Vector2Int gridLocalMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int gridCellSize = new Vector2Int(64, 64);
        [SerializeField] private DimensionLayoutGridCellDefinition[] gridCells =
            new DimensionLayoutGridCellDefinition[0];
        [SerializeField] private int radialBandSizeTiles = 64;
        [SerializeField] private DimensionLayoutRadialRingDefinition[] radialRings =
            new DimensionLayoutRadialRingDefinition[0];
        [SerializeField] private Texture2D biomeMask;
        [SerializeField] private DimensionLayoutMaskBiomeDefinition[] maskBiomeMappings =
            new DimensionLayoutMaskBiomeDefinition[0];
        [SerializeField] private Vector2Int maskLocalMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int tilesPerMaskPixel = new Vector2Int(16, 16);
        [SerializeField] private int maskAlphaThreshold = 8;
        [SerializeField] private string notes = string.Empty;

        public string LayoutId
        {
            get { return layoutId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public DimensionLayoutKind LayoutKind
        {
            get { return layoutKind; }
        }

        public DimensionLayoutRegionDefinition[] Regions
        {
            get { return regions ?? new DimensionLayoutRegionDefinition[0]; }
        }

        public Vector2Int GridLocalMin
        {
            get { return gridLocalMin; }
        }

        public Vector2Int GridCellSize
        {
            get { return gridCellSize; }
        }

        public DimensionLayoutGridCellDefinition[] GridCells
        {
            get { return gridCells ?? new DimensionLayoutGridCellDefinition[0]; }
        }

        public int RadialBandSizeTiles
        {
            get { return radialBandSizeTiles < 1 ? 1 : radialBandSizeTiles; }
        }

        public DimensionLayoutRadialRingDefinition[] RadialRings
        {
            get { return radialRings ?? new DimensionLayoutRadialRingDefinition[0]; }
        }

        public Texture2D BiomeMask
        {
            get { return biomeMask; }
        }

        public DimensionLayoutMaskBiomeDefinition[] MaskBiomeMappings
        {
            get { return maskBiomeMappings ?? new DimensionLayoutMaskBiomeDefinition[0]; }
        }

        public Vector2Int MaskLocalMin
        {
            get { return maskLocalMin; }
        }

        public Vector2Int TilesPerMaskPixel
        {
            get { return tilesPerMaskPixel; }
        }

        public int MaskAlphaThreshold
        {
            get { return maskAlphaThreshold < 0 ? 0 : maskAlphaThreshold > 255 ? 255 : maskAlphaThreshold; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void ApplySingleBiomeSquarePreset(
            string biomeId,
            string zoneId,
            int halfSizeTiles)
        {
            int halfSize = NormalizeTileSize(halfSizeTiles, 8);
            layoutKind = DimensionLayoutKind.ManualRegions;
            regions = new[]
            {
                new DimensionLayoutRegionDefinition(
                    "single-biome",
                    biomeId,
                    zoneId,
                    "Single Biome",
                    new Vector2Int(-halfSize, -halfSize),
                    new Vector2Int(halfSize, halfSize),
                    0,
                    true)
            };
            gridCells = new DimensionLayoutGridCellDefinition[0];
            radialRings = new DimensionLayoutRadialRingDefinition[0];
            maskBiomeMappings = new DimensionLayoutMaskBiomeDefinition[0];
            biomeMask = null;
            notes = "Generated from the single-biome square preset.";
        }

        public void ApplyCenteredGridPreset(
            string fallbackBiomeId,
            string zoneId,
            int columns,
            int rows,
            int cellSizeTiles,
            string[] biomeIds)
        {
            int safeColumns = NormalizeTileSize(columns, 1);
            int safeRows = NormalizeTileSize(rows, 1);
            int safeCellSize = NormalizeTileSize(cellSizeTiles, 16);
            layoutKind = DimensionLayoutKind.GridRegions;
            gridCellSize = new Vector2Int(safeCellSize, safeCellSize);
            gridLocalMin = new Vector2Int(
                -((safeColumns * safeCellSize) / 2),
                -((safeRows * safeCellSize) / 2));
            regions = new DimensionLayoutRegionDefinition[0];
            gridCells = new DimensionLayoutGridCellDefinition[safeColumns * safeRows];
            radialRings = new DimensionLayoutRadialRingDefinition[0];
            maskBiomeMappings = new DimensionLayoutMaskBiomeDefinition[0];
            biomeMask = null;

            int index = 0;
            for (int y = 0; y < safeRows; y++)
            {
                for (int x = 0; x < safeColumns; x++)
                {
                    string biomeId = ResolveBiomeSlot(biomeIds, index, fallbackBiomeId);
                    gridCells[index] = new DimensionLayoutGridCellDefinition(
                        "grid-cell-" + x.ToString() + "-" + y.ToString(),
                        biomeId,
                        zoneId,
                        "Grid Cell " + x.ToString() + ", " + y.ToString(),
                        new Vector2Int(x, y),
                        new Vector2Int(1, 1),
                        0,
                        true);
                    index++;
                }
            }

            notes = "Generated from the centered-grid preset.";
        }

        public void ApplyRadialRingsPreset(
            string centerBiomeId,
            string outerBiomeId,
            string zoneId,
            int ringWidthTiles,
            int ringCount)
        {
            int safeRingWidth = NormalizeTileSize(ringWidthTiles, 16);
            int safeRingCount = NormalizeTileSize(ringCount, 1);
            layoutKind = DimensionLayoutKind.RadialRings;
            radialBandSizeTiles = safeRingWidth;
            regions = new DimensionLayoutRegionDefinition[0];
            gridCells = new DimensionLayoutGridCellDefinition[0];
            radialRings = new DimensionLayoutRadialRingDefinition[safeRingCount];
            maskBiomeMappings = new DimensionLayoutMaskBiomeDefinition[0];
            biomeMask = null;

            for (int i = 0; i < safeRingCount; i++)
            {
                int minRadius = i * safeRingWidth;
                int maxRadius = minRadius + safeRingWidth;
                string biomeId = i == 0 || string.IsNullOrEmpty(outerBiomeId)
                    ? centerBiomeId
                    : outerBiomeId;
                radialRings[i] = new DimensionLayoutRadialRingDefinition(
                    "radial-ring-" + i.ToString(),
                    biomeId,
                    zoneId,
                    i == 0 ? "Center Ring" : "Outer Ring " + i.ToString(),
                    minRadius,
                    maxRadius,
                    i,
                    true);
            }

            notes = "Generated from the radial-rings preset.";
        }

        private static int NormalizeTileSize(int value, int fallback)
        {
            if (value < 1)
            {
                return fallback < 1 ? 1 : fallback;
            }

            return value;
        }

        private static string ResolveBiomeSlot(string[] biomeIds, int index, string fallbackBiomeId)
        {
            if (biomeIds != null && index >= 0 && index < biomeIds.Length)
            {
                string biomeId = biomeIds[index] ?? string.Empty;
                if (!string.IsNullOrEmpty(biomeId))
                {
                    return biomeId;
                }
            }

            return fallbackBiomeId ?? string.Empty;
        }
    }
}
