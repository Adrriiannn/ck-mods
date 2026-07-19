using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionEncounterDefinition> GetEncounters(
        string dimensionId,
        string zoneId,
        DimensionEncounterKind kind,
        bool includeDisabled)
    {
      List<DimensionEncounterDefinition> result =
          new List<DimensionEncounterDefinition>();
      foreach (DimensionEncounterDefinition encounter in encounters.Values)
      {
        if (!EncounterMatchesQuery(encounter, dimensionId, zoneId, kind, includeDisabled))
        {
          continue;
        }

        result.Add(encounter);
      }

      result.Sort(CompareEncounters);
      return result;
    }

    public bool TryGetEncounter(
        string encounterId,
        out DimensionEncounterDefinition encounter)
    {
      if (string.IsNullOrEmpty(encounterId))
      {
        encounter = default(DimensionEncounterDefinition);
        return false;
      }

      return encounters.TryGetValue(encounterId, out encounter);
    }

    public bool TryFindEncounterForScene(
        string sceneId,
        out DimensionEncounterDefinition encounter)
    {
      encounter = default(DimensionEncounterDefinition);
      if (string.IsNullOrEmpty(sceneId))
      {
        return false;
      }

      bool found = false;
      foreach (DimensionEncounterDefinition candidate in encounters.Values)
      {
        if (!candidate.Enabled ||
            !string.Equals(candidate.SceneId, sceneId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!found || CompareEncounters(candidate, encounter) < 0)
        {
          encounter = candidate;
          found = true;
        }
      }

      return found;
    }

    public bool TryRegisterEncounter(
        DimensionEncounterDefinition encounter,
        out DimensionOperationResult result)
    {
      if (!ValidateEncounter(encounter, out result))
      {
        return false;
      }

      if (encounters.ContainsKey(encounter.EncounterId))
      {
        result = DimensionOperationResult.Failed("encounter-already-registered", "An encounter with that id is already registered.");
        return false;
      }

      encounters[encounter.EncounterId] = encounter;
      RaiseEncounterChanged(
          encounter,
          DimensionEncounterChangeKind.Registered,
          false,
          encounter.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateEncounter(
        DimensionEncounterDefinition encounter,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionEncounterDefinition previous;
      if (string.IsNullOrEmpty(encounter.EncounterId))
      {
        result = DimensionOperationResult.Failed("encounter-id-empty", "An encounter id is required.");
        return false;
      }

      if (!encounters.TryGetValue(encounter.EncounterId, out previous))
      {
        result = DimensionOperationResult.Failed("encounter-not-found", "No encounter with that id is registered.");
        return false;
      }

      if (!ValidateEncounter(encounter, out result))
      {
        return false;
      }

      if (EncounterEquals(previous, encounter))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      encounters[encounter.EncounterId] = encounter;
      RaiseEncounterChanged(
          encounter,
          previous.Enabled == encounter.Enabled
              ? DimensionEncounterChangeKind.Updated
              : DimensionEncounterChangeKind.EnabledChanged,
          previous.Enabled,
          encounter.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetEncounterEnabled(
        string encounterId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(encounterId))
      {
        result = DimensionOperationResult.Failed("encounter-id-empty", "An encounter id is required.");
        return false;
      }

      DimensionEncounterDefinition encounter;
      if (!encounters.TryGetValue(encounterId, out encounter))
      {
        result = DimensionOperationResult.Failed("encounter-not-found", "No encounter with that id is registered.");
        return false;
      }

      if (encounter.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionEncounterDefinition updated =
          new DimensionEncounterDefinition(
              encounter.EncounterId,
              encounter.DisplayName,
              encounter.DimensionId,
              encounter.ZoneId,
              encounter.SceneId,
              encounter.SpawnRuleId,
              encounter.MarkerId,
              encounter.DefeatFlagId,
              encounter.Kind,
              encounter.Priority,
              enabled);
      encounters[encounterId] = updated;
      RaiseEncounterChanged(
          updated,
          DimensionEncounterChangeKind.EnabledChanged,
          encounter.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveEncounter(
        string encounterId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(encounterId))
      {
        result = DimensionOperationResult.Failed("encounter-id-empty", "An encounter id is required.");
        return false;
      }

      DimensionEncounterDefinition encounter;
      if (!encounters.TryGetValue(encounterId, out encounter))
      {
        result = DimensionOperationResult.Failed("encounter-not-found", "No encounter with that id is registered.");
        return false;
      }

      encounters.Remove(encounterId);
      RaiseEncounterChanged(
          encounter,
          DimensionEncounterChangeKind.Removed,
          encounter.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
