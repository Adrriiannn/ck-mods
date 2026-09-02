namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentPackRecordCount
    {
        public readonly DimensionContentRecordKind RecordKind;
        public readonly int Count;
        public readonly int OrphanedCount;

        public DimensionContentPackRecordCount(
            DimensionContentRecordKind recordKind,
            int count,
            int orphanedCount)
        {
            RecordKind = recordKind;
            Count = count;
            OrphanedCount = orphanedCount;
        }
    }
}
