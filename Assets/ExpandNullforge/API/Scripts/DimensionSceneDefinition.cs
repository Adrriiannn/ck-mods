namespace ExpandNullforge.Api
{
    public readonly struct DimensionSceneDefinition
    {
        public readonly string SceneId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string Kind;
        public readonly int Priority;
        public readonly DimensionSceneState State;

        public DimensionSceneDefinition(
            string sceneId,
            string displayName,
            string dimensionId,
            DimensionBounds localBounds,
            string kind,
            int priority,
            DimensionSceneState state)
        {
            SceneId = sceneId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            Kind = kind ?? string.Empty;
            Priority = priority;
            State = state;
        }
    }
}
