using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Turning an authored layout into the regions a dimension is generated from.
    /// </summary>
    public static partial class DimensionTemplateCompiler
    {
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
            // There was an int bandSize here. It was read, threaded through and never used again:
            // the scanline walk below emits one region per run and produces an exact circle, so
            // there is no band to size. The stored field is still read above for its guard and is
            // still part of the layout fingerprint, so no pinned world moves.
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
    }
}
