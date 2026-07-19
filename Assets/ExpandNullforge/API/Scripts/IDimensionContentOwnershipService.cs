using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionContentOwnershipService
    {
        event Action<DimensionContentOwnershipChangedEvent> ContentOwnershipChanged;

        IReadOnlyList<DimensionContentOwnershipBinding> GetContentOwnershipBindings(
            DimensionContentOwnershipQuery query);

        IReadOnlyList<DimensionContentOwnershipBinding> GetOrphanedContentOwnershipBindings();

        bool TryGetContentOwner(
            DimensionContentRecordKind recordKind,
            string recordId,
            out DimensionContentOwnershipBinding binding);

        bool TryBindContentOwnership(
            DimensionContentOwnershipBinding binding,
            bool requireExistingRecord,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveContentOwnership(
            DimensionContentRecordKind recordKind,
            string recordId,
            out DimensionContentOwnershipBinding removedBinding,
            out DimensionOperationResult result);
    }
}
