using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  // Zone validation and ordering.
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateZoneDefinition(
        DimensionZoneDefinition zone,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(zone.ZoneId))
      {
        result = DimensionOperationResult.Failed("zone-id-empty", "A zone id is required.");
        return false;
      }

      if (zone.LocalBounds.Size.x <= 0 || zone.LocalBounds.Size.y <= 0)
      {
        result = DimensionOperationResult.Failed("zone-bounds-invalid", "The zone local bounds must have a positive size.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(zone.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("zone-dimension-not-found", "The zone dimension is not registered.");
        return false;
      }

      if (!dimension.LocalBounds.Contains(zone.LocalBounds.Min) ||
          !dimension.LocalBounds.Contains(zone.LocalBounds.MaxExclusive - new int2(1, 1)))
      {
        result = DimensionOperationResult.Failed("zone-bounds-out-of-dimension", "The zone bounds are outside the zone dimension.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareZoneDefinitions(
        DimensionZoneDefinition left,
        DimensionZoneDefinition right)
    {
      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int kind = string.Compare(left.Kind, right.Kind, StringComparison.Ordinal);
      if (kind != 0)
      {
        return kind;
      }

      return string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal);
    }

    private bool ZoneDefinitionEquals(
        DimensionZoneDefinition a,
        DimensionZoneDefinition b)
    {
      return string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             BoundsEqual(a.LocalBounds, b.LocalBounds) &&
             string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}
