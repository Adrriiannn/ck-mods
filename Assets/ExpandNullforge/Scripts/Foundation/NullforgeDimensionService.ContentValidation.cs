using System.Collections.Generic;
using ExpandNullforge.Api;

using System;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public DimensionContentValidationReport ValidateContent(
        DimensionContentValidationRequest request)
    {
      List<DimensionContentValidationIssue> issues =
          new List<DimensionContentValidationIssue>();
      int errorCount = 0;
      int warningCount = 0;

      IReadOnlyList<DimensionContentPackReadinessResult> readinessResults =
          EvaluateContentPackReadiness(request.IncludeDisabled);
      for (int i = 0; i < readinessResults.Count; i++)
      {
        DimensionContentPackReadinessResult readiness = readinessResults[i];
        if (!ContentPackMatchesValidationRequest(readiness.ContentPackId, request))
        {
          continue;
        }

        if (!readiness.Ready)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Custom,
              readiness.ContentPackId,
              readiness.ContentPackId,
              readiness.Code,
              readiness.Message);
        }
      }

      if (request.IncludeWarnings)
      {
        IReadOnlyList<DimensionContentOwnershipBinding> orphanedBindings =
            GetOrphanedContentOwnershipBindings();
        for (int i = 0; i < orphanedBindings.Count; i++)
        {
          DimensionContentOwnershipBinding binding = orphanedBindings[i];
          if (!ContentPackMatchesValidationRequest(binding.ContentPackId, request))
          {
            continue;
          }

          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Warning,
              binding.RecordKind,
              binding.RecordId,
              binding.ContentPackId,
              "content-ownership-orphaned",
              "The content ownership binding points at a missing owner or record.");
        }

        AddUnownedContentValidationWarnings(
            issues,
            request,
            ref errorCount,
            ref warningCount);
      }

      foreach (DimensionZoneDefinition zone in zoneDefinitions.Values)
      {
        if (!ShouldValidateContentRecord(
                zone.Enabled,
                DimensionContentRecordKind.ZoneDefinition,
                zone.ZoneId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.ZoneDefinition, zone.ZoneId);
        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.ZoneDefinition,
                zone.ZoneId,
                ownerId,
                zone.DimensionId,
                "zone",
                "zone",
                out dimension))
        {
          ValidateContentBoundsInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.ZoneDefinition,
              zone.ZoneId,
              ownerId,
              zone.LocalBounds,
              dimension,
              "zone",
              "zone");
        }
      }

      foreach (DimensionMapLayerDefinition layer in mapLayers.Values)
      {
        bool enabled = layer.Visible || layer.Selectable;
        if (!ShouldValidateContentRecord(
                enabled,
                DimensionContentRecordKind.MapLayer,
                layer.LayerId,
                string.Empty,
                request))
        {
          continue;
        }

        DimensionDefinition ignoredDimension;
        TryValidateContentDimensionReference(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.MapLayer,
            layer.LayerId,
            GetContentValidationOwnerId(DimensionContentRecordKind.MapLayer, layer.LayerId),
            layer.DimensionId,
            "map-layer",
            "map layer",
            out ignoredDimension);
      }

      foreach (DimensionMapMarker marker in markers.Values)
      {
        if (!ShouldValidateContentRecord(
                marker.Visible,
                DimensionContentRecordKind.MapMarker,
                marker.MarkerId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.MapMarker, marker.MarkerId);
        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.MapMarker,
                marker.MarkerId,
                ownerId,
                marker.DimensionId,
                "map-marker",
                "map marker",
                out dimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.MapMarker,
              marker.MarkerId,
              ownerId,
              marker.LocalPosition,
              dimension,
              "map-marker",
              "map marker");
        }
      }

      foreach (DimensionAnchorDefinition anchor in anchors.Values)
      {
        if (!ShouldValidateContentRecord(
                anchor.Enabled,
                DimensionContentRecordKind.Anchor,
                anchor.AnchorId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.Anchor, anchor.AnchorId);
        if (!IsValidAnchorKind(anchor.Kind))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Anchor,
              anchor.AnchorId,
              ownerId,
              "anchor-kind-invalid",
              "The anchor kind is not supported.");
        }

        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.Anchor,
                anchor.AnchorId,
                ownerId,
                anchor.DimensionId,
                "anchor",
                "anchor",
                out dimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.Anchor,
              anchor.AnchorId,
              ownerId,
              anchor.LocalPosition,
              dimension,
              "anchor",
              "anchor");
        }
      }

      foreach (DimensionPortalDefinition portal in portals.Values)
      {
        bool enabled = portal.State != DimensionPortalState.Disabled;
        if (!ShouldValidateContentRecord(
                enabled,
                DimensionContentRecordKind.Portal,
                portal.PortalId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.Portal, portal.PortalId);
        if (!IsValidPortalState(portal.State))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Portal,
              portal.PortalId,
              ownerId,
              "portal-state-invalid",
              "The portal state is not supported.");
        }

        DimensionDefinition fromDimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.Portal,
                portal.PortalId,
                ownerId,
                portal.FromDimensionId,
                "portal-source",
                "portal source",
                out fromDimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.Portal,
              portal.PortalId,
              ownerId,
              portal.FromLocalPosition,
              fromDimension,
              "portal-source",
              "portal source");
        }

        DimensionDefinition toDimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.Portal,
                portal.PortalId,
                ownerId,
                portal.ToDimensionId,
                "portal-target",
                "portal target",
                out toDimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.Portal,
              portal.PortalId,
              ownerId,
              portal.ToLocalPosition,
              toDimension,
              "portal-target",
              "portal target");
        }
      }

      foreach (DimensionPortalPresentationDefinition presentation in portalPresentations.Values)
      {
        if (!ShouldValidateContentRecord(
                presentation.Enabled,
                DimensionContentRecordKind.PortalPresentation,
                presentation.PresentationId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.PortalPresentation, presentation.PresentationId);
        DimensionPortalDefinition portal;
        if (!portals.TryGetValue(presentation.PortalId, out portal))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.PortalPresentation,
              presentation.PresentationId,
              ownerId,
              "portal-presentation-portal-missing",
              "The portal presentation points at a missing portal.");
        }
        else if (portal.State == DimensionPortalState.Disabled)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Warning,
              DimensionContentRecordKind.PortalPresentation,
              presentation.PresentationId,
              ownerId,
              "portal-presentation-portal-disabled",
              "The portal presentation points at a disabled portal.");
        }
      }

      foreach (DimensionTravelRequirementDefinition requirement in travelRequirements.Values)
      {
        if (!ShouldValidateContentRecord(
                requirement.Enabled,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.TravelRequirement, requirement.RequirementId);
        if (!IsValidTravelRequirementKind(requirement.Kind) ||
            requirement.Kind == DimensionTravelRequirementKind.Any)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.TravelRequirement,
              requirement.RequirementId,
              ownerId,
              "travel-requirement-kind-invalid",
              "The travel requirement kind is not supported.");
        }

        DimensionPortalDefinition portal;
        if (!string.IsNullOrEmpty(requirement.PortalId))
        {
          if (!portals.TryGetValue(requirement.PortalId, out portal))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-portal-missing",
                "The travel requirement points at a missing portal.");
          }
          else if (!string.IsNullOrEmpty(requirement.DimensionId) &&
                   !string.Equals(portal.ToDimensionId, requirement.DimensionId, StringComparison.Ordinal))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-portal-dimension-mismatch",
                "The travel requirement dimension does not match its portal destination.");
          }
        }

        if (!string.IsNullOrEmpty(requirement.DimensionId))
        {
          DimensionDefinition ignoredDimension;
          TryValidateContentDimensionReference(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.TravelRequirement,
              requirement.RequirementId,
              ownerId,
              requirement.DimensionId,
              "travel-requirement",
              "travel requirement",
              out ignoredDimension);
        }

        if (requirement.Kind == DimensionTravelRequirementKind.ProgressFlag &&
            !string.IsNullOrEmpty(requirement.SubjectId))
        {
          DimensionProgressFlag flag;
          if (!progressFlags.TryGetValue(requirement.SubjectId, out flag))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-progress-flag-missing",
                "The progress-flag travel requirement points at a missing progress flag.");
          }
          else if (!string.IsNullOrEmpty(requirement.DimensionId) &&
                   !string.IsNullOrEmpty(flag.DimensionId) &&
                   !string.Equals(flag.DimensionId, requirement.DimensionId, StringComparison.Ordinal))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-progress-flag-dimension-mismatch",
                "The progress-flag travel requirement points at a flag from another dimension.");
          }
        }
      }

      foreach (DimensionProgressFlag flag in progressFlags.Values)
      {
        if (!ShouldValidateContentRecord(
                true,
                DimensionContentRecordKind.ProgressFlag,
                flag.FlagId,
                string.Empty,
                request))
        {
          continue;
        }

        if (!string.IsNullOrEmpty(flag.DimensionId))
        {
          DimensionDefinition ignoredDimension;
          TryValidateContentDimensionReference(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.ProgressFlag,
              flag.FlagId,
              GetContentValidationOwnerId(DimensionContentRecordKind.ProgressFlag, flag.FlagId),
              flag.DimensionId,
              "progress-flag",
              "progress flag",
              out ignoredDimension);
        }
      }

      foreach (DimensionSceneTemplateDefinition template in sceneTemplates.Values)
      {
        if (!ShouldValidateContentRecord(
                template.Enabled,
                DimensionContentRecordKind.SceneTemplate,
                template.TemplateId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.SceneTemplate, template.TemplateId);
        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.SceneTemplate,
                template.TemplateId,
                ownerId,
                template.DimensionId,
                "scene-template",
                "scene template",
                out dimension))
        {
          if (template.FootprintSize.x <= 0 || template.FootprintSize.y <= 0)
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.SceneTemplate,
                template.TemplateId,
                ownerId,
                "scene-template-footprint-invalid",
                "The scene template footprint is invalid.");
          }
          else if (template.FootprintSize.x > dimension.LocalBounds.Size.x ||
                   template.FootprintSize.y > dimension.LocalBounds.Size.y)
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.SceneTemplate,
                template.TemplateId,
                ownerId,
                "scene-template-footprint-too-large",
                "The scene template footprint is larger than its dimension.");
          }
        }

        DimensionZoneDefinition ignoredZone;
        TryValidateContentOptionalZoneReference(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.SceneTemplate,
            template.TemplateId,
            ownerId,
            template.ZoneId,
            template.DimensionId,
            "scene-template",
            "scene template",
            out ignoredZone);
      }

      foreach (DimensionSceneDefinition scene in scenes.Values)
      {
        bool enabled = scene.State != DimensionSceneState.Disabled;
        if (!ShouldValidateContentRecord(
                enabled,
                DimensionContentRecordKind.Scene,
                scene.SceneId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.Scene, scene.SceneId);
        if (!IsValidSceneState(scene.State))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Scene,
              scene.SceneId,
              ownerId,
              "scene-state-invalid",
              "The scene state is not supported.");
        }

        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.Scene,
                scene.SceneId,
                ownerId,
                scene.DimensionId,
                "scene",
                "scene",
                out dimension))
        {
          ValidateContentBoundsInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.Scene,
              scene.SceneId,
              ownerId,
              scene.LocalBounds,
              dimension,
              "scene",
              "scene");
        }
      }

      foreach (DimensionEncounterDefinition encounter in encounters.Values)
      {
        if (!ShouldValidateContentRecord(
                encounter.Enabled,
                DimensionContentRecordKind.Encounter,
                encounter.EncounterId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.Encounter, encounter.EncounterId);
        if (!IsValidEncounterKind(encounter.Kind) ||
            encounter.Kind == DimensionEncounterKind.Any)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Encounter,
              encounter.EncounterId,
              ownerId,
              "encounter-kind-invalid",
              "The encounter kind is not supported.");
        }

        DimensionDefinition ignoredDimension;
        TryValidateContentDimensionReference(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.Encounter,
            encounter.EncounterId,
            ownerId,
            encounter.DimensionId,
            "encounter",
            "encounter",
            out ignoredDimension);

        DimensionZoneDefinition ignoredZone;
        TryValidateContentOptionalZoneReference(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.Encounter,
            encounter.EncounterId,
            ownerId,
            encounter.ZoneId,
            encounter.DimensionId,
            "encounter",
            "encounter",
            out ignoredZone);

        DimensionSceneDefinition scene;
        if (!string.IsNullOrEmpty(encounter.SceneId) &&
            scenes.TryGetValue(encounter.SceneId, out scene) &&
            !string.Equals(scene.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Encounter,
              encounter.EncounterId,
              ownerId,
              "encounter-scene-dimension-mismatch",
              "The encounter scene belongs to another dimension.");
        }
        else if (!string.IsNullOrEmpty(encounter.SceneId) &&
                 !scenes.ContainsKey(encounter.SceneId))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Encounter,
              encounter.EncounterId,
              ownerId,
              "encounter-scene-missing",
              "The encounter points at a missing scene.");
        }

        DimensionMapMarker marker;
        if (!string.IsNullOrEmpty(encounter.MarkerId) &&
            markers.TryGetValue(encounter.MarkerId, out marker) &&
            !string.Equals(marker.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Encounter,
              encounter.EncounterId,
              ownerId,
              "encounter-marker-dimension-mismatch",
              "The encounter marker belongs to another dimension.");
        }
        else if (!string.IsNullOrEmpty(encounter.MarkerId) &&
                 !markers.ContainsKey(encounter.MarkerId))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Encounter,
              encounter.EncounterId,
              ownerId,
              "encounter-marker-missing",
              "The encounter points at a missing map marker.");
        }

        DimensionProgressFlag flag;
        if (!string.IsNullOrEmpty(encounter.DefeatFlagId) &&
            progressFlags.TryGetValue(encounter.DefeatFlagId, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.Equals(flag.DimensionId, encounter.DimensionId, StringComparison.Ordinal))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Encounter,
              encounter.EncounterId,
              ownerId,
              "encounter-defeat-flag-dimension-mismatch",
              "The encounter defeat flag belongs to another dimension.");
        }
        else if (!string.IsNullOrEmpty(encounter.DefeatFlagId) &&
                 !progressFlags.ContainsKey(encounter.DefeatFlagId))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Encounter,
              encounter.EncounterId,
              ownerId,
              "encounter-defeat-flag-missing",
              "The encounter points at a missing defeat progress flag.");
        }
      }

      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        if (!ShouldValidateContentRecord(
                generationPass.Enabled,
                DimensionContentRecordKind.GenerationPass,
                generationPass.PassId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.GenerationPass, generationPass.PassId);
        if (!IsValidGenerationPassPhase(generationPass.Phase))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.GenerationPass,
              generationPass.PassId,
              ownerId,
              "generation-pass-phase-invalid",
              "The generation pass phase is not supported.");
        }

        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.GenerationPass,
                generationPass.PassId,
                ownerId,
                generationPass.DimensionId,
                "generation-pass",
                "generation pass",
                out dimension) &&
            generationPass.HasLocalBounds)
        {
          ValidateContentBoundsInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.GenerationPass,
              generationPass.PassId,
              ownerId,
              generationPass.LocalBounds,
              dimension,
              "generation-pass",
              "generation pass");
        }

        DimensionZoneDefinition ignoredZone;
        TryValidateContentOptionalZoneReference(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.GenerationPass,
            generationPass.PassId,
            ownerId,
            generationPass.ZoneId,
            generationPass.DimensionId,
            "generation-pass",
            "generation pass",
            out ignoredZone);

        ValidateContentKnownGenerationProvider(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.GenerationPass,
            generationPass.PassId,
            ownerId,
            generationPass.ProviderId,
            "generation-pass",
            "generation pass");
      }

      foreach (DimensionWorldEventDefinition worldEvent in worldEvents.Values)
      {
        if (!ShouldValidateContentRecord(
                worldEvent.Enabled,
                DimensionContentRecordKind.WorldEvent,
                worldEvent.EventId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.WorldEvent, worldEvent.EventId);
        if (!IsValidWorldEventKind(worldEvent.Kind) ||
            worldEvent.Kind == DimensionWorldEventKind.Any)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.WorldEvent,
              worldEvent.EventId,
              ownerId,
              "world-event-kind-invalid",
              "The world event kind is not supported.");
        }

        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.WorldEvent,
                worldEvent.EventId,
                ownerId,
                worldEvent.DimensionId,
                "world-event",
                "world event",
                out dimension) &&
            worldEvent.HasLocalBounds)
        {
          ValidateContentBoundsInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.WorldEvent,
              worldEvent.EventId,
              ownerId,
              worldEvent.LocalBounds,
              dimension,
              "world-event",
              "world event");
        }

        DimensionZoneDefinition ignoredZone;
        TryValidateContentOptionalZoneReference(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.WorldEvent,
            worldEvent.EventId,
            ownerId,
            worldEvent.ZoneId,
            worldEvent.DimensionId,
            "world-event",
            "world event",
            out ignoredZone);

        DimensionProgressFlag flag;
        if (!string.IsNullOrEmpty(worldEvent.ProgressFlagId) &&
            progressFlags.TryGetValue(worldEvent.ProgressFlagId, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.Equals(flag.DimensionId, worldEvent.DimensionId, StringComparison.Ordinal))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.WorldEvent,
              worldEvent.EventId,
              ownerId,
              "world-event-progress-flag-dimension-mismatch",
              "The world event progress flag belongs to another dimension.");
        }
        else if (!string.IsNullOrEmpty(worldEvent.ProgressFlagId) &&
                 !progressFlags.ContainsKey(worldEvent.ProgressFlagId))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.WorldEvent,
              worldEvent.EventId,
              ownerId,
              "world-event-progress-flag-missing",
              "The world event points at a missing progress flag.");
        }

        ValidateContentKnownGenerationProvider(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.WorldEvent,
            worldEvent.EventId,
            ownerId,
            worldEvent.ProviderId,
            "world-event",
            "world event");
      }

      foreach (DimensionAssetReferenceDefinition assetReference in assetReferences.Values)
      {
        if (!ShouldValidateContentRecord(
                assetReference.Enabled,
                DimensionContentRecordKind.AssetReference,
                assetReference.AssetId,
                assetReference.ContentPackId,
                request))
        {
          continue;
        }

        if (!contentPacks.ContainsKey(assetReference.ContentPackId))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.AssetReference,
              assetReference.AssetId,
              assetReference.ContentPackId,
              "asset-reference-content-pack-missing",
              "The asset reference owner content pack is missing.");
        }

        if (!string.IsNullOrEmpty(assetReference.DimensionId) &&
            !definitions.ContainsKey(assetReference.DimensionId))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.AssetReference,
              assetReference.AssetId,
              assetReference.ContentPackId,
              "asset-reference-dimension-missing",
              "The asset reference points at a missing dimension.");
        }
      }

      foreach (DimensionBiomeDefinition biome in biomes.Values)
      {
        if (!ShouldValidateContentRecord(
                biome.Enabled,
                DimensionContentRecordKind.Biome,
                biome.BiomeId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.Biome, biome.BiomeId);
        if (!string.IsNullOrEmpty(biome.DimensionId) &&
            !definitions.ContainsKey(biome.DimensionId))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Biome,
              biome.BiomeId,
              ownerId,
              "biome-dimension-missing",
              "The biome points at a missing dimension.");
        }
      }

      return new DimensionContentValidationReport(
          true,
          errorCount > 0,
          errorCount,
          warningCount,
          issues);
    }
  }
}
