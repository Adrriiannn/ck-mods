namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringPreviewEntry
    {
        public readonly DimensionAuthoringPreviewLayerKind LayerKind;
        public readonly string RecordId;
        public readonly string DisplayName;
        public readonly string BiomeId;
        public readonly string ZoneId;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly int Priority;
        public readonly uint ColorRgba;

        public DimensionAuthoringPreviewEntry(
            DimensionAuthoringPreviewLayerKind layerKind,
            string recordId,
            string displayName,
            string biomeId,
            string zoneId,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            int priority,
            uint colorRgba)
        {
            LayerKind = layerKind;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            Priority = priority;
            ColorRgba = colorRgba;
        }
    }
}
