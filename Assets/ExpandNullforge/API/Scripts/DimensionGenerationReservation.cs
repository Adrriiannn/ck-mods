namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationReservation
    {
        public readonly string ReservationId;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string OwnerId;
        public readonly string Purpose;
        public readonly int Priority;
        public readonly double CreatedAt;

        public DimensionGenerationReservation(
            string reservationId,
            string dimensionId,
            DimensionBounds localBounds,
            string ownerId,
            string purpose,
            int priority,
            double createdAt)
        {
            ReservationId = reservationId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            OwnerId = ownerId ?? string.Empty;
            Purpose = purpose ?? string.Empty;
            Priority = priority;
            CreatedAt = createdAt;
        }
    }
}
