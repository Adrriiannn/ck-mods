namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPassChangedEvent
    {
        public readonly DimensionGenerationPassDefinition Pass;
        public readonly DimensionGenerationPassChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionGenerationPassChangedEvent(
            DimensionGenerationPassDefinition pass,
            DimensionGenerationPassChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Pass = pass;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
