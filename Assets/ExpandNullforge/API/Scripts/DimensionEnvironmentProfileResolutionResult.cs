using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionEnvironmentProfileResolutionResult
    {
        public readonly bool Success;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly bool HasZone;
        public readonly DimensionZoneInfo Zone;
        public readonly bool HasBiome;
        public readonly DimensionBiomeDefinition Biome;
        public readonly bool HasProfile;
        public readonly DimensionEnvironmentProfile Profile;
        public readonly DimensionEnvironmentProfileResolutionSource Source;
        public readonly string Code;
        public readonly string Message;

        public DimensionEnvironmentProfileResolutionResult(
            bool success,
            string dimensionId,
            float2 localPosition,
            bool hasZone,
            DimensionZoneInfo zone,
            bool hasBiome,
            DimensionBiomeDefinition biome,
            bool hasProfile,
            DimensionEnvironmentProfile profile,
            DimensionEnvironmentProfileResolutionSource source,
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
            HasProfile = hasProfile;
            Profile = profile;
            Source = source;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
