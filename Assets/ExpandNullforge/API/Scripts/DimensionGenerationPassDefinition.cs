namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPassDefinition
    {
        public readonly string PassId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly DimensionGenerationPassPhase Phase;
        public readonly int Priority;
        public readonly string ProviderId;
        public readonly bool Enabled;

        public DimensionGenerationPassDefinition(
            string passId,
            string displayName,
            string dimensionId,
            string zoneId,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            DimensionGenerationPassPhase phase,
            int priority,
            string providerId,
            bool enabled)
        {
            PassId = passId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            Phase = phase;
            Priority = priority;
            ProviderId = providerId ?? string.Empty;
            Enabled = enabled;
        }
    }
}
