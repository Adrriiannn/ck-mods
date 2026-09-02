namespace ExpandNullforge.Api
{
    using System;
    using System.Collections.Generic;

    public interface IDimensionMapLayerService
    {
        event Action<DimensionMapLayerChangedEvent> MapLayerChanged;

        IReadOnlyList<DimensionMapLayerDefinition> GetMapLayers(
            string dimensionId,
            bool includeHidden,
            bool includeUnselectable);

        bool TryGetMapLayer(
            string layerId,
            out DimensionMapLayerDefinition layer);

        bool TryFindMapLayerForDimension(
            string dimensionId,
            out DimensionMapLayerDefinition layer);

        bool TryRegisterMapLayer(
            DimensionMapLayerDefinition layer,
            out DimensionOperationResult result);

        bool TryUpdateMapLayer(
            DimensionMapLayerDefinition layer,
            string reason,
            out DimensionOperationResult result);

        bool TrySetMapLayerVisibility(
            string layerId,
            bool visible,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveMapLayer(
            string layerId,
            out DimensionOperationResult result);
    }
}
