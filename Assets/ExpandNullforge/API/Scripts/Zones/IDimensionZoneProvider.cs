using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionZoneProvider
    {
        string ProviderId { get; }

        bool CanResolveZone(DimensionDefinition dimension);

        bool TryGetZoneAtLocal(
            DimensionDefinition dimension,
            float2 localPosition,
            out DimensionZoneInfo zone);
    }
}
