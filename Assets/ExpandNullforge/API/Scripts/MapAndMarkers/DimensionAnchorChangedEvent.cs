namespace ExpandNullforge.Api
{
    public readonly struct DimensionAnchorChangedEvent
    {
        public readonly DimensionAnchorDefinition Anchor;
        public readonly DimensionAnchorChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionAnchorChangedEvent(
            DimensionAnchorDefinition anchor,
            DimensionAnchorChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Anchor = anchor;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
