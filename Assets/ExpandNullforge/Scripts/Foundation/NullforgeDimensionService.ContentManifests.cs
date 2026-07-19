using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateContentPack(
        DimensionContentPackDefinition contentPack,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(contentPack.ContentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
        return false;
      }

      if (contentPack.MinimumApiVersion > DimensionApi.CurrentApiVersion)
      {
        result = DimensionOperationResult.Failed("content-pack-api-too-new", "The content pack requires a newer Dimension API version.");
        return false;
      }

      if (contentPack.DependencyIds != null)
      {
        for (int i = 0; i < contentPack.DependencyIds.Count; i++)
        {
          if (string.Equals(contentPack.DependencyIds[i], contentPack.ContentPackId, StringComparison.Ordinal))
          {
            result = DimensionOperationResult.Failed("content-pack-self-dependency", "A content pack cannot depend on itself.");
            return false;
          }
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareContentPacks(
        DimensionContentPackDefinition left,
        DimensionContentPackDefinition right)
    {
      return string.Compare(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal);
    }

    private static int CompareContentPackReadiness(
        DimensionContentPackReadinessResult left,
        DimensionContentPackReadinessResult right)
    {
      return string.Compare(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal);
    }

    private static int CompareContentPackSummaries(
        DimensionContentPackSummary left,
        DimensionContentPackSummary right)
    {
      return string.Compare(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal);
    }

    private static int CompareContentPackRecordCounts(
        DimensionContentPackRecordCount left,
        DimensionContentPackRecordCount right)
    {
      return left.RecordKind.CompareTo(right.RecordKind);
    }

    private DimensionContentPackSummary BuildContentPackSummary(
        DimensionContentPackDefinition contentPack,
        DimensionContentPackSummaryRequest request)
    {
      Dictionary<DimensionContentRecordKind, int> counts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<DimensionContentRecordKind, int> orphanCounts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<string, bool> recordKeys =
          new Dictionary<string, bool>(StringComparer.Ordinal);

      int ownedRecordCount = 0;
      int orphanedRecordCount = 0;
      foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
      {
        if (!string.Equals(binding.ContentPackId, contentPack.ContentPackId, StringComparison.Ordinal))
        {
          continue;
        }

        bool isOrphaned =
            !ContentRecordExists(binding.RecordKind, binding.RecordId) &&
            !IsMetadataOnlyContentRecordKind(binding.RecordKind);
        if (isOrphaned && !request.IncludeOrphanedOwnership)
        {
          continue;
        }

        if (TryCountContentPackSummaryRecord(
                binding.RecordKind,
                binding.RecordId,
                counts,
                recordKeys))
        {
          ownedRecordCount++;
        }

        if (isOrphaned)
        {
          orphanedRecordCount++;
          IncrementContentPackSummaryCount(orphanCounts, binding.RecordKind);
        }
      }

      foreach (DimensionAssetReferenceDefinition assetReference in assetReferences.Values)
      {
        if (!string.Equals(assetReference.ContentPackId, contentPack.ContentPackId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!request.IncludeDisabled && !assetReference.Enabled)
        {
          continue;
        }

        if (TryCountContentPackSummaryRecord(
                DimensionContentRecordKind.AssetReference,
                assetReference.AssetId,
                counts,
                recordKeys))
        {
          ownedRecordCount++;
        }
      }

      int validationErrorCount = 0;
      int validationWarningCount = 0;
      if (request.IncludeValidation)
      {
        DimensionContentValidationReport validation =
            ValidateContent(
                new DimensionContentValidationRequest(
                    contentPack.ContentPackId,
                    request.IncludeDisabled,
                    request.IncludeWarnings));
        validationErrorCount = validation.ErrorCount;
        validationWarningCount = validation.WarningCount;
      }

      DimensionContentPackReadinessResult readiness =
          EvaluateContentPackReadinessInternal(contentPack);

      return new DimensionContentPackSummary(
          contentPack.ContentPackId,
          true,
          contentPack,
          readiness,
          ownedRecordCount,
          orphanedRecordCount,
          validationErrorCount,
          validationWarningCount,
          BuildContentPackRecordCounts(counts, orphanCounts));
    }

    private DimensionContentPackSummary BuildMissingContentPackSummary(
        string contentPackId,
        DimensionContentPackSummaryRequest request)
    {
      Dictionary<DimensionContentRecordKind, int> counts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<DimensionContentRecordKind, int> orphanCounts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<string, bool> recordKeys =
          new Dictionary<string, bool>(StringComparer.Ordinal);

      int ownedRecordCount = 0;
      int orphanedRecordCount = 0;
      if (request.IncludeOrphanedOwnership)
      {
        foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
        {
          if (!string.Equals(binding.ContentPackId, contentPackId, StringComparison.Ordinal))
          {
            continue;
          }

          if (TryCountContentPackSummaryRecord(
                  binding.RecordKind,
                  binding.RecordId,
                  counts,
                  recordKeys))
          {
            ownedRecordCount++;
          }

          orphanedRecordCount++;
          IncrementContentPackSummaryCount(orphanCounts, binding.RecordKind);
        }
      }

      DimensionContentPackReadinessResult readiness =
          EvaluateContentPackReadiness(contentPackId);
      int validationErrorCount = request.IncludeValidation && !readiness.Ready ? 1 : 0;
      return new DimensionContentPackSummary(
          contentPackId,
          false,
          default(DimensionContentPackDefinition),
          readiness,
          ownedRecordCount,
          orphanedRecordCount,
          validationErrorCount,
          0,
          BuildContentPackRecordCounts(counts, orphanCounts));
    }

    private void AddMissingContentPackSummaries(
        List<DimensionContentPackSummary> summaries,
        DimensionContentPackSummaryRequest request)
    {
      if (!request.IncludeOrphanedOwnership)
      {
        return;
      }

      Dictionary<string, bool> summaryIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      for (int i = 0; i < summaries.Count; i++)
      {
        if (!string.IsNullOrEmpty(summaries[i].ContentPackId))
        {
          summaryIds[summaries[i].ContentPackId] = true;
        }
      }

      Dictionary<string, bool> missingIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
      {
        if (string.IsNullOrEmpty(binding.ContentPackId) ||
            summaryIds.ContainsKey(binding.ContentPackId) ||
            contentPacks.ContainsKey(binding.ContentPackId))
        {
          continue;
        }

        missingIds[binding.ContentPackId] = true;
      }

      foreach (string contentPackId in missingIds.Keys)
      {
        summaries.Add(BuildMissingContentPackSummary(contentPackId, request));
      }
    }

    private static bool TryCountContentPackSummaryRecord(
        DimensionContentRecordKind recordKind,
        string recordId,
        Dictionary<DimensionContentRecordKind, int> counts,
        Dictionary<string, bool> recordKeys)
    {
      if (string.IsNullOrEmpty(recordId))
      {
        return false;
      }

      string key = BuildContentOwnershipKey(recordKind, recordId);
      if (recordKeys.ContainsKey(key))
      {
        return false;
      }

      recordKeys[key] = true;
      IncrementContentPackSummaryCount(counts, recordKind);
      return true;
    }

    private static void IncrementContentPackSummaryCount(
        Dictionary<DimensionContentRecordKind, int> counts,
        DimensionContentRecordKind recordKind)
    {
      int count;
      counts.TryGetValue(recordKind, out count);
      counts[recordKind] = count + 1;
    }

    private static List<DimensionContentPackRecordCount> BuildContentPackRecordCounts(
        Dictionary<DimensionContentRecordKind, int> counts,
        Dictionary<DimensionContentRecordKind, int> orphanCounts)
    {
      List<DimensionContentPackRecordCount> result =
          new List<DimensionContentPackRecordCount>();
      foreach (KeyValuePair<DimensionContentRecordKind, int> pair in counts)
      {
        int orphanedCount;
        orphanCounts.TryGetValue(pair.Key, out orphanedCount);
        result.Add(
            new DimensionContentPackRecordCount(
                pair.Key,
                pair.Value,
                orphanedCount));
      }

      result.Sort(CompareContentPackRecordCounts);
      return result;
    }

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
      Dictionary<string, bool> spawnRuleIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> encounterIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> generationPassIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> resourceNodeIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> progressFlagIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> worldEventIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> assetReferenceIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> environmentProfileIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> biomeIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> tableIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      Dictionary<string, bool> entryIds =
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
            SpawnRuleIds = spawnRuleIds,
            EncounterIds = encounterIds,
            ResourceNodeIds = resourceNodeIds,
            ProgressFlagIds = progressFlagIds,
            WorldEventIds = worldEventIds,
            GenerationPassIds = generationPassIds,
            AssetReferenceIds = assetReferenceIds,
            EnvironmentProfileIds = environmentProfileIds,
            BiomeIds = biomeIds,
            TableIds = tableIds,
            EntryIds = entryIds,
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
      List<DimensionSpawnRule> acceptedManifestSpawnRules =
          new List<DimensionSpawnRule>();
      List<DimensionGenerationPassDefinition> acceptedManifestGenerationPasses =
          new List<DimensionGenerationPassDefinition>();
      List<DimensionProgressFlag> acceptedManifestProgressFlags =
          new List<DimensionProgressFlag>();

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

      IReadOnlyList<DimensionSpawnRule> manifestSpawnRules =
          request.Manifest.SpawnRules ?? new List<DimensionSpawnRule>();
      for (int i = 0; i < manifestSpawnRules.Count; i++)
      {
        DimensionSpawnRule rule = manifestSpawnRules[i];
        DimensionOperationResult result =
            ValidateManifestSpawnRule(
                rule,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                spawnRuleIds,
                manifestDimensions,
                manifestZones);
        if (result.Success)
        {
          acceptedManifestSpawnRules.Add(rule);
        }

        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.SpawnRule,
            rule.RuleId,
            false,
            result);
      }

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
                spawnRuleIds,
                progressFlagIds,
                mapMarkerIds,
                encounterIds,
                manifestDimensions,
                manifestZones,
                acceptedManifestScenes,
                acceptedManifestSpawnRules,
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

      IReadOnlyList<DimensionResourceNodeDefinition> manifestResourceNodes =
          request.Manifest.ResourceNodes ?? new List<DimensionResourceNodeDefinition>();
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

      for (int i = 0; i < manifestResourceNodes.Count; i++)
      {
        DimensionResourceNodeDefinition node = manifestResourceNodes[i];
        DimensionOperationResult result =
            ValidateManifestResourceNode(
                node,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                generationPassIds,
                resourceNodeIds,
                manifestDimensions,
                manifestZones,
                acceptedManifestGenerationPasses);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.ResourceNode,
            node.NodeId,
            false,
            result);
      }

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

      IReadOnlyList<DimensionEnvironmentProfile> manifestEnvironmentProfiles =
          request.Manifest.EnvironmentProfiles ?? new List<DimensionEnvironmentProfile>();
      for (int i = 0; i < manifestEnvironmentProfiles.Count; i++)
      {
        DimensionEnvironmentProfile profile = manifestEnvironmentProfiles[i];
        DimensionOperationResult result =
            ValidateManifestEnvironmentProfile(
                profile,
                request.UpdateExisting,
                dimensionIds,
                zoneIds,
                manifestZones,
                environmentProfileIds);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.EnvironmentProfile,
            profile.ProfileId,
            false,
            result);
      }

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
                environmentProfileIds,
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

      IReadOnlyList<DimensionGenerationTableDefinition> manifestTables =
          request.Manifest.GenerationTables ?? new List<DimensionGenerationTableDefinition>();
      for (int i = 0; i < manifestTables.Count; i++)
      {
        DimensionGenerationTableDefinition table = manifestTables[i];
        DimensionOperationResult result =
            ValidateManifestGenerationTable(
                table,
                request.UpdateExisting,
                dimensionIds,
                biomeIds,
                tableIds);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.GenerationTable,
            table.TableId,
            false,
            result);
      }

      IReadOnlyList<DimensionGenerationTableEntryDefinition> manifestEntries =
          request.Manifest.GenerationTableEntries ?? new List<DimensionGenerationTableEntryDefinition>();
      for (int i = 0; i < manifestEntries.Count; i++)
      {
        DimensionGenerationTableEntryDefinition entry = manifestEntries[i];
        DimensionOperationResult result =
            ValidateManifestGenerationTableEntry(
                entry,
                request.UpdateExisting,
                tableIds,
                entryIds);
        AddManifestOperation(
            operations,
            ref errorCount,
            DimensionContentManifestOperationKind.Validate,
            DimensionContentRecordKind.GenerationTableEntry,
            entry.EntryId,
            false,
            result);
      }

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

      return new DimensionContentManifestResult(
          errorCount == 0,
          false,
          operations.Count,
          errorCount,
          operations);
    }

    private DimensionContentManifestResult ApplyContentManifest(
        DimensionContentManifestRequest request)
    {
      List<DimensionContentManifestOperation> operations =
          new List<DimensionContentManifestOperation>();
      int errorCount = 0;
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

      IReadOnlyList<DimensionZoneDefinition> manifestZoneDefinitions =
          request.Manifest.Zones ?? new List<DimensionZoneDefinition>();
      for (int i = 0; i < manifestZoneDefinitions.Count; i++)
      {
        DimensionZoneDefinition zone = manifestZoneDefinitions[i];
        bool exists = zoneDefinitions.ContainsKey(zone.ZoneId);
        bool success = exists
            ? TryUpdateZoneDefinition(zone, request.Reason, out result)
            : TryRegisterZoneDefinition(zone, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.ZoneDefinition,
            zone.ZoneId,
            success,
            result);
      }

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

      IReadOnlyList<DimensionSpawnRule> manifestSpawnRules =
          request.Manifest.SpawnRules ?? new List<DimensionSpawnRule>();
      for (int i = 0; i < manifestSpawnRules.Count; i++)
      {
        DimensionSpawnRule rule = manifestSpawnRules[i];
        bool exists = spawnRules.ContainsKey(rule.RuleId);
        bool success = exists
            ? TryUpdateSpawnRule(rule, request.Reason, out result)
            : TryRegisterSpawnRule(rule, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.SpawnRule,
            rule.RuleId,
            success,
            result);
      }

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

      IReadOnlyList<DimensionResourceNodeDefinition> manifestResourceNodes =
          request.Manifest.ResourceNodes ?? new List<DimensionResourceNodeDefinition>();
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

      for (int i = 0; i < manifestResourceNodes.Count; i++)
      {
        DimensionResourceNodeDefinition node = manifestResourceNodes[i];
        bool exists = resourceNodes.ContainsKey(node.NodeId);
        bool success = exists
            ? TryUpdateResourceNode(node, request.Reason, out result)
            : TryRegisterResourceNode(node, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.ResourceNode,
            node.NodeId,
            success,
            result);
      }

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

      IReadOnlyList<DimensionEnvironmentProfile> manifestEnvironmentProfiles =
          request.Manifest.EnvironmentProfiles ?? new List<DimensionEnvironmentProfile>();
      for (int i = 0; i < manifestEnvironmentProfiles.Count; i++)
      {
        DimensionEnvironmentProfile profile = manifestEnvironmentProfiles[i];
        bool exists = environmentProfiles.ContainsKey(profile.ProfileId);
        bool success = exists
            ? TryUpdateEnvironmentProfile(profile, request.Reason, out result)
            : TryRegisterEnvironmentProfile(profile, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.EnvironmentProfile,
            profile.ProfileId,
            success,
            result);
      }

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

      IReadOnlyList<DimensionGenerationTableDefinition> manifestTables =
          request.Manifest.GenerationTables ?? new List<DimensionGenerationTableDefinition>();
      for (int i = 0; i < manifestTables.Count; i++)
      {
        DimensionGenerationTableDefinition table = manifestTables[i];
        bool exists = generationTables.ContainsKey(table.TableId);
        bool success = exists
            ? TryUpdateGenerationTable(table, request.Reason, out result)
            : TryRegisterGenerationTable(table, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.GenerationTable,
            table.TableId,
            success,
            result);
      }

      IReadOnlyList<DimensionGenerationTableEntryDefinition> manifestEntries =
          request.Manifest.GenerationTableEntries ?? new List<DimensionGenerationTableEntryDefinition>();
      for (int i = 0; i < manifestEntries.Count; i++)
      {
        DimensionGenerationTableEntryDefinition entry = manifestEntries[i];
        bool exists = generationTableEntries.ContainsKey(entry.EntryId);
        bool success = exists
            ? TryUpdateGenerationTableEntry(entry, request.Reason, out result)
            : TryRegisterGenerationTableEntry(entry, out result);
        AddManifestOperation(
            operations,
            ref errorCount,
            exists ? DimensionContentManifestOperationKind.Update : DimensionContentManifestOperationKind.Register,
            DimensionContentRecordKind.GenerationTableEntry,
            entry.EntryId,
            success,
            result);
      }

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

      return new DimensionContentManifestResult(
          errorCount == 0,
          true,
          operations.Count,
          errorCount,
          operations);
    }

    private DimensionOperationResult ValidateManifestContentPack(
        DimensionContentPackDefinition contentPack,
        bool updateExisting,
        Dictionary<string, bool> manifestIds)
    {
      DimensionOperationResult result;
      if (!ValidateContentPack(contentPack, out result))
      {
        return result;
      }

      if (!TryAddManifestId(manifestIds, contentPack.ContentPackId))
      {
        return DimensionOperationResult.Failed("manifest-content-pack-duplicate", "The manifest contains the same content pack more than once.");
      }

      if (!updateExisting && contentPacks.ContainsKey(contentPack.ContentPackId))
      {
        return DimensionOperationResult.Failed("content-pack-already-registered", "A content pack with that id is already registered.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestDimension(
        DimensionDefinition dimension,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        List<DimensionDefinition> acceptedManifestDimensions)
    {
      if (string.IsNullOrEmpty(dimension.Id))
      {
        return DimensionOperationResult.Failed("dimension-id-empty", "A dimension id is required.");
      }

      if (!TryAddManifestId(manifestDimensionIds, dimension.Id))
      {
        return DimensionOperationResult.Failed("manifest-dimension-duplicate", "The manifest contains the same dimension more than once.");
      }

      int2 size = dimension.LocalBounds.Size;
      if (size.x <= 0 || size.y <= 0)
      {
        return DimensionOperationResult.Failed("dimension-bounds-invalid", "A dimension must have positive local bounds.");
      }

      DimensionDefinition existing;
      if (definitions.TryGetValue(dimension.Id, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("dimension-already-registered", "A dimension with that id is already registered.");
        }

        if (!DimensionDefinitionEquals(existing, dimension))
        {
          return DimensionOperationResult.Failed(
              "dimension-update-not-supported",
              "Manifest preflight cannot mutate an existing dimension definition.");
        }

        return DimensionOperationResult.Ok();
      }

      if (dimension.Id != DimensionIds.Overworld && OverlapsExistingNonOverworldDimension(dimension))
      {
        return DimensionOperationResult.Failed(
            "dimension-absolute-bounds-overlap",
            "The requested dimension absolute bounds overlap another registered non-overworld dimension.");
      }

      for (int i = 0; i < acceptedManifestDimensions.Count; i++)
      {
        DimensionDefinition existingManifestDimension = acceptedManifestDimensions[i];
        if (dimension.Id == DimensionIds.Overworld ||
            existingManifestDimension.Id == DimensionIds.Overworld)
        {
          continue;
        }

        if (BoundsOverlap(dimension.AbsoluteBounds, existingManifestDimension.AbsoluteBounds))
        {
          return DimensionOperationResult.Failed(
              "manifest-dimension-absolute-bounds-overlap",
              "The requested dimension absolute bounds overlap another dimension in this manifest.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestZoneDefinition(
        DimensionZoneDefinition zone,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(zone.ZoneId))
      {
        return DimensionOperationResult.Failed("zone-id-empty", "A zone id is required.");
      }

      if (!TryAddManifestId(manifestZoneIds, zone.ZoneId))
      {
        return DimensionOperationResult.Failed("manifest-zone-duplicate", "The manifest contains the same zone more than once.");
      }

      if (!updateExisting && zoneDefinitions.ContainsKey(zone.ZoneId))
      {
        return DimensionOperationResult.Failed("zone-already-registered", "A zone with that id is already registered.");
      }

      if (zone.LocalBounds.Size.x <= 0 || zone.LocalBounds.Size.y <= 0)
      {
        return DimensionOperationResult.Failed("zone-bounds-invalid", "The zone local bounds must have a positive size.");
      }

      if (string.IsNullOrEmpty(zone.DimensionId))
      {
        return DimensionOperationResult.Failed("zone-dimension-not-found", "The zone dimension is not registered or declared by this manifest.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(zone.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("zone-dimension-not-found", "The zone dimension is not registered or declared by this manifest.");
      }

      if (!dimension.LocalBounds.Contains(zone.LocalBounds.Min) ||
          !dimension.LocalBounds.Contains(zone.LocalBounds.MaxExclusive - new int2(1, 1)))
      {
        return DimensionOperationResult.Failed("zone-bounds-out-of-dimension", "The zone bounds are outside the zone dimension.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestMapLayer(
        DimensionMapLayerDefinition layer,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestMapLayerIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(layer.LayerId))
      {
        return DimensionOperationResult.Failed("map-layer-id-empty", "A map layer id is required.");
      }

      if (!TryAddManifestId(manifestMapLayerIds, layer.LayerId))
      {
        return DimensionOperationResult.Failed("manifest-map-layer-duplicate", "The manifest contains the same map layer more than once.");
      }

      DimensionMapLayerDefinition existing;
      if (mapLayers.TryGetValue(layer.LayerId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("map-layer-already-registered", "A map layer with that id is already registered.");
        }

        if (IsProtectedMapLayerId(layer.LayerId) && !MapLayerAnchorEquals(existing, layer))
        {
          return DimensionOperationResult.Failed("map-layer-protected", "Built-in map layers cannot be moved to another dimension.");
        }
      }

      if (string.IsNullOrEmpty(layer.DimensionId))
      {
        return DimensionOperationResult.Failed("map-layer-dimension-not-found", "The map layer dimension is not registered or declared by this manifest.");
      }

      if (!TryGetManifestAwareDimension(layer.DimensionId, manifestDimensionIds, manifestDimensions, out _))
      {
        return DimensionOperationResult.Failed("map-layer-dimension-not-found", "The map layer dimension is not registered or declared by this manifest.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestMapMarker(
        DimensionMapMarker marker,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestMapMarkerIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(marker.MarkerId))
      {
        return DimensionOperationResult.Failed("marker-id-empty", "A marker id is required.");
      }

      if (!TryAddManifestId(manifestMapMarkerIds, marker.MarkerId))
      {
        return DimensionOperationResult.Failed("manifest-map-marker-duplicate", "The manifest contains the same map marker more than once.");
      }

      DimensionMapMarker existing;
      if (markers.TryGetValue(marker.MarkerId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("marker-already-registered", "A marker with that id is already registered.");
        }

        if (IsProtectedMarkerId(marker.MarkerId) && !MarkerAnchorEquals(existing, marker))
        {
          return DimensionOperationResult.Failed("marker-protected", "Built-in dimension markers cannot be moved to another anchor.");
        }
      }

      if (string.IsNullOrEmpty(marker.DimensionId))
      {
        return DimensionOperationResult.Failed("marker-dimension-not-found", "The marker dimension is not registered or declared by this manifest.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(marker.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("marker-dimension-not-found", "The marker dimension is not registered or declared by this manifest.");
      }

      if (!dimension.ContainsLocal(marker.LocalPosition))
      {
        return DimensionOperationResult.Failed("marker-position-out-of-bounds", "The marker position is outside the marker dimension.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestAnchor(
        DimensionAnchorDefinition anchor,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestAnchorIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(anchor.AnchorId))
      {
        return DimensionOperationResult.Failed("anchor-id-empty", "An anchor id is required.");
      }

      if (!TryAddManifestId(manifestAnchorIds, anchor.AnchorId))
      {
        return DimensionOperationResult.Failed("manifest-anchor-duplicate", "The manifest contains the same anchor more than once.");
      }

      if (!IsValidAnchorKind(anchor.Kind))
      {
        return DimensionOperationResult.Failed("anchor-kind-invalid", "The anchor kind is not supported.");
      }

      DimensionAnchorDefinition existing;
      if (anchors.TryGetValue(anchor.AnchorId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("anchor-already-registered", "An anchor with that id is already registered.");
        }

        if (IsProtectedAnchorId(anchor.AnchorId) && !AnchorLocationEquals(existing, anchor))
        {
          return DimensionOperationResult.Failed("anchor-protected", "Built-in dimension anchors cannot be moved or changed to another kind.");
        }
      }

      if (string.IsNullOrEmpty(anchor.DimensionId))
      {
        return DimensionOperationResult.Failed("anchor-dimension-not-found", "The anchor dimension is not registered or declared by this manifest.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(anchor.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("anchor-dimension-not-found", "The anchor dimension is not registered or declared by this manifest.");
      }

      if (!dimension.ContainsLocal(anchor.LocalPosition))
      {
        return DimensionOperationResult.Failed("anchor-position-out-of-bounds", "The anchor position is outside the anchor dimension.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestPortal(
        DimensionPortalDefinition portal,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestPortalIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(portal.PortalId))
      {
        return DimensionOperationResult.Failed("portal-id-empty", "A portal id is required.");
      }

      if (!TryAddManifestId(manifestPortalIds, portal.PortalId))
      {
        return DimensionOperationResult.Failed("manifest-portal-duplicate", "The manifest contains the same portal more than once.");
      }

      if (!IsValidPortalState(portal.State))
      {
        return DimensionOperationResult.Failed("portal-state-invalid", "The portal state is not valid.");
      }

      DimensionPortalDefinition existing;
      if (portals.TryGetValue(portal.PortalId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("portal-already-registered", "A portal with that id is already registered.");
        }

        if (!PortalDefinitionRouteEquals(existing, portal))
        {
          return DimensionOperationResult.Failed(
              "portal-update-not-supported",
              "Manifest preflight cannot move or reroute an existing portal definition.");
        }
      }

      if (string.IsNullOrEmpty(portal.FromDimensionId) ||
          string.IsNullOrEmpty(portal.ToDimensionId))
      {
        return DimensionOperationResult.Failed("portal-dimension-not-found", "Both portal dimensions must be registered or declared by this manifest.");
      }

      DimensionDefinition fromDimension;
      DimensionDefinition toDimension;
      if (!TryGetManifestAwareDimension(portal.FromDimensionId, manifestDimensionIds, manifestDimensions, out fromDimension) ||
          !TryGetManifestAwareDimension(portal.ToDimensionId, manifestDimensionIds, manifestDimensions, out toDimension))
      {
        return DimensionOperationResult.Failed("portal-dimension-not-found", "Both portal dimensions must be registered or declared by this manifest.");
      }

      if (!fromDimension.ContainsLocal(portal.FromLocalPosition) ||
          !toDimension.ContainsLocal(portal.ToLocalPosition))
      {
        return DimensionOperationResult.Failed("portal-position-out-of-bounds", "Portal positions must be inside their dimensions.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestPortalPresentation(
        DimensionPortalPresentationDefinition presentation,
        bool updateExisting,
        Dictionary<string, bool> manifestPortalIds,
        Dictionary<string, bool> manifestPresentationIds)
    {
      if (string.IsNullOrEmpty(presentation.PresentationId))
      {
        return DimensionOperationResult.Failed("portal-presentation-id-empty", "A portal presentation id is required.");
      }

      if (!TryAddManifestId(manifestPresentationIds, presentation.PresentationId))
      {
        return DimensionOperationResult.Failed("manifest-portal-presentation-duplicate", "The manifest contains the same portal presentation more than once.");
      }

      if (!updateExisting && portalPresentations.ContainsKey(presentation.PresentationId))
      {
        return DimensionOperationResult.Failed("portal-presentation-already-registered", "A portal presentation with that id is already registered.");
      }

      if (string.IsNullOrEmpty(presentation.PortalId))
      {
        return DimensionOperationResult.Failed("portal-presentation-portal-empty", "A portal id is required.");
      }

      if (!PortalKnownForManifest(presentation.PortalId, manifestPortalIds))
      {
        return DimensionOperationResult.Failed("portal-presentation-portal-not-found", "The portal presentation target portal is not registered or declared by this manifest.");
      }

      if (presentation.CooldownSeconds < 0f)
      {
        return DimensionOperationResult.Failed("portal-presentation-cooldown-invalid", "A portal presentation cooldown cannot be negative.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestProgressFlag(
        DimensionProgressFlag flag,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestFlagIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(flag.FlagId))
      {
        return DimensionOperationResult.Failed("progress-flag-id-empty", "A progress flag id is required.");
      }

      if (!TryAddManifestId(manifestFlagIds, flag.FlagId))
      {
        return DimensionOperationResult.Failed("manifest-progress-flag-duplicate", "The manifest contains the same progress flag more than once.");
      }

      if (!updateExisting && progressFlags.ContainsKey(flag.FlagId))
      {
        return DimensionOperationResult.Failed("progress-flag-already-registered", "A progress flag with that id is already registered.");
      }

      if (!string.IsNullOrEmpty(flag.DimensionId))
      {
        DimensionDefinition dimension;
        if (!TryGetManifestAwareDimension(flag.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
        {
          return DimensionOperationResult.Failed("progress-flag-dimension-not-found", "The progress flag dimension is not registered or declared by this manifest.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestTravelRequirement(
        DimensionTravelRequirementDefinition requirement,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestPortalIds,
        Dictionary<string, bool> manifestRequirementIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionPortalDefinition> manifestPortals,
        Dictionary<string, bool> manifestProgressFlagIds,
        List<DimensionProgressFlag> manifestProgressFlags)
    {
      if (string.IsNullOrEmpty(requirement.RequirementId))
      {
        return DimensionOperationResult.Failed("travel-requirement-id-empty", "A travel requirement id is required.");
      }

      if (!TryAddManifestId(manifestRequirementIds, requirement.RequirementId))
      {
        return DimensionOperationResult.Failed("manifest-travel-requirement-duplicate", "The manifest contains the same travel requirement more than once.");
      }

      if (!updateExisting && travelRequirements.ContainsKey(requirement.RequirementId))
      {
        return DimensionOperationResult.Failed("travel-requirement-already-registered", "A travel requirement with that id is already registered.");
      }

      if (string.IsNullOrEmpty(requirement.PortalId) &&
          string.IsNullOrEmpty(requirement.DimensionId))
      {
        return DimensionOperationResult.Failed("travel-requirement-target-empty", "A travel requirement needs a portal id, dimension id, or both.");
      }

      if (!IsValidTravelRequirementKind(requirement.Kind) ||
          requirement.Kind == DimensionTravelRequirementKind.Any)
      {
        return DimensionOperationResult.Failed("travel-requirement-kind-invalid", "The travel requirement kind is not supported.");
      }

      if (requirement.RequiredAmount < 0)
      {
        return DimensionOperationResult.Failed("travel-requirement-amount-invalid", "A travel requirement amount cannot be negative.");
      }

      if (RequirementKindNeedsSubject(requirement.Kind) &&
          string.IsNullOrEmpty(requirement.SubjectId))
      {
        return DimensionOperationResult.Failed("travel-requirement-subject-empty", "This travel requirement kind needs a subject id.");
      }

      DimensionPortalDefinition portal;
      bool hasPortal = false;
      if (!string.IsNullOrEmpty(requirement.PortalId))
      {
        if (!TryGetManifestAwarePortal(requirement.PortalId, manifestPortalIds, manifestPortals, out portal))
        {
          return DimensionOperationResult.Failed("travel-requirement-portal-not-found", "The travel requirement portal is not registered or declared by this manifest.");
        }

        hasPortal = true;
      }
      else
      {
        portal = default(DimensionPortalDefinition);
      }

      DimensionDefinition dimension;
      if (!string.IsNullOrEmpty(requirement.DimensionId) &&
          !TryGetManifestAwareDimension(requirement.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("travel-requirement-dimension-not-found", "The travel requirement dimension is not registered or declared by this manifest.");
      }

      if (hasPortal &&
          !string.IsNullOrEmpty(requirement.DimensionId) &&
          !string.Equals(portal.ToDimensionId, requirement.DimensionId, StringComparison.Ordinal))
      {
        return DimensionOperationResult.Failed("travel-requirement-portal-dimension-mismatch", "The travel requirement dimension must match the portal destination dimension.");
      }

      if (requirement.Kind == DimensionTravelRequirementKind.ProgressFlag &&
          !string.IsNullOrEmpty(requirement.SubjectId))
      {
        DimensionProgressFlag flag;
        if (TryGetManifestAwareProgressFlag(requirement.SubjectId, manifestProgressFlagIds, manifestProgressFlags, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.IsNullOrEmpty(requirement.DimensionId) &&
            !string.Equals(flag.DimensionId, requirement.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("travel-requirement-progress-flag-dimension-mismatch", "The travel requirement progress flag belongs to another dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestAssetReference(
        DimensionAssetReferenceDefinition assetReference,
        bool updateExisting,
        Dictionary<string, bool> manifestContentPackIds,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestAssetReferenceIds)
    {
      if (string.IsNullOrEmpty(assetReference.AssetId))
      {
        return DimensionOperationResult.Failed("asset-reference-id-empty", "An asset reference id is required.");
      }

      if (!TryAddManifestId(manifestAssetReferenceIds, assetReference.AssetId))
      {
        return DimensionOperationResult.Failed("manifest-asset-reference-duplicate", "The manifest contains the same asset reference more than once.");
      }

      if (!updateExisting && assetReferences.ContainsKey(assetReference.AssetId))
      {
        return DimensionOperationResult.Failed("asset-reference-already-registered", "An asset reference with that id is already registered.");
      }

      if (string.IsNullOrEmpty(assetReference.ContentPackId))
      {
        return DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
      }

      if (!ContentPackKnownForManifest(assetReference.ContentPackId, manifestContentPackIds))
      {
        return DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered or declared by this manifest.");
      }

      if (!IsValidAssetReferenceKind(assetReference.Kind))
      {
        return DimensionOperationResult.Failed("asset-reference-kind-invalid", "A valid asset reference kind is required.");
      }

      if (string.IsNullOrEmpty(assetReference.ResourceKey))
      {
        return DimensionOperationResult.Failed("asset-reference-resource-key-empty", "An asset reference resource key is required.");
      }

      if (!string.IsNullOrEmpty(assetReference.DimensionId) &&
          !DimensionKnownForManifest(assetReference.DimensionId, manifestDimensionIds))
      {
        return DimensionOperationResult.Failed("asset-reference-dimension-not-found", "No dimension with that id is registered.");
      }

      if (!string.IsNullOrEmpty(assetReference.ZoneId) &&
          !ZoneKnownForManifest(assetReference.ZoneId, manifestZoneIds))
      {
        return DimensionOperationResult.Failed("asset-reference-zone-not-found", "No zone with that id is registered.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestEnvironmentProfile(
        DimensionEnvironmentProfile profile,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        List<DimensionZoneDefinition> manifestZones,
        Dictionary<string, bool> manifestProfileIds)
    {
      if (string.IsNullOrEmpty(profile.ProfileId))
      {
        return DimensionOperationResult.Failed("environment-profile-id-empty", "An environment profile id is required.");
      }

      if (!TryAddManifestId(manifestProfileIds, profile.ProfileId))
      {
        return DimensionOperationResult.Failed("manifest-environment-profile-duplicate", "The manifest contains the same environment profile more than once.");
      }

      if (!updateExisting && environmentProfiles.ContainsKey(profile.ProfileId))
      {
        return DimensionOperationResult.Failed("environment-profile-already-registered", "An environment profile with that id is already registered.");
      }

      if (!DimensionKnownForManifest(profile.DimensionId, manifestDimensionIds))
      {
        return DimensionOperationResult.Failed("environment-profile-dimension-not-found", "The environment profile dimension is not registered.");
      }

      if (!string.IsNullOrEmpty(profile.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (TryGetManifestAwareZone(profile.ZoneId, manifestZoneIds, manifestZones, out zone) &&
            !string.Equals(zone.DimensionId, profile.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("environment-profile-zone-dimension-mismatch", "The environment profile zone belongs to another dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestBiome(
        DimensionBiomeDefinition biome,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestEnvironmentProfileIds,
        Dictionary<string, bool> manifestBiomeIds)
    {
      if (string.IsNullOrEmpty(biome.BiomeId))
      {
        return DimensionOperationResult.Failed("biome-id-empty", "A biome id is required.");
      }

      if (!TryAddManifestId(manifestBiomeIds, biome.BiomeId))
      {
        return DimensionOperationResult.Failed("manifest-biome-duplicate", "The manifest contains the same biome more than once.");
      }

      if (!updateExisting && biomes.ContainsKey(biome.BiomeId))
      {
        return DimensionOperationResult.Failed("biome-already-registered", "A biome with that id is already registered.");
      }

      if (!string.IsNullOrEmpty(biome.DimensionId) &&
          !DimensionKnownForManifest(biome.DimensionId, manifestDimensionIds))
      {
        return DimensionOperationResult.Failed("biome-dimension-not-found", "No dimension with that id is registered.");
      }

      if (!string.IsNullOrEmpty(biome.EnvironmentProfileId) &&
          !EnvironmentProfileKnownForManifest(biome.EnvironmentProfileId, manifestEnvironmentProfileIds))
      {
        return DimensionOperationResult.Failed("biome-environment-profile-not-found", "No environment profile with that id is registered or declared by this manifest.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestGenerationTable(
        DimensionGenerationTableDefinition table,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestBiomeIds,
        Dictionary<string, bool> manifestTableIds)
    {
      if (string.IsNullOrEmpty(table.TableId))
      {
        return DimensionOperationResult.Failed("generation-table-id-empty", "A generation table id is required.");
      }

      if (!TryAddManifestId(manifestTableIds, table.TableId))
      {
        return DimensionOperationResult.Failed("manifest-generation-table-duplicate", "The manifest contains the same generation table more than once.");
      }

      if (!updateExisting && generationTables.ContainsKey(table.TableId))
      {
        return DimensionOperationResult.Failed("generation-table-already-registered", "A generation table with that id is already registered.");
      }

      if (!IsValidGenerationTableKind(table.Kind))
      {
        return DimensionOperationResult.Failed("generation-table-kind-invalid", "A valid generation table kind is required.");
      }

      if (!string.IsNullOrEmpty(table.DimensionId) &&
          !DimensionKnownForManifest(table.DimensionId, manifestDimensionIds))
      {
        return DimensionOperationResult.Failed("generation-table-dimension-not-found", "No dimension with that id is registered.");
      }

      if (!string.IsNullOrEmpty(table.BiomeId) &&
          !BiomeKnownForManifest(table.BiomeId, manifestBiomeIds))
      {
        return DimensionOperationResult.Failed("generation-table-biome-not-found", "No biome with that id is registered or declared by this manifest.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestGenerationTableEntry(
        DimensionGenerationTableEntryDefinition entry,
        bool updateExisting,
        Dictionary<string, bool> manifestTableIds,
        Dictionary<string, bool> manifestEntryIds)
    {
      if (string.IsNullOrEmpty(entry.EntryId))
      {
        return DimensionOperationResult.Failed("generation-table-entry-id-empty", "A generation table entry id is required.");
      }

      if (!TryAddManifestId(manifestEntryIds, entry.EntryId))
      {
        return DimensionOperationResult.Failed("manifest-generation-table-entry-duplicate", "The manifest contains the same generation table entry more than once.");
      }

      if (!updateExisting && generationTableEntries.ContainsKey(entry.EntryId))
      {
        return DimensionOperationResult.Failed("generation-table-entry-already-registered", "A generation table entry with that id is already registered.");
      }

      if (string.IsNullOrEmpty(entry.TableId))
      {
        return DimensionOperationResult.Failed("generation-table-id-empty", "A generation table id is required.");
      }

      if (!GenerationTableKnownForManifest(entry.TableId, manifestTableIds))
      {
        return DimensionOperationResult.Failed("generation-table-not-found", "No generation table with that id is registered or declared by this manifest.");
      }

      if (string.IsNullOrEmpty(entry.SubjectId))
      {
        return DimensionOperationResult.Failed("generation-table-entry-subject-id-empty", "A generation table entry subject id is required.");
      }

      if (entry.Weight <= 0)
      {
        return DimensionOperationResult.Failed("generation-table-entry-weight-invalid", "A generation table entry weight must be greater than zero.");
      }

      if (entry.MinCount < 0 || entry.MaxCount < entry.MinCount)
      {
        return DimensionOperationResult.Failed("generation-table-entry-count-invalid", "A generation table entry count range is invalid.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestScene(
        DimensionSceneDefinition scene,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestSceneIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionSceneDefinition> manifestScenes)
    {
      if (string.IsNullOrEmpty(scene.SceneId))
      {
        return DimensionOperationResult.Failed("scene-id-empty", "A scene id is required.");
      }

      if (!TryAddManifestId(manifestSceneIds, scene.SceneId))
      {
        return DimensionOperationResult.Failed("manifest-scene-duplicate", "The manifest contains the same scene more than once.");
      }

      if (!updateExisting && scenes.ContainsKey(scene.SceneId))
      {
        return DimensionOperationResult.Failed("scene-already-registered", "A scene with that id is already registered.");
      }

      if (!IsValidSceneState(scene.State))
      {
        return DimensionOperationResult.Failed("scene-state-invalid", "The scene state is not supported.");
      }

      if (scene.LocalBounds.Size.x <= 0 || scene.LocalBounds.Size.y <= 0)
      {
        return DimensionOperationResult.Failed("scene-bounds-invalid", "The scene local bounds must have a positive size.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(scene.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("scene-dimension-not-found", "The scene dimension is not registered or declared by this manifest.");
      }

      if (!dimension.LocalBounds.Contains(scene.LocalBounds.Min) ||
          !dimension.LocalBounds.Contains(scene.LocalBounds.MaxExclusive - new int2(1, 1)))
      {
        return DimensionOperationResult.Failed("scene-bounds-out-of-dimension", "The scene bounds are outside the scene dimension.");
      }

      if (SceneBlocksOverlap(scene.State))
      {
        DimensionSceneDefinition overlap;
        string allowedExistingSceneId = updateExisting && scenes.ContainsKey(scene.SceneId)
            ? scene.SceneId
            : string.Empty;
        if (TryFindOverlappingScene(scene, allowedExistingSceneId, out overlap))
        {
          return DimensionOperationResult.Failed(
              "scene-bounds-overlap",
              "The scene bounds overlap an existing scene reservation: " + overlap.SceneId + ".");
        }

        for (int i = 0; i < manifestScenes.Count; i++)
        {
          DimensionSceneDefinition manifestScene = manifestScenes[i];
          if (!SceneBlocksOverlap(manifestScene.State) ||
              !string.Equals(manifestScene.DimensionId, scene.DimensionId, StringComparison.Ordinal))
          {
            continue;
          }

          if (BoundsOverlap(manifestScene.LocalBounds, scene.LocalBounds))
          {
            return DimensionOperationResult.Failed(
                "scene-bounds-overlap",
                "The scene bounds overlap a scene reservation declared earlier in this manifest: " + manifestScene.SceneId + ".");
          }
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestSceneTemplate(
        DimensionSceneTemplateDefinition template,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestTemplateIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones)
    {
      if (string.IsNullOrEmpty(template.TemplateId))
      {
        return DimensionOperationResult.Failed("scene-template-id-empty", "A scene template id is required.");
      }

      if (!TryAddManifestId(manifestTemplateIds, template.TemplateId))
      {
        return DimensionOperationResult.Failed("manifest-scene-template-duplicate", "The manifest contains the same scene template more than once.");
      }

      if (!updateExisting && sceneTemplates.ContainsKey(template.TemplateId))
      {
        return DimensionOperationResult.Failed("scene-template-already-registered", "A scene template with that id is already registered.");
      }

      if (string.IsNullOrEmpty(template.Kind))
      {
        return DimensionOperationResult.Failed("scene-template-kind-empty", "A scene template kind is required.");
      }

      if (template.FootprintSize.x <= 0 || template.FootprintSize.y <= 0)
      {
        return DimensionOperationResult.Failed("scene-template-footprint-invalid", "A scene template footprint must have a positive size.");
      }

      if (template.Weight <= 0)
      {
        return DimensionOperationResult.Failed("scene-template-weight-invalid", "A scene template weight must be greater than zero.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(template.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("scene-template-dimension-not-found", "The scene template dimension is not registered or declared by this manifest.");
      }

      if (template.FootprintSize.x > dimension.LocalBounds.Size.x ||
          template.FootprintSize.y > dimension.LocalBounds.Size.y)
      {
        return DimensionOperationResult.Failed("scene-template-footprint-too-large", "The scene template footprint is larger than the target dimension.");
      }

      if (!string.IsNullOrEmpty(template.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (TryGetManifestAwareZone(template.ZoneId, manifestZoneIds, manifestZones, out zone) &&
            !string.Equals(zone.DimensionId, template.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("scene-template-zone-dimension-mismatch", "The scene template zone belongs to another dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestEncounter(
        DimensionEncounterDefinition encounter,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestSceneIds,
        Dictionary<string, bool> manifestSpawnRuleIds,
        Dictionary<string, bool> manifestProgressFlagIds,
        Dictionary<string, bool> manifestMarkerIds,
        Dictionary<string, bool> manifestEncounterIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones,
        List<DimensionSceneDefinition> manifestScenes,
        List<DimensionSpawnRule> manifestSpawnRules,
        List<DimensionProgressFlag> manifestProgressFlags,
        List<DimensionMapMarker> manifestMarkers)
    {
      if (string.IsNullOrEmpty(encounter.EncounterId))
      {
        return DimensionOperationResult.Failed("encounter-id-empty", "An encounter id is required.");
      }

      if (!TryAddManifestId(manifestEncounterIds, encounter.EncounterId))
      {
        return DimensionOperationResult.Failed("manifest-encounter-duplicate", "The manifest contains the same encounter more than once.");
      }

      if (!updateExisting && encounters.ContainsKey(encounter.EncounterId))
      {
        return DimensionOperationResult.Failed("encounter-already-registered", "An encounter with that id is already registered.");
      }

      if (!IsValidEncounterKind(encounter.Kind) || encounter.Kind == DimensionEncounterKind.Any)
      {
        return DimensionOperationResult.Failed("encounter-kind-invalid", "The encounter kind is not supported.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(encounter.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("encounter-dimension-not-found", "The encounter dimension is not registered or declared by this manifest.");
      }

      if (!string.IsNullOrEmpty(encounter.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (TryGetManifestAwareZone(encounter.ZoneId, manifestZoneIds, manifestZones, out zone) &&
            !string.Equals(zone.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("encounter-zone-dimension-mismatch", "The encounter zone belongs to another dimension.");
        }
      }

      if (!string.IsNullOrEmpty(encounter.SceneId))
      {
        DimensionSceneDefinition scene;
        if (TryGetManifestAwareScene(encounter.SceneId, manifestSceneIds, manifestScenes, out scene) &&
            !string.Equals(scene.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("encounter-scene-dimension-mismatch", "The encounter scene belongs to another dimension.");
        }
      }

      if (!string.IsNullOrEmpty(encounter.SpawnRuleId))
      {
        DimensionSpawnRule spawnRule;
        if (TryGetManifestAwareSpawnRule(encounter.SpawnRuleId, manifestSpawnRuleIds, manifestSpawnRules, out spawnRule) &&
            !string.Equals(spawnRule.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("encounter-spawn-rule-dimension-mismatch", "The encounter spawn rule belongs to another dimension.");
        }
      }

      if (!string.IsNullOrEmpty(encounter.MarkerId))
      {
        DimensionMapMarker marker;
        if (TryGetManifestAwareMarker(encounter.MarkerId, manifestMarkerIds, manifestMarkers, out marker) &&
            !string.Equals(marker.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("encounter-marker-dimension-mismatch", "The encounter marker belongs to another dimension.");
        }
      }

      if (!string.IsNullOrEmpty(encounter.DefeatFlagId))
      {
        DimensionProgressFlag flag;
        if (TryGetManifestAwareProgressFlag(encounter.DefeatFlagId, manifestProgressFlagIds, manifestProgressFlags, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.Equals(flag.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("encounter-defeat-flag-dimension-mismatch", "The encounter defeat flag belongs to another dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestSpawnRule(
        DimensionSpawnRule rule,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestRuleIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones)
    {
      if (string.IsNullOrEmpty(rule.RuleId))
      {
        return DimensionOperationResult.Failed("spawn-rule-id-empty", "A spawn rule id is required.");
      }

      if (!TryAddManifestId(manifestRuleIds, rule.RuleId))
      {
        return DimensionOperationResult.Failed("manifest-spawn-rule-duplicate", "The manifest contains the same spawn rule more than once.");
      }

      if (!updateExisting && spawnRules.ContainsKey(rule.RuleId))
      {
        return DimensionOperationResult.Failed("spawn-rule-already-registered", "A spawn rule with that id is already registered.");
      }

      if (string.IsNullOrEmpty(rule.SubjectId))
      {
        return DimensionOperationResult.Failed("spawn-rule-subject-empty", "A spawn rule subject id is required.");
      }

      if (!IsValidSpawnSubjectKind(rule.SubjectKind) || rule.SubjectKind == DimensionSpawnSubjectKind.Any)
      {
        return DimensionOperationResult.Failed("spawn-rule-kind-invalid", "The spawn rule subject kind is not supported.");
      }

      if (rule.Weight <= 0)
      {
        return DimensionOperationResult.Failed("spawn-rule-weight-invalid", "The spawn rule weight must be greater than zero.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(rule.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("spawn-rule-dimension-not-found", "The spawn rule dimension is not registered or declared by this manifest.");
      }

      if (!string.IsNullOrEmpty(rule.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (TryGetManifestAwareZone(rule.ZoneId, manifestZoneIds, manifestZones, out zone) &&
            !string.Equals(zone.DimensionId, rule.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("spawn-rule-zone-dimension-mismatch", "The spawn rule zone belongs to another dimension.");
        }
      }

      if (rule.HasLocalBounds)
      {
        if (rule.LocalBounds.Size.x <= 0 || rule.LocalBounds.Size.y <= 0)
        {
          return DimensionOperationResult.Failed("spawn-rule-bounds-invalid", "The spawn rule local bounds must have a positive size.");
        }

        if (!dimension.LocalBounds.Contains(rule.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(rule.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          return DimensionOperationResult.Failed("spawn-rule-bounds-out-of-dimension", "The spawn rule bounds are outside the spawn rule dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestResourceNode(
        DimensionResourceNodeDefinition node,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestGenerationPassIds,
        Dictionary<string, bool> manifestNodeIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones,
        List<DimensionGenerationPassDefinition> manifestGenerationPasses)
    {
      if (string.IsNullOrEmpty(node.NodeId))
      {
        return DimensionOperationResult.Failed("resource-node-id-empty", "A resource node id is required.");
      }

      if (!TryAddManifestId(manifestNodeIds, node.NodeId))
      {
        return DimensionOperationResult.Failed("manifest-resource-node-duplicate", "The manifest contains the same resource node more than once.");
      }

      if (!updateExisting && resourceNodes.ContainsKey(node.NodeId))
      {
        return DimensionOperationResult.Failed("resource-node-already-registered", "A resource node with that id is already registered.");
      }

      if (string.IsNullOrEmpty(node.ResourceId))
      {
        return DimensionOperationResult.Failed("resource-node-resource-empty", "A resource id is required.");
      }

      if (!IsValidResourceNodeKind(node.Kind) || node.Kind == DimensionResourceNodeKind.Any)
      {
        return DimensionOperationResult.Failed("resource-node-kind-invalid", "The resource node kind is not supported.");
      }

      if (node.Weight <= 0)
      {
        return DimensionOperationResult.Failed("resource-node-weight-invalid", "The resource node weight must be greater than zero.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(node.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("resource-node-dimension-not-found", "The resource node dimension is not registered or declared by this manifest.");
      }

      if (!string.IsNullOrEmpty(node.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (TryGetManifestAwareZone(node.ZoneId, manifestZoneIds, manifestZones, out zone) &&
            !string.Equals(zone.DimensionId, node.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("resource-node-zone-dimension-mismatch", "The resource node zone belongs to another dimension.");
        }
      }

      if (!string.IsNullOrEmpty(node.GenerationPassId))
      {
        DimensionGenerationPassDefinition generationPass;
        if (TryGetManifestAwareGenerationPass(node.GenerationPassId, manifestGenerationPassIds, manifestGenerationPasses, out generationPass) &&
            !string.Equals(generationPass.DimensionId, node.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("resource-node-pass-dimension-mismatch", "The resource node generation pass belongs to another dimension.");
        }
      }

      if (node.HasLocalBounds)
      {
        if (node.LocalBounds.Size.x <= 0 || node.LocalBounds.Size.y <= 0)
        {
          return DimensionOperationResult.Failed("resource-node-bounds-invalid", "The resource node local bounds must have a positive size.");
        }

        if (!dimension.LocalBounds.Contains(node.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(node.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          return DimensionOperationResult.Failed("resource-node-bounds-out-of-dimension", "The resource node bounds are outside the resource node dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestGenerationPass(
        DimensionGenerationPassDefinition generationPass,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestPassIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones)
    {
      if (string.IsNullOrEmpty(generationPass.PassId))
      {
        return DimensionOperationResult.Failed("generation-pass-id-empty", "A generation pass id is required.");
      }

      if (!TryAddManifestId(manifestPassIds, generationPass.PassId))
      {
        return DimensionOperationResult.Failed("manifest-generation-pass-duplicate", "The manifest contains the same generation pass more than once.");
      }

      if (!updateExisting && generationPasses.ContainsKey(generationPass.PassId))
      {
        return DimensionOperationResult.Failed("generation-pass-already-registered", "A generation pass with that id is already registered.");
      }

      if (!IsValidGenerationPassPhase(generationPass.Phase))
      {
        return DimensionOperationResult.Failed("generation-pass-phase-invalid", "The generation pass phase is not supported.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(generationPass.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("generation-pass-dimension-not-found", "The generation pass dimension is not registered or declared by this manifest.");
      }

      if (!string.IsNullOrEmpty(generationPass.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (TryGetManifestAwareZone(generationPass.ZoneId, manifestZoneIds, manifestZones, out zone) &&
            !string.Equals(zone.DimensionId, generationPass.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("generation-pass-zone-dimension-mismatch", "The generation pass zone belongs to another dimension.");
        }
      }

      if (generationPass.HasLocalBounds)
      {
        if (generationPass.LocalBounds.Size.x <= 0 || generationPass.LocalBounds.Size.y <= 0)
        {
          return DimensionOperationResult.Failed("generation-pass-bounds-invalid", "The generation pass local bounds must have a positive size.");
        }

        if (!dimension.LocalBounds.Contains(generationPass.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(generationPass.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          return DimensionOperationResult.Failed("generation-pass-bounds-out-of-dimension", "The generation pass bounds are outside the generation pass dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestWorldEvent(
        DimensionWorldEventDefinition worldEvent,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestProgressFlagIds,
        Dictionary<string, bool> manifestWorldEventIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones,
        List<DimensionProgressFlag> manifestProgressFlags)
    {
      if (string.IsNullOrEmpty(worldEvent.EventId))
      {
        return DimensionOperationResult.Failed("world-event-id-empty", "A world event id is required.");
      }

      if (!TryAddManifestId(manifestWorldEventIds, worldEvent.EventId))
      {
        return DimensionOperationResult.Failed("manifest-world-event-duplicate", "The manifest contains the same world event more than once.");
      }

      if (!updateExisting && worldEvents.ContainsKey(worldEvent.EventId))
      {
        return DimensionOperationResult.Failed("world-event-already-registered", "A world event with that id is already registered.");
      }

      if (!IsValidWorldEventKind(worldEvent.Kind) || worldEvent.Kind == DimensionWorldEventKind.Any)
      {
        return DimensionOperationResult.Failed("world-event-kind-invalid", "The world event kind is not supported.");
      }

      if (worldEvent.Weight <= 0)
      {
        return DimensionOperationResult.Failed("world-event-weight-invalid", "The world event weight must be greater than zero.");
      }

      if (worldEvent.CooldownSeconds < 0f)
      {
        return DimensionOperationResult.Failed("world-event-cooldown-invalid", "A world event cooldown cannot be negative.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(worldEvent.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("world-event-dimension-not-found", "The world event dimension is not registered or declared by this manifest.");
      }

      if (!string.IsNullOrEmpty(worldEvent.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (TryGetManifestAwareZone(worldEvent.ZoneId, manifestZoneIds, manifestZones, out zone) &&
            !string.Equals(zone.DimensionId, worldEvent.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("world-event-zone-dimension-mismatch", "The world event zone belongs to another dimension.");
        }
      }

      if (!string.IsNullOrEmpty(worldEvent.ProgressFlagId))
      {
        DimensionProgressFlag flag;
        if (TryGetManifestAwareProgressFlag(worldEvent.ProgressFlagId, manifestProgressFlagIds, manifestProgressFlags, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.Equals(flag.DimensionId, worldEvent.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("world-event-progress-flag-dimension-mismatch", "The world event progress flag belongs to another dimension.");
        }
      }

      if (worldEvent.HasLocalBounds)
      {
        if (worldEvent.LocalBounds.Size.x <= 0 || worldEvent.LocalBounds.Size.y <= 0)
        {
          return DimensionOperationResult.Failed("world-event-bounds-invalid", "The world event local bounds must have a positive size.");
        }

        if (!dimension.LocalBounds.Contains(worldEvent.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(worldEvent.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          return DimensionOperationResult.Failed("world-event-bounds-out-of-dimension", "The world event bounds are outside the world event dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestContentOwnership(
        DimensionContentOwnershipBinding binding,
        bool requireExistingRecord,
        ManifestValidationContext manifestContext)
    {
      if (string.IsNullOrEmpty(binding.ContentPackId))
      {
        return DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
      }

      if (!ContentPackKnownForManifest(binding.ContentPackId, manifestContext.ContentPackIds))
      {
        return DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered or declared by this manifest.");
      }

      if (!IsValidContentRecordKind(binding.RecordKind))
      {
        return DimensionOperationResult.Failed("content-record-kind-invalid", "A valid content record kind is required.");
      }

      if (string.IsNullOrEmpty(binding.RecordId))
      {
        return DimensionOperationResult.Failed("content-record-id-empty", "A content record id is required.");
      }

      string key = BuildContentOwnershipKey(binding.RecordKind, binding.RecordId);
      if (!TryAddManifestId(manifestContext.OwnershipKeys, key))
      {
        return DimensionOperationResult.Failed("manifest-content-ownership-duplicate", "The manifest contains the same ownership binding more than once.");
      }

      if (requireExistingRecord &&
          !ContentRecordKnownForManifest(
              binding.RecordKind,
              binding.RecordId,
              manifestContext))
      {
        return DimensionOperationResult.Failed("content-record-not-found", "No content record with that kind and id is registered or declared by this manifest.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestStarterDefinition(
        DimensionStarterDefinition starter,
        bool updateExisting,
        ManifestValidationContext manifestContext)
    {
      if (string.IsNullOrEmpty(starter.StarterId))
      {
        return DimensionOperationResult.Failed("starter-id-empty", "A starter id is required.");
      }

      if (!TryAddManifestId(manifestContext.StarterIds, starter.StarterId))
      {
        return DimensionOperationResult.Failed("manifest-starter-duplicate", "The manifest contains the same dimension starter more than once.");
      }

      if (!updateExisting && starters.ContainsKey(starter.StarterId))
      {
        return DimensionOperationResult.Failed("starter-already-registered", "A dimension starter with that id is already registered.");
      }

      if (string.IsNullOrEmpty(starter.ContentPackId) ||
          !ContentPackKnownForManifest(starter.ContentPackId, manifestContext.ContentPackIds))
      {
        return DimensionOperationResult.Failed("starter-content-pack-not-found", "The starter content pack is not registered or declared by this manifest.");
      }

      if (!DimensionKnownForManifest(starter.DimensionId, manifestContext.DimensionIds))
      {
        return DimensionOperationResult.Failed("starter-dimension-not-found", "The starter dimension is not registered or declared by this manifest.");
      }

      if (!string.Equals(
              starter.GenerationRequest.DimensionId,
              starter.DimensionId,
              StringComparison.Ordinal))
      {
        return DimensionOperationResult.Failed(
            "starter-generation-dimension-mismatch",
            "The starter generation request must target the starter dimension.");
      }

      string boundsError;
      if (!IsValidGenerationBounds(starter.GenerationRequest.LocalBounds, out boundsError))
      {
        return DimensionOperationResult.Failed("starter-generation-bounds-invalid", boundsError);
      }

      DimensionOperationResult readinessResult =
          ValidateManifestStarterReadinessRequest(starter.ContentReadinessRequest, manifestContext);
      if (!readinessResult.Success)
      {
        return readinessResult;
      }

      DimensionOperationResult travelLoopResult =
          ValidateManifestStarterTravelLoop(starter.TravelLoopPreflightRequest, manifestContext);
      if (!travelLoopResult.Success)
      {
        return travelLoopResult;
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestStarterReadinessRequest(
        DimensionContentReadinessRequest request,
        ManifestValidationContext manifestContext)
    {
      IReadOnlyList<DimensionContentReadinessRequirement> requirements =
          request.Requirements ?? new List<DimensionContentReadinessRequirement>();
      for (int i = 0; i < requirements.Count; i++)
      {
        DimensionContentReadinessRequirement requirement = requirements[i];
        if (!IsValidContentRecordKind(requirement.RecordKind))
        {
          return DimensionOperationResult.Failed(
              "starter-readiness-record-kind-invalid",
              "A starter readiness requirement has an invalid content record kind.");
        }

        if (string.IsNullOrEmpty(requirement.RecordId))
        {
          return DimensionOperationResult.Failed(
              "starter-readiness-record-id-empty",
              "A starter readiness requirement has an empty content record id.");
        }

        if (!ContentRecordKnownForManifest(
                requirement.RecordKind,
                requirement.RecordId,
                manifestContext))
        {
          return DimensionOperationResult.Failed(
              "starter-readiness-record-not-found",
              "A starter readiness requirement references a content record that is not registered or declared by this manifest.");
        }

        if (requirement.RequireOwnership &&
            !string.IsNullOrEmpty(requirement.RequiredOwnerContentPackId) &&
            !ContentPackKnownForManifest(requirement.RequiredOwnerContentPackId, manifestContext.ContentPackIds))
        {
          return DimensionOperationResult.Failed(
              "starter-readiness-owner-not-found",
              "A starter readiness requirement references an owner content pack that is not registered or declared by this manifest.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestStarterTravelLoop(
        DimensionTravelLoopPreflightRequest request,
        ManifestValidationContext manifestContext)
    {
      if (!DimensionKnownForManifest(request.SourceDimensionId, manifestContext.DimensionIds))
      {
        return DimensionOperationResult.Failed(
            "starter-travel-source-dimension-not-found",
            "The starter travel source dimension is not registered or declared by this manifest.");
      }

      if (!DimensionKnownForManifest(request.TargetDimensionId, manifestContext.DimensionIds))
      {
        return DimensionOperationResult.Failed(
            "starter-travel-target-dimension-not-found",
            "The starter travel target dimension is not registered or declared by this manifest.");
      }

      if (string.IsNullOrEmpty(request.EntryPortalId) ||
          !ContentRecordKnownForManifest(DimensionContentRecordKind.Portal, request.EntryPortalId, manifestContext))
      {
        return DimensionOperationResult.Failed(
            "starter-travel-entry-portal-not-found",
            "The starter travel entry portal is not registered or declared by this manifest.");
      }

      if ((request.RequireReturnPortal || !string.IsNullOrEmpty(request.ReturnPortalId)) &&
          !ContentRecordKnownForManifest(DimensionContentRecordKind.Portal, request.ReturnPortalId, manifestContext))
      {
        return DimensionOperationResult.Failed(
            "starter-travel-return-portal-not-found",
            "The starter travel return portal is not registered or declared by this manifest.");
      }

      if ((request.RequireSourceAnchor || !string.IsNullOrEmpty(request.SourceAnchorId)) &&
          !ContentRecordKnownForManifest(DimensionContentRecordKind.Anchor, request.SourceAnchorId, manifestContext))
      {
        return DimensionOperationResult.Failed(
            "starter-travel-source-anchor-not-found",
            "The starter travel source anchor is not registered or declared by this manifest.");
      }

      if ((request.RequireTargetAnchor || !string.IsNullOrEmpty(request.TargetAnchorId)) &&
          !ContentRecordKnownForManifest(DimensionContentRecordKind.Anchor, request.TargetAnchorId, manifestContext))
      {
        return DimensionOperationResult.Failed(
            "starter-travel-target-anchor-not-found",
            "The starter travel target anchor is not registered or declared by this manifest.");
      }

      if (request.RequireMarkers)
      {
        if (!ContentRecordKnownForManifest(DimensionContentRecordKind.MapMarker, request.SourceMarkerId, manifestContext) ||
            !ContentRecordKnownForManifest(DimensionContentRecordKind.MapMarker, request.TargetMarkerId, manifestContext))
        {
          return DimensionOperationResult.Failed(
              "starter-travel-marker-not-found",
              "The starter travel markers are not registered or declared by this manifest.");
        }
      }

      if (request.RequireTargetAreaReady)
      {
        string boundsError;
        if (!IsValidGenerationBounds(request.TargetLandingBounds, out boundsError))
        {
          return DimensionOperationResult.Failed("starter-travel-landing-bounds-invalid", boundsError);
        }
      }

      return DimensionOperationResult.Ok();
    }

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

    private bool EnvironmentProfileKnownForManifest(
        string profileId,
        Dictionary<string, bool> manifestEnvironmentProfileIds)
    {
      if (string.IsNullOrEmpty(profileId))
      {
        return false;
      }

      return environmentProfiles.ContainsKey(profileId) ||
             manifestEnvironmentProfileIds.ContainsKey(profileId);
    }

    private bool BiomeKnownForManifest(
        string biomeId,
        Dictionary<string, bool> manifestBiomeIds)
    {
      if (string.IsNullOrEmpty(biomeId))
      {
        return false;
      }

      return biomes.ContainsKey(biomeId) ||
             manifestBiomeIds.ContainsKey(biomeId);
    }

    private bool GenerationTableKnownForManifest(
        string tableId,
        Dictionary<string, bool> manifestTableIds)
    {
      if (string.IsNullOrEmpty(tableId))
      {
        return false;
      }

      return generationTables.ContainsKey(tableId) ||
             manifestTableIds.ContainsKey(tableId);
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

    private bool TryGetManifestAwareSpawnRule(
        string ruleId,
        Dictionary<string, bool> manifestRuleIds,
        List<DimensionSpawnRule> manifestRules,
        out DimensionSpawnRule rule)
    {
      if (string.IsNullOrEmpty(ruleId))
      {
        rule = default(DimensionSpawnRule);
        return false;
      }

      if (spawnRules.TryGetValue(ruleId, out rule))
      {
        return true;
      }

      if (!manifestRuleIds.ContainsKey(ruleId))
      {
        rule = default(DimensionSpawnRule);
        return false;
      }

      for (int i = 0; i < manifestRules.Count; i++)
      {
        if (string.Equals(manifestRules[i].RuleId, ruleId, StringComparison.Ordinal))
        {
          rule = manifestRules[i];
          return true;
        }
      }

      rule = default(DimensionSpawnRule);
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

    private bool TryGetManifestAwareGenerationPass(
        string passId,
        Dictionary<string, bool> manifestPassIds,
        List<DimensionGenerationPassDefinition> manifestPasses,
        out DimensionGenerationPassDefinition generationPass)
    {
      if (string.IsNullOrEmpty(passId))
      {
        generationPass = default(DimensionGenerationPassDefinition);
        return false;
      }

      if (generationPasses.TryGetValue(passId, out generationPass))
      {
        return true;
      }

      if (!manifestPassIds.ContainsKey(passId))
      {
        generationPass = default(DimensionGenerationPassDefinition);
        return false;
      }

      for (int i = 0; i < manifestPasses.Count; i++)
      {
        if (string.Equals(manifestPasses[i].PassId, passId, StringComparison.Ordinal))
        {
          generationPass = manifestPasses[i];
          return true;
        }
      }

      generationPass = default(DimensionGenerationPassDefinition);
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
        case DimensionContentRecordKind.SpawnRule:
          return manifestContext.SpawnRuleIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Encounter:
          return manifestContext.EncounterIds.ContainsKey(recordId);
        case DimensionContentRecordKind.ResourceNode:
          return manifestContext.ResourceNodeIds.ContainsKey(recordId);
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
        case DimensionContentRecordKind.EnvironmentProfile:
          return manifestContext.EnvironmentProfileIds.ContainsKey(recordId);
        case DimensionContentRecordKind.Biome:
          return manifestContext.BiomeIds.ContainsKey(recordId);
        case DimensionContentRecordKind.GenerationTable:
          return manifestContext.TableIds.ContainsKey(recordId);
        case DimensionContentRecordKind.GenerationTableEntry:
          return manifestContext.EntryIds.ContainsKey(recordId);
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
