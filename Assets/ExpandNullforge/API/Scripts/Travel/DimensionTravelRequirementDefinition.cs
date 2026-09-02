namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelRequirementDefinition
    {
        public readonly string RequirementId;
        public readonly string DisplayName;
        public readonly string PortalId;
        public readonly string DimensionId;
        public readonly DimensionTravelRequirementKind Kind;
        public readonly string SubjectId;
        public readonly int RequiredAmount;
        public readonly bool ConsumeOnTravel;
        public readonly string FailureMessage;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionTravelRequirementDefinition(
            string requirementId,
            string displayName,
            string portalId,
            string dimensionId,
            DimensionTravelRequirementKind kind,
            string subjectId,
            int requiredAmount,
            bool consumeOnTravel,
            string failureMessage,
            int priority,
            bool enabled)
        {
            RequirementId = requirementId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PortalId = portalId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
            RequiredAmount = requiredAmount;
            ConsumeOnTravel = consumeOnTravel;
            FailureMessage = failureMessage ?? string.Empty;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
