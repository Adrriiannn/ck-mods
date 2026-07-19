namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringReadinessEntry
    {
        public readonly DimensionAuthoringReadinessCategory Category;
        public readonly DimensionAuthoringReadinessState State;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string Code;
        public readonly string Message;
        public readonly int PresentCount;
        public readonly int ExpectedCount;

        public DimensionAuthoringReadinessEntry(
            DimensionAuthoringReadinessCategory category,
            DimensionAuthoringReadinessState state,
            string dimensionId,
            string biomeId,
            string code,
            string message,
            int presentCount,
            int expectedCount)
        {
            Category = category;
            State = state;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            PresentCount = presentCount < 0 ? 0 : presentCount;
            ExpectedCount = expectedCount < 0 ? 0 : expectedCount;
        }
    }
}
