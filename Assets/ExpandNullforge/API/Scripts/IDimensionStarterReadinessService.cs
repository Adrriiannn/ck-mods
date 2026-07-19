namespace ExpandNullforge.Api
{
    public interface IDimensionStarterReadinessService
    {
        DimensionStarterReadinessSnapshot GetStarterReadiness(
            DimensionStarterReadinessRequest request);

        DimensionStarterReadinessSnapshot GetStarterReadiness(
            string starterId);

        DimensionGenerationStatus EnsureStarterAreaQueued(
            DimensionStarterGenerationRequest request);

        DimensionGenerationStatus EnsureStarterAreaQueued(
            string starterId,
            string requesterId,
            int priority,
            string reason);
    }
}
