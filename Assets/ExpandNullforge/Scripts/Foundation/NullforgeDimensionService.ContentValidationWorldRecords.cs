using System.Collections.Generic;
using ExpandNullforge.Api;

using System;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Checking the records a world is filled with: scenes, encounters, passes, events, biomes.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    /// <summary>
    /// Checks every scene template this service holds.
    /// </summary>
    private void ValidateSceneTemplateRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
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
    }

    /// <summary>
    /// Checks every scene this service holds.
    /// </summary>
    private void ValidateSceneRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
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
    }

    /// <summary>
    /// Checks every encounter this service holds.
    /// </summary>
    private void ValidateEncounterRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
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
    }

    /// <summary>
    /// Checks every generation pass this service holds.
    /// </summary>
    private void ValidateGenerationPassRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
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
    }

    /// <summary>
    /// Checks every world event this service holds.
    /// </summary>
    private void ValidateWorldEventRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
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
    }

    /// <summary>
    /// Checks every asset reference this service holds.
    /// </summary>
    private void ValidateAssetReferenceRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
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
    }

    /// <summary>
    /// Checks every biome this service holds.
    /// </summary>
    private void ValidateBiomeRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
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
    }
  }
}
