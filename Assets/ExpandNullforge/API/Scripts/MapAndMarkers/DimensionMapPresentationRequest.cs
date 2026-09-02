using Unity.Entities;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionMapPresentationRequest
    {
        public readonly Entity Player;
        public readonly DimensionMapPresentationKind Kind;
        public readonly string RequestedLayerId;
        public readonly bool CurrentDimensionOnly;
        public readonly bool IncludeHiddenLayers;
        public readonly bool IncludeUnselectableLayers;
        public readonly bool IncludeHiddenMarkers;
        public readonly float MarkerRadius;

        public DimensionMapPresentationRequest(
            Entity player,
            DimensionMapPresentationKind kind,
            string requestedLayerId,
            bool currentDimensionOnly,
            bool includeHiddenLayers,
            bool includeUnselectableLayers,
            bool includeHiddenMarkers,
            float markerRadius)
        {
            Player = player;
            Kind = kind;
            RequestedLayerId = requestedLayerId ?? string.Empty;
            CurrentDimensionOnly = currentDimensionOnly;
            IncludeHiddenLayers = includeHiddenLayers;
            IncludeUnselectableLayers = includeUnselectableLayers;
            IncludeHiddenMarkers = includeHiddenMarkers;
            MarkerRadius = markerRadius;
        }
    }
}
