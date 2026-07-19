using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionAnchorDefinition
    {
        public readonly string AnchorId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly float2 LocalPosition;
        public readonly DimensionAnchorKind Kind;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionAnchorDefinition(
            string anchorId,
            string displayName,
            string dimensionId,
            float2 localPosition,
            DimensionAnchorKind kind,
            int priority,
            bool enabled)
        {
            AnchorId = anchorId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalPosition = localPosition;
            Kind = kind;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
