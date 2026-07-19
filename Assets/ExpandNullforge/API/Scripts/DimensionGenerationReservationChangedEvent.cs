namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationReservationChangedEvent
    {
        public readonly DimensionGenerationReservation Reservation;
        public readonly DimensionGenerationReservationChangeKind ChangeKind;
        public readonly string Reason;

        public DimensionGenerationReservationChangedEvent(
            DimensionGenerationReservation reservation,
            DimensionGenerationReservationChangeKind changeKind,
            string reason)
        {
            Reservation = reservation;
            ChangeKind = changeKind;
            Reason = reason ?? string.Empty;
        }
    }
}
