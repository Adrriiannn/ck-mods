namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPreflightResult
    {
        public readonly bool CanProceed;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly bool DimensionExists;
        public readonly bool HasGenerationCapability;
        public readonly bool BoundsValid;
        public readonly bool AreaInsideDimension;
        public readonly bool AlreadyReady;
        public readonly bool ActiveGeneration;
        public readonly bool ReservationFree;
        public readonly DimensionGenerationStatus Status;
        public readonly DimensionGenerationReservation BlockingReservation;

        public DimensionGenerationPreflightResult(
            bool canProceed,
            string code,
            string message,
            string dimensionId,
            DimensionBounds localBounds,
            bool dimensionExists,
            bool hasGenerationCapability,
            bool boundsValid,
            bool areaInsideDimension,
            bool alreadyReady,
            bool activeGeneration,
            bool reservationFree,
            DimensionGenerationStatus status,
            DimensionGenerationReservation blockingReservation)
        {
            CanProceed = canProceed;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            DimensionExists = dimensionExists;
            HasGenerationCapability = hasGenerationCapability;
            BoundsValid = boundsValid;
            AreaInsideDimension = areaInsideDimension;
            AlreadyReady = alreadyReady;
            ActiveGeneration = activeGeneration;
            ReservationFree = reservationFree;
            Status = status;
            BlockingReservation = blockingReservation;
        }
    }
}
