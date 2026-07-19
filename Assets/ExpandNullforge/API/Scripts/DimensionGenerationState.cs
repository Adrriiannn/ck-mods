namespace ExpandNullforge.Api
{
    public enum DimensionGenerationState
    {
        Unknown = 0,
        NotGenerated = 1,
        Queued = 2,
        LoadingArea = 3,
        GeneratingTerrain = 4,
        StampingScenes = 5,
        Populating = 6,
        Ready = 7,
        Failed = 8
    }
}
