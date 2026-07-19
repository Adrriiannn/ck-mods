using ExpandNullforge.Api;

namespace ExpandNullforge.Networking
{
  public static class DimensionPortalTravelActions
  {
    public static uint RequestPortalTravel(
        string portalId,
        bool requireGeneratedArea,
        bool allowFallbackPosition,
        string reason)
    {
      return DimensionTravelNetworkState.RequestPortalTravel(
          portalId,
          requireGeneratedArea,
          allowFallbackPosition,
          reason);
    }

    public static uint RequestPortalTravel(
        DimensionPortalTravelRequest request)
    {
      return DimensionTravelNetworkState.RequestPortalTravel(request);
    }
  }
}
