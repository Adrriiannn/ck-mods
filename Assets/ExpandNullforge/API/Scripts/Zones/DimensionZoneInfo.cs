namespace ExpandNullforge.Api
{
    public readonly struct DimensionZoneInfo
    {
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly string DisplayName;
        public readonly string Kind;
        public readonly DimensionBounds LocalBounds;

        public DimensionZoneInfo(
            string dimensionId,
            string zoneId,
            string displayName,
            DimensionBounds localBounds)
            : this(
                dimensionId,
                zoneId,
                displayName,
                string.Empty,
                localBounds)
        {
        }

        public DimensionZoneInfo(
            string dimensionId,
            string zoneId,
            string displayName,
            string kind,
            DimensionBounds localBounds)
        {
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind ?? string.Empty;
            LocalBounds = localBounds;
        }
    }
}
