using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionVisualAssetValidationSourceKind
    {
        Unknown = 0,
        VanillaReference = 1,
        ModAsset = 2,
        PackageAsset = 3
    }

    public enum DimensionVisualAssetValidationState
    {
        Ready = 0,
        Advisory = 1,
        Blocked = 2
    }

    public sealed class DimensionVisualAssetValidationEntry
    {
        public DimensionVisualAssetValidationEntry(
            DimensionAssetReferenceDefinition reference,
            DimensionVisualAssetValidationSourceKind sourceKind,
            DimensionVisualAssetValidationState state,
            bool visualRuntimeAsset,
            bool requiresUgcSpriteObjectLitReview,
            bool requiresRuntimeSwapReview,
            bool requiresLightingReview,
            string guidance,
            IReadOnlyList<DimensionAuthoringIssue> issues)
        {
            Reference = reference;
            SourceKind = sourceKind;
            State = state;
            VisualRuntimeAsset = visualRuntimeAsset;
            RequiresUgcSpriteObjectLitReview = requiresUgcSpriteObjectLitReview;
            RequiresRuntimeSwapReview = requiresRuntimeSwapReview;
            RequiresLightingReview = requiresLightingReview;
            Guidance = guidance ?? string.Empty;
            Issues = issues ?? new List<DimensionAuthoringIssue>();
        }

        public DimensionAssetReferenceDefinition Reference { get; private set; }

        public DimensionVisualAssetValidationSourceKind SourceKind { get; private set; }

        public DimensionVisualAssetValidationState State { get; private set; }

        public bool VisualRuntimeAsset { get; private set; }

        public bool RequiresUgcSpriteObjectLitReview { get; private set; }

        public bool RequiresRuntimeSwapReview { get; private set; }

        public bool RequiresLightingReview { get; private set; }

        public string Guidance { get; private set; }

        public IReadOnlyList<DimensionAuthoringIssue> Issues { get; private set; }

        public int IssueCount
        {
            get { return Issues == null ? 0 : Issues.Count; }
        }
    }

    public sealed class DimensionVisualAssetValidationReport
    {
        public DimensionVisualAssetValidationReport(
            bool success,
            string code,
            string message,
            int readyCount,
            int advisoryCount,
            int blockedCount,
            int vanillaReferenceCount,
            int modAssetCount,
            int packageAssetCount,
            int unknownSourceCount,
            IReadOnlyList<DimensionVisualAssetValidationEntry> entries,
            IReadOnlyList<DimensionAuthoringIssue> issues)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            AdvisoryCount = advisoryCount < 0 ? 0 : advisoryCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            VanillaReferenceCount = vanillaReferenceCount < 0 ? 0 : vanillaReferenceCount;
            ModAssetCount = modAssetCount < 0 ? 0 : modAssetCount;
            PackageAssetCount = packageAssetCount < 0 ? 0 : packageAssetCount;
            UnknownSourceCount = unknownSourceCount < 0 ? 0 : unknownSourceCount;
            Entries = entries ?? new List<DimensionVisualAssetValidationEntry>();
            Issues = issues ?? new List<DimensionAuthoringIssue>();
        }

        public bool Success { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public int ReadyCount { get; private set; }

        public int AdvisoryCount { get; private set; }

        public int BlockedCount { get; private set; }

        public int VanillaReferenceCount { get; private set; }

        public int ModAssetCount { get; private set; }

        public int PackageAssetCount { get; private set; }

        public int UnknownSourceCount { get; private set; }

        public IReadOnlyList<DimensionVisualAssetValidationEntry> Entries { get; private set; }

        public IReadOnlyList<DimensionAuthoringIssue> Issues { get; private set; }

        public int EntryCount
        {
            get { return Entries == null ? 0 : Entries.Count; }
        }

        public int IssueCount
        {
            get { return Issues == null ? 0 : Issues.Count; }
        }
    }

    public static class DimensionVisualAssetValidationUtility
    {
        private const string RecordKind = "asset-reference";

        public static DimensionVisualAssetValidationReport Build(DimensionTemplateAsset template)
        {
            DimensionContentManifest manifest;
            DimensionCompiledGenerationPlan compiledPlan;
            DimensionOperationResult result;
            if (!DimensionTemplateManifestBuilder.TryBuildManifest(
                template,
                out manifest,
                out compiledPlan,
                out result))
            {
                List<DimensionAuthoringIssue> issues = new List<DimensionAuthoringIssue>();
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Error,
                    string.IsNullOrEmpty(result.Code) ? "manifest-build-failed" : result.Code,
                    string.IsNullOrEmpty(result.Message)
                        ? "The Dimension Asset could not be compiled into a manifest."
                        : result.Message,
                    string.Empty);

                return BuildReport(
                    false,
                    "manifest-build-failed",
                    "Visual asset validation needs a manifest-ready template.",
                    new List<DimensionAssetReferenceDefinition>(),
                    issues);
            }

            return Build(manifest.AssetReferences);
        }

        public static DimensionVisualAssetValidationReport Build(
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences)
        {
            return BuildReport(
                true,
                "ready",
                "Visual asset validation completed.",
                assetReferences,
                new List<DimensionAuthoringIssue>());
        }

        private static DimensionVisualAssetValidationReport BuildReport(
            bool manifestSuccess,
            string code,
            string message,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            List<DimensionAuthoringIssue> rootIssues)
        {
            List<DimensionVisualAssetValidationEntry> entries =
                new List<DimensionVisualAssetValidationEntry>();
            List<DimensionAuthoringIssue> issues = rootIssues ?? new List<DimensionAuthoringIssue>();

            int readyCount = 0;
            int advisoryCount = 0;
            int blockedCount = 0;
            int vanillaReferenceCount = 0;
            int modAssetCount = 0;
            int packageAssetCount = 0;
            int unknownSourceCount = 0;

            if (assetReferences != null)
            {
                for (int i = 0; i < assetReferences.Count; i++)
                {
                    DimensionAssetReferenceDefinition reference = assetReferences[i];
                    DimensionVisualAssetValidationEntry entry = BuildEntry(reference);
                    entries.Add(entry);

                    AddRange(issues, entry.Issues);

                    if (entry.State == DimensionVisualAssetValidationState.Blocked)
                    {
                        blockedCount++;
                    }
                    else if (entry.State == DimensionVisualAssetValidationState.Advisory)
                    {
                        advisoryCount++;
                    }
                    else
                    {
                        readyCount++;
                    }

                    if (entry.SourceKind == DimensionVisualAssetValidationSourceKind.VanillaReference)
                    {
                        vanillaReferenceCount++;
                    }
                    else if (entry.SourceKind == DimensionVisualAssetValidationSourceKind.ModAsset)
                    {
                        modAssetCount++;
                    }
                    else if (entry.SourceKind == DimensionVisualAssetValidationSourceKind.PackageAsset)
                    {
                        packageAssetCount++;
                    }
                    else
                    {
                        unknownSourceCount++;
                    }
                }
            }

            bool success = manifestSuccess && blockedCount == 0;
            string resolvedCode = !manifestSuccess
                ? code
                : blockedCount > 0
                    ? "visual-assets-blocked"
                    : advisoryCount > 0
                        ? "visual-assets-have-advisories"
                        : "ready";
            string resolvedMessage = !manifestSuccess
                ? message
                : blockedCount > 0
                    ? "One or more visual asset references are missing required identity/resource information."
                    : advisoryCount > 0
                        ? "Visual asset references are usable, but some need Core Keeper rendering or lighting review."
                        : "Visual asset references look ready for editor preview and runtime registration.";

            return new DimensionVisualAssetValidationReport(
                success,
                resolvedCode,
                resolvedMessage,
                readyCount,
                advisoryCount,
                blockedCount,
                vanillaReferenceCount,
                modAssetCount,
                packageAssetCount,
                unknownSourceCount,
                entries,
                issues);
        }

        private static DimensionVisualAssetValidationEntry BuildEntry(
            DimensionAssetReferenceDefinition reference)
        {
            List<DimensionAuthoringIssue> issues = new List<DimensionAuthoringIssue>();
            DimensionVisualAssetValidationSourceKind sourceKind =
                ResolveSourceKind(reference.ResourceKey);
            bool symbolicContentId = IsSymbolicContentId(reference.ResourceKey, sourceKind);
            bool visualRuntimeAsset = IsVisualRuntimeAsset(reference.Kind) && !symbolicContentId;
            bool moddedRuntimeVisual =
                visualRuntimeAsset &&
                sourceKind != DimensionVisualAssetValidationSourceKind.VanillaReference;
            bool requiresUgcReview =
                moddedRuntimeVisual &&
                RequiresUgcSpriteObjectLit(reference.Kind) &&
                !MentionsAny(reference.Notes, "ugc", "spriteobject", "sprite object", "lit material");
            bool requiresRuntimeSwapReview =
                moddedRuntimeVisual &&
                RequiresRuntimeSpriteObjectSwap(reference.Kind) &&
                !MentionsAny(reference.Notes, "runtime", "swap", "spriteobject", "sprite object", "vanilla reference");
            bool requiresLightingReview =
                moddedRuntimeVisual &&
                RequiresLightingReview(reference.Kind) &&
                !MentionsAny(reference.Notes, "light", "lighting", "shadow", "managed light", "unlit", "lit");

            if (string.IsNullOrEmpty(reference.AssetId))
            {
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Error,
                    "visual-asset-id-missing",
                    "A visual asset reference is missing a stable asset ID.",
                    reference.AssetId);
            }

            if (NeedsResourceKey(reference.Kind) && string.IsNullOrEmpty(reference.ResourceKey))
            {
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Error,
                    "visual-resource-key-missing",
                    "Asset reference '" + reference.AssetId + "' needs a resource key, vanilla asset key, or project asset key.",
                    reference.AssetId);
            }

            if (!reference.Enabled)
            {
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Info,
                    "visual-asset-disabled",
                    "Asset reference '" + reference.AssetId + "' is disabled and will not be registered.",
                    reference.AssetId);
            }

            if (sourceKind == DimensionVisualAssetValidationSourceKind.Unknown &&
                visualRuntimeAsset &&
                !string.IsNullOrEmpty(reference.ResourceKey))
            {
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Warning,
                    "visual-source-unknown",
                    "Asset reference '" + reference.AssetId + "' does not clearly declare whether it points to vanilla content, a mod asset, or a package asset.",
                    reference.AssetId);
            }

            if (requiresUgcReview)
            {
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Warning,
                    "visual-ugc-lit-review-needed",
                    "Asset reference '" + reference.AssetId + "' looks like a modded runtime visual. Confirm it uses the Core Keeper UGC SpriteObject Lit material path or note the validated alternative.",
                    reference.AssetId);
            }

            if (requiresRuntimeSwapReview)
            {
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Warning,
                    "visual-runtime-swap-review-needed",
                    "Asset reference '" + reference.AssetId + "' may need the runtime SpriteObject/material swap used by modded Core Keeper visuals. Add notes once verified.",
                    reference.AssetId);
            }

            if (requiresLightingReview)
            {
                AddIssue(
                    issues,
                    DimensionAuthoringSeverity.Warning,
                    "visual-lighting-review-needed",
                    "Asset reference '" + reference.AssetId + "' should document whether it emits light, receives darkness/fog correctly, or intentionally uses an unlit path.",
                    reference.AssetId);
            }

            DimensionVisualAssetValidationState state = ResolveState(issues);
            return new DimensionVisualAssetValidationEntry(
                reference,
                sourceKind,
                state,
                visualRuntimeAsset,
                requiresUgcReview,
                requiresRuntimeSwapReview,
                requiresLightingReview,
                BuildGuidance(reference, sourceKind, state),
                issues);
        }

        private static DimensionVisualAssetValidationState ResolveState(
            IReadOnlyList<DimensionAuthoringIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return DimensionVisualAssetValidationState.Ready;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == DimensionAuthoringSeverity.Error)
                {
                    return DimensionVisualAssetValidationState.Blocked;
                }
            }

            return DimensionVisualAssetValidationState.Advisory;
        }

        private static string BuildGuidance(
            DimensionAssetReferenceDefinition reference,
            DimensionVisualAssetValidationSourceKind sourceKind,
            DimensionVisualAssetValidationState state)
        {
            if (state == DimensionVisualAssetValidationState.Blocked)
            {
                return "Fix the missing ID/resource fields before this asset can be exported safely.";
            }

            if (sourceKind == DimensionVisualAssetValidationSourceKind.VanillaReference)
            {
                return "This reference is treated as vanilla-backed. Prefer this for vanilla content so game updates flow through naturally.";
            }

            if (IsSymbolicContentId(reference.ResourceKey, sourceKind))
            {
                return "This reference is treated as a custom content ID. Add a concrete project asset path later when visual validation is needed.";
            }

            if (IsVisualRuntimeAsset(reference.Kind))
            {
                return "Validate this visual in Unity and in-game with Core Keeper's UGC material/runtime SpriteObject path, lighting, shadows, map/icon behavior, and distance darkness.";
            }

            return "No special visual validation is required for this reference kind.";
        }

        private static bool NeedsResourceKey(DimensionAssetReferenceKind kind)
        {
            return kind != DimensionAssetReferenceKind.Any &&
                kind != DimensionAssetReferenceKind.Palette;
        }

        private static bool IsVisualRuntimeAsset(DimensionAssetReferenceKind kind)
        {
            return kind == DimensionAssetReferenceKind.Prefab ||
                kind == DimensionAssetReferenceKind.Sprite ||
                kind == DimensionAssetReferenceKind.Material ||
                kind == DimensionAssetReferenceKind.VisualEffect ||
                kind == DimensionAssetReferenceKind.Tile ||
                kind == DimensionAssetReferenceKind.Object ||
                kind == DimensionAssetReferenceKind.Item ||
                kind == DimensionAssetReferenceKind.Icon ||
                kind == DimensionAssetReferenceKind.Cursor ||
                kind == DimensionAssetReferenceKind.UiSprite ||
                kind == DimensionAssetReferenceKind.Scene;
        }

        private static bool RequiresUgcSpriteObjectLit(DimensionAssetReferenceKind kind)
        {
            return kind == DimensionAssetReferenceKind.Prefab ||
                kind == DimensionAssetReferenceKind.Sprite ||
                kind == DimensionAssetReferenceKind.Tile ||
                kind == DimensionAssetReferenceKind.Object ||
                kind == DimensionAssetReferenceKind.Scene;
        }

        private static bool RequiresRuntimeSpriteObjectSwap(DimensionAssetReferenceKind kind)
        {
            return kind == DimensionAssetReferenceKind.Prefab ||
                kind == DimensionAssetReferenceKind.Sprite ||
                kind == DimensionAssetReferenceKind.Tile ||
                kind == DimensionAssetReferenceKind.Object;
        }

        private static bool RequiresLightingReview(DimensionAssetReferenceKind kind)
        {
            return kind == DimensionAssetReferenceKind.Prefab ||
                kind == DimensionAssetReferenceKind.Tile ||
                kind == DimensionAssetReferenceKind.Object ||
                kind == DimensionAssetReferenceKind.Scene ||
                kind == DimensionAssetReferenceKind.Material ||
                kind == DimensionAssetReferenceKind.VisualEffect;
        }

        private static DimensionVisualAssetValidationSourceKind ResolveSourceKind(
            string resourceKey)
        {
            string key = (resourceKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key))
            {
                return DimensionVisualAssetValidationSourceKind.Unknown;
            }

            string lower = key.ToLowerInvariant();
            if (lower.StartsWith("vanilla:") ||
                lower.StartsWith("core:") ||
                lower.StartsWith("corekeeper:") ||
                lower.StartsWith("ck:"))
            {
                return DimensionVisualAssetValidationSourceKind.VanillaReference;
            }

            if (lower.StartsWith("packages/") ||
                lower.StartsWith("package:"))
            {
                return DimensionVisualAssetValidationSourceKind.PackageAsset;
            }

            if (lower.StartsWith("assets/") ||
                lower.StartsWith("asset:") ||
                lower.IndexOf('/') >= 0 ||
                lower.IndexOf('\\') >= 0)
            {
                return DimensionVisualAssetValidationSourceKind.ModAsset;
            }

            if (lower.IndexOf(':') > 0)
            {
                return DimensionVisualAssetValidationSourceKind.ModAsset;
            }

            return DimensionVisualAssetValidationSourceKind.Unknown;
        }

        private static bool IsSymbolicContentId(
            string resourceKey,
            DimensionVisualAssetValidationSourceKind sourceKind)
        {
            string key = (resourceKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key) ||
                sourceKind != DimensionVisualAssetValidationSourceKind.ModAsset)
            {
                return false;
            }

            return key.IndexOf(':') > 0 &&
                key.IndexOf('/') < 0 &&
                key.IndexOf('\\') < 0;
        }

        private static bool MentionsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            string lower = value.ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && lower.IndexOf(token.ToLowerInvariant()) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddRange(
            List<DimensionAuthoringIssue> destination,
            IReadOnlyList<DimensionAuthoringIssue> source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static void AddIssue(
            List<DimensionAuthoringIssue> issues,
            DimensionAuthoringSeverity severity,
            string code,
            string message,
            string recordId)
        {
            if (issues == null)
            {
                return;
            }

            issues.Add(new DimensionAuthoringIssue(
                severity,
                code,
                message,
                RecordKind,
                recordId,
                false,
                default(DimensionBounds)));
        }
    }
}
