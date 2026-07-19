namespace ExpandNullforge.Api
{
    public readonly struct DimensionResourceNodeDefinition
    {
        public readonly string NodeId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly string ResourceId;
        public readonly DimensionResourceNodeKind Kind;
        public readonly string ProviderId;
        public readonly string GenerationPassId;
        public readonly int Weight;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionResourceNodeDefinition(
            string nodeId,
            string displayName,
            string dimensionId,
            string zoneId,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            string resourceId,
            DimensionResourceNodeKind kind,
            string providerId,
            string generationPassId,
            int weight,
            int priority,
            bool enabled)
        {
            NodeId = nodeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            ResourceId = resourceId ?? string.Empty;
            Kind = kind;
            ProviderId = providerId ?? string.Empty;
            GenerationPassId = generationPassId ?? string.Empty;
            Weight = weight;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
