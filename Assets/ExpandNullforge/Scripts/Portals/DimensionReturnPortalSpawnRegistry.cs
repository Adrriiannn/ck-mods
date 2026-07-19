using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Portals
{
  public readonly struct DimensionReturnPortalSpawnDefinition
  {
    public readonly string PortalId;
    public readonly string SourceDimensionId;
    public readonly string PortalObjectName;
    public readonly string TargetDimensionId;
    public readonly float2 SpawnLocalPosition;
    public readonly float2 TargetLocalPosition;
    public readonly DimensionBounds RequiredGeneratedBounds;
    public readonly float ActivationCooldownSeconds;
    public readonly bool RequireGeneratedAreaOnUse;
    public readonly bool AllowFallbackPositionOnUse;
    public readonly bool Interactable;
    public readonly string DisplayName;

    public DimensionReturnPortalSpawnDefinition(
        string portalId,
        string sourceDimensionId,
        string portalObjectName,
        string targetDimensionId,
        float2 spawnLocalPosition,
        float2 targetLocalPosition,
        DimensionBounds requiredGeneratedBounds,
        float activationCooldownSeconds,
        bool requireGeneratedAreaOnUse,
        bool allowFallbackPositionOnUse,
        string displayName,
        bool interactable = true)
    {
      PortalId = portalId ?? string.Empty;
      SourceDimensionId = sourceDimensionId ?? string.Empty;
      PortalObjectName = portalObjectName ?? string.Empty;
      TargetDimensionId = targetDimensionId ?? string.Empty;
      SpawnLocalPosition = spawnLocalPosition;
      TargetLocalPosition = targetLocalPosition;
      RequiredGeneratedBounds = requiredGeneratedBounds;
      ActivationCooldownSeconds = activationCooldownSeconds < 0.0f ? 0.0f : activationCooldownSeconds;
      RequireGeneratedAreaOnUse = requireGeneratedAreaOnUse;
      AllowFallbackPositionOnUse = allowFallbackPositionOnUse;
      Interactable = interactable;
      DisplayName = string.IsNullOrEmpty(displayName) ? PortalId : displayName;
    }

    public bool IsValid
    {
      get
      {
        return !string.IsNullOrEmpty(PortalId) &&
               !string.IsNullOrEmpty(SourceDimensionId) &&
               !string.IsNullOrEmpty(PortalObjectName) &&
               !string.IsNullOrEmpty(TargetDimensionId) &&
               RequiredGeneratedBounds.MaxExclusive.x >= RequiredGeneratedBounds.Min.x &&
               RequiredGeneratedBounds.MaxExclusive.y >= RequiredGeneratedBounds.Min.y;
      }
    }
  }

  public static class DimensionReturnPortalSpawnRegistry
  {
    private static readonly List<DimensionReturnPortalSpawnDefinition> Definitions =
        new List<DimensionReturnPortalSpawnDefinition>();

    public static int Count
    {
      get { return Definitions.Count; }
    }

    public static void Clear()
    {
      Definitions.Clear();
    }

    public static bool Register(DimensionReturnPortalSpawnDefinition definition)
    {
      if (!definition.IsValid)
      {
        return false;
      }

      for (int i = 0; i < Definitions.Count; i++)
      {
        if (string.Equals(Definitions[i].PortalId, definition.PortalId, StringComparison.Ordinal))
        {
          Definitions[i] = definition;
          return true;
        }
      }

      Definitions.Add(definition);
      return true;
    }

    public static bool TryGet(
        int index,
        out DimensionReturnPortalSpawnDefinition definition)
    {
      if (index < 0 || index >= Definitions.Count)
      {
        definition = default;
        return false;
      }

      definition = Definitions[index];
      return true;
    }
  }
}
