using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableResolutionResult
    {
        public readonly bool Success;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly bool HasZone;
        public readonly DimensionZoneInfo Zone;
        public readonly bool HasBiome;
        public readonly DimensionBiomeDefinition Biome;
        public readonly DimensionGenerationTableKind Kind;
        public readonly IReadOnlyList<DimensionResolvedGenerationTable> Tables;
        public readonly int TotalTableCount;
        public readonly int TotalEntryCount;
        public readonly int TotalWeight;
        public readonly string Code;
        public readonly string Message;

        public DimensionGenerationTableResolutionResult(
            bool success,
            string dimensionId,
            float2 localPosition,
            bool hasZone,
            DimensionZoneInfo zone,
            bool hasBiome,
            DimensionBiomeDefinition biome,
            DimensionGenerationTableKind kind,
            IReadOnlyList<DimensionResolvedGenerationTable> tables,
            int totalTableCount,
            int totalEntryCount,
            int totalWeight,
            string code,
            string message)
        {
            Success = success;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            HasZone = hasZone;
            Zone = zone;
            HasBiome = hasBiome;
            Biome = biome;
            Kind = kind;
            Tables = tables;
            TotalTableCount = totalTableCount;
            TotalEntryCount = totalEntryCount;
            TotalWeight = totalWeight;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
