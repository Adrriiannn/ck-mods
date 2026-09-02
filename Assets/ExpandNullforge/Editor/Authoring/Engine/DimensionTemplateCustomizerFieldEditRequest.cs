using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateCustomizerFieldEditRequest
    {
        public DimensionTemplateCustomizerFieldEditRequest(
            DimensionTemplateCustomizerFocusRequest focusRequest,
            string fieldId,
            string newValue,
            bool acceptWarnings)
        {
            FocusRequest = focusRequest;
            FieldId = fieldId ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            AcceptWarnings = acceptWarnings;
        }

        public DimensionTemplateCustomizerFocusRequest FocusRequest { get; private set; }

        public string FieldId { get; private set; }

        public string NewValue { get; private set; }

        public bool AcceptWarnings { get; private set; }
    }
}
