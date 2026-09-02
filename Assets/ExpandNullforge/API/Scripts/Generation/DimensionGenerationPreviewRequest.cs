namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPreviewRequest
    {
        public readonly DimensionGenerationRequest GenerationRequest;
        public readonly bool RequireExecutablePlan;
        public readonly bool AllowFallbackProvider;
        public readonly bool RequireReservationFree;
        public readonly bool AllowReadyArea;
        public readonly bool AllowActiveGeneration;
        public readonly string Reason;

        public DimensionGenerationPreviewRequest(
            DimensionGenerationRequest generationRequest,
            bool requireExecutablePlan,
            bool allowFallbackProvider,
            bool requireReservationFree,
            bool allowReadyArea,
            bool allowActiveGeneration,
            string reason)
        {
            GenerationRequest = generationRequest;
            RequireExecutablePlan = requireExecutablePlan;
            AllowFallbackProvider = allowFallbackProvider;
            RequireReservationFree = requireReservationFree;
            AllowReadyArea = allowReadyArea;
            AllowActiveGeneration = allowActiveGeneration;
            Reason = reason ?? string.Empty;
        }
    }
}
