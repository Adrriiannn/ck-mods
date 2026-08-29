using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerActionIntent
    {
        Inspect = 0,
        SelectTemplate = 1,
        AddContent = 2,
        ConfigureContent = 3,
        ResolveIssue = 4,
        Review = 5,
        Validate = 6,
        Export = 7,
        Apply = 8,
        PrepareRuntime = 9,
        Ready = 10
    }

    public enum DimensionTemplateCustomizerActionMutability
    {
        None = 0,
        Navigation = 1,
        AssetMutation = 2,
        ManifestValidation = 3,
        ManifestExport = 4,
        ManifestApply = 5,
        RuntimePreparation = 6
    }

    public enum DimensionTemplateCustomizerActionAvailability
    {
        Available = 0,
        Disabled = 1,
        Warning = 2,
        Blocked = 3
    }

    public readonly struct DimensionTemplateCustomizerActionDescriptor
    {
        public readonly string ActionId;
        public readonly string Label;
        public readonly string Tooltip;
        public readonly string SectionId;
        public readonly string BiomeId;
        public readonly string RecordKind;
        public readonly string RecordId;
        public readonly DimensionTemplateCustomizerActionIntent Intent;
        public readonly DimensionTemplateCustomizerActionMutability Mutability;
        public readonly DimensionTemplateCustomizerActionAvailability Availability;
        public readonly DimensionAuthoringReadinessState State;
        public readonly DimensionAuthoringSeverity Severity;
        public readonly bool Recommended;
        public readonly bool RequiresSelection;
        public readonly bool RequiresConfirmation;
        public readonly bool MutatesAssets;
        public readonly bool TouchesRuntimeState;
        public readonly int Priority;

        public DimensionTemplateCustomizerActionDescriptor(
            string actionId,
            string label,
            string tooltip,
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            DimensionTemplateCustomizerActionIntent intent,
            DimensionTemplateCustomizerActionMutability mutability,
            DimensionTemplateCustomizerActionAvailability availability,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            bool recommended,
            bool requiresSelection,
            bool requiresConfirmation,
            bool mutatesAssets,
            bool touchesRuntimeState,
            int priority)
        {
            ActionId = actionId ?? string.Empty;
            Label = label ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            Intent = intent;
            Mutability = mutability;
            Availability = availability;
            State = state;
            Severity = severity;
            Recommended = recommended;
            RequiresSelection = requiresSelection;
            RequiresConfirmation = requiresConfirmation;
            MutatesAssets = mutatesAssets;
            TouchesRuntimeState = touchesRuntimeState;
            Priority = priority < 0 ? 0 : priority;
        }
    }

    public sealed class DimensionTemplateCustomizerActionCatalog
    {
        public DimensionTemplateCustomizerActionCatalog(
            string activeSectionId,
            string primaryActionId,
            int actionCount,
            int recommendedCount,
            int blockedCount,
            int warningCount,
            IReadOnlyList<DimensionTemplateCustomizerActionDescriptor> actions)
        {
            ActiveSectionId = activeSectionId ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            ActionCount = actionCount < 0 ? 0 : actionCount;
            RecommendedCount = recommendedCount < 0 ? 0 : recommendedCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Actions = actions ?? new List<DimensionTemplateCustomizerActionDescriptor>();
        }

        public string ActiveSectionId { get; private set; }

        public string PrimaryActionId { get; private set; }

        public int ActionCount { get; private set; }

        public int RecommendedCount { get; private set; }

        public int BlockedCount { get; private set; }

        public int WarningCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerActionDescriptor> Actions { get; private set; }
    }

    public static class DimensionTemplateCustomizerActionCatalogUtility
    {
        public static DimensionTemplateCustomizerActionCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            if (workspace == null)
            {
                List<DimensionTemplateCustomizerActionDescriptor> missing =
                    new List<DimensionTemplateCustomizerActionDescriptor>();
                AddDescriptor(
                    missing,
                    new List<string>(),
                    CreateDescriptor(
                        "select-dimension-template",
                        "overview",
                        string.Empty,
                        "workspace",
                        string.Empty,
                        DimensionAuthoringReadinessState.Blocked,
                        DimensionAuthoringSeverity.Error,
                        true,
                        0,
                    "Select or create a Dimension Asset before opening the customizer."));

                return new DimensionTemplateCustomizerActionCatalog(
                    "overview",
                    "select-dimension-template",
                    missing.Count,
                    1,
                    1,
                    0,
                    missing);
            }

            string activeSectionId = string.IsNullOrEmpty(sectionId)
                ? workspace.Navigation.ActiveSectionId
                : sectionId;

            List<DimensionTemplateCustomizerActionDescriptor> actions =
                new List<DimensionTemplateCustomizerActionDescriptor>();
            List<string> keys = new List<string>();

            AddSessionAction(actions, keys, workspace.SessionReport);
            AddNavigationActions(actions, keys, workspace.Navigation.Sections);

            DimensionTemplateCustomizerSectionDetail sectionDetail =
                workspace.GetSectionDetail(activeSectionId);
            AddSectionDetailActions(actions, keys, sectionDetail);

            actions.Sort(CompareDescriptors);

            int recommendedCount;
            int blockedCount;
            int warningCount;
            CountActions(actions, out recommendedCount, out blockedCount, out warningCount);

            return new DimensionTemplateCustomizerActionCatalog(
                activeSectionId,
                workspace.Navigation.PrimaryActionId,
                actions.Count,
                recommendedCount,
                blockedCount,
                warningCount,
                actions);
        }

        private static void AddSessionAction(
            List<DimensionTemplateCustomizerActionDescriptor> actions,
            List<string> keys,
            DimensionTemplateCustomizerSessionReport session)
        {
            if (session == null || string.IsNullOrEmpty(session.PrimaryActionId))
            {
                return;
            }

            AddDescriptor(
                actions,
                keys,
                CreateDescriptor(
                    session.PrimaryActionId,
                    "overview",
                    string.Empty,
                    "session",
                    session.DimensionId,
                    ResolveState(session),
                    ResolveSeverity(session),
                    true,
                    0,
                    session.Message));
        }

        private static void AddNavigationActions(
            List<DimensionTemplateCustomizerActionDescriptor> actions,
            List<string> keys,
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections)
        {
            if (sections == null)
            {
                return;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                DimensionTemplateCustomizerSectionItem section = sections[i];
                AddDescriptor(
                    actions,
                    keys,
                    CreateDescriptor(
                        section.PrimaryActionId,
                        section.SectionId,
                        string.Empty,
                        "section",
                        section.SectionId,
                        section.State,
                        ResolveSeverity(section),
                        section.Recommended,
                        section.Priority,
                        section.Message));
            }
        }

        private static void AddSectionDetailActions(
            List<DimensionTemplateCustomizerActionDescriptor> actions,
            List<string> keys,
            DimensionTemplateCustomizerSectionDetail detail)
        {
            if (detail == null || detail.Rows == null)
            {
                return;
            }

            IReadOnlyList<DimensionTemplateCustomizerDetailRow> rows = detail.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                DimensionTemplateCustomizerDetailRow row = rows[i];
                if (string.IsNullOrEmpty(row.PrimaryActionId))
                {
                    continue;
                }

                AddDescriptor(
                    actions,
                    keys,
                    CreateDescriptor(
                        row.PrimaryActionId,
                        row.SectionId,
                        row.BiomeId,
                        row.RecordKind,
                        row.RecordId,
                        row.State,
                        row.Severity,
                        row.Priority <= 20,
                        row.Priority,
                        row.Message));
            }
        }

        private static DimensionTemplateCustomizerActionDescriptor CreateDescriptor(
            string actionId,
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            bool recommended,
            int priority,
            string tooltip)
        {
            string normalizedActionId = actionId ?? string.Empty;
            DimensionTemplateCustomizerActionIntent intent = ResolveIntent(normalizedActionId);
            DimensionTemplateCustomizerActionMutability mutability = ResolveMutability(normalizedActionId, intent);
            bool mutatesAssets = mutability == DimensionTemplateCustomizerActionMutability.AssetMutation ||
                mutability == DimensionTemplateCustomizerActionMutability.ManifestApply;
            bool touchesRuntime = mutability == DimensionTemplateCustomizerActionMutability.RuntimePreparation ||
                mutability == DimensionTemplateCustomizerActionMutability.ManifestApply;
            bool requiresConfirmation =
                mutability == DimensionTemplateCustomizerActionMutability.ManifestApply ||
                normalizedActionId == "fix-issues" ||
                normalizedActionId == "fix-readiness-blocker";

            return new DimensionTemplateCustomizerActionDescriptor(
                normalizedActionId,
                ResolveLabel(normalizedActionId, intent),
                tooltip,
                sectionId,
                biomeId,
                recordKind,
                recordId,
                intent,
                mutability,
                ResolveAvailability(normalizedActionId, state, severity),
                state,
                severity,
                recommended,
                RequiresSelection(intent, normalizedActionId),
                requiresConfirmation,
                mutatesAssets,
                touchesRuntime,
                priority);
        }

        private static void AddDescriptor(
            List<DimensionTemplateCustomizerActionDescriptor> actions,
            List<string> keys,
            DimensionTemplateCustomizerActionDescriptor descriptor)
        {
            if (actions == null || keys == null || string.IsNullOrEmpty(descriptor.ActionId))
            {
                return;
            }

            string key = descriptor.ActionId + "|" +
                descriptor.SectionId + "|" +
                descriptor.BiomeId + "|" +
                descriptor.RecordKind + "|" +
                descriptor.RecordId;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == key)
                {
                    return;
                }
            }

            keys.Add(key);
            actions.Add(descriptor);
        }

        private static DimensionAuthoringReadinessState ResolveState(
            DimensionTemplateCustomizerSessionReport session)
        {
            if (session == null)
            {
                return DimensionAuthoringReadinessState.Blocked;
            }

            if (session.ErrorCount > 0 || session.BlockingManifestExportCount > 0)
            {
                return DimensionAuthoringReadinessState.Blocked;
            }

            if (!session.ReadyForApply || session.WarningCount > 0)
            {
                return DimensionAuthoringReadinessState.Partial;
            }

            return DimensionAuthoringReadinessState.Ready;
        }

        private static DimensionAuthoringSeverity ResolveSeverity(
            DimensionTemplateCustomizerSessionReport session)
        {
            if (session == null || session.ErrorCount > 0 || session.BlockingManifestExportCount > 0)
            {
                return DimensionAuthoringSeverity.Error;
            }

            return session.WarningCount > 0
                ? DimensionAuthoringSeverity.Warning
                : DimensionAuthoringSeverity.Info;
        }

        private static DimensionAuthoringSeverity ResolveSeverity(
            DimensionTemplateCustomizerSectionItem section)
        {
            if (section.ErrorCount > 0 || section.BlockingCount > 0)
            {
                return DimensionAuthoringSeverity.Error;
            }

            return section.WarningCount > 0
                ? DimensionAuthoringSeverity.Warning
                : DimensionAuthoringSeverity.Info;
        }

        private static DimensionTemplateCustomizerActionIntent ResolveIntent(string actionId)
        {
            if (actionId == "select-dimension-template")
            {
                return DimensionTemplateCustomizerActionIntent.SelectTemplate;
            }

            if (actionId == "add-required-content" || actionId == "add-content")
            {
                return DimensionTemplateCustomizerActionIntent.AddContent;
            }

            if (actionId == "configure-content" ||
                actionId == "configure-runtime-generation" ||
                actionId.StartsWith("configure-"))
            {
                return DimensionTemplateCustomizerActionIntent.ConfigureContent;
            }

            if (actionId == "fix-authoring" ||
                actionId == "fix-readiness-blocker" ||
                actionId == "fix-issues" ||
                actionId == "resolve-spatial-conflict")
            {
                return DimensionTemplateCustomizerActionIntent.ResolveIssue;
            }

            if (actionId == "review-recommended-action" ||
                actionId == "review-warning" ||
                actionId == "review-issues" ||
                actionId == "review-diagnostics" ||
                actionId == "inspect-blockers" ||
                actionId == "inspect-runtime-readiness")
            {
                return DimensionTemplateCustomizerActionIntent.Review;
            }

            if (actionId == "validate-manifest" || actionId == "preview-manifest")
            {
                return DimensionTemplateCustomizerActionIntent.Validate;
            }

            if (actionId == "export-manifest")
            {
                return DimensionTemplateCustomizerActionIntent.Export;
            }

            if (actionId == "apply-manifest")
            {
                return DimensionTemplateCustomizerActionIntent.Apply;
            }

            if (actionId == "prepare-runtime-generation")
            {
                return DimensionTemplateCustomizerActionIntent.PrepareRuntime;
            }

            if (actionId == "ready")
            {
                return DimensionTemplateCustomizerActionIntent.Ready;
            }

            return DimensionTemplateCustomizerActionIntent.Inspect;
        }

        private static DimensionTemplateCustomizerActionMutability ResolveMutability(
            string actionId,
            DimensionTemplateCustomizerActionIntent intent)
        {
            if (intent == DimensionTemplateCustomizerActionIntent.SelectTemplate ||
                intent == DimensionTemplateCustomizerActionIntent.Inspect ||
                intent == DimensionTemplateCustomizerActionIntent.Review ||
                intent == DimensionTemplateCustomizerActionIntent.Ready)
            {
                return DimensionTemplateCustomizerActionMutability.Navigation;
            }

            if (intent == DimensionTemplateCustomizerActionIntent.Validate)
            {
                return DimensionTemplateCustomizerActionMutability.ManifestValidation;
            }

            if (intent == DimensionTemplateCustomizerActionIntent.Export)
            {
                return DimensionTemplateCustomizerActionMutability.ManifestExport;
            }

            if (intent == DimensionTemplateCustomizerActionIntent.Apply)
            {
                return DimensionTemplateCustomizerActionMutability.ManifestApply;
            }

            if (intent == DimensionTemplateCustomizerActionIntent.PrepareRuntime ||
                actionId == "configure-runtime-generation")
            {
                return DimensionTemplateCustomizerActionMutability.RuntimePreparation;
            }

            return DimensionTemplateCustomizerActionMutability.AssetMutation;
        }

        private static DimensionTemplateCustomizerActionAvailability ResolveAvailability(
            string actionId,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity)
        {
            if (string.IsNullOrEmpty(actionId) || actionId == "ready")
            {
                return DimensionTemplateCustomizerActionAvailability.Disabled;
            }

            if ((actionId == "apply-manifest" || actionId == "export-manifest") &&
                state != DimensionAuthoringReadinessState.Ready)
            {
                return DimensionTemplateCustomizerActionAvailability.Blocked;
            }

            if (severity == DimensionAuthoringSeverity.Error &&
                !actionId.StartsWith("fix") &&
                !actionId.StartsWith("inspect") &&
                !actionId.StartsWith("review"))
            {
                return DimensionTemplateCustomizerActionAvailability.Blocked;
            }

            if (severity == DimensionAuthoringSeverity.Warning ||
                state == DimensionAuthoringReadinessState.Partial ||
                state == DimensionAuthoringReadinessState.Missing ||
                state == DimensionAuthoringReadinessState.Blocked)
            {
                return DimensionTemplateCustomizerActionAvailability.Warning;
            }

            return DimensionTemplateCustomizerActionAvailability.Available;
        }

        private static bool RequiresSelection(
            DimensionTemplateCustomizerActionIntent intent,
            string actionId)
        {
            if (intent == DimensionTemplateCustomizerActionIntent.SelectTemplate ||
                actionId == "validate-manifest" ||
                actionId == "preview-manifest" ||
                actionId == "export-manifest" ||
                actionId == "apply-manifest")
            {
                return false;
            }

            return intent == DimensionTemplateCustomizerActionIntent.AddContent ||
                intent == DimensionTemplateCustomizerActionIntent.ConfigureContent ||
                intent == DimensionTemplateCustomizerActionIntent.ResolveIssue ||
                intent == DimensionTemplateCustomizerActionIntent.Review;
        }

        private static string ResolveLabel(
            string actionId,
            DimensionTemplateCustomizerActionIntent intent)
        {
            if (actionId == "select-dimension-template")
            {
                return "Select template";
            }

            if (actionId == "add-required-content" || actionId == "add-content")
            {
                return "Add content";
            }

            if (actionId == "configure-content")
            {
                return "Configure";
            }

            if (actionId == "configure-runtime-generation" ||
                actionId == "prepare-runtime-generation" ||
                actionId == "inspect-runtime-readiness")
            {
                return "Runtime readiness";
            }

            if (actionId == "fix-authoring" ||
                actionId == "fix-readiness-blocker" ||
                actionId == "fix-issues")
            {
                return "Fix issue";
            }

            if (actionId == "resolve-spatial-conflict")
            {
                return "Resolve overlap";
            }

            if (actionId == "review-recommended-action" ||
                actionId == "review-warning" ||
                actionId == "review-issues" ||
                actionId == "review-diagnostics")
            {
                return "Review";
            }

            if (actionId == "inspect-blockers")
            {
                return "Inspect blockers";
            }

            if (actionId == "preview-manifest")
            {
                return "Preview manifest";
            }

            if (actionId == "validate-manifest")
            {
                return "Validate manifest";
            }

            if (actionId == "export-manifest")
            {
                return "Export manifest";
            }

            if (actionId == "apply-manifest")
            {
                return "Apply manifest";
            }

            if (actionId == "ready")
            {
                return "Ready";
            }

            if (intent == DimensionTemplateCustomizerActionIntent.ConfigureContent &&
                actionId.StartsWith("configure-"))
            {
                string tail = actionId.Substring("configure-".Length);
                return "Configure " + tail.Replace("-", " ");
            }

            return actionId.Replace("-", " ");
        }

        private static void CountActions(
            IReadOnlyList<DimensionTemplateCustomizerActionDescriptor> actions,
            out int recommendedCount,
            out int blockedCount,
            out int warningCount)
        {
            recommendedCount = 0;
            blockedCount = 0;
            warningCount = 0;
            if (actions == null)
            {
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                DimensionTemplateCustomizerActionDescriptor action = actions[i];
                if (action.Recommended)
                {
                    recommendedCount++;
                }

                if (action.Availability == DimensionTemplateCustomizerActionAvailability.Blocked)
                {
                    blockedCount++;
                }
                else if (action.Availability == DimensionTemplateCustomizerActionAvailability.Warning)
                {
                    warningCount++;
                }
            }
        }

        private static int CompareDescriptors(
            DimensionTemplateCustomizerActionDescriptor left,
            DimensionTemplateCustomizerActionDescriptor right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            if (left.Recommended != right.Recommended)
            {
                return left.Recommended ? -1 : 1;
            }

            int section = string.CompareOrdinal(left.SectionId, right.SectionId);
            if (section != 0)
            {
                return section;
            }

            return string.CompareOrdinal(left.Label, right.Label);
        }
    }
}
