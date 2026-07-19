namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableDefinition
    {
        public readonly string TableId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly DimensionGenerationTableKind Kind;
        public readonly int Priority;
        public readonly bool Enabled;
        public readonly string Notes;

        public DimensionGenerationTableDefinition(
            string tableId,
            string displayName,
            string dimensionId,
            string biomeId,
            DimensionGenerationTableKind kind,
            int priority,
            bool enabled,
            string notes)
        {
            TableId = tableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            Kind = kind;
            Priority = priority;
            Enabled = enabled;
            Notes = notes ?? string.Empty;
        }
    }
}
