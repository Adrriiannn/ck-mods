namespace ExpandNullforge.Api
{
    public readonly struct DimensionPlayerVisitChangedEvent
    {
        public readonly DimensionPlayerVisitRecord PreviousVisit;
        public readonly DimensionPlayerVisitRecord CurrentVisit;
        public readonly DimensionPlayerVisitChangeKind ChangeKind;
        public readonly string Reason;

        public DimensionPlayerVisitChangedEvent(
            DimensionPlayerVisitRecord previousVisit,
            DimensionPlayerVisitRecord currentVisit,
            DimensionPlayerVisitChangeKind changeKind,
            string reason)
        {
            PreviousVisit = previousVisit;
            CurrentVisit = currentVisit;
            ChangeKind = changeKind;
            Reason = reason ?? string.Empty;
        }
    }
}
