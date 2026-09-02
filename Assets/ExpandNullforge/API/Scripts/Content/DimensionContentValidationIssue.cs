namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentValidationIssue
    {
        public readonly DimensionDiagnosticSeverity Severity;
        public readonly DimensionContentRecordKind RecordKind;
        public readonly string RecordId;
        public readonly string ContentPackId;
        public readonly string Code;
        public readonly string Message;

        public DimensionContentValidationIssue(
            DimensionDiagnosticSeverity severity,
            DimensionContentRecordKind recordKind,
            string recordId,
            string contentPackId,
            string code,
            string message)
        {
            Severity = severity;
            RecordKind = recordKind;
            RecordId = recordId ?? string.Empty;
            ContentPackId = contentPackId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
