namespace ExpandNullforge.Api
{
    public readonly struct DimensionStarterReadinessSnapshot
    {
        public readonly bool Ready;
        public readonly string Code;
        public readonly string Message;
        public readonly string StarterId;
        public readonly string DimensionId;
        public readonly DimensionContentReadinessResult ContentReadiness;
        public readonly DimensionTravelLoopPreflightResult TravelLoopPreflight;

        public DimensionStarterReadinessSnapshot(
            bool ready,
            string code,
            string message,
            string starterId,
            string dimensionId,
            DimensionContentReadinessResult contentReadiness,
            DimensionTravelLoopPreflightResult travelLoopPreflight)
        {
            Ready = ready;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            StarterId = starterId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ContentReadiness = contentReadiness;
            TravelLoopPreflight = travelLoopPreflight;
        }
    }
}
