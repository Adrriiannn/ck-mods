using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Dimension-aware position information for a player/entity/absolute point.
    /// </summary>
    public readonly struct DimensionContext
    {
        public readonly bool IsKnown;
        public readonly string DimensionId;
        public readonly float2 AbsolutePosition;
        public readonly float2 LocalPosition;

        public DimensionContext(bool isKnown, string dimensionId, float2 absolutePosition, float2 localPosition)
        {
            IsKnown = isKnown;
            DimensionId = dimensionId;
            AbsolutePosition = absolutePosition;
            LocalPosition = localPosition;
        }

        public bool IsOverworld
        {
            get { return DimensionId == DimensionIds.Overworld; }
        }

        public static DimensionContext Unknown(float2 absolutePosition)
        {
            return new DimensionContext(false, string.Empty, absolutePosition, absolutePosition);
        }

        public static DimensionContext Overworld(float2 absolutePosition)
        {
            return new DimensionContext(true, DimensionIds.Overworld, absolutePosition, absolutePosition);
        }
    }
}
