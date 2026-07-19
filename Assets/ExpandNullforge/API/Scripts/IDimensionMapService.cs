namespace ExpandNullforge.Api
{
    using System;
    using System.Collections.Generic;

    public interface IDimensionMapService
    {
        event Action<DimensionMarkerChangedEvent> MarkerChanged;

        IReadOnlyList<DimensionMapMarker> GetMarkers(DimensionMarkerQuery query);

        bool TryRegisterMarker(DimensionMapMarker marker, out DimensionOperationResult result);

        bool TryUpdateMarker(
            DimensionMapMarker marker,
            string reason,
            out DimensionOperationResult result);

        bool TrySetMarkerVisibility(
            string markerId,
            bool visible,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveMarker(string markerId, out DimensionOperationResult result);
    }
}
