namespace ExpandNullforge.Api
{
    public readonly struct DimensionAssetReferenceDefinition
    {
        public readonly string AssetId;
        public readonly string ContentPackId;
        public readonly string DisplayName;
        public readonly DimensionAssetReferenceKind Kind;
        public readonly string ResourceKey;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly string VariantId;
        public readonly int Priority;
        public readonly bool Enabled;
        public readonly string Notes;

        public DimensionAssetReferenceDefinition(
            string assetId,
            string contentPackId,
            string displayName,
            DimensionAssetReferenceKind kind,
            string resourceKey,
            string dimensionId,
            string zoneId,
            string variantId,
            int priority,
            bool enabled,
            string notes)
        {
            AssetId = assetId ?? string.Empty;
            ContentPackId = contentPackId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            ResourceKey = resourceKey ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            VariantId = variantId ?? string.Empty;
            Priority = priority;
            Enabled = enabled;
            Notes = notes ?? string.Empty;
        }
    }
}
