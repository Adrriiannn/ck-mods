using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public static class DimensionAuthoringPreviewBuilder
    {
        private const uint PlayableBoundsColor = 0xA7D8FFFF;
        private const uint BiomeRegionColor = 0x58D68D99;
        private const uint ScenePlacementColor = 0xF7DC6FBB;
        private const uint ResourceNodeColor = 0xAF7AC5BB;
        private const uint SpawnRuleColor = 0xEC7063AA;
        private const uint GenerationPassColor = 0x5DADE2AA;

        public static DimensionAuthoringPreviewSummary Build(DimensionTemplateAsset template)
        {
            DimensionCompiledGenerationPlan plan = DimensionTemplateCompiler.Compile(template);
            List<DimensionAuthoringPreviewEntry> entries = new List<DimensionAuthoringPreviewEntry>();
            List<DimensionAuthoringContentSummaryEntry> contentEntries =
                new List<DimensionAuthoringContentSummaryEntry>();

            AddPlayableBounds(plan, entries);
            AddBiomeRegions(plan, entries);
            AddScenePlacements(plan, entries);
            AddResourceNodes(plan, entries);
            AddSpawnRules(plan, entries);
            AddGenerationPassBounds(plan, entries);
            AddContentSummary(template, contentEntries);

            int infoCount;
            int warningCount;
            int errorCount;
            CountIssues(plan.Issues, out infoCount, out warningCount, out errorCount);

            return new DimensionAuthoringPreviewSummary(
                plan.Success,
                plan.Code,
                plan.Message,
                plan.DimensionId,
                plan.DisplayName,
                plan.PlayableLocalBounds,
                plan.CoordinateShellPaddingTiles,
                errorCount,
                warningCount,
                infoCount,
                contentEntries,
                entries,
                plan.Issues);
        }

        private static void AddContentSummary(
            DimensionTemplateAsset template,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (entries == null || template == null)
            {
                return;
            }

            AddTemplateLevelContentSummary(template, entries);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                AddBiomeContentSummary(biome, entries);
            }
        }

        private static void AddTemplateLevelContentSummary(
            DimensionTemplateAsset template,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            AddSummary(
                entries,
                DimensionAuthoringContentSummaryKind.Dimension,
                template.DimensionId,
                template.DisplayName,
                string.Empty,
                string.Empty,
                1,
                template.Description);

            DimensionLayoutTemplateAsset layout = template.LayoutTemplate;
            if (layout != null)
            {
                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.LayoutTemplate,
                    layout.LayoutId,
                    layout.DisplayName,
                    string.Empty,
                    string.Empty,
                    CountLayoutRecords(layout),
                    layout.LayoutKind.ToString());
            }

            AddEnvironmentProfiles(template.EnvironmentProfiles, string.Empty, entries);
            AddSceneTemplates(template.GlobalScenes, string.Empty, "global", entries);
            AddResourceNodes(template.GlobalResourceNodes, string.Empty, "global", entries);
            AddItems(template.GlobalItems, "global", entries);
            AddRecipes(template.GlobalRecipes, "global", entries);
            AddWorkbenches(template.GlobalWorkbenches, "global", entries);
            AddLootTables(template.GlobalLootTables, "global", entries);
            AddAnimals(template.GlobalAnimals, "global", entries);
            AddCritters(template.GlobalCritters, "global", entries);
            AddMobs(template.GlobalMobs, "global", entries);
            AddBosses(template.GlobalBosses, "global", entries);
            AddSpawnRules(template.GlobalSpawnRules, string.Empty, "global", entries);
            AddGenerationPasses(template.GlobalGenerationPasses, string.Empty, "global", entries);
            AddGenerationTables(template.GlobalGenerationTables, string.Empty, "global", entries);
        }

        private static void AddBiomeContentSummary(
            BiomeTemplateAsset biome,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            string biomeId = biome.BiomeId;
            AddSummary(
                entries,
                DimensionAuthoringContentSummaryKind.Biome,
                biomeId,
                biome.DisplayName,
                biomeId,
                biome.ResolvedEnvironmentProfileId,
                biome.Enabled ? 1 : 0,
                biome.Enabled ? "Enabled biome template." : "Disabled biome template.");

            BiomeContentPresetAsset[] presets = biome.ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.BiomeContentPreset,
                    preset.PresetId,
                    preset.DisplayName,
                    biomeId,
                    preset.ResolvedEnvironmentProfileId,
                    preset.Enabled ? 1 : 0,
                    preset.HasAnyContent ? preset.Notes : "Preset is empty.");
            }

            BiomeGenerationProfileAsset generationProfile = biome.GenerationProfile;
            if (generationProfile != null)
            {
                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.BiomeGenerationProfile,
                    generationProfile.ProfileId,
                    generationProfile.DisplayName,
                    biomeId,
                    string.Empty,
                    1,
                    generationProfile.Notes);
            }

            AddEnvironmentProfiles(biome.GetEnvironmentProfileTemplatesWithPresets(), biomeId, entries);
            AddPalettes(biome.GetPaletteTemplatesWithPresets(), biomeId, entries);
            AddSceneTemplates(biome.GetScenePoolWithPresets(), biomeId, "biome", entries);
            AddResourceNodes(biome.GetResourceNodesWithPresets(), biomeId, "biome", entries);
            AddSpawnRules(biome.GetSpawnRulesWithPresets(), biomeId, "biome", entries);
            AddGenerationPasses(biome.GetGenerationPassesWithProfile(), biomeId, "biome", entries);
            AddGenerationTables(biome.GetGenerationTablesWithProfile(), biomeId, "biome", entries);

            AddSemanticObjectSummary(
                biome.GetFloorObjectIdsWithPresets(),
                DimensionAuthoringContentSummaryKind.SemanticFloorObject,
                biomeId,
                "Semantic floor object IDs",
                entries);
            AddSemanticObjectSummary(
                biome.GetWallObjectIdsWithPresets(),
                DimensionAuthoringContentSummaryKind.SemanticWallObject,
                biomeId,
                "Semantic wall object IDs",
                entries);
            AddSemanticObjectSummary(
                biome.GetOreObjectIdsWithPresets(),
                DimensionAuthoringContentSummaryKind.SemanticOreObject,
                biomeId,
                "Semantic ore object IDs",
                entries);
            AddSemanticObjectSummary(
                biome.GetWaterObjectIdsWithPresets(),
                DimensionAuthoringContentSummaryKind.SemanticWaterObject,
                biomeId,
                "Semantic water object IDs",
                entries);
        }

        private static void AddEnvironmentProfiles(
            EnvironmentProfileTemplateAsset[] profiles,
            string biomeId,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (profiles == null)
            {
                return;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                EnvironmentProfileTemplateAsset profile = profiles[i];
                if (profile == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.EnvironmentProfile,
                    profile.ProfileId,
                    profile.DisplayName,
                    biomeId,
                    profile.ZoneId,
                    profile.Enabled ? 1 : 0,
                    "Music=" + profile.MusicCueId + "; Ambient=" + profile.AmbientCueId);
            }
        }

        private static void AddPalettes(
            BiomePaletteTemplateAsset[] palettes,
            string biomeId,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (palettes == null)
            {
                return;
            }

            for (int i = 0; i < palettes.Length; i++)
            {
                BiomePaletteTemplateAsset palette = palettes[i];
                if (palette == null)
                {
                    continue;
                }

                BiomePaletteEntryTemplate[] paletteEntries = palette.Entries;
                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.BiomePalette,
                    palette.PaletteId,
                    palette.DisplayName,
                    biomeId,
                    string.Empty,
                    CountEnabledPaletteEntries(paletteEntries),
                    palette.Notes);

                for (int j = 0; j < paletteEntries.Length; j++)
                {
                    BiomePaletteEntryTemplate entry = paletteEntries[j];
                    if (entry == null)
                    {
                        continue;
                    }

                    AddSummary(
                        entries,
                        DimensionAuthoringContentSummaryKind.BiomePaletteEntry,
                        BuildScopedRecordId(palette.PaletteId, entry.EntryId, j),
                        entry.DisplayName,
                        biomeId,
                        entry.SlotKind.ToString(),
                        entry.Enabled ? 1 : 0,
                        entry.ResourceKey);
                }
            }
        }

        private static void AddSceneTemplates(
            SceneTemplateAsset[] scenes,
            string biomeId,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (scenes == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.SceneTemplate,
                    scene.SceneId,
                    scene.DisplayName,
                    biomeId,
                    scope,
                    scene.Enabled ? 1 : 0,
                    scene.Kind);
                AddSceneContent(scene, biomeId, scope, entries);
            }
        }

        private static void AddSceneContent(
            SceneTemplateAsset scene,
            string biomeId,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (scene == null)
            {
                return;
            }

            DimensionScenePropTemplate[] props = scene.Props;
            for (int i = 0; i < props.Length; i++)
            {
                DimensionScenePropTemplate prop = props[i];
                if (prop == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.SceneProp,
                    BuildScopedRecordId(scene.SceneId, prop.PropId, i),
                    prop.DisplayName,
                    biomeId,
                    scope,
                    prop.Enabled ? 1 : 0,
                    "Object=" + prop.ObjectId);
            }

            DimensionSceneLootContainerTemplate[] lootContainers = scene.LootContainers;
            for (int i = 0; i < lootContainers.Length; i++)
            {
                DimensionSceneLootContainerTemplate container = lootContainers[i];
                if (container == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.SceneLootContainer,
                    BuildScopedRecordId(scene.SceneId, container.ContainerId, i),
                    container.DisplayName,
                    biomeId,
                    scope,
                    container.Enabled ? 1 : 0,
                    "Loot=" + container.LootTableId);
            }

            DimensionSceneSpawnPointTemplate[] spawnPoints = scene.SpawnPoints;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                DimensionSceneSpawnPointTemplate spawnPoint = spawnPoints[i];
                if (spawnPoint == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.SceneSpawnPoint,
                    BuildScopedRecordId(scene.SceneId, spawnPoint.SpawnPointId, i),
                    spawnPoint.DisplayName,
                    biomeId,
                    scope,
                    spawnPoint.Enabled ? 1 : 0,
                    spawnPoint.ResolvedSubjectKind.ToString() + "=" + spawnPoint.ResolvedSubjectId);
            }

            DimensionSceneTriggerTemplate[] triggers = scene.Triggers;
            for (int i = 0; i < triggers.Length; i++)
            {
                DimensionSceneTriggerTemplate trigger = triggers[i];
                if (trigger == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.SceneTrigger,
                    BuildScopedRecordId(scene.SceneId, trigger.TriggerId, i),
                    trigger.DisplayName,
                    biomeId,
                    scope,
                    trigger.Enabled ? 1 : 0,
                    "Event=" + trigger.EventId);
            }
        }

        private static void AddResourceNodes(
            ResourceNodeTemplateAsset[] nodes,
            string biomeId,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                ResourceNodeTemplateAsset node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.ResourceNode,
                    node.NodeId,
                    node.name,
                    biomeId,
                    scope,
                    node.Enabled ? 1 : 0,
                    node.HasLocalBounds ? "Has explicit local bounds." : "Auto-placed.");
            }
        }

        private static void AddItems(
            DimensionItemAsset[] items,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.Item,
                    item.ItemId,
                    item.DisplayName,
                    string.Empty,
                    scope,
                    item.Enabled ? 1 : 0,
                    "Kind=" + item.Kind.ToString() + "; Stack=" + item.MaxStack.ToString());
            }
        }

        private static void AddRecipes(
            DimensionRecipeAsset[] recipes,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (recipes == null)
            {
                return;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                DimensionRecipeAsset recipe = recipes[i];
                if (recipe == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.Recipe,
                    recipe.RecipeId,
                    recipe.DisplayName,
                    string.Empty,
                    scope,
                    CountNonNull(recipe.Ingredients),
                    "Output=" + recipe.OutputAmount.ToString() + "x " + recipe.OutputItemId);
            }
        }

        private static void AddWorkbenches(
            DimensionWorkbenchAsset[] workbenches,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (workbenches == null)
            {
                return;
            }

            for (int i = 0; i < workbenches.Length; i++)
            {
                DimensionWorkbenchAsset workbench = workbenches[i];
                if (workbench == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.Workbench,
                    workbench.WorkbenchId,
                    workbench.DisplayName,
                    string.Empty,
                    scope,
                    CountNonNull(workbench.Recipes),
                    "Object=" + workbench.ObjectId);
            }
        }

        private static void AddLootTables(
            DimensionLootTableAsset[] lootTables,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (lootTables == null)
            {
                return;
            }

            for (int i = 0; i < lootTables.Length; i++)
            {
                DimensionLootTableAsset lootTable = lootTables[i];
                if (lootTable == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.LootTable,
                    lootTable.LootTableId,
                    lootTable.DisplayName,
                    string.Empty,
                    scope,
                    lootTable.EnabledEntryCount,
                    lootTable.AllowEmptyRoll ? "Allows empty rolls." : "Requires at least one winning entry.");
            }
        }

        private static void AddAnimals(
            DimensionAnimalAsset[] animals,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (animals == null)
            {
                return;
            }

            for (int i = 0; i < animals.Length; i++)
            {
                DimensionAnimalAsset animal = animals[i];
                if (animal == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.Animal,
                    animal.AnimalId,
                    animal.DisplayName,
                    string.Empty,
                    scope,
                    animal.Enabled ? 1 : 0,
                    "Object=" + animal.ObjectId + "; Loot=" + DimensionSpawnableAuthoringUtility.ResolveLootTableId(animal.LootTable));
            }
        }

        private static void AddCritters(
            DimensionCritterAsset[] critters,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (critters == null)
            {
                return;
            }

            for (int i = 0; i < critters.Length; i++)
            {
                DimensionCritterAsset critter = critters[i];
                if (critter == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.Critter,
                    critter.CritterId,
                    critter.DisplayName,
                    string.Empty,
                    scope,
                    critter.Enabled ? 1 : 0,
                    "Object=" + critter.ObjectId);
            }
        }

        private static void AddMobs(
            DimensionMobAsset[] mobs,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (mobs == null)
            {
                return;
            }

            for (int i = 0; i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.Mob,
                    mob.MobId,
                    mob.DisplayName,
                    string.Empty,
                    scope,
                    mob.Enabled ? 1 : 0,
                    "Object=" + mob.ObjectId + "; Aggression=" + mob.Aggression.ToString());
            }
        }

        private static void AddBosses(
            DimensionBossAsset[] bosses,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.Boss,
                    boss.BossId,
                    boss.DisplayName,
                    string.Empty,
                    scope,
                    boss.Enabled ? 1 : 0,
                    "Arena=" + boss.ArenaSceneId + "; Phases=" + CountNonNull(boss.Phases).ToString());
            }
        }

        private static void AddSpawnRules(
            SpawnRuleTemplateAsset[] rules,
            string biomeId,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (rules == null)
            {
                return;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                SpawnRuleTemplateAsset rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.SpawnRule,
                    rule.RuleId,
                    rule.name,
                    biomeId,
                    scope,
                    rule.Enabled ? 1 : 0,
                    rule.HasLocalBounds ? "Has explicit local bounds." : "Auto-spawn area.");
            }
        }

        private static void AddGenerationPasses(
            GenerationPassTemplateAsset[] passes,
            string biomeId,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (passes == null)
            {
                return;
            }

            for (int i = 0; i < passes.Length; i++)
            {
                GenerationPassTemplateAsset pass = passes[i];
                if (pass == null)
                {
                    continue;
                }

                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.GenerationPass,
                    pass.PassId,
                    pass.DisplayName,
                    biomeId,
                    scope,
                    pass.Enabled ? 1 : 0,
                    pass.ProviderId);
            }
        }

        private static void AddGenerationTables(
            GenerationTableTemplateAsset[] tables,
            string biomeId,
            string scope,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (tables == null)
            {
                return;
            }

            for (int i = 0; i < tables.Length; i++)
            {
                GenerationTableTemplateAsset table = tables[i];
                if (table == null)
                {
                    continue;
                }

                GenerationTableEntryTemplate[] tableEntries = table.Entries;
                AddSummary(
                    entries,
                    DimensionAuthoringContentSummaryKind.GenerationTable,
                    table.TableId,
                    table.DisplayName,
                    string.IsNullOrEmpty(table.BiomeIdOverride) ? biomeId : table.BiomeIdOverride,
                    scope,
                    CountNonNull(tableEntries),
                    table.Kind.ToString());

                for (int j = 0; j < tableEntries.Length; j++)
                {
                    GenerationTableEntryTemplate tableEntry = tableEntries[j];
                    if (tableEntry == null)
                    {
                        continue;
                    }

                    AddSummary(
                        entries,
                        DimensionAuthoringContentSummaryKind.GenerationTableEntry,
                        BuildScopedRecordId(table.TableId, tableEntry.EntryId, j),
                        tableEntry.EntryId,
                        string.IsNullOrEmpty(table.BiomeIdOverride) ? biomeId : table.BiomeIdOverride,
                        scope,
                        1,
                        "Entry in " + table.Kind.ToString() + " generation table.");
                }
            }
        }

        private static void AddSemanticObjectSummary(
            string[] objectIds,
            DimensionAuthoringContentSummaryKind kind,
            string biomeId,
            string label,
            List<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (objectIds == null)
            {
                return;
            }

            AddSummary(
                entries,
                kind,
                BuildScopedRecordId(biomeId, kind.ToString(), 0),
                label,
                biomeId,
                string.Empty,
                CountNonEmpty(objectIds),
                "Expanded into scoped generation tables by the authoring compiler.");
        }

        private static void AddSummary(
            List<DimensionAuthoringContentSummaryEntry> entries,
            DimensionAuthoringContentSummaryKind kind,
            string recordId,
            string displayName,
            string biomeId,
            string zoneId,
            int count,
            string notes)
        {
            if (entries == null)
            {
                return;
            }

            entries.Add(new DimensionAuthoringContentSummaryEntry(
                kind,
                recordId,
                string.IsNullOrEmpty(displayName) ? recordId : displayName,
                biomeId,
                zoneId,
                count,
                notes));
        }

        private static void AddPlayableBounds(
            DimensionCompiledGenerationPlan plan,
            List<DimensionAuthoringPreviewEntry> entries)
        {
            if (!IsValidBounds(plan.PlayableLocalBounds))
            {
                return;
            }

            entries.Add(new DimensionAuthoringPreviewEntry(
                DimensionAuthoringPreviewLayerKind.PlayableBounds,
                plan.DimensionId + ".playable",
                "Playable Bounds",
                string.Empty,
                string.Empty,
                true,
                plan.PlayableLocalBounds,
                -100000,
                PlayableBoundsColor));
        }

        private static void AddBiomeRegions(
            DimensionCompiledGenerationPlan plan,
            List<DimensionAuthoringPreviewEntry> entries)
        {
            if (plan.BiomeRegions == null)
            {
                return;
            }

            for (int i = 0; i < plan.BiomeRegions.Count; i++)
            {
                DimensionCompiledBiomeRegion region = plan.BiomeRegions[i];
                entries.Add(new DimensionAuthoringPreviewEntry(
                    DimensionAuthoringPreviewLayerKind.BiomeRegion,
                    string.IsNullOrEmpty(region.SourceTemplateId) ? region.BiomeId : region.SourceTemplateId,
                    region.DisplayName,
                    region.BiomeId,
                    region.ZoneId,
                    true,
                    region.LocalBounds,
                    region.Priority,
                    BiomeRegionColor));
            }
        }

        private static void AddScenePlacements(
            DimensionCompiledGenerationPlan plan,
            List<DimensionAuthoringPreviewEntry> entries)
        {
            if (plan.ScenePlacements == null)
            {
                return;
            }

            for (int i = 0; i < plan.ScenePlacements.Count; i++)
            {
                DimensionCompiledScenePlacement scene = plan.ScenePlacements[i];
                entries.Add(new DimensionAuthoringPreviewEntry(
                    DimensionAuthoringPreviewLayerKind.ScenePlacement,
                    scene.SceneId,
                    scene.DisplayName,
                    scene.BiomeId,
                    string.Empty,
                    scene.HasLocalBounds,
                    scene.LocalBounds,
                    scene.Priority,
                    ScenePlacementColor));
            }
        }

        private static void AddResourceNodes(
            DimensionCompiledGenerationPlan plan,
            List<DimensionAuthoringPreviewEntry> entries)
        {
            if (plan.ResourceNodes == null)
            {
                return;
            }

            for (int i = 0; i < plan.ResourceNodes.Count; i++)
            {
                DimensionResourceNodeDefinition node = plan.ResourceNodes[i];
                entries.Add(new DimensionAuthoringPreviewEntry(
                    DimensionAuthoringPreviewLayerKind.ResourceNode,
                    node.NodeId,
                    node.DisplayName,
                    string.Empty,
                    node.ZoneId,
                    node.HasLocalBounds,
                    node.LocalBounds,
                    node.Priority,
                    ResourceNodeColor));
            }
        }

        private static void AddSpawnRules(
            DimensionCompiledGenerationPlan plan,
            List<DimensionAuthoringPreviewEntry> entries)
        {
            if (plan.SpawnRules == null)
            {
                return;
            }

            for (int i = 0; i < plan.SpawnRules.Count; i++)
            {
                DimensionSpawnRule rule = plan.SpawnRules[i];
                entries.Add(new DimensionAuthoringPreviewEntry(
                    DimensionAuthoringPreviewLayerKind.SpawnRule,
                    rule.RuleId,
                    rule.DisplayName,
                    string.Empty,
                    rule.ZoneId,
                    rule.HasLocalBounds,
                    rule.LocalBounds,
                    rule.Priority,
                    SpawnRuleColor));
            }
        }

        private static void AddGenerationPassBounds(
            DimensionCompiledGenerationPlan plan,
            List<DimensionAuthoringPreviewEntry> entries)
        {
            if (plan.GenerationPasses == null)
            {
                return;
            }

            for (int i = 0; i < plan.GenerationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition pass = plan.GenerationPasses[i];
                entries.Add(new DimensionAuthoringPreviewEntry(
                    DimensionAuthoringPreviewLayerKind.GenerationPassBounds,
                    pass.PassId,
                    pass.DisplayName,
                    string.Empty,
                    pass.ZoneId,
                    pass.HasLocalBounds,
                    pass.LocalBounds,
                    pass.Priority,
                GenerationPassColor));
            }
        }

        private static int CountLayoutRecords(DimensionLayoutTemplateAsset layout)
        {
            if (layout == null)
            {
                return 0;
            }

            if (layout.LayoutKind == DimensionLayoutKind.ManualRegions)
            {
                return CountNonNull(layout.Regions);
            }

            if (layout.LayoutKind == DimensionLayoutKind.GridRegions)
            {
                return CountNonNull(layout.GridCells);
            }

            if (layout.LayoutKind == DimensionLayoutKind.RadialRings)
            {
                return CountNonNull(layout.RadialRings);
            }

            if (layout.LayoutKind == DimensionLayoutKind.PaintedMask)
            {
                return CountNonNull(layout.MaskBiomeMappings);
            }

            if (layout.LayoutKind == DimensionLayoutKind.Hybrid)
            {
                return CountNonNull(layout.Regions) +
                    CountNonNull(layout.GridCells) +
                    CountNonNull(layout.RadialRings) +
                    CountNonNull(layout.MaskBiomeMappings);
            }

            return 0;
        }

        private static int CountEnabledPaletteEntries(BiomePaletteEntryTemplate[] entries)
        {
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                BiomePaletteEntryTemplate entry = entries[i];
                if (entry != null && entry.Enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountNonNull<T>(T[] entries)
            where T : class
        {
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountNonEmpty(string[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildScopedRecordId(string scopeId, string recordId, int index)
        {
            string resolvedRecordId = string.IsNullOrEmpty(recordId) ? "entry-" + index.ToString() : recordId;
            if (resolvedRecordId.IndexOf('.') >= 0 || resolvedRecordId.IndexOf(':') >= 0)
            {
                return resolvedRecordId;
            }

            if (string.IsNullOrEmpty(scopeId))
            {
                return resolvedRecordId;
            }

            return scopeId + "." + resolvedRecordId;
        }

        private static void CountIssues(
            IReadOnlyList<DimensionAuthoringIssue> issues,
            out int infoCount,
            out int warningCount,
            out int errorCount)
        {
            infoCount = 0;
            warningCount = 0;
            errorCount = 0;
            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                if (issue.Severity == DimensionAuthoringSeverity.Error)
                {
                    errorCount++;
                }
                else if (issue.Severity == DimensionAuthoringSeverity.Warning)
                {
                    warningCount++;
                }
                else
                {
                    infoCount++;
                }
            }
        }

        private static bool IsValidBounds(DimensionBounds bounds)
        {
            int2 size = bounds.Size;
            return size.x > 0 && size.y > 0;
        }
    }
}
