using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Generation;
using ExpandNullforge.Networking;
using ExpandNullforge.Persistence;
using ExpandNullforge.Portals;
using ExpandNullforge.UI;
using PlayerEquipment;
using PugMod;
using Unity.Entities;
using UnityEngine;

public sealed class ExpandNullforgeModEntry : IMod
{
  private const string ProviderId = "expandnullforge";
  private const double ReturnPortalSpawnerWakeProbeSeconds = 1.0d;

  private static readonly NullforgeDimensionService DimensionService = new NullforgeDimensionService();
  private static readonly DimensionSafePlatformGenerationProvider SafePlatformGenerationProvider =
      new DimensionSafePlatformGenerationProvider();
  private static readonly DimensionTileMapGenerationProvider TileMapGenerationProvider =
      new DimensionTileMapGenerationProvider();

  private World registeredServerWorld;
  private World registeredClientWorld;
  private DimensionReturnPortalSpawnSystem returnPortalSpawnSystem;
  private double nextReturnPortalSpawnerWakeProbeAt;
  private bool serverWorldPersistenceInitialized;

  public void EarlyInit()
  {
    if (API.Authoring != null)
    {
      API.Authoring.OnObjectTypeAdded -= DimensionPortalRecipeInjector.OnObjectTypeAdded;
      API.Authoring.OnObjectTypeAdded += DimensionPortalRecipeInjector.OnObjectTypeAdded;
    }

    DimensionApi.RegisterService(ProviderId, DimensionService);
    RegisterFrameworkGenerationProviders();
    DimensionFrameworkLog.Verbose("[ExpandNullforge] Dimension API service registered.");
  }

  public void Init()
  {
    // The instantaneous item portal (V2) is triggered by a Harmony patch on the base
    // EquipmentSlot.UpdateEquipment, which runs inside a Burst-compiled equipment-update job. Disable
    // Burst for that system so the managed patch actually fires (see DimensionItemPortalUseHook and
    // DimensionEquipmentUpdateForceJobCompletePatch). Same workaround as the TeleportAfterEating example.
    BurstDisabler.DisableBurstForSystem<EquipmentUpdateSystem>();

    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterServerWorld;
      API.Server.OnWorldCreated += RegisterServerWorld;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
      API.Server.OnWorldDestroyed += OnServerWorldDestroyed;
    }

    if (API.Client != null)
    {
      API.Client.OnWorldCreated -= RegisterClientWorld;
      API.Client.OnWorldCreated += RegisterClientWorld;
      API.Client.OnWorldDestroyed -= OnClientWorldDestroyed;
      API.Client.OnWorldDestroyed += OnClientWorldDestroyed;
    }

