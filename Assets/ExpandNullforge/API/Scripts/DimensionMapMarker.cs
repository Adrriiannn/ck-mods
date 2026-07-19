using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionMapMarker
    {
        public readonly string MarkerId;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly string Label;
        public readonly string Kind;
        public readonly bool Visible;

        public DimensionMapMarker(
            string markerId,
            string dimensionId,
            float2 localPosition,
            string label,
            string kind,
            bool visible)
        {
            MarkerId = markerId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            Label = label ?? string.Empty;
            Kind = kind ?? string.Empty;
            Visible = visible;
        }
    }
}
