namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableQuery
    {
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly DimensionGenerationTableKind Kind;
        public readonly bool EnabledOnly;

        public DimensionGenerationTableQuery(
            string dimensionId,
            string biomeId,
            DimensionGenerationTableKind kind,
            bool enabledOnly)
        {
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            Kind = kind;
            EnabledOnly = enabledOnly;
        }
    }
}
