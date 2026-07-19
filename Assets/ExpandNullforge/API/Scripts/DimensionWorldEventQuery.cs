using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionWorldEventQuery
    {
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly bool HasLocalPosition;
        public readonly float2 LocalPosition;
        public readonly DimensionWorldEventKind Kind;
        public readonly bool EnabledOnly;

        public DimensionWorldEventQuery(
            string dimensionId,
            string zoneId,
            bool hasLocalPosition,
            float2 localPosition,
            DimensionWorldEventKind kind,
            bool enabledOnly)
        {
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            HasLocalPosition = hasLocalPosition;
            LocalPosition = localPosition;
            Kind = kind;
            EnabledOnly = enabledOnly;
        }
    }
}
