namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentPackSummaryRequest
    {
        public readonly string ContentPackId;
        public readonly bool IncludeDisabled;
        public readonly bool IncludeOrphanedOwnership;
        public readonly bool IncludeValidation;
        public readonly bool IncludeWarnings;

        public DimensionContentPackSummaryRequest(
            string contentPackId,
            bool includeDisabled,
            bool includeOrphanedOwnership,
            bool includeValidation,
            bool includeWarnings)
        {
            ContentPackId = contentPackId ?? string.Empty;
            IncludeDisabled = includeDisabled;
            IncludeOrphanedOwnership = includeOrphanedOwnership;
            IncludeValidation = includeValidation;
            IncludeWarnings = includeWarnings;
        }
    }
}
