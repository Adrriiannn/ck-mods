using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionZoneCatalogService
    {
        event Action<DimensionZoneChangedEvent> ZoneChanged;

        IReadOnlyList<DimensionZoneDefinition> GetZoneDefinitions(
            string dimensionId,
            bool includeDisabled);

        bool TryGetZoneDefinition(string zoneId, out DimensionZoneDefinition zone);

        bool TryFindZoneDefinitionAtLocal(
            string dimensionId,
            float2 localPosition,
            out DimensionZoneDefinition zone);

        bool TryRegisterZoneDefinition(
            DimensionZoneDefinition zone,
            out DimensionOperationResult result);

        bool TryUpdateZoneDefinition(
            DimensionZoneDefinition zone,
            string reason,
            out DimensionOperationResult result);

        bool TrySetZoneDefinitionEnabled(
            string zoneId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveZoneDefinition(string zoneId, out DimensionOperationResult result);
    }
}
