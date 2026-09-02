namespace ExpandNullforge.Api
{
    public readonly struct DimensionCompiledScenePlacement
    {
        public readonly string DimensionId;
        public readonly string SceneId;
        public readonly string TemplateId;
        public readonly string DisplayName;
        public readonly string BiomeId;
        public readonly DimensionBounds LocalBounds;
        public readonly bool HasLocalBounds;
        public readonly bool Exact;
        public readonly bool Required;
        public readonly int Priority;

        public DimensionCompiledScenePlacement(
            string dimensionId,
            string sceneId,
            string templateId,
            string displayName,
            string biomeId,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            bool exact,
            bool required,
            int priority)
        {
            DimensionId = dimensionId ?? string.Empty;
            SceneId = sceneId ?? string.Empty;
            TemplateId = templateId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            Exact = exact;
            Required = required;
            Priority = priority;
        }
    }
}
