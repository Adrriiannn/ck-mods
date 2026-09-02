namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPlanRequest
    {
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string ZoneId;
        public readonly bool IncludeDisabled;
        public readonly bool ResolveZoneFromArea;
        public readonly bool RequireRegisteredProvider;

        public DimensionGenerationPlanRequest(
            string dimensionId,
            DimensionBounds localBounds,
            string zoneId,
            bool includeDisabled,
            bool resolveZoneFromArea,
            bool requireRegisteredProvider)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            ZoneId = zoneId ?? string.Empty;
            IncludeDisabled = includeDisabled;
            ResolveZoneFromArea = resolveZoneFromArea;
            RequireRegisteredProvider = requireRegisteredProvider;
        }
    }
}
