using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeCustomizerCapability
    {
        public readonly string CapabilityId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly DimensionAuthoringContentSummaryKind ContentKind;
        public readonly bool BiomeScoped;
        public readonly bool SupportsWeightedTables;
        public readonly bool SupportsExactPlacement;
        public readonly bool SupportsAutomaticPlacement;
        public readonly bool RuntimeProviderRequired;

        public DimensionBiomeCustomizerCapability(
            string capabilityId,
            string displayName,
            string description,
            DimensionAuthoringContentSummaryKind contentKind,
            bool biomeScoped,
            bool supportsWeightedTables,
            bool supportsExactPlacement,
            bool supportsAutomaticPlacement,
            bool runtimeProviderRequired)
        {
            CapabilityId = capabilityId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            ContentKind = contentKind;
            BiomeScoped = biomeScoped;
            SupportsWeightedTables = supportsWeightedTables;
            SupportsExactPlacement = supportsExactPlacement;
            SupportsAutomaticPlacement = supportsAutomaticPlacement;
            RuntimeProviderRequired = runtimeProviderRequired;
        }
    }

    public static class DimensionBiomeCustomizerCapabilityCatalog
    {
        private static readonly DimensionBiomeCustomizerCapability[] BuiltInCapabilities =
        {
            new DimensionBiomeCustomizerCapability(
                "environment-profile",
                "Environment",
                "Lighting, fog, music, ambient audio, zone defaults, and map color for a dimension or biome.",
                DimensionAuthoringContentSummaryKind.EnvironmentProfile,
                true,
                false,
                false,
                false,
                false),
            new DimensionBiomeCustomizerCapability(
                "biome-palette",
                "Biome Palette",
                "Semantic asset references for floors, walls, liquids, ores, objects, scenes, mobs, bosses, items, audio, and custom provider slots.",
                DimensionAuthoringContentSummaryKind.BiomePalette,
                true,
                false,
                false,
                false,
                false),
            new DimensionBiomeCustomizerCapability(
                "semantic-terrain",
                "Semantic Terrain",
                "Quick floor, wall, ore, and water/liquid-adjacent object declarations that expand into normal generation tables.",
                DimensionAuthoringContentSummaryKind.SemanticFloorObject,
                true,
                true,
                false,
                true,
                true),
            new DimensionBiomeCustomizerCapability(
                "generation-table",
                "Weighted Tables",
                "Weighted provider-readable tables for terrain, objects, resources, spawns, loot, hazards, scenes, polish, and custom subjects.",
                DimensionAuthoringContentSummaryKind.GenerationTable,
                true,
                true,
                false,
                true,
                true),
            new DimensionBiomeCustomizerCapability(
                "generation-pass",
                "Generation Passes",
                "Ordered provider-backed generation work such as terrain, liquid, resources, structures, scenes, objects, spawns, and polish.",
                DimensionAuthoringContentSummaryKind.GenerationPass,
                true,
                false,
                false,
                true,
                true),
            new DimensionBiomeCustomizerCapability(
                "scene-template",
                "Scenes",
                "Scene templates and placements. Scenes can be placed automatically or pinned to exact local coordinates by the content author.",
                DimensionAuthoringContentSummaryKind.SceneTemplate,
                true,
                false,
                true,
                true,
                false),
            new DimensionBiomeCustomizerCapability(
                "resource-node",
                "Resource Nodes",
                "Ore boulders, landmark resources, and other resource nodes with optional local bounds.",
                DimensionAuthoringContentSummaryKind.ResourceNode,
                true,
                true,
                true,
                true,
                false),
            new DimensionBiomeCustomizerCapability(
                "spawn-rule",
                "Spawn Rules",
                "Mob, critter, NPC, boss, and encounter spawn rules with biome/zone/local-bounds filtering.",
                DimensionAuthoringContentSummaryKind.SpawnRule,
                true,
                true,
                true,
                true,
                false),
            new DimensionBiomeCustomizerCapability(
                "layout-template",
                "Biome Positioning",
                "Manual regions, painted masks, centered grids, and radial rings that determine where biomes exist in local coordinates.",
                DimensionAuthoringContentSummaryKind.LayoutTemplate,
                false,
                false,
                true,
                true,
                false),
            new DimensionBiomeCustomizerCapability(
                "content-preset",
                "Content Presets",
                "Reusable bundles of environment, palette, generation, scene, resource, spawn, and semantic object defaults.",
                DimensionAuthoringContentSummaryKind.BiomeContentPreset,
                true,
                true,
                false,
                true,
                false)
        };

        public static IReadOnlyList<DimensionBiomeCustomizerCapability> GetBuiltInCapabilities()
        {
            return BuiltInCapabilities;
        }

        public static bool TryGetBuiltInCapability(
            string capabilityId,
            out DimensionBiomeCustomizerCapability capability)
        {
            string resolvedId = capabilityId ?? string.Empty;
            for (int i = 0; i < BuiltInCapabilities.Length; i++)
            {
                if (BuiltInCapabilities[i].CapabilityId == resolvedId)
                {
                    capability = BuiltInCapabilities[i];
                    return true;
                }
            }

            capability = default(DimensionBiomeCustomizerCapability);
            return false;
        }
    }
}
