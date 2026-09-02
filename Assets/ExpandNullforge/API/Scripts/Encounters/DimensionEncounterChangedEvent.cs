namespace ExpandNullforge.Api
{
    public readonly struct DimensionEncounterChangedEvent
    {
        public readonly DimensionEncounterDefinition Encounter;
        public readonly DimensionEncounterChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionEncounterChangedEvent(
            DimensionEncounterDefinition encounter,
            DimensionEncounterChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Encounter = encounter;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
