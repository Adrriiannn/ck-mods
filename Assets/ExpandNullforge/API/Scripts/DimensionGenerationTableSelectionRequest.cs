namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableSelectionRequest
    {
        public readonly DimensionGenerationTableResolutionRequest ResolutionRequest;
        public readonly uint Seed;
        public readonly string Salt;
        public readonly int PicksPerTable;
        public readonly bool AllowDuplicateEntries;

        public DimensionGenerationTableSelectionRequest(
            DimensionGenerationTableResolutionRequest resolutionRequest,
            uint seed,
            string salt,
            int picksPerTable,
            bool allowDuplicateEntries)
        {
            ResolutionRequest = resolutionRequest;
            Seed = seed;
            Salt = salt ?? string.Empty;
            PicksPerTable = picksPerTable;
            AllowDuplicateEntries = allowDuplicateEntries;
        }
    }
}
