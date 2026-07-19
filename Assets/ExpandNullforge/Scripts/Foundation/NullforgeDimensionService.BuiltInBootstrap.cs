using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private void AddDefinitionInternal(DimensionDefinition definition)
    {
      definitions[definition.Id] = definition;
    }

    private void AddContentPackInternal(DimensionContentPackDefinition contentPack)
    {
      contentPacks[contentPack.ContentPackId] = contentPack;
    }

    private void AddMapLayerInternal(DimensionMapLayerDefinition layer)
    {
      mapLayers[layer.LayerId] = layer;
    }

    private void AddZoneDefinitionInternal(DimensionZoneDefinition zone)
    {
      zoneDefinitions[zone.ZoneId] = zone;
    }

    private void AddEnvironmentProfileInternal(DimensionEnvironmentProfile profile)
    {
      environmentProfiles[profile.ProfileId] = profile;
    }

    private void AddBiomeInternal(DimensionBiomeDefinition biome)
    {
      biomes[biome.BiomeId] = biome;
    }

    private void AddSceneTemplateInternal(DimensionSceneTemplateDefinition template)
    {
      sceneTemplates[template.TemplateId] = template;
    }

    private void AddSceneInternal(DimensionSceneDefinition scene)
    {
      scenes[scene.SceneId] = scene;
    }

    private void AddResourceNodeInternal(DimensionResourceNodeDefinition node)
    {
      resourceNodes[node.NodeId] = node;
    }

    private void AddSpawnRuleInternal(DimensionSpawnRule rule)
    {
      spawnRules[rule.RuleId] = rule;
    }

    private static void AddContentReadinessRequirement(
        List<DimensionContentReadinessRequirement> requirements,
        DimensionContentRecordKind recordKind,
        string recordId,
        string displayName)
    {
      requirements.Add(
          new DimensionContentReadinessRequirement(
              recordKind,
              recordId,
              displayName,
              true,
              BuiltInFrameworkContentPack.ContentPackId,
              true));
    }

    private void CheckContentReadinessRequirement(
        DimensionContentReadinessRequirement requirement,
        List<DimensionContentReadinessIssue> issues)
    {
      if (!IsValidContentRecordKind(requirement.RecordKind))
      {
        issues.Add(
            new DimensionContentReadinessIssue(
                requirement.RecordKind,
                requirement.RecordId,
                "content-kind-invalid",
                "The content readiness requirement uses an unknown record kind."));
        return;
      }

      if (string.IsNullOrEmpty(requirement.RecordId))
      {
        issues.Add(
            new DimensionContentReadinessIssue(
                requirement.RecordKind,
                requirement.RecordId,
                "content-id-empty",
                "The content readiness requirement has an empty record id."));
        return;
      }

      if (!ContentRecordExists(requirement.RecordKind, requirement.RecordId))
      {
        issues.Add(
            new DimensionContentReadinessIssue(
                requirement.RecordKind,
                requirement.RecordId,
                "content-record-missing",
                string.IsNullOrEmpty(requirement.DisplayName)
                    ? "A required content record is not registered."
                    : requirement.DisplayName + " is not registered."));
        return;
      }

      bool ownershipRequired =
          requirement.RequireOwnership ||
          !string.IsNullOrEmpty(requirement.RequiredOwnerContentPackId) ||
          requirement.RequireEnabledOwnerContentPack;
      if (!ownershipRequired)
      {
        return;
      }

      DimensionContentOwnershipBinding owner;
      if (!TryGetContentOwner(requirement.RecordKind, requirement.RecordId, out owner))
      {
        issues.Add(
            new DimensionContentReadinessIssue(
                requirement.RecordKind,
                requirement.RecordId,
                "content-owner-missing",
                string.IsNullOrEmpty(requirement.DisplayName)
                    ? "A required content record has no content-pack owner binding."
                    : requirement.DisplayName + " has no content-pack owner binding."));
        return;
      }

      if (!string.IsNullOrEmpty(requirement.RequiredOwnerContentPackId) &&
          !string.Equals(
              owner.ContentPackId,
              requirement.RequiredOwnerContentPackId,
              StringComparison.Ordinal))
      {
        issues.Add(
            new DimensionContentReadinessIssue(
                requirement.RecordKind,
                requirement.RecordId,
                "content-owner-mismatch",
                string.IsNullOrEmpty(requirement.DisplayName)
                    ? "A required content record is owned by a different content pack."
                    : requirement.DisplayName + " is owned by a different content pack."));
        return;
      }

      if (requirement.RequireEnabledOwnerContentPack &&
          !ContentPackExistsAndEnabled(owner.ContentPackId, true))
      {
        issues.Add(
            new DimensionContentReadinessIssue(
                requirement.RecordKind,
                requirement.RecordId,
                "content-owner-disabled",
                string.IsNullOrEmpty(requirement.DisplayName)
                    ? "A required content record is owned by a missing or disabled content pack."
                    : requirement.DisplayName + " is owned by a missing or disabled content pack."));
      }
    }

    private void RegisterBuiltInAccessProviders()
    {
      if (accessProviders.ContainsKey(BuiltInTravelRequirementAccessProviderId))
      {
        return;
      }

      accessProviders[BuiltInTravelRequirementAccessProviderId] =
          new TravelRequirementAccessProvider(this);
      accessProviderOrderDirty = true;
    }

    private void RegisterBuiltInTravelRequirementEvaluators()
    {
      if (travelRequirementEvaluators.ContainsKey(BuiltInProgressFlagRequirementEvaluatorId))
      {
        return;
      }

      travelRequirementEvaluators[BuiltInProgressFlagRequirementEvaluatorId] =
          new ProgressFlagTravelRequirementEvaluator(this);
      travelRequirementEvaluatorOrderDirty = true;
    }

    private void BindBuiltInContentOwnership()
    {
      BindBuiltInContentOwnership(
          DimensionContentRecordKind.Dimension,
          OverworldDefinition.Id,
          OverworldDefinition.DisplayName);
      BindBuiltInContentOwnership(
          DimensionContentRecordKind.MapLayer,
          OverworldMapLayerDefinition.LayerId,
          OverworldMapLayerDefinition.DisplayName);
      BindBuiltInContentOwnership(
          DimensionContentRecordKind.AccessProvider,
          BuiltInTravelRequirementAccessProviderId,
          "Built-in travel access provider");
      BindBuiltInContentOwnership(
          DimensionContentRecordKind.TravelRequirementEvaluator,
          BuiltInProgressFlagRequirementEvaluatorId,
          "Built-in progress-flag travel requirement evaluator");
    }

    private void BindBuiltInContentOwnership(
        DimensionContentRecordKind recordKind,
        string recordId,
        string displayName)
    {
      DimensionOperationResult ignored;
      TryBindContentOwnership(
          new DimensionContentOwnershipBinding(
              BuiltInFrameworkContentPack.ContentPackId,
              recordKind,
              recordId,
              displayName,
              "Built-in ExpandNullforge framework content."),
          true,
          "built-in framework startup",
          out ignored);
    }
  }
}
