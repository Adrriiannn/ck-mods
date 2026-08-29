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

    /// <summary>
    /// The portal only spawns once its dimension's victory arms it — the Arena bundle.
    /// False keeps today's always-on guarantee, which is what every existing definition gets.
    /// </summary>
    public readonly bool ArmedByVictory;

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
        bool interactable = true,
        bool armedByVictory = false)
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
      ArmedByVictory = armedByVictory;
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

    /// <summary>Whether any return portal leads out of this dimension.</summary>
    public static bool HasDefinitionForSource(string dimensionId)
    {
      for (int i = 0; i < Definitions.Count; i++)
      {
        if (string.Equals(Definitions[i].SourceDimensionId, dimensionId, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }

    // ---- victory arming (Arena) ----

    private static readonly HashSet<string> ArmedPortalIds =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Lets a victory-armed portal spawn. Session state; the flag store persists it.</summary>
    public static void Arm(string portalId)
    {
      if (!string.IsNullOrEmpty(portalId))
      {
        ArmedPortalIds.Add(portalId);
      }
    }

    public static void Disarm(string portalId)
    {
      if (!string.IsNullOrEmpty(portalId))
      {
        ArmedPortalIds.Remove(portalId);
      }
    }

    public static bool IsArmed(string portalId)
    {
      return !string.IsNullOrEmpty(portalId) && ArmedPortalIds.Contains(portalId);
    }

    public static void DisarmAll()
    {
      ArmedPortalIds.Clear();
    }
  }
}
