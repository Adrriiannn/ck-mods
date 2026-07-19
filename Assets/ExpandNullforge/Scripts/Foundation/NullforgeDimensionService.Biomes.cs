using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionBiomeDefinition> GetBiomes(DimensionBiomeQuery query)
    {
      List<DimensionBiomeDefinition> result = new List<DimensionBiomeDefinition>();
      foreach (DimensionBiomeDefinition biome in biomes.Values)
      {
        if (!BiomeMatchesQuery(biome, query))
        {
          continue;
        }

        result.Add(biome);
      }

      result.Sort(CompareBiomes);
      return result;
    }

    public DimensionBiomeResolutionResult ResolveBiomeAtLocal(
        string dimensionId,
        float2 localPosition)
    {
      DimensionDefinition dimension;
      if (!definitions.TryGetValue(dimensionId, out dimension) ||
          !dimension.ContainsLocal(localPosition))
      {
        return new DimensionBiomeResolutionResult(
            false,
            dimensionId,
            localPosition,
            false,
            default(DimensionZoneInfo),
            false,
            default(DimensionBiomeDefinition),
            "dimension-position-not-found",
            "No dimension contains that local position.");
      }

      DimensionZoneInfo zone;
      if (TryGetZoneAtLocal(dimensionId, localPosition, out zone))
      {
        DimensionBiomeDefinition zoneBiome;
        if (TryResolveBiomeForZone(zone, out zoneBiome))
        {
          return new DimensionBiomeResolutionResult(
              true,
              dimensionId,
              localPosition,
              true,
              zone,
              true,
              zoneBiome,
              string.Empty,
              string.Empty);
        }

        DimensionBiomeDefinition fallbackBiome;
        if (TryResolveFallbackBiomeForDimension(dimensionId, out fallbackBiome))
        {
          return new DimensionBiomeResolutionResult(
              true,
              dimensionId,
              localPosition,
              true,
              zone,
              true,
              fallbackBiome,
              "biome-fallback",
              "No zone-specific biome matched; using the highest-priority dimension biome.");
        }

        return new DimensionBiomeResolutionResult(
            false,
            dimensionId,
            localPosition,
            true,
            zone,
            false,
            default(DimensionBiomeDefinition),
            "biome-not-found-for-zone",
            "The zone resolved, but no matching biome profile is registered.");
      }

      DimensionBiomeDefinition dimensionBiome;
      if (TryResolveFallbackBiomeForDimension(dimensionId, out dimensionBiome))
      {
        return new DimensionBiomeResolutionResult(
            true,
            dimensionId,
            localPosition,
            false,
            default(DimensionZoneInfo),
            true,
            dimensionBiome,
            "biome-fallback-no-zone",
            "No zone resolved; using the highest-priority dimension biome.");
      }

      return new DimensionBiomeResolutionResult(
          false,
          dimensionId,
          localPosition,
          false,
          default(DimensionZoneInfo),
          false,
          default(DimensionBiomeDefinition),
          "zone-and-biome-not-found",
          "No zone or fallback biome is registered for that position.");
    }

    public bool TryGetBiome(
        string biomeId,
        out DimensionBiomeDefinition biome)
    {
      if (string.IsNullOrEmpty(biomeId))
      {
        biome = default(DimensionBiomeDefinition);
        return false;
      }

      return biomes.TryGetValue(biomeId, out biome);
    }

    public bool TryRegisterBiome(
        DimensionBiomeDefinition biome,
        out DimensionOperationResult result)
    {
      if (!ValidateBiome(biome, out result))
      {
        return false;
      }

      if (biomes.ContainsKey(biome.BiomeId))
      {
        result = DimensionOperationResult.Failed("biome-already-registered", "A biome with that id is already registered.");
        return false;
      }

      biomes[biome.BiomeId] = biome;
      RaiseBiomeChanged(
          biome,
          DimensionBiomeChangeKind.Registered,
          false,
          biome.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateBiome(
        DimensionBiomeDefinition biome,
        string reason,
        out DimensionOperationResult result)
    {
      if (!ValidateBiome(biome, out result))
      {
        return false;
      }

      DimensionBiomeDefinition previous;
      if (!biomes.TryGetValue(biome.BiomeId, out previous))
      {
        result = DimensionOperationResult.Failed("biome-not-found", "No biome with that id is registered.");
        return false;
      }

      if (BiomeEquals(previous, biome))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      biomes[biome.BiomeId] = biome;
      RaiseBiomeChanged(
          biome,
          previous.Enabled == biome.Enabled
              ? DimensionBiomeChangeKind.Updated
              : DimensionBiomeChangeKind.EnabledChanged,
          previous.Enabled,
          biome.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetBiomeEnabled(
        string biomeId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(biomeId))
      {
        result = DimensionOperationResult.Failed("biome-id-empty", "A biome id is required.");
        return false;
      }

      DimensionBiomeDefinition biome;
      if (!biomes.TryGetValue(biomeId, out biome))
      {
        result = DimensionOperationResult.Failed("biome-not-found", "No biome with that id is registered.");
        return false;
      }

      if (biome.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionBiomeDefinition updated =
          new DimensionBiomeDefinition(
              biome.BiomeId,
              biome.DisplayName,
              biome.DimensionId,
              biome.EnvironmentProfileId,
              biome.PaletteAssetId,
              biome.SpawnTableId,
              biome.ResourceTableId,
              biome.WorldEventTableId,
              biome.MapColorRgba,
              biome.Priority,
              enabled,
              biome.Notes);

      biomes[biomeId] = updated;
      RaiseBiomeChanged(
          updated,
          DimensionBiomeChangeKind.EnabledChanged,
          biome.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveBiome(
        string biomeId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(biomeId))
      {
        result = DimensionOperationResult.Failed("biome-id-empty", "A biome id is required.");
        return false;
      }

      DimensionBiomeDefinition biome;
      if (!biomes.TryGetValue(biomeId, out biome))
      {
        result = DimensionOperationResult.Failed("biome-not-found", "No biome with that id is registered.");
        return false;
      }

      biomes.Remove(biomeId);
      RaiseBiomeChanged(
          biome,
          DimensionBiomeChangeKind.Removed,
          biome.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
