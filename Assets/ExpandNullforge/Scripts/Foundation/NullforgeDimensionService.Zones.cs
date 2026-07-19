using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionZoneDefinition> GetZoneDefinitions(
        string dimensionId,
        bool includeDisabled)
    {
      List<DimensionZoneDefinition> result = new List<DimensionZoneDefinition>();
      foreach (DimensionZoneDefinition zone in zoneDefinitions.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(zone.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!includeDisabled && !zone.Enabled)
        {
          continue;
        }

        result.Add(zone);
      }

      result.Sort(CompareZoneDefinitions);
      return result;
    }

    public bool TryGetZoneDefinition(string zoneId, out DimensionZoneDefinition zone)
    {
      if (string.IsNullOrEmpty(zoneId))
      {
        zone = default(DimensionZoneDefinition);
        return false;
      }

      return zoneDefinitions.TryGetValue(zoneId, out zone);
    }

    public bool TryFindZoneDefinitionAtLocal(
        string dimensionId,
        float2 localPosition,
        out DimensionZoneDefinition zone)
    {
      bool found = false;
      DimensionZoneDefinition best = default(DimensionZoneDefinition);
      foreach (DimensionZoneDefinition candidate in zoneDefinitions.Values)
      {
        if (!candidate.Enabled ||
            !string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal) ||
            !candidate.LocalBounds.Contains(localPosition))
        {
          continue;
        }

        if (!found || CompareZoneDefinitions(candidate, best) < 0)
        {
          best = candidate;
          found = true;
        }
      }

      zone = best;
      return found;
    }

    public bool TryRegisterZoneDefinition(
        DimensionZoneDefinition zone,
        out DimensionOperationResult result)
    {
      if (!ValidateZoneDefinition(zone, out result))
      {
        return false;
      }

      if (zoneDefinitions.ContainsKey(zone.ZoneId))
      {
        result = DimensionOperationResult.Failed("zone-already-registered", "A zone with that id is already registered.");
        return false;
      }

      zoneDefinitions[zone.ZoneId] = zone;
      RaiseZoneChanged(
          zone,
          DimensionZoneChangeKind.Registered,
          false,
          zone.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateZoneDefinition(
        DimensionZoneDefinition zone,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionZoneDefinition previous;
      if (!zoneDefinitions.TryGetValue(zone.ZoneId, out previous))
      {
        result = DimensionOperationResult.Failed("zone-not-found", "No zone with that id is registered.");
        return false;
      }

      if (!ValidateZoneDefinition(zone, out result))
      {
        return false;
      }

      if (ZoneDefinitionEquals(previous, zone))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      zoneDefinitions[zone.ZoneId] = zone;
      RaiseZoneChanged(
          zone,
          previous.Enabled == zone.Enabled
              ? DimensionZoneChangeKind.Updated
              : DimensionZoneChangeKind.EnabledChanged,
          previous.Enabled,
          zone.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetZoneDefinitionEnabled(
        string zoneId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(zoneId))
      {
        result = DimensionOperationResult.Failed("zone-id-empty", "A zone id is required.");
        return false;
      }

      DimensionZoneDefinition zone;
      if (!zoneDefinitions.TryGetValue(zoneId, out zone))
      {
        result = DimensionOperationResult.Failed("zone-not-found", "No zone with that id is registered.");
        return false;
      }

      if (zone.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionZoneDefinition updated =
          new DimensionZoneDefinition(
              zone.ZoneId,
              zone.DisplayName,
              zone.DimensionId,
              zone.LocalBounds,
              zone.Kind,
              zone.Priority,
              enabled);

      zoneDefinitions[zoneId] = updated;
      RaiseZoneChanged(
          updated,
          DimensionZoneChangeKind.EnabledChanged,
          zone.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveZoneDefinition(string zoneId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(zoneId))
      {
        result = DimensionOperationResult.Failed("zone-id-empty", "A zone id is required.");
        return false;
      }

      DimensionZoneDefinition zone;
      if (!zoneDefinitions.TryGetValue(zoneId, out zone))
      {
        result = DimensionOperationResult.Failed("zone-not-found", "No zone with that id is registered.");
        return false;
      }

      zoneDefinitions.Remove(zoneId);
      RaiseZoneChanged(
          zone,
          DimensionZoneChangeKind.Removed,
          zone.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
