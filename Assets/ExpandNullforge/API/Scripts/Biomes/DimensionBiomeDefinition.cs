namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeDefinition
    {
        public readonly string BiomeId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string EnvironmentProfileId;
        public readonly string PaletteAssetId;
        public readonly string SpawnTableId;
        public readonly string ResourceTableId;
        public readonly string WorldEventTableId;
        public readonly uint MapColorRgba;
        public readonly int Priority;
        public readonly bool Enabled;
        public readonly string Notes;

        public DimensionBiomeDefinition(
            string biomeId,
            string displayName,
            string dimensionId,
            string environmentProfileId,
            string paletteAssetId,
            string spawnTableId,
            string resourceTableId,
            string worldEventTableId,
            uint mapColorRgba,
            int priority,
            bool enabled,
            string notes)
        {
            BiomeId = biomeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            EnvironmentProfileId = environmentProfileId ?? string.Empty;
            PaletteAssetId = paletteAssetId ?? string.Empty;
            SpawnTableId = spawnTableId ?? string.Empty;
            ResourceTableId = resourceTableId ?? string.Empty;
            WorldEventTableId = worldEventTableId ?? string.Empty;
            MapColorRgba = mapColorRgba;
            Priority = priority;
            Enabled = enabled;
            Notes = notes ?? string.Empty;
        }
    }
}
