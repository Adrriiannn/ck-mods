using System;
using System.Collections.Generic;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

public struct ConveyorTunnelEndpointVisualState
{
  public int PairId;
  public ConveyorTunnelEndpointRole Role;
  public byte Linked;
  public byte Pending;
  public byte HasPayloads;
  public int2 Direction;
  public int2 PairedTile;
}

public readonly struct ConveyorTunnelConnectionVisual
{
  public ConveyorTunnelConnectionVisual(
      int pairId,
      int2 entranceTile,
      int2 exitTile,
      int2 direction)
  {
    PairId = pairId;
    EntranceTile = entranceTile;
    ExitTile = exitTile;
    Direction = direction;
  }

  public readonly int PairId;
  public readonly int2 EntranceTile;
  public readonly int2 ExitTile;
  public readonly int2 Direction;
}

public readonly struct ConveyorTunnelEndpointStateRecord
{
  public ConveyorTunnelEndpointStateRecord(
      int2 tile,
      ConveyorTunnelEndpointVisualState state)
  {
    Tile = tile;
    State = state;
  }

  public readonly int2 Tile;
  public readonly ConveyorTunnelEndpointVisualState State;
}

public readonly struct ConveyorTunnelItemVisualEffectRecord
{
  public ConveyorTunnelItemVisualEffectRecord(
      int eventId,
      ConveyorTunnelItemVisualEffectKind kind,
      float2 point)
  {
    EventId = eventId;
    Kind = kind;
    Point = point;
  }

  public readonly int EventId;
  public readonly ConveyorTunnelItemVisualEffectKind Kind;
  public readonly float2 Point;
}

public static class ConveyorTunnelNetworkState
{
  private const double SnapshotRequestCooldownSeconds = 1.0d;
  private const int MaxRememberedItemVisualEffectIds = 512;

  private static readonly Dictionary<long, ConveyorTunnelEndpointVisualState> EndpointStatesByTile =
      new Dictionary<long, ConveyorTunnelEndpointVisualState>();
  private static readonly Dictionary<long, ConveyorTunnelEndpointVisualState> AuthoritativeEndpointStatesByTile =
      new Dictionary<long, ConveyorTunnelEndpointVisualState>();
  private static readonly Dictionary<long, ConveyorTunnelEndpointVisualState> AuthoritativeEndpointChangesByTile =
      new Dictionary<long, ConveyorTunnelEndpointVisualState>();
  private static readonly List<ConveyorTunnelItemVisualEffectRecord> AuthoritativeItemVisualEffects =
      new List<ConveyorTunnelItemVisualEffectRecord>();
  private static readonly HashSet<int> ClientItemVisualEffectIds = new HashSet<int>();
  private static readonly Queue<int> ClientItemVisualEffectIdOrder = new Queue<int>();
  private static readonly List<int2> ClientResetTiles = new List<int2>();

  private static bool _hasReceivedSnapshot;
  private static double _lastSnapshotRequestAt = double.NegativeInfinity;
  private static int _nextItemVisualEffectId;

  public static event Action<int2, ConveyorTunnelEndpointVisualState> EndpointStateChanged;

  public static bool HasReceivedSnapshot => _hasReceivedSnapshot;

  public static void Reset()
  {
    ResetClientState();
    ResetAuthoritativeState();
  }

  public static void ResetClientState()
  {
    ClearClientEndpointStates();
    ClientItemVisualEffectIds.Clear();
    ClientItemVisualEffectIdOrder.Clear();
    _hasReceivedSnapshot = false;
    _lastSnapshotRequestAt = double.NegativeInfinity;
  }

  public static void ResetAuthoritativeState()
  {
    AuthoritativeEndpointStatesByTile.Clear();
    AuthoritativeEndpointChangesByTile.Clear();
    AuthoritativeItemVisualEffects.Clear();
    _nextItemVisualEffectId = 0;
  }

  public static void BeginClientSnapshot()
  {
    ClearClientEndpointStates();
    _hasReceivedSnapshot = false;
  }

  public static void CompleteClientSnapshot()
  {
    _hasReceivedSnapshot = true;
  }

  public static bool TryGetEndpointState(
      int2 tile,
      out ConveyorTunnelEndpointVisualState state)
  {
    return EndpointStatesByTile.TryGetValue(GetKey(tile), out state);
  }

