using System;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Honest maturity level of a framework capability. The vocabulary matches the Dimensions
    /// API handoff so the dashboard, docs, and consumers describe features the same way and
    /// never present an unfinished capability as production-ready.
    /// </summary>
    public enum DimensionCapabilityMaturity
    {
        /// <summary>The full path exists and has current build/runtime evidence.</summary>
        ImplementedAndEvidenced,

        /// <summary>
        /// Substantial code exists, but one or more clean-build, runtime, multiplayer,
        /// persistence, or performance proofs are still missing.
        /// </summary>
        ImplementedNotFullyProven,

        /// <summary>A deliberately narrow path works; broader API claims do not yet follow.</summary>
        PartialVerticalSlice,

        /// <summary>
        /// Consumers can register a provider/definition, but the framework does not itself
        /// supply the content behavior.
        /// </summary>
        ContractExtensionSeam,

        /// <summary>
        /// Assets, DTOs, dashboard models, previews, or contracts exist without a complete
        /// runtime realization.
        /// </summary>
        AuthoringModelOnly,

        /// <summary>Known regressions, unfinished work, or no reliable test gate.</summary>
        ExperimentalUnstable,

        /// <summary>No meaningful framework path exists beyond perhaps a name.</summary>
        NotImplemented
    }

    /// <summary>An immutable maturity record for one framework capability.</summary>
    public readonly struct DimensionCapability
    {
        public DimensionCapability(
            string id,
            string title,
            DimensionCapabilityMaturity maturity,
            string note)
        {
            Id = id;
            Title = title;
            Maturity = maturity;
            Note = note;
        }

        /// <summary>Stable identifier (kebab-case) for lookups and UI keys.</summary>
        public string Id { get; }

        /// <summary>Human-readable capability name.</summary>
        public string Title { get; }

        public DimensionCapabilityMaturity Maturity { get; }

        /// <summary>Short note on what is proven and what is still missing.</summary>
        public string Note { get; }

        /// <summary>
        /// True when the capability is proven enough to depend on for a limited alpha. Only
        /// evidenced capabilities qualify; everything else is preview/experimental.
        /// </summary>
        public bool IsAlphaReady =>
            Maturity == DimensionCapabilityMaturity.ImplementedAndEvidenced;
    }

    /// <summary>
    /// Single source of truth for how mature each Dimensions API capability actually is.
    /// The dashboard and documentation should read maturity from here rather than implying
    /// every exposed interface is finished. Values are intentionally conservative: a
    /// capability is only raised once real build/runtime/multiplayer/persistence evidence
    /// exists (see the handoff's definition of done).
    /// </summary>
    public static class DimensionCapabilityRegistry
    {
        private static readonly DimensionCapability[] Capabilities =
        {
            new DimensionCapability(
                "api-service-registration",
                "Shared API and service registration",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "API v1 and one service provider seam exist; lifecycle/duplicate-provider and compatibility tests are still needed."),
            new DimensionCapability(
                "dimension-identity-registry",
                "Dimension identity and registry",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Definitions, lookup, lifecycle, and diagnostics exist; not proven across load order, migration, world switching, and many mods."),
            new DimensionCapability(
                "coordinate-translation",
                "Coordinate translation",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Absolute/local conversion and bounds exist; complete UI presentation isolation is unproven."),
            new DimensionCapability(
                "slot-allocation",
                "Cross-mod slot allocation",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Deterministic candidates, conflict checks, and persisted slots exist; concurrency/load-order/migration are untested."),
            new DimensionCapability(
                "persistence",
                "Per-world persistence",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A/B records, checksums, backups, coalesced writes exist; corruption, migration, server, and world-switch coverage is missing."),
            new DimensionCapability(
                "loaded-area-orchestration",
                "Loaded-area orchestration",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "Real loaded-area components, tickets, merging, and quotas work; a scalable general scheduler is not proven."),
            new DimensionCapability(
                "generation",
                "Generation orchestration",
                DimensionCapabilityMaturity.ContractExtensionSeam,
                "Provider/pass planner plus a safe-platform writer exist; rich terrain/biome/liquid/scene/resource/spawn pipelines do not."),
            new DimensionCapability(
                "travel-networking",
                "Travel and networking",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "Server-side queued vanilla teleport path and state records exist; entry/return/reconnect/death/multiplayer are unproven."),
            new DimensionCapability(
                "portals",
                "Portals",
                DimensionCapabilityMaturity.ExperimentalUnstable,
                "Extensive runtime/editor implementation; recently hardened (swirl, positioning, parity trace) but not yet cleared in a built game."),
            new DimensionCapability(
                "portal-studio",
                "Portal Studio",
                DimensionCapabilityMaturity.ExperimentalUnstable,
                "Rich preview, profiles, layers, packages; profile-owned baking and a parity validator now exist but need in-game golden-capture proof."),
            new DimensionCapability(
                "manifest-ownership",
                "Content manifest and ownership",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Comprehensive definition/ownership/reference snapshot; transactional registration and schema migration tooling are missing."),
            new DimensionCapability(
                "dashboard-wizard",
                "Dashboard and authoring wizard",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "Large models and editor windows; several fields have no runtime executor and should be labeled preview/unsupported."),
            new DimensionCapability(
                "biomes-zones",
                "Biome and zone definitions",
                DimensionCapabilityMaturity.AuthoringModelOnly,
                "Assets, manifests, zones, profiles, and tables exist without actual Core Keeper terrain/ecology realization."),
            new DimensionCapability(
                "scenes-resources-spawns-events",
                "Scenes, resources, spawns, and events",
                DimensionCapabilityMaturity.AuthoringModelOnly,
                "Definitions and registry/service paths exist; runtime providers and deterministic placement/execution do not."),
            new DimensionCapability(
                "custom-items",
                "Custom items",
                DimensionCapabilityMaturity.AuthoringModelOnly,
                "Archetypes declare the required components and the validator blocks incomplete items, but no prefab/ObjectAuthoring/SpriteAsset/localization/recipe generation runs yet."),
            new DimensionCapability(
                "recipes-workbenches-loot",
                "Recipes, workbenches, and loot",
                DimensionCapabilityMaturity.AuthoringModelOnly,
                "Schemas and a narrow portal crafting registry exist; general compatible runtime authoring/injection does not."),
            new DimensionCapability(
                "custom-tilesets",
                "Custom tilesets",
                DimensionCapabilityMaturity.ContractExtensionSeam,
                "Tile roles and a priority-arbitrated provider registry exist so a third-party tileset mod can coexist, but no provider ships yet: authoring, import, and generation are still missing."),
            new DimensionCapability(
                "map-presentation",
                "Map and coordinate presentation",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Marker scoping and presented-local coordinates exist; complete vanilla-quality map/minimap isolation is unproven."),
            new DimensionCapability(
                "diagnostics-readiness",
                "Diagnostics and readiness",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Bounded diagnostics and readiness/preflight models exist; creator-readable, grouped, actionable errors are still needed."),
            new DimensionCapability(
                "automated-tests",
                "Automated tests",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "The editor portal suite compiles and passes; registry/persistence/loading/travel/multiplayer suites do not yet exist.")
        };

        /// <summary>All capability maturity records, in dependency-ish order.</summary>
        public static DimensionCapability[] All()
        {
            return (DimensionCapability[])Capabilities.Clone();
        }

        /// <summary>Looks up a capability by its stable <see cref="DimensionCapability.Id"/>.</summary>
        public static bool TryGet(string id, out DimensionCapability capability)
        {
            for (int i = 0; i < Capabilities.Length; i++)
            {
                if (string.Equals(Capabilities[i].Id, id, StringComparison.Ordinal))
                {
                    capability = Capabilities[i];
                    return true;
                }
            }

            capability = default;
            return false;
        }

        /// <summary>
        /// True only when the named capability is proven enough for a limited alpha. Unknown
        /// ids return false so callers never accidentally advertise an unlisted feature.
        /// </summary>
        public static bool IsAlphaReady(string id)
        {
            return TryGet(id, out DimensionCapability capability) && capability.IsAlphaReady;
        }

        /// <summary>Short human-readable label for a maturity level (for badges/logs).</summary>
        public static string Describe(DimensionCapabilityMaturity maturity)
        {
            switch (maturity)
            {
                case DimensionCapabilityMaturity.ImplementedAndEvidenced:
                    return "Stable";
                case DimensionCapabilityMaturity.ImplementedNotFullyProven:
                    return "Implemented (unproven)";
                case DimensionCapabilityMaturity.PartialVerticalSlice:
                    return "Partial slice";
                case DimensionCapabilityMaturity.ContractExtensionSeam:
                    return "Provider required";
                case DimensionCapabilityMaturity.AuthoringModelOnly:
                    return "Preview (authoring only)";
                case DimensionCapabilityMaturity.ExperimentalUnstable:
                    return "Experimental";
                case DimensionCapabilityMaturity.NotImplemented:
                    return "Not implemented";
                default:
                    return maturity.ToString();
            }
        }
    }
}
