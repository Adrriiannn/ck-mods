namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentOwnershipQuery
    {
        public readonly string ContentPackId;
        public readonly DimensionContentRecordKind RecordKind;
        public readonly string RecordId;
        public readonly bool IncludeOrphaned;
        public readonly bool EnabledContentPacksOnly;

        public DimensionContentOwnershipQuery(
            string contentPackId,
            DimensionContentRecordKind recordKind,
            string recordId,
            bool includeOrphaned,
            bool enabledContentPacksOnly)
        {
            ContentPackId = contentPackId ?? string.Empty;
            RecordKind = recordKind;
            RecordId = recordId ?? string.Empty;
            IncludeOrphaned = includeOrphaned;
            EnabledContentPacksOnly = enabledContentPacksOnly;
        }
    }
}
