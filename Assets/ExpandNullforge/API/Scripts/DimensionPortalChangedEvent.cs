namespace ExpandNullforge.Api
{
    public readonly struct DimensionPortalChangedEvent
    {
        public readonly DimensionPortalDefinition Portal;
        public readonly DimensionPortalState PreviousState;
        public readonly DimensionPortalState CurrentState;
        public readonly DimensionPortalChangeKind ChangeKind;
        public readonly string Reason;

        public DimensionPortalChangedEvent(
            DimensionPortalDefinition portal,
            DimensionPortalState previousState,
            DimensionPortalState currentState,
            DimensionPortalChangeKind changeKind,
            string reason)
        {
            Portal = portal;
            PreviousState = previousState;
            CurrentState = currentState;
            ChangeKind = changeKind;
            Reason = reason ?? string.Empty;
        }
    }
}
