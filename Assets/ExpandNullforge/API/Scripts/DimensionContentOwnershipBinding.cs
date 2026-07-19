namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentOwnershipBinding
    {
        public readonly string ContentPackId;
        public readonly DimensionContentRecordKind RecordKind;
        public readonly string RecordId;
        public readonly string DisplayName;
        public readonly string Notes;

        public DimensionContentOwnershipBinding(
            string contentPackId,
            DimensionContentRecordKind recordKind,
            string recordId,
            string displayName,
            string notes)
        {
            ContentPackId = contentPackId ?? string.Empty;
            RecordKind = recordKind;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Notes = notes ?? string.Empty;
        }
    }
}
