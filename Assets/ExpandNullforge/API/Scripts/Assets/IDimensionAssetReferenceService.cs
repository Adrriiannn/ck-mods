using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionAssetReferenceService
    {
        event Action<DimensionAssetReferenceChangedEvent> AssetReferenceChanged;

        IReadOnlyList<DimensionAssetReferenceDefinition> GetAssetReferences(
            DimensionAssetReferenceQuery query);

        DimensionAssetReferenceResolutionResult ResolveAssetReferences(
            DimensionAssetReferenceResolutionRequest request);

        bool TryGetAssetReference(
            string assetId,
            out DimensionAssetReferenceDefinition assetReference);

        bool TryRegisterAssetReference(
            DimensionAssetReferenceDefinition assetReference,
            out DimensionOperationResult result);

        bool TryUpdateAssetReference(
            DimensionAssetReferenceDefinition assetReference,
            string reason,
            out DimensionOperationResult result);

        bool TrySetAssetReferenceEnabled(
            string assetId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveAssetReference(
            string assetId,
            out DimensionOperationResult result);
    }
}
