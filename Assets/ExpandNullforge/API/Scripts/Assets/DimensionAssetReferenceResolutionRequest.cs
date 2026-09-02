using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionAssetReferenceResolutionRequest
    {
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly string ZoneId;
        public readonly string ContentPackId;
        public readonly DimensionAssetReferenceKind Kind;
        public readonly string VariantId;
        public readonly bool IncludeDisabled;
        public readonly bool ResolveZoneFromPosition;

        public DimensionAssetReferenceResolutionRequest(
            string dimensionId,
            float2 localPosition,
            string zoneId,
            string contentPackId,
            DimensionAssetReferenceKind kind,
            string variantId,
            bool includeDisabled,
            bool resolveZoneFromPosition)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            ZoneId = zoneId ?? string.Empty;
            ContentPackId = contentPackId ?? string.Empty;
            Kind = kind;
            VariantId = variantId ?? string.Empty;
            IncludeDisabled = includeDisabled;
            ResolveZoneFromPosition = resolveZoneFromPosition;
        }
    }
}
