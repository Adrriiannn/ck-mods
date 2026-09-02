using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateCustomizerFieldApplyPlan
    {
        public DimensionTemplateCustomizerFieldApplyPlan(
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
            int stepCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldApplyStep> steps,
            DimensionTemplateCustomizerFieldEditPreview preview)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanApply = canApply;
            SectionId = sectionId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            FieldId = fieldId ?? string.Empty;
            OldValue = oldValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            StepCount = stepCount < 0 ? 0 : stepCount;
            Steps = steps ?? new List<DimensionTemplateCustomizerFieldApplyStep>();
            Preview = preview;
        }

        public DimensionTemplateCustomizerFieldApplyPlanState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanApply { get; private set; }

        public string SectionId { get; private set; }

        public string RecordKind { get; private set; }

        public string RecordId { get; private set; }

        public string FieldId { get; private set; }

        public string OldValue { get; private set; }

        public string NewValue { get; private set; }

        public int StepCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldApplyStep> Steps { get; private set; }

        public DimensionTemplateCustomizerFieldEditPreview Preview { get; private set; }
    }
}
