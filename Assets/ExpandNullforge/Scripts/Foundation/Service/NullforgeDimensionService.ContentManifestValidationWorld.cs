using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Checking the world side of a manifest: biomes, scenes, encounters, starters.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    private DimensionOperationResult ValidateManifestBiome(
        DimensionBiomeDefinition biome,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
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
        Dictionary<string, bool> manifestProgressFlagIds,
        Dictionary<string, bool> manifestMarkerIds,
        Dictionary<string, bool> manifestEncounterIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionZoneDefinition> manifestZones,
        List<DimensionSceneDefinition> manifestScenes,
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
  }
}
