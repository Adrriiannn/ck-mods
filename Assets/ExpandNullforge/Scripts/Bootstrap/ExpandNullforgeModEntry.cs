using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Generation;
using ExpandNullforge.Networking;
using ExpandNullforge.Persistence;
using ExpandNullforge.Portals;
using ExpandNullforge.UI;
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
    DimensionApi.UnregisterService(ProviderId);
    DimensionPortalRecipeInjector.Reset();
    DimensionPortalObjectIdCache.Clear();
    DimensionFrameworkLog.Verbose("[ExpandNullforge] Shutdown");
  }

  public void ModObjectLoaded(Object obj)
  {
  }

  public void Update()
  {
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
    DimensionService.SetServerWorld(world);
    RegisterFrameworkGenerationProviders();
    TryInitializeServerWorldPersistence();
    DimensionFrameworkLog.Verbose("[ExpandNullforge] Dimension API bound to server world.");
  }

  private static void RegisterFrameworkGenerationProviders()
  {
    DimensionOperationResult result;
    if (!DimensionService.TryRegisterGenerationProvider(
        SafePlatformGenerationProvider,
        out result) &&
        !string.Equals(result.Code, "generation-provider-duplicate", System.StringComparison.Ordinal))
    {
      DimensionFrameworkLog.Warning(
          "[ExpandNullforge] Could not register safe-platform generation provider: " +
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
