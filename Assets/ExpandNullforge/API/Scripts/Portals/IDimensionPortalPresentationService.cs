using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionPortalPresentationService
    {
        event Action<DimensionPortalPresentationChangedEvent> PortalPresentationChanged;

        IReadOnlyList<DimensionPortalPresentationDefinition> GetPortalPresentations(
            string portalId,
            bool includeDisabled);

        bool TryGetPortalPresentation(
            string presentationId,
            out DimensionPortalPresentationDefinition presentation);

        bool TryFindPortalPresentationForPortal(
            string portalId,
            out DimensionPortalPresentationDefinition presentation);

        bool TryRegisterPortalPresentation(
            DimensionPortalPresentationDefinition presentation,
            out DimensionOperationResult result);

        bool TryUpdatePortalPresentation(
            DimensionPortalPresentationDefinition presentation,
            string reason,
            out DimensionOperationResult result);

        bool TrySetPortalPresentationEnabled(
            string presentationId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemovePortalPresentation(
            string presentationId,
            out DimensionOperationResult result);
    }
}
