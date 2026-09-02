using System;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [Serializable]
    public sealed class DimensionLayoutRegionDefinition
    {
        [SerializeField] private string regionId = "region";
        [SerializeField] private string biomeId = string.Empty;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Vector2Int localMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int localMaxExclusive = new Vector2Int(64, 64);
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;

        public DimensionLayoutRegionDefinition()
        {
        }

        public DimensionLayoutRegionDefinition(
            string regionId,
            string biomeId,
            string zoneId,
            string displayName,
            Vector2Int localMin,
            Vector2Int localMaxExclusive,
            int priority,
            bool enabled)
        {
            this.regionId = regionId ?? string.Empty;
            this.biomeId = biomeId ?? string.Empty;
            this.zoneId = zoneId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.localMin = localMin;
            this.localMaxExclusive = localMaxExclusive;
            this.priority = priority;
            this.enabled = enabled;
        }

        public string RegionId
        {
            get { return regionId ?? string.Empty; }
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

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public DimensionBounds LocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(localMin.x, localMin.y),
                    new int2(localMaxExclusive.x, localMaxExclusive.y));
            }
        }
    }
}
