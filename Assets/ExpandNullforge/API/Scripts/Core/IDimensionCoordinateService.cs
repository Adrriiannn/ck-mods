using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionCoordinateService
    {
        bool TryGetDimensionAtAbsolute(float2 absolutePosition, out DimensionDefinition definition);

        DimensionContext GetContextForAbsolute(float2 absolutePosition);

        bool TryGetCoordinateDomain(string dimensionId, out DimensionCoordinateDomain domain);

        bool TryGetCoordinateDomainAtAbsolute(float2 absolutePosition, out DimensionCoordinateDomain domain);

        DimensionContext GetCoordinateContextForAbsolute(float2 absolutePosition);

        bool TryToLocal(string dimensionId, float2 absolutePosition, out float2 localPosition);

        bool TryToAbsolute(string dimensionId, float2 localPosition, out float2 absolutePosition);

        bool TryGetArea(string dimensionId, DimensionBounds localBounds, out DimensionArea area);

        bool TryGetZoneAtLocal(string dimensionId, float2 localPosition, out DimensionZoneInfo zone);

        bool TryRegisterZoneProvider(IDimensionZoneProvider provider, out DimensionOperationResult result);

        bool TryRemoveZoneProvider(string providerId, out DimensionOperationResult result);

        DimensionResolveResult ResolveAbsolute(float2 absolutePosition);

        DimensionResolveResult ResolveLocal(string dimensionId, float2 localPosition);
    }
}
