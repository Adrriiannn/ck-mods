using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionMarkerQuery
    {
        public readonly string DimensionId;
        public readonly float2 CenterLocalPosition;
        public readonly float Radius;
        public readonly bool IncludeHidden;

        public DimensionMarkerQuery(
            string dimensionId,
            float2 centerLocalPosition,
            float radius,
            bool includeHidden)
        {
            DimensionId = dimensionId ?? string.Empty;
            CenterLocalPosition = centerLocalPosition;
            Radius = radius;
            IncludeHidden = includeHidden;
        }
    }
}
