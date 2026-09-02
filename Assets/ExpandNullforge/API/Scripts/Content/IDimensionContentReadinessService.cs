namespace ExpandNullforge.Api
{
    public interface IDimensionContentReadinessService
    {
        DimensionContentReadinessResult PreflightContentReadiness(
            DimensionContentReadinessRequest request);

        DimensionContentReadinessResult PreflightStarterContent(
            string starterId);
    }
}
