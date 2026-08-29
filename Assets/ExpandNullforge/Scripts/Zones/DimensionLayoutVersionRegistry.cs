using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// One published layout version, as the running game sees it: a version number, the shape code that
    /// goes with it, and the zones it produces.
    /// </summary>
    public sealed class DimensionLayoutVersionRecord
    {
        public DimensionLayoutVersionRecord(
            int version,
            string fingerprint,
            IReadOnlyList<DimensionZoneDefinition> zones)
        {
            Version = version;
            Fingerprint = fingerprint ?? string.Empty;
            Zones = zones ?? new List<DimensionZoneDefinition>();
        }

        public readonly int Version;
        public readonly string Fingerprint;
        public readonly IReadOnlyList<DimensionZoneDefinition> Zones;
    }

    /// <summary>
    /// Every layout version a loaded mod published, so a save can be generated from the one that made
    /// it rather than from whichever version happens to be installed today.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Filled by generated bootstrap code at load, one entry per version the author published in the
    /// Layout Studio. It exists because Core Keeper builds terrain lazily: a save that was made under
    /// an older layout keeps the terrain it already has, so the only way for the rest of that world to
    /// match is to keep generating from the older layout.
    /// </para>
    /// <para>
    /// Deliberately a plain registry with no engine dependency — deciding which version applies is
    /// <see cref="Authoring.DimensionLayoutPinResolver"/>'s job, and applying it is the caller's.
    /// </para>
    /// </remarks>
    public static class DimensionLayoutVersionRegistry
    {
        private sealed class DimensionEntry
        {
            public int CurrentVersion;
            public string CurrentFingerprint = string.Empty;
            public readonly List<DimensionLayoutVersionRecord> Versions =
                new List<DimensionLayoutVersionRecord>();
        }

        private static readonly Dictionary<string, DimensionEntry> Entries =
            new Dictionary<string, DimensionEntry>(System.StringComparer.Ordinal);

        /// <summary>Whether any mod published a layout version at all.</summary>
        public static bool HasAny
        {
            get { return Entries.Count > 0; }
        }

        /// <summary>
        /// Declares which layout version a dimension currently ships.
        /// </summary>
        public static void RegisterCurrent(string dimensionId, int version, string fingerprint)
        {
            if (string.IsNullOrEmpty(dimensionId) || version <= 0)
            {
                return;
            }

            DimensionEntry entry = GetOrCreate(dimensionId);
            entry.CurrentVersion = version;
            entry.CurrentFingerprint = fingerprint ?? string.Empty;
        }

        /// <summary>
        /// Adds one published version's zones.
        /// </summary>
        /// <remarks>
        /// Registering the same version twice replaces the earlier copy rather than adding a second.
        /// Two mods claiming the same dimension and version is a conflict the framework cannot
        /// arbitrate, and keeping both would mean the winner depended on load order.
        /// </remarks>
        public static void RegisterVersion(
            string dimensionId,
            int version,
            string fingerprint,
            IReadOnlyList<DimensionZoneDefinition> zones)
        {
            if (string.IsNullOrEmpty(dimensionId) || version <= 0)
            {
                return;
            }

            DimensionEntry entry = GetOrCreate(dimensionId);
            DimensionLayoutVersionRecord record =
                new DimensionLayoutVersionRecord(version, fingerprint, zones);

            for (int i = 0; i < entry.Versions.Count; i++)
            {
                if (entry.Versions[i].Version == version)
                {
                    entry.Versions[i] = record;
                    return;
                }
            }

            entry.Versions.Add(record);
        }

        /// <summary>The version this dimension currently ships, or 0 if it published none.</summary>
        public static int GetCurrentVersion(string dimensionId)
        {
            DimensionEntry entry;
            return Entries.TryGetValue(dimensionId ?? string.Empty, out entry) ? entry.CurrentVersion : 0;
        }

        public static string GetCurrentFingerprint(string dimensionId)
        {
            DimensionEntry entry;
            return Entries.TryGetValue(dimensionId ?? string.Empty, out entry)
                ? entry.CurrentFingerprint
                : string.Empty;
        }

        /// <summary>The archived copy of a version, or null when that version was never published.</summary>
        public static DimensionLayoutVersionRecord FindVersion(string dimensionId, int version)
        {
            DimensionEntry entry;
            if (!Entries.TryGetValue(dimensionId ?? string.Empty, out entry))
            {
                return null;
            }

            for (int i = 0; i < entry.Versions.Count; i++)
            {
                if (entry.Versions[i].Version == version)
                {
                    return entry.Versions[i];
                }
            }

            return null;
        }

        /// <summary>Every dimension that published at least one layout version.</summary>
        public static IReadOnlyList<string> DimensionIds
        {
            get
            {
                List<string> ids = new List<string>(Entries.Count);
                foreach (KeyValuePair<string, DimensionEntry> pair in Entries)
                {
                    ids.Add(pair.Key);
                }

                return ids;
            }
        }

        /// <summary>Drops everything. The runtime never needs this; tests do.</summary>
        public static void Clear()
        {
            Entries.Clear();
        }

        private static DimensionEntry GetOrCreate(string dimensionId)
        {
            DimensionEntry entry;
            if (!Entries.TryGetValue(dimensionId, out entry))
            {
                entry = new DimensionEntry();
                Entries.Add(dimensionId, entry);
            }

            return entry;
        }
    }
}
