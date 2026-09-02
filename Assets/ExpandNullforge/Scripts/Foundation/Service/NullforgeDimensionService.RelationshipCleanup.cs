using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    // THE ONLY PROTECTED ID THE FRAMEWORK HAS. There were five of these predicates. Four —
    // marker, anchor, portal, starter — returned false, and eight branches consulted them and
    // produced a "…-protected" refusal that could never fire. Whoever read one of those branches
    // came away believing the built-ins were safe. They were not: any mod, and any bug here, could
    // remove them and be told it worked.
    //
    // The four are gone, and so are their eight branches, because there is nothing left for them
    // to protect. The framework ships no built-in portal, marker, anchor or starter — the content
    // that had them was amputated, and BuiltIns.cs declares no id of those four kinds. A predicate
    // that guards nothing and says it guards something is worse than the absence of one.
    //
    // If the framework ever ships built-in content of one of those kinds again, this method is the
    // template: name the id as a constant in BuiltIns.cs, write the one-line string.Equals here,
    // and put the branch back at the two places that kind is mutated — the manifest validation and
    // the direct register/remove path. Both are named in Docs/protected-ids-decision.md.
    private bool IsProtectedMapLayerId(string layerId)
    {
      return string.Equals(layerId, BuiltInOverworldMapLayerId, StringComparison.Ordinal);
    }

    private bool IsValidAnchorKind(DimensionAnchorKind kind)
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

    private bool IsValidSceneState(DimensionSceneState state)
    {
      return state == DimensionSceneState.Planned ||
             state == DimensionSceneState.Reserved ||
             state == DimensionSceneState.Generating ||
             state == DimensionSceneState.Ready ||
             state == DimensionSceneState.Disabled ||
             state == DimensionSceneState.Error;
    }

    private bool IsProtectedGeneratedArea(string dimensionId, DimensionBounds localBounds)
    {
      foreach (DimensionStarterDefinition starter in starters.Values)
      {
        if (!starter.Enabled)
        {
          continue;
        }

        DimensionStarterGenerationRequest request = starter.GenerationRequest;
        if (string.Equals(request.DimensionId, dimensionId, StringComparison.Ordinal) &&
            BoundsEqual(request.LocalBounds, localBounds))
        {
          return true;
        }
      }

      return false;
    }

    private void RefreshStarterLifecycleForGeneratedArea(
        string dimensionId,
        DimensionBounds localBounds,
        string reason)
    {
      foreach (DimensionStarterDefinition starter in starters.Values)
      {
        if (!starter.Enabled)
        {
          continue;
        }

        DimensionStarterGenerationRequest request = starter.GenerationRequest;
        if (!string.Equals(request.DimensionId, dimensionId, StringComparison.Ordinal) ||
            !BoundsEqual(request.LocalBounds, localBounds))
        {
          continue;
        }

        RefreshStarterDimensionLifecycle(starter, reason);
      }
    }

    private void RefreshStarterDimensionLifecycle(
        DimensionStarterDefinition starter,
        string reason)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(starter.DimensionId, out definition))
      {
        return;
      }

      DimensionGenerationStatus status;
      if (!TryGetGenerationStatus(
              starter.GenerationRequest.DimensionId,
              starter.GenerationRequest.LocalBounds,
              out status))
      {
        return;
      }

      DimensionLifecycleState targetState = DimensionLifecycleState.Registered;
      if (status.State == DimensionGenerationState.Ready)
      {
        targetState = DimensionLifecycleState.Ready;
      }
      else if (status.State == DimensionGenerationState.Failed)
      {
        targetState = DimensionLifecycleState.Error;
      }
      else if (IsTransientGenerationState(status.State))
      {
        targetState = DimensionLifecycleState.Loading;
      }

      DimensionOperationResult ignored;
      SetDimensionLifecycleInternal(
          definition,
          definition.LifecycleState,
          targetState,
          string.IsNullOrEmpty(reason)
              ? "starter-lifecycle-refresh"
              : reason,
          out ignored);
    }

    private bool OverlapsExistingNonOverworldDimension(DimensionDefinition definition)
    {
      DimensionBounds bounds = definition.AbsoluteBounds;
      foreach (DimensionDefinition existing in definitions.Values)
      {
        if (existing.Id == DimensionIds.Overworld)
        {
          continue;
        }

        if (BoundsOverlap(bounds, existing.AbsoluteBounds))
        {
          return true;
        }
      }

      return false;
    }

    private void RemovePortalsForDimension(string dimensionId)
    {
      List<string> portalIds = new List<string>();
      foreach (DimensionPortalDefinition portal in portals.Values)
      {
        if (string.Equals(portal.FromDimensionId, dimensionId, StringComparison.Ordinal)
            || string.Equals(portal.ToDimensionId, dimensionId, StringComparison.Ordinal))
        {
          portalIds.Add(portal.PortalId);
        }
      }

      for (int i = 0; i < portalIds.Count; i++)
      {
        portals.Remove(portalIds[i]);
        RemovePortalPresentationsForPortal(portalIds[i]);
        RemoveTravelRequirementsForPortal(portalIds[i]);
        RemovePersistedPortalIfWorldRegistryLoaded(portalIds[i]);
      }
    }

    private void RemovePortalPresentationsForPortal(string portalId)
    {
      List<string> presentationIds = new List<string>();
      foreach (DimensionPortalPresentationDefinition presentation in portalPresentations.Values)
      {
        if (string.Equals(presentation.PortalId, portalId, StringComparison.Ordinal))
        {
          presentationIds.Add(presentation.PresentationId);
        }
      }

      for (int i = 0; i < presentationIds.Count; i++)
      {
        DimensionPortalPresentationDefinition presentation;
        if (portalPresentations.TryGetValue(presentationIds[i], out presentation))
        {
          portalPresentations.Remove(presentationIds[i]);
          RaisePortalPresentationChanged(
              presentation,
              DimensionPortalPresentationChangeKind.Removed,
              presentation.Enabled,
              false,
              "portal-removed");
        }
      }
    }

    private void RemoveTravelRequirementsForPortal(string portalId)
    {
      List<string> requirementIds = new List<string>();
      foreach (DimensionTravelRequirementDefinition requirement in travelRequirements.Values)
      {
        if (string.Equals(requirement.PortalId, portalId, StringComparison.Ordinal))
        {
          requirementIds.Add(requirement.RequirementId);
        }
      }

      for (int i = 0; i < requirementIds.Count; i++)
      {
        DimensionTravelRequirementDefinition requirement;
        if (travelRequirements.TryGetValue(requirementIds[i], out requirement))
        {
          travelRequirements.Remove(requirementIds[i]);
          RaiseTravelRequirementChanged(
              requirement,
              DimensionTravelRequirementChangeKind.Removed,
              requirement.Enabled,
              false,
              "portal-removed");
        }
      }
    }

    private void RemoveTravelRequirementsForDimension(string dimensionId)
    {
      List<string> requirementIds = new List<string>();
      foreach (DimensionTravelRequirementDefinition requirement in travelRequirements.Values)
      {
        if (string.Equals(requirement.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          requirementIds.Add(requirement.RequirementId);
        }
      }

      for (int i = 0; i < requirementIds.Count; i++)
      {
        DimensionTravelRequirementDefinition requirement;
        if (travelRequirements.TryGetValue(requirementIds[i], out requirement))
        {
          travelRequirements.Remove(requirementIds[i]);
          RaiseTravelRequirementChanged(
              requirement,
              DimensionTravelRequirementChangeKind.Removed,
              requirement.Enabled,
              false,
              "dimension-removed");
        }
      }
    }

    private void RemoveMapLayersForDimension(string dimensionId)
    {
      List<string> layerIds = new List<string>();
      foreach (DimensionMapLayerDefinition layer in mapLayers.Values)
      {
        if (string.Equals(layer.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          layerIds.Add(layer.LayerId);
        }
      }

      for (int i = 0; i < layerIds.Count; i++)
      {
        mapLayers.Remove(layerIds[i]);
      }
    }

    private void RemoveMarkersForDimension(string dimensionId)
    {
      List<string> markerIds = new List<string>();
      foreach (DimensionMapMarker marker in markers.Values)
      {
        if (string.Equals(marker.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          markerIds.Add(marker.MarkerId);
        }
      }

      for (int i = 0; i < markerIds.Count; i++)
      {
        markers.Remove(markerIds[i]);
        RemovePersistedMarkerIfWorldRegistryLoaded(markerIds[i]);
      }
    }

    private void RemoveAnchorsForDimension(string dimensionId)
    {
      List<string> anchorIds = new List<string>();
      foreach (DimensionAnchorDefinition anchor in anchors.Values)
      {
        if (string.Equals(anchor.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          anchorIds.Add(anchor.AnchorId);
        }
      }

      for (int i = 0; i < anchorIds.Count; i++)
      {
        anchors.Remove(anchorIds[i]);
        RemovePersistedAnchorIfWorldRegistryLoaded(anchorIds[i]);
      }
    }

    private void RemoveZoneDefinitionsForDimension(string dimensionId)
    {
      List<string> zoneIds = new List<string>();
      foreach (DimensionZoneDefinition zone in zoneDefinitions.Values)
      {
        if (string.Equals(zone.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          zoneIds.Add(zone.ZoneId);
        }
      }

      for (int i = 0; i < zoneIds.Count; i++)
      {
        zoneDefinitions.Remove(zoneIds[i]);
      }
    }

    private void RemoveGenerationPassesForDimension(string dimensionId)
    {
      List<string> passIds = new List<string>();
      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        if (string.Equals(generationPass.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          passIds.Add(generationPass.PassId);
        }
      }

      for (int i = 0; i < passIds.Count; i++)
      {
        generationPasses.Remove(passIds[i]);
      }
    }

    private void RemoveScenesForDimension(string dimensionId)
    {
      List<string> sceneIds = new List<string>();
      foreach (DimensionSceneDefinition scene in scenes.Values)
      {
        if (string.Equals(scene.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          sceneIds.Add(scene.SceneId);
        }
      }

      for (int i = 0; i < sceneIds.Count; i++)
      {
        scenes.Remove(sceneIds[i]);
        RemovePersistedSceneIfWorldRegistryLoaded(sceneIds[i]);
      }
    }

    private void RemoveEncountersForDimension(string dimensionId)
    {
      List<string> encounterIds = new List<string>();
      foreach (DimensionEncounterDefinition encounter in encounters.Values)
      {
        if (string.Equals(encounter.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          encounterIds.Add(encounter.EncounterId);
        }
      }

      for (int i = 0; i < encounterIds.Count; i++)
      {
        encounters.Remove(encounterIds[i]);
      }
    }

    private void RemoveWorldEventsForDimension(string dimensionId)
    {
      List<string> eventIds = new List<string>();
      foreach (DimensionWorldEventDefinition worldEvent in worldEvents.Values)
      {
        if (string.Equals(worldEvent.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          eventIds.Add(worldEvent.EventId);
        }
      }

      for (int i = 0; i < eventIds.Count; i++)
      {
        worldEvents.Remove(eventIds[i]);
      }
    }

    private void RemoveSceneTemplatesForDimension(string dimensionId)
    {
      List<string> templateIds = new List<string>();
      foreach (DimensionSceneTemplateDefinition template in sceneTemplates.Values)
      {
        if (string.Equals(template.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          templateIds.Add(template.TemplateId);
        }
      }

      for (int i = 0; i < templateIds.Count; i++)
      {
        sceneTemplates.Remove(templateIds[i]);
      }
    }

    private void RemoveProgressFlagsForDimension(string dimensionId)
    {
      List<string> flagIds = new List<string>();
      foreach (DimensionProgressFlag flag in progressFlags.Values)
      {
        if (string.Equals(flag.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          flagIds.Add(flag.FlagId);
        }
      }

      for (int i = 0; i < flagIds.Count; i++)
      {
        progressFlags.Remove(flagIds[i]);
        RemovePersistedProgressFlagIfWorldRegistryLoaded(flagIds[i]);
      }
    }

    private DimensionBounds LocalTileAreaAround(float2 localPosition)
    {
      int2 tile = new int2((int)math.floor(localPosition.x), (int)math.floor(localPosition.y));
      return new DimensionBounds(tile, tile + new int2(1, 1));
    }

    private DimensionBounds TravelTargetGenerationAreaAround(DimensionTravelRequest request)
    {
      DimensionBounds starterBounds;
      if (TryGetStarterGenerationAreaForTravel(request, out starterBounds))
      {
        return starterBounds;
      }

      DimensionZoneDefinition targetZone;
      if (TryFindZoneDefinitionAtLocal(
              request.TargetDimensionId,
              request.TargetLocalPosition,
              out targetZone))
      {
        return targetZone.LocalBounds;
      }

      return CenteredLocalTileAreaAround(request.TargetLocalPosition, 16);
    }

    private bool TryGetStarterGenerationAreaForTravel(
        DimensionTravelRequest request,
        out DimensionBounds localBounds)
    {
      foreach (DimensionStarterDefinition starter in starters.Values)
      {
        if (!starter.Enabled)
        {
          continue;
        }

        DimensionStarterGenerationRequest generationRequest = starter.GenerationRequest;
        if (!string.Equals(generationRequest.DimensionId, request.TargetDimensionId, StringComparison.Ordinal) ||
            !generationRequest.LocalBounds.Contains(request.TargetLocalPosition))
        {
          continue;
        }

        DimensionTravelLoopPreflightRequest loopRequest = starter.TravelLoopPreflightRequest;
        bool matchesPortal =
            !string.IsNullOrEmpty(request.PortalId) &&
            (string.Equals(request.PortalId, loopRequest.EntryPortalId, StringComparison.Ordinal) ||
             string.Equals(request.PortalId, loopRequest.ReturnPortalId, StringComparison.Ordinal));
        if (!matchesPortal)
        {
          continue;
        }

        localBounds = generationRequest.LocalBounds;
        return true;
      }

      localBounds = default(DimensionBounds);
      return false;
    }

    private DimensionBounds CenteredLocalTileAreaAround(float2 localPosition, int sideTiles)
    {
      int clampedSideTiles = math.max(1, sideTiles);
      int2 tile = new int2((int)math.floor(localPosition.x), (int)math.floor(localPosition.y));
      int half = clampedSideTiles / 2;
      int2 min = tile - new int2(half, half);
      return new DimensionBounds(min, min + new int2(clampedSideTiles, clampedSideTiles));
    }
  }
}
