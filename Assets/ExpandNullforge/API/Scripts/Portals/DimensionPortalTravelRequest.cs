using Unity.Entities;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionPortalTravelRequest
    {
        public readonly Entity Player;
        public readonly string PortalId;
        public readonly bool RequireGeneratedArea;
        public readonly bool AllowFallbackPosition;
        public readonly string Reason;

        public DimensionPortalTravelRequest(
            Entity player,
            string portalId,
            bool requireGeneratedArea,
            bool allowFallbackPosition,
            string reason)
        {
            Player = player;
            PortalId = portalId ?? string.Empty;
            RequireGeneratedArea = requireGeneratedArea;
            AllowFallbackPosition = allowFallbackPosition;
            Reason = reason ?? string.Empty;
        }
    }
}
