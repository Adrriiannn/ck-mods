using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionLandingValidationResult
    {
        public readonly bool Valid;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly float2 RequestedLocalPosition;
        public readonly float2 ResolvedLocalPosition;
        public readonly float2 ResolvedAbsolutePosition;
        public readonly DimensionBounds RequiredLocalBounds;
        public readonly bool GenerationReady;
        public readonly bool UsedFallback;
        public readonly string FallbackAnchorId;

        public DimensionLandingValidationResult(
            bool valid,
            string code,
            string message,
            string dimensionId,
            float2 requestedLocalPosition,
            float2 resolvedLocalPosition,
            float2 resolvedAbsolutePosition,
            DimensionBounds requiredLocalBounds,
            bool generationReady,
            bool usedFallback,
            string fallbackAnchorId)
        {
            Valid = valid;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            RequestedLocalPosition = requestedLocalPosition;
            ResolvedLocalPosition = resolvedLocalPosition;
            ResolvedAbsolutePosition = resolvedAbsolutePosition;
            RequiredLocalBounds = requiredLocalBounds;
            GenerationReady = generationReady;
            UsedFallback = usedFallback;
            FallbackAnchorId = fallbackAnchorId ?? string.Empty;
        }
    }
}
