namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelRequirementEvaluationContext
    {
        public readonly DimensionAccessContext TravelContext;
        public readonly DimensionTravelRequirementDefinition Requirement;
        public readonly string Reason;

        public DimensionTravelRequirementEvaluationContext(
            DimensionAccessContext travelContext,
            DimensionTravelRequirementDefinition requirement,
            string reason)
        {
            TravelContext = travelContext;
            Requirement = requirement;
            Reason = reason ?? string.Empty;
        }
    }
}
