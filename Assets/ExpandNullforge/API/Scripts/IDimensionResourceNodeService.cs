using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionResourceNodeService
    {
        event Action<DimensionResourceNodeChangedEvent> ResourceNodeChanged;

        IReadOnlyList<DimensionResourceNodeDefinition> GetResourceNodes(
            DimensionResourceNodeQuery query);

        bool TryGetResourceNode(
            string nodeId,
            out DimensionResourceNodeDefinition node);

        bool TryRegisterResourceNode(
            DimensionResourceNodeDefinition node,
            out DimensionOperationResult result);

        bool TryUpdateResourceNode(
            DimensionResourceNodeDefinition node,
            string reason,
            out DimensionOperationResult result);

        bool TrySetResourceNodeEnabled(
            string nodeId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveResourceNode(
            string nodeId,
            out DimensionOperationResult result);
    }
}
