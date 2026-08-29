using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private static int CompareBiomes(
        DimensionBiomeDefinition left,
        DimensionBiomeDefinition right)
    {
      int byPriority = right.Priority.CompareTo(left.Priority);
      if (byPriority != 0)
      {
        return byPriority;
      }

      return string.Compare(left.BiomeId, right.BiomeId, StringComparison.Ordinal);
    }

    private bool BiomeMatchesQuery(DimensionBiomeDefinition biome, DimensionBiomeQuery query)
    {
      if (query.EnabledOnly && !biome.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(biome.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.EnvironmentProfileId) &&
          !string.Equals(biome.EnvironmentProfileId, query.EnvironmentProfileId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.PaletteAssetId) &&
          !string.Equals(biome.PaletteAssetId, query.PaletteAssetId, StringComparison.Ordinal))
      {
        return false;
      }

      return true;
    }

    private bool TryResolveBiomeForZone(
        DimensionZoneInfo zone,
        out DimensionBiomeDefinition biome)
    {
      if (!string.IsNullOrEmpty(zone.Kind) &&
          TryGetEnabledBiome(zone.Kind, out biome))
      {
        return true;
      }

      if (!string.IsNullOrEmpty(zone.ZoneId) &&
          TryGetEnabledBiome(zone.ZoneId, out biome))
      {
        return true;
      }

      biome = default(DimensionBiomeDefinition);
      return false;
    }

    private bool TryResolveFallbackBiomeForDimension(
        string dimensionId,
        out DimensionBiomeDefinition biome)
    {
      bool found = false;
      DimensionBiomeDefinition best = default(DimensionBiomeDefinition);
      foreach (DimensionBiomeDefinition candidate in biomes.Values)
      {
        if (!candidate.Enabled ||
            !string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!found || CompareBiomes(candidate, best) < 0)
        {
          best = candidate;
          found = true;
        }
      }

      biome = best;
      return found;
    }

    private bool TryGetEnabledBiome(
        string biomeId,
        out DimensionBiomeDefinition biome)
    {
      if (biomes.TryGetValue(biomeId, out biome) &&
          biome.Enabled)
      {
        return true;
      }

      biome = default(DimensionBiomeDefinition);
      return false;
    }

    private bool ValidateBiome(
        DimensionBiomeDefinition biome,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(biome.BiomeId))
      {
        result = DimensionOperationResult.Failed("biome-id-empty", "A biome id is required.");
        return false;
      }

      if (!string.IsNullOrEmpty(biome.DimensionId) &&
          !definitions.ContainsKey(biome.DimensionId))
      {
        result = DimensionOperationResult.Failed("biome-dimension-not-found", "No dimension with that id is registered.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool BiomeEquals(
        DimensionBiomeDefinition a,
        DimensionBiomeDefinition b)
    {
      return string.Equals(a.BiomeId, b.BiomeId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.EnvironmentProfileId, b.EnvironmentProfileId, StringComparison.Ordinal) &&
             string.Equals(a.PaletteAssetId, b.PaletteAssetId, StringComparison.Ordinal) &&
             string.Equals(a.SpawnTableId, b.SpawnTableId, StringComparison.Ordinal) &&
             string.Equals(a.ResourceTableId, b.ResourceTableId, StringComparison.Ordinal) &&
             string.Equals(a.WorldEventTableId, b.WorldEventTableId, StringComparison.Ordinal) &&
             a.MapColorRgba == b.MapColorRgba &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled &&
             string.Equals(a.Notes, b.Notes, StringComparison.Ordinal);
    }

    private void RemoveBiomesForDimension(string dimensionId)
    {
      List<string> keysToRemove = new List<string>();
      foreach (KeyValuePair<string, DimensionBiomeDefinition> pair in biomes)
      {
        if (string.Equals(pair.Value.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          keysToRemove.Add(pair.Key);
        }
      }

      for (int i = 0; i < keysToRemove.Count; i++)
      {
        DimensionBiomeDefinition biome = biomes[keysToRemove[i]];
        biomes.Remove(keysToRemove[i]);
        RaiseBiomeChanged(
            biome,
            DimensionBiomeChangeKind.DimensionRemoved,
            biome.Enabled,
            false,
            "dimension removed");
      }
    }
  }
}
