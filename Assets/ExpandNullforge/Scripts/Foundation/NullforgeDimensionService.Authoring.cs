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

    public bool TryBuildDimensionTemplateManifest(
        DimensionTemplateAsset template,
        out DimensionContentManifest manifest,
        out DimensionCompiledGenerationPlan compiledPlan,
        out DimensionOperationResult result)
    {
      return DimensionTemplateManifestBuilder.TryBuildManifest(
          template,
          out manifest,
          out compiledPlan,
          out result);
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

      if (!TryApplyEnvironmentProfileTemplates(template, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplyBiomeTemplates(template, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplyBiomePaletteAssetReferences(template, updateExistingRecords, out result))
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

      if (!TryApplyGenerationTableTemplates(template, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplyBiomeSemanticObjectTables(template, updateExistingRecords, out result))
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

      if (!TryApplyResourceNodes(compiledPlan, updateExistingRecords, out result))
      {
        return false;
      }

      if (!TryApplySpawnRules(compiledPlan, updateExistingRecords, out result))
      {
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyBiomeSemanticObjectTables(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      List<DimensionGenerationTableDefinition> semanticTables =
          new List<DimensionGenerationTableDefinition>();
      List<DimensionGenerationTableEntryDefinition> semanticEntries =
          new List<DimensionGenerationTableEntryDefinition>();
      Dictionary<string, bool> tableIds = new Dictionary<string, bool>();
      Dictionary<string, bool> entryIds = new Dictionary<string, bool>();

      AddExplicitGenerationTableIds(
          template.GlobalGenerationTables,
          template.DimensionId,
          string.Empty,
          tableIds);

      BiomeTemplateAsset[] biomesToApply = template.Biomes;
      for (int i = 0; i < biomesToApply.Length; i++)
      {
        BiomeTemplateAsset biome = biomesToApply[i];
        if (biome == null)
        {
          continue;
        }

        AddExplicitGenerationTableIds(
            biome.GetGenerationTablesWithProfile(),
            template.DimensionId,
            biome.BiomeId,
            tableIds);

        DimensionSemanticObjectTableBuilder.AddBiomeSemanticObjectTables(
            biome,
            template.DimensionId,
            semanticTables,
            semanticEntries,
            tableIds,
            entryIds);
      }

      for (int tableIndex = 0; tableIndex < semanticTables.Count; tableIndex++)
      {
        if (!TryApplyGenerationTable(
                semanticTables[tableIndex],
                updateExistingRecords,
                out result))
        {
          return false;
        }
      }

      for (int entryIndex = 0; entryIndex < semanticEntries.Count; entryIndex++)
      {
        if (!TryApplyGenerationTableEntry(
                semanticEntries[entryIndex],
                updateExistingRecords,
                out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static void AddExplicitGenerationTableIds(
        GenerationTableTemplateAsset[] generationTableAssets,
        string dimensionId,
        string fallbackBiomeId,
        Dictionary<string, bool> tableIds)
    {
      if (generationTableAssets == null || tableIds == null)
      {
        return;
      }

      for (int i = 0; i < generationTableAssets.Length; i++)
      {
        GenerationTableTemplateAsset generationTableAsset = generationTableAssets[i];
        if (generationTableAsset == null || !generationTableAsset.Enabled)
        {
          continue;
        }

        string scopeId = string.IsNullOrEmpty(fallbackBiomeId) ? string.Empty : fallbackBiomeId;
        string tableId = BuildScopedId(dimensionId, scopeId, generationTableAsset.TableId);
        if (string.IsNullOrEmpty(tableId) || tableIds.ContainsKey(tableId))
        {
          continue;
        }

        tableIds.Add(tableId, true);
      }
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

    private bool TryApplyEnvironmentProfileTemplates(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      List<DimensionEnvironmentProfile> environmentProfiles =
          new List<DimensionEnvironmentProfile>();
      Dictionary<string, bool> profileIds = new Dictionary<string, bool>();

      EnvironmentProfileTemplateAsset[] templateProfiles = template.EnvironmentProfiles;
      for (int i = 0; i < templateProfiles.Length; i++)
      {
        AddEnvironmentProfileFromTemplate(
            templateProfiles[i],
            template.DimensionId,
            environmentProfiles,
            profileIds);
      }

      BiomeTemplateAsset[] biomeAssets = template.Biomes;
      for (int i = 0; i < biomeAssets.Length; i++)
      {
        BiomeTemplateAsset biome = biomeAssets[i];
        if (biome == null)
        {
          continue;
        }

        EnvironmentProfileTemplateAsset[] biomeProfiles =
            biome.GetEnvironmentProfileTemplatesWithPresets();
        for (int profileIndex = 0; profileIndex < biomeProfiles.Length; profileIndex++)
        {
          AddEnvironmentProfileFromTemplate(
              biomeProfiles[profileIndex],
              template.DimensionId,
              environmentProfiles,
              profileIds);
        }
      }

      for (int i = 0; i < environmentProfiles.Count; i++)
      {
        if (!TryApplyEnvironmentProfile(
                environmentProfiles[i],
                updateExistingRecords,
                out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static void AddEnvironmentProfileFromTemplate(
        EnvironmentProfileTemplateAsset asset,
        string dimensionId,
        List<DimensionEnvironmentProfile> destination,
        Dictionary<string, bool> profileIds)
    {
      if (asset == null || string.IsNullOrEmpty(asset.ProfileId))
      {
        return;
      }

      DimensionEnvironmentProfile profile = asset.ToEnvironmentProfile(dimensionId);
      if (profileIds.ContainsKey(profile.ProfileId))
      {
        return;
      }

      profileIds.Add(profile.ProfileId, true);
      destination.Add(profile);
    }

    private bool TryApplyEnvironmentProfile(
        DimensionEnvironmentProfile environmentProfile,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      DimensionEnvironmentProfile existing;
      if (TryGetEnvironmentProfile(environmentProfile.ProfileId, out existing))
      {
        if (EnvironmentProfileEquals(existing, environmentProfile))
        {
          result = DimensionOperationResult.Ok();
          return true;
        }

        if (!updateExistingRecords)
        {
          result = DimensionOperationResult.Failed(
              "environment-profile-conflict",
              "An environment profile with this id already exists with different metadata: " + environmentProfile.ProfileId + ".");
          return false;
        }

        return TryUpdateEnvironmentProfile(environmentProfile, "dimension-template-apply", out result);
      }

      return TryRegisterEnvironmentProfile(environmentProfile, out result);
    }

    private bool TryApplyBiomePaletteAssetReferences(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(template.ContentPackId))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      List<DimensionAssetReferenceDefinition> assetReferences =
          new List<DimensionAssetReferenceDefinition>();
      BiomeTemplateAsset[] biomeAssets = template.Biomes;
      for (int i = 0; i < biomeAssets.Length; i++)
      {
        BiomeTemplateAsset biome = biomeAssets[i];
        if (biome == null)
        {
          continue;
        }

        BiomePaletteTemplateAsset[] palettes = biome.GetPaletteTemplatesWithPresets();
        for (int paletteIndex = 0; paletteIndex < palettes.Length; paletteIndex++)
        {
          BiomePaletteTemplateAsset palette = palettes[paletteIndex];
          if (palette == null || !palette.Enabled)
          {
            continue;
          }

          palette.AddAssetReferencesTo(
              template.ContentPackId,
              template.DimensionId,
              assetReferences);
        }
      }

      Dictionary<string, bool> appliedIds = new Dictionary<string, bool>();
      for (int i = 0; i < assetReferences.Count; i++)
      {
        DimensionAssetReferenceDefinition assetReference = assetReferences[i];
        if (string.IsNullOrEmpty(assetReference.AssetId) ||
            appliedIds.ContainsKey(assetReference.AssetId))
        {
          continue;
        }

        appliedIds.Add(assetReference.AssetId, true);
        if (!TryApplyAssetReference(assetReference, updateExistingRecords, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyAssetReference(
        DimensionAssetReferenceDefinition assetReference,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      DimensionAssetReferenceDefinition existing;
      if (TryGetAssetReference(assetReference.AssetId, out existing))
      {
        if (AssetReferenceEquals(existing, assetReference))
        {
          result = DimensionOperationResult.Ok();
          return true;
        }

        if (!updateExistingRecords)
        {
          result = DimensionOperationResult.Failed(
              "asset-reference-conflict",
              "An asset reference with this id already exists with different metadata: " + assetReference.AssetId + ".");
          return false;
        }

        return TryUpdateAssetReference(assetReference, "dimension-template-apply", out result);
      }

      return TryRegisterAssetReference(assetReference, out result);
    }

    private bool TryApplyGenerationTableTemplates(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      if (!TryApplyGenerationTableAssets(
              template.GlobalGenerationTables,
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

        if (!TryApplyGenerationTableAssets(
                biome.GetGenerationTablesWithProfile(),
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

    private bool TryApplyGenerationTableAssets(
        GenerationTableTemplateAsset[] generationTableAssets,
        string dimensionId,
        string fallbackBiomeId,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      if (generationTableAssets == null)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      for (int i = 0; i < generationTableAssets.Length; i++)
      {
        GenerationTableTemplateAsset generationTableAsset = generationTableAssets[i];
        if (generationTableAsset == null || !generationTableAsset.Enabled)
        {
          continue;
        }

        string scopeId = string.IsNullOrEmpty(fallbackBiomeId) ? string.Empty : fallbackBiomeId;
        string tableId = BuildScopedId(dimensionId, scopeId, generationTableAsset.TableId);
        DimensionGenerationTableDefinition table =
            generationTableAsset.ToTableDefinition(tableId, dimensionId, fallbackBiomeId);

        if (!TryApplyGenerationTable(table, updateExistingRecords, out result))
        {
          return false;
        }

        if (!TryApplyGenerationTableEntries(generationTableAsset, table.TableId, updateExistingRecords, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyGenerationTable(
        DimensionGenerationTableDefinition table,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      DimensionGenerationTableDefinition existing;
      if (TryGetGenerationTable(table.TableId, out existing))
      {
        if (GenerationTableEquals(existing, table))
        {
          result = DimensionOperationResult.Ok();
          return true;
        }

        if (!updateExistingRecords)
        {
          result = DimensionOperationResult.Failed(
              "generation-table-conflict",
              "A generation table with this id already exists with different metadata: " + table.TableId + ".");
          return false;
        }

        return TryUpdateGenerationTable(table, "dimension-template-apply", out result);
      }

      return TryRegisterGenerationTable(table, out result);
    }

    private bool TryApplyGenerationTableEntries(
        GenerationTableTemplateAsset generationTableAsset,
        string tableId,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      System.Collections.Generic.List<DimensionGenerationTableEntryDefinition> entries =
          new System.Collections.Generic.List<DimensionGenerationTableEntryDefinition>();
      generationTableAsset.AddEntryDefinitions(tableId, entries);
      for (int i = 0; i < entries.Count; i++)
      {
        DimensionGenerationTableEntryDefinition entry = entries[i];
        if (!TryApplyGenerationTableEntry(entry, updateExistingRecords, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplyGenerationTableEntry(
        DimensionGenerationTableEntryDefinition entry,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      DimensionGenerationTableEntryDefinition existing;
      if (TryGetGenerationTableEntry(entry.EntryId, out existing))
      {
        if (GenerationTableEntryEquals(existing, entry))
        {
          result = DimensionOperationResult.Ok();
          return true;
        }

        if (!updateExistingRecords)
        {
          result = DimensionOperationResult.Failed(
              "generation-table-entry-conflict",
              "A generation table entry with this id already exists with different metadata: " + entry.EntryId + ".");
          return false;
        }

        return TryUpdateGenerationTableEntry(entry, "dimension-template-apply", out result);
      }

      return TryRegisterGenerationTableEntry(entry, out result);
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
        string zoneId = ResolveCompiledZoneId(region);
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
                biome.GetScenePoolWithPresets(),
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

    private bool TryApplyResourceNodes(
        DimensionCompiledGenerationPlan plan,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      for (int i = 0; i < plan.ResourceNodes.Count; i++)
      {
        DimensionResourceNodeDefinition node = plan.ResourceNodes[i];
        DimensionResourceNodeDefinition existing;
        if (TryGetResourceNode(node.NodeId, out existing))
        {
          if (ResourceNodeEquals(existing, node))
          {
            continue;
          }

          if (!updateExistingRecords)
          {
            result = DimensionOperationResult.Failed(
                "resource-node-conflict",
                "A resource node with this id already exists with different metadata: " + node.NodeId + ".");
            return false;
          }

          if (!TryUpdateResourceNode(node, "dimension-template-apply", out result))
          {
            return false;
          }

          continue;
        }

        if (!TryRegisterResourceNode(node, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryApplySpawnRules(
        DimensionCompiledGenerationPlan plan,
        bool updateExistingRecords,
        out DimensionOperationResult result)
    {
      for (int i = 0; i < plan.SpawnRules.Count; i++)
      {
        DimensionSpawnRule rule = plan.SpawnRules[i];
        DimensionSpawnRule existing;
        if (TryGetSpawnRule(rule.RuleId, out existing))
        {
          if (SpawnRuleEquals(existing, rule))
          {
            continue;
          }

          if (!updateExistingRecords)
          {
            result = DimensionOperationResult.Failed(
                "spawn-rule-conflict",
                "A spawn rule with this id already exists with different metadata: " + rule.RuleId + ".");
            return false;
          }

          if (!TryUpdateSpawnRule(rule, "dimension-template-apply", out result))
          {
            return false;
          }

          continue;
        }

        if (!TryRegisterSpawnRule(rule, out result))
        {
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static string ResolveCompiledZoneId(DimensionCompiledBiomeRegion region)
    {
      if (!string.IsNullOrEmpty(region.ZoneId))
      {
        return region.ZoneId;
      }

      if (!string.IsNullOrEmpty(region.SourceTemplateId))
      {
        return region.DimensionId + "." + region.SourceTemplateId;
      }

      return region.DimensionId + "." + region.BiomeId;
    }

    private static string BuildScopedId(string dimensionId, string zoneId, string id)
    {
      string resolvedId = id ?? string.Empty;
      if (string.IsNullOrEmpty(resolvedId))
      {
        return string.Empty;
      }

      if (resolvedId.IndexOf('.') >= 0 || resolvedId.IndexOf(':') >= 0)
      {
        return resolvedId;
      }

      if (!string.IsNullOrEmpty(zoneId))
      {
        if (zoneId.StartsWith(dimensionId + "."))
        {
          return zoneId + "." + resolvedId;
        }

        return dimensionId + "." + zoneId + "." + resolvedId;
      }

      return dimensionId + "." + resolvedId;
    }
  }
}
