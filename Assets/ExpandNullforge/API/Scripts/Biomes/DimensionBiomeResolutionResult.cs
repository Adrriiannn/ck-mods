using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeResolutionResult
    {
        public readonly bool Success;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly bool HasZone;
        public readonly DimensionZoneInfo Zone;
        public readonly bool HasBiome;
        public readonly DimensionBiomeDefinition Biome;
        public readonly string Code;
        public readonly string Message;

        public DimensionBiomeResolutionResult(
            bool success,
            string dimensionId,
            float2 localPosition,
            bool hasZone,
            DimensionZoneInfo zone,
            bool hasBiome,
            DimensionBiomeDefinition biome,
            string code,
            string message)
        {
            Success = success;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            HasZone = hasZone;
            Zone = zone;
            HasBiome = hasBiome;
            Biome = biome;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
