namespace ExpandNullforge.Api
{
    public interface IDimensionGenerationPlanningService
    {
        DimensionGenerationPlan BuildGenerationPlan(DimensionGenerationPlanRequest request);

        DimensionGenerationPlanPreflightResult PreflightGenerationPlan(
            DimensionGenerationPlanPreflightRequest request);
    }
}
