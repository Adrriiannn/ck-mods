using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionResolveResult
    {
        public readonly bool Success;
        public readonly string Reason;
        public readonly string DimensionId;
        public readonly float2 AbsolutePosition;
        public readonly float2 LocalPosition;

        public DimensionResolveResult(
            bool success,
            string reason,
            string dimensionId,
            float2 absolutePosition,
            float2 localPosition)
        {
            Success = success;
            Reason = reason;
            DimensionId = dimensionId;
            AbsolutePosition = absolutePosition;
            LocalPosition = localPosition;
        }

        public static DimensionResolveResult Failed(string reason)
        {
            return new DimensionResolveResult(false, reason, string.Empty, default(float2), default(float2));
        }

        public static DimensionResolveResult Resolved(string dimensionId, float2 absolutePosition, float2 localPosition)
        {
            return new DimensionResolveResult(true, string.Empty, dimensionId, absolutePosition, localPosition);
        }
    }
}
