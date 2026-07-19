using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionTravelService
    {
        event Action<DimensionTravelSnapshot> DimensionTravelUpdated;

        event Action<DimensionPortalChangedEvent> PortalChanged;

        bool CanTravel(DimensionTravelRequest request, out DimensionAccessResult accessResult);

        DimensionTravelResult RequestTravel(DimensionTravelRequest request);

        bool TryCancelTravel(
            DimensionTravelCancelRequest request,
            out DimensionOperationResult result);

        bool TryCreatePortalTravelRequest(
            DimensionPortalTravelRequest request,
            out DimensionTravelRequest travelRequest,
            out DimensionOperationResult result);

        DimensionTravelPreviewResult PreviewPortalTravel(DimensionPortalTravelRequest request);

        DimensionTravelResult RequestPortalTravel(DimensionPortalTravelRequest request);

        IReadOnlyList<DimensionPortalDefinition> GetPortals(string dimensionId);

        bool TryGetPortal(string portalId, out DimensionPortalDefinition portal);

        bool TryRegisterPortal(DimensionPortalDefinition portal, out DimensionOperationResult result);

        bool TrySetPortalState(
            string portalId,
            DimensionPortalState state,
            string reason,
            out DimensionOperationResult result);

        bool TryRemovePortal(string portalId, out DimensionOperationResult result);
    }
}
