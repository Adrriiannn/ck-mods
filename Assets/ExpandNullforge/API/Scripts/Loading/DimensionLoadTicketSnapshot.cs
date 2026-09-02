namespace ExpandNullforge.Api
{
    public readonly struct DimensionLoadTicketSnapshot
    {
        public readonly string TicketId;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly DimensionBounds AbsoluteBounds;
        public readonly bool KeepTilesResident;
        public readonly bool EnableSimulation;
        public readonly DimensionLoadState State;
        public readonly string Message;
        public readonly double CreatedAtSeconds;
        public readonly double ParentSubMapsObservedAtSeconds;
        public readonly int RequiredParentSubMapCount;

        public DimensionLoadTicketSnapshot(
            string ticketId,
            string dimensionId,
            DimensionBounds localBounds,
            DimensionBounds absoluteBounds,
            bool keepTilesResident,
            bool enableSimulation,
            DimensionLoadState state,
            string message,
            double createdAtSeconds,
            double parentSubMapsObservedAtSeconds,
            int requiredParentSubMapCount)
        {
            TicketId = ticketId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            AbsoluteBounds = absoluteBounds;
            KeepTilesResident = keepTilesResident;
            EnableSimulation = enableSimulation;
            State = state;
            Message = message ?? string.Empty;
            CreatedAtSeconds = createdAtSeconds;
            ParentSubMapsObservedAtSeconds = parentSubMapsObservedAtSeconds;
            RequiredParentSubMapCount = requiredParentSubMapCount;
        }
    }
}
