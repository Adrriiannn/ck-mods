using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelRequest
    {
        public readonly Entity Player;
        public readonly string TargetDimensionId;
        public readonly float2 TargetLocalPosition;
        public readonly bool RequireGeneratedArea;
        public readonly bool AllowFallbackPosition;
        public readonly string PortalId;
        public readonly string Reason;

        public DimensionTravelRequest(
            Entity player,
            string targetDimensionId,
            float2 targetLocalPosition,
            bool requireGeneratedArea,
            bool allowFallbackPosition,
            string reason)
            : this(
                player,
                targetDimensionId,
                targetLocalPosition,
                requireGeneratedArea,
                allowFallbackPosition,
                string.Empty,
                reason)
        {
        }

        public DimensionTravelRequest(
            Entity player,
            string targetDimensionId,
            float2 targetLocalPosition,
            bool requireGeneratedArea,
            bool allowFallbackPosition,
            string portalId,
            string reason)
        {
            Player = player;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            TargetLocalPosition = targetLocalPosition;
            RequireGeneratedArea = requireGeneratedArea;
            AllowFallbackPosition = allowFallbackPosition;
            PortalId = portalId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
