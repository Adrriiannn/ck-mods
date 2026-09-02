using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionCoordinateCompatibilityResult
    {
        public readonly bool Resolved;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly float2 AbsolutePosition;
        public readonly DimensionContext PlayerContext;
        public readonly string Code;
        public readonly string Message;

        public DimensionCoordinateCompatibilityResult(
            bool resolved,
            string dimensionId,
            float2 localPosition,
            float2 absolutePosition,
            DimensionContext playerContext,
            string code,
            string message)
        {
            Resolved = resolved;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            AbsolutePosition = absolutePosition;
            PlayerContext = playerContext;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static DimensionCoordinateCompatibilityResult Failed(
            string code,
            string message,
            DimensionContext playerContext)
        {
            return new DimensionCoordinateCompatibilityResult(
                false,
                string.Empty,
                default(float2),
                default(float2),
                playerContext,
                code,
                message);
        }
    }
}
