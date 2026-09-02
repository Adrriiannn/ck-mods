namespace ExpandNullforge.Api
{
    public readonly struct DimensionWorldEventDefinition
    {
        public readonly string EventId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly DimensionWorldEventKind Kind;
        public readonly string ProviderId;
        public readonly string ProgressFlagId;
        public readonly int Weight;
        public readonly int Priority;
        public readonly float CooldownSeconds;
        public readonly bool Enabled;

        public DimensionWorldEventDefinition(
            string eventId,
            string displayName,
            string dimensionId,
            string zoneId,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            DimensionWorldEventKind kind,
            string providerId,
            string progressFlagId,
            int weight,
            int priority,
            float cooldownSeconds,
            bool enabled)
        {
            EventId = eventId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            Kind = kind;
            ProviderId = providerId ?? string.Empty;
            ProgressFlagId = progressFlagId ?? string.Empty;
            Weight = weight;
            Priority = priority;
            CooldownSeconds = cooldownSeconds;
            Enabled = enabled;
        }
    }
}
