using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [Serializable]
    public sealed class DimensionLayoutMaskBiomeDefinition
    {
        [SerializeField] private string mappingId = "mask-biome";
        [SerializeField] private string biomeId = string.Empty;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Color maskColor = Color.white;
        [SerializeField] private int colorTolerance;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;

        public string MappingId
        {
            get { return mappingId ?? string.Empty; }
        }

        public string BiomeId
        {
            get { return biomeId ?? string.Empty; }
        }

        public string ZoneId
        {
            get { return zoneId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public Color MaskColor
        {
            get { return maskColor; }
        }

        public int ColorTolerance
        {
            get { return colorTolerance < 0 ? 0 : colorTolerance; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }
    }
}
