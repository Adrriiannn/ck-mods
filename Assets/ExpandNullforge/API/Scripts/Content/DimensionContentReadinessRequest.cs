using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentReadinessRequest
    {
        public readonly string RequestId;
        public readonly string DisplayName;
        public readonly IReadOnlyList<DimensionContentReadinessRequirement> Requirements;

        public DimensionContentReadinessRequest(
            string requestId,
            string displayName,
            IReadOnlyList<DimensionContentReadinessRequirement> requirements)
        {
            RequestId = requestId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Requirements = requirements ?? new List<DimensionContentReadinessRequirement>();
        }
    }
}
