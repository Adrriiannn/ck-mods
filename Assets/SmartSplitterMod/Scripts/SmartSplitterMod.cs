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

    _registeredClientWorld = clientWorld;
    SmartSplitterNetworkState.Reset();
    Debug.Log("[SmartSplitterMod] Registered client Smart Splitter RPC systems in client SimulationSystemGroup");
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
