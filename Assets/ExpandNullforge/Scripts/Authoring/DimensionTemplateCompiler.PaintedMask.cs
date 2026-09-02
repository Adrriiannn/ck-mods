using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Reading a painted mask and turning each painted colour into regions.
    /// </summary>
    public static partial class DimensionTemplateCompiler
    {
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
    }
}
