namespace ExpandNullforge.Api
{
    public readonly struct DimensionLoadTicketSnapshotQuery
    {
        public readonly string TicketId;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly bool MatchLocalBounds;
        public readonly DimensionLoadState State;
        public readonly bool MatchState;
        public readonly bool KeepTilesResident;
        public readonly bool MatchKeepTilesResident;
        public readonly bool EnableSimulation;
        public readonly bool MatchEnableSimulation;

        public DimensionLoadTicketSnapshotQuery(
            string ticketId,
            string dimensionId,
            DimensionBounds localBounds,
            bool matchLocalBounds,
            DimensionLoadState state,
            bool matchState,
            bool keepTilesResident,
            bool matchKeepTilesResident,
            bool enableSimulation,
            bool matchEnableSimulation)
        {
            TicketId = ticketId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            MatchLocalBounds = matchLocalBounds;
            State = state;
            MatchState = matchState;
            KeepTilesResident = keepTilesResident;
            MatchKeepTilesResident = matchKeepTilesResident;
            EnableSimulation = enableSimulation;
            MatchEnableSimulation = matchEnableSimulation;
        }
    }
}
