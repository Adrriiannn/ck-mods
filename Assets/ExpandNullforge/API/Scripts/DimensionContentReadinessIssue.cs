namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentReadinessIssue
    {
        public readonly DimensionContentRecordKind RecordKind;
        public readonly string RecordId;
        public readonly string Code;
        public readonly string Message;

        public DimensionContentReadinessIssue(
            DimensionContentRecordKind recordKind,
            string recordId,
            string code,
            string message)
        {
            RecordKind = recordKind;
            RecordId = recordId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
