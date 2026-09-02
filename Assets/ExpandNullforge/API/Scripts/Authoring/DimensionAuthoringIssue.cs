namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringIssue
    {
        public readonly DimensionAuthoringSeverity Severity;
        public readonly string Code;
        public readonly string Message;
        public readonly string RecordKind;
        public readonly string RecordId;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;

        public DimensionAuthoringIssue(
            DimensionAuthoringSeverity severity,
            string code,
            string message,
            string recordKind,
            string recordId,
            bool hasLocalBounds,
            DimensionBounds localBounds)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
        }
    }
}
