using PugMod;
using Unity.Entities;
using UnityEngine;

public sealed class ChunkLoaderModEntry : IMod
{
  private World _registeredServerWorld;
  private World _registeredClientWorld;

  public void EarlyInit()
  {
    ChunkLoaderSettings.EnsureLoaded();
    ChunkLoaderToolkitUi.Initialize(this);
    Debug.Log("[ChunkLoaderMod] EarlyInit");
  }

  public void Init()
  {
    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterServerSystems;
      API.Server.OnWorldCreated += RegisterServerSystems;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
      API.Server.OnWorldDestroyed += OnServerWorldDestroyed;
    }

    if (API.Client != null)
    {
      API.Client.OnWorldCreated -= RegisterClientSystems;
      API.Client.OnWorldCreated += RegisterClientSystems;
      API.Client.OnWorldDestroyed -= OnClientWorldDestroyed;
      API.Client.OnWorldDestroyed += OnClientWorldDestroyed;
    }

    RegisterServerSystems();
    RegisterClientSystems();
    Debug.Log("[ChunkLoaderMod] Init");
  }

  public void Shutdown()
  {
    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterServerSystems;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
    }

    if (API.Client != null)
    {
      API.Client.OnWorldCreated -= RegisterClientSystems;
      API.Client.OnWorldDestroyed -= OnClientWorldDestroyed;
    }

    ChunkLoaderRegistry.ResetLoadedState();
    ChunkLoaderTelemetry.Reset();
    ChunkLoaderNetworkState.Reset();
    ChunkLoaderDetailsState.Reset();
    ChunkLoaderSimulationRegions.Reset();
    ChunkLoaderCompatibility.Reset();
    ChunkLoaderSettings.Reset();
    ChunkLoaderToolkitUi.Shutdown();
    if (ChunkLoaderMapUiHost.Instance != null)
    {
      Object.Destroy(ChunkLoaderMapUiHost.Instance.gameObject);
    }
    _registeredServerWorld = null;
    _registeredClientWorld = null;
  }

  public void ModObjectLoaded(Object obj)
  {
  }

  public void Update()
  {
    ChunkLoaderRegistry.FlushIfDue();
    ChunkLoaderTelemetry.FlushIfDue();
  }

  private void RegisterServerSystems()
  {
    if (API.Server == null ||
        API.Server.World == null ||
        !API.Server.World.IsCreated)
    {
      return;
    }

    World world = API.Server.World;
    if (_registeredServerWorld == world)
    {
      return;
    }

    ChunkLoaderCompatibility.Reset();
    ChunkLoaderSettings.EnsureLoaded();
    ChunkLoaderCompatibility.ValidateCore(world);
    ChunkLoaderRegistry.ResetLoadedState();
    ChunkLoaderRegistry.EnsureLoadedForCurrentWorld();
    ChunkLoaderSimulationRegions.Reset();

    ChunkLoaderRuntimeSystem runtimeSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderRuntimeSystem>();
    API.Server.AddScheduledSystem(runtimeSystem);

    ChunkLoaderPeriodicRespawnSystem respawnSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderPeriodicRespawnSystem>();
    API.Server.AddScheduledSystem(respawnSystem);

    ChunkLoaderPrepareSpawnersSystem prepareSpawnersSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderPrepareSpawnersSystem>();
    API.Server.AddScheduledSystem(prepareSpawnersSystem);

    ChunkLoaderRestoreSpawnersSystem restoreSpawnersSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderRestoreSpawnersSystem>();
    API.Server.AddScheduledSystem(restoreSpawnersSystem);

    ChunkLoaderTemporaryEntityRetentionSystem retentionSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderTemporaryEntityRetentionSystem>();
    API.Server.AddScheduledSystem(retentionSystem);

    ChunkLoaderTelemetrySystem telemetrySystem =
        world.GetOrCreateSystemManaged<ChunkLoaderTelemetrySystem>();
    API.Server.AddScheduledSystem(telemetrySystem);

    ChunkLoaderDetailsServerSystem detailsSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderDetailsServerSystem>();
    API.Server.AddScheduledSystem(detailsSystem);

    ChunkLoaderServerRpcSystem rpcSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderServerRpcSystem>();
    API.Server.AddScheduledSystem(rpcSystem);

    _registeredServerWorld = world;
    Debug.Log("[ChunkLoaderMod] Registered server registry, RPC, and runtime systems.");
  }

  private void RegisterClientSystems()
  {
    if (API.Client == null ||
        API.Client.World == null ||
        !API.Client.World.IsCreated)
    {
      return;
    }

    World world = API.Client.World;
    if (_registeredClientWorld == world)
    {
      return;
    }

    ChunkLoaderNetworkState.Reset();
    ChunkLoaderDetailsState.Reset();
    ChunkLoaderClientRpcSystem rpcSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderClientRpcSystem>();
    API.Client.AddScheduledSystem(rpcSystem);

    ChunkLoaderDetailsClientSystem detailsSystem =
        world.GetOrCreateSystemManaged<ChunkLoaderDetailsClientSystem>();
    API.Client.AddScheduledSystem(detailsSystem);

    _registeredClientWorld = world;
    ChunkLoaderMapUiHost.EnsureExists();
    Debug.Log("[ChunkLoaderMod] Registered client RPC system.");
  }

  private void OnServerWorldDestroyed()
  {
    ChunkLoaderRegistry.ResetLoadedState();
    ChunkLoaderTelemetry.Reset();
    ChunkLoaderSimulationRegions.Reset();
    ChunkLoaderCompatibility.Reset();
    _registeredServerWorld = null;
  }

  private void OnClientWorldDestroyed()
  {
    ChunkLoaderNetworkState.Reset();
    ChunkLoaderDetailsState.Reset();
    ChunkLoaderMapUiHost.Instance?.ResetForWorldChange();
    _registeredClientWorld = null;
  }
}
