namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelRequirementQuery
    {
        public readonly string PortalId;
        public readonly string DimensionId;
        public readonly DimensionTravelRequirementKind Kind;
        public readonly bool EnabledOnly;
        public readonly bool IncludeDimensionWideRequirements;
        public readonly bool IncludePortalWideRequirements;

        public DimensionTravelRequirementQuery(
            string portalId,
            string dimensionId,
            DimensionTravelRequirementKind kind,
            bool enabledOnly,
            bool includeDimensionWideRequirements,
            bool includePortalWideRequirements)
        {
            PortalId = portalId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            Kind = kind;
            EnabledOnly = enabledOnly;
            IncludeDimensionWideRequirements = includeDimensionWideRequirements;
            IncludePortalWideRequirements = includePortalWideRequirements;
        }
    }
}
