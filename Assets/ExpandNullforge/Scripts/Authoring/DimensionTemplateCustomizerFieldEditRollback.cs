using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldEditRollbackPlanState
    {
        MissingApplyPlan = 0,
        Empty = 1,
        NotApplicable = 2,
        Ready = 3
    }

    public sealed class DimensionTemplateCustomizerFieldEditRollbackStep
    {
        public DimensionTemplateCustomizerFieldEditRollbackStep(
            int order,
            int editIndex,
            string targetKey,
            string fieldId,
            string oldValue,
            string newValue,
            string label,
            string detail)
        {
            Order = order < 0 ? 0 : order;
            EditIndex = editIndex < 0 ? -1 : editIndex;
            TargetKey = targetKey ?? string.Empty;
            FieldId = fieldId ?? string.Empty;
            OldValue = oldValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public int Order { get; private set; }

        public int EditIndex { get; private set; }

        public string TargetKey { get; private set; }

        public string FieldId { get; private set; }

        public string OldValue { get; private set; }

        public string NewValue { get; private set; }

        public string Label { get; private set; }

        public string Detail { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldEditRollbackPlan
    {
        public DimensionTemplateCustomizerFieldEditRollbackPlan(
            DimensionTemplateCustomizerFieldEditRollbackPlanState state,
            string code,
            string message,
            bool canRollback,
            int editCount,
            int stepCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditRollbackStep> steps,
            DimensionTemplateCustomizerFieldEditBatchApplyPlan applyPlan)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanRollback = canRollback;
            EditCount = editCount < 0 ? 0 : editCount;
            StepCount = stepCount < 0 ? 0 : stepCount;
            Steps = steps ?? new List<DimensionTemplateCustomizerFieldEditRollbackStep>();
            ApplyPlan = applyPlan;
        }

        public DimensionTemplateCustomizerFieldEditRollbackPlanState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanRollback { get; private set; }

        public int EditCount { get; private set; }

        public int StepCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldEditRollbackStep> Steps { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchApplyPlan ApplyPlan { get; private set; }
    }

    public static class DimensionTemplateCustomizerFieldEditRollbackPlanUtility
    {
        public static DimensionTemplateCustomizerFieldEditRollbackPlan Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return Build(DimensionTemplateCustomizerFieldEditBatchApplyPlanUtility.Build(
                workspace,
                request));
        }

        public static DimensionTemplateCustomizerFieldEditRollbackPlan Build(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            return Build(DimensionTemplateCustomizerFieldEditBatchApplyPlanUtility.Build(preview));
        }

        public static DimensionTemplateCustomizerFieldEditRollbackPlan Build(
            DimensionTemplateCustomizerFieldEditBatchApplyPlan applyPlan)
        {
            if (applyPlan == null)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldEditRollbackPlanState.MissingApplyPlan,
                    "field-edit-rollback-apply-plan-missing",
                    "No staged field edit batch apply plan was supplied.",
                    false,
                    0,
                    new List<DimensionTemplateCustomizerFieldEditRollbackStep>(),
                    null);
            }

            if (applyPlan.State == DimensionTemplateCustomizerFieldEditBatchApplyPlanState.Empty)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldEditRollbackPlanState.Empty,
                    applyPlan.Code,
                    "No staged field edits are available to roll back.",
                    false,
                    0,
                    new List<DimensionTemplateCustomizerFieldEditRollbackStep>(),
                    applyPlan);
            }

            if (applyPlan.State != DimensionTemplateCustomizerFieldEditBatchApplyPlanState.Ready)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldEditRollbackPlanState.NotApplicable,
                    applyPlan.Code,
                    "A rollback plan is only produced for batches that are ready to apply.",
                    false,
                    applyPlan.EditCount,
                    new List<DimensionTemplateCustomizerFieldEditRollbackStep>(),
                    applyPlan);
            }

            List<DimensionTemplateCustomizerFieldEditRollbackStep> steps =
                CreateRollbackSteps(applyPlan);
            return CreatePlan(
                steps.Count == 0
                    ? DimensionTemplateCustomizerFieldEditRollbackPlanState.Empty
                    : DimensionTemplateCustomizerFieldEditRollbackPlanState.Ready,
                steps.Count == 0
                    ? "field-edit-rollback-empty"
                    : "field-edit-rollback-ready",
                steps.Count == 0
                    ? "The ready batch does not contain reversible field edits."
                    : "The ready batch has a source-side rollback plan for future editor transaction support.",
                steps.Count > 0,
                applyPlan.EditCount,
                steps,
                applyPlan);
        }

        private static List<DimensionTemplateCustomizerFieldEditRollbackStep> CreateRollbackSteps(
            DimensionTemplateCustomizerFieldEditBatchApplyPlan applyPlan)
        {
            List<DimensionTemplateCustomizerFieldEditRollbackStep> steps =
                new List<DimensionTemplateCustomizerFieldEditRollbackStep>();
            if (applyPlan == null
                || applyPlan.Preview == null
                || applyPlan.Preview.Entries == null)
            {
                return steps;
            }

            int order = 1000;
            for (int i = applyPlan.Preview.Entries.Count - 1; i >= 0; i--)
            {
                DimensionTemplateCustomizerFieldEditBatchEntry entry =
                    applyPlan.Preview.Entries[i];
                if (entry == null || !entry.CanApply || entry.ApplyPlan == null)
                {
                    continue;
                }

                DimensionTemplateCustomizerFieldApplyPlan fieldPlan = entry.ApplyPlan;
                steps.Add(new DimensionTemplateCustomizerFieldEditRollbackStep(
                    order,
                    entry.Index,
                    entry.TargetKey,
                    fieldPlan.FieldId,
                    fieldPlan.OldValue,
                    fieldPlan.NewValue,
                    "Restore previous field value",
                    "If a future editor transaction applies this batch and then fails, restore "
                        + fieldPlan.FieldId
                        + " from '"
                        + fieldPlan.NewValue
                        + "' back to '"
                        + fieldPlan.OldValue
                        + "'."));
                order += 10;
            }

            return steps;
        }

        private static DimensionTemplateCustomizerFieldEditRollbackPlan CreatePlan(
            DimensionTemplateCustomizerFieldEditRollbackPlanState state,
            string code,
            string message,
            bool canRollback,
            int editCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditRollbackStep> steps,
            DimensionTemplateCustomizerFieldEditBatchApplyPlan applyPlan)
        {
            return new DimensionTemplateCustomizerFieldEditRollbackPlan(
                state,
                code,
                message,
                canRollback,
                editCount,
                steps == null ? 0 : steps.Count,
                steps,
                applyPlan);
        }
    }
}
