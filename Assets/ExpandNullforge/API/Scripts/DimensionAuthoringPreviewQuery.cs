namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringPreviewQuery
    {
        public readonly bool HasPreviewLayerKind;
        public readonly DimensionAuthoringPreviewLayerKind PreviewLayerKind;
        public readonly bool HasContentKind;
        public readonly DimensionAuthoringContentSummaryKind ContentKind;
        public readonly string RecordId;
        public readonly string BiomeId;
        public readonly string ZoneId;
        public readonly bool IncludeGlobalRecords;

        public DimensionAuthoringPreviewQuery(
            bool hasPreviewLayerKind,
            DimensionAuthoringPreviewLayerKind previewLayerKind,
            bool hasContentKind,
            DimensionAuthoringContentSummaryKind contentKind,
            string recordId,
            string biomeId,
            string zoneId,
            bool includeGlobalRecords)
        {
            HasPreviewLayerKind = hasPreviewLayerKind;
            PreviewLayerKind = previewLayerKind;
            HasContentKind = hasContentKind;
            ContentKind = contentKind;
            RecordId = recordId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            IncludeGlobalRecords = includeGlobalRecords;
        }

        public static DimensionAuthoringPreviewQuery All
        {
            get
            {
                return new DimensionAuthoringPreviewQuery(
                    false,
                    DimensionAuthoringPreviewLayerKind.PlayableBounds,
                    false,
                    DimensionAuthoringContentSummaryKind.Dimension,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    true);
            }
        }

        public static DimensionAuthoringPreviewQuery ForBiome(string biomeId)
        {
            return new DimensionAuthoringPreviewQuery(
                false,
                DimensionAuthoringPreviewLayerKind.PlayableBounds,
                false,
                DimensionAuthoringContentSummaryKind.Dimension,
                string.Empty,
                biomeId,
                string.Empty,
                false);
        }
    }
}
