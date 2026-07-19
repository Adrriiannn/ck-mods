using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionSceneTemplateDefinition
    {
        public readonly string TemplateId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly string Kind;
        public readonly string ProviderId;
        public readonly int2 FootprintSize;
        public readonly int Weight;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionSceneTemplateDefinition(
            string templateId,
            string displayName,
            string dimensionId,
            string zoneId,
            string kind,
            string providerId,
            int2 footprintSize,
            int weight,
            int priority,
            bool enabled)
        {
            TemplateId = templateId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            Kind = kind ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            FootprintSize = footprintSize;
            Weight = weight;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