    RegisterServerWorld();
    RegisterClientWorld();
    DimensionFrameworkLog.Verbose("[ExpandNullforge] Init");
  }

  public void Shutdown()
  {
    if (API.Authoring != null)
    {
      API.Authoring.OnObjectTypeAdded -= DimensionPortalRecipeInjector.OnObjectTypeAdded;
    }

    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterServerWorld;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
    }

    if (API.Client != null)
    {
      API.Client.OnWorldCreated -= RegisterClientWorld;
      API.Client.OnWorldDestroyed -= OnClientWorldDestroyed;
    }

    OnServerWorldDestroyed();
    OnClientWorldDestroyed();
    SafePlatformGenerationProvider.ClearJobs();
    TileMapGenerationProvider.ClearJobs();
    DimensionApi.UnregisterService(ProviderId);
    DimensionPortalRecipeInjector.Reset();
    DimensionPortalObjectIdCache.Clear();
    DimensionFrameworkLog.Verbose("[ExpandNullforge] Shutdown");
  }

  public void ModObjectLoaded(Object obj)
  {
    // Tileset assets shipped in the framework's own bundle register here; consumer-mod
    // tilesets register through the generated bootstrap's identical branch.
    ExpandNullforge.Authoring.DimensionTilesetAsset tilesetAsset =
        obj as ExpandNullforge.Authoring.DimensionTilesetAsset;
    if (tilesetAsset != null)
    {
      ExpandNullforge.Tilesets.DimensionTilesetAssetRuntime.Register(tilesetAsset);
    }
  }

  public void Update()
  {
    // Core Keeper's real adaptive tileset lookup isn't shipped to the SDK, so it has to be read
    // in-game and baked into DimensionTilesetAtlasData. That bake is done, so the capture stays
    // quiet — re-dumping the whole atlas into the player log every launch when we already have the
    // answer buries everything else. It runs again only if the bake ever comes up empty, which is
    // what a Core Keeper update that invalidates the layout would look like.
    // Core Keeper's real adaptive tileset lookup isn't shipped to the SDK, so it has to be read
    // in-game and baked into DimensionTilesetAtlasData. That bake is done, so the capture stays
    // quiet — re-dumping the whole atlas into the player log every launch when we already have the
    // answer buries everything else. It runs again only if the bake ever comes up empty, which is
    // what a Core Keeper update that invalidates the layout would look like.
    if (!ExpandNullforge.Tilesets.DimensionTilesetAtlas.IsReady)
    {
      ExpandNullforge.Tilesets.DimensionTilesetAtlasCapture.TryCaptureOnce();
    }

    if (DimensionService.HasAttachedServerWorld)
    {
      TryInitializeServerWorldPersistence();

      if (DimensionService.HasActiveRuntimeLoadingWork)
      {
        DimensionService.UpdateRuntimeLoading();
      }

      if (DimensionService.HasActiveRuntimeGenerationWork)
      {
        DimensionService.UpdateRuntimeGeneration();
      }

      if (DimensionService.HasActiveRuntimeTravelWork)
      {
        DimensionService.UpdateRuntimeTravel();
      }

      if (DimensionService.ShouldRunRuntimePlayerContextTracking ||
          DimensionReturnPortalSpawnRegistry.Count > 0)
      {
        DimensionService.UpdateRuntimePlayerContexts();
      }

      WakeReturnPortalSpawnerIfNeeded();

      if (DimensionService.ShouldFlushPersistence)
      {
        DimensionWorldRegistry.FlushIfDue();
      }
    }

    if (DimensionService.HasAttachedClientWorld)
    {
      DimensionCoordinatePresentation.EnsureAttached();
      DimensionTravelNetworkState.UpdateDeferredClientRequests();

      if (DimensionPlayerContextNetworkState.IsCurrentContextHydrationActive)
      {
        DimensionPlayerContextNetworkState.UpdateCurrentContextHydration();
      }
    }
  }

  /// <summary>
  /// Registers a world with the Burst disabler, so <c>EquipmentUpdateSystem</c> actually runs
  /// un-Bursted there.
  /// </summary>
  /// <remarks>
  /// <c>DisableBurstForSystem&lt;T&gt;</c> in Init only registers the system TYPE. For an unmanaged
  /// system the disabler still has to resolve that type to each world's own SystemHandle, which is
  /// what <c>AddWorld</c> does — and a world nobody registers keeps running the system Burst-compiled.
  /// That silently defeats every managed patch on the placement path: on a dedicated server the tile
  /// write reached vanilla's untouched <c>EntityUtility.AddTile</c>, which rejects any tileset id
  /// above 74 ("Trying to add invalid tileset 45378 for tileType 35"), so placed custom blocks were
  /// never created server-side and the client's prediction was simply corrected away.
  /// Cheap and idempotent — the disabler stores handles in a HashSet.
  /// </remarks>
  private static void EnsureBurstDisabledForWorld(World world)
  {
    if (world == null || !world.IsCreated)
    {
      return;
    }

    BurstDisabler.AddWorld(world);
    UnityEngine.Debug.Log(
        "[NF_TILESET] Registered " + world.Name +
        " with the Burst disabler so tile-write patches apply there.");
  }

  private void RegisterServerWorld()
  {
    if (API.Server == null ||
        API.Server.World == null ||
        !API.Server.World.IsCreated)
    {
      return;
    }

    World world = API.Server.World;
    if (registeredServerWorld == world)
    {
      return;
    }

    registeredServerWorld = world;
    serverWorldPersistenceInitialized = false;
    EnsureBurstDisabledForWorld(world);
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.EnsureSystemOrdering(world);
    world.GetOrCreateSystemManaged<DimensionTravelServerRpcSystem>();
    world.GetOrCreateSystemManaged<DimensionPlayerContextServerRpcSystem>();
    DimensionReturnPortalSpawnSystem returnPortalSpawnSystem =
        world.GetOrCreateSystemManaged<DimensionReturnPortalSpawnSystem>();
    returnPortalSpawnSystem.Enabled = false;
    this.returnPortalSpawnSystem = returnPortalSpawnSystem;
    nextReturnPortalSpawnerWakeProbeAt = 0.0d;
    world.GetOrCreateSystemManaged<DimensionPortalHydrationSystem>();
    world.GetOrCreateSystemManaged<DimensionPortalChargeSystem>();
    world.GetOrCreateSystemManaged<DimensionPortalActivationSystem>();
    world.GetOrCreateSystemManaged<DimensionItemPortalSpawnSystem>();
    DimensionService.SetServerWorld(world);
    RegisterFrameworkGenerationProviders();
    TryInitializeServerWorldPersistence();
    DimensionFrameworkLog.Verbose("[ExpandNullforge] Dimension API bound to server world.");
  }

  private static void RegisterFrameworkGenerationProviders()
  {
    RegisterGenerationProvider(SafePlatformGenerationProvider, "safe-platform");
    RegisterGenerationProvider(TileMapGenerationProvider, "tile-map");
  }

  private static void RegisterGenerationProvider(
      IDimensionGenerationProvider provider,
      string label)
  {
    if (!DimensionService.TryRegisterGenerationProvider(provider, out DimensionOperationResult result) &&
        !string.Equals(result.Code, "generation-provider-duplicate", System.StringComparison.Ordinal))
    {
      DimensionFrameworkLog.Warning(
          "[ExpandNullforge] Could not register " + label + " generation provider: " +
          result.Message);
    }
  }

  private void WakeReturnPortalSpawnerIfNeeded()
  {
    if (registeredServerWorld == null || !registeredServerWorld.IsCreated)
    {
      return;
    }

    if (returnPortalSpawnSystem == null)
    {
      returnPortalSpawnSystem =
          registeredServerWorld.GetOrCreateSystemManaged<DimensionReturnPortalSpawnSystem>();
    }

    if (returnPortalSpawnSystem.Enabled)
    {
      return;
    }

    if (DimensionService.HasActiveRuntimeTravelWork ||
        DimensionService.HasActiveRuntimeGenerationWork)
    {
      returnPortalSpawnSystem.Enabled = true;
      return;
    }

    double now = Time.realtimeSinceStartupAsDouble;
    if (now < nextReturnPortalSpawnerWakeProbeAt)
    {
      return;
    }

    nextReturnPortalSpawnerWakeProbeAt = now + ReturnPortalSpawnerWakeProbeSeconds;
    if (!HasTrackedPlayerInAnyReturnPortalSource(DimensionService))
    {
      return;
    }

    returnPortalSpawnSystem.Enabled = true;
  }

  private static bool HasTrackedPlayerInAnyReturnPortalSource(
      IDimensionRuntimeStateService runtimeState)
  {
    if (runtimeState == null)
    {
      return false;
    }

    for (int i = 0; i < DimensionReturnPortalSpawnRegistry.Count; i++)
    {
      DimensionReturnPortalSpawnDefinition definition;
      if (!DimensionReturnPortalSpawnRegistry.TryGet(i, out definition) ||
          !definition.IsValid)
      {
        continue;
      }

      if (runtimeState.HasTrackedPlayerInDimension(definition.SourceDimensionId))
      {
        return true;
      }
    }

    return false;
  }

  private void RegisterClientWorld()
  {
    if (API.Client == null ||
        API.Client.World == null ||
        !API.Client.World.IsCreated)
    {
      return;
    }

    World world = API.Client.World;
    if (registeredClientWorld == world)
    {
      return;
    }

    registeredClientWorld = world;
    EnsureBurstDisabledForWorld(world);
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.EnsureSystemOrdering(world);
    world.GetOrCreateSystemManaged<DimensionTravelClientRpcSystem>();
    world.GetOrCreateSystemManaged<DimensionPlayerContextClientRpcSystem>();
    world.GetOrCreateSystemManaged<DimensionPortalMapMarkerScopeSystem>();
    DimensionTravelFeedbackState.Initialize();
    DimensionService.SetClientWorld(world);
    DimensionPlayerContextNetworkState.StartCurrentContextHydration(
        true,
        "Client world attached; hydrate current dimension context.");
    DimensionCoordinatePresentation.EnsureAttached();
    DimensionFrameworkLog.Verbose("[ExpandNullforge] Dimension API bound to client world.");
  }

  private void OnServerWorldDestroyed()
  {
    DimensionPortalRecipeInjector.ClearWorldState("server world destroyed");
    DimensionPortalObjectIdCache.Clear();
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.Clear();
    DimensionPortalRuntime.Reset();
    DimensionService.PreparePersistenceForWorldUnload("server world destroyed");
    registeredServerWorld = null;
    returnPortalSpawnSystem = null;
    nextReturnPortalSpawnerWakeProbeAt = 0.0d;
    serverWorldPersistenceInitialized = false;
    DimensionService.ClearServerWorld();
    DimensionWorldRegistry.ResetLoadedState();
  }

  private void OnClientWorldDestroyed()
  {
    DimensionPortalRecipeInjector.ClearWorldState("client world destroyed");
    ExpandNullforge.Tilesets.DimensionCustomTileRescue.Clear();
    DimensionPortalRuntime.Reset();
    registeredClientWorld = null;
    DimensionService.ClearClientWorld();
    DimensionTravelFeedbackState.Reset();
    DimensionTravelNetworkState.Reset();
    DimensionPlayerContextNetworkState.Reset();
    DimensionCoordinatePresentation.Reset();
  }

  private void TryInitializeServerWorldPersistence()
  {
    if (serverWorldPersistenceInitialized ||
        registeredServerWorld == null ||
        !registeredServerWorld.IsCreated)
    {
      return;
    }

    DimensionWorldRegistry.EnsureLoadedForCurrentWorld();
    if (!DimensionWorldRegistry.IsLoaded)
    {
      return;
    }

    DimensionService.LoadPersistedDefinitionsForCurrentWorld();
    serverWorldPersistenceInitialized = true;
    DimensionFrameworkLog.Verbose(
        "[ExpandNullforge] Dimension API persistence initialized for world=" +
        DimensionWorldRegistry.WorldKey +
        ".");
  }
}
