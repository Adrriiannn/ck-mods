using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionAssetReferenceResolutionResult
    {
        public readonly bool Success;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly bool HasZone;
        public readonly DimensionZoneInfo Zone;
        public readonly string ZoneId;
        public readonly DimensionAssetReferenceKind Kind;
        public readonly string VariantId;
        public readonly IReadOnlyList<DimensionAssetReferenceDefinition> AssetReferences;
        public readonly int AssetReferenceCount;
        public readonly string Code;
        public readonly string Message;

        public DimensionAssetReferenceResolutionResult(
            bool success,
            string dimensionId,
            float2 localPosition,
            bool hasZone,
            DimensionZoneInfo zone,
            string zoneId,
            DimensionAssetReferenceKind kind,
            string variantId,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            int assetReferenceCount,
            string code,
            string message)
        {
            Success = success;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            HasZone = hasZone;
            Zone = zone;
            ZoneId = zoneId ?? string.Empty;
            Kind = kind;
            VariantId = variantId ?? string.Empty;
            AssetReferences = assetReferences;
            AssetReferenceCount = assetReferenceCount;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
