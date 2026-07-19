namespace ExpandNullforge.Api
{
    public readonly struct DimensionStarterDefinition
    {
        public readonly string StarterId;
        public readonly string DimensionId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string ContentPackId;
        public readonly bool Enabled;
        public readonly DimensionContentReadinessRequest ContentReadinessRequest;
        public readonly DimensionTravelLoopPreflightRequest TravelLoopPreflightRequest;
        public readonly DimensionStarterGenerationRequest GenerationRequest;

        public DimensionStarterDefinition(
            string starterId,
            string dimensionId,
            string displayName,
            string description,
            string contentPackId,
            bool enabled,
            DimensionContentReadinessRequest contentReadinessRequest,
            DimensionTravelLoopPreflightRequest travelLoopPreflightRequest,
            DimensionStarterGenerationRequest generationRequest)
        {
            StarterId = starterId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            ContentPackId = contentPackId ?? string.Empty;
            Enabled = enabled;
            ContentReadinessRequest = contentReadinessRequest;
            TravelLoopPreflightRequest = travelLoopPreflightRequest;
            GenerationRequest = generationRequest;
        }
    }
}
