namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableEntryDefinition
    {
        public readonly string EntryId;
        public readonly string TableId;
        public readonly string SubjectId;
        public readonly string SubjectKind;
        public readonly int Weight;
        public readonly int MinCount;
        public readonly int MaxCount;
        public readonly int Priority;
        public readonly bool Enabled;
        public readonly string Notes;

        public DimensionGenerationTableEntryDefinition(
            string entryId,
            string tableId,
            string subjectId,
            string subjectKind,
            int weight,
            int minCount,
            int maxCount,
            int priority,
            bool enabled,
            string notes)
        {
            EntryId = entryId ?? string.Empty;
            TableId = tableId ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            SubjectKind = subjectKind ?? string.Empty;
            Weight = weight;
            MinCount = minCount;
            MaxCount = maxCount;
            Priority = priority;
            Enabled = enabled;
            Notes = notes ?? string.Empty;
        }
    }
}
