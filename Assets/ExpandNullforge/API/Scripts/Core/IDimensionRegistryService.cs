using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionRegistryService
    {
        int ApiVersion { get; }

        bool IsReady { get; }

        event Action<DimensionLifecycleEvent> DimensionLifecycleChanged;

        IReadOnlyList<DimensionDefinition> GetDimensions();

        bool TryGetDimension(string dimensionId, out DimensionDefinition definition);

        bool TryRegisterDimension(DimensionDefinition definition, out DimensionOperationResult result);

        bool TryUnregisterDimension(string dimensionId, out DimensionOperationResult result);

        bool TrySetDimensionLifecycle(
            string dimensionId,
            DimensionLifecycleState lifecycleState,
            string reason,
            out DimensionOperationResult result);
    }
}