  public static bool TryGetConnectionForEndpoint(
      int2 tile,
      out ConveyorTunnelConnectionVisual connection)
  {
    connection = default;
    if (!TryGetEndpointState(tile, out ConveyorTunnelEndpointVisualState state) ||
        state.Linked == 0 ||
        state.Role == ConveyorTunnelEndpointRole.None ||
        state.Direction.Equals(int2.zero) ||
        state.PairedTile.Equals(tile))
    {
      return false;
    }

    int2 entranceTile = state.Role == ConveyorTunnelEndpointRole.Entrance
        ? tile
        : state.PairedTile;
    int2 exitTile = state.Role == ConveyorTunnelEndpointRole.Exit
        ? tile
        : state.PairedTile;

    connection = new ConveyorTunnelConnectionVisual(
        state.PairId,
        entranceTile,
        exitTile,
        state.Direction);
    return true;
  }

  public static void GetConnections(
      List<ConveyorTunnelConnectionVisual> connections)
  {
    connections.Clear();
    foreach (KeyValuePair<long, ConveyorTunnelEndpointVisualState> entry
             in EndpointStatesByTile)
    {
      ConveyorTunnelEndpointVisualState state = entry.Value;
      if (state.Linked == 0 ||
          state.Role != ConveyorTunnelEndpointRole.Entrance ||
          state.Direction.Equals(int2.zero))
      {
        continue;
      }

      int2 entranceTile = TileFromKey(entry.Key);
      if (state.PairedTile.Equals(entranceTile))
      {
        continue;
      }

      connections.Add(new ConveyorTunnelConnectionVisual(
          state.PairId,
          entranceTile,
          state.PairedTile,
          state.Direction));
    }
  }

  public static void GetPendingEndpoints(
      List<ConveyorTunnelPendingEndpoint> pendingEndpoints)
  {
    pendingEndpoints.Clear();
    foreach (KeyValuePair<long, ConveyorTunnelEndpointVisualState> entry
             in EndpointStatesByTile)
    {
      ConveyorTunnelEndpointVisualState state = entry.Value;
      if (state.Role == ConveyorTunnelEndpointRole.None ||
          state.Linked != 0 ||
          state.Pending == 0 ||
          state.Direction.Equals(int2.zero))
      {
        continue;
      }

      pendingEndpoints.Add(new ConveyorTunnelPendingEndpoint(
          TileFromKey(entry.Key),
          state.Direction));
    }
  }

  public static void RememberEndpointState(
      int2 tile,
      ConveyorTunnelEndpointVisualState state)
  {
    long key = GetKey(tile);

    if (state.Role == ConveyorTunnelEndpointRole.None)
    {
      if (EndpointStatesByTile.Remove(key))
      {
        EndpointStateChanged?.Invoke(tile, state);
      }

      return;
    }

    bool changed =
        !EndpointStatesByTile.TryGetValue(
            key,
            out ConveyorTunnelEndpointVisualState previous) ||
        !EndpointStatesEqual(previous, state);

    EndpointStatesByTile[key] = state;

    if (changed)
    {
      EndpointStateChanged?.Invoke(tile, state);
    }
  }

  public static void RememberAuthoritativeEndpointState(
      int2 tile,
      ConveyorTunnelEndpointVisualState state)
  {
    long key = GetKey(tile);
    bool changed;

    if (state.Role == ConveyorTunnelEndpointRole.None)
    {
      changed = AuthoritativeEndpointStatesByTile.Remove(key);
    }
    else
    {
      changed =
          !AuthoritativeEndpointStatesByTile.TryGetValue(
              key,
              out ConveyorTunnelEndpointVisualState previous) ||
          !EndpointStatesEqual(previous, state);

      AuthoritativeEndpointStatesByTile[key] = state;
    }

    if (changed)
    {
      AuthoritativeEndpointChangesByTile[key] = state;
    }

    // A listen server shares the process with its client. Keep that path immediate while
    // the RPC remains the source of truth for remote clients.
    RememberEndpointState(tile, state);
  }

  public static void GetAuthoritativeEndpointSnapshot(
      List<ConveyorTunnelEndpointStateRecord> records)
  {
    records.Clear();
    foreach (KeyValuePair<long, ConveyorTunnelEndpointVisualState> entry
             in AuthoritativeEndpointStatesByTile)
    {
      records.Add(new ConveyorTunnelEndpointStateRecord(
          TileFromKey(entry.Key),
          entry.Value));
    }
  }

