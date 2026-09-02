using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public static partial class DimensionTemplateManifestBuilder
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
