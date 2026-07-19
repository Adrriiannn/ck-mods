using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private static int CompareContentOwnershipBindings(
        DimensionContentOwnershipBinding left,
        DimensionContentOwnershipBinding right)
    {
      int byContentPack = string.Compare(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal);
      if (byContentPack != 0)
      {
        return byContentPack;
      }

      int byKind = left.RecordKind.CompareTo(right.RecordKind);
      if (byKind != 0)
      {
        return byKind;
      }

      return string.Compare(left.RecordId, right.RecordId, StringComparison.Ordinal);
    }

    private static string BuildContentOwnershipKey(
        DimensionContentRecordKind recordKind,
        string recordId)
    {
      return ((int)recordKind).ToString() + ":" + (recordId ?? string.Empty);
    }

    private bool ContentOwnershipMatchesQuery(
        DimensionContentOwnershipBinding binding,
        DimensionContentOwnershipQuery query)
    {
      if (!string.IsNullOrEmpty(query.ContentPackId) &&
          !string.Equals(binding.ContentPackId, query.ContentPackId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.RecordKind != DimensionContentRecordKind.Any &&
          binding.RecordKind != query.RecordKind)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.RecordId) &&
          !string.Equals(binding.RecordId, query.RecordId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.EnabledContentPacksOnly && !ContentPackExistsAndEnabled(binding.ContentPackId, true))
      {
        return false;
      }

      if (!query.IncludeOrphaned &&
          (!ContentPackExistsAndEnabled(binding.ContentPackId, false) ||
           (!ContentRecordExists(binding.RecordKind, binding.RecordId) &&
            !IsMetadataOnlyContentRecordKind(binding.RecordKind))))
      {
        return false;
      }

      return true;
    }

    private bool ContentPackExistsAndEnabled(string contentPackId, bool requireEnabled)
    {
      DimensionContentPackDefinition contentPack;
      if (!contentPacks.TryGetValue(contentPackId, out contentPack))
      {
        return false;
      }

      return !requireEnabled || contentPack.Enabled;
    }

    private static bool ContentPackMatchesValidationRequest(
        string contentPackId,
        DimensionContentValidationRequest request)
    {
      return string.IsNullOrEmpty(request.ContentPackId) ||
             string.Equals(contentPackId, request.ContentPackId, StringComparison.Ordinal);
    }

    private void AddUnownedContentValidationWarnings(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount)
    {
      if (!string.IsNullOrEmpty(request.ContentPackId))
      {
        return;
      }

      foreach (DimensionDefinition definition in definitions.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            definition.LifecycleState != DimensionLifecycleState.Disabled,
            DimensionContentRecordKind.Dimension,
            definition.Id,
            "dimension");
      }

      foreach (DimensionZoneDefinition zone in zoneDefinitions.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            zone.Enabled,
            DimensionContentRecordKind.ZoneDefinition,
            zone.ZoneId,
            "zone");
      }

      foreach (DimensionMapLayerDefinition layer in mapLayers.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            layer.Visible || layer.Selectable,
            DimensionContentRecordKind.MapLayer,
            layer.LayerId,
            "map layer");
      }

      foreach (DimensionMapMarker marker in markers.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            marker.Visible,
            DimensionContentRecordKind.MapMarker,
            marker.MarkerId,
            "map marker");
      }

      foreach (DimensionAnchorDefinition anchor in anchors.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            anchor.Enabled,
            DimensionContentRecordKind.Anchor,
            anchor.AnchorId,
            "anchor");
      }

      foreach (DimensionPortalDefinition portal in portals.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            portal.State != DimensionPortalState.Disabled,
            DimensionContentRecordKind.Portal,
            portal.PortalId,
            "portal");
      }

      foreach (DimensionPortalPresentationDefinition presentation in portalPresentations.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            presentation.Enabled,
            DimensionContentRecordKind.PortalPresentation,
            presentation.PresentationId,
            "portal presentation");
      }

      foreach (DimensionTravelRequirementDefinition requirement in travelRequirements.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            requirement.Enabled,
            DimensionContentRecordKind.TravelRequirement,
            requirement.RequirementId,
            "travel requirement");
      }

      foreach (DimensionStarterDefinition starter in starters.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            starter.Enabled,
            DimensionContentRecordKind.Starter,
            starter.StarterId,
            "dimension starter");
      }

      foreach (DimensionProgressFlag flag in progressFlags.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            true,
            DimensionContentRecordKind.ProgressFlag,
            flag.FlagId,
            "progress flag");
      }

      foreach (DimensionSceneTemplateDefinition template in sceneTemplates.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            template.Enabled,
            DimensionContentRecordKind.SceneTemplate,
            template.TemplateId,
            "scene template");
      }

      foreach (DimensionSceneDefinition scene in scenes.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            scene.State != DimensionSceneState.Disabled,
            DimensionContentRecordKind.Scene,
            scene.SceneId,
            "scene");
      }

      foreach (DimensionSpawnRule rule in spawnRules.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            rule.Enabled,
            DimensionContentRecordKind.SpawnRule,
            rule.RuleId,
            "spawn rule");
      }

      foreach (DimensionEncounterDefinition encounter in encounters.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            encounter.Enabled,
            DimensionContentRecordKind.Encounter,
            encounter.EncounterId,
            "encounter");
      }

      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            generationPass.Enabled,
            DimensionContentRecordKind.GenerationPass,
            generationPass.PassId,
            "generation pass");
      }

      foreach (DimensionResourceNodeDefinition node in resourceNodes.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            node.Enabled,
            DimensionContentRecordKind.ResourceNode,
            node.NodeId,
            "resource node");
      }

      foreach (DimensionWorldEventDefinition worldEvent in worldEvents.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            worldEvent.Enabled,
            DimensionContentRecordKind.WorldEvent,
            worldEvent.EventId,
            "world event");
      }

      foreach (DimensionEnvironmentProfile profile in environmentProfiles.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            profile.Enabled,
            DimensionContentRecordKind.EnvironmentProfile,
            profile.ProfileId,
            "environment profile");
      }

      foreach (DimensionBiomeDefinition biome in biomes.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            biome.Enabled,
            DimensionContentRecordKind.Biome,
            biome.BiomeId,
            "biome");
      }

      foreach (DimensionGenerationTableDefinition table in generationTables.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            table.Enabled,
            DimensionContentRecordKind.GenerationTable,
            table.TableId,
            "generation table");
      }

      foreach (DimensionGenerationTableEntryDefinition entry in generationTableEntries.Values)
      {
        AddUnownedContentValidationWarning(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            entry.Enabled,
            DimensionContentRecordKind.GenerationTableEntry,
            entry.EntryId,
            "generation table entry");
      }
    }

    private void AddUnownedContentValidationWarning(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount,
        bool enabled,
        DimensionContentRecordKind recordKind,
        string recordId,
        string label)
    {
      if (string.IsNullOrEmpty(recordId) ||
          (!request.IncludeDisabled && !enabled))
      {
        return;
      }

      DimensionContentOwnershipBinding binding;
      if (TryGetContentOwner(recordKind, recordId, out binding))
      {
        return;
      }

      AddContentValidationIssue(
          issues,
          request,
          ref errorCount,
          ref warningCount,
          DimensionDiagnosticSeverity.Warning,
          recordKind,
          recordId,
          string.Empty,
          "content-record-unowned",
          "The " + label + " has no content ownership binding.");
    }

    private bool ShouldValidateContentRecord(
        bool enabled,
        DimensionContentRecordKind recordKind,
        string recordId,
        string fallbackContentPackId,
        DimensionContentValidationRequest request)
    {
      if (!request.IncludeDisabled && !enabled)
      {
        return false;
      }

      DimensionContentOwnershipBinding binding;
      bool hasBinding = TryGetContentOwner(recordKind, recordId, out binding);
      string ownerContentPackId = !string.IsNullOrEmpty(fallbackContentPackId)
          ? fallbackContentPackId
          : hasBinding ? binding.ContentPackId : string.Empty;

      if (!request.IncludeDisabled &&
          !string.IsNullOrEmpty(ownerContentPackId) &&
          !ContentPackExistsAndEnabled(ownerContentPackId, true))
      {
        return false;
      }

      if (string.IsNullOrEmpty(request.ContentPackId))
      {
        return true;
      }

      if (string.Equals(ownerContentPackId, request.ContentPackId, StringComparison.Ordinal))
      {
        return true;
      }

      return false;
    }

    private string GetContentValidationOwnerId(
        DimensionContentRecordKind recordKind,
        string recordId)
    {
      DimensionContentOwnershipBinding binding;
      return TryGetContentOwner(recordKind, recordId, out binding)
          ? binding.ContentPackId
          : string.Empty;
    }

    private bool TryValidateContentDimensionReference(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount,
        DimensionContentRecordKind recordKind,
        string recordId,
        string ownerId,
        string dimensionId,
        string codePrefix,
        string label,
        out DimensionDefinition dimension)
    {
      if (string.IsNullOrEmpty(dimensionId) ||
          !definitions.TryGetValue(dimensionId, out dimension))
      {
        AddContentValidationIssue(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionDiagnosticSeverity.Error,
            recordKind,
            recordId,
            ownerId,
            codePrefix + "-dimension-missing",
            "The " + label + " points at a missing dimension.");
        dimension = default(DimensionDefinition);
        return false;
      }

      return true;
    }

    private bool TryValidateContentOptionalZoneReference(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount,
        DimensionContentRecordKind recordKind,
        string recordId,
        string ownerId,
        string zoneId,
        string dimensionId,
        string codePrefix,
        string label,
        out DimensionZoneDefinition zone)
    {
      if (string.IsNullOrEmpty(zoneId))
      {
        zone = default(DimensionZoneDefinition);
        return true;
      }

      if (!zoneDefinitions.TryGetValue(zoneId, out zone))
      {
        AddContentValidationIssue(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionDiagnosticSeverity.Error,
            recordKind,
            recordId,
            ownerId,
            codePrefix + "-zone-missing",
            "The " + label + " points at a missing zone.");
        return false;
      }

      if (!string.IsNullOrEmpty(dimensionId) &&
          !string.Equals(zone.DimensionId, dimensionId, StringComparison.Ordinal))
      {
        AddContentValidationIssue(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionDiagnosticSeverity.Error,
            recordKind,
            recordId,
            ownerId,
            codePrefix + "-zone-dimension-mismatch",
            "The " + label + " points at a zone from another dimension.");
        return false;
      }

      return true;
    }

    private void ValidateContentBoundsInsideDimension(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount,
        DimensionContentRecordKind recordKind,
        string recordId,
        string ownerId,
        DimensionBounds bounds,
        DimensionDefinition dimension,
        string codePrefix,
        string label)
    {
      if (bounds.Size.x <= 0 || bounds.Size.y <= 0)
      {
        AddContentValidationIssue(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionDiagnosticSeverity.Error,
            recordKind,
            recordId,
            ownerId,
            codePrefix + "-bounds-invalid",
            "The " + label + " has invalid local bounds.");
        return;
      }

      if (!dimension.LocalBounds.Contains(bounds.Min) ||
          !dimension.LocalBounds.Contains(bounds.MaxExclusive - new int2(1, 1)))
      {
        AddContentValidationIssue(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionDiagnosticSeverity.Error,
            recordKind,
            recordId,
            ownerId,
            codePrefix + "-bounds-out-of-dimension",
            "The " + label + " bounds are outside its dimension.");
      }
    }

    private void ValidateContentPositionInsideDimension(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount,
        DimensionContentRecordKind recordKind,
        string recordId,
        string ownerId,
        float2 localPosition,
        DimensionDefinition dimension,
        string codePrefix,
        string label)
    {
      if (!dimension.LocalBounds.Contains(localPosition))
      {
        AddContentValidationIssue(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionDiagnosticSeverity.Error,
            recordKind,
            recordId,
            ownerId,
            codePrefix + "-position-out-of-dimension",
            "The " + label + " position is outside its dimension.");
      }
    }

    private void ValidateContentKnownGenerationProvider(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount,
        DimensionContentRecordKind recordKind,
        string recordId,
        string ownerId,
        string providerId,
        string codePrefix,
        string label)
    {
      if (string.IsNullOrEmpty(providerId) ||
          generationProviders.ContainsKey(providerId))
      {
        return;
      }

      AddContentValidationIssue(
          issues,
          request,
          ref errorCount,
          ref warningCount,
          DimensionDiagnosticSeverity.Warning,
          recordKind,
          recordId,
          ownerId,
          codePrefix + "-provider-missing",
          "The " + label + " points at a generation provider that is not currently registered.");
    }

    private static void AddContentValidationIssue(
        List<DimensionContentValidationIssue> issues,
        DimensionContentValidationRequest request,
        ref int errorCount,
        ref int warningCount,
        DimensionDiagnosticSeverity severity,
        DimensionContentRecordKind recordKind,
        string recordId,
        string contentPackId,
        string code,
        string message)
    {
      if (severity == DimensionDiagnosticSeverity.Warning && !request.IncludeWarnings)
      {
        return;
      }

      if (severity == DimensionDiagnosticSeverity.Error)
      {
        errorCount++;
      }
      else if (severity == DimensionDiagnosticSeverity.Warning)
      {
        warningCount++;
      }

      issues.Add(
          new DimensionContentValidationIssue(
              severity,
              recordKind,
              recordId,
              contentPackId,
              code,
              message));
    }

    private bool ValidateContentOwnershipBinding(
        DimensionContentOwnershipBinding binding,
        bool requireExistingRecord,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(binding.ContentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
        return false;
      }

      if (!contentPacks.ContainsKey(binding.ContentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered.");
        return false;
      }

      if (!IsValidContentRecordKind(binding.RecordKind))
      {
        result = DimensionOperationResult.Failed("content-record-kind-invalid", "A valid content record kind is required.");
        return false;
      }

      if (string.IsNullOrEmpty(binding.RecordId))
      {
        result = DimensionOperationResult.Failed("content-record-id-empty", "A content record id is required.");
        return false;
      }

      if (requireExistingRecord &&
          !ContentRecordExists(binding.RecordKind, binding.RecordId) &&
          !IsMetadataOnlyContentRecordKind(binding.RecordKind))
      {
        result = DimensionOperationResult.Failed("content-record-not-found", "No registered content record matches that kind and id.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool IsValidContentRecordKind(DimensionContentRecordKind kind)
    {
      return kind == DimensionContentRecordKind.Dimension ||
             kind == DimensionContentRecordKind.Portal ||
             kind == DimensionContentRecordKind.PortalPresentation ||
             kind == DimensionContentRecordKind.TravelRequirement ||
             kind == DimensionContentRecordKind.TravelRequirementEvaluator ||
             kind == DimensionContentRecordKind.MapLayer ||
             kind == DimensionContentRecordKind.MapMarker ||
             kind == DimensionContentRecordKind.Anchor ||
             kind == DimensionContentRecordKind.Biome ||
             kind == DimensionContentRecordKind.GenerationTable ||
             kind == DimensionContentRecordKind.GenerationTableEntry ||
             kind == DimensionContentRecordKind.ZoneDefinition ||
             kind == DimensionContentRecordKind.ZoneProvider ||
             kind == DimensionContentRecordKind.EnvironmentProfile ||
             kind == DimensionContentRecordKind.Scene ||
             kind == DimensionContentRecordKind.SceneTemplate ||
             kind == DimensionContentRecordKind.Encounter ||
             kind == DimensionContentRecordKind.ResourceNode ||
             kind == DimensionContentRecordKind.SpawnRule ||
             kind == DimensionContentRecordKind.WorldEvent ||
             kind == DimensionContentRecordKind.ProgressFlag ||
             kind == DimensionContentRecordKind.GenerationProvider ||
             kind == DimensionContentRecordKind.GenerationPass ||
             kind == DimensionContentRecordKind.AccessProvider ||
             kind == DimensionContentRecordKind.AssetReference ||
             kind == DimensionContentRecordKind.Starter ||
             kind == DimensionContentRecordKind.Item ||
             kind == DimensionContentRecordKind.Recipe ||
             kind == DimensionContentRecordKind.Workbench ||
             kind == DimensionContentRecordKind.LootTable ||
             kind == DimensionContentRecordKind.Animal ||
             kind == DimensionContentRecordKind.Critter ||
             kind == DimensionContentRecordKind.Mob ||
             kind == DimensionContentRecordKind.Boss ||
             kind == DimensionContentRecordKind.SceneProp ||
             kind == DimensionContentRecordKind.SceneLootContainer ||
             kind == DimensionContentRecordKind.SceneSpawnPoint ||
             kind == DimensionContentRecordKind.SceneTrigger ||
             kind == DimensionContentRecordKind.Custom;
    }

    private static bool IsMetadataOnlyContentRecordKind(DimensionContentRecordKind kind)
    {
      return kind == DimensionContentRecordKind.Item ||
             kind == DimensionContentRecordKind.Recipe ||
             kind == DimensionContentRecordKind.Workbench ||
             kind == DimensionContentRecordKind.LootTable ||
             kind == DimensionContentRecordKind.Animal ||
             kind == DimensionContentRecordKind.Critter ||
             kind == DimensionContentRecordKind.Mob ||
             kind == DimensionContentRecordKind.Boss ||
             kind == DimensionContentRecordKind.SceneProp ||
             kind == DimensionContentRecordKind.SceneLootContainer ||
             kind == DimensionContentRecordKind.SceneSpawnPoint ||
             kind == DimensionContentRecordKind.SceneTrigger;
    }

    private bool ContentRecordExists(DimensionContentRecordKind kind, string recordId)
    {
      switch (kind)
      {
        case DimensionContentRecordKind.Dimension:
          return definitions.ContainsKey(recordId);
        case DimensionContentRecordKind.Portal:
          return portals.ContainsKey(recordId);
        case DimensionContentRecordKind.PortalPresentation:
          return portalPresentations.ContainsKey(recordId);
        case DimensionContentRecordKind.TravelRequirement:
          return travelRequirements.ContainsKey(recordId);
        case DimensionContentRecordKind.TravelRequirementEvaluator:
          return travelRequirementEvaluators.ContainsKey(recordId);
        case DimensionContentRecordKind.MapLayer:
          return mapLayers.ContainsKey(recordId);
        case DimensionContentRecordKind.MapMarker:
          return markers.ContainsKey(recordId);
        case DimensionContentRecordKind.Anchor:
          return anchors.ContainsKey(recordId);
        case DimensionContentRecordKind.Biome:
          return biomes.ContainsKey(recordId);
        case DimensionContentRecordKind.GenerationTable:
          return generationTables.ContainsKey(recordId);
        case DimensionContentRecordKind.GenerationTableEntry:
          return generationTableEntries.ContainsKey(recordId);
        case DimensionContentRecordKind.ZoneDefinition:
          return zoneDefinitions.ContainsKey(recordId);
        case DimensionContentRecordKind.ZoneProvider:
          return zoneProviders.ContainsKey(recordId);
        case DimensionContentRecordKind.EnvironmentProfile:
          return environmentProfiles.ContainsKey(recordId);
        case DimensionContentRecordKind.Scene:
          return scenes.ContainsKey(recordId);
        case DimensionContentRecordKind.SceneTemplate:
          return sceneTemplates.ContainsKey(recordId);
        case DimensionContentRecordKind.Encounter:
          return encounters.ContainsKey(recordId);
        case DimensionContentRecordKind.ResourceNode:
          return resourceNodes.ContainsKey(recordId);
        case DimensionContentRecordKind.SpawnRule:
          return spawnRules.ContainsKey(recordId);
        case DimensionContentRecordKind.WorldEvent:
          return worldEvents.ContainsKey(recordId);
        case DimensionContentRecordKind.ProgressFlag:
          return progressFlags.ContainsKey(recordId);
        case DimensionContentRecordKind.GenerationProvider:
          return generationProviders.ContainsKey(recordId);
        case DimensionContentRecordKind.GenerationPass:
          return generationPasses.ContainsKey(recordId);
        case DimensionContentRecordKind.AccessProvider:
          return accessProviders.ContainsKey(recordId);
        case DimensionContentRecordKind.AssetReference:
          return assetReferences.ContainsKey(recordId);
        case DimensionContentRecordKind.Starter:
          return starters.ContainsKey(recordId);
        case DimensionContentRecordKind.Custom:
          return true;
      }

      return false;
    }

    private static bool ContentOwnershipEquals(
        DimensionContentOwnershipBinding a,
        DimensionContentOwnershipBinding b)
    {
      return string.Equals(a.ContentPackId, b.ContentPackId, StringComparison.Ordinal) &&
             a.RecordKind == b.RecordKind &&
             string.Equals(a.RecordId, b.RecordId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.Notes, b.Notes, StringComparison.Ordinal);
    }

    private void RemoveContentOwnershipForContentPack(string contentPackId)
    {
      List<string> keysToRemove = new List<string>();
      foreach (KeyValuePair<string, DimensionContentOwnershipBinding> pair in contentOwnershipBindings)
      {
        if (string.Equals(pair.Value.ContentPackId, contentPackId, StringComparison.Ordinal))
        {
          keysToRemove.Add(pair.Key);
        }
      }

      for (int i = 0; i < keysToRemove.Count; i++)
      {
        DimensionContentOwnershipBinding binding = contentOwnershipBindings[keysToRemove[i]];
        contentOwnershipBindings.Remove(keysToRemove[i]);
        RemovePersistedContentOwnershipIfWorldRegistryLoaded(binding.RecordKind, binding.RecordId);
        RaiseContentOwnershipChanged(
            binding,
            DimensionContentOwnershipChangeKind.OwnerRemoved,
            binding.ContentPackId,
            "content pack removed");
      }
    }
  }
}