  public static void DrainAuthoritativeEndpointChanges(
      List<ConveyorTunnelEndpointStateRecord> records)
  {
    records.Clear();
    foreach (KeyValuePair<long, ConveyorTunnelEndpointVisualState> entry
             in AuthoritativeEndpointChangesByTile)
    {
      records.Add(new ConveyorTunnelEndpointStateRecord(
          TileFromKey(entry.Key),
          entry.Value));
    }

    AuthoritativeEndpointChangesByTile.Clear();
  }

  public static void PublishItemVisualEffect(
      ConveyorTunnelItemVisualEffectKind kind,
      float2 point)
  {
    int eventId = unchecked(++_nextItemVisualEffectId);
    if (eventId == 0)
    {
      eventId = unchecked(++_nextItemVisualEffectId);
    }

    ConveyorTunnelItemVisualEffectRecord record =
        new ConveyorTunnelItemVisualEffectRecord(eventId, kind, point);
    AuthoritativeItemVisualEffects.Add(record);

    // Listen-server clients share this static state with the server. Apply the
    // event immediately there, then use EventId to ignore the echoed RPC.
    if (TryGetClientEntityManager(out _))
    {
      RememberItemVisualEffect(record);
    }
  }

  public static void DrainAuthoritativeItemVisualEffects(
      List<ConveyorTunnelItemVisualEffectRecord> records)
  {
    records.Clear();
    records.AddRange(AuthoritativeItemVisualEffects);
    AuthoritativeItemVisualEffects.Clear();
  }

  public static void RememberItemVisualEffect(
      ConveyorTunnelItemVisualEffectRecord record)
  {
    if (!ClientItemVisualEffectIds.Add(record.EventId))
    {
      return;
    }

    ClientItemVisualEffectIdOrder.Enqueue(record.EventId);
    while (ClientItemVisualEffectIdOrder.Count > MaxRememberedItemVisualEffectIds)
    {
      ClientItemVisualEffectIds.Remove(ClientItemVisualEffectIdOrder.Dequeue());
    }

    if (record.Kind == ConveyorTunnelItemVisualEffectKind.Exit)
    {
      ConveyorTunnelItemVisualEffects.RequestExit(record.Point);
    }
    else
    {
      ConveyorTunnelItemVisualEffects.RequestIntake(record.Point);
    }
  }

  public static void RequestSnapshot(bool force = false)
  {
    if (!TryGetClientEntityManager(out EntityManager entityManager))
    {
      return;
    }

    double now = Time.realtimeSinceStartup;
    if (!force && now - _lastSnapshotRequestAt < SnapshotRequestCooldownSeconds)
    {
      return;
    }

    _lastSnapshotRequestAt = now;

    EntityArchetype archetype = entityManager.CreateArchetype(
        typeof(ConveyorTunnelStateRequestRpc),
        typeof(SendRpcCommandRequest));
    entityManager.CreateEntity(archetype);
  }

  private static void ClearClientEndpointStates()
  {
    if (EndpointStatesByTile.Count == 0)
    {
      return;
    }

    ClientResetTiles.Clear();
    foreach (long key in EndpointStatesByTile.Keys)
    {
      ClientResetTiles.Add(TileFromKey(key));
    }

    EndpointStatesByTile.Clear();
    ConveyorTunnelEndpointVisualState cleared = new ConveyorTunnelEndpointVisualState
    {
      Role = ConveyorTunnelEndpointRole.None,
      Direction = int2.zero
    };

    for (int i = 0; i < ClientResetTiles.Count; i++)
    {
      EndpointStateChanged?.Invoke(ClientResetTiles[i], cleared);
    }

    ClientResetTiles.Clear();
  }

  private static bool TryGetClientEntityManager(out EntityManager entityManager)
  {
    entityManager = default;

    World world = API.Client != null ? API.Client.World : null;
    if ((world == null || !world.IsCreated) && Manager.ecs != null)
    {
      world = Manager.ecs.ClientWorld;
    }

    if (world == null || !world.IsCreated)
    {
      return false;
    }

    entityManager = world.EntityManager;
    return true;
  }

