namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringContentSummaryEntry
    {
        public readonly DimensionAuthoringContentSummaryKind Kind;
        public readonly string RecordId;
        public readonly string DisplayName;
        public readonly string BiomeId;
        public readonly string ZoneId;
        public readonly int Count;
        public readonly string Notes;

        public DimensionAuthoringContentSummaryEntry(
            DimensionAuthoringContentSummaryKind kind,
            string recordId,
            string displayName,
            string biomeId,
            string zoneId,
            int count,
            string notes)
        {
            Kind = kind;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            Count = count;
            Notes = notes ?? string.Empty;
        }
    }
}
