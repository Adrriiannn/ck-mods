namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationSeedRequest
    {
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly string ProviderId;
        public readonly string PassId;
        public readonly string Purpose;
        public readonly string Salt;
        public readonly uint ExternalSeed;

        public DimensionGenerationSeedRequest(
            string dimensionId,
            DimensionBounds localBounds,
            string providerId,
            string passId,
            string purpose,
            string salt,
            uint externalSeed)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            ProviderId = providerId ?? string.Empty;
            PassId = passId ?? string.Empty;
            Purpose = purpose ?? string.Empty;
            Salt = salt ?? string.Empty;
            ExternalSeed = externalSeed;
        }
    }
}
