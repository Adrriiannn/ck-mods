using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionPlayerVisitRecord
    {
        public readonly string PlayerId;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly float2 AbsolutePosition;
        public readonly long SavedUtcTicks;

        public DimensionPlayerVisitRecord(
            string playerId,
            string dimensionId,
            float2 localPosition,
            float2 absolutePosition,
            long savedUtcTicks)
        {
            PlayerId = playerId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            AbsolutePosition = absolutePosition;
            SavedUtcTicks = savedUtcTicks;
        }
    }
}
