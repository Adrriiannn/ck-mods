using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentManifest
    {
        public readonly IReadOnlyList<DimensionContentPackDefinition> ContentPacks;
        public readonly IReadOnlyList<DimensionDefinition> Dimensions;
        public readonly IReadOnlyList<DimensionZoneDefinition> Zones;
        public readonly IReadOnlyList<DimensionMapLayerDefinition> MapLayers;
        public readonly IReadOnlyList<DimensionMapMarker> MapMarkers;
        public readonly IReadOnlyList<DimensionAnchorDefinition> Anchors;
        public readonly IReadOnlyList<DimensionPortalDefinition> Portals;
        public readonly IReadOnlyList<DimensionPortalPresentationDefinition> PortalPresentations;
        public readonly IReadOnlyList<DimensionTravelRequirementDefinition> TravelRequirements;
        public readonly IReadOnlyList<DimensionStarterDefinition> Starters;
        public readonly IReadOnlyList<DimensionSceneTemplateDefinition> SceneTemplates;
        public readonly IReadOnlyList<DimensionSceneDefinition> Scenes;
        public readonly IReadOnlyList<DimensionSpawnRule> SpawnRules;
        public readonly IReadOnlyList<DimensionEncounterDefinition> Encounters;
        public readonly IReadOnlyList<DimensionGenerationPassDefinition> GenerationPasses;
        public readonly IReadOnlyList<DimensionResourceNodeDefinition> ResourceNodes;
        public readonly IReadOnlyList<DimensionProgressFlag> ProgressFlags;
        public readonly IReadOnlyList<DimensionWorldEventDefinition> WorldEvents;
        public readonly IReadOnlyList<DimensionContentOwnershipBinding> OwnershipBindings;
        public readonly IReadOnlyList<DimensionAssetReferenceDefinition> AssetReferences;
        public readonly IReadOnlyList<DimensionEnvironmentProfile> EnvironmentProfiles;
        public readonly IReadOnlyList<DimensionBiomeDefinition> Biomes;
        public readonly IReadOnlyList<DimensionGenerationTableDefinition> GenerationTables;
        public readonly IReadOnlyList<DimensionGenerationTableEntryDefinition> GenerationTableEntries;

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  new List<DimensionDefinition>(),
                  new List<DimensionZoneDefinition>(),
                  new List<DimensionMapLayerDefinition>(),
                  new List<DimensionMapMarker>(),
                  new List<DimensionAnchorDefinition>(),
                  new List<DimensionPortalDefinition>(),
                  new List<DimensionPortalPresentationDefinition>(),
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  new List<DimensionZoneDefinition>(),
                  new List<DimensionMapLayerDefinition>(),
                  new List<DimensionMapMarker>(),
                  new List<DimensionAnchorDefinition>(),
                  new List<DimensionPortalDefinition>(),
                  new List<DimensionPortalPresentationDefinition>(),
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  new List<DimensionMapLayerDefinition>(),
                  new List<DimensionMapMarker>(),
                  new List<DimensionAnchorDefinition>(),
                  new List<DimensionPortalDefinition>(),
                  new List<DimensionPortalPresentationDefinition>(),
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  new List<DimensionMapMarker>(),
                  new List<DimensionAnchorDefinition>(),
                  new List<DimensionPortalDefinition>(),
                  new List<DimensionPortalPresentationDefinition>(),
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  new List<DimensionAnchorDefinition>(),
                  new List<DimensionPortalDefinition>(),
                  new List<DimensionPortalPresentationDefinition>(),
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  new List<DimensionPortalDefinition>(),
                  new List<DimensionPortalPresentationDefinition>(),
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  new List<DimensionPortalPresentationDefinition>(),
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  new List<DimensionTravelRequirementDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  travelRequirements,
                  new List<DimensionSceneTemplateDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionSceneTemplateDefinition> sceneTemplates,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  travelRequirements,
                  sceneTemplates,
                  new List<DimensionSceneDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionSceneTemplateDefinition> sceneTemplates,
            IReadOnlyList<DimensionSceneDefinition> scenes,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  travelRequirements,
                  sceneTemplates,
                  scenes,
                  new List<DimensionSpawnRule>(),
                  new List<DimensionEncounterDefinition>(),
                  new List<DimensionGenerationPassDefinition>(),
                  new List<DimensionResourceNodeDefinition>(),
                  new List<DimensionProgressFlag>(),
                  new List<DimensionWorldEventDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionSceneTemplateDefinition> sceneTemplates,
            IReadOnlyList<DimensionSceneDefinition> scenes,
            IReadOnlyList<DimensionEncounterDefinition> encounters,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  travelRequirements,
                  sceneTemplates,
                  scenes,
                  new List<DimensionSpawnRule>(),
                  encounters,
                  new List<DimensionGenerationPassDefinition>(),
                  new List<DimensionResourceNodeDefinition>(),
                  new List<DimensionProgressFlag>(),
                  new List<DimensionWorldEventDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionSceneTemplateDefinition> sceneTemplates,
            IReadOnlyList<DimensionSceneDefinition> scenes,
            IReadOnlyList<DimensionEncounterDefinition> encounters,
            IReadOnlyList<DimensionResourceNodeDefinition> resourceNodes,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  travelRequirements,
                  sceneTemplates,
                  scenes,
                  new List<DimensionSpawnRule>(),
                  encounters,
                  new List<DimensionGenerationPassDefinition>(),
                  resourceNodes,
                  new List<DimensionProgressFlag>(),
                  new List<DimensionWorldEventDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionSceneTemplateDefinition> sceneTemplates,
            IReadOnlyList<DimensionSceneDefinition> scenes,
            IReadOnlyList<DimensionSpawnRule> spawnRules,
            IReadOnlyList<DimensionEncounterDefinition> encounters,
            IReadOnlyList<DimensionResourceNodeDefinition> resourceNodes,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  travelRequirements,
                  sceneTemplates,
                  scenes,
                  spawnRules,
                  encounters,
                  new List<DimensionGenerationPassDefinition>(),
                  resourceNodes,
                  new List<DimensionProgressFlag>(),
                  new List<DimensionWorldEventDefinition>(),
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionSceneTemplateDefinition> sceneTemplates,
            IReadOnlyList<DimensionSceneDefinition> scenes,
            IReadOnlyList<DimensionSpawnRule> spawnRules,
            IReadOnlyList<DimensionEncounterDefinition> encounters,
            IReadOnlyList<DimensionResourceNodeDefinition> resourceNodes,
            IReadOnlyList<DimensionProgressFlag> progressFlags,
            IReadOnlyList<DimensionWorldEventDefinition> worldEvents,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
            : this(
                  contentPacks,
                  dimensions,
                  zones,
                  mapLayers,
                  mapMarkers,
                  anchors,
                  portals,
                  portalPresentations,
                  travelRequirements,
                  sceneTemplates,
                  scenes,
                  spawnRules,
                  encounters,
                  new List<DimensionGenerationPassDefinition>(),
                  resourceNodes,
                  progressFlags,
                  worldEvents,
                  ownershipBindings,
                  assetReferences,
                  environmentProfiles,
                  biomes,
                  generationTables,
                  generationTableEntries)
        {
        }

        public DimensionContentManifest(
            IReadOnlyList<DimensionContentPackDefinition> contentPacks,
            IReadOnlyList<DimensionDefinition> dimensions,
            IReadOnlyList<DimensionZoneDefinition> zones,
            IReadOnlyList<DimensionMapLayerDefinition> mapLayers,
            IReadOnlyList<DimensionMapMarker> mapMarkers,
            IReadOnlyList<DimensionAnchorDefinition> anchors,
            IReadOnlyList<DimensionPortalDefinition> portals,
            IReadOnlyList<DimensionPortalPresentationDefinition> portalPresentations,
            IReadOnlyList<DimensionTravelRequirementDefinition> travelRequirements,
            IReadOnlyList<DimensionSceneTemplateDefinition> sceneTemplates,
            IReadOnlyList<DimensionSceneDefinition> scenes,
            IReadOnlyList<DimensionSpawnRule> spawnRules,
            IReadOnlyList<DimensionEncounterDefinition> encounters,
            IReadOnlyList<DimensionGenerationPassDefinition> generationPasses,
            IReadOnlyList<DimensionResourceNodeDefinition> resourceNodes,
            IReadOnlyList<DimensionProgressFlag> progressFlags,
            IReadOnlyList<DimensionWorldEventDefinition> worldEvents,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionEnvironmentProfile> environmentProfiles,
            IReadOnlyList<DimensionBiomeDefinition> biomes,
            IReadOnlyList<DimensionGenerationTableDefinition> generationTables,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> generationTableEntries)
        {
            ContentPacks = contentPacks ?? new List<DimensionContentPackDefinition>();
            Dimensions = dimensions ?? new List<DimensionDefinition>();
            Zones = zones ?? new List<DimensionZoneDefinition>();
            MapLayers = mapLayers ?? new List<DimensionMapLayerDefinition>();
            MapMarkers = mapMarkers ?? new List<DimensionMapMarker>();
            Anchors = anchors ?? new List<DimensionAnchorDefinition>();
            Portals = portals ?? new List<DimensionPortalDefinition>();
            PortalPresentations = portalPresentations ?? new List<DimensionPortalPresentationDefinition>();
            TravelRequirements = travelRequirements ?? new List<DimensionTravelRequirementDefinition>();
            Starters = new List<DimensionStarterDefinition>();
            SceneTemplates = sceneTemplates ?? new List<DimensionSceneTemplateDefinition>();
            Scenes = scenes ?? new List<DimensionSceneDefinition>();
            SpawnRules = spawnRules ?? new List<DimensionSpawnRule>();
            Encounters = encounters ?? new List<DimensionEncounterDefinition>();
            GenerationPasses = generationPasses ?? new List<DimensionGenerationPassDefinition>();
            ResourceNodes = resourceNodes ?? new List<DimensionResourceNodeDefinition>();
            ProgressFlags = progressFlags ?? new List<DimensionProgressFlag>();
            WorldEvents = worldEvents ?? new List<DimensionWorldEventDefinition>();
            OwnershipBindings = ownershipBindings ?? new List<DimensionContentOwnershipBinding>();
            AssetReferences = assetReferences ?? new List<DimensionAssetReferenceDefinition>();
            EnvironmentProfiles = environmentProfiles ?? new List<DimensionEnvironmentProfile>();
            Biomes = biomes ?? new List<DimensionBiomeDefinition>();
            GenerationTables = generationTables ?? new List<DimensionGenerationTableDefinition>();
            GenerationTableEntries = generationTableEntries ?? new List<DimensionGenerationTableEntryDefinition>();
        }

        public DimensionContentManifest(
            DimensionContentManifest source,
            IReadOnlyList<DimensionStarterDefinition> starters)
        {
            ContentPacks = source.ContentPacks ?? new List<DimensionContentPackDefinition>();
            Dimensions = source.Dimensions ?? new List<DimensionDefinition>();
            Zones = source.Zones ?? new List<DimensionZoneDefinition>();
            MapLayers = source.MapLayers ?? new List<DimensionMapLayerDefinition>();
            MapMarkers = source.MapMarkers ?? new List<DimensionMapMarker>();
            Anchors = source.Anchors ?? new List<DimensionAnchorDefinition>();
            Portals = source.Portals ?? new List<DimensionPortalDefinition>();
            PortalPresentations = source.PortalPresentations ?? new List<DimensionPortalPresentationDefinition>();
            TravelRequirements = source.TravelRequirements ?? new List<DimensionTravelRequirementDefinition>();
            Starters = starters ?? new List<DimensionStarterDefinition>();
            SceneTemplates = source.SceneTemplates ?? new List<DimensionSceneTemplateDefinition>();
            Scenes = source.Scenes ?? new List<DimensionSceneDefinition>();
            SpawnRules = source.SpawnRules ?? new List<DimensionSpawnRule>();
            Encounters = source.Encounters ?? new List<DimensionEncounterDefinition>();
            GenerationPasses = source.GenerationPasses ?? new List<DimensionGenerationPassDefinition>();
            ResourceNodes = source.ResourceNodes ?? new List<DimensionResourceNodeDefinition>();
            ProgressFlags = source.ProgressFlags ?? new List<DimensionProgressFlag>();
            WorldEvents = source.WorldEvents ?? new List<DimensionWorldEventDefinition>();
            OwnershipBindings = source.OwnershipBindings ?? new List<DimensionContentOwnershipBinding>();
            AssetReferences = source.AssetReferences ?? new List<DimensionAssetReferenceDefinition>();
            EnvironmentProfiles = source.EnvironmentProfiles ?? new List<DimensionEnvironmentProfile>();
            Biomes = source.Biomes ?? new List<DimensionBiomeDefinition>();
            GenerationTables = source.GenerationTables ?? new List<DimensionGenerationTableDefinition>();
            GenerationTableEntries = source.GenerationTableEntries ?? new List<DimensionGenerationTableEntryDefinition>();
        }
    }
}
