using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldEditBatchState
    {
        Empty = 0,
        Blocked = 1,
        NeedsWarningConfirmation = 2,
        Ready = 3
    }

    public sealed class DimensionTemplateCustomizerFieldEditBatchRequest
    {
        public DimensionTemplateCustomizerFieldEditBatchRequest(
            IReadOnlyList<DimensionTemplateCustomizerFieldEditRequest> edits)
        {
            Edits = edits ?? new List<DimensionTemplateCustomizerFieldEditRequest>();
        }

        public IReadOnlyList<DimensionTemplateCustomizerFieldEditRequest> Edits { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldEditBatchEntry
    {
        public DimensionTemplateCustomizerFieldEditBatchEntry(
            int index,
            string targetKey,
            bool duplicateTarget,
            bool canApply,
            bool hasWarning,
            string code,
            string message,
            DimensionTemplateCustomizerFieldEditRequest request,
            DimensionTemplateCustomizerFieldEditPreview preview,
            DimensionTemplateCustomizerFieldApplyPlan applyPlan)
        {
            Index = index < 0 ? 0 : index;
            TargetKey = targetKey ?? string.Empty;
            DuplicateTarget = duplicateTarget;
            CanApply = canApply;
            HasWarning = hasWarning;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Request = request;
            Preview = preview;
            ApplyPlan = applyPlan;
        }

        public int Index { get; private set; }

        public string TargetKey { get; private set; }

        public bool DuplicateTarget { get; private set; }

        public bool CanApply { get; private set; }

        public bool HasWarning { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public DimensionTemplateCustomizerFieldEditRequest Request { get; private set; }

        public DimensionTemplateCustomizerFieldEditPreview Preview { get; private set; }

        public DimensionTemplateCustomizerFieldApplyPlan ApplyPlan { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldEditBatchPreview
    {
        public DimensionTemplateCustomizerFieldEditBatchPreview(
            DimensionTemplateCustomizerFieldEditBatchState state,
            string code,
            string message,
            bool canApply,
            bool requiresWarningConfirmation,
            int totalCount,
            int readyCount,
            int warningCount,
            int blockedCount,
            int duplicateTargetCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchEntry> entries)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanApply = canApply;
            RequiresWarningConfirmation = requiresWarningConfirmation;
            TotalCount = totalCount < 0 ? 0 : totalCount;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            DuplicateTargetCount = duplicateTargetCount < 0 ? 0 : duplicateTargetCount;
            Entries = entries ?? new List<DimensionTemplateCustomizerFieldEditBatchEntry>();
        }

        public DimensionTemplateCustomizerFieldEditBatchState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanApply { get; private set; }

        public bool RequiresWarningConfirmation { get; private set; }

        public int TotalCount { get; private set; }

        public int ReadyCount { get; private set; }

        public int WarningCount { get; private set; }

        public int BlockedCount { get; private set; }

        public int DuplicateTargetCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchEntry> Entries { get; private set; }
    }

    public enum DimensionTemplateCustomizerFieldEditBatchApplyPlanState
    {
        MissingPreview = 0,
        Empty = 1,
        Blocked = 2,
        NeedsWarningConfirmation = 3,
        Ready = 4
    }

    public sealed class DimensionTemplateCustomizerFieldEditBatchApplyStep
    {
        public DimensionTemplateCustomizerFieldEditBatchApplyStep(
            int order,
            int editIndex,
            string stepId,
            string label,
            string detail)
        {
            Order = order < 0 ? 0 : order;
            EditIndex = editIndex < 0 ? -1 : editIndex;
            StepId = stepId ?? string.Empty;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public int Order { get; private set; }

        public int EditIndex { get; private set; }

        public string StepId { get; private set; }

        public string Label { get; private set; }

        public string Detail { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldEditBatchApplyPlan
    {
        public DimensionTemplateCustomizerFieldEditBatchApplyPlan(
            DimensionTemplateCustomizerFieldEditBatchApplyPlanState state,
            string code,
            string message,
            bool canApply,
            bool requiresWarningConfirmation,
            int editCount,
            int stepCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchApplyStep> steps,
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanApply = canApply;
            RequiresWarningConfirmation = requiresWarningConfirmation;
            EditCount = editCount < 0 ? 0 : editCount;
            StepCount = stepCount < 0 ? 0 : stepCount;
            Steps = steps ?? new List<DimensionTemplateCustomizerFieldEditBatchApplyStep>();
            Preview = preview;
        }

        public DimensionTemplateCustomizerFieldEditBatchApplyPlanState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanApply { get; private set; }

        public bool RequiresWarningConfirmation { get; private set; }

        public int EditCount { get; private set; }

        public int StepCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchApplyStep> Steps { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchPreview Preview { get; private set; }
    }

    public static class DimensionTemplateCustomizerFieldEditBatchUtility
    {
        public static DimensionTemplateCustomizerFieldEditBatchPreview Preview(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            IReadOnlyList<DimensionTemplateCustomizerFieldEditRequest> edits =
                request == null
                    ? null
                    : request.Edits;
            return Preview(workspace, edits);
        }

        public static DimensionTemplateCustomizerFieldEditBatchPreview Preview(
            DimensionTemplateAuthoringWorkspace workspace,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditRequest> edits)
        {
            if (edits == null || edits.Count == 0)
            {
                return new DimensionTemplateCustomizerFieldEditBatchPreview(
                    DimensionTemplateCustomizerFieldEditBatchState.Empty,
                    "field-edit-batch-empty",
                    "No staged field edits are waiting for preview.",
                    false,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    new List<DimensionTemplateCustomizerFieldEditBatchEntry>());
            }

            Dictionary<string, int> targetCounts = CountTargets(edits);
            List<DimensionTemplateCustomizerFieldEditBatchEntry> entries =
                new List<DimensionTemplateCustomizerFieldEditBatchEntry>();
            int readyCount = 0;
            int warningCount = 0;
            int blockedCount = 0;
            int duplicateTargetCount = 0;

            for (int i = 0; i < edits.Count; i++)
            {
                DimensionTemplateCustomizerFieldEditRequest edit = edits[i];
                string targetKey = MakeTargetKey(edit);
                bool duplicateTarget = IsDuplicateTarget(targetCounts, targetKey);
                DimensionTemplateCustomizerFieldEditPreview preview =
                    DimensionTemplateCustomizerFieldEditPreviewUtility.Preview(workspace, edit);
                DimensionTemplateCustomizerFieldApplyPlan applyPlan =
                    DimensionTemplateCustomizerFieldApplyPlanUtility.Build(preview);
                bool hasWarning = HasWarning(preview, applyPlan);
                bool canApply = !duplicateTarget && applyPlan != null && applyPlan.CanApply;

                if (duplicateTarget)
                {
                    duplicateTargetCount++;
                    blockedCount++;
                }
                else if (canApply)
                {
                    readyCount++;
                }
                else if (hasWarning)
                {
                    warningCount++;
                }
                else
                {
                    blockedCount++;
                }

                entries.Add(new DimensionTemplateCustomizerFieldEditBatchEntry(
                    i,
                    targetKey,
                    duplicateTarget,
                    canApply,
                    hasWarning,
                    ResolveEntryCode(duplicateTarget, preview, applyPlan),
                    ResolveEntryMessage(duplicateTarget, preview, applyPlan),
                    edit,
                    preview,
                    applyPlan));
            }

            return CreatePreview(
                edits.Count,
                readyCount,
                warningCount,
                blockedCount,
                duplicateTargetCount,
                entries);
        }

        private static DimensionTemplateCustomizerFieldEditBatchPreview CreatePreview(
            int totalCount,
            int readyCount,
            int warningCount,
            int blockedCount,
            int duplicateTargetCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchEntry> entries)
        {
            if (duplicateTargetCount > 0)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditBatchState.Blocked,
                    "field-edit-batch-duplicate-targets",
                    "One or more staged edits target the same customizer field. Resolve duplicates before applying the batch.",
                    false,
                    false,
                    totalCount,
                    readyCount,
                    warningCount,
                    blockedCount,
                    duplicateTargetCount,
                    entries);
            }

            if (blockedCount > 0)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditBatchState.Blocked,
                    "field-edit-batch-blocked",
                    "One or more staged edits are blocked.",
                    false,
                    false,
                    totalCount,
                    readyCount,
                    warningCount,
                    blockedCount,
                    duplicateTargetCount,
                    entries);
            }

            if (warningCount > 0)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditBatchState.NeedsWarningConfirmation,
                    "field-edit-batch-warning-confirmation-needed",
                    "One or more staged edits need warning confirmation before the batch can be applied.",
                    false,
                    true,
                    totalCount,
                    readyCount,
                    warningCount,
                    blockedCount,
                    duplicateTargetCount,
                    entries);
            }

            return CreateResult(
                DimensionTemplateCustomizerFieldEditBatchState.Ready,
                "field-edit-batch-ready",
                "All staged field edits are ready for a future editor implementation to apply.",
                totalCount > 0,
                false,
                totalCount,
                readyCount,
                warningCount,
                blockedCount,
                duplicateTargetCount,
                entries);
        }

        private static DimensionTemplateCustomizerFieldEditBatchPreview CreateResult(
            DimensionTemplateCustomizerFieldEditBatchState state,
            string code,
            string message,
            bool canApply,
            bool requiresWarningConfirmation,
            int totalCount,
            int readyCount,
            int warningCount,
            int blockedCount,
            int duplicateTargetCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchEntry> entries)
        {
            return new DimensionTemplateCustomizerFieldEditBatchPreview(
                state,
                code,
                message,
                canApply,
                requiresWarningConfirmation,
                totalCount,
                readyCount,
                warningCount,
                blockedCount,
                duplicateTargetCount,
                entries);
        }

        private static Dictionary<string, int> CountTargets(
            IReadOnlyList<DimensionTemplateCustomizerFieldEditRequest> edits)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            if (edits == null)
            {
                return counts;
            }

            for (int i = 0; i < edits.Count; i++)
            {
                string key = MakeTargetKey(edits[i]);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (counts.ContainsKey(key))
                {
                    counts[key]++;
                }
                else
                {
                    counts.Add(key, 1);
                }
            }

            return counts;
        }

        private static bool IsDuplicateTarget(
            Dictionary<string, int> targetCounts,
            string targetKey)
        {
            if (targetCounts == null || string.IsNullOrEmpty(targetKey))
            {
                return false;
            }

            int count;
            return targetCounts.TryGetValue(targetKey, out count) && count > 1;
        }

        private static string MakeTargetKey(DimensionTemplateCustomizerFieldEditRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.FieldId))
            {
                return string.Empty;
            }

            DimensionTemplateCustomizerFocusRequest focus = request.FocusRequest;
            return (focus.SectionId ?? string.Empty)
                + "|"
                + (focus.BiomeId ?? string.Empty)
                + "|"
                + (focus.ZoneId ?? string.Empty)
                + "|"
                + (focus.RecordKind ?? string.Empty)
                + "|"
                + (focus.RecordId ?? string.Empty)
                + "|"
                + (focus.ActionId ?? string.Empty)
                + "|"
                + request.FieldId;
        }

        private static bool HasWarning(
            DimensionTemplateCustomizerFieldEditPreview preview,
            DimensionTemplateCustomizerFieldApplyPlan applyPlan)
        {
            if (preview != null && preview.HasWarning)
            {
                return true;
            }

            return applyPlan != null
                && applyPlan.State == DimensionTemplateCustomizerFieldApplyPlanState.NeedsWarningConfirmation;
        }

        private static string ResolveEntryCode(
            bool duplicateTarget,
            DimensionTemplateCustomizerFieldEditPreview preview,
            DimensionTemplateCustomizerFieldApplyPlan applyPlan)
        {
            if (duplicateTarget)
            {
                return "duplicate-target";
            }

            if (applyPlan != null && !string.IsNullOrEmpty(applyPlan.Code))
            {
                return applyPlan.Code;
            }

            return preview == null ? "preview-missing" : preview.Code;
        }

        private static string ResolveEntryMessage(
            bool duplicateTarget,
            DimensionTemplateCustomizerFieldEditPreview preview,
            DimensionTemplateCustomizerFieldApplyPlan applyPlan)
        {
            if (duplicateTarget)
            {
                return "Another staged edit already targets this field.";
            }

            if (applyPlan != null && !string.IsNullOrEmpty(applyPlan.Message))
            {
                return applyPlan.Message;
            }

            return preview == null ? "No preview is available." : preview.Message;
        }
    }

    public static class DimensionTemplateCustomizerFieldEditBatchApplyPlanUtility
    {
        public static DimensionTemplateCustomizerFieldEditBatchApplyPlan Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return Build(DimensionTemplateCustomizerFieldEditBatchUtility.Preview(
                workspace,
                request));
        }

        public static DimensionTemplateCustomizerFieldEditBatchApplyPlan Build(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            if (preview == null)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldEditBatchApplyPlanState.MissingPreview,
                    "field-edit-batch-preview-missing",
                    "No staged field edit batch preview was supplied.",
                    false,
                    false,
                    0,
                    new List<DimensionTemplateCustomizerFieldEditBatchApplyStep>(),
                    null);
            }

            if (preview.State == DimensionTemplateCustomizerFieldEditBatchState.Empty)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldEditBatchApplyPlanState.Empty,
                    preview.Code,
                    preview.Message,
                    false,
                    false,
                    0,
                    CreateBaseSteps(preview),
                    preview);
            }

            if (preview.State == DimensionTemplateCustomizerFieldEditBatchState.Blocked)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldEditBatchApplyPlanState.Blocked,
                    preview.Code,
                    preview.Message,
                    false,
                    false,
                    preview.TotalCount,
                    CreateBaseSteps(preview),
                    preview);
            }

            if (preview.State == DimensionTemplateCustomizerFieldEditBatchState.NeedsWarningConfirmation)
            {
                List<DimensionTemplateCustomizerFieldEditBatchApplyStep> warningSteps =
                    CreateBaseSteps(preview);
                AddStep(
                    warningSteps,
                    90,
                    -1,
                    "confirm-batch-warnings",
                    "Confirm batch warnings",
                    "A future editor implementation should require confirmation before applying warning-only staged edits.");
                return CreatePlan(
                    DimensionTemplateCustomizerFieldEditBatchApplyPlanState.NeedsWarningConfirmation,
                    preview.Code,
                    preview.Message,
                    false,
                    true,
                    preview.TotalCount,
                    warningSteps,
                    preview);
            }

            List<DimensionTemplateCustomizerFieldEditBatchApplyStep> steps =
                CreateBaseSteps(preview);
            AddPerEditSteps(steps, preview);
            AddStep(
                steps,
                9000,
                -1,
                "rebuild-customizer-state",
                "Rebuild customizer state",
                "Rebuild field sets, view models, previews, canvas summaries, and manifest export preview once the editor apply phase finishes.");
            AddStep(
                steps,
                9010,
                -1,
                "revalidate-authoring-graph",
                "Revalidate authoring graph",
                "Run readiness checks after all staged edits are applied.");
            AddStep(
                steps,
                9020,
                -1,
                "mark-touched-assets-dirty",
                "Mark touched assets dirty",
                "Editor-only apply code may mark touched assets dirty; this source-side plan does not write files.");

            return CreatePlan(
                DimensionTemplateCustomizerFieldEditBatchApplyPlanState.Ready,
                "field-edit-batch-apply-ready",
                "The staged edit batch is ready for a future editor implementation to apply as one transaction.",
                preview.TotalCount > 0,
                false,
                preview.TotalCount,
                steps,
                preview);
        }

        private static List<DimensionTemplateCustomizerFieldEditBatchApplyStep> CreateBaseSteps(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            List<DimensionTemplateCustomizerFieldEditBatchApplyStep> steps =
                new List<DimensionTemplateCustomizerFieldEditBatchApplyStep>();
            AddStep(
                steps,
                0,
                -1,
                "resolve-batch",
                "Resolve staged edit batch",
                "Read the staged edit requests and their current target fields.");
            AddStep(
                steps,
                10,
                -1,
                "validate-batch",
                "Validate staged edit batch",
                preview == null
                    ? "No preview is available."
                    : preview.Message);
            AddStep(
                steps,
                20,
                -1,
                "check-duplicate-targets",
                "Check duplicate targets",
                preview == null
                    ? "No target list is available."
                    : "Duplicate target count: " + preview.DuplicateTargetCount);
            return steps;
        }

        private static void AddPerEditSteps(
            List<DimensionTemplateCustomizerFieldEditBatchApplyStep> steps,
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            if (steps == null || preview == null || preview.Entries == null)
            {
                return;
            }

            for (int i = 0; i < preview.Entries.Count; i++)
            {
                DimensionTemplateCustomizerFieldEditBatchEntry entry = preview.Entries[i];
                if (entry == null || !entry.CanApply)
                {
                    continue;
                }

                AddStep(
                    steps,
                    1000 + (entry.Index * 10),
                    entry.Index,
                    "apply-edit-" + entry.Index,
                    "Apply staged field edit",
                    "Apply normalized value for target " + entry.TargetKey + ".");
            }
        }

        private static void AddStep(
            List<DimensionTemplateCustomizerFieldEditBatchApplyStep> steps,
            int order,
            int editIndex,
            string stepId,
            string label,
            string detail)
        {
            if (steps == null)
            {
                return;
            }

            steps.Add(new DimensionTemplateCustomizerFieldEditBatchApplyStep(
                order,
                editIndex,
                stepId,
                label,
                detail));
        }

        private static DimensionTemplateCustomizerFieldEditBatchApplyPlan CreatePlan(
            DimensionTemplateCustomizerFieldEditBatchApplyPlanState state,
            string code,
            string message,
            bool canApply,
            bool requiresWarningConfirmation,
            int editCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchApplyStep> steps,
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            return new DimensionTemplateCustomizerFieldEditBatchApplyPlan(
                state,
                code,
                message,
                canApply,
                requiresWarningConfirmation,
                editCount,
                steps == null ? 0 : steps.Count,
                steps,
                preview);
        }
    }
}
