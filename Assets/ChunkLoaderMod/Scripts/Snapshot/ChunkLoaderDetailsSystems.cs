using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ChunkLoaderDetailsServerSystem : SystemBase
{
  private sealed class PendingRequest
  {
    public Entity TargetConnection;
    public ulong ActorId;
    public ulong RegistrationId;
    public long LastKnownLogSequence;
    public bool IncludeSamples;
  }

  private readonly Dictionary<ulong, double> _lastSnapshotRequestAt = new();
  private readonly Dictionary<ulong, double> _lastLiveRequestAt = new();
  private readonly Queue<PendingRequest> _pending = new();
  private readonly HashSet<ulong> _pendingActors = new();
  private readonly Queue<double> _captureTimes = new();
  private readonly List<ChunkLoaderTelemetryEvent> _logs = new();
  private EntityQuery _requestQuery;
  private EntityArchetype _beginArchetype;
  private EntityArchetype _sampleArchetype;
  private EntityArchetype _logArchetype;
  private EntityArchetype _endArchetype;
  private EntityQuery _captureEntityQuery;

  protected override void OnCreate()
  {
    _requestQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderDetailsRequestRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _beginArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderDetailsBeginRpc),
        typeof(SendRpcCommandRequest));
    _sampleArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderSnapshotSampleRpc),
        typeof(SendRpcCommandRequest));
    _logArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderLogLineRpc),
        typeof(SendRpcCommandRequest));
    _endArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderDetailsEndRpc),
        typeof(SendRpcCommandRequest));
    _captureEntityQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[] { ComponentType.ReadOnly<LocalTransform>() },
      None = new[]
      {
        ComponentType.ReadOnly<Prefab>(),
        ComponentType.ReadOnly<EntityDestroyedCD>(),
        ComponentType.ReadOnly<ChunkLoaderRuntimeAnchorCD>()
      }
    });
  }

  protected override void OnUpdate()
  {
    using NativeArray<Entity> entities =
        _requestQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderDetailsRequestRpc> requests =
        _requestQuery.ToComponentDataArray<ChunkLoaderDetailsRequestRpc>(
            Allocator.Temp);
    using NativeArray<ReceiveRpcCommandRequest> sources =
        _requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ValidateAndQueue(requests[i], sources[i].SourceConnection);
      EntityManager.DestroyEntity(entities[i]);
    }

    ProcessQueue();
  }

  private void ValidateAndQueue(
      ChunkLoaderDetailsRequestRpc request,
      Entity targetConnection)
  {
    ChunkLoaderActor actor =
        ChunkLoaderPlayerResolver.Resolve(World, targetConnection);
    if (!ChunkLoaderRegistry.TryGetRecord(
            request.RegistrationId,
            out ChunkLoaderRegistrationRecord record) ||
        (!actor.IsAdmin &&
         record.ownerPersistentId != actor.PersistentId &&
         !record.CountsAgainstActiveQuota))
    {
      SendError(
          targetConnection,
          request.RegistrationId,
          ChunkLoaderErrorCode.PermissionDenied,
          "This chunk cannot be inspected.");
      return;
    }

    double now = SystemAPI.Time.ElapsedTime;
    bool includeSamples = request.IncludeSamples != 0;
    bool isLive = request.IsLive != 0 || !includeSamples;
    double cooldown = includeSamples
        ? ChunkLoaderSettings.Current.snapshotCooldownSeconds
        : ChunkLoaderSettings.Current.liveDetailsCooldownSeconds;
    Dictionary<ulong, double> lastRequestLookup = includeSamples
        ? _lastSnapshotRequestAt
        : _lastLiveRequestAt;
    if (lastRequestLookup.TryGetValue(actor.PersistentId, out double lastRequest) &&
        now - lastRequest < cooldown)
    {
      if (!isLive)
      {
        SendError(
            targetConnection,
            request.RegistrationId,
            ChunkLoaderErrorCode.InternalError,
            $"Chunk details can be refreshed once every {cooldown:0.#} seconds.");
      }
      return;
    }

    if (_pendingActors.Contains(actor.PersistentId))
    {
      if (!isLive)
      {
        SendError(
            targetConnection,
            request.RegistrationId,
            ChunkLoaderErrorCode.InternalError,
            "A chunk details request is already queued for this player.");
      }
      return;
    }

    if (_pending.Count >= 64)
    {
      if (!isLive)
      {
        SendError(
            targetConnection,
            request.RegistrationId,
            ChunkLoaderErrorCode.InternalError,
            "The server snapshot queue is busy. Please try again shortly.");
      }
      return;
    }

    lastRequestLookup[actor.PersistentId] = now;
    _pendingActors.Add(actor.PersistentId);
    _pending.Enqueue(new PendingRequest
    {
      TargetConnection = targetConnection,
      ActorId = actor.PersistentId,
      RegistrationId = record.registrationId,
      LastKnownLogSequence = request.LastKnownLogSequence,
      IncludeSamples = includeSamples
    });
  }

  private void ProcessQueue()
  {
    double now = SystemAPI.Time.ElapsedTime;
    while (_captureTimes.Count > 0 && now - _captureTimes.Peek() >= 1.0d)
    {
      _captureTimes.Dequeue();
    }

    int budget = ChunkLoaderSettings.Current.snapshotCapturesPerSecond;
    int processed = 0;
    while (_pending.Count > 0 && processed < 16)
    {
      PendingRequest pending = _pending.Peek();
      if (pending.IncludeSamples && _captureTimes.Count >= budget)
      {
        break;
      }

      pending = _pending.Dequeue();
      _pendingActors.Remove(pending.ActorId);
      if (!EntityManager.Exists(pending.TargetConnection) ||
          !ChunkLoaderRegistry.TryGetRecord(
              pending.RegistrationId,
              out ChunkLoaderRegistrationRecord record))
      {
        continue;
      }

      CaptureAndSend(
          pending.TargetConnection,
          record,
          pending.IncludeSamples,
          pending.LastKnownLogSequence);
      if (pending.IncludeSamples)
      {
        _captureTimes.Enqueue(now);
      }
      processed++;
    }
  }

  private void CaptureAndSend(
      Entity targetConnection,
      ChunkLoaderRegistrationRecord record,
      bool includeSamples,
      long lastKnownLogSequence)
  {
    int total = includeSamples ? 0 : -1;
    int objects = includeSamples ? 0 : -1;
    int enemies = includeSamples ? 0 : -1;
    int players = includeSamples ? 0 : -1;
    ChunkCoordinate coordinate = record.Coordinate;
    int2 origin = coordinate.Origin;

    int maximumSamples = ChunkLoaderSettings.Current.snapshotMaximumSamples;
    List<ChunkLoaderSnapshotSampleRpc> samples =
        includeSamples
            ? new List<ChunkLoaderSnapshotSampleRpc>(maximumSamples)
            : new List<ChunkLoaderSnapshotSampleRpc>(0);

    if (includeSamples)
    {
      using NativeArray<Entity> entities =
          _captureEntityQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<LocalTransform> transforms =
          _captureEntityQuery.ToComponentDataArray<LocalTransform>(
              Allocator.Temp);

      for (int i = 0; i < entities.Length; i++)
      {
        int2 tile = new int2(
            (int)math.floor(transforms[i].Position.x),
            (int)math.floor(transforms[i].Position.z));
        if (!coordinate.ContainsTile(tile))
        {
          continue;
        }

        Entity entity = entities[i];
        bool hasObject = EntityManager.HasComponent<ObjectDataCD>(entity);
        bool isEnemy = EntityManager.HasComponent<EnemyCD>(entity);
        bool isPlayer = EntityManager.HasComponent<PlayerGhost>(entity);
        if (!hasObject && !isEnemy && !isPlayer)
        {
          continue;
        }

        total++;
        if (hasObject) objects++;
        if (isEnemy) enemies++;
        if (isPlayer) players++;

        if (!ChunkLoaderSettings.Current.snapshotsEnabled ||
            samples.Count >= maximumSamples ||
            (!isEnemy && !isPlayer))
        {
          continue;
        }

        ObjectDataCD objectData = hasObject
            ? EntityManager.GetComponentData<ObjectDataCD>(entity)
            : default;
        byte flags = 0;
        if (isEnemy) flags |= 1;
        if (isPlayer) flags |= 2;
        samples.Add(new ChunkLoaderSnapshotSampleRpc
        {
          RegistrationId = record.registrationId,
          RelativeX = (byte)math.clamp(
              tile.x - origin.x,
              0,
              ChunkLoaderConstants.ChunkSize - 1),
          RelativeY = (byte)math.clamp(
              tile.y - origin.y,
              0,
              ChunkLoaderConstants.ChunkSize - 1),
          ObjectId = (int)objectData.objectID,
          Variation = objectData.variation,
          Amount = objectData.amount,
          Flags = flags
        });
      }
    }

    ChunkLoaderTelemetry.GetRecent(
        record.registrationId,
        ChunkLoaderConstants.ActivityDetailsLineLimit,
        _logs,
        includeSamples ? long.MinValue : lastKnownLogSequence);
    Entity begin = EntityManager.CreateEntity(_beginArchetype);
    EntityManager.SetComponentData(begin, new ChunkLoaderDetailsBeginRpc
    {
      RegistrationId = record.registrationId,
      CapturedAtUtcTicks = DateTime.UtcNow.Ticks,
      TotalEntities = total,
      Objects = objects,
      Enemies = enemies,
      Players = players,
      Samples = samples.Count,
      LogLines = _logs.Count,
      IncludesSamples = includeSamples ? (byte)1 : (byte)0
    });
    SetTarget(begin, targetConnection);

    for (int i = 0; i < samples.Count; i++)
    {
      Entity sample = EntityManager.CreateEntity(_sampleArchetype);
      EntityManager.SetComponentData(sample, samples[i]);
      SetTarget(sample, targetConnection);
    }

    for (int i = 0; i < _logs.Count; i++)
    {
      ChunkLoaderTelemetryEvent log = _logs[i];
      Entity logEntity = EntityManager.CreateEntity(_logArchetype);
      EntityManager.SetComponentData(logEntity, new ChunkLoaderLogLineRpc
      {
        RegistrationId = record.registrationId,
        Sequence = log.sequence,
        TimestampUtcTicks = log.timestampUtcTicks,
        Category = ChunkLoaderFixedStringUtility.ToFixed32(log.category),
        Message = ChunkLoaderFixedStringUtility.ToFixed128(log.message)
      });
      SetTarget(logEntity, targetConnection);
    }

    SendEnd(targetConnection, record.registrationId);
  }

  private void SendError(
      Entity targetConnection,
      ulong registrationId,
      ChunkLoaderErrorCode errorCode,
      string error)
  {
    Entity begin = EntityManager.CreateEntity(_beginArchetype);
    EntityManager.SetComponentData(begin, new ChunkLoaderDetailsBeginRpc
    {
      RegistrationId = registrationId,
      CapturedAtUtcTicks = DateTime.UtcNow.Ticks,
      ErrorCode = (ushort)errorCode,
      ErrorText = ChunkLoaderFixedStringUtility.ToFixed128(error)
    });
    SetTarget(begin, targetConnection);
    SendEnd(targetConnection, registrationId);
  }

  private void SendEnd(Entity targetConnection, ulong registrationId)
  {
    Entity end = EntityManager.CreateEntity(_endArchetype);
    EntityManager.SetComponentData(end, new ChunkLoaderDetailsEndRpc
    {
      RegistrationId = registrationId
    });
    SetTarget(end, targetConnection);
  }

  private void SetTarget(Entity entity, Entity targetConnection)
  {
    EntityManager.SetComponentData(entity, new SendRpcCommandRequest
    {
      TargetConnection = targetConnection
    });
  }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ChunkLoaderDetailsClientSystem : SystemBase
{
  private EntityQuery _beginQuery;
  private EntityQuery _sampleQuery;
  private EntityQuery _logQuery;
  private EntityQuery _endQuery;

  protected override void OnCreate()
  {
    _beginQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderDetailsBeginRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _sampleQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderSnapshotSampleRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _logQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderLogLineRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _endQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderDetailsEndRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
  }

  protected override void OnUpdate()
  {
    Consume(_beginQuery, (ChunkLoaderDetailsBeginRpc value) =>
        ChunkLoaderDetailsState.Begin(value));
    Consume(_sampleQuery, (ChunkLoaderSnapshotSampleRpc value) =>
        ChunkLoaderDetailsState.AddSample(value));
    Consume(_logQuery, (ChunkLoaderLogLineRpc value) =>
        ChunkLoaderDetailsState.AddLog(value));
    Consume(_endQuery, (ChunkLoaderDetailsEndRpc value) =>
        ChunkLoaderDetailsState.Complete(value.RegistrationId));
  }

  private void Consume<T>(EntityQuery query, Action<T> consume)
      where T : unmanaged, IComponentData
  {
    using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
    using NativeArray<T> messages =
        query.ToComponentDataArray<T>(Allocator.Temp);
    for (int i = 0; i < entities.Length; i++)
    {
      consume(messages[i]);
      EntityManager.DestroyEntity(entities[i]);
    }
  }
}

public static class ChunkLoaderFixedStringUtility
{
  public static FixedString32Bytes ToFixed32(string value)
  {
    FixedString32Bytes result = default;
    if (string.IsNullOrEmpty(value)) return result;
    int count = Math.Min(value.Length, 28);
    for (int i = 0; i < count; i++)
    {
      if (!char.IsControl(value[i])) result.Append(value[i]);
    }
    return result;
  }

  public static FixedString128Bytes ToFixed128(string value)
  {
    FixedString128Bytes result = default;
    if (string.IsNullOrEmpty(value)) return result;
    int count = Math.Min(value.Length, 120);
    for (int i = 0; i < count; i++)
    {
      if (!char.IsControl(value[i])) result.Append(value[i]);
    }
    return result;
  }
}
