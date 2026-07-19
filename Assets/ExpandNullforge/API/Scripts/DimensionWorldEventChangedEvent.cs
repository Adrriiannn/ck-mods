namespace ExpandNullforge.Api
{
    public readonly struct DimensionWorldEventChangedEvent
    {
        public readonly DimensionWorldEventDefinition WorldEvent;
        public readonly DimensionWorldEventChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionWorldEventChangedEvent(
            DimensionWorldEventDefinition worldEvent,
            DimensionWorldEventChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            WorldEvent = worldEvent;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
