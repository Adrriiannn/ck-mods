using PugMod;
using Unity.Entities;
using UnityEngine;

public class SmartSplitterMod : IMod
{
  private World _registeredServerWorld;

  public void EarlyInit()
  {
    Debug.Log("[SmartSplitterMod] EarlyInit");
  }

  public void Init()
  {
    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterRuntimeSystem;
      API.Server.OnWorldCreated += RegisterRuntimeSystem;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
      API.Server.OnWorldDestroyed += OnServerWorldDestroyed;
    }

    RegisterRuntimeSystem();

    SmartSplitterAssetRegistry.EnsureExists();
    SmartSplitterVisualSwapController.EnsureExists();
    SmartSplitterFilterPanelHost.EnsureExists();
    SmartSplitterLaneFilterVerticalSliceController.EnsureExists();
    Debug.Log("[SmartSplitterMod] Init");
  }

  public void Shutdown()
  {
    if (API.Server != null)
    {
      API.Server.OnWorldCreated -= RegisterRuntimeSystem;
      API.Server.OnWorldDestroyed -= OnServerWorldDestroyed;
    }
  }

  public void ModObjectLoaded(Object obj)
  {
    SmartSplitterAssetRegistry.RegisterLoadedObject(obj);
  }

  public void Update()
  {
  }

  private void RegisterRuntimeSystem()
  {
    if (API.Server == null ||
        API.Server.World == null ||
        !API.Server.World.IsCreated)
    {
      return;
    }

    World serverWorld = API.Server.World;
    SmartSplitterRuntimeSystem system =
        serverWorld.GetOrCreateSystemManaged<SmartSplitterRuntimeSystem>();
    API.Server.AddScheduledSystem(system);

    if (_registeredServerWorld != serverWorld)
    {
      _registeredServerWorld = serverWorld;
      Debug.Log("[SmartSplitterMod] Registered SmartSplitterRuntimeSystem in server SimulationSystemGroup");
    }
  }

  private void OnServerWorldDestroyed()
  {
    _registeredServerWorld = null;
  }
}
