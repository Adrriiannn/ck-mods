using Unity.Entities;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelCancelRequest
    {
        public readonly Entity Player;
        public readonly string TravelId;
        public readonly string Reason;

        public DimensionTravelCancelRequest(
            Entity player,
            string reason)
            : this(player, string.Empty, reason)
        {
        }

        public DimensionTravelCancelRequest(
            string travelId,
            string reason)
            : this(Entity.Null, travelId, reason)
        {
        }

        public DimensionTravelCancelRequest(
            Entity player,
            string travelId,
            string reason)
        {
            Player = player;
            TravelId = travelId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
