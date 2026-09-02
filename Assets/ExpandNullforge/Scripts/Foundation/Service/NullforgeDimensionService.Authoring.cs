using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using System.Collections.Generic;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public DimensionCompiledGenerationPlan CompileDimensionTemplate(DimensionTemplateAsset template)
    {
      return DimensionTemplateCompiler.Compile(template);
    }

    public bool TryApplyDimensionTemplate(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionCompiledGenerationPlan compiledPlan,
        out DimensionOperationResult result)
    {
      compiledPlan = CompileDimensionTemplate(template);
      if (!compiledPlan.Success)
      {
        result = DimensionOperationResult.Failed(compiledPlan.Code, compiledPlan.Message);
        return false;
      }

      if (template == null)
      {
        result = DimensionOperationResult.Failed("template-null", "Dimension template is missing.");
        return false;
      }

      if (!TryApplyTemplateContentPack(template, updateExistingRecords, out result))
      {
        return false;
      }

      DimensionDefinition dimension = template.ToDimensionDefinition();
      if (!TryApplyDimensionDefinition(dimension, out result))
      {
        return false;
      }

      if (!TryApplyBiomeTemplates(template, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplyCompiledBiomeRegions(compiledPlan, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplyGenerationPasses(compiledPlan, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplySceneTemplates(template, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplyCompiledScenePlacements(compiledPlan, updateExistingRecords, out result))
      {
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyTemplateContentPack(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(template.ContentPackId))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionContentPackDefinition contentPack =
          new DimensionContentPackDefinition(
              template.ContentPackId,
              template.ContentPackDisplayName,
              template.ContentPackVersion,
              template.ContentPackAuthor,
              template.Description,
              template.MinimumApiVersion,
              template.DependencyContentPackIds,
              true);

      DimensionContentPackDefinition existing;
      if (TryGetContentPack(contentPack.ContentPackId, out existing))
      {
        if (ContentPackEquals(existing, contentPack))
        {
          result = DimensionOperationResult.Ok();
          return true;
        }

        if (!updateExistingRecords)
        {
          result = DimensionOperationResult.Failed(
              "content-pack-conflict",
              "A content pack with this id already exists with different metadata: " + contentPack.ContentPackId + ".");
          return false;
        }

        return TryUpdateContentPack(contentPack, "dimension-template-apply", out result);
      }

      return TryRegisterContentPack(contentPack, out result);
    }

    private bool TryApplyDimensionDefinition(
        DimensionDefinition dimension,
        out DimensionOperationResult result)
    {
      DimensionDefinition existing;
      if (TryGetDimension(dimension.Id, out existing))
      {
        if (DimensionDefinitionEquals(existing, dimension))
        {
          result = DimensionOperationResult.Ok();
          return true;
        }

        result = DimensionOperationResult.Failed(
            "dimension-definition-conflict",
            "A dimension with this id already exists with different bounds or metadata. Rename the dimension or reserve a different absolute area.");
        return false;
      }

      return TryRegisterDimension(dimension, out result);
    }

    private bool TryApplyBiomeTemplates(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      BiomeTemplateAsset[] biomesToApply = template.Biomes;
      for (int i = 0; i < biomesToApply.Length; i++)
      {
        BiomeTemplateAsset biomeTemplate = biomesToApply[i];
        if (biomeTemplate == null)
        {
          continue;
        }

        DimensionBiomeDefinition biome = biomeTemplate.ToBiomeDefinition(template.DimensionId);
        DimensionBiomeDefinition existing;
        if (TryGetBiome(biome.BiomeId, out existing))
        {
          if (BiomeEquals(existing, biome))
          {
            continue;
          }

          if (!updateExistingRecords)
          {
            result = DimensionOperationResult.Failed(
                "biome-definition-conflict",
                "A biome with this id already exists with different metadata: " + biome.BiomeId + ".");
            return false;
          }

          if (!TryUpdateBiome(biome, "dimension-template-apply", out result))
          {
            return false;
          }

          continue;
        }

        if (!TryRegisterBiome(biome, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyCompiledBiomeRegions(
        DimensionCompiledGenerationPlan plan,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      for (int i = 0; i < plan.BiomeRegions.Count; i++)
      {
        DimensionCompiledBiomeRegion region = plan.BiomeRegions[i];
        string zoneId = DimensionTemplateCompiler.ResolveCompiledZoneId(region);
        DimensionZoneDefinition zone =
            new DimensionZoneDefinition(
                zoneId,
                string.IsNullOrEmpty(region.DisplayName) ? region.BiomeId : region.DisplayName,
                region.DimensionId,
                region.LocalBounds,
                region.BiomeId,
                region.Priority,
                true);

        DimensionZoneDefinition existing;
        if (TryGetZoneDefinition(zone.ZoneId, out existing))
        {
          if (ZoneDefinitionEquals(existing, zone))
          {
            continue;
          }

          if (!updateExistingRecords)
          {
            result = DimensionOperationResult.Failed(
                "zone-definition-conflict",
                "A zone with this id already exists with different bounds or metadata: " + zone.ZoneId + ".");
            return false;
          }

          if (!TryUpdateZoneDefinition(zone, "dimension-template-apply", out result))
          {
            return false;
          }

          continue;
        }

        if (!TryRegisterZoneDefinition(zone, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyGenerationPasses(
        DimensionCompiledGenerationPlan plan,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      for (int i = 0; i < plan.GenerationPasses.Count; i++)
      {
        DimensionGenerationPassDefinition generationPass = plan.GenerationPasses[i];
        DimensionGenerationPassDefinition existing;
        if (TryGetGenerationPass(generationPass.PassId, out existing))
        {
          if (GenerationPassEquals(existing, generationPass))
          {
            continue;
          }

          if (!updateExistingRecords)
          {
            result = DimensionOperationResult.Failed(
                "generation-pass-conflict",
                "A generation pass with this id already exists with different metadata: " + generationPass.PassId + ".");
            return false;
          }

          if (!TryUpdateGenerationPass(generationPass, "dimension-template-apply", out result))
          {
            return false;
          }

          continue;
        }

        if (!TryRegisterGenerationPass(generationPass, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplySceneTemplates(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      if (!TryApplySceneTemplateAssets(
              template.GlobalScenes,
              template.DimensionId,
              string.Empty,
              updateExistingRecords,
              out result))
      {
        return false;
      }

      BiomeTemplateAsset[] biomesToApply = template.Biomes;
      for (int i = 0; i < biomesToApply.Length; i++)
      {
        BiomeTemplateAsset biome = biomesToApply[i];
        if (biome == null)
        {
          continue;
        }

        if (!TryApplySceneTemplateAssets(
                biome.ScenePool,
                template.DimensionId,
                biome.BiomeId,
                updateExistingRecords,
                out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplySceneTemplateAssets(
        SceneTemplateAsset[] sceneAssets,
        string dimensionId,
        string fallbackZoneId,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      if (sceneAssets == null)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      for (int i = 0; i < sceneAssets.Length; i++)
      {
        SceneTemplateAsset sceneAsset = sceneAssets[i];
        if (sceneAsset == null)
        {
          continue;
        }

        // The placement policy goes into the pool the placement pass reads. Registered before
        // any equality short-circuit below, because the pool is a static that empties on domain
        // reload while these template records persist — an early continue would leave the
        // policy lost exactly when it looks already-applied. In this direct-apply path the
        // scene's tile data is registered under the raw scene id (there is no mod name to
        // qualify with); the built mod's bootstrap uses the qualified name instead.
        if (sceneAsset.Enabled)
        {
          Scenes.DimensionScenePoolRegistry.Register(
              dimensionId,
              new Scenes.DimensionScenePoolEntry(
                  sceneAsset.SceneId,
                  sceneAsset.SceneId,
                  fallbackZoneId ?? string.Empty,
                  sceneAsset.AllowedBiomeIds,
                  sceneAsset.PlacementMode,
                  new Unity.Mathematics.int2(sceneAsset.ExactLocalPosition.x, sceneAsset.ExactLocalPosition.y),
                  sceneAsset.PreferredLocalBounds,
                  sceneAsset.PlacementMode == DimensionScenePlacementMode.PreferredBounds,
                  sceneAsset.MinRadiusTiles,
                  sceneAsset.MaxRadiusTiles,
                  sceneAsset.RadialBiomeId,
                  new Unity.Mathematics.int2(sceneAsset.FootprintSize.x, sceneAsset.FootprintSize.y),
                  sceneAsset.Weight,
                  sceneAsset.Unique,
                  sceneAsset.Required,
                  sceneAsset.Priority));
        }

        DimensionSceneTemplateDefinition template =
            sceneAsset.ToTemplateDefinition(dimensionId, fallbackZoneId);

        DimensionSceneTemplateDefinition existing;
        if (TryGetSceneTemplate(template.TemplateId, out existing))
        {
          if (SceneTemplateEquals(existing, template))
          {
            continue;
          }

          if (!updateExistingRecords)
          {
            result = DimensionOperationResult.Failed(
                "scene-template-conflict",
                "A scene template with this id already exists with different metadata: " + template.TemplateId + ".");
            return false;
          }

          if (!TryUpdateSceneTemplate(template, "dimension-template-apply", out result))
          {
            return false;
          }

          continue;
        }

        if (!TryRegisterSceneTemplate(template, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyCompiledScenePlacements(
        DimensionCompiledGenerationPlan plan,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      for (int i = 0; i < plan.ScenePlacements.Count; i++)
      {
        DimensionCompiledScenePlacement placement = plan.ScenePlacements[i];
        if (!placement.HasLocalBounds)
        {
          continue;
        }

        DimensionSceneDefinition scene =
            new DimensionSceneDefinition(
                placement.SceneId,
                placement.DisplayName,
                placement.DimensionId,
                placement.LocalBounds,
                string.IsNullOrEmpty(placement.BiomeId) ? "scene" : placement.BiomeId,
                placement.Priority,
                DimensionSceneState.Planned);

        DimensionSceneDefinition existing;
        if (TryGetScene(scene.SceneId, out existing))
        {
          if (SceneEquals(existing, scene))
          {
            continue;
          }

          if (!updateExistingRecords)
          {
            result = DimensionOperationResult.Failed(
                "scene-definition-conflict",
                "A scene with this id already exists with different bounds or metadata: " + scene.SceneId + ".");
            return false;
          }

          if (!TryUpdateScene(scene, "dimension-template-apply", out result))
          {
            return false;
          }

          continue;
        }

        if (!TryRegisterScene(scene, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

  }
}
