using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public static class DimensionBiomeGenerationBlueprintUtility
    {
        public static bool TryBuild(
            IDimensionService service,
            string biomeId,
            out DimensionBiomeGenerationBlueprint blueprint)
        {
            return TryBuild(service, string.Empty, biomeId, true, out blueprint);
        }

        public static bool TryBuild(
            IDimensionService service,
            string dimensionId,
            string biomeId,
            bool enabledOnly,
            out DimensionBiomeGenerationBlueprint blueprint)
        {
            if (service == null)
            {
                blueprint = CreateFailure(
                    "missing-service",
                    "A dimension service is required to build a biome generation blueprint.",
                    dimensionId,
                    biomeId);
                return false;
            }

            if (string.IsNullOrEmpty(biomeId))
            {
                blueprint = CreateFailure(
                    "missing-biome",
                    "A biome id is required to build a biome generation blueprint.",
                    dimensionId,
                    biomeId);
                return false;
            }

            DimensionBiomeDefinition biome;
            if (!service.TryGetBiome(biomeId, out biome))
            {
                blueprint = CreateFailure(
                    "unknown-biome",
                    "The requested biome is not registered.",
                    dimensionId,
                    biomeId);
                return false;
            }

            if (!string.IsNullOrEmpty(dimensionId)
                && !string.Equals(biome.DimensionId, dimensionId, StringComparison.Ordinal))
            {
                blueprint = CreateFailure(
                    "dimension-mismatch",
                    "The requested biome belongs to a different dimension.",
                    dimensionId,
                    biomeId);
                return false;
            }

            if (enabledOnly && !biome.Enabled)
            {
                blueprint = CreateFailure(
                    "biome-disabled",
                    "The requested biome is disabled.",
                    biome.DimensionId,
                    biomeId);
                return false;
            }

            string resolvedDimensionId = biome.DimensionId;
            DimensionAssetReferenceDefinition palette;
            bool hasPalette = TryGetPalette(service, biome.PaletteAssetId, enabledOnly, out palette);

            List<DimensionAssetReferenceDefinition> paletteEntries =
                GetPaletteEntries(service, resolvedDimensionId, biome.PaletteAssetId, enabledOnly);
            List<DimensionGenerationTableDefinition> generationTables =
                new List<DimensionGenerationTableDefinition>(service.GetGenerationTables(
                    new DimensionGenerationTableQuery(
                        resolvedDimensionId,
                        biome.BiomeId,
                        DimensionGenerationTableKind.Any,
                        enabledOnly)));
            List<DimensionSceneDefinition> scenes =
                GetBiomeScenes(service, resolvedDimensionId, biome.BiomeId, enabledOnly);
            List<DimensionResourceNodeDefinition> resourceNodes =
                GetBiomeResourceNodes(service, resolvedDimensionId, biome.BiomeId, enabledOnly);
            List<DimensionSpawnRule> spawnRules =
                GetBiomeSpawnRules(service, resolvedDimensionId, biome.BiomeId, enabledOnly);

            blueprint = new DimensionBiomeGenerationBlueprint(
                true,
                "ok",
                string.Empty,
                resolvedDimensionId,
                biome.BiomeId,
                string.IsNullOrEmpty(biome.DisplayName) ? biome.BiomeId : biome.DisplayName,
                true,
                biome,
                hasPalette,
                palette,
                paletteEntries,
                generationTables,
                scenes,
                resourceNodes,
                spawnRules);
            return true;
        }

        private static bool TryGetPalette(
            IDimensionService service,
            string paletteAssetId,
            bool enabledOnly,
            out DimensionAssetReferenceDefinition palette)
        {
            palette = default(DimensionAssetReferenceDefinition);
            if (string.IsNullOrEmpty(paletteAssetId))
            {
                return false;
            }

            if (!service.TryGetAssetReference(paletteAssetId, out palette))
            {
                return false;
            }

            return palette.Kind == DimensionAssetReferenceKind.Palette
                && (!enabledOnly || palette.Enabled);
        }

        private static List<DimensionAssetReferenceDefinition> GetPaletteEntries(
            IDimensionService service,
            string dimensionId,
            string paletteAssetId,
            bool enabledOnly)
        {
            List<DimensionAssetReferenceDefinition> results =
                new List<DimensionAssetReferenceDefinition>();

            if (string.IsNullOrEmpty(paletteAssetId))
            {
                return results;
            }

            IReadOnlyList<DimensionAssetReferenceDefinition> references =
                service.GetAssetReferences(new DimensionAssetReferenceQuery(
                    string.Empty,
                    dimensionId,
                    string.Empty,
                    DimensionAssetReferenceKind.Any,
                    string.Empty,
                    enabledOnly));

            string palettePrefix = paletteAssetId + ".";
            for (int i = 0; i < references.Count; i++)
            {
                DimensionAssetReferenceDefinition assetReference = references[i];
                if (string.Equals(assetReference.AssetId, paletteAssetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!assetReference.AssetId.StartsWith(palettePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(assetReference);
            }

            return results;
        }

        private static List<DimensionSceneDefinition> GetBiomeScenes(
            IDimensionService service,
            string dimensionId,
            string biomeId,
            bool enabledOnly)
        {
            List<DimensionSceneDefinition> results = new List<DimensionSceneDefinition>();
            IReadOnlyList<DimensionSceneDefinition> scenes = service.GetScenes(dimensionId, !enabledOnly);
            for (int i = 0; i < scenes.Count; i++)
            {
                DimensionSceneDefinition scene = scenes[i];
                if (enabledOnly && scene.State == DimensionSceneState.Disabled)
                {
                    continue;
                }

                if (BoundsCenterResolvesToBiome(service, dimensionId, scene.LocalBounds, biomeId))
                {
                    results.Add(scene);
                }
            }

            return results;
        }

        private static List<DimensionResourceNodeDefinition> GetBiomeResourceNodes(
            IDimensionService service,
            string dimensionId,
            string biomeId,
            bool enabledOnly)
        {
            List<DimensionResourceNodeDefinition> results =
                new List<DimensionResourceNodeDefinition>();
            IReadOnlyList<DimensionResourceNodeDefinition> nodes =
                service.GetResourceNodes(new DimensionResourceNodeQuery(
                    dimensionId,
                    string.Empty,
                    false,
                    new float2(0f, 0f),
                    DimensionResourceNodeKind.Any,
                    enabledOnly));

            for (int i = 0; i < nodes.Count; i++)
            {
                DimensionResourceNodeDefinition node = nodes[i];
                if (!node.HasLocalBounds)
                {
                    continue;
                }

                if (BoundsCenterResolvesToBiome(service, dimensionId, node.LocalBounds, biomeId))
                {
                    results.Add(node);
                }
            }

            return results;
        }

        private static List<DimensionSpawnRule> GetBiomeSpawnRules(
            IDimensionService service,
            string dimensionId,
            string biomeId,
            bool enabledOnly)
        {
            List<DimensionSpawnRule> results = new List<DimensionSpawnRule>();
            IReadOnlyList<DimensionSpawnRule> rules =
                service.GetSpawnRules(new DimensionSpawnRuleQuery(
                    dimensionId,
                    string.Empty,
                    false,
                    new float2(0f, 0f),
                    DimensionSpawnSubjectKind.Any,
                    enabledOnly));

            for (int i = 0; i < rules.Count; i++)
            {
                DimensionSpawnRule rule = rules[i];
                if (!rule.HasLocalBounds)
                {
                    continue;
                }

                if (BoundsCenterResolvesToBiome(service, dimensionId, rule.LocalBounds, biomeId))
                {
                    results.Add(rule);
                }
            }

            return results;
        }

        private static bool BoundsCenterResolvesToBiome(
            IDimensionService service,
            string dimensionId,
            DimensionBounds bounds,
            string biomeId)
        {
            if (string.IsNullOrEmpty(dimensionId) || string.IsNullOrEmpty(biomeId))
            {
                return false;
            }

            int2 size = bounds.Size;
            if (size.x <= 0 || size.y <= 0)
            {
                return false;
            }

            float2 center = new float2(
                (bounds.Min.x + bounds.MaxExclusive.x) * 0.5f,
                (bounds.Min.y + bounds.MaxExclusive.y) * 0.5f);
            DimensionBiomeResolutionResult resolution = service.ResolveBiomeAtLocal(
                dimensionId,
                center);
            return resolution.Success
                && resolution.HasBiome
                && string.Equals(resolution.Biome.BiomeId, biomeId, StringComparison.Ordinal);
        }

        private static DimensionBiomeGenerationBlueprint CreateFailure(
            string code,
            string message,
            string dimensionId,
            string biomeId)
        {
            return new DimensionBiomeGenerationBlueprint(
                false,
                code,
                message,
                dimensionId,
                biomeId,
                string.Empty,
                false,
                default(DimensionBiomeDefinition),
                false,
                default(DimensionAssetReferenceDefinition),
                new List<DimensionAssetReferenceDefinition>(),
                new List<DimensionGenerationTableDefinition>(),
                new List<DimensionSceneDefinition>(),
                new List<DimensionResourceNodeDefinition>(),
                new List<DimensionSpawnRule>());
        }
    }
}
