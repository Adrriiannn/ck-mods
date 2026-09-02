using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The definitions a template turns into: biomes, zones, starters, scenes.
    /// </summary>
    public static partial class DimensionTemplateManifestBuilder
    {
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
    }
}
