namespace ExpandNullforge.Api
{
    public interface IDimensionTravelRequirementEvaluator
    {
        string ProviderId { get; }

        int Priority { get; }

        bool TryEvaluateTravelRequirement(
            DimensionTravelRequirementEvaluationContext context,
            out DimensionTravelRequirementEvaluationResult result);
    }
}
