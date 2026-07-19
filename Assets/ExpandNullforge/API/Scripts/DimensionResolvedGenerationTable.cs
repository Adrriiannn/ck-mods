using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionResolvedGenerationTable
    {
        public readonly DimensionGenerationTableDefinition Table;
        public readonly IReadOnlyList<DimensionGenerationTableEntryDefinition> Entries;
        public readonly int TotalWeight;

        public DimensionResolvedGenerationTable(
            DimensionGenerationTableDefinition table,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> entries,
            int totalWeight)
        {
            Table = table;
            Entries = entries;
            TotalWeight = totalWeight;
        }
    }
}
