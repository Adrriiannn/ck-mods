using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Which layout version a particular world should generate from, and why.
    /// </summary>
    public enum DimensionLayoutPinOutcome
    {
        /// <summary>A brand-new world. It gets the current layout and is stamped with its version.</summary>
        StampedNewWorld = 0,

        /// <summary>The world was made by the current layout and nothing has changed.</summary>
        Unchanged = 1,

        /// <summary>An older world, generating from its own archived layout as the policy asks.</summary>
        PinnedToArchivedVersion = 2,

        /// <summary>An older world adopting the current layout, because the policy asks for that.</summary>
        AdoptedLatest = 3,

        /// <summary>
        /// An older world whose original layout was never archived, so the current one is all there is.
        /// </summary>
        ArchiveMissing = 4
    }

    /// <summary>The decision, plus the sentence to show whoever needs to know about it.</summary>
    public sealed class DimensionLayoutPinDecision
    {
        public DimensionLayoutPinDecision(
            DimensionLayoutPinOutcome outcome,
            int versionToUse,
            string fingerprintToStamp,
            bool usesArchivedLayout,
            string message)
        {
            Outcome = outcome;
            VersionToUse = versionToUse;
            FingerprintToStamp = fingerprintToStamp ?? DimensionLayoutFingerprint.Empty;
            UsesArchivedLayout = usesArchivedLayout;
            Message = message ?? string.Empty;
        }

        public readonly DimensionLayoutPinOutcome Outcome;

        /// <summary>The layout version this world generates from.</summary>
        public readonly int VersionToUse;

        /// <summary>What to write into the world's record, so the next load reaches the same answer.</summary>
        public readonly string FingerprintToStamp;

        /// <summary>
        /// Whether to generate from the archived copy of <see cref="VersionToUse"/> rather than from
        /// the layout as it stands today.
        /// </summary>
        /// <remarks>
        /// The decision says which version applies; fetching that version's regions is left to the
        /// caller, because the editor holds them as authored archive entries and the running game holds
        /// them as registered zones. Returning one shape would force the other side to convert, and the
        /// conversion is exactly where the two could drift apart.
        /// </remarks>
        public readonly bool UsesArchivedLayout;

        /// <summary>Empty when there is nothing worth telling anyone.</summary>
        public readonly string Message;
    }

    /// <summary>
    /// Decides whether a world keeps the layout it was born with or takes the newest one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept apart from both the asset and the registry because it is pure reasoning over two numbers
    /// and a fingerprint, and that is exactly the part worth testing directly. The caller supplies what
    /// the world recorded; this returns what to do and what to say.
    /// </para>
    /// <para>
    /// See <see cref="DimensionLayoutFingerprint"/> for why a version number alone is not enough.
    /// </para>
    /// </remarks>
    public static class DimensionLayoutPinResolver
    {
        /// <summary>
        /// Resolves the layout for a world.
        /// </summary>
        /// <param name="layout">The layout as it stands in the mod being loaded.</param>
        /// <param name="storedVersion">The version the world recorded, or 0 if it recorded none.</param>
        /// <param name="storedFingerprint">The fingerprint the world recorded, if any.</param>
        public static DimensionLayoutPinDecision Resolve(
            DimensionLayoutTemplateAsset layout,
            int storedVersion,
            string storedFingerprint)
        {
            if (layout == null)
            {
                return new DimensionLayoutPinDecision(
                    DimensionLayoutPinOutcome.StampedNewWorld,
                    1,
                    DimensionLayoutFingerprint.Empty,
                    false,
                    string.Empty);
            }

            DimensionLayoutArchiveEntry stored = layout.FindPublishedVersion(storedVersion);
            return Resolve(
                layout.LayoutVersion,
                layout.CurrentFingerprint,
                layout.DriftPolicy,
                storedVersion,
                storedFingerprint,
                stored != null,
                stored == null ? null : stored.Fingerprint);
        }

        /// <summary>
        /// The same decision, expressed over plain values so the running game can reach it too.
        /// </summary>
        /// <remarks>
        /// The authoring asset does not exist at runtime — a built mod carries generated code and a
        /// registry, not ScriptableObjects. Splitting the reasoning out here is what lets the editor
        /// preview and the game agree by construction rather than by two implementations staying in
        /// step.
        /// </remarks>
        public static DimensionLayoutPinDecision Resolve(
            int currentVersion,
            string currentFingerprintValue,
            DimensionLayoutDriftPolicy policy,
            int storedVersion,
            string storedFingerprint,
            bool hasArchivedVersion,
            string archivedFingerprint)
        {
            string currentFingerprint = string.IsNullOrEmpty(currentFingerprintValue)
                ? DimensionLayoutFingerprint.Empty
                : currentFingerprintValue;

            // A world with no record is either brand new or predates pinning. Either way the only
            // sensible thing to hand it is the layout that exists now, and to write down what it got so
            // the question is answerable next time.
            if (storedVersion <= 0)
            {
                return new DimensionLayoutPinDecision(
                    DimensionLayoutPinOutcome.StampedNewWorld,
                    currentVersion,
                    currentFingerprint,
                    false,
                    string.Empty);
            }

            if (storedVersion == currentVersion &&
                (string.IsNullOrEmpty(storedFingerprint) ||
                 string.Equals(storedFingerprint, currentFingerprint, System.StringComparison.OrdinalIgnoreCase)))
            {
                return new DimensionLayoutPinDecision(
                    DimensionLayoutPinOutcome.Unchanged,
                    currentVersion,
                    currentFingerprint,
                    false,
                    string.Empty);
            }

            // Same version number, different shape. This is the dangerous case and the easiest to reach
            // by accident: the layout was edited without publishing, so two different worlds both claim
            // to be this version. Say so rather than quietly picking one.
            bool editedWithoutPublishing =
                storedVersion == currentVersion &&
                !string.IsNullOrEmpty(storedFingerprint);

            bool hasArchive = hasArchivedVersion;

            if (policy == DimensionLayoutDriftPolicy.AlwaysUseLatest)
            {
                return new DimensionLayoutPinDecision(
                    DimensionLayoutPinOutcome.AdoptedLatest,
                    currentVersion,
                    currentFingerprint,
                    false,
                    "This world was generated by layout version " + storedVersion +
                    (editedWithoutPublishing ? " before it was edited" : string.Empty) +
                    ", and the layout is set to always use the latest. Terrain the player has already " +
                    "visited keeps its old shape, so new areas may not line up with it.");
            }

            if (hasArchive && !editedWithoutPublishing)
            {
                return new DimensionLayoutPinDecision(
                    DimensionLayoutPinOutcome.PinnedToArchivedVersion,
                    storedVersion,
                    archivedFingerprint,
                    true,
                    string.Empty);
            }

            if (hasArchive)
            {
                // The version was published, but this world holds a different shape under the same
                // number — it was generated from an unpublished edit. The archived copy is the closest
                // honest answer available.
                return new DimensionLayoutPinDecision(
                    DimensionLayoutPinOutcome.PinnedToArchivedVersion,
                    storedVersion,
                    archivedFingerprint,
                    true,
                    "This world was generated from layout version " + storedVersion +
                    " while it had unpublished edits, so its exact shape was never recorded. " +
                    "Generating from the published version " + storedVersion + " instead, which may " +
                    "not match what the player already explored.");
            }

            return new DimensionLayoutPinDecision(
                DimensionLayoutPinOutcome.ArchiveMissing,
                currentVersion,
                currentFingerprint,
                false,
                "This world was generated by layout version " + storedVersion +
                ", which was never published, so there is no copy of it to generate from. Using the " +
                "current layout (version " + currentVersion + ") — areas the player has not visited " +
                "yet may not match the ones they have.");
        }
    }
}
