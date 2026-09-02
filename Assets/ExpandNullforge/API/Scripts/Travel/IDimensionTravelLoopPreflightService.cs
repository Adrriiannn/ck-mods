namespace ExpandNullforge.Api
{
    public interface IDimensionTravelLoopPreflightService
    {
        DimensionTravelLoopPreflightResult PreflightTravelLoop(
            DimensionTravelLoopPreflightRequest request);

        DimensionTravelLoopPreflightResult PreflightStarterTravelLoop(
            string starterId);
    }
}
