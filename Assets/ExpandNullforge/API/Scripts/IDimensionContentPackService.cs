using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionContentPackService
    {
        event Action<DimensionContentPackChangedEvent> ContentPackChanged;

        IReadOnlyList<DimensionContentPackDefinition> GetContentPacks(bool includeDisabled);

        bool TryGetContentPack(
            string contentPackId,
            out DimensionContentPackDefinition contentPack);

        IReadOnlyList<string> GetMissingContentPackDependencies(string contentPackId);

        DimensionContentPackReadinessResult EvaluateContentPackReadiness(string contentPackId);

        IReadOnlyList<DimensionContentPackReadinessResult> EvaluateContentPackReadiness(bool includeDisabled);

        IReadOnlyList<DimensionContentPackSummary> GetContentPackSummaries(
            DimensionContentPackSummaryRequest request);

        bool TryRegisterContentPack(
            DimensionContentPackDefinition contentPack,
            out DimensionOperationResult result);

        bool TryUpdateContentPack(
            DimensionContentPackDefinition contentPack,
            string reason,
            out DimensionOperationResult result);

        bool TrySetContentPackEnabled(
            string contentPackId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveContentPack(
            string contentPackId,
            out DimensionOperationResult result);
    }
}
