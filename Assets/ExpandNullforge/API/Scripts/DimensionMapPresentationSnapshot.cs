using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionMapPresentationSnapshot
    {
        public readonly bool IsValid;
        public readonly string Message;
        public readonly DimensionMapPresentationKind Kind;
        public readonly DimensionContext ViewerContext;
        public readonly DimensionMapLayerDefinition ActiveLayer;
        public readonly IReadOnlyList<DimensionMapLayerDefinition> AvailableLayers;
        public readonly IReadOnlyList<DimensionMapMarker> VisibleMarkers;
        public readonly string CoordinateText;
        public readonly bool UseVanillaCoordinateDisplay;
        public readonly bool UseVanillaMapSurface;
        public readonly bool CurrentDimensionOnly;

        public DimensionMapPresentationSnapshot(
            bool isValid,
            string message,
            DimensionMapPresentationKind kind,
            DimensionContext viewerContext,
            DimensionMapLayerDefinition activeLayer,
            IReadOnlyList<DimensionMapLayerDefinition> availableLayers,
            IReadOnlyList<DimensionMapMarker> visibleMarkers,
            string coordinateText,
            bool useVanillaCoordinateDisplay,
            bool useVanillaMapSurface,
            bool currentDimensionOnly)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
            Kind = kind;
            ViewerContext = viewerContext;
            ActiveLayer = activeLayer;
            AvailableLayers = availableLayers ?? new List<DimensionMapLayerDefinition>();
            VisibleMarkers = visibleMarkers ?? new List<DimensionMapMarker>();
            CoordinateText = coordinateText ?? string.Empty;
            UseVanillaCoordinateDisplay = useVanillaCoordinateDisplay;
            UseVanillaMapSurface = useVanillaMapSurface;
            CurrentDimensionOnly = currentDimensionOnly;
        }
    }
}
