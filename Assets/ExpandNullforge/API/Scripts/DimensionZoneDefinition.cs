namespace ExpandNullforge.Api
{
    public readonly struct DimensionZoneDefinition
    {
        public readonly string ZoneId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string Kind;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionZoneDefinition(
            string zoneId,
            string displayName,
            string dimensionId,
            DimensionBounds localBounds,
            string kind,
            int priority,
            bool enabled)
        {
            ZoneId = zoneId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            Kind = kind ?? string.Empty;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
