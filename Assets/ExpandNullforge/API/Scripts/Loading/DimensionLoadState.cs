namespace ExpandNullforge.Api
{
    public enum DimensionLoadState
    {
        Unknown = 0,
        Requested = 1,
        Loading = 2,
        Resident = 3,
        Simulating = 4,
        Releasing = 5,
        Released = 6,
        Failed = 7
    }
}