  private static bool EndpointStatesEqual(
      ConveyorTunnelEndpointVisualState a,
      ConveyorTunnelEndpointVisualState b)
  {
    return a.PairId == b.PairId &&
           a.Role == b.Role &&
           a.Linked == b.Linked &&
           a.Pending == b.Pending &&
           a.HasPayloads == b.HasPayloads &&
           a.Direction.Equals(b.Direction) &&
           a.PairedTile.Equals(b.PairedTile);
  }

  private static long GetKey(int2 tile)
  {
    return ((long)tile.x << 32) ^ (uint)tile.y;
  }

  private static int2 TileFromKey(long key)
  {
    return new int2((int)(key >> 32), unchecked((int)(uint)key));
  }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ConveyorTunnelRuntimeSystem))]
public partial class ConveyorTunnelServerStateRpcSystem : SystemBase
{
  private readonly List<ConveyorTunnelEndpointStateRecord> _records =
      new List<ConveyorTunnelEndpointStateRecord>();
  private readonly List<ConveyorTunnelItemVisualEffectRecord> _itemVisualEffects =
      new List<ConveyorTunnelItemVisualEffectRecord>();

  private EntityQuery _requestQuery;
  private EntityArchetype _snapshotBeginArchetype;
  private EntityArchetype _endpointStateArchetype;
  private EntityArchetype _itemVisualEffectArchetype;
  private EntityArchetype _snapshotEndArchetype;

