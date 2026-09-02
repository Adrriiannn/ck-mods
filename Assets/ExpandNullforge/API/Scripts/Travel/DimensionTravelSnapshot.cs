using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelSnapshot
    {
        public readonly string TravelId;
        public readonly Entity Player;
        public readonly string PlayerId;
        public readonly string PreviousDimensionId;
        public readonly string TargetDimensionId;
        public readonly float2 PreviousAbsolutePosition;
        public readonly float2 TargetLocalPosition;
        public readonly float2 TargetAbsolutePosition;
        public readonly string LoadTicketId;
        public readonly DimensionTravelState State;
        public readonly string Message;
        public readonly double CreatedAt;
        public readonly double UpdatedAt;

        public DimensionTravelSnapshot(
            string travelId,
            Entity player,
            string playerId,
            string previousDimensionId,
            string targetDimensionId,
            float2 previousAbsolutePosition,
            float2 targetLocalPosition,
            float2 targetAbsolutePosition,
            string loadTicketId,
            DimensionTravelState state,
            string message,
            double createdAt,
            double updatedAt)
        {
            TravelId = travelId ?? string.Empty;
            Player = player;
            PlayerId = playerId ?? string.Empty;
            PreviousDimensionId = previousDimensionId ?? string.Empty;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            PreviousAbsolutePosition = previousAbsolutePosition;
            TargetLocalPosition = targetLocalPosition;
            TargetAbsolutePosition = targetAbsolutePosition;
            LoadTicketId = loadTicketId ?? string.Empty;
            State = state;
            Message = message ?? string.Empty;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }
    }
}
