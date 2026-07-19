namespace ExpandNullforge.Api
{
    public enum DimensionTravelState
    {
        Unknown = 0,
        WaitingForDestinationLoad = 1,
        TeleportQueued = 2,
        Arrived = 3,
        Completed = 4,
        Failed = 5,
        Cancelled = 6
    }
}
