using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldEditPreviewState
    {
        MissingWorkspace = 0,
        MissingFieldSet = 1,
        MissingField = 2,
        ReadOnly = 3,
        RequiresEditorImplementation = 4,
        RuntimeOnly = 5,
        MissingValue = 6,
        InvalidFormat = 7,
        Warning = 8,
        Ready = 9
    }
}
