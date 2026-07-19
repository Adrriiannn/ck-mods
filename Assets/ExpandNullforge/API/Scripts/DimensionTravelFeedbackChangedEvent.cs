namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelFeedbackChangedEvent
    {
        public readonly DimensionTravelFeedbackSnapshot Previous;
        public readonly DimensionTravelFeedbackSnapshot Current;

        public DimensionTravelFeedbackChangedEvent(
            DimensionTravelFeedbackSnapshot previous,
            DimensionTravelFeedbackSnapshot current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
