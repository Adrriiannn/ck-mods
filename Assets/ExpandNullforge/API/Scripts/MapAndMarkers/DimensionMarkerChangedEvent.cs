namespace ExpandNullforge.Api
{
    public readonly struct DimensionMarkerChangedEvent
    {
        public readonly DimensionMapMarker PreviousMarker;
        public readonly DimensionMapMarker CurrentMarker;
        public readonly DimensionMarkerChangeKind ChangeKind;
        public readonly string Reason;

        public DimensionMarkerChangedEvent(
            DimensionMapMarker previousMarker,
            DimensionMapMarker currentMarker,
            DimensionMarkerChangeKind changeKind,
            string reason)
        {
            PreviousMarker = previousMarker;
            CurrentMarker = currentMarker;
            ChangeKind = changeKind;
            Reason = reason ?? string.Empty;
        }
    }
}
