using System;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateEncounter(
        DimensionEncounterDefinition encounter,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(encounter.EncounterId))
      {
        result = DimensionOperationResult.Failed("encounter-id-empty", "An encounter id is required.");
        return false;
      }

      if (!IsValidEncounterKind(encounter.Kind) || encounter.Kind == DimensionEncounterKind.Any)
      {
        result = DimensionOperationResult.Failed("encounter-kind-invalid", "The encounter kind is not supported.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(encounter.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("encounter-dimension-not-found", "The encounter dimension is not registered.");
        return false;
      }

      if (!string.IsNullOrEmpty(encounter.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (zoneDefinitions.TryGetValue(encounter.ZoneId, out zone) &&
            !string.Equals(zone.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("encounter-zone-dimension-mismatch", "The encounter zone belongs to another dimension.");
          return false;
        }
      }

      if (!string.IsNullOrEmpty(encounter.SceneId))
      {
        DimensionSceneDefinition scene;
        if (scenes.TryGetValue(encounter.SceneId, out scene) &&
            !string.Equals(scene.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("encounter-scene-dimension-mismatch", "The encounter scene belongs to another dimension.");
          return false;
        }
      }

      if (!string.IsNullOrEmpty(encounter.MarkerId))
      {
        DimensionMapMarker marker;
        if (markers.TryGetValue(encounter.MarkerId, out marker) &&
            !string.Equals(marker.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("encounter-marker-dimension-mismatch", "The encounter marker belongs to another dimension.");
          return false;
        }
      }

      if (!string.IsNullOrEmpty(encounter.DefeatFlagId))
      {
        DimensionProgressFlag flag;
        if (progressFlags.TryGetValue(encounter.DefeatFlagId, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.Equals(flag.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("encounter-defeat-flag-dimension-mismatch", "The encounter defeat flag belongs to another dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool IsValidEncounterKind(DimensionEncounterKind kind)
    {
      return kind == DimensionEncounterKind.Any ||
             kind == DimensionEncounterKind.Boss ||
             kind == DimensionEncounterKind.MiniBoss ||
             kind == DimensionEncounterKind.Elite ||
             kind == DimensionEncounterKind.SceneEvent ||
             kind == DimensionEncounterKind.AmbientEvent ||
             kind == DimensionEncounterKind.Custom;
    }

    private static bool EncounterMatchesQuery(
        DimensionEncounterDefinition encounter,
        string dimensionId,
        string zoneId,
        DimensionEncounterKind kind,
        bool includeDisabled)
    {
      if (!includeDisabled && !encounter.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(dimensionId) &&
          !string.Equals(encounter.DimensionId, dimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(zoneId) &&
          !string.IsNullOrEmpty(encounter.ZoneId) &&
          !string.Equals(encounter.ZoneId, zoneId, StringComparison.Ordinal))
      {
        return false;
      }

      if (kind != DimensionEncounterKind.Any && encounter.Kind != kind)
      {
        return false;
      }

      return true;
    }

    private static int CompareEncounters(
        DimensionEncounterDefinition left,
        DimensionEncounterDefinition right)
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

      return string.Compare(left.EncounterId, right.EncounterId, StringComparison.Ordinal);
    }

    private bool EncounterEquals(
        DimensionEncounterDefinition a,
        DimensionEncounterDefinition b)
    {
      return string.Equals(a.EncounterId, b.EncounterId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             string.Equals(a.SceneId, b.SceneId, StringComparison.Ordinal) &&
             string.Equals(a.MarkerId, b.MarkerId, StringComparison.Ordinal) &&
             string.Equals(a.DefeatFlagId, b.DefeatFlagId, StringComparison.Ordinal) &&
             a.Kind == b.Kind &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}