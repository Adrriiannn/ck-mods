namespace ExpandNullforge.Api
{
    public readonly struct DimensionEnvironmentProfileChangedEvent
    {
        public readonly DimensionEnvironmentProfile Profile;
        public readonly DimensionEnvironmentProfileChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionEnvironmentProfileChangedEvent(
            DimensionEnvironmentProfile profile,
            DimensionEnvironmentProfileChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Profile = profile;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
