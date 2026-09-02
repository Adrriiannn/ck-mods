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
  /// Putting a row in, taking one out, and deciding whether anything actually changed.
  /// </summary>
  public static partial class DimensionWorldRegistry
  {
    /// <summary>
    /// Records which layout version generated this world's copy of a dimension.
    /// </summary>
    /// <remarks>
    /// Refuses to overwrite an existing pin. The pin describes terrain that already exists on disk,
    /// and the one situation where a caller most wants to "correct" it — the mod shipped a new layout
    /// — is exactly the situation where changing it would erase the record of what the player's world
    /// is actually made of.
    /// </remarks>
    public static bool TryStampLayoutPin(string dimensionId, int layoutVersion, string layoutFingerprint)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || string.IsNullOrEmpty(dimensionId) || layoutVersion <= 0)
      {
        return false;
      }

      int index = FindDimensionRecordIndex(dimensionId);
      if (index < 0)
      {
        return false;
      }

      DimensionDefinitionRecord record = _state.dimensions[index];
      if (record == null || record.layoutVersion > 0)
      {
        return false;
      }

      record.layoutVersion = layoutVersion;
      record.layoutFingerprint = SanitizeName(layoutFingerprint, 32, string.Empty);
      Touch(true);
      return true;
    }

    public static void UpsertDimension(DimensionDefinition definition, bool builtIn)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.dimensions = _state.dimensions ?? new List<DimensionDefinitionRecord>();
      DimensionDefinitionRecord record = ToRecord(definition, builtIn);
      int index = FindDimensionRecordIndex(record.dimensionId);
      if (index >= 0)
      {
        // The layout pin belongs to the WORLD, not to the definition being upserted — it records
        // which layout made this save's terrain. ToRecord knows nothing about it, so without this
        // carry-over every registration would quietly erase the pin and the world would be treated
        // as brand new on the next load.
        record.layoutVersion = _state.dimensions[index].layoutVersion;
        record.layoutFingerprint = _state.dimensions[index].layoutFingerprint;

        if (RecordsEqual(_state.dimensions[index], record))
        {
          return;
        }

        _state.dimensions[index] = record;
      }
      else
      {
        _state.dimensions.Add(record);
      }

      if (IsNonOverworld(record.dimensionId))
      {
        _hasNonOverworldFootprint = true;
        Touch(false);
      }
      else
      {
        Touch(true);
      }
    }

    public static void UpsertDimensionSlot(DimensionSlotRecord slot)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || string.IsNullOrEmpty(slot.DimensionId))
      {
        return;
      }

      _state.dimensionSlots =
          _state.dimensionSlots ?? new List<DimensionSlotPersistenceRecord>();
      DimensionSlotPersistenceRecord record = ToRecord(slot);
      int index = FindDimensionSlotRecordIndex(record.dimensionId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.dimensionSlots[index], record))
        {
          return;
        }

        _state.dimensionSlots[index] = record;
      }
      else
      {
        _state.dimensionSlots.Add(record);
      }

      if (IsNonOverworld(record.dimensionId))
      {
        _hasNonOverworldFootprint = true;
        Touch(false);
      }
      else
      {
        Touch(true);
      }
    }

    public static void RemoveDimensionSlot(string dimensionId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensionSlots == null)
      {
        return;
      }

      int index = FindDimensionSlotRecordIndex(dimensionId);
      if (index < 0)
      {
        return;
      }

      _state.dimensionSlots.RemoveAt(index);
      Touch();
    }

    public static void UpsertPortal(DimensionPortalDefinition portal)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.portals = _state.portals ?? new List<DimensionPortalRecord>();
      DimensionPortalRecord record = ToRecord(portal);
      int index = FindPortalRecordIndex(record.portalId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.portals[index], record))
        {
          return;
        }

        _state.portals[index] = record;
      }
      else
      {
        _state.portals.Add(record);
      }

      Touch();
    }

    public static void RemovePortal(string portalId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.portals == null)
      {
        return;
      }

      int index = FindPortalRecordIndex(portalId);
      if (index < 0)
      {
        return;
      }

      _state.portals.RemoveAt(index);
      Touch();
    }

    public static void UpsertMarker(DimensionMapMarker marker)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.markers = _state.markers ?? new List<DimensionMarkerRecord>();
      DimensionMarkerRecord record = ToRecord(marker);
      int index = FindMarkerRecordIndex(record.markerId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.markers[index], record))
        {
          return;
        }

        _state.markers[index] = record;
      }
      else
      {
        _state.markers.Add(record);
      }

      if (IsNonOverworld(record.dimensionId))
      {
        _hasNonOverworldFootprint = true;
        Touch(false);
      }
      else
      {
        Touch(true);
      }
    }

    public static void RemoveMarker(string markerId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.markers == null)
      {
        return;
      }

      int index = FindMarkerRecordIndex(markerId);
      if (index < 0)
      {
        return;
      }

      _state.markers.RemoveAt(index);
      Touch();
    }

    public static void UpsertAnchor(DimensionAnchorDefinition anchor)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.anchors = _state.anchors ?? new List<DimensionAnchorRecord>();
      DimensionAnchorRecord record = ToRecord(anchor);
      int index = FindAnchorRecordIndex(record.anchorId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.anchors[index], record))
        {
          return;
        }

        _state.anchors[index] = record;
      }
      else
      {
        _state.anchors.Add(record);
      }

      Touch();
    }

    public static void RemoveAnchor(string anchorId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.anchors == null)
      {
        return;
      }

      int index = FindAnchorRecordIndex(anchorId);
      if (index < 0)
      {
        return;
      }

      _state.anchors.RemoveAt(index);
      Touch();
    }

    public static void UpsertScene(DimensionSceneDefinition scene)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.scenes = _state.scenes ?? new List<DimensionSceneRecord>();
      DimensionSceneRecord record = ToRecord(scene);
      int index = FindSceneRecordIndex(record.sceneId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.scenes[index], record))
        {
          return;
        }

        _state.scenes[index] = record;
      }
      else
      {
        _state.scenes.Add(record);
      }

      Touch();
    }

    public static void RemoveScene(string sceneId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.scenes == null)
      {
        return;
      }

      int index = FindSceneRecordIndex(sceneId);
      if (index < 0)
      {
        return;
      }

      _state.scenes.RemoveAt(index);
      Touch();
    }

    public static void UpsertProgressFlag(DimensionProgressFlag flag)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.progressFlags = _state.progressFlags ?? new List<DimensionProgressFlagRecord>();
      DimensionProgressFlagRecord record = ToRecord(flag);
      int index = FindProgressFlagRecordIndex(record.flagId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.progressFlags[index], record))
        {
          return;
        }

        _state.progressFlags[index] = record;
      }
      else
      {
        _state.progressFlags.Add(record);
      }

      Touch();
    }

    public static void UpsertBossDefeat(string bossObjectName, long defeatedUtcTicks)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || string.IsNullOrEmpty(bossObjectName))
      {
        return;
      }

      _state.bossDefeats = _state.bossDefeats ?? new List<DimensionBossDefeatRecord>();
      int index = FindBossDefeatRecordIndex(bossObjectName);
      if (index >= 0)
      {
        if (_state.bossDefeats[index].defeatedUtcTicks == defeatedUtcTicks)
        {
          return;
        }

        _state.bossDefeats[index].defeatedUtcTicks = defeatedUtcTicks;
      }
      else
      {
        _state.bossDefeats.Add(new DimensionBossDefeatRecord
        {
          bossObjectName = bossObjectName,
          defeatedUtcTicks = defeatedUtcTicks
        });
      }

      Touch();
    }

    public static void RemoveBossDefeat(string bossObjectName)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.bossDefeats == null)
      {
        return;
      }

      int index = FindBossDefeatRecordIndex(bossObjectName);
      if (index < 0)
      {
        return;
      }

      _state.bossDefeats.RemoveAt(index);
      Touch();
    }

    public static void RemoveProgressFlag(string flagId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.progressFlags == null)
      {
        return;
      }

      int index = FindProgressFlagRecordIndex(flagId);
      if (index < 0)
      {
        return;
      }

      _state.progressFlags.RemoveAt(index);
      Touch();
    }

    public static void UpsertGeneratedArea(DimensionGenerationStatus status)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.generatedAreas = _state.generatedAreas ?? new List<DimensionGeneratedAreaRecord>();
      DimensionGeneratedAreaRecord record = ToRecord(status);
      int index = FindGeneratedAreaRecordIndex(
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)));
      if (index >= 0)
      {
        if (RecordsEqual(_state.generatedAreas[index], record))
        {
          return;
        }

        _state.generatedAreas[index] = record;
      }
      else
      {
        _state.generatedAreas.Add(record);
      }

      Touch();
    }

    public static void RemoveGeneratedArea(string dimensionId, DimensionBounds localBounds)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.generatedAreas == null)
      {
        return;
      }

      int index = FindGeneratedAreaRecordIndex(dimensionId, localBounds);
      if (index < 0)
      {
        return;
      }

      _state.generatedAreas.RemoveAt(index);
      Touch();
    }

    public static void UpsertContentOwnership(DimensionContentOwnershipBinding binding)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.contentOwnership =
          _state.contentOwnership ?? new List<DimensionContentOwnershipRecord>();
      DimensionContentOwnershipRecord record = ToRecord(binding);
      int index = FindContentOwnershipRecordIndex(record.recordKind, record.recordId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.contentOwnership[index], record))
        {
          return;
        }

        _state.contentOwnership[index] = record;
      }
      else
      {
        _state.contentOwnership.Add(record);
      }

      Touch();
    }

    public static void RemoveContentOwnership(DimensionContentRecordKind recordKind, string recordId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.contentOwnership == null)
      {
        return;
      }

      int index = FindContentOwnershipRecordIndex((int)recordKind, recordId);
      if (index < 0)
      {
        return;
      }

      _state.contentOwnership.RemoveAt(index);
      Touch();
    }

    private static bool RecordsEqual(DimensionDefinitionRecord a, DimensionDefinitionRecord b)
    {
      return a != null &&
             b != null &&
             a.dimensionId == b.dimensionId &&
             a.displayName == b.displayName &&
             a.absoluteOriginX == b.absoluteOriginX &&
             a.absoluteOriginY == b.absoluteOriginY &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.generationVersion == b.generationVersion &&
             a.spaceKind == b.spaceKind &&
             a.capabilities == b.capabilities &&
             a.lifecycleState == b.lifecycleState &&
             a.layoutVersion == b.layoutVersion &&
             string.Equals(a.layoutFingerprint, b.layoutFingerprint, StringComparison.Ordinal) &&
             a.builtIn == b.builtIn;
    }

    private static bool RecordsEqual(
        DimensionSlotPersistenceRecord a,
        DimensionSlotPersistenceRecord b)
    {
      return a != null &&
             b != null &&
             a.dimensionId == b.dimensionId &&
             a.absoluteOriginX == b.absoluteOriginX &&
             a.absoluteOriginY == b.absoluteOriginY &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.candidateIndex == b.candidateIndex &&
             a.usedFixedOrigin == b.usedFixedOrigin &&
             a.assignedUtcTicks == b.assignedUtcTicks &&
             a.allocationCode == b.allocationCode &&
             a.allocationMessage == b.allocationMessage;
    }

    private static bool RecordsEqual(DimensionPortalRecord a, DimensionPortalRecord b)
    {
      return a != null &&
             b != null &&
             a.portalId == b.portalId &&
             a.displayName == b.displayName &&
             a.fromDimensionId == b.fromDimensionId &&
             a.fromLocalX == b.fromLocalX &&
             a.fromLocalY == b.fromLocalY &&
             a.toDimensionId == b.toDimensionId &&
             a.toLocalX == b.toLocalX &&
             a.toLocalY == b.toLocalY &&
             a.state == b.state;
    }

    private static bool RecordsEqual(DimensionMarkerRecord a, DimensionMarkerRecord b)
    {
      return a != null &&
             b != null &&
             a.markerId == b.markerId &&
             a.dimensionId == b.dimensionId &&
             a.localX == b.localX &&
             a.localY == b.localY &&
             a.label == b.label &&
             a.kind == b.kind &&
             a.visible == b.visible;
    }

    private static bool RecordsEqual(DimensionAnchorRecord a, DimensionAnchorRecord b)
    {
      return a != null &&
             b != null &&
             a.anchorId == b.anchorId &&
             a.displayName == b.displayName &&
             a.dimensionId == b.dimensionId &&
             a.localX == b.localX &&
             a.localY == b.localY &&
             a.kind == b.kind &&
             a.priority == b.priority &&
             a.enabled == b.enabled;
    }

    private static bool RecordsEqual(DimensionSceneRecord a, DimensionSceneRecord b)
    {
      return a != null &&
             b != null &&
             a.sceneId == b.sceneId &&
             a.displayName == b.displayName &&
             a.dimensionId == b.dimensionId &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.kind == b.kind &&
             a.priority == b.priority &&
             a.state == b.state;
    }

    private static bool RecordsEqual(DimensionProgressFlagRecord a, DimensionProgressFlagRecord b)
    {
      return a != null &&
             b != null &&
             a.flagId == b.flagId &&
             a.dimensionId == b.dimensionId &&
             a.category == b.category &&
             a.value == b.value &&
             a.updatedUtcTicks == b.updatedUtcTicks;
    }

    private static bool RecordsEqual(DimensionGeneratedAreaRecord a, DimensionGeneratedAreaRecord b)
    {
      return a != null &&
             b != null &&
             a.dimensionId == b.dimensionId &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.state == b.state &&
             a.progress01 == b.progress01 &&
             a.message == b.message;
    }

    private static bool RecordsEqual(DimensionContentOwnershipRecord a, DimensionContentOwnershipRecord b)
    {
      return a != null &&
             b != null &&
             a.contentPackId == b.contentPackId &&
             a.recordKind == b.recordKind &&
             a.recordId == b.recordId &&
             a.displayName == b.displayName &&
             a.notes == b.notes;
    }

    public static void RemoveDimension(string dimensionId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensions == null)
      {
        return;
      }

      int index = FindDimensionRecordIndex(dimensionId);
      if (index < 0)
      {
        return;
      }

      _state.dimensions.RemoveAt(index);
      if (_state.dimensionSlots != null)
      {
        int slotIndex = FindDimensionSlotRecordIndex(dimensionId);
        if (slotIndex >= 0)
        {
          _state.dimensionSlots.RemoveAt(slotIndex);
        }
      }

      Touch();
    }
  }
}
