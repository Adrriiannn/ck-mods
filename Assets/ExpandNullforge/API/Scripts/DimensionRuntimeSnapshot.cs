namespace ExpandNullforge.Api
{
    public readonly struct DimensionRuntimeSnapshot
    {
        public readonly int DimensionCount;
        public readonly int ContentPackCount;
        public readonly int ContentOwnershipBindingCount;
        public readonly int AssetReferenceCount;
        public readonly int BiomeCount;
        public readonly int GenerationTableCount;
        public readonly int GenerationTableEntryCount;
        public readonly int PlayerVisitCount;
        public readonly int PortalCount;
        public readonly int PortalPresentationCount;
        public readonly int TravelRequirementCount;
        public readonly int TravelRequirementEvaluatorCount;
        public readonly int MapLayerCount;
        public readonly int MarkerCount;
        public readonly int AnchorCount;
        public readonly int SceneCount;
        public readonly int SceneTemplateCount;
        public readonly int EncounterCount;
        public readonly int ResourceNodeCount;
        public readonly int SpawnRuleCount;
        public readonly int WorldEventCount;
        public readonly int ProgressFlagCount;
        public readonly int GeneratedAreaCount;
        public readonly int GenerationReservationCount;
        public readonly int GenerationProviderCount;
        public readonly int GenerationPassCount;
        public readonly int ZoneDefinitionCount;
        public readonly int EnvironmentProfileCount;
        public readonly int ZoneProviderCount;
        public readonly int AccessProviderCount;
        public readonly int RuntimeGenerationJobCount;
        public readonly int ActiveLoadTicketCount;
        public readonly int RuntimeLoadAnchorCount;
        public readonly int RuntimeSimulationRegionCount;
        public readonly int ObservedParentSubMapCount;
        public readonly bool ServerWorldAttached;
        public readonly bool ClientWorldAttached;
        public readonly bool RegistryLoaded;
        public readonly ulong RegistryRevision;
        public readonly string RegistryWorldKey;

        public DimensionRuntimeSnapshot(
            int dimensionCount,
            int contentPackCount,
            int contentOwnershipBindingCount,
            int assetReferenceCount,
            int biomeCount,
            int generationTableCount,
            int generationTableEntryCount,
            int playerVisitCount,
            int portalCount,
            int portalPresentationCount,
            int travelRequirementCount,
            int travelRequirementEvaluatorCount,
            int mapLayerCount,
            int markerCount,
            int anchorCount,
            int sceneCount,
            int sceneTemplateCount,
            int encounterCount,
            int resourceNodeCount,
            int spawnRuleCount,
            int worldEventCount,
            int progressFlagCount,
            int generatedAreaCount,
            int generationReservationCount,
            int generationProviderCount,
            int generationPassCount,
            int zoneDefinitionCount,
            int environmentProfileCount,
            int zoneProviderCount,
            int accessProviderCount,
            int runtimeGenerationJobCount,
            int activeLoadTicketCount,
            int runtimeLoadAnchorCount,
            int runtimeSimulationRegionCount,
            int observedParentSubMapCount,
            bool serverWorldAttached,
            bool clientWorldAttached,
            bool registryLoaded,
            ulong registryRevision,
            string registryWorldKey)
        {
            DimensionCount = dimensionCount;
            ContentPackCount = contentPackCount;
            ContentOwnershipBindingCount = contentOwnershipBindingCount;
            AssetReferenceCount = assetReferenceCount;
            BiomeCount = biomeCount;
            GenerationTableCount = generationTableCount;
            GenerationTableEntryCount = generationTableEntryCount;
            PlayerVisitCount = playerVisitCount;
            PortalCount = portalCount;
            PortalPresentationCount = portalPresentationCount;
            TravelRequirementCount = travelRequirementCount;
            TravelRequirementEvaluatorCount = travelRequirementEvaluatorCount;
            MapLayerCount = mapLayerCount;
            MarkerCount = markerCount;
            AnchorCount = anchorCount;
            SceneCount = sceneCount;
            SceneTemplateCount = sceneTemplateCount;
            EncounterCount = encounterCount;
            ResourceNodeCount = resourceNodeCount;
            SpawnRuleCount = spawnRuleCount;
            WorldEventCount = worldEventCount;
            ProgressFlagCount = progressFlagCount;
            GeneratedAreaCount = generatedAreaCount;
            GenerationReservationCount = generationReservationCount;
            GenerationProviderCount = generationProviderCount;
            GenerationPassCount = generationPassCount;
            ZoneDefinitionCount = zoneDefinitionCount;
            EnvironmentProfileCount = environmentProfileCount;
            ZoneProviderCount = zoneProviderCount;
            AccessProviderCount = accessProviderCount;
            RuntimeGenerationJobCount = runtimeGenerationJobCount;
            ActiveLoadTicketCount = activeLoadTicketCount;
            RuntimeLoadAnchorCount = runtimeLoadAnchorCount;
            RuntimeSimulationRegionCount = runtimeSimulationRegionCount;
            ObservedParentSubMapCount = observedParentSubMapCount;
            ServerWorldAttached = serverWorldAttached;
            ClientWorldAttached = clientWorldAttached;
            RegistryLoaded = registryLoaded;
            RegistryRevision = registryRevision;
            RegistryWorldKey = registryWorldKey ?? string.Empty;
        }
    }
}
