namespace ExpandNullforge.Api
{
    using System;
    using System.Collections.Generic;

    public interface IDimensionEncounterService
    {
        event Action<DimensionEncounterChangedEvent> EncounterChanged;

        IReadOnlyList<DimensionEncounterDefinition> GetEncounters(
            string dimensionId,
            string zoneId,
            DimensionEncounterKind kind,
            bool includeDisabled);

        bool TryGetEncounter(
            string encounterId,
            out DimensionEncounterDefinition encounter);

        bool TryFindEncounterForScene(
            string sceneId,
            out DimensionEncounterDefinition encounter);

        bool TryRegisterEncounter(
            DimensionEncounterDefinition encounter,
            out DimensionOperationResult result);

        bool TryUpdateEncounter(
            DimensionEncounterDefinition encounter,
            string reason,
            out DimensionOperationResult result);

        bool TrySetEncounterEnabled(
            string encounterId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveEncounter(
            string encounterId,
            out DimensionOperationResult result);
    }
}
