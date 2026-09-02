using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionPortalItemPresentationService
    {
        IReadOnlyList<DimensionPortalItemPresentationDefinition> GetPortalItemPresentations(
            bool includeDisabled);

        bool TryFindPortalItemPresentationForObject(
            string portalObjectName,
            out DimensionPortalItemPresentationDefinition presentation);

        bool TryRegisterPortalItemPresentation(
            DimensionPortalItemPresentationDefinition presentation,
            out DimensionOperationResult result);
    }
}
