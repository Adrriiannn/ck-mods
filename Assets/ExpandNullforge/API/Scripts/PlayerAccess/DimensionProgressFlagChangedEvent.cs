namespace ExpandNullforge.Api
{
    public readonly struct DimensionProgressFlagChangedEvent
    {
        public readonly DimensionProgressFlag PreviousFlag;
        public readonly DimensionProgressFlag CurrentFlag;
        public readonly DimensionProgressFlagChangeKind ChangeKind;
        public readonly string Reason;

        public DimensionProgressFlagChangedEvent(
            DimensionProgressFlag previousFlag,
            DimensionProgressFlag currentFlag,
            DimensionProgressFlagChangeKind changeKind,
            string reason)
        {
            PreviousFlag = previousFlag;
            CurrentFlag = currentFlag;
            ChangeKind = changeKind;
            Reason = reason ?? string.Empty;
        }
    }
}
