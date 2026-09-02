using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionMapMarker> GetMarkers(DimensionMarkerQuery query)
    {
      List<DimensionMapMarker> result = new List<DimensionMapMarker>();
      float radiusSquared = query.Radius * query.Radius;

      foreach (DimensionMapMarker marker in markers.Values)
      {
        if (!string.IsNullOrEmpty(query.DimensionId)
            && !string.Equals(marker.DimensionId, query.DimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!query.IncludeHidden && !marker.Visible)
        {
          continue;
        }

        if (query.Radius > 0f && math.lengthsq(marker.LocalPosition - query.CenterLocalPosition) > radiusSquared)
        {
          continue;
        }

        result.Add(marker);
      }

      return result;
    }

    public bool TryRegisterMarker(DimensionMapMarker marker, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(marker.MarkerId))
      {
        result = DimensionOperationResult.Failed("marker-id-empty", "A marker id is required.");
        return false;
      }

      if (markers.ContainsKey(marker.MarkerId))
      {
        result = DimensionOperationResult.Failed("marker-already-registered", "A marker with that id is already registered.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(marker.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("marker-dimension-not-found", "The marker dimension is not registered.");
        return false;
      }

      if (!dimension.ContainsLocal(marker.LocalPosition))
      {
        result = DimensionOperationResult.Failed("marker-position-out-of-bounds", "The marker position is outside the marker dimension.");
        return false;
      }

      markers[marker.MarkerId] = marker;
      PersistMarkerIfWorldRegistryLoaded(marker);
      RaiseMarkerChanged(
          default(DimensionMapMarker),
          marker,
          DimensionMarkerChangeKind.Registered,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateMarker(
        DimensionMapMarker marker,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(marker.MarkerId))
      {
        result = DimensionOperationResult.Failed("marker-id-empty", "A marker id is required.");
        return false;
      }

      DimensionMapMarker previous;
      if (!markers.TryGetValue(marker.MarkerId, out previous))
      {
        result = DimensionOperationResult.Failed("marker-not-found", "No marker with that id is registered.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(marker.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("marker-dimension-not-found", "The marker dimension is not registered.");
        return false;
      }

      if (!dimension.ContainsLocal(marker.LocalPosition))
      {
        result = DimensionOperationResult.Failed("marker-position-out-of-bounds", "The marker position is outside the marker dimension.");
        return false;
      }

      if (MarkerEquals(previous, marker))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      markers[marker.MarkerId] = marker;
      PersistMarkerIfWorldRegistryLoaded(marker);
      RaiseMarkerChanged(
          previous,
          marker,
          DimensionMarkerChangeKind.Updated,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetMarkerVisibility(
        string markerId,
        bool visible,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(markerId))
      {
        result = DimensionOperationResult.Failed("marker-id-empty", "A marker id is required.");
        return false;
      }

      DimensionMapMarker marker;
      if (!markers.TryGetValue(markerId, out marker))
      {
        result = DimensionOperationResult.Failed("marker-not-found", "No marker with that id is registered.");
        return false;
      }

      if (marker.Visible == visible)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionMapMarker updated =
          new DimensionMapMarker(
              marker.MarkerId,
              marker.DimensionId,
              marker.LocalPosition,
              marker.Label,
              marker.Kind,
              visible);
      markers[markerId] = updated;
      PersistMarkerIfWorldRegistryLoaded(updated);
      RaiseMarkerChanged(
          marker,
          updated,
          DimensionMarkerChangeKind.VisibilityChanged,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveMarker(string markerId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(markerId))
      {
        result = DimensionOperationResult.Failed("marker-id-empty", "A marker id is required.");
        return false;
      }

      DimensionMapMarker marker;
      if (!markers.TryGetValue(markerId, out marker))
      {
        result = DimensionOperationResult.Failed("marker-not-found", "No marker with that id is registered.");
        return false;
      }

      markers.Remove(markerId);
      RemovePersistedMarkerIfWorldRegistryLoaded(markerId);
      RaiseMarkerChanged(
          marker,
          default(DimensionMapMarker),
          DimensionMarkerChangeKind.Removed,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<DimensionAnchorDefinition> GetAnchors(
        string dimensionId,
        DimensionAnchorKind kind,
        bool enabledOnly)
    {
      List<DimensionAnchorDefinition> result = new List<DimensionAnchorDefinition>();
      foreach (DimensionAnchorDefinition anchor in anchors.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(anchor.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (kind != DimensionAnchorKind.Any && anchor.Kind != kind)
        {
          continue;
        }

        if (enabledOnly && !anchor.Enabled)
        {
          continue;
        }

        result.Add(anchor);
      }

      result.Sort(CompareAnchors);
      return result;
    }

    public bool TryGetAnchor(string anchorId, out DimensionAnchorDefinition anchor)
    {
      if (string.IsNullOrEmpty(anchorId))
      {
        anchor = default(DimensionAnchorDefinition);
        return false;
      }

      return anchors.TryGetValue(anchorId, out anchor);
    }

    public bool TryResolveBestAnchor(
        string dimensionId,
        DimensionAnchorKind kind,
        out DimensionAnchorDefinition anchor)
    {
      anchor = default(DimensionAnchorDefinition);
      if (string.IsNullOrEmpty(dimensionId) || kind == DimensionAnchorKind.Any)
      {
        return false;
      }

      bool found = false;
      foreach (DimensionAnchorDefinition candidate in anchors.Values)
      {
        if (!candidate.Enabled ||
            candidate.Kind != kind ||
            !string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!found || CompareAnchors(candidate, anchor) < 0)
        {
          anchor = candidate;
          found = true;
        }
      }

      return found;
    }

    public bool TryRegisterAnchor(DimensionAnchorDefinition anchor, out DimensionOperationResult result)
    {
      if (!ValidateAnchor(anchor, out result))
      {
        return false;
      }

      if (anchors.ContainsKey(anchor.AnchorId))
      {
        result = DimensionOperationResult.Failed("anchor-already-registered", "An anchor with that id is already registered.");
        return false;
      }

      anchors[anchor.AnchorId] = anchor;
      PersistAnchorIfWorldRegistryLoaded(anchor);
      RaiseAnchorChanged(
          anchor,
          DimensionAnchorChangeKind.Registered,
          false,
          anchor.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateAnchor(
        DimensionAnchorDefinition anchor,
        string reason,
        out DimensionOperationResult result)
    {
      if (!ValidateAnchor(anchor, out result))
      {
        return false;
      }

      DimensionAnchorDefinition previous;
      if (!anchors.TryGetValue(anchor.AnchorId, out previous))
      {
        result = DimensionOperationResult.Failed("anchor-not-found", "No anchor with that id is registered.");
        return false;
      }

      if (AnchorEquals(previous, anchor))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      anchors[anchor.AnchorId] = anchor;
      PersistAnchorIfWorldRegistryLoaded(anchor);
      RaiseAnchorChanged(
          anchor,
          previous.Enabled == anchor.Enabled
              ? DimensionAnchorChangeKind.Updated
              : DimensionAnchorChangeKind.EnabledChanged,
          previous.Enabled,
          anchor.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetAnchorEnabled(
        string anchorId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(anchorId))
      {
        result = DimensionOperationResult.Failed("anchor-id-empty", "An anchor id is required.");
        return false;
      }

      DimensionAnchorDefinition anchor;
      if (!anchors.TryGetValue(anchorId, out anchor))
      {
        result = DimensionOperationResult.Failed("anchor-not-found", "No anchor with that id is registered.");
        return false;
      }

      if (anchor.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionAnchorDefinition updated =
          new DimensionAnchorDefinition(
              anchor.AnchorId,
              anchor.DisplayName,
              anchor.DimensionId,
              anchor.LocalPosition,
              anchor.Kind,
              anchor.Priority,
              enabled);
      anchors[anchorId] = updated;
      PersistAnchorIfWorldRegistryLoaded(updated);
      RaiseAnchorChanged(
          updated,
          DimensionAnchorChangeKind.EnabledChanged,
          anchor.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveAnchor(string anchorId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(anchorId))
      {
        result = DimensionOperationResult.Failed("anchor-id-empty", "An anchor id is required.");
        return false;
      }

      DimensionAnchorDefinition anchor;
      if (!anchors.TryGetValue(anchorId, out anchor))
      {
        result = DimensionOperationResult.Failed("anchor-not-found", "No anchor with that id is registered.");
        return false;
      }

      anchors.Remove(anchorId);
      RemovePersistedAnchorIfWorldRegistryLoaded(anchorId);
      RaiseAnchorChanged(
          anchor,
          DimensionAnchorChangeKind.Removed,
          anchor.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
