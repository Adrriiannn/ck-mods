using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldApplyPlanState
    {
        MissingPreview = 0,
        Blocked = 1,
        NeedsWarningConfirmation = 2,
        Ready = 3
    }
}
