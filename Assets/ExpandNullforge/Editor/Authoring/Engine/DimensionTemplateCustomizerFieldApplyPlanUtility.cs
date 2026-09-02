using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public static class DimensionTemplateCustomizerFieldApplyPlanUtility
    {
        public static DimensionTemplateCustomizerFieldApplyPlan Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            DimensionTemplateCustomizerFieldEditPreview preview =
                DimensionTemplateCustomizerFieldEditPreviewUtility.Preview(workspace, request);
            return Build(preview);
        }

        public static DimensionTemplateCustomizerFieldApplyPlan Build(
            DimensionTemplateCustomizerFieldEditPreview preview)
        {
            if (preview == null)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldApplyPlanState.MissingPreview,
                    "preview-missing",
                    "No field edit preview was supplied.",
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    new List<DimensionTemplateCustomizerFieldApplyStep>(),
                    null);
            }

            string sectionId = preview.FieldSet == null ? string.Empty : preview.FieldSet.SectionId;
            string recordKind = preview.FieldSet == null ? string.Empty : preview.FieldSet.RecordKind;
            string recordId = preview.FieldSet == null ? string.Empty : preview.FieldSet.RecordId;
            string fieldId = preview.Field == null ? string.Empty : preview.Field.FieldId;
            string oldValue = preview.Field == null ? string.Empty : preview.Field.Value;
            string newValue = preview.NormalizedValue;

            if (preview.State == DimensionTemplateCustomizerFieldEditPreviewState.Warning)
            {
                List<DimensionTemplateCustomizerFieldApplyStep> warningSteps =
                    CreateBaseSteps(preview);
                AddStep(
                    warningSteps,
                    90,
                    "confirm-warning",
                    "Confirm warning",
                    preview.Warning);
                return CreatePlan(
                    DimensionTemplateCustomizerFieldApplyPlanState.NeedsWarningConfirmation,
                    "warning-confirmation-needed",
                    "The edit is valid but needs confirmation before it can be applied.",
                    false,
                    sectionId,
                    recordKind,
                    recordId,
                    fieldId,
                    oldValue,
                    newValue,
                    warningSteps,
                    preview);
            }

            if (!preview.CanApply)
            {
                return CreatePlan(
                    DimensionTemplateCustomizerFieldApplyPlanState.Blocked,
                    preview.Code,
                    preview.Message,
                    false,
                    sectionId,
                    recordKind,
                    recordId,
                    fieldId,
                    oldValue,
                    newValue,
                    CreateBaseSteps(preview),
                    preview);
            }

            List<DimensionTemplateCustomizerFieldApplyStep> steps =
                CreateBaseSteps(preview);
            AddStep(
                steps,
                30,
                "assign-field",
                "Assign field",
                "A future editor implementation may now write the normalized value to the target asset field.");
            AddStep(
                steps,
                40,
                "rebuild-preview",
                "Rebuild preview",
                "Rebuild customizer view models, field sets, canvas summaries, and manifest export preview.");
            AddStep(
                steps,
                50,
                "revalidate-authoring",
                "Revalidate authoring",
                "Re-run authoring readiness checks and surface any new blockers or warnings.");
            AddStep(
                steps,
                60,
                "mark-assets-dirty",
                "Mark assets dirty",
                "Editor-only apply code may mark touched assets dirty; this framework plan does not write files.");

            return CreatePlan(
                DimensionTemplateCustomizerFieldApplyPlanState.Ready,
                "field-apply-ready",
                "The edit is ready for a future editor implementation to apply.",
                true,
                sectionId,
                recordKind,
                recordId,
                fieldId,
                oldValue,
                newValue,
                steps,
                preview);
        }

        private static List<DimensionTemplateCustomizerFieldApplyStep> CreateBaseSteps(
            DimensionTemplateCustomizerFieldEditPreview preview)
        {
            List<DimensionTemplateCustomizerFieldApplyStep> steps =
                new List<DimensionTemplateCustomizerFieldApplyStep>();
            AddStep(
                steps,
                0,
                "resolve-field",
                "Resolve field",
                "Find the selected customizer field on the active authoring target.");
            AddStep(
                steps,
                10,
                "validate-value",
                "Validate value",
                preview == null
                    ? "No preview is available."
                    : preview.Message);
            AddStep(
                steps,
                20,
                "normalize-value",
                "Normalize value",
                preview == null
                    ? "No normalized value is available."
                    : "Normalized value: " + preview.NormalizedValue);
            return steps;
        }

        private static void AddStep(
            List<DimensionTemplateCustomizerFieldApplyStep> steps,
            int order,
            string stepId,
            string label,
            string detail)
        {
            if (steps == null)
            {
                return;
            }

            steps.Add(new DimensionTemplateCustomizerFieldApplyStep(
                order,
                stepId,
                label,
                detail));
        }

        private static DimensionTemplateCustomizerFieldApplyPlan CreatePlan(
            DimensionTemplateCustomizerFieldApplyPlanState state,
            string code,
            string message,
            bool canApply,
            string sectionId,
            string recordKind,
            string recordId,
            string fieldId,
            string oldValue,
            string newValue,
            IReadOnlyList<DimensionTemplateCustomizerFieldApplyStep> steps,
            DimensionTemplateCustomizerFieldEditPreview preview)
        {
            return new DimensionTemplateCustomizerFieldApplyPlan(
                state,
                code,
                message,
                canApply,
                sectionId,
                recordKind,
                recordId,
                fieldId,
                oldValue,
                newValue,
                steps == null ? 0 : steps.Count,
                steps,
                preview);
        }
    }
}
