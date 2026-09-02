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
  /// Turning a live definition into a row, and a row back into one.
  /// </summary>
  public static partial class DimensionWorldRegistry
  {
    private static DimensionDefinitionRecord ToRecord(DimensionDefinition definition, bool builtIn)
    {
      return new DimensionDefinitionRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        dimensionId = SanitizeName(definition.Id, 128, string.Empty),
        displayName = SanitizeName(definition.DisplayName, 128, definition.Id),
        absoluteOriginX = definition.AbsoluteOrigin.x,
        absoluteOriginY = definition.AbsoluteOrigin.y,
        localMinX = definition.LocalBounds.Min.x,
        localMinY = definition.LocalBounds.Min.y,
        localMaxExclusiveX = definition.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = definition.LocalBounds.MaxExclusive.y,
        generationVersion = Math.Max(1, definition.GenerationVersion),
        // The JSON field keeps its old name so every existing registry parses unchanged.
        spaceKind = (int)definition.Type,
        capabilities = (int)definition.Capabilities,
        lifecycleState = (int)definition.LifecycleState,
        builtIn = builtIn
      };
    }

    private static DimensionPortalRecord ToRecord(DimensionPortalDefinition portal)
    {
      return new DimensionPortalRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        portalId = SanitizeName(portal.PortalId, 128, string.Empty),
        displayName = SanitizeName(portal.DisplayName, 128, portal.PortalId),
        fromDimensionId = SanitizeName(portal.FromDimensionId, 128, string.Empty),
        fromLocalX = portal.FromLocalPosition.x,
        fromLocalY = portal.FromLocalPosition.y,
        toDimensionId = SanitizeName(portal.ToDimensionId, 128, string.Empty),
        toLocalX = portal.ToLocalPosition.x,
        toLocalY = portal.ToLocalPosition.y,
        state = (int)portal.State
      };
    }

    private static DimensionSlotPersistenceRecord ToRecord(DimensionSlotRecord slot)
    {
      return new DimensionSlotPersistenceRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        dimensionId = SanitizeName(slot.DimensionId, 128, string.Empty),
        absoluteOriginX = slot.AbsoluteOrigin.x,
        absoluteOriginY = slot.AbsoluteOrigin.y,
        localMinX = slot.LocalBounds.Min.x,
        localMinY = slot.LocalBounds.Min.y,
        localMaxExclusiveX = slot.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = slot.LocalBounds.MaxExclusive.y,
        candidateIndex = slot.CandidateIndex,
        usedFixedOrigin = slot.UsedFixedOrigin,
        assignedUtcTicks = slot.AssignedUtcTicks,
        allocationCode = SanitizeName(slot.AllocationCode, 64, string.Empty),
        allocationMessage = SanitizeName(slot.AllocationMessage, 256, string.Empty)
      };
    }

    private static DimensionMarkerRecord ToRecord(DimensionMapMarker marker)
    {
      return new DimensionMarkerRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        markerId = SanitizeName(marker.MarkerId, 128, string.Empty),
        dimensionId = SanitizeName(marker.DimensionId, 128, string.Empty),
        localX = marker.LocalPosition.x,
        localY = marker.LocalPosition.y,
        label = SanitizeName(marker.Label, 128, marker.MarkerId),
        kind = SanitizeName(marker.Kind, 64, string.Empty),
        visible = marker.Visible
      };
    }

    private static DimensionAnchorRecord ToRecord(DimensionAnchorDefinition anchor)
    {
      return new DimensionAnchorRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        anchorId = SanitizeName(anchor.AnchorId, 128, string.Empty),
        displayName = SanitizeName(anchor.DisplayName, 128, anchor.AnchorId),
        dimensionId = SanitizeName(anchor.DimensionId, 128, string.Empty),
        localX = anchor.LocalPosition.x,
        localY = anchor.LocalPosition.y,
        kind = (int)anchor.Kind,
        priority = anchor.Priority,
        enabled = anchor.Enabled
      };
    }

    private static DimensionSceneRecord ToRecord(DimensionSceneDefinition scene)
    {
      return new DimensionSceneRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        sceneId = SanitizeName(scene.SceneId, 128, string.Empty),
        displayName = SanitizeName(scene.DisplayName, 128, scene.SceneId),
        dimensionId = SanitizeName(scene.DimensionId, 128, string.Empty),
        localMinX = scene.LocalBounds.Min.x,
        localMinY = scene.LocalBounds.Min.y,
        localMaxExclusiveX = scene.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = scene.LocalBounds.MaxExclusive.y,
        kind = SanitizeName(scene.Kind, 64, string.Empty),
        priority = scene.Priority,
        state = (int)scene.State
      };
    }

    private static DimensionProgressFlagRecord ToRecord(DimensionProgressFlag flag)
    {
      return new DimensionProgressFlagRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        flagId = SanitizeName(flag.FlagId, 128, string.Empty),
        dimensionId = SanitizeName(flag.DimensionId, 128, string.Empty),
        category = SanitizeName(flag.Category, 64, string.Empty),
        value = flag.Value,
        updatedUtcTicks = flag.UpdatedUtcTicks
      };
    }

    private static DimensionDefinition ToDefinition(DimensionDefinitionRecord record)
    {
      return new DimensionDefinition(
          record.dimensionId,
          record.displayName,
          new int2(record.absoluteOriginX, record.absoluteOriginY),
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          Math.Max(1, record.generationVersion),
          DimensionTypeMigration.Normalize(record.spaceKind),
          (DimensionCapabilityFlags)record.capabilities,
          (DimensionLifecycleState)record.lifecycleState);
    }

    private static DimensionPortalDefinition ToPortal(DimensionPortalRecord record)
    {
      return new DimensionPortalDefinition(
          record.portalId,
          record.displayName,
          record.fromDimensionId,
          new float2(record.fromLocalX, record.fromLocalY),
          record.toDimensionId,
          new float2(record.toLocalX, record.toLocalY),
          (DimensionPortalState)record.state);
    }

    private static DimensionSlotRecord ToSlotRecord(DimensionSlotPersistenceRecord record)
    {
      if (record == null)
      {
        return default(DimensionSlotRecord);
      }

      return new DimensionSlotRecord(
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          new int2(record.absoluteOriginX, record.absoluteOriginY),
          record.candidateIndex,
          record.usedFixedOrigin,
          record.assignedUtcTicks,
          record.allocationCode,
          record.allocationMessage);
    }

    private static DimensionPlayerVisitRecord ToPlayerVisit(DimensionPlayerVisitStateRecord record)
    {
      if (record == null)
      {
        return default(DimensionPlayerVisitRecord);
      }

      return new DimensionPlayerVisitRecord(
          record.playerId,
          record.dimensionId,
          new float2(record.localX, record.localY),
          new float2(record.absoluteX, record.absoluteY),
          record.savedUtcTicks);
    }

    private static int ComparePlayerVisits(
        DimensionPlayerVisitRecord left,
        DimensionPlayerVisitRecord right)
    {
      int saved = right.SavedUtcTicks.CompareTo(left.SavedUtcTicks);
      if (saved != 0)
      {
        return saved;
      }

      int player = string.Compare(left.PlayerId, right.PlayerId, StringComparison.Ordinal);
      if (player != 0)
      {
        return player;
      }

      return string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
    }

    private static DimensionMapMarker ToMarker(DimensionMarkerRecord record)
    {
      return new DimensionMapMarker(
          record.markerId,
          record.dimensionId,
          new float2(record.localX, record.localY),
          record.label,
          record.kind,
          record.visible);
    }

    private static DimensionAnchorDefinition ToAnchor(DimensionAnchorRecord record)
    {
      return new DimensionAnchorDefinition(
          record.anchorId,
          record.displayName,
          record.dimensionId,
          new float2(record.localX, record.localY),
          (DimensionAnchorKind)record.kind,
          record.priority,
          record.enabled);
    }

    private static DimensionSceneDefinition ToScene(DimensionSceneRecord record)
    {
      return new DimensionSceneDefinition(
          record.sceneId,
          record.displayName,
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          record.kind,
          record.priority,
          (DimensionSceneState)record.state);
    }

    private static DimensionProgressFlag ToProgressFlag(DimensionProgressFlagRecord record)
    {
      return new DimensionProgressFlag(
          record.flagId,
          record.dimensionId,
          record.category,
          record.value,
          record.updatedUtcTicks);
    }

    private static DimensionGeneratedAreaRecord ToRecord(DimensionGenerationStatus status)
    {
      return new DimensionGeneratedAreaRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        dimensionId = SanitizeName(status.DimensionId, 128, string.Empty),
        localMinX = status.LocalBounds.Min.x,
        localMinY = status.LocalBounds.Min.y,
        localMaxExclusiveX = status.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = status.LocalBounds.MaxExclusive.y,
        state = (int)status.State,
        progress01 = Mathf.Clamp01(status.Progress01),
        message = SanitizeName(status.Message, 256, string.Empty),
        updatedUtcTicks = DateTime.UtcNow.Ticks
      };
    }

    private static DimensionContentOwnershipRecord ToRecord(DimensionContentOwnershipBinding binding)
    {
      return new DimensionContentOwnershipRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        contentPackId = SanitizeName(binding.ContentPackId, 128, string.Empty),
        recordKind = (int)binding.RecordKind,
        recordId = SanitizeName(binding.RecordId, 128, string.Empty),
        displayName = SanitizeName(binding.DisplayName, 128, string.Empty),
        notes = SanitizeName(binding.Notes, 256, string.Empty)
      };
    }

    private static DimensionGenerationStatus ToGenerationStatus(DimensionGeneratedAreaRecord record)
    {
      return new DimensionGenerationStatus(
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          (DimensionGenerationState)record.state,
          Mathf.Clamp01(record.progress01),
          record.message);
    }

    private static DimensionContentOwnershipBinding ToContentOwnership(DimensionContentOwnershipRecord record)
    {
      return new DimensionContentOwnershipBinding(
          record.contentPackId,
          (DimensionContentRecordKind)record.recordKind,
          record.recordId,
          record.displayName,
          record.notes);
    }
  }
}
