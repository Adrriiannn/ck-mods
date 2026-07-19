using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Persisted absolute coordinate slot assigned to a registered dimension.
    /// Slot records let dimension packs avoid hardcoded 100k coordinate collisions
    /// while still exposing the absolute-vs-local mapping for interoperability.
    /// </summary>
    public readonly struct DimensionSlotRecord
    {
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly int2 AbsoluteOrigin;
        public readonly int CandidateIndex;
        public readonly bool UsedFixedOrigin;
        public readonly long AssignedUtcTicks;
        public readonly string AllocationCode;
        public readonly string AllocationMessage;

        public DimensionSlotRecord(
            string dimensionId,
            DimensionBounds localBounds,
            int2 absoluteOrigin,
            int candidateIndex,
            bool usedFixedOrigin,
            long assignedUtcTicks,
            string allocationCode,
            string allocationMessage)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            AbsoluteOrigin = absoluteOrigin;
            CandidateIndex = candidateIndex;
            UsedFixedOrigin = usedFixedOrigin;
            AssignedUtcTicks = assignedUtcTicks;
            AllocationCode = allocationCode ?? string.Empty;
            AllocationMessage = allocationMessage ?? string.Empty;
        }

        public DimensionBounds AbsoluteBounds
        {
            get
            {
                return new DimensionBounds(
                    AbsoluteOrigin + LocalBounds.Min,
                    AbsoluteOrigin + LocalBounds.MaxExclusive);
            }
        }
    }
}
