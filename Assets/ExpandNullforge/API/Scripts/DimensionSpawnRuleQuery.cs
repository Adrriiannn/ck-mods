using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionSpawnRuleQuery
    {
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly bool HasLocalPosition;
        public readonly float2 LocalPosition;
        public readonly DimensionSpawnSubjectKind SubjectKind;
        public readonly bool EnabledOnly;

        public DimensionSpawnRuleQuery(
            string dimensionId,
            string zoneId,
            bool hasLocalPosition,
            float2 localPosition,
            DimensionSpawnSubjectKind subjectKind,
            bool enabledOnly)
        {
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            HasLocalPosition = hasLocalPosition;
            LocalPosition = localPosition;
            SubjectKind = subjectKind;
            EnabledOnly = enabledOnly;
        }
    }
}
