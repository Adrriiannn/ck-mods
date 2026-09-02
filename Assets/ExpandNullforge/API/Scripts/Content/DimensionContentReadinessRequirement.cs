namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentReadinessRequirement
    {
        public readonly DimensionContentRecordKind RecordKind;
        public readonly string RecordId;
        public readonly string DisplayName;
        public readonly bool RequireOwnership;
        public readonly string RequiredOwnerContentPackId;
        public readonly bool RequireEnabledOwnerContentPack;

        public DimensionContentReadinessRequirement(
            DimensionContentRecordKind recordKind,
            string recordId,
            string displayName,
            bool requireOwnership,
            string requiredOwnerContentPackId,
            bool requireEnabledOwnerContentPack)
        {
            RecordKind = recordKind;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RequireOwnership = requireOwnership;
            RequiredOwnerContentPackId = requiredOwnerContentPackId ?? string.Empty;
            RequireEnabledOwnerContentPack = requireEnabledOwnerContentPack;
        }
    }
}
