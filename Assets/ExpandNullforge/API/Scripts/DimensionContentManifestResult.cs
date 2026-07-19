using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentManifestResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int OperationCount;
        public readonly int ErrorCount;
        public readonly IReadOnlyList<DimensionContentManifestOperation> Operations;

        public DimensionContentManifestResult(
            bool success,
            bool applied,
            int operationCount,
            int errorCount,
            IReadOnlyList<DimensionContentManifestOperation> operations)
        {
            Success = success;
            Applied = applied;
            OperationCount = operationCount;
            ErrorCount = errorCount;
            Operations = operations ?? new List<DimensionContentManifestOperation>();
        }
    }
}
