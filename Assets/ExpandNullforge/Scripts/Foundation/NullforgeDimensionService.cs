using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Loading;
using ExpandNullforge.Persistence;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService : IDimensionService, IDimensionRuntimeStateService
  {
    private readonly Dictionary<string, DimensionDefinition> definitions =
        new Dictionary<string, DimensionDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionContentPackDefinition> contentPacks =
        new Dictionary<string, DimensionContentPackDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionContentOwnershipBinding> contentOwnershipBindings =
        new Dictionary<string, DimensionContentOwnershipBinding>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionAssetReferenceDefinition> assetReferences =
        new Dictionary<string, DimensionAssetReferenceDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionBiomeDefinition> biomes =
        new Dictionary<string, DimensionBiomeDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionGenerationTableDefinition> generationTables =
        new Dictionary<string, DimensionGenerationTableDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionGenerationTableEntryDefinition> generationTableEntries =
        new Dictionary<string, DimensionGenerationTableEntryDefinition>(StringComparer.Ordinal);

    private readonly List<DimensionDefinition> definitionSnapshot =
        new List<DimensionDefinition>();

    private readonly Dictionary<string, DimensionPortalDefinition> portals =
        new Dictionary<string, DimensionPortalDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionPortalPresentationDefinition> portalPresentations =
        new Dictionary<string, DimensionPortalPresentationDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionTravelRequirementDefinition> travelRequirements =
        new Dictionary<string, DimensionTravelRequirementDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionStarterDefinition> starters =
        new Dictionary<string, DimensionStarterDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, IDimensionTravelRequirementEvaluator> travelRequirementEvaluators =
        new Dictionary<string, IDimensionTravelRequirementEvaluator>(StringComparer.Ordinal);

    private readonly List<IDimensionTravelRequirementEvaluator> orderedTravelRequirementEvaluators =
        new List<IDimensionTravelRequirementEvaluator>();

    private readonly Dictionary<string, DimensionMapLayerDefinition> mapLayers =
        new Dictionary<string, DimensionMapLayerDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionMapMarker> markers =
        new Dictionary<string, DimensionMapMarker>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionAnchorDefinition> anchors =
        new Dictionary<string, DimensionAnchorDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionSceneDefinition> scenes =
        new Dictionary<string, DimensionSceneDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionSceneTemplateDefinition> sceneTemplates =
        new Dictionary<string, DimensionSceneTemplateDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionEncounterDefinition> encounters =
        new Dictionary<string, DimensionEncounterDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionResourceNodeDefinition> resourceNodes =
        new Dictionary<string, DimensionResourceNodeDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionSpawnRule> spawnRules =
        new Dictionary<string, DimensionSpawnRule>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionWorldEventDefinition> worldEvents =
        new Dictionary<string, DimensionWorldEventDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionProgressFlag> progressFlags =
        new Dictionary<string, DimensionProgressFlag>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionZoneDefinition> zoneDefinitions =
        new Dictionary<string, DimensionZoneDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionEnvironmentProfile> environmentProfiles =
        new Dictionary<string, DimensionEnvironmentProfile>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionGenerationStatus> generationStatuses =
        new Dictionary<string, DimensionGenerationStatus>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionGenerationReservation> generationReservations =
        new Dictionary<string, DimensionGenerationReservation>(StringComparer.Ordinal);

    private readonly Dictionary<string, IDimensionGenerationProvider> generationProviders =
        new Dictionary<string, IDimensionGenerationProvider>(StringComparer.Ordinal);

    private readonly Dictionary<string, DimensionGenerationPassDefinition> generationPasses =
        new Dictionary<string, DimensionGenerationPassDefinition>(StringComparer.Ordinal);

    private readonly Dictionary<string, IDimensionZoneProvider> zoneProviders =
        new Dictionary<string, IDimensionZoneProvider>(StringComparer.Ordinal);

    private readonly Dictionary<string, IDimensionAccessProvider> accessProviders =
        new Dictionary<string, IDimensionAccessProvider>(StringComparer.Ordinal);

    private readonly List<IDimensionAccessProvider> orderedAccessProviders =
        new List<IDimensionAccessProvider>();

    private readonly Dictionary<string, IDimensionPermissionProvider> permissionProviders =
        new Dictionary<string, IDimensionPermissionProvider>(StringComparer.Ordinal);

    private readonly List<IDimensionPermissionProvider> orderedPermissionProviders =
        new List<IDimensionPermissionProvider>();

    private readonly Dictionary<string, RuntimeGenerationRecord> runtimeGenerationRecords =
        new Dictionary<string, RuntimeGenerationRecord>(StringComparer.Ordinal);

    private readonly List<string> runtimeGenerationKeys =
        new List<string>();

    private readonly Dictionary<string, PendingTravelRecord> pendingTravelByPlayerId =
        new Dictionary<string, PendingTravelRecord>(StringComparer.Ordinal);

    private readonly List<string> pendingTravelKeys =
        new List<string>();

    private readonly Dictionary<string, TrackedPlayerContextRecord> trackedPlayerContexts =
        new Dictionary<string, TrackedPlayerContextRecord>(StringComparer.Ordinal);

    private readonly HashSet<string> observedPlayerIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly List<string> trackedPlayerKeys =
        new List<string>();

    private readonly Dictionary<string, DimensionLoadTicket> loadTickets =
        new Dictionary<string, DimensionLoadTicket>(StringComparer.Ordinal);

    private readonly Dictionary<string, RuntimeLoadRecord> runtimeLoadRecords =
        new Dictionary<string, RuntimeLoadRecord>(StringComparer.Ordinal);

    private readonly Dictionary<long, Entity> runtimeSimulationRegionAnchors =
        new Dictionary<long, Entity>();

    private readonly HashSet<long> observedSubMaps =
        new HashSet<long>();

    private readonly HashSet<long> desiredSimulationRegionKeys =
        new HashSet<long>();

    private readonly List<MergedSimulationRegion> mergedSimulationRegions =
        new List<MergedSimulationRegion>();

    private readonly List<int> simulationEdgesX =
        new List<int>();

    private readonly List<int> simulationEdgesY =
        new List<int>();

    private readonly HashSet<long> simulationCells =
        new HashSet<long>();

    private readonly List<long> staleSimulationRegionKeys =
        new List<long>();

    private World serverWorld;
    private World clientWorld;
    private EntityQuery runtimeLoadAnchorQuery;
    private EntityQuery runtimeSimulationRegionQuery;
    private EntityQuery subMapQuery;
    private EntityQuery networkTimeQuery;
    private EntityQuery serverPlayerQuery;
    private bool serverQueriesCreated;
    private double nextRuntimeLoadReconcileAt;
    private double nextRuntimeGenerationTickAt;
    private double nextPlayerContextTrackAt;
    private bool runtimeLoadTopologyDirty;
    private bool accessProviderOrderDirty;
    private bool permissionProviderOrderDirty;
    private bool travelRequirementEvaluatorOrderDirty;

    public NullforgeDimensionService()
    {
      AddContentPackInternal(BuiltInFrameworkContentPack);
      AddDefinitionInternal(OverworldDefinition);
      AddMapLayerInternal(OverworldMapLayerDefinition);
      RegisterBuiltInAccessProviders();
      RegisterBuiltInTravelRequirementEvaluators();
      BindBuiltInContentOwnership();
      RebuildDefinitionSnapshot();
    }

    public event Action<DimensionChangedEvent> PlayerDimensionChanged;

    public event Action<DimensionContentPackChangedEvent> ContentPackChanged;

    public event Action<DimensionContentOwnershipChangedEvent> ContentOwnershipChanged;

    public event Action<DimensionAssetReferenceChangedEvent> AssetReferenceChanged;

    public event Action<DimensionBiomeChangedEvent> BiomeChanged;

    public event Action<DimensionGenerationTableChangedEvent> GenerationTableChanged;

    public event Action<DimensionGenerationTableEntryChangedEvent> GenerationTableEntryChanged;

    public event Action<DimensionPlayerVisitChangedEvent> PlayerVisitChanged;

    public event Action<DimensionLifecycleEvent> DimensionLifecycleChanged;

    public event Action<DimensionTravelSnapshot> DimensionTravelUpdated;

    public event Action<DimensionPortalChangedEvent> PortalChanged;

    public event Action<DimensionPortalPresentationChangedEvent> PortalPresentationChanged;

    public event Action<DimensionTravelRequirementChangedEvent> TravelRequirementChanged;

    public event Action<DimensionGenerationStatus> GenerationStatusChanged;

    public event Action<DimensionGenerationReservationChangedEvent> GenerationReservationChanged;

    public event Action<DimensionGenerationPassChangedEvent> GenerationPassChanged;

    public event Action<DimensionMapLayerChangedEvent> MapLayerChanged;

    public event Action<DimensionMarkerChangedEvent> MarkerChanged;

    public event Action<DimensionAnchorChangedEvent> AnchorChanged;

    public event Action<DimensionZoneChangedEvent> ZoneChanged;

    public event Action<DimensionEnvironmentProfileChangedEvent> EnvironmentProfileChanged;

    public event Action<DimensionSceneChangedEvent> SceneChanged;

    public event Action<DimensionSceneTemplateChangedEvent> SceneTemplateChanged;

    public event Action<DimensionEncounterChangedEvent> EncounterChanged;

    public event Action<DimensionResourceNodeChangedEvent> ResourceNodeChanged;

    public event Action<DimensionSpawnRuleChangedEvent> SpawnRuleChanged;

    public event Action<DimensionWorldEventChangedEvent> WorldEventChanged;

    public event Action<DimensionProgressFlagChangedEvent> ProgressFlagChanged;

    public event Action<DimensionLoadTicketSnapshot> LoadTicketChanged;

    public int ApiVersion
    {
      get { return DimensionApi.CurrentApiVersion; }
    }

    public bool IsReady
    {
      get { return true; }
    }




  }
}
