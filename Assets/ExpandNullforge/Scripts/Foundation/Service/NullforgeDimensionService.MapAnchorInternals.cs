using System;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateMapLayer(
        DimensionMapLayerDefinition layer,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(layer.LayerId))
      {
        result = DimensionOperationResult.Failed("map-layer-id-empty", "A map layer id is required.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(layer.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("map-layer-dimension-not-found", "The map layer dimension is not registered.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareMapLayers(
        DimensionMapLayerDefinition left,
        DimensionMapLayerDefinition right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      return string.Compare(left.LayerId, right.LayerId, StringComparison.Ordinal);
    }

    private bool MapLayerAnchorEquals(
        DimensionMapLayerDefinition a,
        DimensionMapLayerDefinition b)
    {
      return string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal);
    }

    private bool MapLayerEquals(
        DimensionMapLayerDefinition a,
        DimensionMapLayerDefinition b)
    {
      return string.Equals(a.LayerId, b.LayerId, StringComparison.Ordinal) &&
             MapLayerAnchorEquals(a, b) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.Description, b.Description, StringComparison.Ordinal) &&
             string.Equals(a.IconId, b.IconId, StringComparison.Ordinal) &&
             a.HasTintColor == b.HasTintColor &&
             a.TintColorRgba == b.TintColorRgba &&
             a.Priority == b.Priority &&
             a.Visible == b.Visible &&
             a.Selectable == b.Selectable;
    }

    private bool MarkerAnchorEquals(
        DimensionMapMarker a,
        DimensionMapMarker b)
    {
      return string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             a.LocalPosition.x == b.LocalPosition.x &&
             a.LocalPosition.y == b.LocalPosition.y;
    }

    private bool MarkerEquals(
        DimensionMapMarker a,
        DimensionMapMarker b)
    {
      return string.Equals(a.MarkerId, b.MarkerId, StringComparison.Ordinal) &&
             MarkerAnchorEquals(a, b) &&
             string.Equals(a.Label, b.Label, StringComparison.Ordinal) &&
             string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) &&
             a.Visible == b.Visible;
    }

    private bool ValidateAnchor(
        DimensionAnchorDefinition anchor,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(anchor.AnchorId))
      {
        result = DimensionOperationResult.Failed("anchor-id-empty", "An anchor id is required.");
        return false;
      }

      if (!IsValidAnchorKind(anchor.Kind))
      {
        result = DimensionOperationResult.Failed("anchor-kind-invalid", "The anchor kind is not supported.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(anchor.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("anchor-dimension-not-found", "The anchor dimension is not registered.");
        return false;
      }

      if (!dimension.ContainsLocal(anchor.LocalPosition))
      {
        result = DimensionOperationResult.Failed("anchor-position-out-of-bounds", "The anchor position is outside the anchor dimension.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareAnchors(
        DimensionAnchorDefinition left,
        DimensionAnchorDefinition right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int kind = left.Kind.CompareTo(right.Kind);
      if (kind != 0)
      {
        return kind;
      }

      return string.Compare(left.AnchorId, right.AnchorId, StringComparison.Ordinal);
    }

    private bool AnchorLocationEquals(
        DimensionAnchorDefinition a,
        DimensionAnchorDefinition b)
    {
      return string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             a.LocalPosition.x == b.LocalPosition.x &&
             a.LocalPosition.y == b.LocalPosition.y &&
             a.Kind == b.Kind;
    }

    private bool AnchorEquals(
        DimensionAnchorDefinition a,
        DimensionAnchorDefinition b)
    {
      return string.Equals(a.AnchorId, b.AnchorId, StringComparison.Ordinal) &&
             AnchorLocationEquals(a, b) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}