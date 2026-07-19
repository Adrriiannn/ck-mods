namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelFeedbackRetentionPolicy
    {
        public readonly double CompletedSeconds;
        public readonly double CancelledSeconds;
        public readonly double FailedSeconds;

        public DimensionTravelFeedbackRetentionPolicy(
            double completedSeconds,
            double cancelledSeconds,
            double failedSeconds)
        {
            CompletedSeconds = completedSeconds < 0 ? 0 : completedSeconds;
            CancelledSeconds = cancelledSeconds < 0 ? 0 : cancelledSeconds;
            FailedSeconds = failedSeconds < 0 ? 0 : failedSeconds;
        }

        public static DimensionTravelFeedbackRetentionPolicy Default()
        {
            return new DimensionTravelFeedbackRetentionPolicy(4, 4, 8);
        }

        public double GetRetentionSeconds(DimensionTravelFeedbackPhase phase)
        {
            switch (phase)
            {
                case DimensionTravelFeedbackPhase.Completed:
                    return CompletedSeconds;
                case DimensionTravelFeedbackPhase.Cancelled:
                    return CancelledSeconds;
                case DimensionTravelFeedbackPhase.Failed:
                    return FailedSeconds;
                default:
                    return 0;
            }
        }
    }
}
