namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeQuery
    {
        public readonly string DimensionId;
        public readonly string EnvironmentProfileId;
        public readonly string PaletteAssetId;
        public readonly bool EnabledOnly;

        public DimensionBiomeQuery(
            string dimensionId,
            string environmentProfileId,
            string paletteAssetId,
            bool enabledOnly)
        {
            DimensionId = dimensionId ?? string.Empty;
            EnvironmentProfileId = environmentProfileId ?? string.Empty;
            PaletteAssetId = paletteAssetId ?? string.Empty;
            EnabledOnly = enabledOnly;
        }
    }
}
