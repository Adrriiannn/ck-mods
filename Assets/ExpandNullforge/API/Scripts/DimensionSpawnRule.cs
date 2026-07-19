namespace ExpandNullforge.Api
{
    public readonly struct DimensionSpawnRule
    {
        public readonly string RuleId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly string SubjectId;
        public readonly DimensionSpawnSubjectKind SubjectKind;
        public readonly int Weight;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionSpawnRule(
            string ruleId,
            string displayName,
            string dimensionId,
            string zoneId,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            string subjectId,
            DimensionSpawnSubjectKind subjectKind,
            int weight,
            int priority,
            bool enabled)
        {
            RuleId = ruleId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            SubjectId = subjectId ?? string.Empty;
            SubjectKind = subjectKind;
            Weight = weight;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
