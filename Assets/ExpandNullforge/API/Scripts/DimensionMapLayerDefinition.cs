namespace ExpandNullforge.Api
{
    public readonly struct DimensionMapLayerDefinition
    {
        public readonly string LayerId;
        public readonly string DimensionId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string IconId;
        public readonly bool HasTintColor;
        public readonly uint TintColorRgba;
        public readonly int Priority;
        public readonly bool Visible;
        public readonly bool Selectable;

        public DimensionMapLayerDefinition(
            string layerId,
            string dimensionId,
            string displayName,
            string description,
            string iconId,
            bool hasTintColor,
            uint tintColorRgba,
            int priority,
            bool visible,
            bool selectable)
        {
            LayerId = layerId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            IconId = iconId ?? string.Empty;
            HasTintColor = hasTintColor;
            TintColorRgba = tintColorRgba;
            Priority = priority;
            Visible = visible;
            Selectable = selectable;
        }
    }
}
