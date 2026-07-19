namespace ExpandNullforge.Api
{
    public enum DimensionTravelFeedbackPhase
    {
        Idle = 0,
        RequestQueued = 1,
        AwaitingServer = 2,
        PreparingDestination = 3,
        Travelling = 4,
        CancelRequested = 5,
        Completed = 6,
        Failed = 7,
        Cancelled = 8
    }
}
