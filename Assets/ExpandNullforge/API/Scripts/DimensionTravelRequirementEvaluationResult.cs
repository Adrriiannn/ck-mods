namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelRequirementEvaluationResult
    {
        public readonly DimensionTravelRequirementDefinition Requirement;
        public readonly bool Evaluated;
        public readonly bool Satisfied;
        public readonly string ProviderId;
        public readonly string Code;
        public readonly string Message;

        public DimensionTravelRequirementEvaluationResult(
            DimensionTravelRequirementDefinition requirement,
            bool evaluated,
            bool satisfied,
            string providerId,
            string code,
            string message)
        {
            Requirement = requirement;
            Evaluated = evaluated;
            Satisfied = satisfied;
            ProviderId = providerId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static DimensionTravelRequirementEvaluationResult Met(
            DimensionTravelRequirementDefinition requirement,
            string providerId,
            string message)
        {
            return new DimensionTravelRequirementEvaluationResult(
                requirement,
                true,
                true,
                providerId,
                string.Empty,
                message);
        }

        public static DimensionTravelRequirementEvaluationResult Unmet(
            DimensionTravelRequirementDefinition requirement,
            string providerId,
            string code,
            string message)
        {
            return new DimensionTravelRequirementEvaluationResult(
                requirement,
                true,
                false,
                providerId,
                code,
                message);
        }

        public static DimensionTravelRequirementEvaluationResult Unevaluated(
            DimensionTravelRequirementDefinition requirement,
            string code,
            string message)
        {
            return new DimensionTravelRequirementEvaluationResult(
                requirement,
                false,
                false,
                string.Empty,
                code,
                message);
        }
    }
}
