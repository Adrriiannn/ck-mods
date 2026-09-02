using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionPortalDefinition
    {
        public readonly string PortalId;
        public readonly string DisplayName;
        public readonly string FromDimensionId;
        public readonly float2 FromLocalPosition;
        public readonly string ToDimensionId;
        public readonly float2 ToLocalPosition;
        public readonly DimensionPortalState State;

        public DimensionPortalDefinition(
            string portalId,
            string displayName,
            string fromDimensionId,
            float2 fromLocalPosition,
            string toDimensionId,
            float2 toLocalPosition,
            DimensionPortalState state)
        {
            PortalId = portalId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FromDimensionId = fromDimensionId ?? string.Empty;
            FromLocalPosition = fromLocalPosition;
            ToDimensionId = toDimensionId ?? string.Empty;
            ToLocalPosition = toLocalPosition;
            State = state;
        }
    }
}
