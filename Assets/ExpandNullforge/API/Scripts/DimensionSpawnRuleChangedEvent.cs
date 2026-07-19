namespace ExpandNullforge.Api
{
    public readonly struct DimensionSpawnRuleChangedEvent
    {
        public readonly DimensionSpawnRule Rule;
        public readonly DimensionSpawnRuleChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionSpawnRuleChangedEvent(
            DimensionSpawnRule rule,
            DimensionSpawnRuleChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Rule = rule;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
