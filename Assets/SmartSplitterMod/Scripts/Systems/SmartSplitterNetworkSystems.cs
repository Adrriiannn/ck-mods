using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Pug.ECS.Components;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterServerFilterRpcSystem : SystemBase
{
  private EntityQuery _filterRequestQuery;
  private EntityQuery _setFilterQuery;
  private EntityArchetype _stateRpcArchetype;
  private EntityArchetype _powerStateRpcArchetype;

  protected override void OnCreate()
  {
    _filterRequestQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterFilterRequestRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

    _setFilterQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterSetLaneFilterRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

    _stateRpcArchetype = EntityManager.CreateArchetype(
        typeof(SmartSplitterFilterStateRpc),
        typeof(SendRpcCommandRequest));
    _powerStateRpcArchetype = EntityManager.CreateArchetype(
        typeof(SmartSplitterPowerStateRpc),
        typeof(SendRpcCommandRequest));
  }

  protected override void OnUpdate()
  {
    HandleFilterRequests();
    HandleSetFilterRequests();
  }

  private void HandleFilterRequests()
  {
    using NativeArray<Entity> entities = _filterRequestQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<SmartSplitterFilterRequestRpc> requests =
        _filterRequestQuery.ToComponentDataArray<SmartSplitterFilterRequestRpc>(Allocator.Temp);
    using NativeArray<ReceiveRpcCommandRequest> sources =
        _filterRequestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      int2 center = new int2(requests[i].CenterX, requests[i].CenterY);
      SendCurrentState(center, sources[i].SourceConnection);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleSetFilterRequests()
  {
    using NativeArray<Entity> entities = _setFilterQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<SmartSplitterSetLaneFilterRpc> requests =
        _setFilterQuery.ToComponentDataArray<SmartSplitterSetLaneFilterRpc>(Allocator.Temp);
    using NativeArray<ReceiveRpcCommandRequest> sources =
        _setFilterQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      SmartSplitterSetLaneFilterRpc request = requests[i];
      int2 center = new int2(request.CenterX, request.CenterY);

      if (TryApplyRequest(center, request))
      {
        SendCurrentState(center, sources[i].SourceConnection);
      }

      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private bool TryApplyRequest(int2 center, SmartSplitterSetLaneFilterRpc request)
  {
    if (!SmartSplitterLaneFilterUtility.TryFindSmartSplitterAtCenter(
            World,
            center,
            out Entity splitter))
    {
      Debug.LogWarning(
          $"[SmartSplitterNetwork] Ignored filter RPC because no smart splitter exists at center=({center.x},{center.y}).");
      return false;
    }

    if (!TryDecodeLane(request.Lane, out SmartSplitterLane lane) ||
        !TryDecodeFilter(request, out SmartSplitterLaneFilter filter))
    {
      Debug.LogWarning(
          $"[SmartSplitterNetwork] Ignored invalid filter RPC center=({center.x},{center.y}) lane={request.Lane} mode={request.Mode}.");
      return false;
    }

    SmartSplitterLaneFilterUtility.SetLaneFilter(EntityManager, splitter, lane, filter);
    return true;
  }

  private void SendCurrentState(int2 center, Entity targetConnection)
  {
    if (!SmartSplitterLaneFilterUtility.TryFindSmartSplitterAtCenter(
            World,
            center,
            out Entity splitter) ||
        !EntityManager.HasComponent<SmartSplitterLaneFiltersCD>(splitter))
    {
      return;
    }

    SmartSplitterLaneFiltersCD filters =
        EntityManager.GetComponentData<SmartSplitterLaneFiltersCD>(splitter);

    Entity entity = EntityManager.CreateEntity(_stateRpcArchetype);
    EntityManager.SetComponentData(entity, ToStateRpc(center, filters));
    EntityManager.SetComponentData(entity, new SendRpcCommandRequest
    {
      TargetConnection = targetConnection
    });

    if (SmartSplitterNetworkState.TryGetPower(center, out bool powered))
    {
      Entity powerEntity = EntityManager.CreateEntity(_powerStateRpcArchetype);
      EntityManager.SetComponentData(powerEntity, new SmartSplitterPowerStateRpc
      {
        CenterX = center.x,
        CenterY = center.y,
        Powered = powered ? (byte)1 : (byte)0
      });
      EntityManager.SetComponentData(powerEntity, new SendRpcCommandRequest
      {
        TargetConnection = targetConnection
      });
    }
  }

  private static SmartSplitterFilterStateRpc ToStateRpc(
      int2 center,
      SmartSplitterLaneFiltersCD filters)
  {
    return new SmartSplitterFilterStateRpc
    {
      CenterX = center.x,
      CenterY = center.y,
      LeftMode = (byte)filters.Left.Mode,
      LeftObjectID = (int)filters.Left.FilterObject,
      LeftVariation = filters.Left.FilterVariation,
      CenterMode = (byte)filters.Center.Mode,
      CenterObjectID = (int)filters.Center.FilterObject,
      CenterVariation = filters.Center.FilterVariation,
      RightMode = (byte)filters.Right.Mode,
      RightObjectID = (int)filters.Right.FilterObject,
      RightVariation = filters.Right.FilterVariation
    };
  }

  private static bool TryDecodeLane(byte laneValue, out SmartSplitterLane lane)
  {
    lane = (SmartSplitterLane)laneValue;
    return lane == SmartSplitterLane.Left ||
           lane == SmartSplitterLane.Center ||
           lane == SmartSplitterLane.Right;
  }

  private static bool TryDecodeFilter(
      SmartSplitterSetLaneFilterRpc request,
      out SmartSplitterLaneFilter filter)
  {
    return TryDecodeFilter(
        request.Mode,
        request.ObjectID,
        request.Variation,
        out filter);
  }

  private static bool TryDecodeFilter(
      byte modeValue,
      int objectID,
      int variation,
      out SmartSplitterLaneFilter filter)
  {
    filter = SmartSplitterLaneFilterUtility.CreateAnyFilter();

    SmartSplitterLaneFilterMode mode = (SmartSplitterLaneFilterMode)modeValue;
    switch (mode)
    {
      case SmartSplitterLaneFilterMode.Any:
        filter = SmartSplitterLaneFilterUtility.CreateAnyFilter();
        return true;

      case SmartSplitterLaneFilterMode.None:
        filter = SmartSplitterLaneFilterUtility.CreateNoneFilter();
        return true;

      case SmartSplitterLaneFilterMode.Item:
        if (objectID == (int)ObjectID.None)
        {
          return false;
        }

        filter = SmartSplitterLaneFilterUtility.CreateItemFilter((ObjectID)objectID, variation);
        return true;

      default:
        return false;
    }
  }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterClientFilterStateRpcSystem : SystemBase
{
  private EntityQuery _stateQuery;
  private EntityQuery _powerStateQuery;

  protected override void OnCreate()
  {
    _stateQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterFilterStateRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

    _powerStateQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterPowerStateRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
  }

  protected override void OnUpdate()
  {
    HandleFilterStates();
    HandlePowerStates();
  }

  private void HandleFilterStates()
  {
    using NativeArray<Entity> entities = _stateQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<SmartSplitterFilterStateRpc> states =
        _stateQuery.ToComponentDataArray<SmartSplitterFilterStateRpc>(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      SmartSplitterFilterStateRpc state = states[i];
      SmartSplitterNetworkState.RememberFilters(
          new int2(state.CenterX, state.CenterY),
          FromStateRpc(state));
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandlePowerStates()
  {
    using NativeArray<Entity> entities = _powerStateQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<SmartSplitterPowerStateRpc> states =
        _powerStateQuery.ToComponentDataArray<SmartSplitterPowerStateRpc>(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      SmartSplitterPowerStateRpc state = states[i];
      SmartSplitterNetworkState.RememberPower(
          new int2(state.CenterX, state.CenterY),
          state.Powered != 0);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private static SmartSplitterLaneFiltersCD FromStateRpc(SmartSplitterFilterStateRpc state)
  {
    return new SmartSplitterLaneFiltersCD
    {
      Left = DecodeFilter(state.LeftMode, state.LeftObjectID, state.LeftVariation),
      Center = DecodeFilter(state.CenterMode, state.CenterObjectID, state.CenterVariation),
      Right = DecodeFilter(state.RightMode, state.RightObjectID, state.RightVariation)
    };
  }

  private static SmartSplitterLaneFilter DecodeFilter(byte modeValue, int objectID, int variation)
  {
    SmartSplitterLaneFilterMode mode = (SmartSplitterLaneFilterMode)modeValue;
    switch (mode)
    {
      case SmartSplitterLaneFilterMode.Item:
        return objectID == (int)ObjectID.None
            ? SmartSplitterLaneFilterUtility.CreateAnyFilter()
            : SmartSplitterLaneFilterUtility.CreateItemFilter((ObjectID)objectID, variation);

      case SmartSplitterLaneFilterMode.None:
        return SmartSplitterLaneFilterUtility.CreateNoneFilter();

      case SmartSplitterLaneFilterMode.Any:
      default:
        return SmartSplitterLaneFilterUtility.CreateAnyFilter();
    }
  }
}
