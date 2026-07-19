using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Provider-facing snapshot of the generation context resolved into deterministic bounds, seed, biome, and tables.
    /// This keeps individual biome/terrain providers from reimplementing their own local-bounds, biome, and table lookup rules.
    /// </summary>
    public readonly struct DimensionGenerationPassWorkPlan
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string Message;
        public readonly DimensionGenerationPassContext PassContext;
        public readonly DimensionDefinition Dimension;
        public readonly DimensionArea Area;
        public readonly DimensionBounds RequestedLocalBounds;
        public readonly DimensionBounds EffectiveLocalBounds;
        public readonly float2 SampleLocalPosition;
        public readonly DimensionGenerationTableKind TableKind;
        public readonly uint Seed;
        public readonly bool HasBiome;
        public readonly DimensionBiomeDefinition Biome;
        public readonly DimensionGenerationTableResolutionResult TableResolution;
        public readonly DimensionGenerationTableSelectionResult TableSelection;

        public DimensionGenerationPassWorkPlan(
            bool success,
            string code,
            string message,
            DimensionGenerationPassContext passContext,
            DimensionDefinition dimension,
            DimensionArea area,
            DimensionBounds requestedLocalBounds,
            DimensionBounds effectiveLocalBounds,
            float2 sampleLocalPosition,
            DimensionGenerationTableKind tableKind,
            uint seed,
            bool hasBiome,
            DimensionBiomeDefinition biome,
            DimensionGenerationTableResolutionResult tableResolution,
            DimensionGenerationTableSelectionResult tableSelection)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            PassContext = passContext;
            Dimension = dimension;
            Area = area;
            RequestedLocalBounds = requestedLocalBounds;
            EffectiveLocalBounds = effectiveLocalBounds;
            SampleLocalPosition = sampleLocalPosition;
            TableKind = tableKind;
            Seed = seed;
            HasBiome = hasBiome;
            Biome = biome;
            TableResolution = tableResolution;
            TableSelection = tableSelection;
        }

        public int2 EffectiveSize
        {
            get { return EffectiveLocalBounds.Size; }
        }

        public bool HasTableContent
        {
            get { return TableResolution.Success && TableResolution.TotalEntryCount > 0; }
        }

        public bool HasSelectedEntries
        {
            get { return TableSelection.Success && TableSelection.SelectionCount > 0; }
        }
    }
}
