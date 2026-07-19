using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Environment Profile Template")]
    public sealed class EnvironmentProfileTemplateAsset : ScriptableObject
    {
        [SerializeField] private string profileId = "environment";
        [SerializeField] private string displayName = "Environment";
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string musicCueId = string.Empty;
        [SerializeField] private string ambientCueId = string.Empty;
        [SerializeField] private string lightingProfileId = string.Empty;
        [SerializeField] private string fogProfileId = string.Empty;
        [SerializeField] private bool hasMapColor;
        [SerializeField] private Color mapColor = new Color(0.25f, 0.45f, 0.55f, 1f);
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;

        public string ProfileId
        {
            get { return profileId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string ZoneId
        {
            get { return zoneId ?? string.Empty; }
        }

        public string MusicCueId
        {
            get { return musicCueId ?? string.Empty; }
        }

        public string AmbientCueId
        {
            get { return ambientCueId ?? string.Empty; }
        }

        public string LightingProfileId
        {
            get { return lightingProfileId ?? string.Empty; }
        }

        public string FogProfileId
        {
            get { return fogProfileId ?? string.Empty; }
        }

        public bool HasMapColor
        {
            get { return hasMapColor; }
        }

        public uint MapColorRgba
        {
            get { return ToRgba(mapColor); }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public void ConfigureIdentity(
            string newProfileId,
            string newDisplayName,
            string newZoneId,
            int newPriority,
            bool newEnabled)
        {
            profileId = newProfileId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            zoneId = newZoneId ?? string.Empty;
            priority = newPriority;
            enabled = newEnabled;
        }

        public void ConfigureAudio(
            string newMusicCueId,
            string newAmbientCueId)
        {
            musicCueId = newMusicCueId ?? string.Empty;
            ambientCueId = newAmbientCueId ?? string.Empty;
        }

        public void ConfigureVisuals(
            string newLightingProfileId,
            string newFogProfileId)
        {
            lightingProfileId = newLightingProfileId ?? string.Empty;
            fogProfileId = newFogProfileId ?? string.Empty;
        }

        public void ConfigureMapColor(Color newMapColor)
        {
            mapColor = newMapColor;
            hasMapColor = true;
        }

        public void ClearMapColor()
        {
            hasMapColor = false;
            mapColor = new Color(0.25f, 0.45f, 0.55f, 1f);
        }

        public DimensionEnvironmentProfile ToEnvironmentProfile(string dimensionId)
        {
            return new DimensionEnvironmentProfile(
                ProfileId,
                string.IsNullOrEmpty(DisplayName) ? ProfileId : DisplayName,
                dimensionId,
                ZoneId,
                MusicCueId,
                AmbientCueId,
                LightingProfileId,
                FogProfileId,
                HasMapColor,
                MapColorRgba,
                Priority,
                Enabled);
        }

        private static uint ToRgba(Color color)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            uint a = (uint)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }
    }
}
