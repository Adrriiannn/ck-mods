using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionLandingValidationRequest
    {
        public readonly Entity Player;
        public readonly string TargetDimensionId;
        public readonly float2 TargetLocalPosition;
        public readonly bool RequireGeneratedArea;
        public readonly bool RequirePlayerTravelCapability;
        public readonly bool EvaluateAccessProviders;
        public readonly bool AllowFallbackPosition;
        public readonly int AreaSideTiles;
        public readonly string PortalId;
        public readonly string Reason;

        public DimensionLandingValidationRequest(
            Entity player,
            string targetDimensionId,
            float2 targetLocalPosition,
            bool requireGeneratedArea,
            bool requirePlayerTravelCapability,
            bool evaluateAccessProviders,
            bool allowFallbackPosition,
            int areaSideTiles,
            string portalId,
            string reason)
        {
            Player = player;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            TargetLocalPosition = targetLocalPosition;
            RequireGeneratedArea = requireGeneratedArea;
            RequirePlayerTravelCapability = requirePlayerTravelCapability;
            EvaluateAccessProviders = evaluateAccessProviders;
            AllowFallbackPosition = allowFallbackPosition;
            AreaSideTiles = areaSideTiles;
            PortalId = portalId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public static DimensionLandingValidationRequest FromTravelRequest(
            DimensionTravelRequest request)
        {
            return new DimensionLandingValidationRequest(
                request.Player,
                request.TargetDimensionId,
                request.TargetLocalPosition,
                request.RequireGeneratedArea,
                true,
                true,
                request.AllowFallbackPosition,
                1,
                request.PortalId,
                request.Reason);
        }
    }
}
