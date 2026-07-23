using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ChunkLoaderServerRpcSystem : SystemBase
{
  private readonly List<ChunkLoaderRegistrationRecord> _records = new();
  private readonly List<ChunkLoaderRegistrationRecord> _pendingChanges = new();
  private readonly List<ulong> _pendingDeletes = new();

  private EntityQuery _registryRequestQuery;
  private EntityQuery _mutationRequestQuery;
  private EntityArchetype _snapshotBeginArchetype;
  private EntityArchetype _recordArchetype;
  private EntityArchetype _snapshotEndArchetype;
  private EntityArchetype _mutationResultArchetype;
  private EntityArchetype _deletedArchetype;
  private EntityArchetype _quotaArchetype;

  protected override void OnCreate()
  {
    _registryRequestQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderRegistryRequestRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _mutationRequestQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderMutationRequestRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

    _snapshotBeginArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderRegistrySnapshotBeginRpc),
        typeof(SendRpcCommandRequest));
    _recordArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderRegistrationRpc),
        typeof(SendRpcCommandRequest));
    _snapshotEndArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderRegistrySnapshotEndRpc),
        typeof(SendRpcCommandRequest));
    _mutationResultArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderMutationResultRpc),
        typeof(SendRpcCommandRequest));
    _deletedArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderRegistrationDeletedRpc),
        typeof(SendRpcCommandRequest));
    _quotaArchetype = EntityManager.CreateArchetype(
        typeof(ChunkLoaderQuotaUpdateRpc),
        typeof(SendRpcCommandRequest));

    ChunkLoaderRegistry.RecordChanged += OnRecordChanged;
    ChunkLoaderRegistry.RecordDeleted += OnRecordDeleted;
  }

  protected override void OnDestroy()
  {
    ChunkLoaderRegistry.RecordChanged -= OnRecordChanged;
    ChunkLoaderRegistry.RecordDeleted -= OnRecordDeleted;
  }

  protected override void OnUpdate()
  {
    ChunkLoaderRegistry.EnsureLoadedForCurrentWorld();
    HandleRegistryRequests();
    HandleMutationRequests();
    SendPendingRuntimeChanges();
  }

  private void HandleRegistryRequests()
  {
    using NativeArray<Entity> entities =
        _registryRequestQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderRegistryRequestRpc> requests =
        _registryRequestQuery.ToComponentDataArray<ChunkLoaderRegistryRequestRpc>(
            Allocator.Temp);
    using NativeArray<ReceiveRpcCommandRequest> sources =
        _registryRequestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderActor actor =
          ChunkLoaderPlayerResolver.Resolve(World, sources[i].SourceConnection);
      SendRegistrySnapshot(
          sources[i].SourceConnection,
          actor,
          requests[i].IncludeAll != 0 && actor.IsAdmin);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleMutationRequests()
  {
    using NativeArray<Entity> entities =
        _mutationRequestQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderMutationRequestRpc> requests =
        _mutationRequestQuery.ToComponentDataArray<ChunkLoaderMutationRequestRpc>(
            Allocator.Temp);
    using NativeArray<ReceiveRpcCommandRequest> sources =
        _mutationRequestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderActor actor =
          ChunkLoaderPlayerResolver.Resolve(World, sources[i].SourceConnection);
      ChunkLoaderMutationRequestRpc request = requests[i];
      ChunkLoaderMutationAction action = (ChunkLoaderMutationAction)request.Action;
      ChunkLoaderMutationResult result = ApplyMutation(actor, action, request);
      SendMutationResult(
          sources[i].SourceConnection,
          request.RequestId,
          action,
          result);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private ChunkLoaderMutationResult ApplyMutation(
      ChunkLoaderActor actor,
      ChunkLoaderMutationAction action,
      ChunkLoaderMutationRequestRpc request)
  {
    switch (action)
    {
      case ChunkLoaderMutationAction.Create:
        return ChunkLoaderRegistry.Create(
            actor,
            new ChunkCoordinate(request.ChunkX, request.ChunkY));

      case ChunkLoaderMutationAction.Rename:
        return ChunkLoaderRegistry.Rename(
            actor,
            request.RegistrationId,
            request.ExpectedRevision,
            request.Name.ToString());

      case ChunkLoaderMutationAction.SetEnabled:
        return ChunkLoaderRegistry.SetEnabled(
            actor,
            request.RegistrationId,
            request.ExpectedRevision,
            request.DesiredEnabled != 0);

      case ChunkLoaderMutationAction.Delete:
        return ChunkLoaderRegistry.Delete(
            actor,
            request.RegistrationId,
            request.ExpectedRevision);

      default:
        return ChunkLoaderMutationResult.Fail(
            ChunkLoaderErrorCode.InternalError,
            "Unknown chunk mutation request.");
    }
  }

  private void SendRegistrySnapshot(
      Entity targetConnection,
      ChunkLoaderActor actor,
      bool includeAll)
  {
    ChunkLoaderRegistry.GetRecords(_records);
    ChunkLoaderQuotaSummary quota =
        ChunkLoaderRegistry.GetQuotaSummary(actor.PersistentId);

    int count = 0;
    for (int i = 0; i < _records.Count; i++)
    {
      count++;
    }

    Entity begin = EntityManager.CreateEntity(_snapshotBeginArchetype);
    EntityManager.SetComponentData(begin, new ChunkLoaderRegistrySnapshotBeginRpc
    {
      RegistryRevision = ChunkLoaderRegistry.Revision,
      RecordCount = count,
      PersonalActive = quota.PersonalActive,
      PersonalActiveLimit = quota.PersonalActiveLimit,
      WorldActive = quota.WorldActive,
      WorldActiveLimit = quota.WorldActiveLimit,
      PersonalSaved = quota.PersonalSaved,
      PersonalSavedLimit = quota.PersonalSavedLimit,
      SnapshotCooldownSeconds =
          ChunkLoaderSettings.Current.snapshotCooldownSeconds,
      SnapshotsEnabled =
          ChunkLoaderSettings.Current.snapshotsEnabled ? (byte)1 : (byte)0,
      TelemetryEnabled =
          ChunkLoaderSettings.Current.telemetryEnabled ? (byte)1 : (byte)0,
      ViewerPersistentId = actor.PersistentId,
      ViewerIsAdmin = actor.IsAdmin ? (byte)1 : (byte)0
    });
    SetTarget(begin, targetConnection);

    for (int i = 0; i < _records.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _records[i];
      SendRecord(record, targetConnection);
    }

    Entity end = EntityManager.CreateEntity(_snapshotEndArchetype);
    EntityManager.SetComponentData(end, new ChunkLoaderRegistrySnapshotEndRpc
    {
      RegistryRevision = ChunkLoaderRegistry.Revision
    });
    SetTarget(end, targetConnection);
  }

  private void SendMutationResult(
      Entity targetConnection,
      uint requestId,
      ChunkLoaderMutationAction action,
      ChunkLoaderMutationResult result)
  {
    Entity entity = EntityManager.CreateEntity(_mutationResultArchetype);
    EntityManager.SetComponentData(entity, new ChunkLoaderMutationResultRpc
    {
      RequestId = requestId,
      Action = (byte)action,
      Success = result.Success ? (byte)1 : (byte)0,
      ErrorCode = (ushort)result.ErrorCode,
      Message = ToFixed128(result.Message),
      HasRecord = result.Record != null ? (byte)1 : (byte)0,
      Record = result.Record != null ? ToRpc(result.Record) : default
    });
    SetTarget(entity, targetConnection);
  }

  private void SendPendingRuntimeChanges()
  {
    if (_pendingChanges.Count == 0 && _pendingDeletes.Count == 0)
    {
      return;
    }

    EntityQuery playersQuery =
        EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>());
    using NativeArray<PlayerGhost> players =
        playersQuery.ToComponentDataArray<PlayerGhost>(Allocator.Temp);
    playersQuery.Dispose();

    for (int i = 0; i < _pendingChanges.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _pendingChanges[i];
      for (int j = 0; j < players.Length; j++)
      {
        PlayerGhost player = players[j];
        SendRecord(record, player.connection);
      }
    }

    for (int i = 0; i < _pendingDeletes.Count; i++)
    {
      ulong registrationId = _pendingDeletes[i];
      for (int j = 0; j < players.Length; j++)
      {
        SendDeleted(registrationId, players[j].connection);
      }
    }

    if (_pendingChanges.Count > 0 || _pendingDeletes.Count > 0)
    {
      for (int i = 0; i < players.Length; i++)
      {
        ulong playerId =
            ChunkLoaderPlayerResolver.GetPersistentId(players[i]);
        SendQuota(
            ChunkLoaderRegistry.GetQuotaSummary(playerId),
            players[i].connection);
      }
    }

    _pendingChanges.Clear();
    _pendingDeletes.Clear();
  }

  private void OnRecordChanged(ChunkLoaderRegistrationRecord record)
  {
    for (int i = 0; i < _pendingChanges.Count; i++)
    {
      if (_pendingChanges[i].registrationId == record.registrationId)
      {
        _pendingChanges[i] = record;
        return;
      }
    }

    _pendingChanges.Add(record);
  }

  private void OnRecordDeleted(ulong registrationId)
  {
    _pendingChanges.RemoveAll(
        record => record.registrationId == registrationId);
    if (!_pendingDeletes.Contains(registrationId))
    {
      _pendingDeletes.Add(registrationId);
    }
  }

  private void SendRecord(
      ChunkLoaderRegistrationRecord record,
      Entity targetConnection)
  {
    Entity entity = EntityManager.CreateEntity(_recordArchetype);
    EntityManager.SetComponentData(entity, ToRpc(record));
    SetTarget(entity, targetConnection);
  }

  private void SendDeleted(ulong registrationId, Entity targetConnection)
  {
    Entity entity = EntityManager.CreateEntity(_deletedArchetype);
    EntityManager.SetComponentData(entity, new ChunkLoaderRegistrationDeletedRpc
    {
      RegistrationId = registrationId
    });
    SetTarget(entity, targetConnection);
  }

  private void SendQuota(
      ChunkLoaderQuotaSummary quota,
      Entity targetConnection)
  {
    Entity entity = EntityManager.CreateEntity(_quotaArchetype);
    EntityManager.SetComponentData(entity, new ChunkLoaderQuotaUpdateRpc
    {
      PersonalActive = quota.PersonalActive,
      PersonalActiveLimit = quota.PersonalActiveLimit,
      WorldActive = quota.WorldActive,
      WorldActiveLimit = quota.WorldActiveLimit,
      PersonalSaved = quota.PersonalSaved,
      PersonalSavedLimit = quota.PersonalSavedLimit
    });
    SetTarget(entity, targetConnection);
  }

  private void SetTarget(Entity entity, Entity targetConnection)
  {
    EntityManager.SetComponentData(entity, new SendRpcCommandRequest
    {
      TargetConnection = targetConnection
    });
  }

  internal static ChunkLoaderRegistrationRpc ToRpc(
      ChunkLoaderRegistrationRecord record)
  {
    return new ChunkLoaderRegistrationRpc
    {
      RegistrationId = record.registrationId,
      ChunkX = record.chunkX,
      ChunkY = record.chunkY,
      Name = ToFixed64(record.customName),
      OwnerPersistentId = record.ownerPersistentId,
      OwnerName = ToFixed64(record.ownerDisplayNameAtCreation),
      CreatedAtUtcTicks = record.createdAtUtcTicks,
      LastEnabledAtUtcTicks = record.lastEnabledAtUtcTicks,
      LastDisabledAtUtcTicks = record.lastDisabledAtUtcTicks,
      TotalActiveTicks = record.totalActiveTicks,
      DesiredEnabled = record.desiredEnabled ? (byte)1 : (byte)0,
      ActualState = (byte)record.actualState,
      LastErrorCode = (ushort)record.lastErrorCode,
      LastErrorText = ToFixed128(record.lastErrorText),
      Revision = record.revision
    };
  }

  internal static ChunkLoaderRegistrationRecord FromRpc(
      ChunkLoaderRegistrationRpc rpc)
  {
    return new ChunkLoaderRegistrationRecord
    {
      registrationId = rpc.RegistrationId,
      chunkX = rpc.ChunkX,
      chunkY = rpc.ChunkY,
      customName = rpc.Name.ToString(),
      ownerPersistentId = rpc.OwnerPersistentId,
      ownerDisplayNameAtCreation = rpc.OwnerName.ToString(),
      createdAtUtcTicks = rpc.CreatedAtUtcTicks,
      lastEnabledAtUtcTicks = rpc.LastEnabledAtUtcTicks,
      lastDisabledAtUtcTicks = rpc.LastDisabledAtUtcTicks,
      totalActiveTicks = rpc.TotalActiveTicks,
      desiredEnabled = rpc.DesiredEnabled != 0,
      actualState = (ChunkLoaderRuntimeState)rpc.ActualState,
      lastErrorCode = (ChunkLoaderErrorCode)rpc.LastErrorCode,
      lastErrorText = rpc.LastErrorText.ToString(),
      revision = rpc.Revision
    };
  }

  private static FixedString64Bytes ToFixed64(string value)
  {
    FixedString64Bytes result = default;
    AppendSafe(ref result, value, ChunkLoaderConstants.MaxNameCharacters);
    return result;
  }

  private static FixedString128Bytes ToFixed128(string value)
  {
    FixedString128Bytes result = default;
    if (!string.IsNullOrEmpty(value))
    {
      int count = Math.Min(value.Length, 120);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }
    }

    return result;
  }

  private static void AppendSafe(
      ref FixedString64Bytes target,
      string value,
      int maxCharacters)
  {
    if (string.IsNullOrEmpty(value))
    {
      return;
    }

    int count = Math.Min(value.Length, maxCharacters);
    for (int i = 0; i < count; i++)
    {
      if (!char.IsControl(value[i]))
      {
        target.Append(value[i]);
      }
    }
  }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ChunkLoaderClientRpcSystem : SystemBase
{
  private EntityQuery _snapshotBeginQuery;
  private EntityQuery _recordQuery;
  private EntityQuery _deletedQuery;
  private EntityQuery _snapshotEndQuery;
  private EntityQuery _mutationResultQuery;
  private EntityQuery _quotaQuery;
  private EntityQuery _connectionInGameQuery;

  protected override void OnCreate()
  {
    _snapshotBeginQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderRegistrySnapshotBeginRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _recordQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderRegistrationRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _deletedQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderRegistrationDeletedRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _snapshotEndQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderRegistrySnapshotEndRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _mutationResultQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderMutationResultRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _quotaQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderQuotaUpdateRpc>(),
        ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
    _connectionInGameQuery = GetEntityQuery(
        ComponentType.ReadOnly<NetworkStreamInGame>());
  }

  protected override void OnUpdate()
  {
    HandleSnapshotBegins();
    HandleRecords();
    HandleDeleted();
    HandleSnapshotEnds();
    HandleMutationResults();
    HandleQuotaUpdates();

    if (!_connectionInGameQuery.IsEmptyIgnoreFilter &&
        !ChunkLoaderNetworkState.HasSnapshot)
    {
      ChunkLoaderNetworkState.RequestSnapshot();
    }
  }

  private void HandleSnapshotBegins()
  {
    using NativeArray<Entity> entities =
        _snapshotBeginQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderRegistrySnapshotBeginRpc> messages =
        _snapshotBeginQuery.ToComponentDataArray<ChunkLoaderRegistrySnapshotBeginRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderRegistrySnapshotBeginRpc message = messages[i];
      ChunkLoaderNetworkState.BeginSnapshot(
          message.RegistryRevision,
          new ChunkLoaderQuotaSummary(
              message.PersonalActive,
              message.PersonalActiveLimit,
              message.WorldActive,
              message.WorldActiveLimit,
              message.PersonalSaved,
              message.PersonalSavedLimit),
          message.SnapshotsEnabled != 0,
          message.TelemetryEnabled != 0,
          message.SnapshotCooldownSeconds,
          message.ViewerPersistentId,
          message.ViewerIsAdmin != 0);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleRecords()
  {
    using NativeArray<Entity> entities =
        _recordQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderRegistrationRpc> messages =
        _recordQuery.ToComponentDataArray<ChunkLoaderRegistrationRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderNetworkState.Remember(
          ChunkLoaderServerRpcSystem.FromRpc(messages[i]));
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleDeleted()
  {
    using NativeArray<Entity> entities =
        _deletedQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderRegistrationDeletedRpc> messages =
        _deletedQuery.ToComponentDataArray<ChunkLoaderRegistrationDeletedRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderNetworkState.Forget(messages[i].RegistrationId);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleSnapshotEnds()
  {
    using NativeArray<Entity> entities =
        _snapshotEndQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderRegistrySnapshotEndRpc> messages =
        _snapshotEndQuery.ToComponentDataArray<ChunkLoaderRegistrySnapshotEndRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderNetworkState.CompleteSnapshot(messages[i].RegistryRevision);
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleMutationResults()
  {
    using NativeArray<Entity> entities =
        _mutationResultQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderMutationResultRpc> messages =
        _mutationResultQuery.ToComponentDataArray<ChunkLoaderMutationResultRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderMutationResultRpc message = messages[i];
      ChunkLoaderRegistrationRecord record = message.HasRecord != 0
          ? ChunkLoaderServerRpcSystem.FromRpc(message.Record)
          : null;
      ChunkLoaderNetworkState.CompleteMutation(
          message.RequestId,
          (ChunkLoaderMutationAction)message.Action,
          new ChunkLoaderMutationResult(
              message.Success != 0,
              (ChunkLoaderErrorCode)message.ErrorCode,
              message.Message.ToString(),
              record));
      EntityManager.DestroyEntity(entities[i]);
    }
  }

  private void HandleQuotaUpdates()
  {
    using NativeArray<Entity> entities =
        _quotaQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderQuotaUpdateRpc> messages =
        _quotaQuery.ToComponentDataArray<ChunkLoaderQuotaUpdateRpc>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderQuotaUpdateRpc message = messages[i];
      ChunkLoaderNetworkState.UpdateQuota(
          new ChunkLoaderQuotaSummary(
              message.PersonalActive,
              message.PersonalActiveLimit,
              message.WorldActive,
              message.WorldActiveLimit,
              message.PersonalSaved,
              message.PersonalSavedLimit));
      EntityManager.DestroyEntity(entities[i]);
    }
  }
}
