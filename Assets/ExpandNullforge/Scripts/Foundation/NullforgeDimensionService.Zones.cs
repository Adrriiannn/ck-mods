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
      BindZoneToGenerationGates(zone);
      RaiseZoneChanged(
          zone,
          DimensionZoneChangeKind.Registered,
          false,
          zone.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    /// <summary>
    /// Tells the generation gates where a zone's biome actually is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bootstrap knows which ores and which blocks a biome names, but not the geography — the
    /// bounds exist only once a zone is registered. So both gates keep the biome's answer waiting
    /// and this marries it to every zone whose Kind is that biome's id. Kind IS the biome id: the
    /// manifest builder passes the biome id as the zone's kind, and the generated bootstrap does
    /// the same for the zones it ensures.
    /// </para>
    /// <para>
    /// This lives HERE, at the bottom of registration, rather than at the one manifest-apply call
    /// site it used to sit at. Zones created by the generated bootstrap's minimum-zone path never
    /// went through that call site, so a dimension that never applied a content manifest bound
    /// nothing and both gates stayed inert for it. Every path that can put a zone in the catalog
    /// runs through register or update, so this is the one place that cannot be walked around.
    /// </para>
    /// </remarks>
    private static void BindZoneToGenerationGates(DimensionZoneDefinition zone)
    {
      Generation.DimensionOreBiomeGate.BindZone(
          zone.DimensionId, zone.Kind, zone.LocalBounds);
      Generation.DimensionTerrainMaterialRegistry.BindZone(
          zone.DimensionId, zone.Kind, zone.LocalBounds);
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
        // Nothing about the zone moved, but re-binding is still right and still free: both gates
        // replace a row for the same biome and bounds rather than stacking a second one, and an
        // update that arrives before the zone was ever bound would otherwise never bind at all.
        BindZoneToGenerationGates(zone);
        result = DimensionOperationResult.Ok();
        return true;
      }

      zoneDefinitions[zone.ZoneId] = zone;
      BindZoneToGenerationGates(zone);
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
