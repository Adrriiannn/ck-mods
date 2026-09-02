using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// What applying a manifest would do, worked out before anything is written.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    private DimensionContentManifestResult BuildContentManifestPreflightResult(
        DimensionContentManifestRequest request)
    {
      List<DimensionContentManifestOperation> operations =
          new List<DimensionContentManifestOperation>();
      int errorCount = 0;

      Dictionary<string, bool> contentPackIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> dimensionIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> zoneIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> mapLayerIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> mapMarkerIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> anchorIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> portalIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> portalPresentationIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> travelRequirementIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> starterIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> sceneTemplateIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> sceneIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> encounterIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> generationPassIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> progressFlagIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> worldEventIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> assetReferenceIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> biomeIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> ownershipKeys =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      ManifestValidationContext manifestContext =
          new ManifestValidationContext
          {
            ContentPackIds = contentPackIds,
            DimensionIds = dimensionIds,
            ZoneIds = zoneIds,
            MapLayerIds = mapLayerIds,
            MapMarkerIds = mapMarkerIds,
            AnchorIds = anchorIds,
            PortalIds = portalIds,
            PortalPresentationIds = portalPresentationIds,
            TravelRequirementIds = travelRequirementIds,
            StarterIds = starterIds,
            SceneTemplateIds = sceneTemplateIds,
            SceneIds = sceneIds,
            EncounterIds = encounterIds,
            ProgressFlagIds = progressFlagIds,
            WorldEventIds = worldEventIds,
            GenerationPassIds = generationPassIds,
            AssetReferenceIds = assetReferenceIds,
            BiomeIds = biomeIds,
            OwnershipKeys = ownershipKeys
          };
      List<DimensionDefinition> manifestDimensions =
          new List<DimensionDefinition>();
      List<DimensionZoneDefinition> manifestZones =
          new List<DimensionZoneDefinition>();
      List<DimensionMapMarker> acceptedManifestMarkers =
          new List<DimensionMapMarker>();
      List<DimensionPortalDefinition> acceptedManifestPortals =
          new List<DimensionPortalDefinition>();
      List<DimensionSceneDefinition> acceptedManifestScenes =
          new List<DimensionSceneDefinition>();
      List<DimensionGenerationPassDefinition> acceptedManifestGenerationPasses =
          new List<DimensionGenerationPassDefinition>();
      List<DimensionProgressFlag> acceptedManifestProgressFlags =
          new List<DimensionProgressFlag>();

      PreflightContentPacks(request, operations, ref errorCount, contentPackIds);
      PreflightDimensions(request, operations, ref errorCount, dimensionIds, manifestDimensions);
      PreflightZones(request, operations, ref errorCount, dimensionIds, zoneIds, manifestDimensions, manifestZones);
      PreflightMapLayers(request, operations, ref errorCount, dimensionIds, mapLayerIds, manifestDimensions);
      PreflightMapMarkers(request, operations, ref errorCount, dimensionIds, mapMarkerIds, manifestDimensions, acceptedManifestMarkers);
      PreflightAnchors(request, operations, ref errorCount, dimensionIds, anchorIds, manifestDimensions);
      PreflightPortals(request, operations, ref errorCount, dimensionIds, portalIds, manifestDimensions, acceptedManifestPortals);
      PreflightPortalPresentations(request, operations, ref errorCount, portalIds, portalPresentationIds);
      PreflightProgressFlags(request, operations, ref errorCount, dimensionIds, progressFlagIds, manifestDimensions, acceptedManifestProgressFlags);
      PreflightTravelRequirements(request, operations, ref errorCount, dimensionIds, portalIds, travelRequirementIds, progressFlagIds, manifestDimensions, acceptedManifestPortals, acceptedManifestProgressFlags);
      PreflightSceneTemplates(request, operations, ref errorCount, dimensionIds, zoneIds, sceneTemplateIds, manifestDimensions, manifestZones);
      PreflightScenes(request, operations, ref errorCount, dimensionIds, sceneIds, manifestDimensions, acceptedManifestScenes);
      PreflightEncounters(request, operations, ref errorCount, dimensionIds, zoneIds, mapMarkerIds, sceneIds, encounterIds, progressFlagIds, manifestDimensions, manifestZones, acceptedManifestMarkers, acceptedManifestScenes, acceptedManifestProgressFlags);
      PreflightGenerationPasses(request, operations, ref errorCount, dimensionIds, zoneIds, generationPassIds, manifestDimensions, manifestZones, acceptedManifestGenerationPasses);
      PreflightWorldEvents(request, operations, ref errorCount, dimensionIds, zoneIds, progressFlagIds, worldEventIds, manifestDimensions, manifestZones, acceptedManifestProgressFlags);
      PreflightAssetReferences(request, operations, ref errorCount, contentPackIds, dimensionIds, zoneIds, assetReferenceIds);
      PreflightBiomes(request, operations, ref errorCount, dimensionIds, biomeIds);
      PreflightStarters(request, operations, ref errorCount, manifestContext);
      PreflightOwnershipBindings(request, operations, ref errorCount, manifestContext);

      return new DimensionContentManifestResult(
          errorCount == 0,
          false,
          operations.Count,
          errorCount,
          operations);
    }

    /// <summary>
    /// Checks the content packs a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightContentPacks(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> contentPackIds)
    {
      IReadOnlyList<DimensionContentPackDefinition> manifestContentPacks =
          request.Manifest.ContentPacks ?? new List<DimensionContentPackDefinition>();
      for (int i = 0; i < manifestContentPacks.Count; i++)
      {
        DimensionContentPackDefinition contentPack = manifestContentPacks[i];
        DimensionOperationResult result =
            ValidateManifestContentPack(contentPack, request.UpdateExisting, contentPackIds);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Custom,
            contentPack.ContentPackId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the dimensions a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightDimensions(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        List<DimensionDefinition> manifestDimensions)
    {
      IReadOnlyList<DimensionDefinition> manifestDimensionDefinitions =
          request.Manifest.Dimensions ?? new List<DimensionDefinition>();
      for (int i = 0; i < manifestDimensionDefinitions.Count; i++)
      {
        DimensionDefinition dimension = manifestDimensionDefinitions[i];
        DimensionOperationResult result =
            ValidateManifestDimension(
                dimension,
                request.UpdateExisting,
                dimensionIds,
                manifestDimensions);
        if (result.Success)
        {
          manifestDimensions.Add(dimension);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Dimension,
            dimension.Id,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the zones a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightZones(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> zoneIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones)
    {
      IReadOnlyList<DimensionZoneDefinition> manifestZoneDefinitions =
          request.Manifest.Zones ?? new List<DimensionZoneDefinition>();
      for (int i = 0; i < manifestZoneDefinitions.Count; i++)
      {
        DimensionZoneDefinition zone = manifestZoneDefinitions[i];
        DimensionOperationResult result =
            ValidateManifestZoneDefinition(
                zone,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                manifestDimensions);
        if (result.Success)
        {
          manifestZones.Add(zone);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.ZoneDefinition,
            zone.ZoneId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the map layers a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightMapLayers(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> mapLayerIds,
        List<DimensionDefinition> manifestDimensions)
    {
      IReadOnlyList<DimensionMapLayerDefinition> manifestMapLayers =
          request.Manifest.MapLayers ?? new List<DimensionMapLayerDefinition>();
      for (int i = 0; i < manifestMapLayers.Count; i++)
      {
        DimensionMapLayerDefinition layer = manifestMapLayers[i];
        DimensionOperationResult result =
            ValidateManifestMapLayer(
                layer,
                request.UpdateExisting,
                dimensionIds,
                mapLayerIds,
                manifestDimensions);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.MapLayer,
            layer.LayerId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the map markers a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightMapMarkers(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> mapMarkerIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionMapMarker> acceptedManifestMarkers)
    {
      IReadOnlyList<DimensionMapMarker> manifestMapMarkers =
          request.Manifest.MapMarkers ?? new List<DimensionMapMarker>();
      for (int i = 0; i < manifestMapMarkers.Count; i++)
      {
        DimensionMapMarker marker = manifestMapMarkers[i];
        DimensionOperationResult result =
            ValidateManifestMapMarker(
                marker,
                request.UpdateExisting,
                dimensionIds,
                mapMarkerIds,
                manifestDimensions);
        if (result.Success)
        {
          acceptedManifestMarkers.Add(marker);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.MapMarker,
            marker.MarkerId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the anchors a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightAnchors(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> anchorIds,
        List<DimensionDefinition> manifestDimensions)
    {
      IReadOnlyList<DimensionAnchorDefinition> manifestAnchors =
          request.Manifest.Anchors ?? new List<DimensionAnchorDefinition>();
      for (int i = 0; i < manifestAnchors.Count; i++)
      {
        DimensionAnchorDefinition anchor = manifestAnchors[i];
        DimensionOperationResult result =
            ValidateManifestAnchor(
                anchor,
                request.UpdateExisting,
                dimensionIds,
                anchorIds,
                manifestDimensions);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Anchor,
            anchor.AnchorId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the portals a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightPortals(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> portalIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionPortalDefinition> acceptedManifestPortals)
    {
      IReadOnlyList<DimensionPortalDefinition> manifestPortals =
          request.Manifest.Portals ?? new List<DimensionPortalDefinition>();
      for (int i = 0; i < manifestPortals.Count; i++)
      {
        DimensionPortalDefinition portal = manifestPortals[i];
        DimensionOperationResult result =
            ValidateManifestPortal(
                portal,
                request.UpdateExisting,
                dimensionIds,
                portalIds,
                manifestDimensions);
        if (result.Success)
        {
          acceptedManifestPortals.Add(portal);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Portal,
            portal.PortalId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the portal presentations a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightPortalPresentations(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> portalIds,
        Dictionary<string, bool> portalPresentationIds)
    {
      IReadOnlyList<DimensionPortalPresentationDefinition> manifestPortalPresentations =
          request.Manifest.PortalPresentations ?? new List<DimensionPortalPresentationDefinition>();
      for (int i = 0; i < manifestPortalPresentations.Count; i++)
      {
        DimensionPortalPresentationDefinition presentation = manifestPortalPresentations[i];
        DimensionOperationResult result =
            ValidateManifestPortalPresentation(
                presentation,
                request.UpdateExisting,
                portalIds,
                portalPresentationIds);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.PortalPresentation,
            presentation.PresentationId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the progress flags a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightProgressFlags(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> progressFlagIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionProgressFlag> acceptedManifestProgressFlags)
    {
      IReadOnlyList<DimensionProgressFlag> manifestProgressFlags =
          request.Manifest.ProgressFlags ?? new List<DimensionProgressFlag>();
      for (int i = 0; i < manifestProgressFlags.Count; i++)
      {
        DimensionProgressFlag flag = manifestProgressFlags[i];
        DimensionOperationResult result =
            ValidateManifestProgressFlag(
                flag,
                request.UpdateExisting,
                dimensionIds,
                progressFlagIds,
                manifestDimensions);
        if (result.Success)
        {
          acceptedManifestProgressFlags.Add(flag);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.ProgressFlag,
            flag.FlagId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the travel requirements a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightTravelRequirements(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> portalIds,
        Dictionary<string, bool> travelRequirementIds,
        Dictionary<string, bool> progressFlagIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionPortalDefinition> acceptedManifestPortals,
        List<DimensionProgressFlag> acceptedManifestProgressFlags)
    {
      IReadOnlyList<DimensionTravelRequirementDefinition> manifestTravelRequirements =
          request.Manifest.TravelRequirements ?? new List<DimensionTravelRequirementDefinition>();
      for (int i = 0; i < manifestTravelRequirements.Count; i++)
      {
        DimensionTravelRequirementDefinition requirement = manifestTravelRequirements[i];
        DimensionOperationResult result =
            ValidateManifestTravelRequirement(
                requirement,
                request.UpdateExisting,
                dimensionIds,
                portalIds,
                travelRequirementIds,
                manifestDimensions,
                acceptedManifestPortals,
                progressFlagIds,
                acceptedManifestProgressFlags);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.TravelRequirement,
            requirement.RequirementId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the scene templates a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightSceneTemplates(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> zoneIds,
        Dictionary<string, bool> sceneTemplateIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones)
    {
      IReadOnlyList<DimensionSceneTemplateDefinition> manifestSceneTemplates =
          request.Manifest.SceneTemplates ?? new List<DimensionSceneTemplateDefinition>();
      for (int i = 0; i < manifestSceneTemplates.Count; i++)
      {
        DimensionSceneTemplateDefinition template = manifestSceneTemplates[i];
        DimensionOperationResult result =
            ValidateManifestSceneTemplate(
                template,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                sceneTemplateIds,
                manifestDimensions,
                manifestZones);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.SceneTemplate,
            template.TemplateId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the scenes a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightScenes(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> sceneIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionSceneDefinition> acceptedManifestScenes)
    {
      IReadOnlyList<DimensionSceneDefinition> manifestScenes =
          request.Manifest.Scenes ?? new List<DimensionSceneDefinition>();
      for (int i = 0; i < manifestScenes.Count; i++)
      {
        DimensionSceneDefinition scene = manifestScenes[i];
        DimensionOperationResult result =
            ValidateManifestScene(
                scene,
                request.UpdateExisting,
                dimensionIds,
                sceneIds,
                manifestDimensions,
                acceptedManifestScenes);
        if (result.Success)
        {
          acceptedManifestScenes.Add(scene);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Scene,
            scene.SceneId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the encounters a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightEncounters(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> zoneIds,
        Dictionary<string, bool> mapMarkerIds,
        Dictionary<string, bool> sceneIds,
        Dictionary<string, bool> encounterIds,
        Dictionary<string, bool> progressFlagIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones,
        List<DimensionMapMarker> acceptedManifestMarkers,
        List<DimensionSceneDefinition> acceptedManifestScenes,
        List<DimensionProgressFlag> acceptedManifestProgressFlags)
    {
      IReadOnlyList<DimensionEncounterDefinition> manifestEncounters =
          request.Manifest.Encounters ?? new List<DimensionEncounterDefinition>();
      for (int i = 0; i < manifestEncounters.Count; i++)
      {
        DimensionEncounterDefinition encounter = manifestEncounters[i];
        DimensionOperationResult result =
            ValidateManifestEncounter(
                encounter,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                sceneIds,
                progressFlagIds,
                mapMarkerIds,
                encounterIds,
                manifestDimensions,
                manifestZones,
                acceptedManifestScenes,
                acceptedManifestProgressFlags,
                acceptedManifestMarkers);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Encounter,
            encounter.EncounterId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the generation passes a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightGenerationPasses(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> zoneIds,
        Dictionary<string, bool> generationPassIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones,
        List<DimensionGenerationPassDefinition> acceptedManifestGenerationPasses)
    {
      IReadOnlyList<DimensionGenerationPassDefinition> manifestGenerationPasses =
          request.Manifest.GenerationPasses ?? new List<DimensionGenerationPassDefinition>();
      for (int i = 0; i < manifestGenerationPasses.Count; i++)
      {
        DimensionGenerationPassDefinition generationPass = manifestGenerationPasses[i];
        DimensionOperationResult result =
            ValidateManifestGenerationPass(
                generationPass,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                generationPassIds,
                manifestDimensions,
                manifestZones);
        if (result.Success)
        {
          acceptedManifestGenerationPasses.Add(generationPass);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.GenerationPass,
            generationPass.PassId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the world events a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightWorldEvents(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> zoneIds,
        Dictionary<string, bool> progressFlagIds,
        Dictionary<string, bool> worldEventIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones,
        List<DimensionProgressFlag> acceptedManifestProgressFlags)
    {
      IReadOnlyList<DimensionWorldEventDefinition> manifestWorldEvents =
          request.Manifest.WorldEvents ?? new List<DimensionWorldEventDefinition>();
      for (int i = 0; i < manifestWorldEvents.Count; i++)
      {
        DimensionWorldEventDefinition worldEvent = manifestWorldEvents[i];
        DimensionOperationResult result =
            ValidateManifestWorldEvent(
                worldEvent,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                progressFlagIds,
                worldEventIds,
                manifestDimensions,
                manifestZones,
                acceptedManifestProgressFlags);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.WorldEvent,
            worldEvent.EventId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the asset references a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightAssetReferences(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> contentPackIds,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> zoneIds,
        Dictionary<string, bool> assetReferenceIds)
    {
      IReadOnlyList<DimensionAssetReferenceDefinition> manifestAssetReferences =
          request.Manifest.AssetReferences ?? new List<DimensionAssetReferenceDefinition>();
      for (int i = 0; i < manifestAssetReferences.Count; i++)
      {
        DimensionAssetReferenceDefinition assetReference = manifestAssetReferences[i];
        DimensionOperationResult result =
            ValidateManifestAssetReference(
                assetReference,
                request.UpdateExisting,
                contentPackIds,
                dimensionIds,
                zoneIds,
                assetReferenceIds);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.AssetReference,
            assetReference.AssetId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the biomes a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightBiomes(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        Dictionary<string, bool> dimensionIds,
        Dictionary<string, bool> biomeIds)
    {
      IReadOnlyList<DimensionBiomeDefinition> manifestBiomes =
          request.Manifest.Biomes ?? new List<DimensionBiomeDefinition>();
      for (int i = 0; i < manifestBiomes.Count; i++)
      {
        DimensionBiomeDefinition biome = manifestBiomes[i];
        DimensionOperationResult result =
            ValidateManifestBiome(
                biome,
                request.UpdateExisting,
                dimensionIds,
                biomeIds);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Biome,
            biome.BiomeId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the starters a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightStarters(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        ManifestValidationContext manifestContext)
    {
      IReadOnlyList<DimensionStarterDefinition> manifestStarters =
          request.Manifest.Starters ?? new List<DimensionStarterDefinition>();
      for (int i = 0; i < manifestStarters.Count; i++)
      {
        DimensionStarterDefinition starter = manifestStarters[i];
        DimensionOperationResult result =
            ValidateManifestStarterDefinition(
                starter,
                request.UpdateExisting,
                manifestContext);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.Starter,
            starter.StarterId,
            false,
            result);
      }
    }

    /// <summary>
    /// Checks the ownership bindings a manifest declares, and records what each one would do.
    /// </summary>
    private void PreflightOwnershipBindings(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount,
        ManifestValidationContext manifestContext)
    {
      IReadOnlyList<DimensionContentOwnershipBinding> manifestOwnership =
          request.Manifest.OwnershipBindings ?? new List<DimensionContentOwnershipBinding>();
      for (int i = 0; i < manifestOwnership.Count; i++)
      {
        DimensionContentOwnershipBinding binding = manifestOwnership[i];
        DimensionOperationResult result =
            ValidateManifestContentOwnership(
                binding,
                request.RequireExistingOwnershipRecords,
                manifestContext);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            binding.RecordKind,
            binding.RecordId,
            false,
            result);
      }
    }
  }
}
