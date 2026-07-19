using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionGenerationTableService
    {
        event Action<DimensionGenerationTableChangedEvent> GenerationTableChanged;
        event Action<DimensionGenerationTableEntryChangedEvent> GenerationTableEntryChanged;

        IReadOnlyList<DimensionGenerationTableDefinition> GetGenerationTables(
            DimensionGenerationTableQuery query);

        DimensionGenerationTableResolutionResult ResolveGenerationTables(
            DimensionGenerationTableResolutionRequest request);

        DimensionGenerationTableSelectionResult SelectGenerationTableEntries(
            DimensionGenerationTableSelectionRequest request);

        bool TryGetGenerationTable(
            string tableId,
            out DimensionGenerationTableDefinition table);

        bool TryRegisterGenerationTable(
            DimensionGenerationTableDefinition table,
            out DimensionOperationResult result);

        bool TryUpdateGenerationTable(
            DimensionGenerationTableDefinition table,
            string reason,
            out DimensionOperationResult result);

        bool TrySetGenerationTableEnabled(
            string tableId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveGenerationTable(
            string tableId,
            out DimensionOperationResult result);

        IReadOnlyList<DimensionGenerationTableEntryDefinition> GetGenerationTableEntries(
            string tableId,
            bool enabledOnly);

        bool TryGetGenerationTableEntry(
            string entryId,
            out DimensionGenerationTableEntryDefinition entry);

        bool TryRegisterGenerationTableEntry(
            DimensionGenerationTableEntryDefinition entry,
            out DimensionOperationResult result);

        bool TryUpdateGenerationTableEntry(
            DimensionGenerationTableEntryDefinition entry,
            string reason,
            out DimensionOperationResult result);

        bool TrySetGenerationTableEntryEnabled(
            string entryId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveGenerationTableEntry(
            string entryId,
            out DimensionOperationResult result);
    }
}
