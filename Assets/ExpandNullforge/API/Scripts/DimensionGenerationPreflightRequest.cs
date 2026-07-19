namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPreflightRequest
    {
        public readonly string RequesterId;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string OwnerId;
        public readonly bool RequireGenerationCapability;
        public readonly bool RequireAreaInsideDimension;
        public readonly bool AllowReadyArea;
        public readonly bool AllowActiveGeneration;
        public readonly bool RequireReservationFree;
        public readonly bool AllowReservationOverlapWithSameOwner;
        public readonly string Reason;

        public DimensionGenerationPreflightRequest(
            string requesterId,
            string dimensionId,
            DimensionBounds localBounds,
            string ownerId,
            bool requireGenerationCapability,
            bool requireAreaInsideDimension,
            bool allowReadyArea,
            bool allowActiveGeneration,
            bool requireReservationFree,
            bool allowReservationOverlapWithSameOwner,
            string reason)
        {
            RequesterId = requesterId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            OwnerId = ownerId ?? string.Empty;
            RequireGenerationCapability = requireGenerationCapability;
            RequireAreaInsideDimension = requireAreaInsideDimension;
            AllowReadyArea = allowReadyArea;
            AllowActiveGeneration = allowActiveGeneration;
            RequireReservationFree = requireReservationFree;
            AllowReservationOverlapWithSameOwner = allowReservationOverlapWithSameOwner;
            Reason = reason ?? string.Empty;
        }
    }
}
