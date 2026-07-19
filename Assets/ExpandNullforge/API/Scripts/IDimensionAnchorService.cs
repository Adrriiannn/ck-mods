using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionAnchorService
    {
        event Action<DimensionAnchorChangedEvent> AnchorChanged;

        IReadOnlyList<DimensionAnchorDefinition> GetAnchors(
            string dimensionId,
            DimensionAnchorKind kind,
            bool enabledOnly);

        bool TryGetAnchor(string anchorId, out DimensionAnchorDefinition anchor);

        bool TryResolveBestAnchor(
            string dimensionId,
            DimensionAnchorKind kind,
            out DimensionAnchorDefinition anchor);

        bool TryRegisterAnchor(DimensionAnchorDefinition anchor, out DimensionOperationResult result);

        bool TryUpdateAnchor(
            DimensionAnchorDefinition anchor,
            string reason,
            out DimensionOperationResult result);

        bool TrySetAnchorEnabled(
            string anchorId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveAnchor(string anchorId, out DimensionOperationResult result);
    }
}
