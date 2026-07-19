using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionRespawnTarget
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly float2 AbsolutePosition;
        public readonly string AnchorId;
        public readonly DimensionAnchorKind AnchorKind;
        public readonly bool GenerationReady;

        public DimensionRespawnTarget(
            bool success,
            string code,
            string message,
            string dimensionId,
            float2 localPosition,
            float2 absolutePosition,
            string anchorId,
            DimensionAnchorKind anchorKind,
            bool generationReady)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            AbsolutePosition = absolutePosition;
            AnchorId = anchorId ?? string.Empty;
            AnchorKind = anchorKind;
            GenerationReady = generationReady;
        }

        public static DimensionRespawnTarget Failed(string code, string message)
        {
            return new DimensionRespawnTarget(
                false,
                code,
                message,
                string.Empty,
                default(float2),
                default(float2),
                string.Empty,
                DimensionAnchorKind.Any,
                false);
        }

        public static DimensionRespawnTarget Resolved(
            string dimensionId,
            float2 localPosition,
            float2 absolutePosition,
            string anchorId,
            DimensionAnchorKind anchorKind,
            bool generationReady)
        {
            return new DimensionRespawnTarget(
                true,
                string.Empty,
                string.Empty,
                dimensionId,
                localPosition,
                absolutePosition,
                anchorId,
                anchorKind,
                generationReady);
        }
    }
}