  protected override void OnCreate()
  {
    _requestQuery = GetEntityQuery(
        ComponentType.ReadOnly<ConveyorTunnelStateRequestRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

    _snapshotBeginArchetype = EntityManager.CreateArchetype(
        typeof(ConveyorTunnelSnapshotBeginRpc),
        typeof(SendRpcCommandRequest));
    _endpointStateArchetype = EntityManager.CreateArchetype(
        typeof(ConveyorTunnelEndpointStateRpc),
        typeof(SendRpcCommandRequest));
    _itemVisualEffectArchetype = EntityManager.CreateArchetype(
        typeof(ConveyorTunnelItemVisualEffectRpc),
        typeof(SendRpcCommandRequest));
    _snapshotEndArchetype = EntityManager.CreateArchetype(
        typeof(ConveyorTunnelSnapshotEndRpc),
        typeof(SendRpcCommandRequest));
  }

  protected override void OnUpdate()
  {
    HandleSnapshotRequests();
    BroadcastAuthoritativeChanges();
    BroadcastItemVisualEffects();
  }

  private void HandleSnapshotRequests()
  {
    using NativeArray<Entity> entities = _requestQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ReceiveRpcCommandRequest> sources =
        _requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      SendSnapshot(sources[i].SourceConnection);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void SendSnapshot(Entity targetConnection)
  {
    Entity beginEntity = EntityManager.CreateEntity(_snapshotBeginArchetype);
    EntityManager.SetComponentData(beginEntity, new SendRpcCommandRequest
    {
      TargetConnection = targetConnection
    });

    ConveyorTunnelNetworkState.GetAuthoritativeEndpointSnapshot(_records);
    for (int i = 0; i < _records.Count; i++)
    {
      SendEndpointState(_records[i], targetConnection);
    }

    Entity endEntity = EntityManager.CreateEntity(_snapshotEndArchetype);
    EntityManager.SetComponentData(endEntity, new SendRpcCommandRequest
    {
      TargetConnection = targetConnection
    });
  }

  private void BroadcastAuthoritativeChanges()
  {
    ConveyorTunnelNetworkState.DrainAuthoritativeEndpointChanges(_records);
    for (int i = 0; i < _records.Count; i++)
    {
      SendEndpointState(_records[i], Entity.Null);
    }
  }

  private void BroadcastItemVisualEffects()
  {
    ConveyorTunnelNetworkState.DrainAuthoritativeItemVisualEffects(_itemVisualEffects);
    for (int i = 0; i < _itemVisualEffects.Count; i++)
    {
      ConveyorTunnelItemVisualEffectRecord record = _itemVisualEffects[i];
      Entity entity = EntityManager.CreateEntity(_itemVisualEffectArchetype);
      EntityManager.SetComponentData(entity, new ConveyorTunnelItemVisualEffectRpc
      {
        EventId = record.EventId,
        Kind = (byte)record.Kind,
        PointX = record.Point.x,
        PointY = record.Point.y
      });
      EntityManager.SetComponentData(entity, new SendRpcCommandRequest
      {
        TargetConnection = Entity.Null
      });
    }
  }

  private void SendEndpointState(
      ConveyorTunnelEndpointStateRecord record,
      Entity targetConnection)
  {
    Entity entity = EntityManager.CreateEntity(_endpointStateArchetype);
    EntityManager.SetComponentData(entity, ToRpc(record));
    EntityManager.SetComponentData(entity, new SendRpcCommandRequest
    {
      TargetConnection = targetConnection
    });
  }

  private static ConveyorTunnelEndpointStateRpc ToRpc(
      ConveyorTunnelEndpointStateRecord record)
  {
    return new ConveyorTunnelEndpointStateRpc
    {
      TileX = record.Tile.x,
      TileY = record.Tile.y,
      PairId = record.State.PairId,
      Role = (byte)record.State.Role,
      Linked = record.State.Linked,
      Pending = record.State.Pending,
      HasPayloads = record.State.HasPayloads,
      DirectionX = record.State.Direction.x,
      DirectionY = record.State.Direction.y,
      PairedTileX = record.State.PairedTile.x,
      PairedTileY = record.State.PairedTile.y
    };
  }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ConveyorTunnelClientStateRpcSystem : SystemBase
{
  private EntityQuery _snapshotBeginQuery;
  private EntityQuery _endpointStateQuery;
  private EntityQuery _itemVisualEffectQuery;
  private EntityQuery _snapshotEndQuery;

  protected override void OnCreate()
  {
    _snapshotBeginQuery = GetEntityQuery(
        ComponentType.ReadOnly<ConveyorTunnelSnapshotBeginRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _endpointStateQuery = GetEntityQuery(
        ComponentType.ReadOnly<ConveyorTunnelEndpointStateRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _itemVisualEffectQuery = GetEntityQuery(
        ComponentType.ReadOnly<ConveyorTunnelItemVisualEffectRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _snapshotEndQuery = GetEntityQuery(
        ComponentType.ReadOnly<ConveyorTunnelSnapshotEndRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
  }

  protected override void OnUpdate()
  {
    HandleSnapshotBegins();
    HandleEndpointStates();
    HandleItemVisualEffects();
    HandleSnapshotEnds();

    if (!ConveyorTunnelNetworkState.HasReceivedSnapshot)
    {
      ConveyorTunnelNetworkState.RequestSnapshot();
    }
  }

  private void HandleSnapshotBegins()
  {
    using NativeArray<Entity> entities =
        _snapshotBeginQuery.ToEntityArray(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ConveyorTunnelNetworkState.BeginClientSnapshot();
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleEndpointStates()
  {
    using NativeArray<Entity> entities =
        _endpointStateQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ConveyorTunnelEndpointStateRpc> states =
        _endpointStateQuery.ToComponentDataArray<ConveyorTunnelEndpointStateRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ConveyorTunnelEndpointStateRpc state = states[i];
      ConveyorTunnelNetworkState.RememberEndpointState(
          new int2(state.TileX, state.TileY),
          new ConveyorTunnelEndpointVisualState
          {
            PairId = state.PairId,
            Role = (ConveyorTunnelEndpointRole)state.Role,
            Linked = state.Linked,
            Pending = state.Pending,
            HasPayloads = state.HasPayloads,
            Direction = new int2(state.DirectionX, state.DirectionY),
            PairedTile = new int2(state.PairedTileX, state.PairedTileY)
          });
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleSnapshotEnds()
  {
    using NativeArray<Entity> entities =
        _snapshotEndQuery.ToEntityArray(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ConveyorTunnelNetworkState.CompleteClientSnapshot();
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleItemVisualEffects()
  {
    using NativeArray<Entity> entities =
        _itemVisualEffectQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ConveyorTunnelItemVisualEffectRpc> effects =
        _itemVisualEffectQuery.ToComponentDataArray<ConveyorTunnelItemVisualEffectRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ConveyorTunnelItemVisualEffectRpc effect = effects[i];
      ConveyorTunnelNetworkState.RememberItemVisualEffect(
          new ConveyorTunnelItemVisualEffectRecord(
              effect.EventId,
              (ConveyorTunnelItemVisualEffectKind)effect.Kind,
              new float2(effect.PointX, effect.PointY)));
      EntityManager.DestroyEntity(entities[i]);
    }
  }
}
