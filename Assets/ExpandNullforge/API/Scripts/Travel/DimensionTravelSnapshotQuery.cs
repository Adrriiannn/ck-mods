namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelSnapshotQuery
    {
        public readonly string TravelId;
        public readonly string PlayerId;
        public readonly string TargetDimensionId;
        public readonly DimensionTravelState State;
        public readonly bool MatchState;

        public DimensionTravelSnapshotQuery(
            string travelId,
            string playerId,
            string targetDimensionId,
            DimensionTravelState state,
            bool matchState)
        {
            TravelId = travelId ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            State = state;
            MatchState = matchState;
        }
    }
}
