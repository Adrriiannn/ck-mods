using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What the compiler refuses, and the sentence it says when it does.
    /// </summary>
    public static partial class DimensionTemplateCompiler
    {
        private static void ValidateBiomeSemanticObjectIds(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            if (template == null)
            {
                return;
            }

            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int biomeIndex = 0; biomeIndex < biomeAssets.Length; biomeIndex++)
            {
                BiomeTemplateAsset biome = biomeAssets[biomeIndex];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                Dictionary<string, string> objectKindsById = new Dictionary<string, string>();
                ValidateSemanticObjectIds(
                    biome,
                    "floor object",
                    DimensionGenerationSubjectKind.FloorObject,
                    biome.FloorObjectIds,
                    objectKindsById,
                    issues);
                ValidateSemanticObjectIds(
                    biome,
                    "wall object",
                    DimensionGenerationSubjectKind.WallObject,
                    biome.WallObjectIds,
                    objectKindsById,
                    issues);
                ValidateSemanticObjectIds(
                    biome,
                    "ore object",
                    DimensionGenerationSubjectKind.OreObject,
                    biome.OreObjectIds,
                    objectKindsById,
                    issues);
            }
        }

        /// <summary>
        /// Says out loud what each biome's ground and walls will actually be made of, and the four
        /// ways that can go wrong.
        /// </summary>
        /// <remarks>
        /// The world builds terrain from the FIRST entry of each list. That was true the moment the
        /// terrain material registry shipped, and nothing on the page said so, so the checks here
        /// are the other half of the feature rather than decoration on it.
        /// </remarks>
        /// <summary>The five steps this framework can actually run.</summary>
        private static bool IsKnownGenerationProviderId(string providerId)
        {
            return string.Equals(providerId, DimensionGenerationProviderIds.SafePlatform, System.StringComparison.Ordinal) ||
                   string.Equals(providerId, DimensionGenerationProviderIds.TileMap, System.StringComparison.Ordinal) ||
                   string.Equals(providerId, DimensionGenerationProviderIds.ScenePlacement, System.StringComparison.Ordinal) ||
                   string.Equals(providerId, DimensionGenerationProviderIds.OreScatter, System.StringComparison.Ordinal) ||
                   string.Equals(providerId, DimensionGenerationProviderIds.DungeonPlacement, System.StringComparison.Ordinal);
        }

        private static void ValidateBiomeTerrainMaterials(
            DimensionTemplateAsset template,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionAuthoringIssue> issues)
        {
            if (template == null)
            {
                return;
            }

            DimensionTilesetAsset[] tilesets = template.Tilesets;
            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biome = biomeAssets[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                ValidateBiomeTerrainHalf(biome, biome.FloorObjectIds, tilesets, true, issues);
                ValidateBiomeTerrainHalf(biome, biome.WallObjectIds, tilesets, false, issues);
            }

            ValidateBiomeRegionMaterialOverlaps(biomeRegions, issues);
        }

        private static void ValidateBiomeTerrainHalf(
            BiomeTemplateAsset biome,
            string[] objectIds,
            DimensionTilesetAsset[] tilesets,
            bool isGround,
            List<DimensionAuthoringIssue> issues)
        {
            string half = isGround ? "Ground" : "Walls";

            int tilesetId;
            string named;
            bool hasGround;
            DimensionBiomeTerrainSource source = DimensionBiomeTerrainMaterial.ResolveFirst(
                objectIds, tilesets, out tilesetId, out named, out hasGround);

            if (source == DimensionBiomeTerrainSource.Unknown)
            {
                // A warning, not a blocker. The dimension still generates — it generates dirt —
                // and turning this into an export blocker would stop every project that still
                // carries the placeholder ids the framework itself used to write into new biomes.
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "biome-terrain-block-unresolved",
                    "Biome '" + biome.BiomeId + "' names '" + named + "' as its " + half +
                    ", and that is not one of this dimension's blocks or one of the game's. The " +
                    "world will lay plain dirt there. Pick the block from the " + half +
                    " row on the Biome page.",
                    "BiomeTemplate",
                    biome.BiomeId));
                return;
            }

            if (source == DimensionBiomeTerrainSource.Empty)
            {
                return;
            }

            if (isGround && !hasGround)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "biome-terrain-block-not-ground",
                    "Biome '" + biome.BiomeId + "' uses '" + named + "' as its Ground, and that " +
                    "block has no ground surface — it is walls only. The floor of this biome will " +
                    "not look like anything you drew.",
                    "BiomeTemplate",
                    biome.BiomeId));
            }

            int count = 0;
            for (int i = 0; i < objectIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(objectIds[i]))
                {
                    count++;
                }
            }

            if (count > 1)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Info,
                    "biome-terrain-extra-blocks",
                    "Biome '" + biome.BiomeId + "' lists " + count + " blocks under " + half +
                    ". The world builds terrain from the first one, '" + named +
                    "'; the rest say what the biome is made of and are not built from.",
                    "BiomeTemplate",
                    biome.BiomeId));
            }
        }

        /// <summary>
        /// Two biomes claiming the same ground.
        /// </summary>
        /// <remarks>
        /// A cell has exactly one ground, so an overlap has to resolve to one of the two and the
        /// author cannot see which from here. Reported once per pair of biomes rather than once per
        /// overlapping rectangle — a radial layout compiles one region per scanline row, and a pair
        /// of overlapping rings would otherwise report hundreds of times.
        /// </remarks>
        private static void ValidateBiomeRegionMaterialOverlaps(
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionAuthoringIssue> issues)
        {
            if (biomeRegions == null || biomeRegions.Count < 2)
            {
                return;
            }

            Dictionary<string, bool> reportedPairs = new Dictionary<string, bool>();
            for (int a = 0; a < biomeRegions.Count; a++)
            {
                for (int b = a + 1; b < biomeRegions.Count; b++)
                {
                    string biomeA = biomeRegions[a].BiomeId;
                    string biomeB = biomeRegions[b].BiomeId;
                    if (string.Equals(biomeA, biomeB, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!BoundsOverlap(biomeRegions[a].LocalBounds, biomeRegions[b].LocalBounds))
                    {
                        continue;
                    }

                    string pair = string.CompareOrdinal(biomeA, biomeB) <= 0
                        ? biomeA + "|" + biomeB
                        : biomeB + "|" + biomeA;
                    if (reportedPairs.ContainsKey(pair))
                    {
                        continue;
                    }

                    reportedPairs.Add(pair, true);
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Warning,
                        "biome-terrain-regions-overlap",
                        "Biomes '" + biomeA + "' and '" + biomeB + "' cover some of the same " +
                        "ground. A tile can only be made of one block, so whichever of the two the " +
                        "world registers last wins there — which is not something you can read off " +
                        "this page. Move one of them so they do not overlap.",
                        "BiomeTemplate",
                        biomeA));
                }
            }
        }

        private static bool BoundsOverlap(DimensionBounds a, DimensionBounds b)
        {
            return a.Min.x < b.MaxExclusive.x &&
                   b.Min.x < a.MaxExclusive.x &&
                   a.Min.y < b.MaxExclusive.y &&
                   b.Min.y < a.MaxExclusive.y;
        }

        private static void ValidateSemanticObjectIds(
            BiomeTemplateAsset biome,
            string label,
            string subjectKind,
            string[] objectIds,
            Dictionary<string, string> objectKindsById,
            List<DimensionAuthoringIssue> issues)
        {
            if (biome == null || objectIds == null)
            {
                return;
            }

            Dictionary<string, bool> seenIds = new Dictionary<string, bool>();
            for (int i = 0; i < objectIds.Length; i++)
            {
                string objectId = objectIds[i] ?? string.Empty;
                if (string.IsNullOrEmpty(objectId))
                {
                    continue;
                }

                if (seenIds.ContainsKey(objectId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Warning,
                        "biome-semantic-object-id-duplicate",
                        "Biome semantic " + label + " id is duplicated. Generation table export will de-duplicate it, but the authoring data is ambiguous: " + objectId + ".",
                        "BiomeTemplate",
                        biome.BiomeId));
                    continue;
                }

                seenIds.Add(objectId, true);

                string existingKind;
                if (objectKindsById.TryGetValue(objectId, out existingKind))
                {
                    if (!string.Equals(existingKind, subjectKind))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-semantic-object-id-category-overlap",
                            "Biome semantic object id appears in more than one category. The generated tables will keep both categories, but this may represent a misplaced floor/wall/ore/water id: " + objectId + ".",
                            "BiomeTemplate",
                            biome.BiomeId));
                    }

                    continue;
                }

                objectKindsById.Add(objectId, subjectKind);
            }
        }

        private static Dictionary<string, BiomeTemplateAsset> BuildBiomeLookup(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            Dictionary<string, BiomeTemplateAsset> result = new Dictionary<string, BiomeTemplateAsset>();
            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                string biomeId = biome.BiomeId;
                if (string.IsNullOrEmpty(biomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "biome-id-empty",
                        "Biome id is required.",
                        "BiomeTemplate",
                        biome.name));
                    continue;
                }

                if (result.ContainsKey(biomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "biome-id-duplicate",
                        "Biome id is duplicated in this Dimension Asset.",
                        "BiomeTemplate",
                        biomeId));
                    continue;
                }

                result.Add(biomeId, biome);
            }

            return result;
        }

        private static void ValidatePlacementAgainstBiomes(
            SceneTemplateAsset scene,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            DimensionBounds bounds,
            List<DimensionAuthoringIssue> issues)
        {
            string[] allowedBiomeIds = scene.AllowedBiomeIds;
            bool hasAllowedBiomeFilter = false;
            for (int i = 0; i < allowedBiomeIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(allowedBiomeIds[i]))
                {
                    hasAllowedBiomeFilter = true;
                    break;
                }
            }

            bool foundKnownAllowedBiome = !hasAllowedBiomeFilter;
            bool intersectsAllowedBiome = false;

            for (int i = 0; i < biomeRegions.Count; i++)
            {
                DimensionCompiledBiomeRegion region = biomeRegions[i];
                bool biomeAllowed = !hasAllowedBiomeFilter || ContainsString(allowedBiomeIds, region.BiomeId);
                if (biomeAllowed)
                {
                    foundKnownAllowedBiome = true;
                }

                if (biomeAllowed && Intersects(region.LocalBounds, bounds))
                {
                    intersectsAllowedBiome = true;
                }
            }

            if (!foundKnownAllowedBiome)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "scene-allowed-biome-missing",
                    "Scene references an allowed biome id that is not present in the dimension layout.",
                    "SceneTemplate",
                    scene.TemplateId,
                    true,
                    bounds));
                return;
            }

            if (!intersectsAllowedBiome)
            {
                issues.Add(CreateIssue(
                    scene.Required ? DimensionAuthoringSeverity.Error : DimensionAuthoringSeverity.Warning,
                    "scene-outside-biome",
                    "Scene placement does not overlap any allowed biome region.",
                    "SceneTemplate",
                    scene.TemplateId,
                    true,
                    bounds));
            }
        }

        private static void ValidateExactSceneOverlaps(
            List<DimensionCompiledScenePlacement> scenePlacements,
            List<DimensionAuthoringIssue> issues)
        {
            for (int i = 0; i < scenePlacements.Count; i++)
            {
                DimensionCompiledScenePlacement left = scenePlacements[i];
                if (!left.Exact || !left.HasLocalBounds)
                {
                    continue;
                }

                for (int j = i + 1; j < scenePlacements.Count; j++)
                {
                    DimensionCompiledScenePlacement right = scenePlacements[j];
                    if (!right.Exact || !right.HasLocalBounds)
                    {
                        continue;
                    }

                    if (!Intersects(left.LocalBounds, right.LocalBounds))
                    {
                        continue;
                    }

                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "scene-exact-overlap",
                        "Two exact scene placements overlap. Move one scene or make one automatic/preferred.",
                        "SceneTemplate",
                        left.TemplateId + " / " + right.TemplateId,
                        true,
                        Union(left.LocalBounds, right.LocalBounds)));
                }
            }
        }

        private static void ValidateBiomeRegionDiagnostics(
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionAuthoringIssue> issues)
        {
            Dictionary<string, bool> regionIds = new Dictionary<string, bool>();
            for (int i = 0; i < biomeRegions.Count; i++)
            {
                DimensionCompiledBiomeRegion region = biomeRegions[i];
                string regionId = region.SourceTemplateId;
                if (string.IsNullOrEmpty(regionId))
                {
                    regionId = region.BiomeId + "." + i;
                }

                if (regionIds.ContainsKey(regionId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Warning,
                        "biome-region-id-duplicate",
                        "Compiled biome region id is duplicated. Runtime priority still applies, but diagnostics and map tooling will be less clear.",
                        "BiomeRegion",
                        regionId,
                        true,
                        region.LocalBounds));
                    continue;
                }

                regionIds.Add(regionId, true);
            }

            for (int leftIndex = 0; leftIndex < biomeRegions.Count; leftIndex++)
            {
                DimensionCompiledBiomeRegion left = biomeRegions[leftIndex];
                for (int rightIndex = leftIndex + 1; rightIndex < biomeRegions.Count; rightIndex++)
                {
                    DimensionCompiledBiomeRegion right = biomeRegions[rightIndex];
                    if (!Intersects(left.LocalBounds, right.LocalBounds))
                    {
                        continue;
                    }

                    string recordId = BuildRegionDiagnosticPairId(left, right);
                    DimensionBounds overlap = Intersection(left.LocalBounds, right.LocalBounds);
                    if (left.Priority == right.Priority)
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-region-overlap-same-priority",
                            "Two biome regions overlap with the same priority. The generator may not have a clear owner for the overlapped area.",
                            "BiomeRegion",
                            recordId,
                            true,
                            overlap));
                    }
                    else
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Info,
                            "biome-region-overlap-priority",
                            "Two biome regions overlap. Higher priority region selection is expected for the overlapped area.",
                            "BiomeRegion",
                            recordId,
                            true,
                            overlap));
                    }
                }
            }
        }

        private static string BuildRegionDiagnosticPairId(
            DimensionCompiledBiomeRegion left,
            DimensionCompiledBiomeRegion right)
        {
            string leftId = string.IsNullOrEmpty(left.SourceTemplateId)
                ? left.BiomeId
                : left.SourceTemplateId;
            string rightId = string.IsNullOrEmpty(right.SourceTemplateId)
                ? right.BiomeId
                : right.SourceTemplateId;
            return leftId + " / " + rightId;
        }

        private static string ResolveFirstAllowedBiomeId(SceneTemplateAsset scene)
        {
            string[] allowedBiomeIds = scene.AllowedBiomeIds;
            for (int i = 0; i < allowedBiomeIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(allowedBiomeIds[i]))
                {
                    return allowedBiomeIds[i];
                }
            }

            return string.Empty;
        }

        private static bool IntersectsAnyBiome(
            List<DimensionCompiledBiomeRegion> biomeRegions,
            DimensionBounds bounds,
            string zoneId)
        {
            for (int i = 0; i < biomeRegions.Count; i++)
            {
                DimensionCompiledBiomeRegion region = biomeRegions[i];
                if (!string.IsNullOrEmpty(zoneId) &&
                    !string.Equals(region.ZoneId, zoneId) &&
                    !string.Equals(region.BiomeId, zoneId))
                {
                    continue;
                }

                if (Intersects(region.LocalBounds, bounds))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
