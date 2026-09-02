using System;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A published layout, kept so a world made under it can still be generated after the layout moves
    /// on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes pinning mean something. Recording "this save used layout v2" is only a label
    /// unless v2 still exists somewhere — otherwise the honest behaviour when v3 ships is to shrug and
    /// generate v3, which is the drift the pin was supposed to prevent.
    /// </para>
    /// <para>
    /// What is archived is the COMPILED form — flat rectangles with a biome and a priority — not the
    /// authoring form. Rings, grids and painted masks are all just different ways of arriving at that
    /// list, so archiving the result rather than the recipe keeps an old world reproducible even if the
    /// author later switches the layout to an entirely different mode.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionLayoutArchiveEntry
    {
        [SerializeField] private int version = 1;
        [SerializeField] private string fingerprint = DimensionLayoutFingerprint.Empty;
        [SerializeField] private string publishedUtc = string.Empty;
        [SerializeField] private string notes = string.Empty;
        [SerializeField] private DimensionLayoutArchivedRegion[] regions = new DimensionLayoutArchivedRegion[0];

        public DimensionLayoutArchiveEntry()
        {
        }

        public DimensionLayoutArchiveEntry(
            int version,
            string fingerprint,
            string publishedUtc,
            string notes,
            DimensionLayoutArchivedRegion[] regions)
        {
            this.version = version < 1 ? 1 : version;
            this.fingerprint = string.IsNullOrEmpty(fingerprint) ? DimensionLayoutFingerprint.Empty : fingerprint;
            this.publishedUtc = publishedUtc ?? string.Empty;
            this.notes = notes ?? string.Empty;
            this.regions = regions ?? new DimensionLayoutArchivedRegion[0];
        }

        public int Version
        {
            get { return version < 1 ? 1 : version; }
        }

        public string Fingerprint
        {
            get { return string.IsNullOrEmpty(fingerprint) ? DimensionLayoutFingerprint.Empty : fingerprint; }
        }

        /// <summary>When this version was published, as a round-trip UTC string.</summary>
        /// <remarks>
        /// Stored as text rather than ticks so a human reading the asset file can tell which of two
        /// archived versions came first without decoding anything.
        /// </remarks>
        public string PublishedUtc
        {
            get { return publishedUtc ?? string.Empty; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public DimensionLayoutArchivedRegion[] Regions
        {
            get { return regions ?? new DimensionLayoutArchivedRegion[0]; }
        }
    }

    /// <summary>One rectangle of one biome, as generation actually consumes it.</summary>
    [Serializable]
    public sealed class DimensionLayoutArchivedRegion
    {
        [SerializeField] private string regionId = string.Empty;
        [SerializeField] private string biomeId = string.Empty;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Vector2Int localMin;
        [SerializeField] private Vector2Int localMaxExclusive;
        [SerializeField] private int priority;

        public DimensionLayoutArchivedRegion()
        {
        }

        public DimensionLayoutArchivedRegion(
            string regionId,
            string biomeId,
            string zoneId,
            string displayName,
            DimensionBounds localBounds,
            int priority)
        {
            this.regionId = regionId ?? string.Empty;
            this.biomeId = biomeId ?? string.Empty;
            this.zoneId = zoneId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.localMin = new Vector2Int(localBounds.Min.x, localBounds.Min.y);
            this.localMaxExclusive = new Vector2Int(localBounds.MaxExclusive.x, localBounds.MaxExclusive.y);
            this.priority = priority;
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

        public DimensionBounds LocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new Unity.Mathematics.int2(localMin.x, localMin.y),
                    new Unity.Mathematics.int2(localMaxExclusive.x, localMaxExclusive.y));
            }
        }
    }
}
