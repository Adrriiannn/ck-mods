using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Writing a manifest into the service, record kind by record kind.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    private DimensionContentManifestResult ApplyContentManifest(
        DimensionContentManifestRequest request)
    {
      List<DimensionContentManifestOperation> operations =
          new List<DimensionContentManifestOperation>();
      int errorCount = 0;
      ApplyManifestContentPacks(request, operations, ref errorCount);
      ApplyManifestDimensions(request, operations, ref errorCount);
      ApplyManifestZones(request, operations, ref errorCount);
      ApplyManifestMapLayers(request, operations, ref errorCount);
      ApplyManifestMapMarkers(request, operations, ref errorCount);
      ApplyManifestAnchors(request, operations, ref errorCount);
      ApplyManifestPortals(request, operations, ref errorCount);
      ApplyManifestPortalPresentations(request, operations, ref errorCount);
      ApplyManifestProgressFlags(request, operations, ref errorCount);
      ApplyManifestTravelRequirements(request, operations, ref errorCount);
      ApplyManifestSceneTemplates(request, operations, ref errorCount);
      ApplyManifestScenes(request, operations, ref errorCount);
      ApplyManifestEncounters(request, operations, ref errorCount);
      ApplyManifestGenerationPasses(request, operations, ref errorCount);
      ApplyManifestWorldEvents(request, operations, ref errorCount);
      ApplyManifestAssetReferences(request, operations, ref errorCount);
      ApplyManifestBiomes(request, operations, ref errorCount);
      ApplyManifestStarters(request, operations, ref errorCount);
      ApplyManifestOwnershipBindings(request, operations, ref errorCount);

      return new DimensionContentManifestResult(
          errorCount == 0,
          true,
          operations.Count,
          errorCount,
          operations);
    }

    /// <summary>
    /// Writes the content packs a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestContentPacks(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionContentPackDefinition> manifestContentPacks =
          request.Manifest.ContentPacks ?? new List<DimensionContentPackDefinition>();
      for (int i = 0; i < manifestContentPacks.Count; i++)
      {
        DimensionContentPackDefinition contentPack = manifestContentPacks[i];
        bool exists = contentPacks.ContainsKey(contentPack.ContentPackId);
        bool success = exists
            ? TryUpdateContentPack(contentPack, request.Reason, out result)
            : TryRegisterContentPack(contentPack, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Custom,
            contentPack.ContentPackId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the dimensions a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestDimensions(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionDefinition> manifestDimensionDefinitions =
          request.Manifest.Dimensions ?? new List<DimensionDefinition>();
      for (int i = 0; i < manifestDimensionDefinitions.Count; i++)
      {
        DimensionDefinition dimension = manifestDimensionDefinitions[i];
        DimensionDefinition existing;
        bool exists = definitions.TryGetValue(dimension.Id, out existing);
        bool success;
        if (exists)
        {
          success = DimensionDefinitionEquals(existing, dimension);
          result = success
              ? new DimensionOperationResult(true, string.Empty, "Dimension already registered with matching definition.")
              : DimensionOperationResult.Failed(
                  "dimension-update-not-supported",
                  "Manifest apply cannot mutate an existing dimension definition.");
        }
        else
        {
          success = TryRegisterDimension(dimension, out result);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Validate : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Dimension,
            dimension.Id,
            !exists && success,
            result);
      }
    }

    /// <summary>
    /// Writes the zones a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestZones(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionZoneDefinition> manifestZoneDefinitions =
          request.Manifest.Zones ?? new List<DimensionZoneDefinition>();
      for (int i = 0; i < manifestZoneDefinitions.Count; i++)
      {
        DimensionZoneDefinition zone = manifestZoneDefinitions[i];
        bool exists = zoneDefinitions.ContainsKey(zone.ZoneId);
        bool success = exists
            ? TryUpdateZoneDefinition(zone, request.Reason, out result)
            : TryRegisterZoneDefinition(zone, out result);
        // The gates are bound inside register/update now (NullforgeDimensionService.Zones.cs,
        // BindZoneToGenerationGates), so this loop no longer binds them itself. Manifest apply
        // was never the only way a zone reaches the catalog, and the bootstrap's own zones came
        // in the other way — which is how a biome's ore list could be compiled, shipped and
        // still never gate anything.
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.ZoneDefinition,
            zone.ZoneId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the map layers a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestMapLayers(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionMapLayerDefinition> manifestMapLayers =
          request.Manifest.MapLayers ?? new List<DimensionMapLayerDefinition>();
      for (int i = 0; i < manifestMapLayers.Count; i++)
      {
        DimensionMapLayerDefinition layer = manifestMapLayers[i];
        bool exists = mapLayers.ContainsKey(layer.LayerId);
        bool success = exists
            ? TryUpdateMapLayer(layer, request.Reason, out result)
            : TryRegisterMapLayer(layer, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.MapLayer,
            layer.LayerId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the map markers a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestMapMarkers(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionMapMarker> manifestMapMarkers =
          request.Manifest.MapMarkers ?? new List<DimensionMapMarker>();
      for (int i = 0; i < manifestMapMarkers.Count; i++)
      {
        DimensionMapMarker marker = manifestMapMarkers[i];
        bool exists = markers.ContainsKey(marker.MarkerId);
        bool success = exists
            ? TryUpdateMarker(marker, request.Reason, out result)
            : TryRegisterMarker(marker, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.MapMarker,
            marker.MarkerId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the anchors a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestAnchors(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionAnchorDefinition> manifestAnchors =
          request.Manifest.Anchors ?? new List<DimensionAnchorDefinition>();
      for (int i = 0; i < manifestAnchors.Count; i++)
      {
        DimensionAnchorDefinition anchor = manifestAnchors[i];
        bool exists = anchors.ContainsKey(anchor.AnchorId);
        bool success = exists
            ? TryUpdateAnchor(anchor, request.Reason, out result)
            : TryRegisterAnchor(anchor, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Anchor,
            anchor.AnchorId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the portals a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestPortals(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionPortalDefinition> manifestPortals =
          request.Manifest.Portals ?? new List<DimensionPortalDefinition>();
      for (int i = 0; i < manifestPortals.Count; i++)
      {
        DimensionPortalDefinition portal = manifestPortals[i];
        DimensionPortalDefinition existingPortal;
        bool exists = portals.TryGetValue(portal.PortalId, out existingPortal);
        bool success;
        if (exists)
        {
          if (PortalDefinitionEquals(existingPortal, portal))
          {
            success = true;
            result = DimensionOperationResult.Ok();
          }
          else if (PortalDefinitionRouteEquals(existingPortal, portal))
          {
            success = TrySetPortalState(portal.PortalId, portal.State, request.Reason, out result);
          }
          else
          {
            success = false;
            result = DimensionOperationResult.Failed(
                "portal-update-not-supported",
                "Manifest apply cannot move or reroute an existing portal definition.");
          }
        }
        else
        {
          success = TryRegisterPortal(portal, out result);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Portal,
            portal.PortalId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the portal presentations a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestPortalPresentations(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionPortalPresentationDefinition> manifestPortalPresentations =
          request.Manifest.PortalPresentations ?? new List<DimensionPortalPresentationDefinition>();
      for (int i = 0; i < manifestPortalPresentations.Count; i++)
      {
        DimensionPortalPresentationDefinition presentation = manifestPortalPresentations[i];
        bool exists = portalPresentations.ContainsKey(presentation.PresentationId);
        bool success = exists
            ? TryUpdatePortalPresentation(presentation, request.Reason, out result)
            : TryRegisterPortalPresentation(presentation, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.PortalPresentation,
            presentation.PresentationId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the progress flags a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestProgressFlags(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionProgressFlag> manifestProgressFlags =
          request.Manifest.ProgressFlags ?? new List<DimensionProgressFlag>();
      for (int i = 0; i < manifestProgressFlags.Count; i++)
      {
        DimensionProgressFlag flag = manifestProgressFlags[i];
        bool exists = progressFlags.ContainsKey(flag.FlagId);
        bool success =
            TrySetProgressFlag(
                flag.FlagId,
                flag.DimensionId,
                flag.Category,
                flag.Value,
                request.Reason,
                out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.ProgressFlag,
            flag.FlagId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the travel requirements a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestTravelRequirements(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionTravelRequirementDefinition> manifestTravelRequirements =
          request.Manifest.TravelRequirements ?? new List<DimensionTravelRequirementDefinition>();
      for (int i = 0; i < manifestTravelRequirements.Count; i++)
      {
        DimensionTravelRequirementDefinition requirement = manifestTravelRequirements[i];
        bool exists = travelRequirements.ContainsKey(requirement.RequirementId);
        bool success = exists
            ? TryUpdateTravelRequirement(requirement, request.Reason, out result)
            : TryRegisterTravelRequirement(requirement, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.TravelRequirement,
            requirement.RequirementId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the scene templates a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestSceneTemplates(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionSceneTemplateDefinition> manifestSceneTemplates =
          request.Manifest.SceneTemplates ?? new List<DimensionSceneTemplateDefinition>();
      for (int i = 0; i < manifestSceneTemplates.Count; i++)
      {
        DimensionSceneTemplateDefinition template = manifestSceneTemplates[i];
        bool exists = sceneTemplates.ContainsKey(template.TemplateId);
        bool success = exists
            ? TryUpdateSceneTemplate(template, request.Reason, out result)
            : TryRegisterSceneTemplate(template, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.SceneTemplate,
            template.TemplateId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the scenes a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestScenes(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionSceneDefinition> manifestScenes =
          request.Manifest.Scenes ?? new List<DimensionSceneDefinition>();
      for (int i = 0; i < manifestScenes.Count; i++)
      {
        DimensionSceneDefinition scene = manifestScenes[i];
        bool exists = scenes.ContainsKey(scene.SceneId);
        bool success = exists
            ? TryUpdateScene(scene, request.Reason, out result)
            : TryRegisterScene(scene, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Scene,
            scene.SceneId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the encounters a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestEncounters(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionEncounterDefinition> manifestEncounters =
          request.Manifest.Encounters ?? new List<DimensionEncounterDefinition>();
      for (int i = 0; i < manifestEncounters.Count; i++)
      {
        DimensionEncounterDefinition encounter = manifestEncounters[i];
        bool exists = encounters.ContainsKey(encounter.EncounterId);
        bool success = exists
            ? TryUpdateEncounter(encounter, request.Reason, out result)
            : TryRegisterEncounter(encounter, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Encounter,
            encounter.EncounterId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the generation passes a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestGenerationPasses(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionGenerationPassDefinition> manifestGenerationPasses =
          request.Manifest.GenerationPasses ?? new List<DimensionGenerationPassDefinition>();
      for (int i = 0; i < manifestGenerationPasses.Count; i++)
      {
        DimensionGenerationPassDefinition generationPass = manifestGenerationPasses[i];
        bool exists = generationPasses.ContainsKey(generationPass.PassId);
        bool success = exists
            ? TryUpdateGenerationPass(generationPass, request.Reason, out result)
            : TryRegisterGenerationPass(generationPass, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.GenerationPass,
            generationPass.PassId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the world events a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestWorldEvents(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionWorldEventDefinition> manifestWorldEvents =
          request.Manifest.WorldEvents ?? new List<DimensionWorldEventDefinition>();
      for (int i = 0; i < manifestWorldEvents.Count; i++)
      {
        DimensionWorldEventDefinition worldEvent = manifestWorldEvents[i];
        bool exists = worldEvents.ContainsKey(worldEvent.EventId);
        bool success = exists
            ? TryUpdateWorldEvent(worldEvent, request.Reason, out result)
            : TryRegisterWorldEvent(worldEvent, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.WorldEvent,
            worldEvent.EventId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the asset references a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestAssetReferences(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionAssetReferenceDefinition> manifestAssetReferences =
          request.Manifest.AssetReferences ?? new List<DimensionAssetReferenceDefinition>();
      for (int i = 0; i < manifestAssetReferences.Count; i++)
      {
        DimensionAssetReferenceDefinition assetReference = manifestAssetReferences[i];
        bool exists = assetReferences.ContainsKey(assetReference.AssetId);
        bool success = exists
            ? TryUpdateAssetReference(assetReference, request.Reason, out result)
            : TryRegisterAssetReference(assetReference, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.AssetReference,
            assetReference.AssetId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the biomes a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestBiomes(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionBiomeDefinition> manifestBiomes =
          request.Manifest.Biomes ?? new List<DimensionBiomeDefinition>();
      for (int i = 0; i < manifestBiomes.Count; i++)
      {
        DimensionBiomeDefinition biome = manifestBiomes[i];
        bool exists = biomes.ContainsKey(biome.BiomeId);
        bool success = exists
            ? TryUpdateBiome(biome, request.Reason, out result)
            : TryRegisterBiome(biome, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Biome,
            biome.BiomeId,
            success,
            result);
      }
    }

    /// <summary>
    /// Writes the starters a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestStarters(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionStarterDefinition> manifestStarters =
          request.Manifest.Starters ?? new List<DimensionStarterDefinition>();
      for (int i = 0; i < manifestStarters.Count; i++)
      {
        DimensionStarterDefinition starter = manifestStarters[i];
        DimensionStarterDefinition existingStarter;
        bool exists = starters.TryGetValue(starter.StarterId, out existingStarter);
        bool success;
        if (exists)
        {
          success = StarterDefinitionEquals(existingStarter, starter);
          result = success
              ? new DimensionOperationResult(true, string.Empty, "Dimension starter already registered with matching definition.")
              : DimensionOperationResult.Failed(
                  "starter-update-not-supported",
                  "Manifest apply cannot mutate an existing dimension starter definition.");
        }
        else
        {
          success = TryRegisterStarter(starter, out result);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Validate : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.Starter,
            starter.StarterId,
            !exists && success,
            result);
      }
    }

    /// <summary>
    /// Writes the ownership bindings a manifest declares, and records what each write did.
    /// </summary>
    private void ApplyManifestOwnershipBindings(
        DimensionContentManifestRequest request,
        List<DimensionContentManifestOperation> operations,
        ref int errorCount)
    {
      DimensionOperationResult result;
      IReadOnlyList<DimensionContentOwnershipBinding> manifestOwnership =
          request.Manifest.OwnershipBindings ?? new List<DimensionContentOwnershipBinding>();
      for (int i = 0; i < manifestOwnership.Count; i++)
      {
        DimensionContentOwnershipBinding binding = manifestOwnership[i];
        bool success =
            TryBindContentOwnership(
                binding,
                request.RequireExistingOwnershipRecords,
                request.Reason,
                out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.BindOwnership,
            binding.RecordKind,
            binding.RecordId,
            success,
            result);
      }
    }
  }
}
