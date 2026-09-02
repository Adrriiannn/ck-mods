namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationReservationQuery
    {
        public readonly string DimensionId;
        public readonly string OwnerId;
        public readonly bool RequireOverlap;
        public readonly DimensionBounds LocalBounds;

        public DimensionGenerationReservationQuery(
            string dimensionId,
            string ownerId,
            bool requireOverlap,
            DimensionBounds localBounds)
        {
            DimensionId = dimensionId ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
            RequireOverlap = requireOverlap;
            LocalBounds = localBounds;
        }
    }
}
