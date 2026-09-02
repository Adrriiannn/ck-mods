using System;
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Newtonsoft.Json;
using Pug.ECS.Components;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Persistence
{
  /// <summary>
  /// Making a loaded file safe to use: clamping, trimming and dropping what cannot be read.
  /// </summary>
  public static partial class DimensionWorldRegistry
  {
    private static void NormalizeState(string worldKey)
    {
      if (_state == null)
      {
        _state = NewPayload(worldKey);
        _dirty = true;
      }

      NormalizeSchemaVersion(ref _state.schemaVersion);
      string normalizedWorldKey = worldKey ?? string.Empty;
      if (!string.Equals(_state.worldKey, normalizedWorldKey, StringComparison.Ordinal))
      {
        _state.worldKey = normalizedWorldKey;
        _dirty = true;
      }

      EnsureList(ref _state.dimensions);
      EnsureList(ref _state.dimensionSlots);
      EnsureList(ref _state.players);
      EnsureList(ref _state.playerVisits);
      EnsureList(ref _state.portals);
      EnsureList(ref _state.markers);
      EnsureList(ref _state.anchors);
      EnsureList(ref _state.scenes);
      EnsureList(ref _state.progressFlags);
      EnsureList(ref _state.generatedAreas);
      EnsureList(ref _state.contentOwnership);

      NormalizeDimensionRecords();
      NormalizeDimensionSlotRecords();
      NormalizePlayerRecords();
      NormalizePlayerVisitRecords();
      NormalizePortalRecords();
      NormalizeMarkerRecords();
      NormalizeAnchorRecords();
      NormalizeSceneRecords();
      NormalizeProgressFlagRecords();
      NormalizeGeneratedAreaRecords();
      NormalizeContentOwnershipRecords();
    }

    private static void NormalizeSchemaVersion(ref int schemaVersion)
    {
      if (schemaVersion == DimensionRegistryConstants.RegistrySchemaVersion)
      {
        return;
      }

      schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      _dirty = true;
    }

    private static void EnsureList<T>(ref List<T> list)
    {
      if (list != null)
      {
        return;
      }

      list = new List<T>();
      _dirty = true;
    }

    private static void ReplaceNormalizedList<T>(ref List<T> list, List<T> normalized)
    {
      if (list == null || normalized == null || list.Count != normalized.Count)
      {
        _dirty = true;
      }

      list = normalized ?? new List<T>();
    }

    private static void SetSanitizedName(ref string field, int maxCharacters, string fallback)
    {
      string sanitized = SanitizeName(field, maxCharacters, fallback);
      if (!string.Equals(field, sanitized, StringComparison.Ordinal))
      {
        field = sanitized;
        _dirty = true;
      }
    }

    private static void SetIntIfChanged(ref int field, int value)
    {
      if (field == value)
      {
        return;
      }

      field = value;
      _dirty = true;
    }

    private static void SetFloatIfChanged(ref float field, float value)
    {
      if (field == value)
      {
        return;
      }

      field = value;
      _dirty = true;
    }

    private static float NormalizeProgress01(float value)
    {
      if (float.IsNaN(value) || float.IsInfinity(value))
      {
        return 0.0f;
      }

      return Mathf.Clamp01(value);
    }

    private static void NormalizeDimensionRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionDefinitionRecord> normalized = new List<DimensionDefinitionRecord>();
      for (int i = 0; i < _state.dimensions.Count; i++)
      {
        DimensionDefinitionRecord record = _state.dimensions[i];
        if (record == null || string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.dimensionId);
        SetIntIfChanged(ref record.generationVersion, Math.Max(1, record.generationVersion));

        // Zero is a real value here — it means "made before layout versions existed" — so unlike
        // generationVersion this must not be floored to 1, which would claim a pin nobody set.
        SetIntIfChanged(ref record.layoutVersion, Math.Max(0, record.layoutVersion));
        SetSanitizedName(ref record.layoutFingerprint, 32, string.Empty);
        if (record.localMaxExclusiveX <= record.localMinX || record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        if (!ids.Add(record.dimensionId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.dimensions, normalized);
    }

    private static void NormalizeDimensionSlotRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionSlotPersistenceRecord> normalized =
          new List<DimensionSlotPersistenceRecord>();
      for (int i = 0; i < _state.dimensionSlots.Count; i++)
      {
        DimensionSlotPersistenceRecord record = _state.dimensionSlots[i];
        if (record == null || string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.allocationCode, 64, string.Empty);
        SetSanitizedName(ref record.allocationMessage, 256, string.Empty);
        if (record.localMaxExclusiveX <= record.localMinX ||
            record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        if (!ids.Add(record.dimensionId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.dimensionSlots, normalized);
    }

    private static void NormalizePortalRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionPortalRecord> normalized = new List<DimensionPortalRecord>();
      for (int i = 0; i < _state.portals.Count; i++)
      {
        DimensionPortalRecord record = _state.portals[i];
        if (record == null || string.IsNullOrEmpty(record.portalId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.portalId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.portalId);
        SetSanitizedName(ref record.fromDimensionId, 128, string.Empty);
        SetSanitizedName(ref record.toDimensionId, 128, string.Empty);
        if (!ids.Add(record.portalId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.portals, normalized);
    }

    private static void NormalizeMarkerRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionMarkerRecord> normalized = new List<DimensionMarkerRecord>();
      for (int i = 0; i < _state.markers.Count; i++)
      {
        DimensionMarkerRecord record = _state.markers[i];
        if (record == null || string.IsNullOrEmpty(record.markerId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.markerId, 128, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.label, 128, record.markerId);
        SetSanitizedName(ref record.kind, 64, string.Empty);
        if (!ids.Add(record.markerId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.markers, normalized);
    }

    private static void NormalizeAnchorRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionAnchorRecord> normalized = new List<DimensionAnchorRecord>();
      for (int i = 0; i < _state.anchors.Count; i++)
      {
        DimensionAnchorRecord record = _state.anchors[i];
        if (record == null || string.IsNullOrEmpty(record.anchorId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.anchorId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.anchorId);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        if (!IsValidAnchorKind((DimensionAnchorKind)record.kind))
        {
          SetIntIfChanged(ref record.kind, (int)DimensionAnchorKind.Fallback);
        }

        if (!ids.Add(record.anchorId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.anchors, normalized);
    }

    private static void NormalizeSceneRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionSceneRecord> normalized = new List<DimensionSceneRecord>();
      for (int i = 0; i < _state.scenes.Count; i++)
      {
        DimensionSceneRecord record = _state.scenes[i];
        if (record == null || string.IsNullOrEmpty(record.sceneId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.sceneId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.sceneId);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.kind, 64, string.Empty);
        if (record.localMaxExclusiveX <= record.localMinX ||
            record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        if (!IsValidSceneState((DimensionSceneState)record.state))
        {
          SetIntIfChanged(ref record.state, (int)DimensionSceneState.Planned);
        }

        if (!ids.Add(record.sceneId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.scenes, normalized);
    }

    private static void NormalizeProgressFlagRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionProgressFlagRecord> normalized = new List<DimensionProgressFlagRecord>();
      for (int i = 0; i < _state.progressFlags.Count; i++)
      {
        DimensionProgressFlagRecord record = _state.progressFlags[i];
        if (record == null || string.IsNullOrEmpty(record.flagId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.flagId, 128, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.category, 64, string.Empty);
        if (!ids.Add(record.flagId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.progressFlags, normalized);
    }

    private static bool IsValidAnchorKind(DimensionAnchorKind kind)
    {
      return kind == DimensionAnchorKind.Entry ||
             kind == DimensionAnchorKind.Return ||
             kind == DimensionAnchorKind.Respawn ||
             kind == DimensionAnchorKind.Fallback ||
             kind == DimensionAnchorKind.Checkpoint ||
             kind == DimensionAnchorKind.Portal ||
             kind == DimensionAnchorKind.Scene ||
             kind == DimensionAnchorKind.Debug;
    }

    private static bool IsValidSceneState(DimensionSceneState state)
    {
      return state == DimensionSceneState.Planned ||
             state == DimensionSceneState.Reserved ||
             state == DimensionSceneState.Generating ||
             state == DimensionSceneState.Ready ||
             state == DimensionSceneState.Disabled ||
             state == DimensionSceneState.Error;
    }

    private static void NormalizePlayerRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionPlayerStateRecord> normalized = new List<DimensionPlayerStateRecord>();
      for (int i = 0; i < _state.players.Count; i++)
      {
        DimensionPlayerStateRecord record = _state.players[i];
        if (record == null || string.IsNullOrEmpty(record.playerId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.playerId, 96, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, DimensionIds.Overworld);
        if (!ids.Add(record.playerId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.players, normalized);
    }

    private static void NormalizePlayerVisitRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionPlayerVisitStateRecord> normalized =
          new List<DimensionPlayerVisitStateRecord>();
      for (int i = 0; i < _state.playerVisits.Count; i++)
      {
        DimensionPlayerVisitStateRecord record = _state.playerVisits[i];
        if (record == null ||
            string.IsNullOrEmpty(record.playerId) ||
            string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.playerId, 96, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, DimensionIds.Overworld);

        string key = record.playerId + "|" + record.dimensionId;
        if (!ids.Add(key))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.playerVisits, normalized);
    }

    private static void NormalizeGeneratedAreaRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionGeneratedAreaRecord> normalized = new List<DimensionGeneratedAreaRecord>();
      for (int i = 0; i < _state.generatedAreas.Count; i++)
      {
        DimensionGeneratedAreaRecord record = _state.generatedAreas[i];
        if (record == null || string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.message, 256, string.Empty);
        SetFloatIfChanged(ref record.progress01, NormalizeProgress01(record.progress01));
        if (record.localMaxExclusiveX <= record.localMinX || record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        string key =
            record.dimensionId +
            "|" +
            record.localMinX +
            "," +
            record.localMinY +
            "," +
            record.localMaxExclusiveX +
            "," +
            record.localMaxExclusiveY;
        if (!ids.Add(key))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.generatedAreas, normalized);
    }

    private static void NormalizeContentOwnershipRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionContentOwnershipRecord> normalized =
          new List<DimensionContentOwnershipRecord>();
      for (int i = 0; i < _state.contentOwnership.Count; i++)
      {
        DimensionContentOwnershipRecord record = _state.contentOwnership[i];
        if (record == null ||
            string.IsNullOrEmpty(record.contentPackId) ||
            string.IsNullOrEmpty(record.recordId) ||
            !IsValidContentRecordKind((DimensionContentRecordKind)record.recordKind))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.contentPackId, 128, string.Empty);
        SetSanitizedName(ref record.recordId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, string.Empty);
        SetSanitizedName(ref record.notes, 256, string.Empty);

        string key = record.recordKind + "|" + record.recordId;
        if (!ids.Add(key))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.contentOwnership, normalized);
    }

    private static bool IsValidContentRecordKind(DimensionContentRecordKind kind)
    {
      return kind == DimensionContentRecordKind.Dimension ||
             kind == DimensionContentRecordKind.Portal ||
             kind == DimensionContentRecordKind.PortalPresentation ||
             kind == DimensionContentRecordKind.TravelRequirement ||
             kind == DimensionContentRecordKind.TravelRequirementEvaluator ||
             kind == DimensionContentRecordKind.MapLayer ||
             kind == DimensionContentRecordKind.MapMarker ||
             kind == DimensionContentRecordKind.Anchor ||
             kind == DimensionContentRecordKind.Biome ||
             kind == DimensionContentRecordKind.GenerationTable ||
             kind == DimensionContentRecordKind.GenerationTableEntry ||
             kind == DimensionContentRecordKind.ZoneDefinition ||
             kind == DimensionContentRecordKind.ZoneProvider ||
             kind == DimensionContentRecordKind.EnvironmentProfile ||
             kind == DimensionContentRecordKind.Scene ||
             kind == DimensionContentRecordKind.SceneTemplate ||
             kind == DimensionContentRecordKind.Encounter ||
             kind == DimensionContentRecordKind.ResourceNode ||
             kind == DimensionContentRecordKind.SpawnRule ||
             kind == DimensionContentRecordKind.WorldEvent ||
             kind == DimensionContentRecordKind.ProgressFlag ||
             kind == DimensionContentRecordKind.GenerationProvider ||
             kind == DimensionContentRecordKind.GenerationPass ||
             kind == DimensionContentRecordKind.AccessProvider ||
             kind == DimensionContentRecordKind.AssetReference ||
             kind == DimensionContentRecordKind.Custom;
    }
  }
}
