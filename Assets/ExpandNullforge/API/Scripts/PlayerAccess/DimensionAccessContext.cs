using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionAccessContext
    {
        public readonly Entity Player;
        public readonly DimensionContext SourceContext;
        public readonly DimensionDefinition TargetDimension;
        public readonly float2 TargetLocalPosition;
        public readonly float2 TargetAbsolutePosition;
        public readonly bool RequireGeneratedArea;
        public readonly bool AllowFallbackPosition;
        public readonly string PortalId;
        public readonly string Reason;

        public DimensionAccessContext(
            Entity player,
            DimensionContext sourceContext,
            DimensionDefinition targetDimension,
            float2 targetLocalPosition,
            float2 targetAbsolutePosition,
            bool requireGeneratedArea,
            bool allowFallbackPosition,
            string portalId,
            string reason)
        {
            Player = player;
            SourceContext = sourceContext;
            TargetDimension = targetDimension;
            TargetLocalPosition = targetLocalPosition;
            TargetAbsolutePosition = targetAbsolutePosition;
            RequireGeneratedArea = requireGeneratedArea;
            AllowFallbackPosition = allowFallbackPosition;
            PortalId = portalId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
