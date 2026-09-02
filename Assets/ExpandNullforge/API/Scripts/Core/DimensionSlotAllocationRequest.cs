using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionSlotAllocationRequest
    {
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly bool UseFixedAbsoluteOrigin;
        public readonly int2 FixedAbsoluteOrigin;
        public readonly int BaseOffsetTiles;
        public readonly int StepTiles;
        public readonly int SafetyMarginTiles;
        public readonly int MaximumSearchRings;

        public DimensionSlotAllocationRequest(
            string dimensionId,
            DimensionBounds localBounds,
            bool useFixedAbsoluteOrigin,
            int2 fixedAbsoluteOrigin,
            int baseOffsetTiles,
            int stepTiles,
            int safetyMarginTiles,
            int maximumSearchRings)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            UseFixedAbsoluteOrigin = useFixedAbsoluteOrigin;
            FixedAbsoluteOrigin = fixedAbsoluteOrigin;
            BaseOffsetTiles = baseOffsetTiles;
            StepTiles = stepTiles;
            SafetyMarginTiles = safetyMarginTiles;
            MaximumSearchRings = maximumSearchRings;
        }

        public static DimensionSlotAllocationRequest Auto(
            string dimensionId,
            DimensionBounds localBounds)
        {
            return new DimensionSlotAllocationRequest(
                dimensionId,
                localBounds,
                false,
                default,
                100000,
                100000,
                8192,
                16);
        }

        public static DimensionSlotAllocationRequest Fixed(
            string dimensionId,
            DimensionBounds localBounds,
            int2 absoluteOrigin)
        {
            return new DimensionSlotAllocationRequest(
                dimensionId,
                localBounds,
                true,
                absoluteOrigin,
                100000,
                100000,
                8192,
                16);
        }
    }
}
