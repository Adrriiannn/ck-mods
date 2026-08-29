using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerIssueRepairState
    {
        None = 0,
        InspectOnly = 1,
        CanJumpToTarget = 2,
        CanPreviewFieldEdit = 3,
        NeedsContentCreation = 4,
        NeedsEditorImplementation = 5,
        Blocked = 6
    }

    public sealed class DimensionTemplateCustomizerIssueRepairStep
    {
        public DimensionTemplateCustomizerIssueRepairStep(
            int stepIndex,
            string stepId,
            string label,
            string detail,
            bool isRequired,
            bool isAutomatic,
            bool requiresEditorImplementation)
        {
            StepIndex = stepIndex < 0 ? 0 : stepIndex;
            StepId = stepId ?? string.Empty;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
            IsRequired = isRequired;
            IsAutomatic = isAutomatic;
            RequiresEditorImplementation = requiresEditorImplementation;
        }

        public int StepIndex { get; private set; }

        public string StepId { get; private set; }

        public string Label { get; private set; }

        public string Detail { get; private set; }

        public bool IsRequired { get; private set; }

        public bool IsAutomatic { get; private set; }

        public bool RequiresEditorImplementation { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerIssueRepairPlan
    {
        public DimensionTemplateCustomizerIssueRepairPlan(
            DimensionTemplateCustomizerIssueResolutionHint hint,
            DimensionTemplateCustomizerIssueRepairState state,
            string repairId,
            string summary,
            string blockedReason,
            bool canRunReadOnly,
            bool canMutateAssets,
            bool requiresConfirmation,
            bool requiresEditorImplementation,
            IReadOnlyList<DimensionTemplateCustomizerIssueRepairStep> steps)
        {
            Hint = hint;
            State = state;
            RepairId = repairId ?? string.Empty;
            Summary = summary ?? string.Empty;
            BlockedReason = blockedReason ?? string.Empty;
            CanRunReadOnly = canRunReadOnly;
            CanMutateAssets = canMutateAssets;
            RequiresConfirmation = requiresConfirmation;
            RequiresEditorImplementation = requiresEditorImplementation;
            Steps = steps ?? new List<DimensionTemplateCustomizerIssueRepairStep>();
        }

        public DimensionTemplateCustomizerIssueResolutionHint Hint { get; private set; }

        public DimensionTemplateCustomizerIssueRepairState State { get; private set; }

        public string RepairId { get; private set; }

        public string Summary { get; private set; }

        public string BlockedReason { get; private set; }

        public bool CanRunReadOnly { get; private set; }

        public bool CanMutateAssets { get; private set; }

        public bool RequiresConfirmation { get; private set; }

        public bool RequiresEditorImplementation { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerIssueRepairStep> Steps { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerIssueRepairCatalog
    {
        public DimensionTemplateCustomizerIssueRepairCatalog(
            string sectionId,
            int repairCount,
            int readOnlyCount,
            int assetMutationCount,
            int requiresImplementationCount,
            int blockedCount,
            IReadOnlyList<DimensionTemplateCustomizerIssueRepairPlan> repairs)
        {
            SectionId = sectionId ?? string.Empty;
            RepairCount = repairCount < 0 ? 0 : repairCount;
            ReadOnlyCount = readOnlyCount < 0 ? 0 : readOnlyCount;
            AssetMutationCount = assetMutationCount < 0 ? 0 : assetMutationCount;
            RequiresImplementationCount = requiresImplementationCount < 0 ? 0 : requiresImplementationCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            Repairs = repairs ?? new List<DimensionTemplateCustomizerIssueRepairPlan>();
        }

        public string SectionId { get; private set; }

        public int RepairCount { get; private set; }

        public int ReadOnlyCount { get; private set; }

        public int AssetMutationCount { get; private set; }

        public int RequiresImplementationCount { get; private set; }

        public int BlockedCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerIssueRepairPlan> Repairs { get; private set; }
    }

    public static class DimensionTemplateCustomizerIssueRepairPlanUtility
    {
        public static DimensionTemplateCustomizerIssueRepairCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            return Build(workspace, string.Empty);
        }

        public static DimensionTemplateCustomizerIssueRepairCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            DimensionTemplateCustomizerIssueResolutionCatalog resolutionCatalog =
                workspace == null
                    ? DimensionTemplateCustomizerIssueResolutionUtility.Build(null, sectionId)
                    : workspace.GetIssueResolutionCatalog(sectionId);

            List<DimensionTemplateCustomizerIssueRepairPlan> repairs =
                new List<DimensionTemplateCustomizerIssueRepairPlan>();
            IReadOnlyList<DimensionTemplateCustomizerIssueResolutionHint> hints =
                resolutionCatalog.Hints;
            if (hints != null)
            {
                for (int i = 0; i < hints.Count; i++)
                {
                    repairs.Add(BuildPlan(hints[i]));
                }
            }

            int readOnly;
            int assetMutation;
            int requiresImplementation;
            int blocked;
            CountPlans(
                repairs,
                out readOnly,
                out assetMutation,
                out requiresImplementation,
                out blocked);

            return new DimensionTemplateCustomizerIssueRepairCatalog(
                resolutionCatalog.SectionId,
                repairs.Count,
                readOnly,
                assetMutation,
                requiresImplementation,
                blocked,
                repairs);
        }

        public static DimensionTemplateCustomizerIssueRepairPlan BuildPlan(
            DimensionTemplateCustomizerIssueResolutionHint hint)
        {
            if (hint == null)
            {
                return new DimensionTemplateCustomizerIssueRepairPlan(
                    null,
                    DimensionTemplateCustomizerIssueRepairState.None,
                    string.Empty,
                    string.Empty,
                    "No issue hint was supplied.",
                    false,
                    false,
                    false,
                    false,
                    new List<DimensionTemplateCustomizerIssueRepairStep>());
            }

            List<DimensionTemplateCustomizerIssueRepairStep> steps =
                new List<DimensionTemplateCustomizerIssueRepairStep>();
            AddFocusStep(steps, hint);
            AddActionStep(steps, hint);
            AddValidationStep(steps, hint);

            DimensionTemplateCustomizerIssueRepairState state = ResolveState(hint);
            bool requiresImplementation = RequiresEditorImplementation(hint, state);
            bool canMutateAssets = CanMutateAssets(state);
            bool canRunReadOnly = state == DimensionTemplateCustomizerIssueRepairState.InspectOnly ||
                state == DimensionTemplateCustomizerIssueRepairState.CanJumpToTarget;
            string blockedReason = ResolveBlockedReason(hint, state);

            return new DimensionTemplateCustomizerIssueRepairPlan(
                hint,
                state,
                BuildRepairId(hint),
                ResolveSummary(hint, state),
                blockedReason,
                canRunReadOnly,
                canMutateAssets,
                RequiresConfirmation(hint, state),
                requiresImplementation,
                steps);
        }

        private static void AddFocusStep(
            List<DimensionTemplateCustomizerIssueRepairStep> steps,
            DimensionTemplateCustomizerIssueResolutionHint hint)
        {
            string detail = string.IsNullOrEmpty(hint.RecordId)
                ? "Focus section `" + hint.SectionId + "`."
                : "Focus `" + hint.RecordKind + "` record `" + hint.RecordId + "` in `" + hint.SectionId + "`.";
            steps.Add(new DimensionTemplateCustomizerIssueRepairStep(
                steps.Count + 1,
                "focus-target",
                "Focus target",
                detail,
                true,
                true,
                false));

            if (hint.CanJumpToBounds)
            {
                steps.Add(new DimensionTemplateCustomizerIssueRepairStep(
                    steps.Count + 1,
                    "jump-to-bounds",
                    "Jump to bounds",
                    "Move the canvas/map preview to the affected local bounds.",
                    false,
                    true,
                    false));
            }
        }

        private static void AddActionStep(
            List<DimensionTemplateCustomizerIssueRepairStep> steps,
            DimensionTemplateCustomizerIssueResolutionHint hint)
        {
            if (!string.IsNullOrEmpty(hint.SuggestedFieldId))
            {
                steps.Add(new DimensionTemplateCustomizerIssueRepairStep(
                    steps.Count + 1,
                    "edit-field",
                    "Edit field",
                    "Open field `" + hint.SuggestedFieldId + "` and preview the new value before applying it.",
                    true,
                    false,
                    true));
                return;
            }

            if (!string.IsNullOrEmpty(hint.SuggestedActionId))
            {
                steps.Add(new DimensionTemplateCustomizerIssueRepairStep(
                    steps.Count + 1,
                    "run-action",
                    "Run action",
                    "Offer action `" + hint.SuggestedActionId + "` from the matching customizer section.",
                    true,
                    false,
                    hint.RequiresEditorImplementation));
                return;
            }

            steps.Add(new DimensionTemplateCustomizerIssueRepairStep(
                steps.Count + 1,
                "inspect-issue",
                "Inspect issue",
                "Show the issue message and affected record without changing assets.",
                true,
                true,
                false));
        }

        private static void AddValidationStep(
            List<DimensionTemplateCustomizerIssueRepairStep> steps,
            DimensionTemplateCustomizerIssueResolutionHint hint)
        {
            steps.Add(new DimensionTemplateCustomizerIssueRepairStep(
                steps.Count + 1,
                "revalidate-template",
                "Revalidate",
                "Rebuild the authoring preview and dry-run validation after the correction.",
                true,
                false,
                hint.RequiresEditorImplementation));
        }

        private static DimensionTemplateCustomizerIssueRepairState ResolveState(
            DimensionTemplateCustomizerIssueResolutionHint hint)
        {
            if (hint.Kind == DimensionTemplateCustomizerIssueResolutionKind.EditField)
            {
                return DimensionTemplateCustomizerIssueRepairState.CanPreviewFieldEdit;
            }

            if (hint.Kind == DimensionTemplateCustomizerIssueResolutionKind.AddContent)
            {
                return DimensionTemplateCustomizerIssueRepairState.NeedsContentCreation;
            }

            if (hint.Kind == DimensionTemplateCustomizerIssueResolutionKind.JumpToBounds)
            {
                return DimensionTemplateCustomizerIssueRepairState.CanJumpToTarget;
            }

            if (hint.RequiresEditorImplementation)
            {
                return DimensionTemplateCustomizerIssueRepairState.NeedsEditorImplementation;
            }

            if (hint.Kind == DimensionTemplateCustomizerIssueResolutionKind.Inspect ||
                hint.Kind == DimensionTemplateCustomizerIssueResolutionKind.Validate)
            {
                return DimensionTemplateCustomizerIssueRepairState.InspectOnly;
            }

            return string.IsNullOrEmpty(hint.SuggestedActionId)
                ? DimensionTemplateCustomizerIssueRepairState.Blocked
                : DimensionTemplateCustomizerIssueRepairState.NeedsEditorImplementation;
        }

        private static string ResolveBlockedReason(
            DimensionTemplateCustomizerIssueResolutionHint hint,
            DimensionTemplateCustomizerIssueRepairState state)
        {
            if (state == DimensionTemplateCustomizerIssueRepairState.Blocked)
            {
                return "No safe repair route is available yet; inspect the issue manually.";
            }

            if (state == DimensionTemplateCustomizerIssueRepairState.NeedsEditorImplementation)
            {
                return "The repair route is known, but the future Unity editor implementation must perform it.";
            }

            if (state == DimensionTemplateCustomizerIssueRepairState.NeedsContentCreation)
            {
                return "Content creation must be implemented by the future editor UI before this can be one-click repaired.";
            }

            return string.Empty;
        }

        private static string ResolveSummary(
            DimensionTemplateCustomizerIssueResolutionHint hint,
            DimensionTemplateCustomizerIssueRepairState state)
        {
            if (!string.IsNullOrEmpty(hint.Hint))
            {
                return hint.Hint;
            }

            if (state == DimensionTemplateCustomizerIssueRepairState.CanPreviewFieldEdit)
            {
                return "Preview and apply a safe field edit for this issue.";
            }

            if (state == DimensionTemplateCustomizerIssueRepairState.CanJumpToTarget)
            {
                return "Jump to the affected area and inspect the issue.";
            }

            return string.IsNullOrEmpty(hint.IssueMessage)
                ? "Inspect this authoring issue."
                : hint.IssueMessage;
        }

        private static bool RequiresEditorImplementation(
            DimensionTemplateCustomizerIssueResolutionHint hint,
            DimensionTemplateCustomizerIssueRepairState state)
        {
            return hint.RequiresEditorImplementation ||
                state == DimensionTemplateCustomizerIssueRepairState.CanPreviewFieldEdit ||
                state == DimensionTemplateCustomizerIssueRepairState.NeedsContentCreation ||
                state == DimensionTemplateCustomizerIssueRepairState.NeedsEditorImplementation;
        }

        private static bool CanMutateAssets(
            DimensionTemplateCustomizerIssueRepairState state)
        {
            return state == DimensionTemplateCustomizerIssueRepairState.CanPreviewFieldEdit ||
                state == DimensionTemplateCustomizerIssueRepairState.NeedsContentCreation ||
                state == DimensionTemplateCustomizerIssueRepairState.NeedsEditorImplementation;
        }

        private static bool RequiresConfirmation(
            DimensionTemplateCustomizerIssueResolutionHint hint,
            DimensionTemplateCustomizerIssueRepairState state)
        {
            return hint.Severity == ExpandNullforge.Api.DimensionAuthoringSeverity.Error &&
                CanMutateAssets(state);
        }

        private static string BuildRepairId(
            DimensionTemplateCustomizerIssueResolutionHint hint)
        {
            string code = string.IsNullOrEmpty(hint.IssueCode) ? "issue" : hint.IssueCode;
            string recordKind = string.IsNullOrEmpty(hint.RecordKind) ? "record" : hint.RecordKind;
            string recordId = string.IsNullOrEmpty(hint.RecordId) ? "none" : hint.RecordId;
            return code + ":" + recordKind + ":" + recordId;
        }

        private static void CountPlans(
            IReadOnlyList<DimensionTemplateCustomizerIssueRepairPlan> plans,
            out int readOnly,
            out int assetMutation,
            out int requiresImplementation,
            out int blocked)
        {
            readOnly = 0;
            assetMutation = 0;
            requiresImplementation = 0;
            blocked = 0;
            if (plans == null)
            {
                return;
            }

            for (int i = 0; i < plans.Count; i++)
            {
                DimensionTemplateCustomizerIssueRepairPlan plan = plans[i];
                if (plan.CanRunReadOnly)
                {
                    readOnly++;
                }

                if (plan.CanMutateAssets)
                {
                    assetMutation++;
                }

                if (plan.RequiresEditorImplementation)
                {
                    requiresImplementation++;
                }

                if (plan.State == DimensionTemplateCustomizerIssueRepairState.Blocked)
                {
                    blocked++;
                }
            }
        }
    }
}
