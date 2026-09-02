using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionBiomeCatalogService
    {
        event Action<DimensionBiomeChangedEvent> BiomeChanged;

        IReadOnlyList<DimensionBiomeDefinition> GetBiomes(DimensionBiomeQuery query);

        DimensionBiomeResolutionResult ResolveBiomeAtLocal(
            string dimensionId,
            float2 localPosition);

        bool TryGetBiome(
            string biomeId,
            out DimensionBiomeDefinition biome);

        bool TryRegisterBiome(
            DimensionBiomeDefinition biome,
            out DimensionOperationResult result);

        bool TryUpdateBiome(
            DimensionBiomeDefinition biome,
            string reason,
            out DimensionOperationResult result);

        bool TrySetBiomeEnabled(
            string biomeId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveBiome(
            string biomeId,
            out DimensionOperationResult result);
    }
}
