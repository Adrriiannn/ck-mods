using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Persistence;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Makes a save keep the world shape it was generated with, even after the mod ships a new layout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs once per world, after the generated bootstrap has registered this build's zones and layout
    /// versions. For each dimension it asks what layout that save was made with, and — when the answer
    /// is an older published version — puts that version's zones back in place of the current ones.
    /// </para>
    /// <para>
    /// WHY THIS MATTERS AT ALL. Core Keeper does not build a world up front. The ground under a player
    /// is created the first time someone stands there, which means a save carries terrain from
    /// whichever layout was installed when each part of it was first visited. Ship a new layout to a
    /// world that is half-explored and the unexplored half comes out shaped differently: biomes that
    /// stop mid-corridor, a desert that becomes a swamp along a straight line the player can walk.
    /// Nothing logs it, and no amount of reloading fixes it, because the old terrain is on disk.
    /// </para>
    /// <para>
    /// So the rule is: a world's layout is decided once, at first generation, and everything after that
    /// obeys the decision. Changing it is a choice the author makes deliberately, per layout, via
    /// <see cref="DimensionLayoutDriftPolicy"/>.
    /// </para>
    /// </remarks>
    public static class DimensionLayoutPinService
    {
        private static readonly HashSet<string> Applied = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>How many dimensions were pinned to an older layout on the last run.</summary>
        public static int LastPinnedCount { get; private set; }

        /// <summary>
        /// Resolves and applies the layout pin for every dimension that published one.
        /// </summary>
        /// <remarks>
        /// Safe to call repeatedly — each dimension is settled once per world. The registry is the
        /// authority on what "this world" means, so a player returning to the menu and loading a second
        /// save re-resolves rather than carrying the first save's answer across.
        /// </remarks>
        public static void ApplyForCurrentWorld(IDimensionService service)
        {
            if (service == null || !DimensionLayoutVersionRegistry.HasAny)
            {
                return;
            }

            LastPinnedCount = 0;
            IReadOnlyList<string> dimensionIds = DimensionLayoutVersionRegistry.DimensionIds;
            for (int i = 0; i < dimensionIds.Count; i++)
            {
                ApplyForDimension(service, dimensionIds[i]);
            }
        }

        private static void ApplyForDimension(IDimensionService service, string dimensionId)
        {
            if (Applied.Contains(dimensionId))
            {
                return;
            }

            int currentVersion = DimensionLayoutVersionRegistry.GetCurrentVersion(dimensionId);
            if (currentVersion <= 0)
            {
                return;
            }

            int storedVersion;
            string storedFingerprint;
            if (!DimensionWorldRegistry.TryGetLayoutPin(dimensionId, out storedVersion, out storedFingerprint))
            {
                storedVersion = 0;
                storedFingerprint = string.Empty;
            }

            DimensionLayoutVersionRecord archived = storedVersion > 0
                ? DimensionLayoutVersionRegistry.FindVersion(dimensionId, storedVersion)
                : null;

            DimensionLayoutPinDecision decision = DimensionLayoutPinResolver.Resolve(
                currentVersion,
                DimensionLayoutVersionRegistry.GetCurrentFingerprint(dimensionId),
                DimensionLayoutDriftPolicyRegistry.GetPolicy(dimensionId),
                storedVersion,
                storedFingerprint,
                archived != null,
                archived == null ? null : archived.Fingerprint);

            if (!string.IsNullOrEmpty(decision.Message))
            {
                DimensionFrameworkLog.Warning(dimensionId + ": " + decision.Message);
            }

            if (decision.UsesArchivedLayout && archived != null)
            {
                ApplyArchivedZones(service, dimensionId, archived);
                LastPinnedCount++;
            }

            // Stamped only when the world had no pin. TryStampLayoutPin refuses to overwrite one, so
            // this is belt and braces — but stating the intent here keeps the two halves honest.
            if (decision.Outcome == DimensionLayoutPinOutcome.StampedNewWorld)
            {
                DimensionWorldRegistry.TryStampLayoutPin(
                    dimensionId,
                    decision.VersionToUse,
                    decision.FingerprintToStamp);
            }

            Applied.Add(dimensionId);
        }

        /// <summary>
        /// Replaces the zones the bootstrap registered with the ones the pinned version published.
        /// </summary>
        /// <remarks>
        /// Updates in place rather than clearing and re-adding, because a zone id that exists in both
        /// versions is the same place in the world — dropping and recreating it would fire a removal
        /// every load for anything watching zones. A zone the old version never had is simply
        /// registered; one the old version had and the new one dropped is registered back.
        /// </remarks>
        private static void ApplyArchivedZones(
            IDimensionService service,
            string dimensionId,
            DimensionLayoutVersionRecord archived)
        {
            int applied = 0;
            for (int i = 0; i < archived.Zones.Count; i++)
            {
                DimensionZoneDefinition zone = archived.Zones[i];
                DimensionOperationResult result;

                DimensionZoneDefinition existing;
                bool ok = service.TryGetZoneDefinition(zone.ZoneId, out existing)
                    ? service.TryUpdateZoneDefinition(zone, "layout-pin", out result)
                    : service.TryRegisterZoneDefinition(zone, out result);

                if (ok)
                {
                    applied++;
                    continue;
                }

                DimensionFrameworkLog.Warning(
                    "Could not restore zone '" + zone.ZoneId + "' from layout v" +
                    archived.Version + ": " + result.Message);
            }

            DimensionFrameworkLog.Info(
                dimensionId + " keeps the layout it was generated with (v" +
                archived.Version + ", " + applied + " zones).");
        }

        /// <summary>Forgets which dimensions were settled. The runtime does not need it; tests do.</summary>
        public static void ResetForNewWorld()
        {
            Applied.Clear();
            LastPinnedCount = 0;
        }
    }

    /// <summary>
    /// What each dimension wants done when its layout has moved on since a save was made.
    /// </summary>
    /// <remarks>
    /// A tiny registry of its own rather than a field on the version records, because the policy is a
    /// property of the layout as it is TODAY — the author's current intent — not of any one published
    /// version. Storing it per version would mean an old world was governed by a decision the author
    /// made long ago and has since changed their mind about.
    /// </remarks>
    public static class DimensionLayoutDriftPolicyRegistry
    {
        private static readonly Dictionary<string, DimensionLayoutDriftPolicy> Policies =
            new Dictionary<string, DimensionLayoutDriftPolicy>(System.StringComparer.Ordinal);

        public static void Register(string dimensionId, DimensionLayoutDriftPolicy policy)
        {
            if (string.IsNullOrEmpty(dimensionId))
            {
                return;
            }

            Policies[dimensionId] = policy;
        }

        /// <summary>
        /// The dimension's policy, defaulting to keeping existing worlds intact.
        /// </summary>
        /// <remarks>
        /// The default is the conservative one on purpose: a missing policy must never be the reason a
        /// player's world quietly changes shape.
        /// </remarks>
        public static DimensionLayoutDriftPolicy GetPolicy(string dimensionId)
        {
            DimensionLayoutDriftPolicy policy;
            return Policies.TryGetValue(dimensionId ?? string.Empty, out policy)
                ? policy
                : DimensionLayoutDriftPolicy.KeepExistingWorlds;
        }

        public static void Clear()
        {
            Policies.Clear();
        }
    }
}
