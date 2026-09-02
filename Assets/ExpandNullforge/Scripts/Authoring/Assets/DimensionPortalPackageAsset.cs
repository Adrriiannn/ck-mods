using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Stable, consumer-owned root for one complete Portal Studio preset. The profile and
    /// every owned SpriteAsset live beside this asset under one package folder; the entries
    /// are a lightweight ownership inventory used by editor migration and manifest repair.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PortalPackage",
        menuName = "Expand Nullforge/Portal Package")]
    public sealed class DimensionPortalPackageAsset : ScriptableObject
    {
        [Serializable]
        public sealed class ArtworkEntry
        {
            [SerializeField] private string role = string.Empty;
            [SerializeField] private string assetGuid = string.Empty;
            [SerializeField] private string relativePath = string.Empty;
            [SerializeField] private long addressLow;
            [SerializeField] private long addressHigh;

            public string Role => role ?? string.Empty;
            public string AssetGuid => assetGuid ?? string.Empty;
            public string RelativePath => relativePath ?? string.Empty;
            public long AddressLow => addressLow;
            public long AddressHigh => addressHigh;

            public ArtworkEntry(
                string role,
                string assetGuid,
                string relativePath,
                long addressLow,
                long addressHigh)
            {
                this.role = role ?? string.Empty;
                this.assetGuid = assetGuid ?? string.Empty;
                this.relativePath = relativePath ?? string.Empty;
                this.addressLow = addressLow;
                this.addressHigh = addressHigh;
            }
        }

        [Serializable]
        public sealed class DependencyEntry
        {
            [SerializeField] private string role = string.Empty;
            [SerializeField] private string assetGuid = string.Empty;
            [SerializeField] private string relativePath = string.Empty;

            public string Role => role ?? string.Empty;
            public string AssetGuid => assetGuid ?? string.Empty;
            public string RelativePath => relativePath ?? string.Empty;

            public DependencyEntry(string role, string assetGuid, string relativePath)
            {
                this.role = role ?? string.Empty;
                this.assetGuid = assetGuid ?? string.Empty;
                this.relativePath = relativePath ?? string.Empty;
            }
        }

        public const int CurrentSchemaVersion = 2;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string packageId = string.Empty;
        [SerializeField] private string displayName = "Portal";
        [SerializeField] private string dimensionId = string.Empty;
        [SerializeField] private DimensionPortalVisualProfileAsset profile;
        [SerializeField] private List<ArtworkEntry> artwork = new List<ArtworkEntry>();
        [SerializeField] private List<DependencyEntry> dependencies =
            new List<DependencyEntry>();

        public int SchemaVersion => schemaVersion;
        public string PackageId => packageId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string DimensionId => dimensionId ?? string.Empty;
        public DimensionPortalVisualProfileAsset Profile => profile;
        public IReadOnlyList<ArtworkEntry> Artwork => artwork;
        public IReadOnlyList<DependencyEntry> Dependencies => dependencies;

        public void Configure(
            string stablePackageId,
            string packageDisplayName,
            string ownerDimensionId,
            DimensionPortalVisualProfileAsset portalProfile)
        {
            schemaVersion = CurrentSchemaVersion;
            packageId = stablePackageId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(packageDisplayName)
                ? "Portal"
                : packageDisplayName.Trim();
            dimensionId = ownerDimensionId ?? string.Empty;
            profile = portalProfile;
        }

        public void SetArtwork(IReadOnlyList<ArtworkEntry> entries)
        {
            schemaVersion = CurrentSchemaVersion;
            artwork.Clear();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ArtworkEntry entry = entries[i];
                if (entry != null)
                {
                    artwork.Add(entry);
                }
            }
        }

        public void SetDependencies(IReadOnlyList<DependencyEntry> entries)
        {
            schemaVersion = CurrentSchemaVersion;
            dependencies.Clear();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DependencyEntry entry = entries[i];
                if (entry != null)
                {
                    dependencies.Add(entry);
                }
            }
        }
    }
}
