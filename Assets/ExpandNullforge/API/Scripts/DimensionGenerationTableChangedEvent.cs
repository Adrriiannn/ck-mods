namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableChangedEvent
    {
        public readonly DimensionGenerationTableDefinition Table;
        public readonly DimensionGenerationTableChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionGenerationTableChangedEvent(
            DimensionGenerationTableDefinition table,
            DimensionGenerationTableChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Table = table;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
