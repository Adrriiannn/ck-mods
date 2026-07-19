namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableEntryChangedEvent
    {
        public readonly DimensionGenerationTableEntryDefinition Entry;
        public readonly DimensionGenerationTableChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionGenerationTableEntryChangedEvent(
            DimensionGenerationTableEntryDefinition entry,
            DimensionGenerationTableChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Entry = entry;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
