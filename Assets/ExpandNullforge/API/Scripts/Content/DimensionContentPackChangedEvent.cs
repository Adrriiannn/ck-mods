namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentPackChangedEvent
    {
        public readonly DimensionContentPackDefinition ContentPack;
        public readonly DimensionContentPackChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionContentPackChangedEvent(
            DimensionContentPackDefinition contentPack,
            DimensionContentPackChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            ContentPack = contentPack;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
