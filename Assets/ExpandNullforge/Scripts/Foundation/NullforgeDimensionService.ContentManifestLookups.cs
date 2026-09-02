using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Answering what already exists, counting what the manifest itself would add.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    private static bool TryAddManifestId(Dictionary<string, bool> ids, string id)
    {
      if (string.IsNullOrEmpty(id))
      {
        return true;
      }

      if (ids.ContainsKey(id))
      {
        return false;
      }

      ids[id] = true;
      return true;
    }

    private bool ContentPackKnownForManifest(
        string contentPackId,
        Dictionary<string, bool> manifestContentPackIds)
    {
      if (string.IsNullOrEmpty(contentPackId))
      {
        return false;
      }

      return contentPacks.ContainsKey(contentPackId) ||
             manifestContentPackIds.ContainsKey(contentPackId);
    }

    private bool DimensionKnownForManifest(
        string dimensionId,
        Dictionary<string, bool> manifestDimensionIds)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        return false;
      }

      return definitions.ContainsKey(dimensionId) ||
             manifestDimensionIds.ContainsKey(dimensionId);
    }

    private bool TryGetManifestAwareDimension(
        string dimensionId,
        Dictionary<string, bool> manifestDimensionIds,
        List<DimensionDefinition> manifestDimensions,
        out DimensionDefinition dimension)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        dimension = default(DimensionDefinition);
        return false;
      }

      if (definitions.TryGetValue(dimensionId, out dimension))
      {
        return true;
      }

      if (!manifestDimensionIds.ContainsKey(dimensionId))
      {
        dimension = default(DimensionDefinition);
        return false;
      }

      for (int i = 0; i < manifestDimensions.Count; i++)
      {
        if (string.Equals(manifestDimensions[i].Id, dimensionId, StringComparison.Ordinal))
        {
          dimension = manifestDimensions[i];
          return true;
        }
      }

      dimension = default(DimensionDefinition);
      return false;
    }

    private bool ZoneKnownForManifest(
        string zoneId,
        Dictionary<string, bool> manifestZoneIds)
    {
      if (string.IsNullOrEmpty(zoneId))
      {
        return false;
      }

      return zoneDefinitions.ContainsKey(zoneId) ||
             manifestZoneIds.ContainsKey(zoneId);
    }

    private bool TryGetManifestAwareZone(
        string zoneId,
        Dictionary<string, bool> manifestZoneIds,
        List<DimensionZoneDefinition> manifestZones,
        out DimensionZoneDefinition zone)
    {
      if (string.IsNullOrEmpty(zoneId))
      {
        zone = default(DimensionZoneDefinition);
        return false;
      }

      if (zoneDefinitions.TryGetValue(zoneId, out zone))
      {
        return true;
      }

      if (!manifestZoneIds.ContainsKey(zoneId))
      {
        zone = default(DimensionZoneDefinition);
        return false;
      }

      for (int i = 0; i < manifestZones.Count; i++)
      {
        if (string.Equals(manifestZones[i].ZoneId, zoneId, StringComparison.Ordinal))
        {
          zone = manifestZones[i];
          return true;
        }
      }

      zone = default(DimensionZoneDefinition);
      return false;
    }

    private bool PortalKnownForManifest(
        string portalId,
        Dictionary<string, bool> manifestPortalIds)
    {
      if (string.IsNullOrEmpty(portalId))
      {
        return false;
      }

      return portals.ContainsKey(portalId) ||
             manifestPortalIds.ContainsKey(portalId);
    }

    private bool TryGetManifestAwarePortal(
        string portalId,
        Dictionary<string, bool> manifestPortalIds,
        List<DimensionPortalDefinition> manifestPortals,
        out DimensionPortalDefinition portal)
    {
      if (string.IsNullOrEmpty(portalId))
      {
        portal = default(DimensionPortalDefinition);
        return false;
      }

      if (portals.TryGetValue(portalId, out portal))
      {
        return true;
      }

      if (!manifestPortalIds.ContainsKey(portalId))
      {
        portal = default(DimensionPortalDefinition);
        return false;
      }

      for (int i = 0; i < manifestPortals.Count; i++)
      {
        if (string.Equals(manifestPortals[i].PortalId, portalId, StringComparison.Ordinal))
        {
          portal = manifestPortals[i];
          return true;
        }
      }

      portal = default(DimensionPortalDefinition);
      return false;
    }

    private bool TryGetManifestAwareScene(
        string sceneId,
        Dictionary<string, bool> manifestSceneIds,
        List<DimensionSceneDefinition> manifestScenes,
        out DimensionSceneDefinition scene)
    {
      if (string.IsNullOrEmpty(sceneId))
      {
        scene = default(DimensionSceneDefinition);
        return false;
      }

      if (scenes.TryGetValue(sceneId, out scene))
      {
        return true;
      }

      if (!manifestSceneIds.ContainsKey(sceneId))
      {
        scene = default(DimensionSceneDefinition);
        return false;
      }

      for (int i = 0; i < manifestScenes.Count; i++)
      {
        if (string.Equals(manifestScenes[i].SceneId, sceneId, StringComparison.Ordinal))
        {
          scene = manifestScenes[i];
          return true;
        }
      }

      scene = default(DimensionSceneDefinition);
      return false;
    }

    private bool TryGetManifestAwareProgressFlag(
        string flagId,
        Dictionary<string, bool> manifestFlagIds,
        List<DimensionProgressFlag> manifestFlags,
        out DimensionProgressFlag flag)
    {
      if (string.IsNullOrEmpty(flagId))
      {
        flag = default(DimensionProgressFlag);
        return false;
      }

      if (progressFlags.TryGetValue(flagId, out flag))
      {
        return true;
      }

      if (!manifestFlagIds.ContainsKey(flagId))
      {
        flag = default(DimensionProgressFlag);
        return false;
      }

      for (int i = 0; i < manifestFlags.Count; i++)
      {
        if (string.Equals(manifestFlags[i].FlagId, flagId, StringComparison.Ordinal))
        {
          flag = manifestFlags[i];
          return true;
        }
      }

      flag = default(DimensionProgressFlag);
      return false;
    }

    private bool TryGetManifestAwareMarker(
        string markerId,
        Dictionary<string, bool> manifestMarkerIds,
        List<DimensionMapMarker> manifestMarkers,
        out DimensionMapMarker marker)
    {
      if (string.IsNullOrEmpty(markerId))
      {
        marker = default(DimensionMapMarker);
        return false;
      }

      if (markers.TryGetValue(markerId, out marker))
      {
        return true;
      }

      if (!manifestMarkerIds.ContainsKey(markerId))
      {
        marker = default(DimensionMapMarker);
        return false;
      }

      for (int i = 0; i < manifestMarkers.Count; i++)
      {
        if (string.Equals(manifestMarkers[i].MarkerId, markerId, StringComparison.Ordinal))
        {
          marker = manifestMarkers[i];
          return true;
        }
      }

      marker = default(DimensionMapMarker);
      return false;
    }

    private bool ContentRecordKnownForManifest(
        DimensionContentRecordKind recordKind,
        string recordId,
        ManifestValidationContext manifestContext)
    {
      if (ContentRecordExists(recordKind, recordId))
      {
        return true;
      }

      switch (recordKind)
      {
        case DimensionContentRecordKind.Dimension:
          return manifestContext.DimensionIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Portal:
          return manifestContext.PortalIds.ContainsKey(recordId);
        case DimensionContentRecordKind.PortalPresentation:
          return manifestContext.PortalPresentationIds.ContainsKey(recordId);
        case DimensionContentRecordKind.TravelRequirement:
          return manifestContext.TravelRequirementIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Starter:
          return manifestContext.StarterIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Scene:
          return manifestContext.SceneIds.ContainsKey(recordId);
        case DimensionContentRecordKind.SceneTemplate:
          return manifestContext.SceneTemplateIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Encounter:
          return manifestContext.EncounterIds.ContainsKey(recordId);
        case DimensionContentRecordKind.ProgressFlag:
          return manifestContext.ProgressFlagIds.ContainsKey(recordId);
        case DimensionContentRecordKind.WorldEvent:
          return manifestContext.WorldEventIds.ContainsKey(recordId);
        case DimensionContentRecordKind.GenerationPass:
          return manifestContext.GenerationPassIds.ContainsKey(recordId);
        case DimensionContentRecordKind.ZoneDefinition:
          return manifestContext.ZoneIds.ContainsKey(recordId);
        case DimensionContentRecordKind.MapLayer:
          return manifestContext.MapLayerIds.ContainsKey(recordId);
        case DimensionContentRecordKind.MapMarker:
          return manifestContext.MapMarkerIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Anchor:
          return manifestContext.AnchorIds.ContainsKey(recordId);
        case DimensionContentRecordKind.AssetReference:
          return manifestContext.AssetReferenceIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Biome:
          return manifestContext.BiomeIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Item:
        case DimensionContentRecordKind.Recipe:
        case DimensionContentRecordKind.Workbench:
        case DimensionContentRecordKind.LootTable:
        case DimensionContentRecordKind.Animal:
        case DimensionContentRecordKind.Critter:
        case DimensionContentRecordKind.Mob:
        case DimensionContentRecordKind.Boss:
        case DimensionContentRecordKind.SceneProp:
        case DimensionContentRecordKind.SceneLootContainer:
        case DimensionContentRecordKind.SceneSpawnPoint:
        case DimensionContentRecordKind.SceneTrigger:
          return manifestContext.OwnershipKeys.ContainsKey(
              BuildContentOwnershipKey(recordKind, recordId));
        case DimensionContentRecordKind.Custom:
          return true;
      }

      return false;
    }

    private static void AddManifestOperation(
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        DimensionContentManifestOperationKind operationKind,
        DimensionContentRecordKind recordKind,
        string recordId,
        bool applied,
        DimensionOperationResult result)
    {
      if (!result.Success)
      {
        errorCount++;
      }

      operations.Add(
          new DimensionContentManifestOperation(
              operationKind,
              recordKind,
              recordId,
              result.Success,
              applied && result.Success,
              result.Code,
              result.Message));
    }
  }
}
