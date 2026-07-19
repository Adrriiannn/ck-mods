using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionProgressService
    {
        event Action<DimensionProgressFlagChangedEvent> ProgressFlagChanged;

        IReadOnlyList<DimensionProgressFlag> GetProgressFlags(
            string dimensionId,
            string category);

        bool TryGetProgressFlag(string flagId, out DimensionProgressFlag flag);

        bool IsProgressFlagSet(string flagId);

        bool TrySetProgressFlag(
            string flagId,
            string dimensionId,
            string category,
            bool value,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveProgressFlag(string flagId, out DimensionOperationResult result);
    }
}
