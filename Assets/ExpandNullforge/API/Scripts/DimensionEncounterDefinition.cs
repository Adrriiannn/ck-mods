namespace ExpandNullforge.Api
{
    public readonly struct DimensionEncounterDefinition
    {
        public readonly string EncounterId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string ZoneId;
        public readonly string SceneId;
        public readonly string SpawnRuleId;
        public readonly string MarkerId;
        public readonly string DefeatFlagId;
        public readonly DimensionEncounterKind Kind;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionEncounterDefinition(
            string encounterId,
            string displayName,
            string dimensionId,
            string zoneId,
            string sceneId,
            string spawnRuleId,
            string markerId,
            string defeatFlagId,
            DimensionEncounterKind kind,
            int priority,
            bool enabled)
        {
            EncounterId = encounterId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            SceneId = sceneId ?? string.Empty;
            SpawnRuleId = spawnRuleId ?? string.Empty;
            MarkerId = markerId ?? string.Empty;
            DefeatFlagId = defeatFlagId ?? string.Empty;
            Kind = kind;
            Priority = priority;
            Enabled = enabled;
        }
    }
}
