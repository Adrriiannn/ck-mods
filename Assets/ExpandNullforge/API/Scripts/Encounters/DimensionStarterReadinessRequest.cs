namespace ExpandNullforge.Api
{
    public readonly struct DimensionStarterReadinessRequest
    {
        public readonly string StarterId;
        public readonly string DimensionId;
        public readonly DimensionContentReadinessRequest ContentReadinessRequest;
        public readonly DimensionTravelLoopPreflightRequest TravelLoopPreflightRequest;

        public DimensionStarterReadinessRequest(
            string starterId,
            string dimensionId,
            DimensionContentReadinessRequest contentReadinessRequest,
            DimensionTravelLoopPreflightRequest travelLoopPreflightRequest)
        {
            StarterId = starterId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ContentReadinessRequest = contentReadinessRequest;
            TravelLoopPreflightRequest = travelLoopPreflightRequest;
        }
    }
}
