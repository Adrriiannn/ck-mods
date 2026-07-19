namespace ExpandNullforge.Api
{
    public readonly struct DimensionAssetReferenceChangedEvent
    {
        public readonly DimensionAssetReferenceDefinition Reference;
        public readonly DimensionAssetReferenceChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionAssetReferenceChangedEvent(
            DimensionAssetReferenceDefinition reference,
            DimensionAssetReferenceChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Reference = reference;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
