namespace ExpandNullforge.Api
{
    public readonly struct DimensionCompiledBiomeRegion
    {
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string ZoneId;
        public readonly string DisplayName;
        public readonly string SourceTemplateId;
        public readonly DimensionBounds LocalBounds;
        public readonly int Priority;

        public DimensionCompiledBiomeRegion(
            string dimensionId,
            string biomeId,
            string zoneId,
            string displayName,
            string sourceTemplateId,
            DimensionBounds localBounds,
            int priority)
        {
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SourceTemplateId = sourceTemplateId ?? string.Empty;
            LocalBounds = localBounds;
            Priority = priority;
        }
    }
}
