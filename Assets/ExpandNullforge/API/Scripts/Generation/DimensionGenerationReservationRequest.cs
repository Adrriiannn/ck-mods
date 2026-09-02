namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationReservationRequest
    {
        public readonly string ReservationId;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string OwnerId;
        public readonly string Purpose;
        public readonly int Priority;
        public readonly bool AllowOverlapWithSameOwner;
        public readonly string Reason;

        public DimensionGenerationReservationRequest(
            string reservationId,
            string dimensionId,
            DimensionBounds localBounds,
            string ownerId,
            string purpose,
            int priority,
            bool allowOverlapWithSameOwner,
            string reason)
        {
            ReservationId = reservationId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            OwnerId = ownerId ?? string.Empty;
            Purpose = purpose ?? string.Empty;
            Priority = priority;
            AllowOverlapWithSameOwner = allowOverlapWithSameOwner;
            Reason = reason ?? string.Empty;
        }
    }
}
