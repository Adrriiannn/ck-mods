namespace ExpandNullforge.Api
{
    /// <summary>
    /// Public dimension service consumed by dimension-aware mods.
    /// Server-side systems should use this to resolve gameplay coordinates.
    /// Client-side systems may use this for UI/map presentation, but must not rely on it for authoritative gameplay.
    /// </summary>
    public interface IDimensionService :
        IDimensionContentPackService,
        IDimensionContentOwnershipService,
        IDimensionContentReadinessService,
        IDimensionStarterService,
        IDimensionStarterReadinessService,
        IDimensionContentManifestService,
        IDimensionAssetReferenceService,
        IDimensionBiomeCatalogService,
        IDimensionGenerationTableService,
        IDimensionRegistryService,
        IDimensionSlotAllocationService,
        IDimensionCoordinateService,
        IDimensionCompatibilityService,
        IDimensionPlayerService,
        IDimensionPlayerVisitService,
        IDimensionReturnTargetService,
        IDimensionEntityService,
        IDimensionRespawnService,
        IDimensionLandingValidationService,
        IDimensionAccessService,
        IDimensionPermissionService,
        IDimensionAnchorService,
        IDimensionZoneCatalogService,
        IDimensionEnvironmentProfileService,
        IDimensionSceneService,
        IDimensionSceneTemplateService,
        IDimensionEncounterService,
        IDimensionResourceNodeService,
        IDimensionSpawnRuleService,
        IDimensionWorldEventService,
        IDimensionProgressService,
        IDimensionTravelPreviewService,
        IDimensionTravelService,
        IDimensionTravelPolicyService,
        IDimensionTravelLoopPreflightService,
        IDimensionPortalPresentationService,
        IDimensionPortalItemPresentationService,
        IDimensionTravelRequirementService,
        IDimensionGenerationService,
        IDimensionGenerationPassService,
        IDimensionGenerationPlanningService,
        IDimensionGenerationSeedService,
        IDimensionLoadingService,
        IDimensionMapLayerService,
        IDimensionMapService,
        IDimensionMapPresentationService,
        IDimensionPersistenceService,
        IDimensionDiagnosticsService
    {
    }
}
