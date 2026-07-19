using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public static class DimensionTemplateCompiler
    {
        private const int BoundsAlignmentTiles = 16;
        private const int MinimumShellPaddingTiles = 64;
        private const int MaximumShellPaddingTiles = 5000;

        public static DimensionCompiledGenerationPlan Compile(DimensionTemplateAsset template)
        {
            List<DimensionAuthoringIssue> issues = new List<DimensionAuthoringIssue>();
            List<DimensionCompiledBiomeRegion> biomeRegions = new List<DimensionCompiledBiomeRegion>();
            List<DimensionCompiledScenePlacement> scenePlacements = new List<DimensionCompiledScenePlacement>();
            List<DimensionResourceNodeDefinition> resourceNodes = new List<DimensionResourceNodeDefinition>();
            List<DimensionSpawnRule> spawnRules = new List<DimensionSpawnRule>();
            List<DimensionGenerationPassDefinition> generationPasses = new List<DimensionGenerationPassDefinition>();

            if (template == null)
            {
                DimensionBounds emptyBounds = new DimensionBounds(new int2(0, 0), new int2(0, 0));
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "template-null",
                    "Dimension template is missing.",
                    "DimensionTemplate",
                    string.Empty));

                return new DimensionCompiledGenerationPlan(
                    false,
                    "template-null",
                    "Dimension template is missing.",
                    string.Empty,
                    string.Empty,
                    emptyBounds,
                    emptyBounds,
                    MinimumShellPaddingTiles,
                    biomeRegions,
                    scenePlacements,
                    resourceNodes,
                    spawnRules,
                    generationPasses,
                    issues);
            }

            string dimensionId = template.DimensionId;
            DimensionBounds reservedBounds = template.ReservedLocalBounds;

            if (string.IsNullOrEmpty(dimensionId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "dimension-id-empty",
                    "Dimension id is required.",
                    "DimensionTemplate",
                    template.name));
            }

            if (!IsValidBounds(reservedBounds))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "dimension-reserved-bounds-invalid",
                    "Dimension reserved local bounds must have positive width and height.",
                    "DimensionTemplate",
                    dimensionId,
                    true,
                    reservedBounds));
            }

            BuildBiomeRegions(template, dimensionId, biomeRegions, issues);
            ValidateBiomeRegionDiagnostics(biomeRegions, issues);
            BuildScenePlacements(template, dimensionId, biomeRegions, scenePlacements, issues);
            BuildResourceNodes(template, dimensionId, biomeRegions, resourceNodes, issues);
            BuildSpawnRules(template, dimensionId, biomeRegions, spawnRules, issues);
            BuildGenerationPasses(template, dimensionId, biomeRegions, generationPasses, issues);
            ValidateBiomeContentPresets(template, issues);
            ValidateBiomeSemanticObjectIds(template, issues);
            ValidateEnvironmentProfileTemplates(template, dimensionId, issues);
            ValidateGenerationTables(template, dimensionId, issues);
            ValidateBiomePaletteTemplates(template, dimensionId, issues);
            ValidateExactSceneOverlaps(scenePlacements, issues);

            DimensionBounds playableBounds;
            if (!TryResolvePlayableBounds(
                    biomeRegions,
                    scenePlacements,
                    resourceNodes,
                    spawnRules,
                    generationPasses,
                    out playableBounds))
            {
                playableBounds = CreateCenteredBounds(BoundsAlignmentTiles);
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "dimension-empty-playable-area",
                    "No enabled biome region, scene, resource node, or spawn rule contributes a playable area.",
                    "DimensionTemplate",
                    dimensionId));
            }

            playableBounds = AlignToSquareBounds(playableBounds, BoundsAlignmentTiles);
            int shellPadding = ResolveShellPadding(playableBounds);
            DimensionBounds compiledReservedBounds = ExpandBounds(playableBounds, shellPadding);

            if (IsValidBounds(reservedBounds) && !Contains(reservedBounds, playableBounds))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "dimension-playable-exceeds-reserved",
                    "Compiled playable bounds extend outside the dimension reserved bounds. The runtime placement allocator may need a larger reserved range.",
                    "DimensionTemplate",
                    dimensionId,
                    true,
                    playableBounds));
            }

            int errorCount = CountIssues(issues, DimensionAuthoringSeverity.Error);
            bool success = errorCount == 0;
            string code = success ? "ok" : "validation-failed";
            string message = success
                ? "Dimension authoring template compiled successfully."
                : "Dimension authoring template has validation errors.";

            return new DimensionCompiledGenerationPlan(
                success,
                code,
                message,
                dimensionId,
                template.DisplayName,
                compiledReservedBounds,
                playableBounds,
                shellPadding,
                biomeRegions,
                scenePlacements,
                resourceNodes,
                spawnRules,
                generationPasses,
                issues);
        }

        private static void BuildBiomeRegions(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionCompiledBiomeRegion> regions,
            List<DimensionAuthoringIssue> issues)
        {
            Dictionary<string, BiomeTemplateAsset> biomesById = BuildBiomeLookup(template, issues);
            bool usedLayoutRegions = false;

            DimensionLayoutTemplateAsset layout = template.LayoutTemplate;
            if (layout != null)
            {
                if (layout.LayoutKind == DimensionLayoutKind.PaintedMask ||
                    layout.LayoutKind == DimensionLayoutKind.Hybrid)
                {
                    usedLayoutRegions |= BuildPaintedMaskLayoutRegions(
                        layout,
                        dimensionId,
                        biomesById,
                        regions,
                        issues);
                }

                if (layout.LayoutKind == DimensionLayoutKind.GridRegions ||
                    (layout.LayoutKind == DimensionLayoutKind.Hybrid && layout.GridCells.Length > 0))
                {
                    usedLayoutRegions |= BuildGridLayoutRegions(
                        layout,
                        dimensionId,
                        biomesById,
                        regions,
                        issues);
                }

                if (layout.LayoutKind == DimensionLayoutKind.RadialRings ||
                    (layout.LayoutKind == DimensionLayoutKind.Hybrid && layout.RadialRings.Length > 0))
                {
                    if (layout.RadialRings.Length > 0 || layout.Regions.Length == 0)
                    {
                        usedLayoutRegions |= BuildRadialRingLayoutRegions(
                            layout,
                            dimensionId,
                            biomesById,
                            regions,
                            issues);
                    }
                }

                if (layout.LayoutKind == DimensionLayoutKind.ManualRegions ||
                    layout.LayoutKind == DimensionLayoutKind.Hybrid ||
                    (layout.LayoutKind == DimensionLayoutKind.GridRegions && layout.Regions.Length > 0) ||
                    layout.LayoutKind == DimensionLayoutKind.RadialRings)
                {
                    if (layout.LayoutKind == DimensionLayoutKind.RadialRings &&
                        layout.RadialRings.Length == 0 &&
                        layout.Regions.Length > 0)
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "layout-kind-manual-region-fallback",
                            "Radial ring layouts are not generated automatically yet. Explicit layout regions will still be compiled as manual regions.",
                            "DimensionLayout",
                            layout.LayoutId));
                    }

                    usedLayoutRegions |= BuildManualLayoutRegions(
                        layout,
                        dimensionId,
                        biomesById,
                        regions,
                        issues);
                }
            }

            if (usedLayoutRegions)
            {
                return;
            }

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled || !biome.HasFallbackLocalBounds)
                {
                    continue;
                }

                DimensionBounds localBounds = biome.FallbackLocalBounds;
                if (!IsValidBounds(localBounds))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "biome-fallback-bounds-invalid",
                        "Biome fallback bounds must have positive width and height.",
                        "BiomeTemplate",
                        biome.BiomeId,
                        true,
                        localBounds));
                    continue;
                }

                regions.Add(new DimensionCompiledBiomeRegion(
                    dimensionId,
                    biome.BiomeId,
                    biome.BiomeId,
                    biome.DisplayName,
                    biome.BiomeId,
                    localBounds,
                    0));
            }

            if (regions.Count == 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "biome-region-none",
                    "Dimension template does not define any enabled biome regions. Add a layout template or biome fallback bounds.",
                    "DimensionTemplate",
                    dimensionId));
            }
        }

        private static bool BuildManualLayoutRegions(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            Dictionary<string, BiomeTemplateAsset> biomesById,
            List<DimensionCompiledBiomeRegion> regions,
            List<DimensionAuthoringIssue> issues)
        {
            bool added = false;
            Dictionary<string, bool> regionIds = new Dictionary<string, bool>();
            DimensionLayoutRegionDefinition[] layoutRegions = layout.Regions;
            for (int i = 0; i < layoutRegions.Length; i++)
            {
                DimensionLayoutRegionDefinition region = layoutRegions[i];
                if (region == null || !region.Enabled)
                {
                    continue;
                }

                string regionId = string.IsNullOrEmpty(region.RegionId)
                    ? layout.LayoutId + ".region." + i
                    : region.RegionId;
                string biomeId = region.BiomeId;
                DimensionBounds localBounds = region.LocalBounds;

                if (regionIds.ContainsKey(regionId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-region-id-duplicate",
                        "Layout region id is duplicated in this layout template.",
                        "LayoutRegion",
                        regionId,
                        true,
                        localBounds));
                    continue;
                }

                regionIds.Add(regionId, true);

                if (string.IsNullOrEmpty(biomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-region-biome-empty",
                        "Layout region has no biome id.",
                        "LayoutRegion",
                        regionId,
                        true,
                        localBounds));
                    continue;
                }

                BiomeTemplateAsset biome;
                if (!biomesById.TryGetValue(biomeId, out biome))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-region-biome-missing",
                        "Layout region references a biome id that is not present in the Dimension Asset.",
                        "LayoutRegion",
                        regionId,
                        true,
                        localBounds));
                    continue;
                }

                if (!IsValidBounds(localBounds))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-region-bounds-invalid",
                        "Layout region bounds must have positive width and height.",
                        "LayoutRegion",
                        regionId,
                        true,
                        localBounds));
                    continue;
                }

                regions.Add(new DimensionCompiledBiomeRegion(
                    dimensionId,
                    biomeId,
                    region.ZoneId,
                    string.IsNullOrEmpty(region.DisplayName) ? biome.DisplayName : region.DisplayName,
                    regionId,
                    localBounds,
                    region.Priority));
                added = true;
            }

            return added;
        }

        private static bool BuildGridLayoutRegions(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            Dictionary<string, BiomeTemplateAsset> biomesById,
            List<DimensionCompiledBiomeRegion> regions,
            List<DimensionAuthoringIssue> issues)
        {
            Vector2Int gridCellSize = layout.GridCellSize;
            if (gridCellSize.x <= 0 || gridCellSize.y <= 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-grid-cell-size-invalid",
                    "Grid layout needs a positive tile size for each grid cell.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            DimensionLayoutGridCellDefinition[] gridCells = layout.GridCells;
            if (gridCells.Length == 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-grid-cell-missing",
                    "Grid layout needs at least one enabled grid cell definition.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            bool added = false;
            Dictionary<string, bool> cellIds = new Dictionary<string, bool>();
            for (int i = 0; i < gridCells.Length; i++)
            {
                DimensionLayoutGridCellDefinition cell = gridCells[i];
                if (cell == null || !cell.Enabled)
                {
                    continue;
                }

                string cellId = string.IsNullOrEmpty(cell.CellId)
                    ? layout.LayoutId + ".grid." + i
                    : cell.CellId;
                DimensionBounds localBounds = cell.ToLocalBounds(layout.GridLocalMin, gridCellSize);

                if (cellIds.ContainsKey(cellId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-grid-cell-id-duplicate",
                        "Grid cell id is duplicated in this layout template.",
                        "GridCell",
                        cellId,
                        true,
                        localBounds));
                    continue;
                }

                cellIds.Add(cellId, true);

                if (string.IsNullOrEmpty(cell.BiomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-grid-cell-biome-empty",
                        "Grid cell has no biome id.",
                        "GridCell",
                        cellId,
                        true,
                        localBounds));
                    continue;
                }

                BiomeTemplateAsset biome;
                if (!biomesById.TryGetValue(cell.BiomeId, out biome))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-grid-cell-biome-missing",
                        "Grid cell references a biome id that is not present in the Dimension Asset.",
                        "GridCell",
                        cellId,
                        true,
                        localBounds));
                    continue;
                }

                if (!IsValidBounds(localBounds))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-grid-cell-bounds-invalid",
                        "Grid cell produced invalid local bounds. Check grid cell size, cell coordinate, and span.",
                        "GridCell",
                        cellId,
                        true,
                        localBounds));
                    continue;
                }

                regions.Add(new DimensionCompiledBiomeRegion(
                    dimensionId,
                    cell.BiomeId,
                    cell.ZoneId,
                    string.IsNullOrEmpty(cell.DisplayName) ? biome.DisplayName : cell.DisplayName,
                    layout.LayoutId + "." + cellId,
                    localBounds,
                    cell.Priority));
                added = true;
            }

            if (!added)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-grid-region-none",
                    "Grid layout did not produce any biome regions.",
                    "DimensionLayout",
                    layout.LayoutId));
            }

            return added;
        }

        private static bool BuildRadialRingLayoutRegions(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            Dictionary<string, BiomeTemplateAsset> biomesById,
            List<DimensionCompiledBiomeRegion> regions,
            List<DimensionAuthoringIssue> issues)
        {
            int bandSize = layout.RadialBandSizeTiles;
            if (bandSize <= 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-radial-band-size-invalid",
                    "Radial layout needs a positive band size in tiles.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            DimensionLayoutRadialRingDefinition[] rings = layout.RadialRings;
            if (rings.Length == 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-radial-ring-missing",
                    "Radial layout needs at least one enabled ring definition.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            bool added = false;
            int regionIndex = 0;
            Dictionary<string, bool> ringIds = new Dictionary<string, bool>();
            for (int i = 0; i < rings.Length; i++)
            {
                DimensionLayoutRadialRingDefinition ring = rings[i];
                if (ring == null || !ring.Enabled)
                {
                    continue;
                }

                string ringId = string.IsNullOrEmpty(ring.RingId)
                    ? layout.LayoutId + ".radial." + i
                    : ring.RingId;
                if (ringIds.ContainsKey(ringId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-radial-ring-id-duplicate",
                        "Radial ring id is duplicated in this layout template.",
                        "RadialRing",
                        ringId));
                    continue;
                }

                ringIds.Add(ringId, true);

                if (string.IsNullOrEmpty(ring.BiomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-radial-ring-biome-empty",
                        "Radial ring has no biome id.",
                        "RadialRing",
                        ringId));
                    continue;
                }

                BiomeTemplateAsset biome;
                if (!biomesById.TryGetValue(ring.BiomeId, out biome))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-radial-ring-biome-missing",
                        "Radial ring references a biome id that is not present in the Dimension Asset.",
                        "RadialRing",
                        ringId));
                    continue;
                }

                int minRadius = ring.MinRadiusTiles;
                int maxRadius = ring.MaxRadiusTiles;
                if (maxRadius <= minRadius)
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-radial-ring-radius-invalid",
                        "Radial ring max radius must be greater than min radius.",
                        "RadialRing",
                        ringId));
                    continue;
                }

                BuildRadialRingBands(
                    layout,
                    dimensionId,
                    biome,
                    ring,
                    ringId,
                    minRadius,
                    maxRadius,
                    bandSize,
                    regions,
                    ref regionIndex,
                    ref added);
            }

            if (!added)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-radial-region-none",
                    "Radial layout did not produce any biome regions.",
                    "DimensionLayout",
                    layout.LayoutId));
            }

            return added;
        }

        private static void BuildRadialRingBands(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            BiomeTemplateAsset biome,
            DimensionLayoutRadialRingDefinition ring,
            string ringId,
            int minRadius,
            int maxRadius,
            int bandSize,
            List<DimensionCompiledBiomeRegion> regions,
            ref int regionIndex,
            ref bool added)
        {
            float minRadiusSq = minRadius * minRadius;
            float maxRadiusSq = maxRadius * maxRadius;
            int min = -maxRadius;
            int maxExclusive = maxRadius;

            for (int y = min; y < maxExclusive; y++)
            {
                int runStart = 0;
                bool hasRun = false;

                for (int x = min; x < maxExclusive; x++)
                {
                    float sampleX = x + 0.5f;
                    float sampleY = y + 0.5f;
                    float distanceSq = sampleX * sampleX + sampleY * sampleY;
                    bool insideRing = distanceSq >= minRadiusSq &&
                        distanceSq < maxRadiusSq &&
                        IsInsideRadialAngle(ring, sampleX, sampleY);

                    if (insideRing)
                    {
                        if (!hasRun)
                        {
                            runStart = x;
                            hasRun = true;
                        }
                    }
                    else if (hasRun)
                    {
                        AddRadialBandRegion(
                            layout,
                            dimensionId,
                            biome,
                            ring,
                            ringId,
                            runStart,
                            x,
                            y,
                            y + 1,
                            regions,
                            ref regionIndex,
                            ref added);
                        hasRun = false;
                    }
                }

                if (hasRun)
                {
                    AddRadialBandRegion(
                        layout,
                        dimensionId,
                        biome,
                        ring,
                        ringId,
                        runStart,
                        maxExclusive,
                        y,
                        y + 1,
                        regions,
                        ref regionIndex,
                        ref added);
                }
            }
        }

        private static bool IsInsideRadialAngle(
            DimensionLayoutRadialRingDefinition ring,
            float sampleX,
            float sampleY)
        {
            if (ring == null || !ring.HasAngleRange)
            {
                return true;
            }

            float angle = Mathf.Atan2(sampleY, sampleX) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            float start = ring.StartAngleDegrees;
            float end = ring.EndAngleDegrees;
            if (start < end)
            {
                return angle >= start && angle < end;
            }

            return angle >= start || angle < end;
        }

        private static void AddRadialBandRegion(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            BiomeTemplateAsset biome,
            DimensionLayoutRadialRingDefinition ring,
            string ringId,
            int minX,
            int maxXExclusive,
            int minY,
            int maxYExclusive,
            List<DimensionCompiledBiomeRegion> regions,
            ref int regionIndex,
            ref bool added)
        {
            DimensionBounds localBounds = new DimensionBounds(
                new int2(minX, minY),
                new int2(maxXExclusive, maxYExclusive));
            if (!IsValidBounds(localBounds))
            {
                return;
            }

            string regionId = layout.LayoutId + "." + ringId + "." + regionIndex;
            regionIndex++;

            regions.Add(new DimensionCompiledBiomeRegion(
                dimensionId,
                ring.BiomeId,
                ring.ZoneId,
                string.IsNullOrEmpty(ring.DisplayName) ? biome.DisplayName : ring.DisplayName,
                regionId,
                localBounds,
                ring.Priority));
            added = true;
        }

        private static bool BuildPaintedMaskLayoutRegions(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            Dictionary<string, BiomeTemplateAsset> biomesById,
            List<DimensionCompiledBiomeRegion> regions,
            List<DimensionAuthoringIssue> issues)
        {
            Texture2D mask = layout.BiomeMask;
            if (mask == null)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-mask-missing",
                    "Painted mask layout needs a readable biome mask texture.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            int2 tilesPerPixel = new int2(
                layout.TilesPerMaskPixel.x,
                layout.TilesPerMaskPixel.y);
            if (tilesPerPixel.x <= 0 || tilesPerPixel.y <= 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-mask-tile-scale-invalid",
                    "Painted mask layout needs a positive tile size per mask pixel.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            DimensionLayoutMaskBiomeDefinition[] mappings = layout.MaskBiomeMappings;
            if (mappings.Length == 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-mask-biome-mapping-missing",
                    "Painted mask layout needs at least one color-to-biome mapping.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            Color32[] pixels;
            try
            {
                pixels = mask.GetPixels32();
            }
            catch (UnityException)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-mask-not-readable",
                    "Painted mask texture is not readable. Enable Read/Write on the texture import settings.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            if (pixels == null || pixels.Length == 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-mask-empty",
                    "Painted mask texture has no readable pixels.",
                    "DimensionLayout",
                    layout.LayoutId));
                return false;
            }

            Dictionary<string, bool> mappingIds = new Dictionary<string, bool>();
            for (int i = 0; i < mappings.Length; i++)
            {
                DimensionLayoutMaskBiomeDefinition mapping = mappings[i];
                if (mapping == null || !mapping.Enabled)
                {
                    continue;
                }

                string mappingId = string.IsNullOrEmpty(mapping.MappingId)
                    ? layout.LayoutId + ".mask." + i
                    : mapping.MappingId;
                if (mappingIds.ContainsKey(mappingId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-mask-mapping-id-duplicate",
                        "Painted mask biome mapping id is duplicated in this layout template.",
                        "MaskBiomeMapping",
                        mappingId));
                    continue;
                }

                mappingIds.Add(mappingId, true);

                if (string.IsNullOrEmpty(mapping.BiomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-mask-mapping-biome-empty",
                        "Painted mask biome mapping has no biome id.",
                        "MaskBiomeMapping",
                        mappingId));
                    continue;
                }

                if (!biomesById.ContainsKey(mapping.BiomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "layout-mask-mapping-biome-missing",
                        "Painted mask biome mapping references a biome id that is not present in the Dimension Asset.",
                        "MaskBiomeMapping",
                        mappingId));
                }
            }

            int width = mask.width;
            int height = mask.height;
            int alphaThreshold = layout.MaskAlphaThreshold;
            int unmappedOpaquePixels = 0;
            int regionIndex = 0;
            bool added = false;
            Dictionary<string, MaskRegionBuilder> openRegions =
                new Dictionary<string, MaskRegionBuilder>();

            for (int y = 0; y < height; y++)
            {
                Dictionary<string, MaskRegionBuilder> nextOpenRegions =
                    new Dictionary<string, MaskRegionBuilder>();

                int x = 0;
                while (x < width)
                {
                    int pixelIndex = y * width + x;
                    int mappingIndex = FindMaskMappingIndex(
                        pixels[pixelIndex],
                        alphaThreshold,
                        mappings);

                    if (mappingIndex < 0)
                    {
                        if (pixels[pixelIndex].a >= alphaThreshold)
                        {
                            unmappedOpaquePixels++;
                        }

                        x++;
                        continue;
                    }

                    int runStart = x;
                    x++;
                    while (x < width)
                    {
                        int nextPixelIndex = y * width + x;
                        int nextMappingIndex = FindMaskMappingIndex(
                            pixels[nextPixelIndex],
                            alphaThreshold,
                            mappings);
                        if (nextMappingIndex != mappingIndex)
                        {
                            break;
                        }

                        x++;
                    }

                    string key = mappingIndex + ":" + runStart + ":" + x;
                    MaskRegionBuilder builder;
                    if (openRegions.TryGetValue(key, out builder))
                    {
                        builder.MaxPixelYExclusive = y + 1;
                    }
                    else
                    {
                        builder = new MaskRegionBuilder(
                            mappingIndex,
                            runStart,
                            x,
                            y,
                            y + 1);
                    }

                    nextOpenRegions[key] = builder;
                }

                FlushClosedMaskRegions(
                    layout,
                    dimensionId,
                    biomesById,
                    mappings,
                    tilesPerPixel,
                    openRegions,
                    nextOpenRegions,
                    regions,
                    ref regionIndex,
                    ref added);
                openRegions = nextOpenRegions;
            }

            FlushAllMaskRegions(
                layout,
                dimensionId,
                biomesById,
                mappings,
                tilesPerPixel,
                openRegions,
                regions,
                ref regionIndex,
                ref added);

            if (unmappedOpaquePixels > 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "layout-mask-unmapped-opaque-pixels",
                    "Painted mask contains opaque pixels that do not match any enabled biome mapping. They were ignored.",
                    "DimensionLayout",
                    layout.LayoutId));
            }

            if (!added)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "layout-mask-region-none",
                    "Painted mask did not produce any biome regions.",
                    "DimensionLayout",
                    layout.LayoutId));
            }

            return added;
        }

        private static int FindMaskMappingIndex(
            Color32 pixel,
            int alphaThreshold,
            DimensionLayoutMaskBiomeDefinition[] mappings)
        {
            if (pixel.a < alphaThreshold || mappings == null)
            {
                return -1;
            }

            for (int i = 0; i < mappings.Length; i++)
            {
                DimensionLayoutMaskBiomeDefinition mapping = mappings[i];
                if (mapping == null || !mapping.Enabled || string.IsNullOrEmpty(mapping.BiomeId))
                {
                    continue;
                }

                Color32 target = mapping.MaskColor;
                int tolerance = mapping.ColorTolerance;
                if (math.abs(pixel.r - target.r) <= tolerance &&
                    math.abs(pixel.g - target.g) <= tolerance &&
                    math.abs(pixel.b - target.b) <= tolerance)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void FlushClosedMaskRegions(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            Dictionary<string, BiomeTemplateAsset> biomesById,
            DimensionLayoutMaskBiomeDefinition[] mappings,
            int2 tilesPerPixel,
            Dictionary<string, MaskRegionBuilder> openRegions,
            Dictionary<string, MaskRegionBuilder> nextOpenRegions,
            List<DimensionCompiledBiomeRegion> regions,
            ref int regionIndex,
            ref bool added)
        {
            foreach (KeyValuePair<string, MaskRegionBuilder> pair in openRegions)
            {
                if (nextOpenRegions.ContainsKey(pair.Key))
                {
                    continue;
                }

                AddMaskRegion(
                    layout,
                    dimensionId,
                    biomesById,
                    mappings,
                    tilesPerPixel,
                    pair.Value,
                    regions,
                    ref regionIndex,
                    ref added);
            }
        }

        private static void FlushAllMaskRegions(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            Dictionary<string, BiomeTemplateAsset> biomesById,
            DimensionLayoutMaskBiomeDefinition[] mappings,
            int2 tilesPerPixel,
            Dictionary<string, MaskRegionBuilder> openRegions,
            List<DimensionCompiledBiomeRegion> regions,
            ref int regionIndex,
            ref bool added)
        {
            foreach (KeyValuePair<string, MaskRegionBuilder> pair in openRegions)
            {
                AddMaskRegion(
                    layout,
                    dimensionId,
                    biomesById,
                    mappings,
                    tilesPerPixel,
                    pair.Value,
                    regions,
                    ref regionIndex,
                    ref added);
            }
        }

        private static void AddMaskRegion(
            DimensionLayoutTemplateAsset layout,
            string dimensionId,
            Dictionary<string, BiomeTemplateAsset> biomesById,
            DimensionLayoutMaskBiomeDefinition[] mappings,
            int2 tilesPerPixel,
            MaskRegionBuilder builder,
            List<DimensionCompiledBiomeRegion> regions,
            ref int regionIndex,
            ref bool added)
        {
            if (builder.MappingIndex < 0 || builder.MappingIndex >= mappings.Length)
            {
                return;
            }

            DimensionLayoutMaskBiomeDefinition mapping = mappings[builder.MappingIndex];
            if (mapping == null || string.IsNullOrEmpty(mapping.BiomeId))
            {
                return;
            }

            BiomeTemplateAsset biome;
            if (!biomesById.TryGetValue(mapping.BiomeId, out biome))
            {
                return;
            }

            int2 maskOrigin = new int2(layout.MaskLocalMin.x, layout.MaskLocalMin.y);
            int2 localMin = maskOrigin + new int2(
                builder.MinPixelX * tilesPerPixel.x,
                builder.MinPixelY * tilesPerPixel.y);
            int2 localMaxExclusive = maskOrigin + new int2(
                builder.MaxPixelXExclusive * tilesPerPixel.x,
                builder.MaxPixelYExclusive * tilesPerPixel.y);
            DimensionBounds localBounds = new DimensionBounds(localMin, localMaxExclusive);

            if (!IsValidBounds(localBounds))
            {
                return;
            }

            string mappingId = string.IsNullOrEmpty(mapping.MappingId)
                ? "mask." + builder.MappingIndex
                : mapping.MappingId;
            string regionId = layout.LayoutId + "." + mappingId + "." + regionIndex;
            regionIndex++;

            regions.Add(new DimensionCompiledBiomeRegion(
                dimensionId,
                mapping.BiomeId,
                mapping.ZoneId,
                string.IsNullOrEmpty(mapping.DisplayName) ? biome.DisplayName : mapping.DisplayName,
                regionId,
                localBounds,
                mapping.Priority));
            added = true;
        }

        private sealed class MaskRegionBuilder
        {
            public readonly int MappingIndex;
            public readonly int MinPixelX;
            public readonly int MaxPixelXExclusive;
            public readonly int MinPixelY;
            public int MaxPixelYExclusive;

            public MaskRegionBuilder(
                int mappingIndex,
                int minPixelX,
                int maxPixelXExclusive,
                int minPixelY,
                int maxPixelYExclusive)
            {
                MappingIndex = mappingIndex;
                MinPixelX = minPixelX;
                MaxPixelXExclusive = maxPixelXExclusive;
                MinPixelY = minPixelY;
                MaxPixelYExclusive = maxPixelYExclusive;
            }
        }

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

                AddScenePlacements(biome.GetScenePoolWithPresets(), dimensionId, biomeRegions, placements, issues);
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

        private static void BuildResourceNodes(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionResourceNodeDefinition> resourceNodes,
            List<DimensionAuthoringIssue> issues)
        {
            AddResourceNodes(template.GlobalResourceNodes, dimensionId, string.Empty, biomeRegions, resourceNodes, issues);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                AddResourceNodes(biome.GetResourceNodesWithPresets(), dimensionId, biome.BiomeId, biomeRegions, resourceNodes, issues);
            }
        }

        private static void AddResourceNodes(
            ResourceNodeTemplateAsset[] nodes,
            string dimensionId,
            string fallbackZoneId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionResourceNodeDefinition> resourceNodes,
            List<DimensionAuthoringIssue> issues)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                ResourceNodeTemplateAsset node = nodes[i];
                if (node == null || !node.Enabled)
                {
                    continue;
                }

                DimensionResourceNodeDefinition definition = node.ToDefinition(dimensionId, fallbackZoneId);
                if (string.IsNullOrEmpty(definition.NodeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "resource-node-id-empty",
                        "Resource node id is required.",
                        "ResourceNodeTemplate",
                        node.name));
                }

                if (string.IsNullOrEmpty(definition.ResourceId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Warning,
                        "resource-node-resource-empty",
                        "Resource node has no resource id yet. The generation solver will need a provider-specific fallback.",
                        "ResourceNodeTemplate",
                        definition.NodeId));
                }

                if (definition.HasLocalBounds)
                {
                    if (!IsValidBounds(definition.LocalBounds))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Error,
                            "resource-node-bounds-invalid",
                            "Resource node bounds must have positive width and height.",
                            "ResourceNodeTemplate",
                            definition.NodeId,
                            true,
                            definition.LocalBounds));
                        continue;
                    }

                    if (!IntersectsAnyBiome(biomeRegions, definition.LocalBounds, definition.ZoneId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "resource-node-outside-biome",
                            "Resource node explicit bounds do not overlap any matching biome region.",
                            "ResourceNodeTemplate",
                            definition.NodeId,
                            true,
                            definition.LocalBounds));
                    }
                }

                resourceNodes.Add(definition);
            }
        }

        private static void BuildSpawnRules(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionSpawnRule> spawnRules,
            List<DimensionAuthoringIssue> issues)
        {
            AddSpawnRules(template.GlobalSpawnRules, dimensionId, string.Empty, biomeRegions, spawnRules, issues);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                AddSpawnRules(biome.GetSpawnRulesWithPresets(), dimensionId, biome.BiomeId, biomeRegions, spawnRules, issues);
            }
        }

        private static void AddSpawnRules(
            SpawnRuleTemplateAsset[] rules,
            string dimensionId,
            string fallbackZoneId,
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionSpawnRule> spawnRules,
            List<DimensionAuthoringIssue> issues)
        {
            if (rules == null)
            {
                return;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                SpawnRuleTemplateAsset rule = rules[i];
                if (rule == null || !rule.Enabled)
                {
                    continue;
                }

                DimensionSpawnRule definition = rule.ToRule(dimensionId, fallbackZoneId);
                if (string.IsNullOrEmpty(definition.RuleId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "spawn-rule-id-empty",
                        "Spawn rule id is required.",
                        "SpawnRuleTemplate",
                        rule.name));
                }

                if (string.IsNullOrEmpty(definition.SubjectId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Warning,
                        "spawn-rule-subject-empty",
                        "Spawn rule has no subject id yet. The generation solver will need a provider-specific fallback.",
                        "SpawnRuleTemplate",
                        definition.RuleId));
                }

                if (definition.HasLocalBounds)
                {
                    if (!IsValidBounds(definition.LocalBounds))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Error,
                            "spawn-rule-bounds-invalid",
                            "Spawn rule bounds must have positive width and height.",
                            "SpawnRuleTemplate",
                            definition.RuleId,
                            true,
                            definition.LocalBounds));
                        continue;
                    }

                    if (!IntersectsAnyBiome(biomeRegions, definition.LocalBounds, definition.ZoneId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "spawn-rule-outside-biome",
                            "Spawn rule explicit bounds do not overlap any matching biome region.",
                            "SpawnRuleTemplate",
                            definition.RuleId,
                            true,
                            definition.LocalBounds));
                    }
                }

                spawnRules.Add(definition);
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
                    biome.GetGenerationPassesWithProfile(),
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

        private static void ValidateGenerationTables(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionAuthoringIssue> issues)
        {
            Dictionary<string, bool> biomeIds = BuildBiomeIdSet(template);
            Dictionary<string, GenerationTableTemplateAsset> tableIds =
                new Dictionary<string, GenerationTableTemplateAsset>();
            Dictionary<string, bool> entryIds = new Dictionary<string, bool>();

            ValidateGenerationTableAssets(
                template.GlobalGenerationTables,
                dimensionId,
                string.Empty,
                biomeIds,
                tableIds,
                entryIds,
                issues);

            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biome = biomeAssets[i];
                if (biome == null)
                {
                    continue;
                }

                ValidateGenerationTableAssets(
                    biome.GetGenerationTablesWithProfile(),
                    dimensionId,
                    biome.BiomeId,
                    biomeIds,
                    tableIds,
                    entryIds,
                    issues);
            }
        }

        private static void ValidateBiomeContentPresets(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int biomeIndex = 0; biomeIndex < biomeAssets.Length; biomeIndex++)
            {
                BiomeTemplateAsset biome = biomeAssets[biomeIndex];
                if (biome == null)
                {
                    continue;
                }

                Dictionary<string, bool> presetIds = new Dictionary<string, bool>();
                BiomeContentPresetAsset[] presets = biome.ContentPresets;
                for (int presetIndex = 0; presetIndex < presets.Length; presetIndex++)
                {
                    BiomeContentPresetAsset preset = presets[presetIndex];
                    if (preset == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(preset.PresetId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-content-preset-id-empty",
                            "Biome content preset has no stable preset id. This is allowed for local iteration, but named presets are easier to diagnose.",
                            "BiomeContentPreset",
                            preset.name));
                    }
                    else if (presetIds.ContainsKey(preset.PresetId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-content-preset-id-duplicate",
                            "Biome references multiple content presets with the same preset id. The contents are still composed, but diagnostics will be ambiguous.",
                            "BiomeContentPreset",
                            preset.PresetId));
                    }
                    else
                    {
                        presetIds.Add(preset.PresetId, true);
                    }

                    if (!preset.Enabled)
                    {
                        continue;
                    }

                    if (!preset.HasAnyContent)
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-content-preset-empty",
                            "Enabled biome content preset does not currently define environment, palette, generation, scene, resource, spawn, or object defaults.",
                            "BiomeContentPreset",
                            string.IsNullOrEmpty(preset.PresetId) ? preset.name : preset.PresetId));
                    }

                    EnvironmentProfileTemplateAsset profile = preset.EnvironmentProfileTemplate;
                    if (profile != null &&
                        !string.IsNullOrEmpty(preset.EnvironmentProfileId) &&
                        !string.Equals(preset.EnvironmentProfileId, profile.ProfileId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-content-preset-environment-overridden",
                            "Preset has both an explicit environment profile id and an environment profile template. The explicit profile id wins for biome resolution.",
                            "BiomeContentPreset",
                            preset.PresetId));
                    }

                    BiomePaletteTemplateAsset palette = preset.PaletteTemplate;
                    if (palette != null &&
                        !string.IsNullOrEmpty(preset.PaletteAssetId) &&
                        !string.Equals(preset.PaletteAssetId, palette.PaletteId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-content-preset-palette-overridden",
                            "Preset has both an explicit palette asset id and a palette template. The explicit palette asset id wins for biome resolution.",
                            "BiomeContentPreset",
                            preset.PresetId));
                    }
                }
            }
        }

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
                    biome.GetFloorObjectIdsWithPresets(),
                    objectKindsById,
                    issues);
                ValidateSemanticObjectIds(
                    biome,
                    "wall object",
                    DimensionGenerationSubjectKind.WallObject,
                    biome.GetWallObjectIdsWithPresets(),
                    objectKindsById,
                    issues);
                ValidateSemanticObjectIds(
                    biome,
                    "ore object",
                    DimensionGenerationSubjectKind.OreObject,
                    biome.GetOreObjectIdsWithPresets(),
                    objectKindsById,
                    issues);
                ValidateSemanticObjectIds(
                    biome,
                    "water object",
                    DimensionGenerationSubjectKind.WaterObject,
                    biome.GetWaterObjectIdsWithPresets(),
                    objectKindsById,
                    issues);
            }
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

        private static void ValidateBiomePaletteTemplates(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionAuthoringIssue> issues)
        {
            Dictionary<string, bool> assetReferenceIds = new Dictionary<string, bool>();
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
                    if (palette == null)
                    {
                        continue;
                    }

                    if (!palette.Enabled)
                    {
                        if (string.IsNullOrEmpty(biome.PaletteAssetId))
                        {
                            issues.Add(CreateIssue(
                                DimensionAuthoringSeverity.Warning,
                                "biome-palette-template-disabled",
                                "Biome has a disabled palette template and no explicit palette asset id. It will compile without that palette.",
                                "BiomePaletteTemplate",
                                palette.name));
                        }

                        continue;
                    }

                    if (string.IsNullOrEmpty(template.ContentPackId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-palette-content-pack-empty",
                            "Biome palette references require a content pack id. The biome can compile, but palette asset references will not be exported or applied until the Dimension Asset has a content pack id.",
                            "BiomePaletteTemplate",
                            palette.name));
                    }

                    if (!string.IsNullOrEmpty(biome.PaletteAssetId) &&
                        !string.Equals(biome.PaletteAssetId, palette.PaletteId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-palette-template-overridden",
                            "Biome has both an explicit palette asset id and one or more palette templates. The explicit palette asset id wins for biome resolution.",
                            "BiomeTemplate",
                            biome.BiomeId));
                    }

                    List<DimensionAssetReferenceDefinition> references =
                        new List<DimensionAssetReferenceDefinition>();
                    palette.AddAssetReferencesTo(template.ContentPackId, dimensionId, references);
                    for (int referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
                    {
                        ValidatePaletteAssetReference(
                            references[referenceIndex],
                            palette.name,
                            assetReferenceIds,
                            issues);
                    }
                }
            }
        }

        private static void ValidateEnvironmentProfileTemplates(
            DimensionTemplateAsset template,
            string dimensionId,
            List<DimensionAuthoringIssue> issues)
        {
            Dictionary<string, EnvironmentProfileTemplateAsset> profileIds =
                new Dictionary<string, EnvironmentProfileTemplateAsset>();
            EnvironmentProfileTemplateAsset[] templateProfiles = template.EnvironmentProfiles;
            for (int i = 0; i < templateProfiles.Length; i++)
            {
                EnvironmentProfileTemplateAsset profile = templateProfiles[i];
                ValidateEnvironmentProfileTemplate(
                    profile,
                    dimensionId,
                    "DimensionTemplate",
                    template.name,
                    profileIds,
                    issues);
            }

            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biome = biomeAssets[i];
                if (biome == null)
                {
                    continue;
                }

                EnvironmentProfileTemplateAsset[] profiles =
                    biome.GetEnvironmentProfileTemplatesWithPresets();
                for (int profileIndex = 0; profileIndex < profiles.Length; profileIndex++)
                {
                    EnvironmentProfileTemplateAsset profile = profiles[profileIndex];
                    if (profile == null)
                    {
                        continue;
                    }

                    if (!profile.Enabled && string.IsNullOrEmpty(biome.EnvironmentProfileId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-environment-profile-template-disabled",
                            "Biome has a disabled environment profile template and no explicit environment profile id. It will compile without that biome-specific environment profile.",
                            "EnvironmentProfileTemplate",
                            profile.name));
                    }

                    if (!string.IsNullOrEmpty(biome.EnvironmentProfileId) &&
                        !string.Equals(biome.EnvironmentProfileId, profile.ProfileId))
                    {
                        issues.Add(CreateIssue(
                            DimensionAuthoringSeverity.Warning,
                            "biome-environment-profile-template-overridden",
                            "Biome has both an explicit environment profile id and one or more environment profile templates. The explicit environment profile id wins for biome resolution.",
                            "BiomeTemplate",
                            biome.BiomeId));
                    }

                    ValidateEnvironmentProfileTemplate(
                        profile,
                        dimensionId,
                        "BiomeTemplate",
                        biome.BiomeId,
                        profileIds,
                        issues);
                }
            }
        }

        private static void ValidateEnvironmentProfileTemplate(
            EnvironmentProfileTemplateAsset profile,
            string dimensionId,
            string sourceKind,
            string sourceName,
            Dictionary<string, EnvironmentProfileTemplateAsset> profileIds,
            List<DimensionAuthoringIssue> issues)
        {
            if (profile == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(profile.ProfileId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "environment-profile-id-empty",
                    "Environment profile templates need a stable profile id.",
                    sourceKind,
                    sourceName));
                return;
            }

            EnvironmentProfileTemplateAsset existingProfile;
            if (profileIds.TryGetValue(profile.ProfileId, out existingProfile))
            {
                if (existingProfile == profile)
                {
                    return;
                }

                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "environment-profile-id-duplicate",
                    "Environment profile id is duplicated in this Dimension Asset.",
                    "EnvironmentProfileTemplate",
                    profile.ProfileId));
                return;
            }

            profileIds.Add(profile.ProfileId, profile);

            if (string.IsNullOrEmpty(dimensionId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Warning,
                    "environment-profile-dimension-empty",
                    "Environment profile will not be usable until the Dimension Asset has a dimension id.",
                    "EnvironmentProfileTemplate",
                    profile.ProfileId));
            }
        }

        private static void ValidatePaletteAssetReference(
            DimensionAssetReferenceDefinition reference,
            string sourceName,
            Dictionary<string, bool> assetReferenceIds,
            List<DimensionAuthoringIssue> issues)
        {
            if (string.IsNullOrEmpty(reference.AssetId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "biome-palette-asset-id-empty",
                    "Palette asset references need a stable asset id.",
                    "BiomePaletteTemplate",
                    sourceName));
                return;
            }

            if (assetReferenceIds.ContainsKey(reference.AssetId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "biome-palette-asset-id-duplicate",
                    "Palette asset reference id is duplicated in this Dimension Asset.",
                    "BiomePaletteTemplate",
                    reference.AssetId));
                return;
            }

            assetReferenceIds.Add(reference.AssetId, true);

            if (reference.Kind == DimensionAssetReferenceKind.Any)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "biome-palette-asset-kind-any",
                    "Palette asset references must use a concrete asset kind, not Any.",
                    "BiomePaletteTemplate",
                    reference.AssetId));
            }

            if (string.IsNullOrEmpty(reference.ResourceKey))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "biome-palette-resource-key-empty",
                    "Palette asset references need a resource key so the runtime can resolve the referenced asset.",
                    "BiomePaletteTemplate",
                    reference.AssetId));
            }
        }

        private static void ValidateGenerationTableAssets(
            GenerationTableTemplateAsset[] tables,
            string dimensionId,
            string fallbackBiomeId,
            Dictionary<string, bool> biomeIds,
            Dictionary<string, GenerationTableTemplateAsset> tableIds,
            Dictionary<string, bool> entryIds,
            List<DimensionAuthoringIssue> issues)
        {
            if (tables == null)
            {
                return;
            }

            for (int i = 0; i < tables.Length; i++)
            {
                GenerationTableTemplateAsset tableAsset = tables[i];
                if (tableAsset == null || !tableAsset.Enabled)
                {
                    continue;
                }

                string tableId = BuildScopedId(dimensionId, fallbackBiomeId, tableAsset.TableId);
                DimensionGenerationTableDefinition table =
                    tableAsset.ToTableDefinition(tableId, dimensionId, fallbackBiomeId);
                string sourceName = tableAsset.name;

                if (string.IsNullOrEmpty(table.TableId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "generation-table-id-empty",
                        "Generation table id is required.",
                        "GenerationTableTemplate",
                        sourceName));
                    continue;
                }

                if (table.Kind == DimensionGenerationTableKind.Any)
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "generation-table-kind-any",
                        "Generation table templates must use a concrete table kind, not Any.",
                        "GenerationTableTemplate",
                        table.TableId));
                }

                GenerationTableTemplateAsset existingTable;
                if (tableIds.TryGetValue(table.TableId, out existingTable))
                {
                    if (existingTable == tableAsset)
                    {
                        continue;
                    }

                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "generation-table-id-duplicate",
                        "Generation table id is duplicated in this Dimension Asset.",
                        "GenerationTableTemplate",
                        table.TableId));
                    continue;
                }

                tableIds.Add(table.TableId, tableAsset);

                if (!string.IsNullOrEmpty(table.BiomeId) && !biomeIds.ContainsKey(table.BiomeId))
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Error,
                        "generation-table-biome-missing",
                        "Generation table references a biome that is not present in the Dimension Asset.",
                        "GenerationTableTemplate",
                        table.TableId));
                }

                List<DimensionGenerationTableEntryDefinition> entries =
                    new List<DimensionGenerationTableEntryDefinition>();
                tableAsset.AddEntryDefinitions(table.TableId, entries);
                if (entries.Count == 0)
                {
                    issues.Add(CreateIssue(
                        DimensionAuthoringSeverity.Warning,
                        "generation-table-empty",
                        "Generation table has no entries.",
                        "GenerationTableTemplate",
                        table.TableId));
                    continue;
                }

                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    DimensionGenerationTableEntryDefinition entry = entries[entryIndex];
                    ValidateGenerationTableEntry(entry, entryIds, issues);
                }
            }
        }

        private static void ValidateGenerationTableEntry(
            DimensionGenerationTableEntryDefinition entry,
            Dictionary<string, bool> entryIds,
            List<DimensionAuthoringIssue> issues)
        {
            if (string.IsNullOrEmpty(entry.EntryId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-table-entry-id-empty",
                    "Generation table entry id is required.",
                    "GenerationTableEntry",
                    entry.TableId));
                return;
            }

            if (entryIds.ContainsKey(entry.EntryId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-table-entry-id-duplicate",
                    "Generation table entry id is duplicated in this Dimension Asset.",
                    "GenerationTableEntry",
                    entry.EntryId));
                return;
            }

            entryIds.Add(entry.EntryId, true);

            if (string.IsNullOrEmpty(entry.SubjectId))
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-table-entry-subject-empty",
                    "Generation table entry subject id is required.",
                    "GenerationTableEntry",
                    entry.EntryId));
            }

            if (entry.Weight <= 0)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-table-entry-weight-invalid",
                    "Generation table entry weight must be greater than zero.",
                    "GenerationTableEntry",
                    entry.EntryId));
            }

            if (entry.MinCount < 0 || entry.MaxCount < entry.MinCount)
            {
                issues.Add(CreateIssue(
                    DimensionAuthoringSeverity.Error,
                    "generation-table-entry-count-invalid",
                    "Generation table entry count range is invalid.",
                    "GenerationTableEntry",
                    entry.EntryId));
            }
        }

        private static Dictionary<string, bool> BuildBiomeIdSet(DimensionTemplateAsset template)
        {
            Dictionary<string, bool> biomeIds = new Dictionary<string, bool>();
            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biome = biomeAssets[i];
                if (biome == null || string.IsNullOrEmpty(biome.BiomeId))
                {
                    continue;
                }

                if (!biomeIds.ContainsKey(biome.BiomeId))
                {
                    biomeIds.Add(biome.BiomeId, true);
                }
            }

            return biomeIds;
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

        private static bool TryResolvePlayableBounds(
            List<DimensionCompiledBiomeRegion> biomeRegions,
            List<DimensionCompiledScenePlacement> scenePlacements,
            List<DimensionResourceNodeDefinition> resourceNodes,
            List<DimensionSpawnRule> spawnRules,
            List<DimensionGenerationPassDefinition> generationPasses,
            out DimensionBounds bounds)
        {
            bool found = false;
            DimensionBounds result = new DimensionBounds(new int2(0, 0), new int2(0, 0));

            for (int i = 0; i < biomeRegions.Count; i++)
            {
                Merge(ref found, ref result, biomeRegions[i].LocalBounds);
            }

            for (int i = 0; i < scenePlacements.Count; i++)
            {
                DimensionCompiledScenePlacement scene = scenePlacements[i];
                if (scene.HasLocalBounds)
                {
                    Merge(ref found, ref result, scene.LocalBounds);
                }
            }

            for (int i = 0; i < resourceNodes.Count; i++)
            {
                DimensionResourceNodeDefinition node = resourceNodes[i];
                if (node.HasLocalBounds)
                {
                    Merge(ref found, ref result, node.LocalBounds);
                }
            }

            for (int i = 0; i < spawnRules.Count; i++)
            {
                DimensionSpawnRule rule = spawnRules[i];
                if (rule.HasLocalBounds)
                {
                    Merge(ref found, ref result, rule.LocalBounds);
                }
            }

            for (int i = 0; i < generationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition pass = generationPasses[i];
                if (pass.HasLocalBounds)
                {
                    Merge(ref found, ref result, pass.LocalBounds);
                }
            }

            bounds = result;
            return found;
        }

        private static DimensionBounds AlignToSquareBounds(DimensionBounds source, int alignment)
        {
            int2 size = source.Size;
            int side = math.max(size.x, size.y);
            side = AlignUp(math.max(side, alignment), alignment);

            int extraX = side - size.x;
            int extraY = side - size.y;
            int2 min = new int2(
                source.Min.x - extraX / 2,
                source.Min.y - extraY / 2);
            int2 max = min + new int2(side, side);

            return new DimensionBounds(min, max);
        }

        private static DimensionBounds CreateCenteredBounds(int side)
        {
            int half = side / 2;
            return new DimensionBounds(new int2(-half, -half), new int2(side - half, side - half));
        }

        private static int ResolveShellPadding(DimensionBounds playableBounds)
        {
            int2 size = playableBounds.Size;
            int dominantSide = math.max(size.x, size.y);
            int padding = math.max(MinimumShellPaddingTiles, dominantSide);
            padding = math.min(MaximumShellPaddingTiles, padding);
            return AlignUp(padding, BoundsAlignmentTiles);
        }

        private static int CountIssues(List<DimensionAuthoringIssue> issues, DimensionAuthoringSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
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

        private static bool ContainsString(string[] values, string value)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value))
                {
                    return true;
                }
            }

            return false;
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

        private static void Merge(ref bool found, ref DimensionBounds result, DimensionBounds next)
        {
            if (!IsValidBounds(next))
            {
                return;
            }

            if (!found)
            {
                result = next;
                found = true;
                return;
            }

            result = Union(result, next);
        }

        private static bool IsValidBounds(DimensionBounds bounds)
        {
            return bounds.MaxExclusive.x > bounds.Min.x &&
                   bounds.MaxExclusive.y > bounds.Min.y;
        }

        private static bool Contains(DimensionBounds outer, DimensionBounds inner)
        {
            return inner.Min.x >= outer.Min.x &&
                   inner.Min.y >= outer.Min.y &&
                   inner.MaxExclusive.x <= outer.MaxExclusive.x &&
                   inner.MaxExclusive.y <= outer.MaxExclusive.y;
        }

        private static bool Intersects(DimensionBounds a, DimensionBounds b)
        {
            return a.Min.x < b.MaxExclusive.x &&
                   a.MaxExclusive.x > b.Min.x &&
                   a.Min.y < b.MaxExclusive.y &&
                   a.MaxExclusive.y > b.Min.y;
        }

        private static DimensionBounds Union(DimensionBounds a, DimensionBounds b)
        {
            return new DimensionBounds(
                new int2(math.min(a.Min.x, b.Min.x), math.min(a.Min.y, b.Min.y)),
                new int2(math.max(a.MaxExclusive.x, b.MaxExclusive.x), math.max(a.MaxExclusive.y, b.MaxExclusive.y)));
        }

        private static DimensionBounds ExpandBounds(DimensionBounds bounds, int padding)
        {
            int resolvedPadding = math.max(0, padding);
            return new DimensionBounds(
                bounds.Min - new int2(resolvedPadding, resolvedPadding),
                bounds.MaxExclusive + new int2(resolvedPadding, resolvedPadding));
        }

        private static DimensionBounds Intersection(DimensionBounds a, DimensionBounds b)
        {
            return new DimensionBounds(
                new int2(math.max(a.Min.x, b.Min.x), math.max(a.Min.y, b.Min.y)),
                new int2(math.min(a.MaxExclusive.x, b.MaxExclusive.x), math.min(a.MaxExclusive.y, b.MaxExclusive.y)));
        }

        private static int AlignUp(int value, int alignment)
        {
            if (alignment <= 1)
            {
                return value;
            }

            int remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        private static int FloorToMultiple(int value, int alignment)
        {
            if (alignment <= 1)
            {
                return value;
            }

            int remainder = value % alignment;
            if (remainder == 0)
            {
                return value;
            }

            return value < 0 ? value - alignment - remainder : value - remainder;
        }

        private static int CeilToMultiple(int value, int alignment)
        {
            if (alignment <= 1)
            {
                return value;
            }

            int floored = FloorToMultiple(value, alignment);
            return floored == value ? value : floored + alignment;
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

        private static DimensionAuthoringIssue CreateIssue(
            DimensionAuthoringSeverity severity,
            string code,
            string message,
            string recordKind,
            string recordId)
        {
            return CreateIssue(
                severity,
                code,
                message,
                recordKind,
                recordId,
                false,
                new DimensionBounds(new int2(0, 0), new int2(0, 0)));
        }

        private static DimensionAuthoringIssue CreateIssue(
            DimensionAuthoringSeverity severity,
            string code,
            string message,
            string recordKind,
            string recordId,
            bool hasLocalBounds,
            DimensionBounds localBounds)
        {
            return new DimensionAuthoringIssue(
                severity,
                code,
                message,
                recordKind,
                recordId,
                hasLocalBounds,
                localBounds);
        }
    }
}
