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
  /// Reading what the registry holds, and finding the row one id belongs to.
  /// </summary>
  public static partial class DimensionWorldRegistry
  {
    public static DimensionRegistrySnapshot GetSnapshot()
    {
      EnsureLoadedForCurrentWorld();
      RegistryPayload state = _state;
      return new DimensionRegistrySnapshot(
          _loaded,
          _dirty,
          _dirty && !double.IsPositiveInfinity(_nextFlushAt),
          state != null ? state.schemaVersion : DimensionRegistryConstants.RegistrySchemaVersion,
          _generation,
          state != null ? state.registryRevision : 0UL,
          _consecutiveFlushFailures,
          _worldKey,
          CountOrZero(state != null ? state.dimensions : null),
          CountOrZero(state != null ? state.dimensionSlots : null),
          CountOrZero(state != null ? state.players : null),
          CountOrZero(state != null ? state.playerVisits : null),
          CountOrZero(state != null ? state.portals : null),
          CountOrZero(state != null ? state.markers : null),
          CountOrZero(state != null ? state.anchors : null),
          CountOrZero(state != null ? state.scenes : null),
          CountOrZero(state != null ? state.progressFlags : null),
          CountOrZero(state != null ? state.generatedAreas : null),
          CountOrZero(state != null ? state.contentOwnership : null));
    }

    public static void GetDimensions(List<DimensionDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensions == null)
      {
        return;
      }

      for (int i = 0; i < _state.dimensions.Count; i++)
      {
        DimensionDefinitionRecord record = _state.dimensions[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToDefinition(record));
      }
    }

    public static void GetDimensionSlots(List<DimensionSlotRecord> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensionSlots == null)
      {
        return;
      }

      for (int i = 0; i < _state.dimensionSlots.Count; i++)
      {
        DimensionSlotPersistenceRecord record = _state.dimensionSlots[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToSlotRecord(record));
      }
    }

    public static bool TryGetDimensionSlot(string dimensionId, out DimensionSlotRecord slot)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded ||
          _state == null ||
          _state.dimensionSlots == null ||
          string.IsNullOrEmpty(dimensionId))
      {
        slot = default(DimensionSlotRecord);
        return false;
      }

      int index = FindDimensionSlotRecordIndex(dimensionId);
      if (index < 0)
      {
        slot = default(DimensionSlotRecord);
        return false;
      }

      slot = ToSlotRecord(_state.dimensionSlots[index]);
      return !string.IsNullOrEmpty(slot.DimensionId);
    }

    public static void GetPortals(List<DimensionPortalDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.portals == null)
      {
        return;
      }

      for (int i = 0; i < _state.portals.Count; i++)
      {
        DimensionPortalRecord record = _state.portals[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToPortal(record));
      }
    }

    public static void GetMarkers(List<DimensionMapMarker> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.markers == null)
      {
        return;
      }

      for (int i = 0; i < _state.markers.Count; i++)
      {
        DimensionMarkerRecord record = _state.markers[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToMarker(record));
      }
    }

    public static void GetAnchors(List<DimensionAnchorDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.anchors == null)
      {
        return;
      }

      for (int i = 0; i < _state.anchors.Count; i++)
      {
        DimensionAnchorRecord record = _state.anchors[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToAnchor(record));
      }
    }

    public static void GetScenes(List<DimensionSceneDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.scenes == null)
      {
        return;
      }

      for (int i = 0; i < _state.scenes.Count; i++)
      {
        DimensionSceneRecord record = _state.scenes[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToScene(record));
      }
    }

    public static void GetProgressFlags(List<DimensionProgressFlag> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.progressFlags == null)
      {
        return;
      }

      for (int i = 0; i < _state.progressFlags.Count; i++)
      {
        DimensionProgressFlagRecord record = _state.progressFlags[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToProgressFlag(record));
      }
    }

    public static void GetGeneratedAreas(List<DimensionGenerationStatus> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.generatedAreas == null)
      {
        return;
      }

      for (int i = 0; i < _state.generatedAreas.Count; i++)
      {
        DimensionGeneratedAreaRecord record = _state.generatedAreas[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToGenerationStatus(record));
      }
    }

    public static void GetContentOwnershipBindings(List<DimensionContentOwnershipBinding> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.contentOwnership == null)
      {
        return;
      }

      for (int i = 0; i < _state.contentOwnership.Count; i++)
      {
        DimensionContentOwnershipRecord record = _state.contentOwnership[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToContentOwnership(record));
      }
    }

    /// <summary>
    /// Reads back which layout version generated this world's copy of a dimension.
    /// </summary>
    /// <returns>False when this world has no pin yet, which is the normal state of a new world.</returns>
    public static bool TryGetLayoutPin(string dimensionId, out int layoutVersion, out string layoutFingerprint)
    {
      layoutVersion = 0;
      layoutFingerprint = string.Empty;

      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return false;
      }

      int index = FindDimensionRecordIndex(dimensionId);
      if (index < 0)
      {
        return false;
      }

      DimensionDefinitionRecord record = _state.dimensions[index];
      if (record == null || record.layoutVersion <= 0)
      {
        return false;
      }

      layoutVersion = record.layoutVersion;
      layoutFingerprint = record.layoutFingerprint ?? string.Empty;
      return true;
    }

    public static bool TryGetBossDefeat(string bossObjectName, out long defeatedUtcTicks)
    {
      defeatedUtcTicks = 0L;
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.bossDefeats == null)
      {
        return false;
      }

      int index = FindBossDefeatRecordIndex(bossObjectName);
      if (index < 0)
      {
        return false;
      }

      defeatedUtcTicks = _state.bossDefeats[index].defeatedUtcTicks;
      return true;
    }

    private static int FindBossDefeatRecordIndex(string bossObjectName)
    {
      if (_state == null || _state.bossDefeats == null || string.IsNullOrEmpty(bossObjectName))
      {
        return -1;
      }

      for (int i = 0; i < _state.bossDefeats.Count; i++)
      {
        if (string.Equals(
                _state.bossDefeats[i].bossObjectName,
                bossObjectName,
                StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int CountOrZero<T>(List<T> list)
    {
      return list != null ? list.Count : 0;
    }

    private static int FindDimensionRecordIndex(string dimensionId)
    {
      if (_state == null || _state.dimensions == null || string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.dimensions.Count; i++)
      {
        DimensionDefinitionRecord record = _state.dimensions[i];
        if (record != null && string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindDimensionSlotRecordIndex(string dimensionId)
    {
      if (_state == null || _state.dimensionSlots == null || string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.dimensionSlots.Count; i++)
      {
        DimensionSlotPersistenceRecord record = _state.dimensionSlots[i];
        if (record != null &&
            string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindPortalRecordIndex(string portalId)
    {
      if (_state == null || _state.portals == null || string.IsNullOrEmpty(portalId))
      {
        return -1;
      }

      for (int i = 0; i < _state.portals.Count; i++)
      {
        DimensionPortalRecord record = _state.portals[i];
        if (record != null && string.Equals(record.portalId, portalId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindMarkerRecordIndex(string markerId)
    {
      if (_state == null || _state.markers == null || string.IsNullOrEmpty(markerId))
      {
        return -1;
      }

      for (int i = 0; i < _state.markers.Count; i++)
      {
        DimensionMarkerRecord record = _state.markers[i];
        if (record != null && string.Equals(record.markerId, markerId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindAnchorRecordIndex(string anchorId)
    {
      if (_state == null || _state.anchors == null || string.IsNullOrEmpty(anchorId))
      {
        return -1;
      }

      for (int i = 0; i < _state.anchors.Count; i++)
      {
        DimensionAnchorRecord record = _state.anchors[i];
        if (record != null && string.Equals(record.anchorId, anchorId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindSceneRecordIndex(string sceneId)
    {
      if (_state == null || _state.scenes == null || string.IsNullOrEmpty(sceneId))
      {
        return -1;
      }

      for (int i = 0; i < _state.scenes.Count; i++)
      {
        DimensionSceneRecord record = _state.scenes[i];
        if (record != null && string.Equals(record.sceneId, sceneId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindProgressFlagRecordIndex(string flagId)
    {
      if (_state == null || _state.progressFlags == null || string.IsNullOrEmpty(flagId))
      {
        return -1;
      }

      for (int i = 0; i < _state.progressFlags.Count; i++)
      {
        DimensionProgressFlagRecord record = _state.progressFlags[i];
        if (record != null && string.Equals(record.flagId, flagId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindPlayerRecordIndex(string playerId)
    {
      if (_state == null || _state.players == null || string.IsNullOrEmpty(playerId))
      {
        return -1;
      }

      for (int i = 0; i < _state.players.Count; i++)
      {
        DimensionPlayerStateRecord record = _state.players[i];
        if (record != null && string.Equals(record.playerId, playerId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindPlayerVisitRecordIndex(string playerId, string dimensionId)
    {
      if (_state == null ||
          _state.playerVisits == null ||
          string.IsNullOrEmpty(playerId) ||
          string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.playerVisits.Count; i++)
      {
        DimensionPlayerVisitStateRecord record = _state.playerVisits[i];
        if (record != null &&
            string.Equals(record.playerId, playerId, StringComparison.Ordinal) &&
            string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindGeneratedAreaRecordIndex(string dimensionId, DimensionBounds localBounds)
    {
      if (_state == null || _state.generatedAreas == null || string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.generatedAreas.Count; i++)
      {
        DimensionGeneratedAreaRecord record = _state.generatedAreas[i];
        if (record != null &&
            string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal) &&
            record.localMinX == localBounds.Min.x &&
            record.localMinY == localBounds.Min.y &&
            record.localMaxExclusiveX == localBounds.MaxExclusive.x &&
            record.localMaxExclusiveY == localBounds.MaxExclusive.y)
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindContentOwnershipRecordIndex(int recordKind, string recordId)
    {
      if (_state == null ||
          _state.contentOwnership == null ||
          string.IsNullOrEmpty(recordId))
      {
        return -1;
      }

      for (int i = 0; i < _state.contentOwnership.Count; i++)
      {
        DimensionContentOwnershipRecord record = _state.contentOwnership[i];
        if (record != null &&
            record.recordKind == recordKind &&
            string.Equals(record.recordId, recordId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }
  }
}
