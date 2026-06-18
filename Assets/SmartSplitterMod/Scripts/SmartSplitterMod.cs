using PugMod;
using Unity.Entities;
using UnityEngine;

public class SmartSplitterModEntry : IMod
{
  private World _registeredServerWorld;
  private World _registeredClientWorld;

  public void EarlyInit()
  {
    Debug.Log("[SmartSplitterMod] EarlyInit");
  }

  public void Init()
  {
    SmartSplitterPlacementCompatibility.RefreshLoadedMods();

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
      API.Client.OnObjectSpawnedOnClient -= SmartSplitterVisualSwapController.HandleObjectSpawnedOnClient;
      API.Client.OnObjectSpawnedOnClient += SmartSplitterVisualSwapController.HandleObjectSpawnedOnClient;
      API.Client.OnObjectDespawnedOnClient -= SmartSplitterVisualSwapController.HandleObjectDespawnedOnClient;
      API.Client.OnObjectDespawnedOnClient += SmartSplitterVisualSwapController.HandleObjectDespawnedOnClient;
    }

    RegisterServerSystems();
    RegisterClientSystems();

    SmartSplitterAssetRegistry.EnsureExists();
    SmartSplitterVisualSwapController.EnsureExists();
    SmartSplitterFilterPanelHost.EnsureExists();
    Debug.Log("[SmartSplitterMod] Init");
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
      API.Client.OnObjectSpawnedOnClient -= SmartSplitterVisualSwapController.HandleObjectSpawnedOnClient;
      API.Client.OnObjectDespawnedOnClient -= SmartSplitterVisualSwapController.HandleObjectDespawnedOnClient;
    }
  }

  public void ModObjectLoaded(Object obj)
  {
    SmartSplitterAssetRegistry.RegisterLoadedObject(obj);
  }

  public void Update()
  {
    SmartSplitterPersistence.FlushIfDue();
  }

  private void RegisterServerSystems()
  {
    SmartSplitterPlacementCompatibility.RefreshLoadedMods();

    if (API.Server == null ||
        API.Server.World == null ||
        !API.Server.World.IsCreated)
    {
      return;
    }

    World serverWorld = API.Server.World;

    if (_registeredServerWorld == serverWorld)
    {
      Debug.Log("[SmartSplitterMod] Runtime system already registered for this server world; skipping duplicate schedule.");
      return;
    }

    SmartSplitterRuntimeSystem system =
        serverWorld.GetOrCreateSystemManaged<SmartSplitterRuntimeSystem>();
    API.Server.AddScheduledSystem(system);

    SchedulePlacementCompatibilitySystems(serverWorld);

    SmartSplitterServerFilterRpcSystem rpcSystem =
        serverWorld.GetOrCreateSystemManaged<SmartSplitterServerFilterRpcSystem>();
    API.Server.AddScheduledSystem(rpcSystem);

    _registeredServerWorld = serverWorld;
    SmartSplitterPersistence.ResetLoadedState();
    SmartSplitterPersistence.EnsureLoadedForCurrentWorld();
    Debug.Log("[SmartSplitterMod] Registered server runtime/RPC systems in server SimulationSystemGroup");
  }

  private void RegisterClientSystems()
  {
    SmartSplitterPlacementCompatibility.RefreshLoadedMods();

    if (API.Client == null ||
        API.Client.World == null ||
        !API.Client.World.IsCreated)
    {
      return;
    }

    World clientWorld = API.Client.World;

    if (_registeredClientWorld == clientWorld)
    {
      return;
    }

    SmartSplitterClientFilterStateRpcSystem rpcSystem =
        clientWorld.GetOrCreateSystemManaged<SmartSplitterClientFilterStateRpcSystem>();
    API.Client.AddScheduledSystem(rpcSystem);

    SchedulePlacementCompatibilitySystems(clientWorld);

    _registeredClientWorld = clientWorld;
    SmartSplitterNetworkState.Reset();
    Debug.Log("[SmartSplitterMod] Registered client Smart Splitter RPC systems in client SimulationSystemGroup");
  }

  private static void SchedulePlacementCompatibilitySystems(World world)
  {
    SmartSplitterPlacementRequestSystem system =
        world.GetOrCreateSystemManaged<SmartSplitterPlacementRequestSystem>();

    EndPredictedSimulationSystemGroup group =
        world.GetExistingSystemManaged<EndPredictedSimulationSystemGroup>();

    if (group != null)
    {
      group.AddSystemToUpdateList(system);
      group.SortSystems();
    }
    else
    {
      SimulationSystemGroup simulationGroup =
          world.GetExistingSystemManaged<SimulationSystemGroup>();

      if (simulationGroup != null)
      {
        simulationGroup.AddSystemToUpdateList(system);
        simulationGroup.SortSystems();
      }
    }

    SchedulePlacementDirectionSyncSystem(world);
  }

  private static void SchedulePlacementDirectionSyncSystem(World world)
  {
    SmartSplitterPlacementDirectionSyncSystem system =
        world.GetOrCreateSystemManaged<SmartSplitterPlacementDirectionSyncSystem>();

    SimulationSystemGroup simulationGroup =
        world.GetExistingSystemManaged<SimulationSystemGroup>();

    if (simulationGroup != null)
    {
      simulationGroup.AddSystemToUpdateList(system);
      simulationGroup.SortSystems();
    }
  }

  private void OnServerWorldDestroyed()
  {
    SmartSplitterPersistence.ResetLoadedState();
    _registeredServerWorld = null;
  }

  private void OnClientWorldDestroyed()
  {
    SmartSplitterNetworkState.Reset();
    _registeredClientWorld = null;
  }
}
