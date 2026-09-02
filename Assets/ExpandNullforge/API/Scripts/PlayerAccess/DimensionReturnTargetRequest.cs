namespace ExpandNullforge.Api
{
    public readonly struct DimensionReturnTargetRequest
    {
        public readonly string PlayerId;
        public readonly string TargetDimensionId;
        public readonly bool PreferPlayerVisit;
        public readonly bool AllowReturnAnchorFallback;
        public readonly bool AllowEntryAnchorFallback;
        public readonly bool RequireGeneratedArea;
        public readonly string Reason;

        public DimensionReturnTargetRequest(
            string playerId,
            string targetDimensionId,
            bool preferPlayerVisit,
            bool allowReturnAnchorFallback,
            bool allowEntryAnchorFallback,
            bool requireGeneratedArea,
            string reason)
        {
            PlayerId = playerId ?? string.Empty;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            PreferPlayerVisit = preferPlayerVisit;
            AllowReturnAnchorFallback = allowReturnAnchorFallback;
            AllowEntryAnchorFallback = allowEntryAnchorFallback;
            RequireGeneratedArea = requireGeneratedArea;
            Reason = reason ?? string.Empty;
        }
    }
}
