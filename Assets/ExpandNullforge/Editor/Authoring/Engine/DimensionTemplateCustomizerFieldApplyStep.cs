using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateCustomizerFieldApplyStep
    {
        public DimensionTemplateCustomizerFieldApplyStep(
            int order,
            string stepId,
            string label,
            string detail)
        {
            Order = order < 0 ? 0 : order;
            StepId = stepId ?? string.Empty;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public int Order { get; private set; }

        public string StepId { get; private set; }

        public string Label { get; private set; }

        public string Detail { get; private set; }
    }
}
