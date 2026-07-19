using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPlan
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string ZoneId;
        public readonly IReadOnlyList<DimensionGenerationPassDefinition> Passes;

        public DimensionGenerationPlan(
            bool success,
            string message,
            string dimensionId,
            DimensionBounds localBounds,
            string zoneId,
            IReadOnlyList<DimensionGenerationPassDefinition> passes)
        {
            Success = success;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            ZoneId = zoneId ?? string.Empty;
            Passes = passes;
        }
    }
}
