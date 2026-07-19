namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentOwnershipChangedEvent
    {
        public readonly DimensionContentOwnershipBinding Binding;
        public readonly DimensionContentOwnershipChangeKind ChangeKind;
        public readonly string PreviousContentPackId;
        public readonly string Reason;

        public DimensionContentOwnershipChangedEvent(
            DimensionContentOwnershipBinding binding,
            DimensionContentOwnershipChangeKind changeKind,
            string previousContentPackId,
            string reason)
        {
            Binding = binding;
            ChangeKind = changeKind;
            PreviousContentPackId = previousContentPackId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
