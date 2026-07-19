namespace ExpandNullforge.Api
{
    public readonly struct DimensionEnvironmentProfile
    {
        public readonly string ProfileId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly string MusicCueId;
        public readonly string AmbientCueId;
        public readonly string LightingProfileId;
        public readonly string FogProfileId;
        public readonly bool HasMapColor;
        public readonly uint MapColorRgba;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionEnvironmentProfile(
            string profileId,
            string displayName,
            string dimensionId,
            string zoneId,
            string musicCueId,
            string ambientCueId,
            string lightingProfileId,
            string fogProfileId,
            bool hasMapColor,
            uint mapColorRgba,
            int priority,
            bool enabled)
        {
            ProfileId = profileId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            MusicCueId = musicCueId ?? string.Empty;
            AmbientCueId = ambientCueId ?? string.Empty;
            LightingProfileId = lightingProfileId ?? string.Empty;
            FogProfileId = fogProfileId ?? string.Empty;
            HasMapColor = hasMapColor;
            MapColorRgba = mapColorRgba;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
