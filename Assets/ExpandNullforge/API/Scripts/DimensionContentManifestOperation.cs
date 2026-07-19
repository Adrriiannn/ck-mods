namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentManifestOperation
    {
        public readonly DimensionContentManifestOperationKind OperationKind;
        public readonly DimensionContentRecordKind RecordKind;
        public readonly string RecordId;
        public readonly bool Success;
        public readonly bool Applied;
        public readonly string Code;
        public readonly string Message;

        public DimensionContentManifestOperation(
            DimensionContentManifestOperationKind operationKind,
            DimensionContentRecordKind recordKind,
            string recordId,
            bool success,
            bool applied,
            string code,
            string message)
        {
            OperationKind = operationKind;
            RecordKind = recordKind;
            RecordId = recordId ?? string.Empty;
            Success = success;
            Applied = applied;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
