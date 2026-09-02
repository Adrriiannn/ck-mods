namespace ExpandNullforge.Api
{
    public readonly struct DimensionLifecycleEvent
    {
        public readonly string DimensionId;
        public readonly DimensionLifecycleState PreviousState;
        public readonly DimensionLifecycleState CurrentState;
        public readonly string Reason;

        public DimensionLifecycleEvent(
            string dimensionId,
            DimensionLifecycleState previousState,
            DimensionLifecycleState currentState,
            string reason)
        {
            DimensionId = dimensionId ?? string.Empty;
            PreviousState = previousState;
            CurrentState = currentState;
            Reason = reason ?? string.Empty;
        }
    }
}
