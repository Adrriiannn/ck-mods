namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentValidationRequest
    {
        public readonly string ContentPackId;
        public readonly bool IncludeDisabled;
        public readonly bool IncludeWarnings;

        public DimensionContentValidationRequest(
            string contentPackId,
            bool includeDisabled,
            bool includeWarnings)
        {
            ContentPackId = contentPackId ?? string.Empty;
            IncludeDisabled = includeDisabled;
            IncludeWarnings = includeWarnings;
        }
    }
}
