namespace ExpandNullforge.Api
{
    public readonly struct DimensionAssetReferenceQuery
    {
        public readonly string ContentPackId;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly DimensionAssetReferenceKind Kind;
        public readonly string VariantId;
        public readonly bool EnabledOnly;

        public DimensionAssetReferenceQuery(
            string contentPackId,
            string dimensionId,
            string zoneId,
            DimensionAssetReferenceKind kind,
            string variantId,
            bool enabledOnly)
        {
            ContentPackId = contentPackId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            Kind = kind;
            VariantId = variantId ?? string.Empty;
            EnabledOnly = enabledOnly;
        }
    }
}
