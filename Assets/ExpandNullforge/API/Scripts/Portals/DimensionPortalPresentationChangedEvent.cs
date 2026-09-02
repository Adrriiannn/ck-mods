namespace ExpandNullforge.Api
{
    public readonly struct DimensionPortalPresentationChangedEvent
    {
        public readonly DimensionPortalPresentationDefinition Presentation;
        public readonly DimensionPortalPresentationChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionPortalPresentationChangedEvent(
            DimensionPortalPresentationDefinition presentation,
            DimensionPortalPresentationChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Presentation = presentation;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
