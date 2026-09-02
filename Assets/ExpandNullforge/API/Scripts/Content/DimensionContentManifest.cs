using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    // The manifest once carried spawn-rule, resource-node, environment-profile and
    // generation-table record lists. Those were Phase-0 record kinds whose stores had no
    // readers; the surfaces were removed rather than left as records nothing consumed, and
    // the telescoping constructor ladder that grew around them collapsed to the two shapes
    // the builders actually use.
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
        public readonly IReadOnlyList<DimensionEncounterDefinition> Encounters;
        public readonly IReadOnlyList<DimensionGenerationPassDefinition> GenerationPasses;
        public readonly IReadOnlyList<DimensionProgressFlag> ProgressFlags;
        public readonly IReadOnlyList<DimensionWorldEventDefinition> WorldEvents;
        public readonly IReadOnlyList<DimensionContentOwnershipBinding> OwnershipBindings;
        public readonly IReadOnlyList<DimensionAssetReferenceDefinition> AssetReferences;
        public readonly IReadOnlyList<DimensionBiomeDefinition> Biomes;

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
            IReadOnlyList<DimensionGenerationPassDefinition> generationPasses,
            IReadOnlyList<DimensionProgressFlag> progressFlags,
            IReadOnlyList<DimensionWorldEventDefinition> worldEvents,
            IReadOnlyList<DimensionContentOwnershipBinding> ownershipBindings,
            IReadOnlyList<DimensionAssetReferenceDefinition> assetReferences,
            IReadOnlyList<DimensionBiomeDefinition> biomes)
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
            Encounters = encounters ?? new List<DimensionEncounterDefinition>();
            GenerationPasses = generationPasses ?? new List<DimensionGenerationPassDefinition>();
            ProgressFlags = progressFlags ?? new List<DimensionProgressFlag>();
            WorldEvents = worldEvents ?? new List<DimensionWorldEventDefinition>();
            OwnershipBindings = ownershipBindings ?? new List<DimensionContentOwnershipBinding>();
            AssetReferences = assetReferences ?? new List<DimensionAssetReferenceDefinition>();
            Biomes = biomes ?? new List<DimensionBiomeDefinition>();
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
            Encounters = source.Encounters ?? new List<DimensionEncounterDefinition>();
            GenerationPasses = source.GenerationPasses ?? new List<DimensionGenerationPassDefinition>();
            ProgressFlags = source.ProgressFlags ?? new List<DimensionProgressFlag>();
            WorldEvents = source.WorldEvents ?? new List<DimensionWorldEventDefinition>();
            OwnershipBindings = source.OwnershipBindings ?? new List<DimensionContentOwnershipBinding>();
            AssetReferences = source.AssetReferences ?? new List<DimensionAssetReferenceDefinition>();
            Biomes = source.Biomes ?? new List<DimensionBiomeDefinition>();
        }
    }
}
