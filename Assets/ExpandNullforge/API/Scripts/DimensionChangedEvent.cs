using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionChangedEvent
    {
        public readonly Entity Player;
        public readonly string PreviousDimensionId;
        public readonly string CurrentDimensionId;
        public readonly float2 PreviousAbsolutePosition;
        public readonly float2 CurrentAbsolutePosition;

        public DimensionChangedEvent(
            Entity player,
            string previousDimensionId,
            string currentDimensionId,
            float2 previousAbsolutePosition,
            float2 currentAbsolutePosition)
        {
            Player = player;
            PreviousDimensionId = previousDimensionId;
            CurrentDimensionId = currentDimensionId;
            PreviousAbsolutePosition = previousAbsolutePosition;
            CurrentAbsolutePosition = currentAbsolutePosition;
        }
    }
}
