using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionWorldEventService
    {
        event Action<DimensionWorldEventChangedEvent> WorldEventChanged;

        IReadOnlyList<DimensionWorldEventDefinition> GetWorldEvents(
            DimensionWorldEventQuery query);

        bool TryGetWorldEvent(
            string eventId,
            out DimensionWorldEventDefinition worldEvent);

        bool TryRegisterWorldEvent(
            DimensionWorldEventDefinition worldEvent,
            out DimensionOperationResult result);

        bool TryUpdateWorldEvent(
            DimensionWorldEventDefinition worldEvent,
            string reason,
            out DimensionOperationResult result);

        bool TrySetWorldEventEnabled(
            string eventId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveWorldEvent(
            string eventId,
            out DimensionOperationResult result);
    }
}
