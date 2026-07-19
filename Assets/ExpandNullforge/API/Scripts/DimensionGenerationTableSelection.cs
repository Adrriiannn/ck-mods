namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableSelection
    {
        public readonly DimensionGenerationTableDefinition Table;
        public readonly DimensionGenerationTableEntryDefinition Entry;
        public readonly int SelectedCount;
        public readonly int Roll;
        public readonly int TotalWeight;
        public readonly int PickIndex;

        public DimensionGenerationTableSelection(
            DimensionGenerationTableDefinition table,
            DimensionGenerationTableEntryDefinition entry,
            int selectedCount,
            int roll,
            int totalWeight,
            int pickIndex)
        {
            Table = table;
            Entry = entry;
            SelectedCount = selectedCount;
            Roll = roll;
            TotalWeight = totalWeight;
            PickIndex = pickIndex;
        }
    }
}
