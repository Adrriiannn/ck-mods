namespace ExpandNullforge.Api
{
    public readonly struct DimensionZoneChangedEvent
    {
        public readonly DimensionZoneDefinition Zone;
        public readonly DimensionZoneChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionZoneChangedEvent(
            DimensionZoneDefinition zone,
            DimensionZoneChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Zone = zone;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
