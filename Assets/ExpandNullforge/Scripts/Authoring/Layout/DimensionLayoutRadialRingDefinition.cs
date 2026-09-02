using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [Serializable]
    public sealed class DimensionLayoutRadialRingDefinition
    {
        [SerializeField] private string ringId = "radial-ring";
        [SerializeField] private string biomeId = string.Empty;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private int minRadiusTiles;
        [SerializeField] private int maxRadiusTiles = 128;
        [SerializeField] private bool limitToAngleRange;
        [SerializeField] private float startAngleDegrees;
        [SerializeField] private float endAngleDegrees = 360f;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;

        public DimensionLayoutRadialRingDefinition()
        {
        }

        public DimensionLayoutRadialRingDefinition(
            string ringId,
            string biomeId,
            string zoneId,
            string displayName,
            int minRadiusTiles,
            int maxRadiusTiles,
            int priority,
            bool enabled)
        {
            this.ringId = ringId ?? string.Empty;
            this.biomeId = biomeId ?? string.Empty;
            this.zoneId = zoneId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.minRadiusTiles = minRadiusTiles;
            this.maxRadiusTiles = maxRadiusTiles;
            this.limitToAngleRange = false;
            this.startAngleDegrees = 0f;
            this.endAngleDegrees = 360f;
            this.priority = priority;
            this.enabled = enabled;
        }

        public DimensionLayoutRadialRingDefinition(
            string ringId,
            string biomeId,
            string zoneId,
            string displayName,
            int minRadiusTiles,
            int maxRadiusTiles,
            bool limitToAngleRange,
            float startAngleDegrees,
            float endAngleDegrees,
            int priority,
            bool enabled)
        {
            this.ringId = ringId ?? string.Empty;
            this.biomeId = biomeId ?? string.Empty;
            this.zoneId = zoneId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.minRadiusTiles = minRadiusTiles;
            this.maxRadiusTiles = maxRadiusTiles;
            this.limitToAngleRange = limitToAngleRange;
            this.startAngleDegrees = startAngleDegrees;
            this.endAngleDegrees = endAngleDegrees;
            this.priority = priority;
            this.enabled = enabled;
        }

        public string RingId
        {
            get { return ringId ?? string.Empty; }
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

        public int MinRadiusTiles
        {
            get { return minRadiusTiles < 0 ? 0 : minRadiusTiles; }
        }

        public int MaxRadiusTiles
        {
            get { return maxRadiusTiles < 0 ? 0 : maxRadiusTiles; }
        }

        public bool HasAngleRange
        {
            get
            {
                return limitToAngleRange &&
                    Mathf.Abs(Mathf.DeltaAngle(StartAngleDegrees, EndAngleDegrees)) > 0.01f;
            }
        }

        public float StartAngleDegrees
        {
            get { return NormalizeAngle(startAngleDegrees); }
        }

        public float EndAngleDegrees
        {
            get { return NormalizeAngle(endAngleDegrees); }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        private static float NormalizeAngle(float value)
        {
            float normalized = value % 360f;
            if (normalized < 0f)
            {
                normalized += 360f;
            }

            return normalized;
        }
    }
}
