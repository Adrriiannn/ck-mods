using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionCompiledGenerationPlan
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly string DisplayName;
        public readonly DimensionBounds ReservedLocalBounds;
        public readonly DimensionBounds PlayableLocalBounds;
        public readonly int CoordinateShellPaddingTiles;
        public readonly IReadOnlyList<DimensionCompiledBiomeRegion> BiomeRegions;
        public readonly IReadOnlyList<DimensionCompiledScenePlacement> ScenePlacements;
        public readonly IReadOnlyList<DimensionResourceNodeDefinition> ResourceNodes;
        public readonly IReadOnlyList<DimensionSpawnRule> SpawnRules;
        public readonly IReadOnlyList<DimensionGenerationPassDefinition> GenerationPasses;
        public readonly IReadOnlyList<DimensionAuthoringIssue> Issues;

        public DimensionCompiledGenerationPlan(
            bool success,
            string code,
            string message,
            string dimensionId,
            string displayName,
            DimensionBounds reservedLocalBounds,
            DimensionBounds playableLocalBounds,
            int coordinateShellPaddingTiles,
            IReadOnlyList<DimensionCompiledBiomeRegion> biomeRegions,
            IReadOnlyList<DimensionCompiledScenePlacement> scenePlacements,
            IReadOnlyList<DimensionResourceNodeDefinition> resourceNodes,
            IReadOnlyList<DimensionSpawnRule> spawnRules,
            IReadOnlyList<DimensionGenerationPassDefinition> generationPasses,
            IReadOnlyList<DimensionAuthoringIssue> issues)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ReservedLocalBounds = reservedLocalBounds;
            PlayableLocalBounds = playableLocalBounds;
            CoordinateShellPaddingTiles = coordinateShellPaddingTiles;
            BiomeRegions = biomeRegions;
            ScenePlacements = scenePlacements;
            ResourceNodes = resourceNodes;
            SpawnRules = spawnRules;
            GenerationPasses = generationPasses;
            Issues = issues;
        }
    }
}
