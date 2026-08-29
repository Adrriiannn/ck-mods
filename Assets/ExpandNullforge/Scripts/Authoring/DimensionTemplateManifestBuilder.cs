using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public static class DimensionTemplateManifestBuilder
    {
        public static bool TryBuildManifest(
            DimensionTemplateAsset template,
            out DimensionContentManifest manifest,
            out DimensionCompiledGenerationPlan compiledPlan,
            out DimensionOperationResult result)
        {
            compiledPlan = DimensionTemplateCompiler.Compile(template);
            if (!compiledPlan.Success)
            {
                manifest = CreateEmptyManifest();
                result = DimensionOperationResult.Failed(
                    compiledPlan.Code,
                    BuildValidationFailedMessage(compiledPlan));
                return false;
            }

            if (template == null)
            {
                manifest = CreateEmptyManifest();
                result = DimensionOperationResult.Failed("template-null", "Dimension template is missing.");
                return false;
            }

            List<DimensionContentPackDefinition> contentPacks = new List<DimensionContentPackDefinition>();
            List<DimensionDefinition> dimensions = new List<DimensionDefinition>();
            List<DimensionZoneDefinition> zones = new List<DimensionZoneDefinition>();
            List<DimensionSceneTemplateDefinition> sceneTemplates = new List<DimensionSceneTemplateDefinition>();
            List<DimensionSceneDefinition> scenes = new List<DimensionSceneDefinition>();
            List<DimensionGenerationPassDefinition> generationPasses =
                new List<DimensionGenerationPassDefinition>(compiledPlan.GenerationPasses);
            List<DimensionBiomeDefinition> biomes = new List<DimensionBiomeDefinition>();
            List<DimensionAssetReferenceDefinition> assetReferences =
                new List<DimensionAssetReferenceDefinition>();
            List<DimensionContentOwnershipBinding> ownershipBindings =
                new List<DimensionContentOwnershipBinding>();
            List<DimensionPortalDefinition> portals = new List<DimensionPortalDefinition>();
            List<DimensionPortalPresentationDefinition> portalPresentations =
                new List<DimensionPortalPresentationDefinition>();
            List<DimensionTravelRequirementDefinition> travelRequirements =
                new List<DimensionTravelRequirementDefinition>();
            List<DimensionStarterDefinition> starters = new List<DimensionStarterDefinition>();
            Dictionary<string, bool> ownershipKeys = new Dictionary<string, bool>();

            string dimensionId = template.DimensionId;
            string contentPackId = template.ContentPackId;
            bool hasContentPack = !string.IsNullOrEmpty(contentPackId);

            if (hasContentPack)
            {
                contentPacks.Add(new DimensionContentPackDefinition(
                    contentPackId,
                    template.ContentPackDisplayName,
                    template.ContentPackVersion,
                    template.ContentPackAuthor,
                    template.Description,
                    template.MinimumApiVersion,
                    template.DependencyContentPackIds,
                    true));
            }

            DimensionDefinition dimension = template.ToDimensionDefinition(compiledPlan.ReservedLocalBounds);
            dimensions.Add(dimension);
            AddOwnership(
                hasContentPack,
                contentPackId,
                DimensionContentRecordKind.Dimension,
                dimension.Id,
                dimension.DisplayName,
                ownershipBindings,
                ownershipKeys);

            AddBiomeDefinitions(template, contentPackId, hasContentPack, biomes, ownershipBindings, ownershipKeys);
            AddZoneDefinitions(compiledPlan, contentPackId, hasContentPack, zones, ownershipBindings, ownershipKeys);
            AddSceneTemplates(template, dimensionId, contentPackId, hasContentPack, sceneTemplates, ownershipBindings, ownershipKeys);
            AddSceneDefinitions(compiledPlan, contentPackId, hasContentPack, scenes, ownershipBindings, ownershipKeys);
            AddAuthoredContentOwnership(template, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddGenerationPassOwnership(contentPackId, hasContentPack, generationPasses, ownershipBindings, ownershipKeys);
            AddAuthoredContentAssetReferences(
                template,
                contentPackId,
                hasContentPack,
                assetReferences,
                ownershipBindings,
                ownershipKeys);
            AddPortalAccessRules(
                template,
                contentPackId,
                hasContentPack,
                portals,
                portalPresentations,
                travelRequirements,
                ownershipBindings,
                ownershipKeys);
            AddStarterDefinitions(
                template,
                compiledPlan,
                contentPackId,
                hasContentPack,
                portals,
                starters,
                ownershipBindings,
                ownershipKeys);

            DimensionContentManifest baseManifest = new DimensionContentManifest(
                contentPacks,
                dimensions,
                zones,
                new List<DimensionMapLayerDefinition>(),
                new List<DimensionMapMarker>(),
                new List<DimensionAnchorDefinition>(),
                portals,
                portalPresentations,
                travelRequirements,
                sceneTemplates,
                scenes,
                new List<DimensionEncounterDefinition>(),
                generationPasses,
                new List<DimensionProgressFlag>(),
                new List<DimensionWorldEventDefinition>(),
                ownershipBindings,
                assetReferences,
                biomes);
            manifest = new DimensionContentManifest(baseManifest, starters);

            result = DimensionOperationResult.Ok();
            return true;
        }

        private static string BuildValidationFailedMessage(
            DimensionCompiledGenerationPlan compiledPlan)
        {
            string message = string.IsNullOrEmpty(compiledPlan.Message)
                ? "Dimension authoring template has validation errors."
                : compiledPlan.Message;
            IReadOnlyList<DimensionAuthoringIssue> issues = compiledPlan.Issues;
            if (issues == null)
            {
                return message;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                if (issue.Severity != DimensionAuthoringSeverity.Error)
                {
                    continue;
                }

                return message +
                    " First error: " +
                    issue.Code +
                    " - " +
                    issue.Message +
                    FormatIssueTarget(issue) +
                    ".";
            }

            return message;
        }

        private static string FormatIssueTarget(DimensionAuthoringIssue issue)
        {
            if (string.IsNullOrEmpty(issue.RecordKind) &&
                string.IsNullOrEmpty(issue.RecordId))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(issue.RecordId))
            {
                return " [" + issue.RecordKind + "]";
            }

            if (string.IsNullOrEmpty(issue.RecordKind))
            {
                return " [" + issue.RecordId + "]";
            }

            return " [" + issue.RecordKind + ": " + issue.RecordId + "]";
        }

        private static void AddBiomeDefinitions(
            DimensionTemplateAsset template,
            string contentPackId,
            bool hasContentPack,
            List<DimensionBiomeDefinition> biomes,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            Dictionary<string, bool> biomeIds = new Dictionary<string, bool>();
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biomeAsset = biomeAssets[i];
                if (biomeAsset == null)
                {
                    continue;
                }

                DimensionBiomeDefinition biome = biomeAsset.ToBiomeDefinition(template.DimensionId);
                if (!AddUnique(biomeIds, biome.BiomeId))
                {
                    continue;
                }

                biomes.Add(biome);
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.Biome,
                    biome.BiomeId,
                    biome.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddZoneDefinitions(
            DimensionCompiledGenerationPlan plan,
            string contentPackId,
            bool hasContentPack,
            List<DimensionZoneDefinition> zones,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            Dictionary<string, ZoneAggregate> zoneAggregates =
                new Dictionary<string, ZoneAggregate>();
            List<string> zoneOrder = new List<string>();
            for (int i = 0; i < plan.BiomeRegions.Count; i++)
            {
                DimensionCompiledBiomeRegion region = plan.BiomeRegions[i];
                string zoneId = DimensionTemplateCompiler.ResolveCompiledZoneId(region);
                if (string.IsNullOrEmpty(zoneId))
                {
                    continue;
                }

                ZoneAggregate aggregate;
                if (zoneAggregates.TryGetValue(zoneId, out aggregate))
                {
                    aggregate.LocalBounds = UnionBounds(aggregate.LocalBounds, region.LocalBounds);
                    aggregate.Priority = math.min(aggregate.Priority, region.Priority);
                    zoneAggregates[zoneId] = aggregate;
                    continue;
                }

                zoneAggregates.Add(
                    zoneId,
                    new ZoneAggregate(
                        zoneId,
                        string.IsNullOrEmpty(region.DisplayName) ? region.BiomeId : region.DisplayName,
                        region.DimensionId,
                        region.LocalBounds,
                        region.BiomeId,
                        region.Priority));
                zoneOrder.Add(zoneId);
            }

            for (int i = 0; i < zoneOrder.Count; i++)
            {
                ZoneAggregate aggregate = zoneAggregates[zoneOrder[i]];
                DimensionZoneDefinition zone = new DimensionZoneDefinition(
                    aggregate.ZoneId,
                    aggregate.DisplayName,
                    aggregate.DimensionId,
                    aggregate.LocalBounds,
                    aggregate.BiomeId,
                    aggregate.Priority,
                    true);
                zones.Add(zone);
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.ZoneDefinition,
                    zone.ZoneId,
                    zone.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddStarterDefinitions(
            DimensionTemplateAsset template,
            DimensionCompiledGenerationPlan compiledPlan,
            string contentPackId,
            bool hasContentPack,
            List<DimensionPortalDefinition> portals,
            List<DimensionStarterDefinition> starters,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (template == null || !compiledPlan.Success || !hasContentPack)
            {
                return;
            }

            DimensionPortalDefinition entryPortal;
            DimensionPortalDefinition returnPortal;
            if (!TryFindStarterPortals(template.DimensionId, portals, out entryPortal, out returnPortal))
            {
                return;
            }

            string starterId = BuildDefaultStarterId(template.DimensionId);
            string displayName = BuildDefaultStarterDisplayName(
                template.DimensionId,
                template.DisplayName);
            DimensionBounds generationBounds = compiledPlan.PlayableLocalBounds;
            DimensionStarterDefinition starter = CreateStarterDefinition(
                starterId,
                template.DimensionId,
                displayName,
                BuildDefaultStarterDescription(displayName),
                contentPackId,
                entryPortal,
                returnPortal,
                generationBounds,
                CenteredLocalTileAreaAround(entryPortal.ToLocalPosition, 16));
            starters.Add(starter);
            AddOwnership(
                hasContentPack,
                contentPackId,
                DimensionContentRecordKind.Starter,
                starter.StarterId,
                starter.DisplayName,
                ownershipBindings,
                ownershipKeys);
        }

        private static bool TryFindStarterPortals(
            string dimensionId,
            List<DimensionPortalDefinition> portals,
            out DimensionPortalDefinition entryPortal,
            out DimensionPortalDefinition returnPortal)
        {
            entryPortal = default(DimensionPortalDefinition);
            returnPortal = default(DimensionPortalDefinition);
            bool foundEntry = false;
            bool foundReturn = false;
            if (portals == null)
            {
                return false;
            }

            for (int i = 0; i < portals.Count; i++)
            {
                DimensionPortalDefinition portal = portals[i];
                if (!foundEntry &&
                    string.Equals(portal.FromDimensionId, DimensionIds.Overworld) &&
                    string.Equals(portal.ToDimensionId, dimensionId))
                {
                    entryPortal = portal;
                    foundEntry = true;
                }

                if (!foundReturn &&
                    string.Equals(portal.FromDimensionId, dimensionId) &&
                    string.Equals(portal.ToDimensionId, DimensionIds.Overworld))
                {
                    returnPortal = portal;
                    foundReturn = true;
                }
            }

            return foundEntry && foundReturn;
        }

        private static DimensionStarterDefinition CreateStarterDefinition(
            string starterId,
            string dimensionId,
            string displayName,
            string description,
            string contentPackId,
            DimensionPortalDefinition entryPortal,
            DimensionPortalDefinition returnPortal,
            DimensionBounds generationBounds,
            DimensionBounds targetLandingBounds)
        {
            return new DimensionStarterDefinition(
                starterId,
                dimensionId,
                displayName,
                description,
                contentPackId,
                true,
                new DimensionContentReadinessRequest(
                    starterId + ".readiness",
                    displayName + " Readiness",
                    new List<DimensionContentReadinessRequirement>()),
                new DimensionTravelLoopPreflightRequest(
                    entryPortal.FromDimensionId,
                    entryPortal.ToDimensionId,
                    entryPortal.PortalId,
                    returnPortal.PortalId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    targetLandingBounds,
                    true,
                    false,
                    false,
                    false,
                    true,
                    false),
                new DimensionStarterGenerationRequest(
                    starterId,
                    "dimension-starter:" + starterId,
                    dimensionId,
                    generationBounds,
                    100,
                    true,
                    "Starter generation area for " + displayName + "."));
        }

        private static string BuildDefaultStarterId(string dimensionId)
        {
            return (dimensionId ?? string.Empty) + ".starter";
        }

        private static string BuildDefaultStarterDisplayName(
            string dimensionId,
            string dimensionDisplayName)
        {
            return (string.IsNullOrEmpty(dimensionDisplayName) ? dimensionId : dimensionDisplayName) + " Starter";
        }

        private static string BuildDefaultStarterDescription(string displayName)
        {
            return "Starter generation and travel loop for " + displayName + ".";
        }

        private static DimensionBounds CenteredLocalTileAreaAround(float2 localPosition, int sideTiles)
        {
            int clampedSideTiles = math.max(1, sideTiles);
            int2 tile = new int2((int)math.floor(localPosition.x), (int)math.floor(localPosition.y));
            int half = clampedSideTiles / 2;
            int2 min = tile - new int2(half, half);
            return new DimensionBounds(min, min + new int2(clampedSideTiles, clampedSideTiles));
        }

        private static DimensionBounds UnionBounds(DimensionBounds a, DimensionBounds b)
        {
            return new DimensionBounds(
                new int2(math.min(a.Min.x, b.Min.x), math.min(a.Min.y, b.Min.y)),
                new int2(math.max(a.MaxExclusive.x, b.MaxExclusive.x), math.max(a.MaxExclusive.y, b.MaxExclusive.y)));
        }

        private static void AddSceneTemplates(
            DimensionTemplateAsset template,
            string dimensionId,
            string contentPackId,
            bool hasContentPack,
            List<DimensionSceneTemplateDefinition> sceneTemplates,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            Dictionary<string, bool> sceneTemplateIds = new Dictionary<string, bool>();
            AddSceneTemplateAssets(
                template.GlobalScenes,
                dimensionId,
                string.Empty,
                contentPackId,
                hasContentPack,
                sceneTemplates,
                sceneTemplateIds,
                ownershipBindings,
                ownershipKeys);

            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biome = biomeAssets[i];
                if (biome == null)
                {
                    continue;
                }

                AddSceneTemplateAssets(
                    biome.ScenePool,
                    dimensionId,
                    biome.BiomeId,
                    contentPackId,
                    hasContentPack,
                    sceneTemplates,
                    sceneTemplateIds,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddSceneTemplateAssets(
            SceneTemplateAsset[] assets,
            string dimensionId,
            string fallbackZoneId,
            string contentPackId,
            bool hasContentPack,
            List<DimensionSceneTemplateDefinition> sceneTemplates,
            Dictionary<string, bool> sceneTemplateIds,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                SceneTemplateAsset asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                DimensionSceneTemplateDefinition sceneTemplate =
                    asset.ToTemplateDefinition(dimensionId, fallbackZoneId);
                if (!AddUnique(sceneTemplateIds, sceneTemplate.TemplateId))
                {
                    continue;
                }

                sceneTemplates.Add(sceneTemplate);
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.SceneTemplate,
                    sceneTemplate.TemplateId,
                    sceneTemplate.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddSceneDefinitions(
            DimensionCompiledGenerationPlan plan,
            string contentPackId,
            bool hasContentPack,
            List<DimensionSceneDefinition> scenes,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            Dictionary<string, bool> sceneIds = new Dictionary<string, bool>();
            for (int i = 0; i < plan.ScenePlacements.Count; i++)
            {
                DimensionCompiledScenePlacement placement = plan.ScenePlacements[i];
                if (!placement.HasLocalBounds || !AddUnique(sceneIds, placement.SceneId))
                {
                    continue;
                }

                DimensionSceneDefinition scene = new DimensionSceneDefinition(
                    placement.SceneId,
                    placement.DisplayName,
                    placement.DimensionId,
                    placement.LocalBounds,
                    string.IsNullOrEmpty(placement.BiomeId) ? "scene" : placement.BiomeId,
                    placement.Priority,
                    DimensionSceneState.Planned);

                scenes.Add(scene);
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.Scene,
                    scene.SceneId,
                    scene.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddAuthoredContentOwnership(
            DimensionTemplateAsset template,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (template == null)
            {
                return;
            }

            AddItemOwnership(template.GlobalItems, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddRecipeOwnership(template.GlobalRecipes, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddWorkbenchOwnership(template.GlobalWorkbenches, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddLootTableOwnership(template.GlobalLootTables, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddAnimalOwnership(template.GlobalAnimals, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddCritterOwnership(template.GlobalCritters, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddMobOwnership(template.GlobalMobs, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddBossOwnership(template.GlobalBosses, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddSceneContentOwnership(template.GlobalScenes, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);

            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biome = biomeAssets[i];
                if (biome != null)
                {
                    AddSceneContentOwnership(biome.ScenePool, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddItemOwnership(
            DimensionItemAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionItemAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Item, asset.ItemId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddRecipeOwnership(
            DimensionRecipeAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionRecipeAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Recipe, asset.RecipeId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddWorkbenchOwnership(
            DimensionWorkbenchAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionWorkbenchAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Workbench, asset.WorkbenchId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddLootTableOwnership(
            DimensionLootTableAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionLootTableAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.LootTable, asset.LootTableId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddAnimalOwnership(
            DimensionAnimalAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionAnimalAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Animal, asset.AnimalId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddCritterOwnership(
            DimensionCritterAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionCritterAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Critter, asset.CritterId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddMobOwnership(
            DimensionMobAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionMobAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Mob, asset.MobId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddBossOwnership(
            DimensionBossAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionBossAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Boss, asset.BossId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddSceneContentOwnership(
            SceneTemplateAsset[] scenes,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
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

                AddSceneTriggerOwnership(scene, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            }
        }

        private static void AddSceneTriggerOwnership(
            SceneTemplateAsset scene,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            DimensionSceneTriggerTemplate[] triggers = scene.Triggers;
            for (int i = 0; i < triggers.Length; i++)
            {
                DimensionSceneTriggerTemplate trigger = triggers[i];
                if (trigger != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.SceneTrigger, BuildSceneContentRecordId(scene.SceneId, "trigger", trigger.TriggerId, i), trigger.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddGenerationPassOwnership(
            string contentPackId,
            bool hasContentPack,
            List<DimensionGenerationPassDefinition> generationPasses,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            for (int i = 0; i < generationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition generationPass = generationPasses[i];
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.GenerationPass,
                    generationPass.PassId,
                    generationPass.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddAuthoredContentAssetReferences(
            DimensionTemplateAsset template,
            string contentPackId,
            bool hasContentPack,
            List<DimensionAssetReferenceDefinition> assetReferences,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (!hasContentPack || template == null)
            {
                return;
            }

            Dictionary<string, bool> assetReferenceIds = new Dictionary<string, bool>();
            AddExistingAssetReferenceIds(assetReferences, assetReferenceIds);
            List<DimensionAssetReferenceDefinition> references =
                new List<DimensionAssetReferenceDefinition>();

            AddSceneAssetReferences(
                template.GlobalScenes,
                template.DimensionId,
                string.Empty,
                contentPackId,
                references);
            AddItemAssetReferences(template.GlobalItems, template.DimensionId, contentPackId, references);
            AddWorkbenchAssetReferences(template.GlobalWorkbenches, template.DimensionId, contentPackId, references);
            AddAnimalAssetReferences(template.GlobalAnimals, template.DimensionId, contentPackId, references);
            AddCritterAssetReferences(template.GlobalCritters, template.DimensionId, contentPackId, references);
            AddMobAssetReferences(template.GlobalMobs, template.DimensionId, contentPackId, references);
            AddBossAssetReferences(template.GlobalBosses, template.DimensionId, contentPackId, references);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome != null)
                {
                    AddSceneAssetReferences(
                        biome.ScenePool,
                        template.DimensionId,
                        biome.BiomeId,
                        contentPackId,
                        references);
                }
            }

            for (int i = 0; i < references.Count; i++)
            {
                DimensionAssetReferenceDefinition reference = references[i];
                if (!AddUnique(assetReferenceIds, reference.AssetId))
                {
                    continue;
                }

                assetReferences.Add(reference);
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.AssetReference,
                    reference.AssetId,
                    reference.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddExistingAssetReferenceIds(
            List<DimensionAssetReferenceDefinition> assetReferences,
            Dictionary<string, bool> assetReferenceIds)
        {
            if (assetReferences == null || assetReferenceIds == null)
            {
                return;
            }

            for (int i = 0; i < assetReferences.Count; i++)
            {
                string assetId = assetReferences[i].AssetId;
                if (!string.IsNullOrEmpty(assetId) && !assetReferenceIds.ContainsKey(assetId))
                {
                    assetReferenceIds.Add(assetId, true);
                }
            }
        }

        private static void AddSceneAssetReferences(
            SceneTemplateAsset[] scenes,
            string dimensionId,
            string zoneId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (scenes == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene != null)
                {
                    scene.AddAssetReferencesTo(contentPackId, dimensionId, zoneId, references);
                }
            }
        }

        private static void AddItemAssetReferences(
            DimensionItemAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionItemAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddWorkbenchAssetReferences(
            DimensionWorkbenchAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionWorkbenchAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddAnimalAssetReferences(
            DimensionAnimalAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionAnimalAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddCritterAssetReferences(
            DimensionCritterAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionCritterAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddMobAssetReferences(
            DimensionMobAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionMobAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddBossAssetReferences(
            DimensionBossAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionBossAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddPortalAccessRules(
            DimensionTemplateAsset template,
            string contentPackId,
            bool hasContentPack,
            List<DimensionPortalDefinition> portals,
            List<DimensionPortalPresentationDefinition> portalPresentations,
            List<DimensionTravelRequirementDefinition> travelRequirements,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (template == null)
            {
                return;
            }

            DimensionPortalAccessRuleAsset[] rules = template.PortalAccessRules;
            if (rules.Length == 0)
            {
                return;
            }

            Dictionary<string, bool> portalIds = new Dictionary<string, bool>();
            Dictionary<string, bool> presentationIds = new Dictionary<string, bool>();
            Dictionary<string, bool> requirementIds = new Dictionary<string, bool>();
            List<DimensionTravelRequirementDefinition> localRequirements =
                new List<DimensionTravelRequirementDefinition>();

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !rule.Enabled)
                {
                    continue;
                }

                DimensionPortalDefinition portal = rule.ToPortalDefinition();
                if (AddUnique(portalIds, portal.PortalId))
                {
                    portals.Add(portal);
                    AddOwnership(
                        hasContentPack,
                        contentPackId,
                        DimensionContentRecordKind.Portal,
                        portal.PortalId,
                        portal.DisplayName,
                        ownershipBindings,
                        ownershipKeys);
                }

                DimensionPortalPresentationDefinition presentation = rule.ToPortalPresentationDefinition();
                if (AddUnique(presentationIds, presentation.PresentationId))
                {
                    portalPresentations.Add(presentation);
                    AddOwnership(
                        hasContentPack,
                        contentPackId,
                        DimensionContentRecordKind.PortalPresentation,
                        presentation.PresentationId,
                        presentation.DisplayName,
                        ownershipBindings,
                        ownershipKeys);
                }

                localRequirements.Clear();
                rule.AppendTravelRequirements(localRequirements);
                for (int requirementIndex = 0; requirementIndex < localRequirements.Count; requirementIndex++)
                {
                    DimensionTravelRequirementDefinition requirement = localRequirements[requirementIndex];
                    if (!AddUnique(requirementIds, requirement.RequirementId))
                    {
                        continue;
                    }

                    travelRequirements.Add(requirement);
                    AddOwnership(
                        hasContentPack,
                        contentPackId,
                        DimensionContentRecordKind.TravelRequirement,
                        requirement.RequirementId,
                        requirement.DisplayName,
                        ownershipBindings,
                        ownershipKeys);
                }
            }
        }

        private static void AddOwnership(
            bool hasContentPack,
            string contentPackId,
            DimensionContentRecordKind kind,
            string recordId,
            string displayName,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (!hasContentPack || string.IsNullOrEmpty(recordId))
            {
                return;
            }

            string key = ((int)kind).ToString() + ":" + recordId;
            if (!AddUnique(ownershipKeys, key))
            {
                return;
            }

            ownershipBindings.Add(new DimensionContentOwnershipBinding(
                contentPackId,
                kind,
                recordId,
                displayName,
                "Generated from a dimension authoring template."));
        }

        private static bool AddUnique(Dictionary<string, bool> keys, string key)
        {
            if (string.IsNullOrEmpty(key) || keys.ContainsKey(key))
            {
                return false;
            }

            keys.Add(key, true);
            return true;
        }

        private static string BuildSceneContentRecordId(
            string sceneId,
            string contentKind,
            string contentId,
            int index)
        {
            string resolvedContentId = string.IsNullOrEmpty(contentId)
                ? contentKind + "-" + index.ToString()
                : contentId;
            if (resolvedContentId.IndexOf('.') >= 0 || resolvedContentId.IndexOf(':') >= 0)
            {
                return resolvedContentId;
            }

            if (string.IsNullOrEmpty(sceneId))
            {
                return contentKind + "." + resolvedContentId;
            }

            return sceneId + "." + contentKind + "." + resolvedContentId;
        }

        private static DimensionContentManifest CreateEmptyManifest()
        {
            return new DimensionContentManifest(
                new List<DimensionContentPackDefinition>(),
                new List<DimensionDefinition>(),
                new List<DimensionZoneDefinition>(),
                new List<DimensionMapLayerDefinition>(),
                new List<DimensionMapMarker>(),
                new List<DimensionAnchorDefinition>(),
                new List<DimensionPortalDefinition>(),
                new List<DimensionPortalPresentationDefinition>(),
                new List<DimensionTravelRequirementDefinition>(),
                new List<DimensionSceneTemplateDefinition>(),
                new List<DimensionSceneDefinition>(),
                new List<DimensionEncounterDefinition>(),
                new List<DimensionGenerationPassDefinition>(),
                new List<DimensionProgressFlag>(),
                new List<DimensionWorldEventDefinition>(),
                new List<DimensionContentOwnershipBinding>(),
                new List<DimensionAssetReferenceDefinition>(),
                new List<DimensionBiomeDefinition>());
        }

        private struct ZoneAggregate
        {
            public readonly string ZoneId;
            public readonly string DisplayName;
            public readonly string DimensionId;
            public DimensionBounds LocalBounds;
            public readonly string BiomeId;
            public int Priority;

            public ZoneAggregate(
                string zoneId,
                string displayName,
                string dimensionId,
                DimensionBounds localBounds,
                string biomeId,
                int priority)
            {
                ZoneId = zoneId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                DimensionId = dimensionId ?? string.Empty;
                LocalBounds = localBounds;
                BiomeId = biomeId ?? string.Empty;
                Priority = priority;
            }
        }
    }
}
