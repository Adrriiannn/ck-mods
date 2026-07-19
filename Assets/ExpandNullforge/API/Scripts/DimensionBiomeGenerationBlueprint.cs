using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Read-only runtime summary of the data currently attached to a biome.
    /// This is intended for generation providers, debug UI, and future editor/runtime preview tools.
    /// </summary>
    public readonly struct DimensionBiomeGenerationBlueprint
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string DisplayName;
        public readonly bool HasBiome;
        public readonly DimensionBiomeDefinition Biome;
        public readonly bool HasPalette;
        public readonly DimensionAssetReferenceDefinition Palette;
        public readonly int PaletteEntryCount;
        public readonly int GenerationTableCount;
        public readonly int SceneCount;
        public readonly int ResourceNodeCount;
        public readonly int SpawnRuleCount;
        public readonly IReadOnlyList<DimensionAssetReferenceDefinition> PaletteEntries;
        public readonly IReadOnlyList<DimensionGenerationTableDefinition> GenerationTables;
        public readonly IReadOnlyList<DimensionSceneDefinition> Scenes;
        public readonly IReadOnlyList<DimensionResourceNodeDefinition> ResourceNodes;
        public readonly IReadOnlyList<DimensionSpawnRule> SpawnRules;

        public DimensionBiomeGenerationBlueprint(
            bool success,
            string code,
            string message,
            string dimensionId,
            string biomeId,
            string displayName,
            bool hasBiome,
            DimensionBiomeDefinition biome,
            bool hasPalette,
            DimensionAssetReferenceDefinition palette,
            IReadOnlyList<DimensionAssetReferenceDefinition> paletteEntries,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionSceneDefinition> scenes,
            IReadOnlyList<DimensionResourceNodeDefinition> resourceNodes,
            IReadOnlyList<DimensionSpawnRule> spawnRules)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            HasBiome = hasBiome;
            Biome = biome;
            HasPalette = hasPalette;
            Palette = palette;
            PaletteEntries = paletteEntries ?? new List<DimensionAssetReferenceDefinition>();
            GenerationTables = generationTables ?? new List<DimensionGenerationTableDefinition>();
            Scenes = scenes ?? new List<DimensionSceneDefinition>();
            ResourceNodes = resourceNodes ?? new List<DimensionResourceNodeDefinition>();
            SpawnRules = spawnRules ?? new List<DimensionSpawnRule>();
            PaletteEntryCount = PaletteEntries.Count;
            GenerationTableCount = GenerationTables.Count;
            SceneCount = Scenes.Count;
            ResourceNodeCount = ResourceNodes.Count;
            SpawnRuleCount = SpawnRules.Count;
        }

        public bool HasGenerationContent
        {
            get
            {
                return PaletteEntryCount > 0
                    || GenerationTableCount > 0
                    || SceneCount > 0
                    || ResourceNodeCount > 0
                    || SpawnRuleCount > 0;
            }
        }
    }
}
