namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelRequirementChangedEvent
    {
        public readonly DimensionTravelRequirementDefinition Requirement;
        public readonly DimensionTravelRequirementChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionTravelRequirementChangedEvent(
            DimensionTravelRequirementDefinition requirement,
            DimensionTravelRequirementChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Requirement = requirement;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
