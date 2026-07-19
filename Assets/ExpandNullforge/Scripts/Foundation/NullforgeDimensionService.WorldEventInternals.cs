using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateWorldEvent(
        DimensionWorldEventDefinition worldEvent,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(worldEvent.EventId))
      {
        result = DimensionOperationResult.Failed("world-event-id-empty", "A world event id is required.");
        return false;
      }

      if (!IsValidWorldEventKind(worldEvent.Kind) || worldEvent.Kind == DimensionWorldEventKind.Any)
      {
        result = DimensionOperationResult.Failed("world-event-kind-invalid", "The world event kind is not supported.");
        return false;
      }

      if (worldEvent.Weight <= 0)
      {
        result = DimensionOperationResult.Failed("world-event-weight-invalid", "The world event weight must be greater than zero.");
        return false;
      }

      if (worldEvent.CooldownSeconds < 0f)
      {
        result = DimensionOperationResult.Failed("world-event-cooldown-invalid", "A world event cooldown cannot be negative.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(worldEvent.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("world-event-dimension-not-found", "The world event dimension is not registered.");
        return false;
      }

      if (!string.IsNullOrEmpty(worldEvent.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (zoneDefinitions.TryGetValue(worldEvent.ZoneId, out zone) &&
            !string.Equals(zone.DimensionId, worldEvent.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("world-event-zone-dimension-mismatch", "The world event zone belongs to another dimension.");
          return false;
        }
      }

      if (!string.IsNullOrEmpty(worldEvent.ProgressFlagId))
      {
        DimensionProgressFlag flag;
        if (progressFlags.TryGetValue(worldEvent.ProgressFlagId, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.Equals(flag.DimensionId, worldEvent.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("world-event-progress-flag-dimension-mismatch", "The world event progress flag belongs to another dimension.");
          return false;
        }
      }

      if (worldEvent.HasLocalBounds)
      {
        if (worldEvent.LocalBounds.Size.x <= 0 || worldEvent.LocalBounds.Size.y <= 0)
        {
          result = DimensionOperationResult.Failed("world-event-bounds-invalid", "The world event local bounds must have a positive size.");
          return false;
        }

        if (!dimension.LocalBounds.Contains(worldEvent.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(worldEvent.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          result = DimensionOperationResult.Failed("world-event-bounds-out-of-dimension", "The world event bounds are outside the world event dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool IsValidWorldEventKind(DimensionWorldEventKind kind)
    {
      return kind == DimensionWorldEventKind.Any ||
             kind == DimensionWorldEventKind.CaveIn ||
             kind == DimensionWorldEventKind.Ambience ||
             kind == DimensionWorldEventKind.Weather ||
             kind == DimensionWorldEventKind.Hazard ||
             kind == DimensionWorldEventKind.SpawnWave ||
             kind == DimensionWorldEventKind.Scripted ||
             kind == DimensionWorldEventKind.Discovery ||
             kind == DimensionWorldEventKind.Custom;
    }

    private static bool WorldEventMatchesQuery(
        DimensionWorldEventDefinition worldEvent,
        DimensionWorldEventQuery query)
    {
      if (query.EnabledOnly && !worldEvent.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(worldEvent.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.ZoneId) &&
          !string.IsNullOrEmpty(worldEvent.ZoneId) &&
          !string.Equals(worldEvent.ZoneId, query.ZoneId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.Kind != DimensionWorldEventKind.Any && worldEvent.Kind != query.Kind)
      {
        return false;
      }

      if (query.HasLocalPosition &&
          worldEvent.HasLocalBounds &&
          !worldEvent.LocalBounds.Contains(query.LocalPosition))
      {
        return false;
      }

      return true;
    }

    private static int CompareWorldEvents(
        DimensionWorldEventDefinition left,
        DimensionWorldEventDefinition right)
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

      int weight = right.Weight.CompareTo(left.Weight);
      if (weight != 0)
      {
        return weight;
      }

      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int zone = string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal);
      if (zone != 0)
      {
        return zone;
      }

      return string.Compare(left.EventId, right.EventId, StringComparison.Ordinal);
    }

    private bool WorldEventEquals(
        DimensionWorldEventDefinition a,
        DimensionWorldEventDefinition b)
    {
      return string.Equals(a.EventId, b.EventId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             a.HasLocalBounds == b.HasLocalBounds &&
             (!a.HasLocalBounds || BoundsEqual(a.LocalBounds, b.LocalBounds)) &&
             a.Kind == b.Kind &&
             string.Equals(a.ProviderId, b.ProviderId, StringComparison.Ordinal) &&
             string.Equals(a.ProgressFlagId, b.ProgressFlagId, StringComparison.Ordinal) &&
             a.Weight == b.Weight &&
             a.Priority == b.Priority &&
             a.CooldownSeconds == b.CooldownSeconds &&
             a.Enabled == b.Enabled;
    }
  }
}
