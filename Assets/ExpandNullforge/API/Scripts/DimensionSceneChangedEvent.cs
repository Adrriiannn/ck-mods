namespace ExpandNullforge.Api
{
    public readonly struct DimensionSceneChangedEvent
    {
        public readonly DimensionSceneDefinition Scene;
        public readonly DimensionSceneChangeKind ChangeKind;
        public readonly DimensionSceneState PreviousState;
        public readonly DimensionSceneState CurrentState;
        public readonly string Reason;

        public DimensionSceneChangedEvent(
            DimensionSceneDefinition scene,
            DimensionSceneChangeKind changeKind,
            DimensionSceneState previousState,
            DimensionSceneState currentState,
            string reason)
        {
            Scene = scene;
            ChangeKind = changeKind;
            PreviousState = previousState;
            CurrentState = currentState;
            Reason = reason ?? string.Empty;
        }
    }
}
