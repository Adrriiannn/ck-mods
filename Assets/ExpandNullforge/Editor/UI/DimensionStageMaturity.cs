using ExpandNullforge.Api;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which capability record each section of the framework answers to, and how to say it in one
    /// chip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE MAP IS HERE AND NOT ON EITHER PAGE. The capability registry is the framework's
    /// declared product truth — the one place that says how far a feature actually got — and it was
    /// being read by exactly one drawer, inside the amputated panel body nobody can reach. A
    /// creator therefore saw an authoring surface with no indication of whether it was proven,
    /// preview-only, or experimental, which is the opposite of what the registry was written for.
    /// Putting the map in its own file lets the live shell and the legacy drawer read one answer
    /// instead of drifting into two.
    /// </para>
    /// <para>
    /// A section with no capability of its own returns empty rather than being quietly pointed at
    /// somebody else's record. A badge that names the wrong feature's maturity is worse than no
    /// badge, because it is believed.
    /// </para>
    /// </remarks>
    internal static class DimensionStageMaturity
    {
        /// <summary>
        /// The capability whose maturity a section's controls actually depend on, or empty.
        /// </summary>
        internal static string CapabilityForSection(string sectionId)
        {
            switch (sectionId)
            {
                case "dimension":
                    return "dimension-identity-registry";
                case "portals":
                    return "portal-studio";
                case "tilesets":
                    return "custom-tilesets";
                case "layout":
                    return "coordinate-translation";
                case "biomes":
                    return "biomes-zones";
                case "resources":
                    return "custom-items";
                case "nature":
                    return "plants-and-food";
                case "spawns":
                    return "custom-creatures";
                case "scenes":
                    return "dungeons";
                case "worldrules":
                case "terrain":
                case "generation":
                    return "generation";
                case "export":
                    return "manifest-ownership";
                case "diagnostics":
                    return "diagnostics-readiness";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The capability record behind a section, when it has one.</summary>
        internal static bool TryResolve(string sectionId, out DimensionCapability capability)
        {
            string id = CapabilityForSection(sectionId);
            if (string.IsNullOrEmpty(id))
            {
                capability = default(DimensionCapability);
                return false;
            }

            return DimensionCapabilityRegistry.TryGet(id, out capability);
        }

        /// <summary>The short label a chip carries, in the registry's own vocabulary.</summary>
        internal static string Label(DimensionCapabilityMaturity maturity)
        {
            return DimensionCapabilityRegistry.Describe(maturity);
        }

        /// <summary>
        /// Which of the shell's three chip colours a maturity level takes.
        /// </summary>
        /// <remarks>
        /// Three colours rather than seven: the shell already means one thing by each of them, and a
        /// creator reading a page needs to know whether to trust it, be careful with it, or expect
        /// it not to work — not to rank seven shades against each other.
        /// </remarks>
        internal static string ChipClass(DimensionCapabilityMaturity maturity)
        {
            switch (maturity)
            {
                case DimensionCapabilityMaturity.ImplementedAndEvidenced:
                    return "dim-chip-ready";
                case DimensionCapabilityMaturity.ExperimentalUnstable:
                case DimensionCapabilityMaturity.NotImplemented:
                    return "dim-chip-blocked";
                default:
                    return "dim-chip-warn";
            }
        }
    }
}
