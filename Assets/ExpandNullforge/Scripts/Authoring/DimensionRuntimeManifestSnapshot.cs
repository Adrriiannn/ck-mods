using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    [Serializable]
    internal sealed class DimensionRuntimeManifestSnapshot
    {
        public ContentPackSnapshot[] contentPacks = new ContentPackSnapshot[0];
        public DimensionSnapshot[] dimensions = new DimensionSnapshot[0];
        public ZoneSnapshot[] zones = new ZoneSnapshot[0];
        public MapLayerSnapshot[] mapLayers = new MapLayerSnapshot[0];
        public MapMarkerSnapshot[] mapMarkers = new MapMarkerSnapshot[0];
        public AnchorSnapshot[] anchors = new AnchorSnapshot[0];
        public PortalSnapshot[] portals = new PortalSnapshot[0];
        public PortalPresentationSnapshot[] portalPresentations = new PortalPresentationSnapshot[0];
        public TravelRequirementSnapshot[] travelRequirements = new TravelRequirementSnapshot[0];
        public StarterSnapshot[] starters = new StarterSnapshot[0];
        public SceneTemplateSnapshot[] sceneTemplates = new SceneTemplateSnapshot[0];
        public SceneSnapshot[] scenes = new SceneSnapshot[0];
        public SpawnRuleSnapshot[] spawnRules = new SpawnRuleSnapshot[0];
        public EncounterSnapshot[] encounters = new EncounterSnapshot[0];
        public GenerationPassSnapshot[] generationPasses = new GenerationPassSnapshot[0];
        public ResourceNodeSnapshot[] resourceNodes = new ResourceNodeSnapshot[0];
        public ProgressFlagSnapshot[] progressFlags = new ProgressFlagSnapshot[0];
        public WorldEventSnapshot[] worldEvents = new WorldEventSnapshot[0];
        public OwnershipBindingSnapshot[] ownershipBindings = new OwnershipBindingSnapshot[0];
        public AssetReferenceSnapshot[] assetReferences = new AssetReferenceSnapshot[0];
        public EnvironmentProfileSnapshot[] environmentProfiles = new EnvironmentProfileSnapshot[0];
        public BiomeSnapshot[] biomes = new BiomeSnapshot[0];
        public GenerationTableSnapshot[] generationTables = new GenerationTableSnapshot[0];
        public GenerationTableEntrySnapshot[] generationTableEntries = new GenerationTableEntrySnapshot[0];

        public static DimensionRuntimeManifestSnapshot FromManifest(DimensionContentManifest manifest)
        {
            return new DimensionRuntimeManifestSnapshot
            {
                contentPacks = Convert(manifest.ContentPacks, ContentPackSnapshot.From),
                dimensions = Convert(manifest.Dimensions, DimensionSnapshot.From),
                zones = Convert(manifest.Zones, ZoneSnapshot.From),
                mapLayers = Convert(manifest.MapLayers, MapLayerSnapshot.From),
                mapMarkers = Convert(manifest.MapMarkers, MapMarkerSnapshot.From),
                anchors = Convert(manifest.Anchors, AnchorSnapshot.From),
                portals = Convert(manifest.Portals, PortalSnapshot.From),
                portalPresentations = Convert(manifest.PortalPresentations, PortalPresentationSnapshot.From),
                travelRequirements = Convert(manifest.TravelRequirements, TravelRequirementSnapshot.From),
                starters = Convert(manifest.Starters, StarterSnapshot.From),
                sceneTemplates = Convert(manifest.SceneTemplates, SceneTemplateSnapshot.From),
                scenes = Convert(manifest.Scenes, SceneSnapshot.From),
                spawnRules = Convert(manifest.SpawnRules, SpawnRuleSnapshot.From),
                encounters = Convert(manifest.Encounters, EncounterSnapshot.From),
                generationPasses = Convert(manifest.GenerationPasses, GenerationPassSnapshot.From),
                resourceNodes = Convert(manifest.ResourceNodes, ResourceNodeSnapshot.From),
                progressFlags = Convert(manifest.ProgressFlags, ProgressFlagSnapshot.From),
                worldEvents = Convert(manifest.WorldEvents, WorldEventSnapshot.From),
                ownershipBindings = Convert(manifest.OwnershipBindings, OwnershipBindingSnapshot.From),
                assetReferences = Convert(manifest.AssetReferences, AssetReferenceSnapshot.From),
                environmentProfiles = Convert(manifest.EnvironmentProfiles, EnvironmentProfileSnapshot.From),
                biomes = Convert(manifest.Biomes, BiomeSnapshot.From),
                generationTables = Convert(manifest.GenerationTables, GenerationTableSnapshot.From),
                generationTableEntries = Convert(manifest.GenerationTableEntries, GenerationTableEntrySnapshot.From)
            };
        }

        public DimensionContentManifest ToManifest()
        {
            DimensionContentManifest baseManifest =
                new DimensionContentManifest(
                    Convert(contentPacks, record => record.ToDefinition()),
                    Convert(dimensions, record => record.ToDefinition()),
                    Convert(zones, record => record.ToDefinition()),
                    Convert(mapLayers, record => record.ToDefinition()),
                    Convert(mapMarkers, record => record.ToDefinition()),
                    Convert(anchors, record => record.ToDefinition()),
                    Convert(portals, record => record.ToDefinition()),
                    Convert(portalPresentations, record => record.ToDefinition()),
                    Convert(travelRequirements, record => record.ToDefinition()),
                    Convert(sceneTemplates, record => record.ToDefinition()),
                    Convert(scenes, record => record.ToDefinition()),
                    Convert(spawnRules, record => record.ToDefinition()),
                    Convert(encounters, record => record.ToDefinition()),
                    Convert(generationPasses, record => record.ToDefinition()),
                    Convert(resourceNodes, record => record.ToDefinition()),
                    Convert(progressFlags, record => record.ToDefinition()),
                    Convert(worldEvents, record => record.ToDefinition()),
                    Convert(ownershipBindings, record => record.ToDefinition()),
                    Convert(assetReferences, record => record.ToDefinition()),
                    Convert(environmentProfiles, record => record.ToDefinition()),
                    Convert(biomes, record => record.ToDefinition()),
                    Convert(generationTables, record => record.ToDefinition()),
                    Convert(generationTableEntries, record => record.ToDefinition()));

            return new DimensionContentManifest(
                baseManifest,
                Convert(starters, record => record.ToDefinition()));
        }

        private static TOut[] Convert<TIn, TOut>(
            IReadOnlyList<TIn> source,
            Func<TIn, TOut> convert)
        {
            if (source == null || source.Count == 0)
            {
                return new TOut[0];
            }

            List<TOut> result = new List<TOut>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(convert(source[i]));
            }

            return result.ToArray();
        }

        private static List<TOut> Convert<TIn, TOut>(
            TIn[] source,
            Func<TIn, TOut> convert)
            where TIn : class
        {
            List<TOut> result = new List<TOut>();
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    result.Add(convert(source[i]));
                }
            }

            return result;
        }

        [Serializable]
        internal sealed class BoundsSnapshot
        {
            public int minX;
            public int minY;
            public int maxX;
            public int maxY;

            public static BoundsSnapshot From(DimensionBounds bounds)
            {
                return new BoundsSnapshot
                {
                    minX = bounds.Min.x,
                    minY = bounds.Min.y,
                    maxX = bounds.MaxExclusive.x,
                    maxY = bounds.MaxExclusive.y
                };
            }

            public DimensionBounds ToBounds()
            {
                return new DimensionBounds(
                    new int2(minX, minY),
                    new int2(maxX, maxY));
            }
        }

        [Serializable]
        internal sealed class Float2Snapshot
        {
            public float x;
            public float y;

            public static Float2Snapshot From(float2 value)
            {
                return new Float2Snapshot { x = value.x, y = value.y };
            }

            public float2 ToFloat2()
            {
                return new float2(x, y);
            }
        }

        [Serializable]
        internal sealed class Int2Snapshot
        {
            public int x;
            public int y;

            public static Int2Snapshot From(int2 value)
            {
                return new Int2Snapshot { x = value.x, y = value.y };
            }

            public int2 ToInt2()
            {
                return new int2(x, y);
            }
        }

        [Serializable]
        internal sealed class ContentPackSnapshot
        {
            public string id;
            public string displayName;
            public string version;
            public string author;
            public string description;
            public int minimumApiVersion;
            public string[] dependencyIds = new string[0];
            public bool enabled;

            public static ContentPackSnapshot From(DimensionContentPackDefinition value)
            {
                return new ContentPackSnapshot
                {
                    id = value.ContentPackId,
                    displayName = value.DisplayName,
                    version = value.Version,
                    author = value.Author,
                    description = value.Description,
                    minimumApiVersion = value.MinimumApiVersion,
                    dependencyIds = CopyStrings(value.DependencyIds),
                    enabled = value.Enabled
                };
            }

            public DimensionContentPackDefinition ToDefinition()
            {
                return new DimensionContentPackDefinition(
                    id,
                    displayName,
                    version,
                    author,
                    description,
                    minimumApiVersion,
                    dependencyIds,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class DimensionSnapshot
        {
            public string id;
            public string displayName;
            public Int2Snapshot absoluteOrigin;
            public BoundsSnapshot localBounds;
            public int generationVersion;
            public int spaceKind;
            public int capabilities;
            public int lifecycleState;

            public static DimensionSnapshot From(DimensionDefinition value)
            {
                return new DimensionSnapshot
                {
                    id = value.Id,
                    displayName = value.DisplayName,
                    absoluteOrigin = Int2Snapshot.From(value.AbsoluteOrigin),
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    generationVersion = value.GenerationVersion,
                    spaceKind = (int)value.SpaceKind,
                    capabilities = (int)value.Capabilities,
                    lifecycleState = (int)value.LifecycleState
                };
            }

            public DimensionDefinition ToDefinition()
            {
                return new DimensionDefinition(
                    id,
                    displayName,
                    ToInt2(absoluteOrigin),
                    ToBounds(localBounds),
                    generationVersion,
                    (DimensionSpaceKind)spaceKind,
                    (DimensionCapabilityFlags)capabilities,
                    (DimensionLifecycleState)lifecycleState);
            }
        }

        [Serializable]
        internal sealed class ZoneSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public BoundsSnapshot localBounds;
            public string kind;
            public int priority;
            public bool enabled;

            public static ZoneSnapshot From(DimensionZoneDefinition value)
            {
                return new ZoneSnapshot
                {
                    id = value.ZoneId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    kind = value.Kind,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionZoneDefinition ToDefinition()
            {
                return new DimensionZoneDefinition(
                    id,
                    displayName,
                    dimensionId,
                    ToBounds(localBounds),
                    kind,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class MapLayerSnapshot
        {
            public string id;
            public string dimensionId;
            public string displayName;
            public string description;
            public string iconId;
            public bool hasTintColor;
            public uint tintColorRgba;
            public int priority;
            public bool visible;
            public bool selectable;

            public static MapLayerSnapshot From(DimensionMapLayerDefinition value)
            {
                return new MapLayerSnapshot
                {
                    id = value.LayerId,
                    dimensionId = value.DimensionId,
                    displayName = value.DisplayName,
                    description = value.Description,
                    iconId = value.IconId,
                    hasTintColor = value.HasTintColor,
                    tintColorRgba = value.TintColorRgba,
                    priority = value.Priority,
                    visible = value.Visible,
                    selectable = value.Selectable
                };
            }

            public DimensionMapLayerDefinition ToDefinition()
            {
                return new DimensionMapLayerDefinition(
                    id,
                    dimensionId,
                    displayName,
                    description,
                    iconId,
                    hasTintColor,
                    tintColorRgba,
                    priority,
                    visible,
                    selectable);
            }
        }

        [Serializable]
        internal sealed class MapMarkerSnapshot
        {
            public string id;
            public string dimensionId;
            public Float2Snapshot localPosition;
            public string label;
            public string kind;
            public bool visible;

            public static MapMarkerSnapshot From(DimensionMapMarker value)
            {
                return new MapMarkerSnapshot
                {
                    id = value.MarkerId,
                    dimensionId = value.DimensionId,
                    localPosition = Float2Snapshot.From(value.LocalPosition),
                    label = value.Label,
                    kind = value.Kind,
                    visible = value.Visible
                };
            }

            public DimensionMapMarker ToDefinition()
            {
                return new DimensionMapMarker(
                    id,
                    dimensionId,
                    ToFloat2(localPosition),
                    label,
                    kind,
                    visible);
            }
        }

        [Serializable]
        internal sealed class AnchorSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public Float2Snapshot localPosition;
            public int kind;
            public int priority;
            public bool enabled;

            public static AnchorSnapshot From(DimensionAnchorDefinition value)
            {
                return new AnchorSnapshot
                {
                    id = value.AnchorId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    localPosition = Float2Snapshot.From(value.LocalPosition),
                    kind = (int)value.Kind,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionAnchorDefinition ToDefinition()
            {
                return new DimensionAnchorDefinition(
                    id,
                    displayName,
                    dimensionId,
                    ToFloat2(localPosition),
                    (DimensionAnchorKind)kind,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class PortalSnapshot
        {
            public string id;
            public string displayName;
            public string fromDimensionId;
            public Float2Snapshot fromLocalPosition;
            public string toDimensionId;
            public Float2Snapshot toLocalPosition;
            public int state;

            public static PortalSnapshot From(DimensionPortalDefinition value)
            {
                return new PortalSnapshot
                {
                    id = value.PortalId,
                    displayName = value.DisplayName,
                    fromDimensionId = value.FromDimensionId,
                    fromLocalPosition = Float2Snapshot.From(value.FromLocalPosition),
                    toDimensionId = value.ToDimensionId,
                    toLocalPosition = Float2Snapshot.From(value.ToLocalPosition),
                    state = (int)value.State
                };
            }

            public DimensionPortalDefinition ToDefinition()
            {
                return new DimensionPortalDefinition(
                    id,
                    displayName,
                    fromDimensionId,
                    ToFloat2(fromLocalPosition),
                    toDimensionId,
                    ToFloat2(toLocalPosition),
                    (DimensionPortalState)state);
            }
        }

        [Serializable]
        internal sealed class PortalPresentationSnapshot
        {
            public string id;
            public string portalId;
            public string displayName;
            public string promptText;
            public string lockedPromptText;
            public string iconId;
            public string visualEffectId;
            public string audioCueId;
            public float cooldownSeconds;
            public int priority;
            public bool enabled;
            public bool interactable = true;
            public bool requireGeneratedAreaOnUse;
            public bool allowFallbackPositionOnUse;

            public static PortalPresentationSnapshot From(DimensionPortalPresentationDefinition value)
            {
                return new PortalPresentationSnapshot
                {
                    id = value.PresentationId,
                    portalId = value.PortalId,
                    displayName = value.DisplayName,
                    promptText = value.PromptText,
                    lockedPromptText = value.LockedPromptText,
                    iconId = value.IconId,
                    visualEffectId = value.VisualEffectId,
                    audioCueId = value.AudioCueId,
                    cooldownSeconds = value.CooldownSeconds,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    interactable = value.Interactable,
                    requireGeneratedAreaOnUse = value.RequireGeneratedAreaOnUse,
                    allowFallbackPositionOnUse = value.AllowFallbackPositionOnUse
                };
            }

            public DimensionPortalPresentationDefinition ToDefinition()
            {
                return new DimensionPortalPresentationDefinition(
                    id,
                    portalId,
                    displayName,
                    promptText,
                    lockedPromptText,
                    iconId,
                    visualEffectId,
                    audioCueId,
                    cooldownSeconds,
                    priority,
                    enabled,
                    requireGeneratedAreaOnUse,
                    allowFallbackPositionOnUse,
                    interactable);
            }
        }

        [Serializable]
        internal sealed class TravelRequirementSnapshot
        {
            public string id;
            public string displayName;
            public string portalId;
            public string dimensionId;
            public int kind;
            public string subjectId;
            public int requiredAmount;
            public bool consumeOnTravel;
            public string failureMessage;
            public int priority;
            public bool enabled;

            public static TravelRequirementSnapshot From(DimensionTravelRequirementDefinition value)
            {
                return new TravelRequirementSnapshot
                {
                    id = value.RequirementId,
                    displayName = value.DisplayName,
                    portalId = value.PortalId,
                    dimensionId = value.DimensionId,
                    kind = (int)value.Kind,
                    subjectId = value.SubjectId,
                    requiredAmount = value.RequiredAmount,
                    consumeOnTravel = value.ConsumeOnTravel,
                    failureMessage = value.FailureMessage,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionTravelRequirementDefinition ToDefinition()
            {
                return new DimensionTravelRequirementDefinition(
                    id,
                    displayName,
                    portalId,
                    dimensionId,
                    (DimensionTravelRequirementKind)kind,
                    subjectId,
                    requiredAmount,
                    consumeOnTravel,
                    failureMessage,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class StarterSnapshot
        {
            public string id;
            public string dimensionId;
            public string displayName;
            public string description;
            public string contentPackId;
            public bool enabled;
            public ContentReadinessSnapshot readiness;
            public TravelLoopSnapshot travelLoop;
            public StarterGenerationSnapshot generation;

            public static StarterSnapshot From(DimensionStarterDefinition value)
            {
                return new StarterSnapshot
                {
                    id = value.StarterId,
                    dimensionId = value.DimensionId,
                    displayName = value.DisplayName,
                    description = value.Description,
                    contentPackId = value.ContentPackId,
                    enabled = value.Enabled,
                    readiness = ContentReadinessSnapshot.From(value.ContentReadinessRequest),
                    travelLoop = TravelLoopSnapshot.From(value.TravelLoopPreflightRequest),
                    generation = StarterGenerationSnapshot.From(value.GenerationRequest)
                };
            }

            public DimensionStarterDefinition ToDefinition()
            {
                return new DimensionStarterDefinition(
                    id,
                    dimensionId,
                    displayName,
                    description,
                    contentPackId,
                    enabled,
                    readiness == null ? default(DimensionContentReadinessRequest) : readiness.ToRequest(),
                    travelLoop == null ? default(DimensionTravelLoopPreflightRequest) : travelLoop.ToRequest(),
                    generation == null ? default(DimensionStarterGenerationRequest) : generation.ToRequest());
            }
        }

        [Serializable]
        internal sealed class ContentReadinessSnapshot
        {
            public string id;
            public string displayName;
            public ContentReadinessRequirementSnapshot[] requirements =
                new ContentReadinessRequirementSnapshot[0];

            public static ContentReadinessSnapshot From(DimensionContentReadinessRequest value)
            {
                return new ContentReadinessSnapshot
                {
                    id = value.RequestId,
                    displayName = value.DisplayName,
                    requirements = Convert(value.Requirements, ContentReadinessRequirementSnapshot.From)
                };
            }

            public DimensionContentReadinessRequest ToRequest()
            {
                return new DimensionContentReadinessRequest(
                    id,
                    displayName,
                    Convert(requirements, record => record.ToRequirement()));
            }
        }

        [Serializable]
        internal sealed class ContentReadinessRequirementSnapshot
        {
            public int recordKind;
            public string recordId;
            public string displayName;
            public bool requireOwnership;
            public string requiredOwnerContentPackId;
            public bool requireEnabledOwnerContentPack;

            public static ContentReadinessRequirementSnapshot From(DimensionContentReadinessRequirement value)
            {
                return new ContentReadinessRequirementSnapshot
                {
                    recordKind = (int)value.RecordKind,
                    recordId = value.RecordId,
                    displayName = value.DisplayName,
                    requireOwnership = value.RequireOwnership,
                    requiredOwnerContentPackId = value.RequiredOwnerContentPackId,
                    requireEnabledOwnerContentPack = value.RequireEnabledOwnerContentPack
                };
            }

            public DimensionContentReadinessRequirement ToRequirement()
            {
                return new DimensionContentReadinessRequirement(
                    (DimensionContentRecordKind)recordKind,
                    recordId,
                    displayName,
                    requireOwnership,
                    requiredOwnerContentPackId,
                    requireEnabledOwnerContentPack);
            }
        }

        [Serializable]
        internal sealed class TravelLoopSnapshot
        {
            public string sourceDimensionId;
            public string targetDimensionId;
            public string entryPortalId;
            public string returnPortalId;
            public string sourceAnchorId;
            public string targetAnchorId;
            public string sourceMarkerId;
            public string targetMarkerId;
            public BoundsSnapshot targetLandingBounds;
            public bool requireReturnPortal;
            public bool requireSourceAnchor;
            public bool requireTargetAnchor;
            public bool requireMarkers;
            public bool requireTargetAreaReady;
            public bool requireMapLayers;

            public static TravelLoopSnapshot From(DimensionTravelLoopPreflightRequest value)
            {
                return new TravelLoopSnapshot
                {
                    sourceDimensionId = value.SourceDimensionId,
                    targetDimensionId = value.TargetDimensionId,
                    entryPortalId = value.EntryPortalId,
                    returnPortalId = value.ReturnPortalId,
                    sourceAnchorId = value.SourceAnchorId,
                    targetAnchorId = value.TargetAnchorId,
                    sourceMarkerId = value.SourceMarkerId,
                    targetMarkerId = value.TargetMarkerId,
                    targetLandingBounds = BoundsSnapshot.From(value.TargetLandingBounds),
                    requireReturnPortal = value.RequireReturnPortal,
                    requireSourceAnchor = value.RequireSourceAnchor,
                    requireTargetAnchor = value.RequireTargetAnchor,
                    requireMarkers = value.RequireMarkers,
                    requireTargetAreaReady = value.RequireTargetAreaReady,
                    requireMapLayers = value.RequireMapLayers
                };
            }

            public DimensionTravelLoopPreflightRequest ToRequest()
            {
                return new DimensionTravelLoopPreflightRequest(
                    sourceDimensionId,
                    targetDimensionId,
                    entryPortalId,
                    returnPortalId,
                    sourceAnchorId,
                    targetAnchorId,
                    sourceMarkerId,
                    targetMarkerId,
                    ToBounds(targetLandingBounds),
                    requireReturnPortal,
                    requireSourceAnchor,
                    requireTargetAnchor,
                    requireMarkers,
                    requireTargetAreaReady,
                    requireMapLayers);
            }
        }

        [Serializable]
        internal sealed class StarterGenerationSnapshot
        {
            public string starterId;
            public string requesterId;
            public string dimensionId;
            public BoundsSnapshot localBounds;
            public int priority;
            public bool createIfMissing;
            public string reason;

            public static StarterGenerationSnapshot From(DimensionStarterGenerationRequest value)
            {
                return new StarterGenerationSnapshot
                {
                    starterId = value.StarterId,
                    requesterId = value.RequesterId,
                    dimensionId = value.DimensionId,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    priority = value.Priority,
                    createIfMissing = value.CreateIfMissing,
                    reason = value.Reason
                };
            }

            public DimensionStarterGenerationRequest ToRequest()
            {
                return new DimensionStarterGenerationRequest(
                    starterId,
                    requesterId,
                    dimensionId,
                    ToBounds(localBounds),
                    priority,
                    createIfMissing,
                    reason);
            }
        }

        [Serializable]
        internal sealed class SceneTemplateSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public string kind;
            public string providerId;
            public Int2Snapshot footprintSize;
            public int weight;
            public int priority;
            public bool enabled;

            public static SceneTemplateSnapshot From(DimensionSceneTemplateDefinition value)
            {
                return new SceneTemplateSnapshot
                {
                    id = value.TemplateId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    kind = value.Kind,
                    providerId = value.ProviderId,
                    footprintSize = Int2Snapshot.From(value.FootprintSize),
                    weight = value.Weight,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionSceneTemplateDefinition ToDefinition()
            {
                return new DimensionSceneTemplateDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    kind,
                    providerId,
                    ToInt2(footprintSize),
                    weight,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class SceneSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public BoundsSnapshot localBounds;
            public string kind;
            public int priority;
            public int state;

            public static SceneSnapshot From(DimensionSceneDefinition value)
            {
                return new SceneSnapshot
                {
                    id = value.SceneId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    kind = value.Kind,
                    priority = value.Priority,
                    state = (int)value.State
                };
            }

            public DimensionSceneDefinition ToDefinition()
            {
                return new DimensionSceneDefinition(
                    id,
                    displayName,
                    dimensionId,
                    ToBounds(localBounds),
                    kind,
                    priority,
                    (DimensionSceneState)state);
            }
        }

        [Serializable]
        internal sealed class SpawnRuleSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public bool hasLocalBounds;
            public BoundsSnapshot localBounds;
            public string subjectId;
            public int subjectKind;
            public int weight;
            public int priority;
            public bool enabled;

            public static SpawnRuleSnapshot From(DimensionSpawnRule value)
            {
                return new SpawnRuleSnapshot
                {
                    id = value.RuleId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    hasLocalBounds = value.HasLocalBounds,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    subjectId = value.SubjectId,
                    subjectKind = (int)value.SubjectKind,
                    weight = value.Weight,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionSpawnRule ToDefinition()
            {
                return new DimensionSpawnRule(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    hasLocalBounds,
                    ToBounds(localBounds),
                    subjectId,
                    (DimensionSpawnSubjectKind)subjectKind,
                    weight,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class EncounterSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public string sceneId;
            public string spawnRuleId;
            public string markerId;
            public string defeatFlagId;
            public int kind;
            public int priority;
            public bool enabled;

            public static EncounterSnapshot From(DimensionEncounterDefinition value)
            {
                return new EncounterSnapshot
                {
                    id = value.EncounterId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    sceneId = value.SceneId,
                    spawnRuleId = value.SpawnRuleId,
                    markerId = value.MarkerId,
                    defeatFlagId = value.DefeatFlagId,
                    kind = (int)value.Kind,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionEncounterDefinition ToDefinition()
            {
                return new DimensionEncounterDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    sceneId,
                    spawnRuleId,
                    markerId,
                    defeatFlagId,
                    (DimensionEncounterKind)kind,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class GenerationPassSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public bool hasLocalBounds;
            public BoundsSnapshot localBounds;
            public int phase;
            public int priority;
            public string providerId;
            public bool enabled;

            public static GenerationPassSnapshot From(DimensionGenerationPassDefinition value)
            {
                return new GenerationPassSnapshot
                {
                    id = value.PassId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    hasLocalBounds = value.HasLocalBounds,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    phase = (int)value.Phase,
                    priority = value.Priority,
                    providerId = value.ProviderId,
                    enabled = value.Enabled
                };
            }

            public DimensionGenerationPassDefinition ToDefinition()
            {
                return new DimensionGenerationPassDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    hasLocalBounds,
                    ToBounds(localBounds),
                    (DimensionGenerationPassPhase)phase,
                    priority,
                    providerId,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class ResourceNodeSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public bool hasLocalBounds;
            public BoundsSnapshot localBounds;
            public string resourceId;
            public int kind;
            public string providerId;
            public string generationPassId;
            public int weight;
            public int priority;
            public bool enabled;

            public static ResourceNodeSnapshot From(DimensionResourceNodeDefinition value)
            {
                return new ResourceNodeSnapshot
                {
                    id = value.NodeId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    hasLocalBounds = value.HasLocalBounds,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    resourceId = value.ResourceId,
                    kind = (int)value.Kind,
                    providerId = value.ProviderId,
                    generationPassId = value.GenerationPassId,
                    weight = value.Weight,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionResourceNodeDefinition ToDefinition()
            {
                return new DimensionResourceNodeDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    hasLocalBounds,
                    ToBounds(localBounds),
                    resourceId,
                    (DimensionResourceNodeKind)kind,
                    providerId,
                    generationPassId,
                    weight,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class ProgressFlagSnapshot
        {
            public string id;
            public string dimensionId;
            public string category;
            public bool value;
            public long updatedUtcTicks;

            public static ProgressFlagSnapshot From(DimensionProgressFlag flag)
            {
                return new ProgressFlagSnapshot
                {
                    id = flag.FlagId,
                    dimensionId = flag.DimensionId,
                    category = flag.Category,
                    value = flag.Value,
                    updatedUtcTicks = flag.UpdatedUtcTicks
                };
            }

            public DimensionProgressFlag ToDefinition()
            {
                return new DimensionProgressFlag(
                    id,
                    dimensionId,
                    category,
                    value,
                    updatedUtcTicks);
            }
        }

        [Serializable]
        internal sealed class WorldEventSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public bool hasLocalBounds;
            public BoundsSnapshot localBounds;
            public int kind;
            public string providerId;
            public string progressFlagId;
            public int weight;
            public int priority;
            public float cooldownSeconds;
            public bool enabled;

            public static WorldEventSnapshot From(DimensionWorldEventDefinition value)
            {
                return new WorldEventSnapshot
                {
                    id = value.EventId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    hasLocalBounds = value.HasLocalBounds,
                    localBounds = BoundsSnapshot.From(value.LocalBounds),
                    kind = (int)value.Kind,
                    providerId = value.ProviderId,
                    progressFlagId = value.ProgressFlagId,
                    weight = value.Weight,
                    priority = value.Priority,
                    cooldownSeconds = value.CooldownSeconds,
                    enabled = value.Enabled
                };
            }

            public DimensionWorldEventDefinition ToDefinition()
            {
                return new DimensionWorldEventDefinition(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    hasLocalBounds,
                    ToBounds(localBounds),
                    (DimensionWorldEventKind)kind,
                    providerId,
                    progressFlagId,
                    weight,
                    priority,
                    cooldownSeconds,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class OwnershipBindingSnapshot
        {
            public string contentPackId;
            public int recordKind;
            public string recordId;
            public string displayName;
            public string notes;

            public static OwnershipBindingSnapshot From(DimensionContentOwnershipBinding value)
            {
                return new OwnershipBindingSnapshot
                {
                    contentPackId = value.ContentPackId,
                    recordKind = (int)value.RecordKind,
                    recordId = value.RecordId,
                    displayName = value.DisplayName,
                    notes = value.Notes
                };
            }

            public DimensionContentOwnershipBinding ToDefinition()
            {
                return new DimensionContentOwnershipBinding(
                    contentPackId,
                    (DimensionContentRecordKind)recordKind,
                    recordId,
                    displayName,
                    notes);
            }
        }

        [Serializable]
        internal sealed class AssetReferenceSnapshot
        {
            public string id;
            public string contentPackId;
            public string displayName;
            public int kind;
            public string resourceKey;
            public string dimensionId;
            public string zoneId;
            public string variantId;
            public int priority;
            public bool enabled;
            public string notes;

            public static AssetReferenceSnapshot From(DimensionAssetReferenceDefinition value)
            {
                return new AssetReferenceSnapshot
                {
                    id = value.AssetId,
                    contentPackId = value.ContentPackId,
                    displayName = value.DisplayName,
                    kind = (int)value.Kind,
                    resourceKey = value.ResourceKey,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    variantId = value.VariantId,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    notes = value.Notes
                };
            }

            public DimensionAssetReferenceDefinition ToDefinition()
            {
                return new DimensionAssetReferenceDefinition(
                    id,
                    contentPackId,
                    displayName,
                    (DimensionAssetReferenceKind)kind,
                    resourceKey,
                    dimensionId,
                    zoneId,
                    variantId,
                    priority,
                    enabled,
                    notes);
            }
        }

        [Serializable]
        internal sealed class EnvironmentProfileSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string zoneId;
            public string musicCueId;
            public string ambientCueId;
            public string lightingProfileId;
            public string fogProfileId;
            public bool hasMapColor;
            public uint mapColorRgba;
            public int priority;
            public bool enabled;

            public static EnvironmentProfileSnapshot From(DimensionEnvironmentProfile value)
            {
                return new EnvironmentProfileSnapshot
                {
                    id = value.ProfileId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    zoneId = value.ZoneId,
                    musicCueId = value.MusicCueId,
                    ambientCueId = value.AmbientCueId,
                    lightingProfileId = value.LightingProfileId,
                    fogProfileId = value.FogProfileId,
                    hasMapColor = value.HasMapColor,
                    mapColorRgba = value.MapColorRgba,
                    priority = value.Priority,
                    enabled = value.Enabled
                };
            }

            public DimensionEnvironmentProfile ToDefinition()
            {
                return new DimensionEnvironmentProfile(
                    id,
                    displayName,
                    dimensionId,
                    zoneId,
                    musicCueId,
                    ambientCueId,
                    lightingProfileId,
                    fogProfileId,
                    hasMapColor,
                    mapColorRgba,
                    priority,
                    enabled);
            }
        }

        [Serializable]
        internal sealed class BiomeSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string environmentProfileId;
            public string paletteAssetId;
            public string spawnTableId;
            public string resourceTableId;
            public string worldEventTableId;
            public uint mapColorRgba;
            public int priority;
            public bool enabled;
            public string notes;

            public static BiomeSnapshot From(DimensionBiomeDefinition value)
            {
                return new BiomeSnapshot
                {
                    id = value.BiomeId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    environmentProfileId = value.EnvironmentProfileId,
                    paletteAssetId = value.PaletteAssetId,
                    spawnTableId = value.SpawnTableId,
                    resourceTableId = value.ResourceTableId,
                    worldEventTableId = value.WorldEventTableId,
                    mapColorRgba = value.MapColorRgba,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    notes = value.Notes
                };
            }

            public DimensionBiomeDefinition ToDefinition()
            {
                return new DimensionBiomeDefinition(
                    id,
                    displayName,
                    dimensionId,
                    environmentProfileId,
                    paletteAssetId,
                    spawnTableId,
                    resourceTableId,
                    worldEventTableId,
                    mapColorRgba,
                    priority,
                    enabled,
                    notes);
            }
        }

        [Serializable]
        internal sealed class GenerationTableSnapshot
        {
            public string id;
            public string displayName;
            public string dimensionId;
            public string biomeId;
            public int kind;
            public int priority;
            public bool enabled;
            public string notes;

            public static GenerationTableSnapshot From(DimensionGenerationTableDefinition value)
            {
                return new GenerationTableSnapshot
                {
                    id = value.TableId,
                    displayName = value.DisplayName,
                    dimensionId = value.DimensionId,
                    biomeId = value.BiomeId,
                    kind = (int)value.Kind,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    notes = value.Notes
                };
            }

            public DimensionGenerationTableDefinition ToDefinition()
            {
                return new DimensionGenerationTableDefinition(
                    id,
                    displayName,
                    dimensionId,
                    biomeId,
                    (DimensionGenerationTableKind)kind,
                    priority,
                    enabled,
                    notes);
            }
        }

        [Serializable]
        internal sealed class GenerationTableEntrySnapshot
        {
            public string id;
            public string tableId;
            public string subjectId;
            public string subjectKind;
            public int weight;
            public int minCount;
            public int maxCount;
            public int priority;
            public bool enabled;
            public string notes;

            public static GenerationTableEntrySnapshot From(DimensionGenerationTableEntryDefinition value)
            {
                return new GenerationTableEntrySnapshot
                {
                    id = value.EntryId,
                    tableId = value.TableId,
                    subjectId = value.SubjectId,
                    subjectKind = value.SubjectKind,
                    weight = value.Weight,
                    minCount = value.MinCount,
                    maxCount = value.MaxCount,
                    priority = value.Priority,
                    enabled = value.Enabled,
                    notes = value.Notes
                };
            }

            public DimensionGenerationTableEntryDefinition ToDefinition()
            {
                return new DimensionGenerationTableEntryDefinition(
                    id,
                    tableId,
                    subjectId,
                    subjectKind,
                    weight,
                    minCount,
                    maxCount,
                    priority,
                    enabled,
                    notes);
            }
        }

        private static string[] CopyStrings(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return new string[0];
            }

            string[] result = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i] ?? string.Empty;
            }

            return result;
        }

        private static DimensionBounds ToBounds(BoundsSnapshot snapshot)
        {
            return snapshot == null
                ? new DimensionBounds(default(int2), default(int2))
                : snapshot.ToBounds();
        }

        private static float2 ToFloat2(Float2Snapshot snapshot)
        {
            return snapshot == null ? default(float2) : snapshot.ToFloat2();
        }

        private static int2 ToInt2(Int2Snapshot snapshot)
        {
            return snapshot == null ? default(int2) : snapshot.ToInt2();
        }
    }
}
