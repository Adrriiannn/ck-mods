namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPreviewResult
    {
        public readonly bool CanRequest;
        public readonly string Code;
        public readonly string Message;
        public readonly DimensionGenerationPreflightResult AreaPreflight;
        public readonly DimensionGenerationPlanPreflightResult PlanPreflight;
        public readonly bool WouldQueueGeneration;
        public readonly bool WouldReuseExistingStatus;
        public readonly bool WouldReturnNotGenerated;
        public readonly DimensionGenerationStatus ExistingStatus;

        public DimensionGenerationPreviewResult(
            bool canRequest,
            string code,
            string message,
            DimensionGenerationPreflightResult areaPreflight,
            DimensionGenerationPlanPreflightResult planPreflight,
            bool wouldQueueGeneration,
            bool wouldReuseExistingStatus,
            bool wouldReturnNotGenerated,
            DimensionGenerationStatus existingStatus)
        {
            CanRequest = canRequest;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            AreaPreflight = areaPreflight;
            PlanPreflight = planPreflight;
            WouldQueueGeneration = wouldQueueGeneration;
            WouldReuseExistingStatus = wouldReuseExistingStatus;
            WouldReturnNotGenerated = wouldReturnNotGenerated;
            ExistingStatus = existingStatus;
        }
    }
}
