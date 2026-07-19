using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableResolutionRequest
    {
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly string BiomeId;
        public readonly DimensionGenerationTableKind Kind;
        public readonly bool IncludeDisabled;
        public readonly bool ResolveBiomeFromPosition;

        public DimensionGenerationTableResolutionRequest(
            string dimensionId,
            float2 localPosition,
            string biomeId,
            DimensionGenerationTableKind kind,
            bool includeDisabled,
            bool resolveBiomeFromPosition)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            BiomeId = biomeId ?? string.Empty;
            Kind = kind;
            IncludeDisabled = includeDisabled;
            ResolveBiomeFromPosition = resolveBiomeFromPosition;
        }
    }
}
