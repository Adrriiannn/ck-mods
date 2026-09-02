using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The scenes a template places and the generation passes it runs.
    /// </summary>
    public static partial class DimensionTemplateCompiler
    {
        private static void BuildScenePlacements(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionCompiledScenePlacement> placements,
            List<DimensionAuthoringIssue> issues)
        {
            AddScenePlacements(template.GlobalScenes, dimensionId, biomeRegions, placements, issues);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                AddScenePlacements(biome.ScenePool, dimensionId, biomeRegions, placements, issues);
            }
        }

        private static void AddScenePlacements(
            SceneTemplateAsset[] scenes,
            string dimensionId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionCompiledScenePlacement> placements,
            List<DimensionAuthoringIssue> issues)
        {
            if (scenes == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene == null || !scene.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(scene.TemplateId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "scene-template-id-empty",
                        "Scene template id is required.",
                        "SceneTemplate",
                        scene.name));
                }

                if (string.IsNullOrEmpty(scene.SceneId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "scene-id-empty",
                        "Scene id is required.",
                        "SceneTemplate",
                        scene.TemplateId));
                }

                if (scene.FootprintSize.x <= 0 || scene.FootprintSize.y <= 0)
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "scene-footprint-invalid",
                        "Scene footprint size must be positive.",
                        "SceneTemplate",
                        scene.TemplateId));
                    continue;
                }

                bool hasBounds = false;
                bool exact = false;
                DimensionBounds bounds = new DimensionBounds(new int2(0, 0), new int2(0, 0));

                if (scene.PlacementMode == DimensionScenePlacementMode.ExactLocalPosition)
                {
                    bounds = scene.ExactLocalBounds;
                    hasBounds = true;
                    exact = true;
                }
                else if (scene.PlacementMode == DimensionScenePlacementMode.PreferredBounds)
                {
                    bounds = scene.PreferredLocalBounds;
                    hasBounds = true;
                }

                string biomeId = ResolveFirstAllowedBiomeId(scene);
                if (hasBounds)
                {
                    if (!IsValidBounds(bounds))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Error,
                            "scene-bounds-invalid",
                            "Scene placement bounds must have positive width and height.",
                            "SceneTemplate",
                            scene.TemplateId,
                            true,
                            bounds));
                        continue;
                    }

                    ValidatePlacementAgainstBiomes(scene, biomeRegions, bounds, issues);
                }
                else
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Info,
                        "scene-placement-automatic",
                        "Scene will be placed by the runtime scene solver because it has no exact or preferred local bounds.",
                        "SceneTemplate",
                        scene.TemplateId));
                }

                placements.Add(new DimensionCompiledScenePlacement(
                    dimensionId,
                    scene.SceneId,
                    scene.TemplateId,
                    scene.DisplayName,
                    biomeId,
                    hasBounds,
                    bounds,
                    exact,
                    scene.Required,
                    scene.Priority));
            }
        }

        private static void BuildGenerationPasses(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionGenerationPassDefinition> generationPasses,
            List<DimensionAuthoringIssue> issues)
        {
            Dictionary<string, bool> passIds = new Dictionary<string, bool>();
            AddGlobalGenerationPasses(
                template.GlobalGenerationPasses,
                dimensionId,
                passIds,
                generationPasses,
                issues);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                AddBiomeGenerationPasses(
                    biome.GenerationPasses,
                    dimensionId,
                    biome.BiomeId,
                    biomeRegions,
                    passIds,
                    generationPasses,
                    issues);
            }
        }

        private static void AddGlobalGenerationPasses(
            GenerationPassTemplateAsset[] passes,
            string dimensionId,
            Dictionary<string, bool> passIds,
            List<DimensionGenerationPassDefinition> generationPasses,
            List<DimensionAuthoringIssue> issues)
        {
            if (passes == null)
            {
                return;
            }

            for (int i = 0; i < passes.Length; i++)
            {
                GenerationPassTemplateAsset pass = passes[i];
                if (pass == null || !pass.Enabled)
                {
                    continue;
                }

                string passId = BuildScopedId(dimensionId, string.Empty, pass.PassId);
                DimensionGenerationPassDefinition definition =
                    pass.ToDefinition(
                        passId,
                        dimensionId,
                        string.Empty,
                        false,
                        new DimensionBounds(new int2(0, 0), new int2(0, 0)));

                ValidateAndAddGenerationPass(definition, pass.name, passIds, generationPasses, issues);
            }
        }

        private static void AddBiomeGenerationPasses(
            GenerationPassTemplateAsset[] passes,
            string dimensionId,
            string biomeId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            Dictionary<string, bool> passIds,
            List<DimensionGenerationPassDefinition> generationPasses,
            List<DimensionAuthoringIssue> issues)
        {
            if (passes == null)
            {
                return;
            }

            for (int i = 0; i < passes.Length; i++)
            {
                GenerationPassTemplateAsset pass = passes[i];
                if (pass == null || !pass.Enabled)
                {
                    continue;
                }

                if (pass.HasExplicitLocalBounds)
                {
                    string passId = BuildScopedId(dimensionId, biomeId, pass.PassId);
                    DimensionGenerationPassDefinition definition =
                        pass.ToDefinition(
                            passId,
                            dimensionId,
                            biomeId,
                            false,
                            new DimensionBounds(new int2(0, 0), new int2(0, 0)));

                    if (!IntersectsAnyBiome(biomeRegions, definition.LocalBounds, biomeId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "generation-pass-outside-biome",
                            "Biome generation pass explicit bounds do not overlap any matching biome region.",
                            "GenerationPassTemplate",
                            definition.PassId,
                            true,
                            definition.LocalBounds));
                    }

                    ValidateAndAddGenerationPass(definition, pass.name, passIds, generationPasses, issues);
                    continue;
                }

                bool matchedRegion = false;
                for (int regionIndex = 0; regionIndex < biomeRegions.Count; regionIndex++)
                {
                    DimensionCompiledBiomeRegion region = biomeRegions[regionIndex];
                    if (!string.Equals(region.BiomeId, biomeId))
                    {
                        continue;
                    }

                    matchedRegion = true;
                    string zoneId = ResolveCompiledZoneId(region);
                    string regionScopeId = ResolveRegionScopeId(region, zoneId, regionIndex);
                    string passId = BuildScopedId(dimensionId, regionScopeId, pass.PassId);
                    DimensionGenerationPassDefinition definition =
                        pass.ToDefinition(
                            passId,
                            dimensionId,
                            zoneId,
                            !pass.HasExplicitLocalBounds,
                            region.LocalBounds);

                    ValidateAndAddGenerationPass(definition, pass.name, passIds, generationPasses, issues);
                }

                if (!matchedRegion)
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Warning,
                        "generation-pass-biome-region-missing",
                        "Biome generation pass has no matching compiled biome region.",
                        "GenerationPassTemplate",
                        pass.PassId));
                }
            }
        }

        private static string ResolveRegionScopeId(
            DimensionCompiledBiomeRegion region,
            string zoneId,
            int regionIndex)
        {
            if (!string.IsNullOrEmpty(region.SourceTemplateId))
            {
                return region.SourceTemplateId;
            }

            if (!string.IsNullOrEmpty(zoneId))
            {
                return zoneId + "." + regionIndex;
            }

            return "region." + regionIndex;
        }

        private static void ValidateAndAddGenerationPass(
            DimensionGenerationPassDefinition definition,
            string sourceName,
            Dictionary<string, bool> passIds,
            List<DimensionGenerationPassDefinition> generationPasses,
            List<DimensionAuthoringIssue> issues)
        {
            if (string.IsNullOrEmpty(definition.PassId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-pass-id-empty",
                    "Generation pass id is required.",
                    "GenerationPassTemplate",
                    sourceName));
                return;
            }

            if (passIds.ContainsKey(definition.PassId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-pass-id-duplicate",
                    "Generation pass id is duplicated after dimension/zone scoping.",
                    "GenerationPassTemplate",
                    definition.PassId));
                return;
            }

            if (string.IsNullOrEmpty(definition.ProviderId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "generation-pass-provider-empty",
                    "Generation pass has no provider id yet. It will be registered but cannot execute until a provider is assigned.",
                    "GenerationPassTemplate",
                    definition.PassId));
            }
            else if (!IsKnownGenerationProviderId(definition.ProviderId))
            {
                // A misspelled id compiles clean, registers, appears in the plan and is skipped at
                // runtime without a word. It is a warning rather than an error because a third
                // party's provider is a legitimate thing to name and this compiler cannot know
                // about it.
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "generation-pass-provider-unknown",
                    "Generation pass '" + definition.PassId + "' names the step '" +
                    definition.ProviderId + "', which is not one this framework runs. If it is " +
                    "not a step another mod adds, the pass is registered and then skipped every " +
                    "time the world generates.",
                    "GenerationPassTemplate",
                    definition.PassId));
            }

            if (definition.HasLocalBounds && !IsValidBounds(definition.LocalBounds))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-pass-bounds-invalid",
                    "Generation pass local bounds must have positive width and height.",
                    "GenerationPassTemplate",
                    definition.PassId,
                    true,
                    definition.LocalBounds));
                return;
            }

            passIds.Add(definition.PassId, true);
            generationPasses.Add(definition);
        }
    }
}
