using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionReturnTargetResult
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly float2 AbsolutePosition;
        public readonly bool UsedPlayerVisit;
        public readonly string AnchorId;

        public DimensionReturnTargetResult(
            bool success,
            string code,
            string message,
            string dimensionId,
            float2 localPosition,
            float2 absolutePosition,
            bool usedPlayerVisit,
            string anchorId)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            AbsolutePosition = absolutePosition;
            UsedPlayerVisit = usedPlayerVisit;
            AnchorId = anchorId ?? string.Empty;
        }

        public static DimensionReturnTargetResult Failed(string code, string message)
        {
            return new DimensionReturnTargetResult(
                false,
                code,
                message,
                string.Empty,
                default(float2),
                default(float2),
                false,
                string.Empty);
        }
    }
}
