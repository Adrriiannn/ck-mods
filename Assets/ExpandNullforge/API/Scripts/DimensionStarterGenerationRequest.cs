namespace ExpandNullforge.Api
{
    public readonly struct DimensionStarterGenerationRequest
    {
        public readonly string StarterId;
        public readonly string RequesterId;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly int Priority;
        public readonly bool CreateIfMissing;
        public readonly string Reason;

        public DimensionStarterGenerationRequest(
            string starterId,
            string requesterId,
            string dimensionId,
            DimensionBounds localBounds,
            int priority,
            bool createIfMissing,
            string reason)
        {
            StarterId = starterId ?? string.Empty;
            RequesterId = requesterId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            Priority = priority;
            CreateIfMissing = createIfMissing;
            Reason = reason ?? string.Empty;
        }
    }
}
