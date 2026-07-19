using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionMapLayerDefinition> GetMapLayers(
        string dimensionId,
        bool includeHidden,
        bool includeUnselectable)
    {
      List<DimensionMapLayerDefinition> result =
          new List<DimensionMapLayerDefinition>();
      foreach (DimensionMapLayerDefinition layer in mapLayers.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(layer.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!includeHidden && !layer.Visible)
        {
          continue;
        }

        if (!includeUnselectable && !layer.Selectable)
        {
          continue;
        }

        result.Add(layer);
      }

      result.Sort(CompareMapLayers);
      return result;
    }

    public bool TryGetMapLayer(
        string layerId,
        out DimensionMapLayerDefinition layer)
    {
      if (string.IsNullOrEmpty(layerId))
      {
        layer = default(DimensionMapLayerDefinition);
        return false;
      }

      return mapLayers.TryGetValue(layerId, out layer);
    }

    public bool TryFindMapLayerForDimension(
        string dimensionId,
        out DimensionMapLayerDefinition layer)
    {
      List<DimensionMapLayerDefinition> candidates =
          new List<DimensionMapLayerDefinition>();
      foreach (DimensionMapLayerDefinition candidate in mapLayers.Values)
      {
        if (!candidate.Visible ||
            !candidate.Selectable ||
            !string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        candidates.Add(candidate);
      }

      candidates.Sort(CompareMapLayers);
      if (candidates.Count > 0)
      {
        layer = candidates[0];
        return true;
      }

      layer = default(DimensionMapLayerDefinition);
      return false;
    }

    public bool TryRegisterMapLayer(
        DimensionMapLayerDefinition layer,
        out DimensionOperationResult result)
    {
      if (!ValidateMapLayer(layer, out result))
      {
        return false;
      }

      if (mapLayers.ContainsKey(layer.LayerId))
      {
        result = DimensionOperationResult.Failed("map-layer-already-registered", "A map layer with that id is already registered.");
        return false;
      }

      mapLayers[layer.LayerId] = layer;
      RaiseMapLayerChanged(
          default(DimensionMapLayerDefinition),
          layer,
          DimensionMapLayerChangeKind.Registered,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateMapLayer(
        DimensionMapLayerDefinition layer,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionMapLayerDefinition previous;
      if (string.IsNullOrEmpty(layer.LayerId))
      {
        result = DimensionOperationResult.Failed("map-layer-id-empty", "A map layer id is required.");
        return false;
      }

      if (!mapLayers.TryGetValue(layer.LayerId, out previous))
      {
        result = DimensionOperationResult.Failed("map-layer-not-found", "No map layer with that id is registered.");
        return false;
      }

      if (!ValidateMapLayer(layer, out result))
      {
        return false;
      }

      if (IsProtectedMapLayerId(layer.LayerId) &&
          !MapLayerAnchorEquals(previous, layer))
      {
        result = DimensionOperationResult.Failed("map-layer-protected", "Built-in map layers cannot be moved to another dimension.");
        return false;
      }

      if (MapLayerEquals(previous, layer))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      mapLayers[layer.LayerId] = layer;
      RaiseMapLayerChanged(
          previous,
          layer,
          layer.Visible == previous.Visible
              ? DimensionMapLayerChangeKind.Updated
              : DimensionMapLayerChangeKind.VisibilityChanged,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetMapLayerVisibility(
        string layerId,
        bool visible,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(layerId))
      {
        result = DimensionOperationResult.Failed("map-layer-id-empty", "A map layer id is required.");
        return false;
      }

      DimensionMapLayerDefinition layer;
      if (!mapLayers.TryGetValue(layerId, out layer))
      {
        result = DimensionOperationResult.Failed("map-layer-not-found", "No map layer with that id is registered.");
        return false;
      }

      if (layer.Visible == visible)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionMapLayerDefinition updated =
          new DimensionMapLayerDefinition(
              layer.LayerId,
              layer.DimensionId,
              layer.DisplayName,
              layer.Description,
              layer.IconId,
              layer.HasTintColor,
              layer.TintColorRgba,
              layer.Priority,
              visible,
              layer.Selectable);
      mapLayers[layerId] = updated;
      RaiseMapLayerChanged(
          layer,
          updated,
          DimensionMapLayerChangeKind.VisibilityChanged,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveMapLayer(
        string layerId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(layerId))
      {
        result = DimensionOperationResult.Failed("map-layer-id-empty", "A map layer id is required.");
        return false;
      }

      if (IsProtectedMapLayerId(layerId))
      {
        result = DimensionOperationResult.Failed("map-layer-protected", "Built-in map layers cannot be removed.");
        return false;
      }

      DimensionMapLayerDefinition layer;
      if (!mapLayers.TryGetValue(layerId, out layer))
      {
        result = DimensionOperationResult.Failed("map-layer-not-found", "No map layer with that id is registered.");
        return false;
      }

      mapLayers.Remove(layerId);
      RaiseMapLayerChanged(
          layer,
          default(DimensionMapLayerDefinition),
          DimensionMapLayerChangeKind.Removed,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public DimensionMapPresentationSnapshot GetMapPresentation(DimensionMapPresentationRequest request)
    {
      DimensionContext context = ResolveMapPresentationContext(request.Player);
      bool currentDimensionOnly =
          request.CurrentDimensionOnly ||
          request.Kind == DimensionMapPresentationKind.Minimap;
      string currentDimensionId =
          context.IsKnown
              ? context.DimensionId
              : DimensionIds.Overworld;

      DimensionMapLayerDefinition requestedLayer = default(DimensionMapLayerDefinition);
      bool hasRequestedLayer =
          !string.IsNullOrEmpty(request.RequestedLayerId) &&
          mapLayers.TryGetValue(request.RequestedLayerId, out requestedLayer) &&
          IsMapPresentationLayerAllowed(
              requestedLayer,
              request.IncludeHiddenLayers,
              request.IncludeUnselectableLayers);

      string layerDimensionFilter = currentDimensionOnly ? currentDimensionId : string.Empty;
      List<DimensionMapLayerDefinition> availableLayers =
          BuildMapPresentationLayerList(
              layerDimensionFilter,
              request.IncludeHiddenLayers,
              request.IncludeUnselectableLayers);

      DimensionMapLayerDefinition activeLayer = default(DimensionMapLayerDefinition);
      bool hasActiveLayer =
          hasRequestedLayer &&
          (!currentDimensionOnly ||
           string.Equals(requestedLayer.DimensionId, currentDimensionId, StringComparison.Ordinal)) &&
          ContainsMapLayer(availableLayers, requestedLayer.LayerId);

      if (hasActiveLayer)
      {
        activeLayer = requestedLayer;
      }
      else if (!TryFindPresentationLayerForDimension(availableLayers, currentDimensionId, out activeLayer) &&
               availableLayers.Count > 0)
      {
        activeLayer = availableLayers[0];
        hasActiveLayer = true;
      }
      else
      {
        hasActiveLayer = availableLayers.Count > 0;
      }

      if (!hasActiveLayer)
      {
        return new DimensionMapPresentationSnapshot(
            false,
            "No visible/selectable dimension map layer is available.",
            request.Kind,
            context,
            default(DimensionMapLayerDefinition),
            availableLayers,
            new List<DimensionMapMarker>(),
            string.Empty,
            false,
            false,
            currentDimensionOnly);
      }

      bool activeLayerIsCurrentDimension =
          context.IsKnown &&
          string.Equals(activeLayer.DimensionId, context.DimensionId, StringComparison.Ordinal);
      DimensionMarkerQuery markerQuery =
          new DimensionMarkerQuery(
              activeLayer.DimensionId,
              activeLayerIsCurrentDimension ? context.LocalPosition : float2.zero,
              activeLayerIsCurrentDimension ? request.MarkerRadius : 0f,
              request.IncludeHiddenMarkers);
      IReadOnlyList<DimensionMapMarker> visibleMarkers = GetMarkers(markerQuery);
      bool useVanillaSurface =
          string.Equals(activeLayer.DimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
      string coordinateText =
          activeLayerIsCurrentDimension
              ? FormatMapCoordinateText(context)
              : string.Empty;

      return new DimensionMapPresentationSnapshot(
          true,
          "Map presentation resolved.",
          request.Kind,
          context,
          activeLayer,
          availableLayers,
          visibleMarkers,
          coordinateText,
          useVanillaSurface,
          useVanillaSurface,
          currentDimensionOnly);
    }

    private DimensionContext ResolveMapPresentationContext(Entity player)
    {
      DimensionContext context;
      if (player != Entity.Null)
      {
        if (TryGetPlayerContext(player, out context) && context.IsKnown)
        {
          return context;
        }

        if (TryGetPersistedPlayerContext(player, out context) && context.IsKnown)
        {
          return context;
        }
      }

      return DimensionContext.Overworld(default(float2));
    }

    private List<DimensionMapLayerDefinition> BuildMapPresentationLayerList(
        string dimensionId,
        bool includeHidden,
        bool includeUnselectable)
    {
      List<DimensionMapLayerDefinition> result =
          new List<DimensionMapLayerDefinition>();
      foreach (DimensionMapLayerDefinition layer in mapLayers.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(layer.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!IsMapPresentationLayerAllowed(layer, includeHidden, includeUnselectable))
        {
          continue;
        }

        result.Add(layer);
      }

      result.Sort(CompareMapLayers);
      return result;
    }

    private bool IsMapPresentationLayerAllowed(
        DimensionMapLayerDefinition layer,
        bool includeHidden,
        bool includeUnselectable)
    {
      if (!includeHidden && !layer.Visible)
      {
        return false;
      }

      if (!includeUnselectable && !layer.Selectable)
      {
        return false;
      }

      return true;
    }

    private bool ContainsMapLayer(
        IReadOnlyList<DimensionMapLayerDefinition> layers,
        string layerId)
    {
      for (int i = 0; i < layers.Count; i++)
      {
        if (string.Equals(layers[i].LayerId, layerId, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }

    private bool TryFindPresentationLayerForDimension(
        IReadOnlyList<DimensionMapLayerDefinition> layers,
        string dimensionId,
        out DimensionMapLayerDefinition layer)
    {
      for (int i = 0; i < layers.Count; i++)
      {
        if (string.Equals(layers[i].DimensionId, dimensionId, StringComparison.Ordinal))
        {
          layer = layers[i];
          return true;
        }
      }

      layer = default(DimensionMapLayerDefinition);
      return false;
    }

    private string FormatMapCoordinateText(DimensionContext context)
    {
      int x = (int)math.floor(context.LocalPosition.x);
      int y = (int)math.floor(context.LocalPosition.y);
      return x + ", " + y;
    }

  }
}
