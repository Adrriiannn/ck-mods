using PugMod;
using Unity.Entities;
using UnityEngine;

public class ConveyorTunnelModEntry : IMod
{
  private World _registeredServerWorld;
  private World _registeredClientWorld;

  public void EarlyInit()
  {
    if (API.Authoring != null)
    {
      API.Authoring.OnObjectTypeAdded -= ConveyorTunnelAdvancedAutomationRecipeInjector.OnObjectTypeAdded;
      API.Authoring.OnObjectTypeAdded += ConveyorTunnelAdvancedAutomationRecipeInjector.OnObjectTypeAdded;
    }

    Debug.Log("[ConveyorTunnelMod] EarlyInit");
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

    ConveyorTunnelIds.TryRefresh();
    RegisterServerSystems();
    RegisterClientSystems();
    ConveyorTunnelPlacementGuideController.EnsureExists();
    ConveyorTunnelConnectionOverlayController.EnsureExists();
    Debug.Log("[ConveyorTunnelMod] Init");
  }

  public void Shutdown()
  {
    if (API.Authoring != null)
    {
      API.Authoring.OnObjectTypeAdded -= ConveyorTunnelAdvancedAutomationRecipeInjector.OnObjectTypeAdded;
    }

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

    ConveyorTunnelIds.Reset();
    ConveyorTunnelNetworkState.Reset();
    ConveyorTunnelVisual.ClearRegisteredTunnels();
    ConveyorTunnelHelperSpriteRegistry.Clear();
    ConveyorTunnelItemVisualEffects.Clear();
    ConveyorTunnelPlacementGuideController.Clear();
    ConveyorTunnelConnectionOverlayController.Clear();
    ConveyorTunnelPersistence.ResetLoadedState();
    _registeredServerWorld = null;
    _registeredClientWorld = null;
  }

  public void ModObjectLoaded(Object obj)
  {
    ConveyorTunnelIds.TryRefresh();
    ConveyorTunnelHelperSpriteRegistry.RegisterLoadedObject(obj);
    ConveyorTunnelVisual.RegisterLoadedObject(obj);
  }

  public void Update()
  {
    ConveyorTunnelItemVisualEffects.Update();
    ConveyorTunnelPersistence.FlushIfDue();
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
      return;
    }

    ConveyorTunnelRuntimeSystem runtimeSystem =
        serverWorld.GetOrCreateSystemManaged<ConveyorTunnelRuntimeSystem>();
    API.Server.AddScheduledSystem(runtimeSystem);

    ConveyorTunnelServerStateRpcSystem rpcSystem =
        serverWorld.GetOrCreateSystemManaged<ConveyorTunnelServerStateRpcSystem>();
    API.Server.AddScheduledSystem(rpcSystem);

    ConveyorTunnelNetworkState.ResetAuthoritativeState();
    ConveyorTunnelPersistence.EnsureLoadedForCurrentWorld();

    _registeredServerWorld = serverWorld;
    Debug.Log("[ConveyorTunnelMod] Registered server runtime/RPC systems");
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

    ConveyorTunnelClientStateRpcSystem rpcSystem =
        clientWorld.GetOrCreateSystemManaged<ConveyorTunnelClientStateRpcSystem>();
    API.Client.AddScheduledSystem(rpcSystem);

    _registeredClientWorld = clientWorld;
    ConveyorTunnelNetworkState.ResetClientState();
    ConveyorTunnelVisual.ClearRegisteredTunnels();
    ConveyorTunnelPlacementGuideController.EnsureExists();
    ConveyorTunnelConnectionOverlayController.EnsureExists();
    ConveyorTunnelNetworkState.RequestSnapshot(force: true);
    Debug.Log("[ConveyorTunnelMod] Registered client visual/RPC systems");
  }

  private void OnServerWorldDestroyed()
  {
    ConveyorTunnelPersistence.ResetLoadedState();
    ConveyorTunnelNetworkState.ResetAuthoritativeState();
    _registeredServerWorld = null;
  }

  private void OnClientWorldDestroyed()
  {
    ConveyorTunnelNetworkState.ResetClientState();
    ConveyorTunnelVisual.ClearRegisteredTunnels();
    ConveyorTunnelItemVisualEffects.Clear();
    ConveyorTunnelPlacementGuideController.Hide();
    ConveyorTunnelConnectionOverlayController.Hide();
    _registeredClientWorld = null;
  }
}
