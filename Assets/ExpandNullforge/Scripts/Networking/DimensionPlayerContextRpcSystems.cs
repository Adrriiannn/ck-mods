using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using ExpandNullforge.Core;

namespace ExpandNullforge.Networking
{
  [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public partial class DimensionPlayerContextServerRpcSystem : SystemBase
  {
    private EntityQuery requestQuery;
    private EntityQuery playerQuery;
    private EntityArchetype snapshotArchetype;
    private IDimensionService boundService;
    private double nextBindAttemptAt;
    private readonly List<DimensionChangedEvent> queuedChanges =
        new List<DimensionChangedEvent>();

    private const double BindRetryIntervalSeconds = 0.50d;

    protected override void OnCreate()
    {
      requestQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionPlayerContextRequestRpc>(),
          ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
      playerQuery = GetEntityQuery(
          ComponentType.ReadOnly<PlayerGhost>());
      snapshotArchetype = EntityManager.CreateArchetype(
          typeof(DimensionPlayerContextSnapshotRpc),
          typeof(SendRpcCommandRequest));
    }

    protected override void OnDestroy()
    {
      BindService(null);
      queuedChanges.Clear();
    }

    protected override void OnUpdate()
    {
      bool hasRequests = !requestQuery.IsEmptyIgnoreFilter;
      if (boundService != null && queuedChanges.Count == 0 && !hasRequests)
      {
        return;
      }

      if (boundService == null)
      {
        if (queuedChanges.Count == 0 && !hasRequests && SystemAPI.Time.ElapsedTime < nextBindAttemptAt)
        {
          return;
        }

        TryBindCurrentServiceThrottled();
      }
      else if (!boundService.IsReady)
      {
        BindService(null);
      }

      if (queuedChanges.Count > 0)
      {
        FlushQueuedDimensionChanges();
      }

      if (!hasRequests)
      {
        return;
      }

      using NativeArray<Entity> entities =
          requestQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<DimensionPlayerContextRequestRpc> requests =
          requestQuery.ToComponentDataArray<DimensionPlayerContextRequestRpc>(Allocator.Temp);
      using NativeArray<ReceiveRpcCommandRequest> sources =
          requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

      for (int i = 0; i < entities.Length; i++)
      {
        HandleContextRequest(requests[i], sources[i].SourceConnection);
        EntityManager.DestroyEntity(entities[i]);
      }
    }

    private void HandleContextRequest(
        DimensionPlayerContextRequestRpc rpc,
        Entity sourceConnection)
    {
      Entity player;
      if (!TryResolvePlayerEntity(sourceConnection, out player))
      {
        SendSnapshot(
            sourceConnection,
            CreateUnknownSnapshot(
                rpc.RequestId,
                "player-not-found",
                "No server player entity is associated with the requesting connection."));
        return;
      }

      IDimensionService service = boundService;
      if (service == null || !service.IsReady)
      {
        SendSnapshot(
            sourceConnection,
            CreateUnknownSnapshot(
                rpc.RequestId,
                "dimension-service-unavailable",
                "The dimension service is not ready."));
        return;
      }

      DimensionContext context;
      if (service.TryGetPlayerContext(player, out context) && context.IsKnown)
      {
        SendSnapshot(
            sourceConnection,
            CreateKnownSnapshot(
                rpc.RequestId,
                false,
                "ok",
                "Current player dimension context resolved.",
                context));
        return;
      }

      if (rpc.IncludePersistedFallback != 0 &&
          service.TryGetPersistedPlayerContext(player, out context) &&
          context.IsKnown)
      {
        SendSnapshot(
            sourceConnection,
            CreateKnownSnapshot(
                rpc.RequestId,
                true,
                "persisted-fallback",
                "Persisted player dimension context resolved.",
                context));
        return;
      }

      SendSnapshot(
          sourceConnection,
          CreateUnknownSnapshot(
              rpc.RequestId,
              "context-unavailable",
              "The player dimension context is not available yet."));
    }

    private void FlushQueuedDimensionChanges()
    {
      if (queuedChanges.Count == 0)
      {
        return;
      }

      IDimensionService service = boundService;
      for (int i = 0; i < queuedChanges.Count; i++)
      {
        DimensionChangedEvent changedEvent = queuedChanges[i];
        Entity targetConnection;
        if (!TryResolveConnectionForPlayer(changedEvent.Player, out targetConnection))
        {
          continue;
        }

        DimensionContext context;
        if (service != null &&
            service.IsReady &&
            service.TryGetPlayerContext(changedEvent.Player, out context) &&
            context.IsKnown)
        {
          SendSnapshot(
              targetConnection,
              CreateKnownSnapshot(
                  0,
                  false,
                  "dimension-context-changed",
                  "Player dimension context changed.",
                  context));
          continue;
        }

        SendSnapshot(
            targetConnection,
            CreateUnknownSnapshot(
                0,
                "dimension-context-changed",
                "Player dimension changed, but the current context could not be resolved."));
      }

      queuedChanges.Clear();
    }

    private void OnPlayerDimensionChanged(DimensionChangedEvent changedEvent)
    {
      queuedChanges.Add(changedEvent);
    }

    private bool TryResolvePlayerEntity(Entity sourceConnection, out Entity playerEntity)
    {
      playerEntity = Entity.Null;
      if (sourceConnection == Entity.Null)
      {
        return false;
      }

      using NativeArray<Entity> playerEntities =
          playerQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<PlayerGhost> players =
          playerQuery.ToComponentDataArray<PlayerGhost>(Allocator.Temp);

      for (int i = 0; i < players.Length; i++)
      {
        if (players[i].connection == sourceConnection)
        {
          playerEntity = playerEntities[i];
          return true;
        }
      }

      return false;
    }

    private bool TryResolveConnectionForPlayer(
        Entity player,
        out Entity sourceConnection)
    {
      sourceConnection = Entity.Null;
      if (player == Entity.Null || !EntityManager.Exists(player))
      {
        return false;
      }

      if (!EntityManager.HasComponent<PlayerGhost>(player))
      {
        return false;
      }

      PlayerGhost playerGhost = EntityManager.GetComponentData<PlayerGhost>(player);
      sourceConnection = playerGhost.connection;
      return sourceConnection != Entity.Null;
    }

    private void SendSnapshot(
        Entity targetConnection,
        DimensionPlayerContextSnapshotRpc snapshot)
    {
      if (targetConnection == Entity.Null)
      {
        return;
      }

      Entity entity = EntityManager.CreateEntity(snapshotArchetype);
      EntityManager.SetComponentData(entity, snapshot);
      EntityManager.SetComponentData(
          entity,
          new SendRpcCommandRequest
          {
            TargetConnection = targetConnection
          });
    }

    private DimensionPlayerContextSnapshotRpc CreateKnownSnapshot(
        uint requestId,
        bool persistedFallback,
        string code,
        string message,
        DimensionContext context)
    {
      return new DimensionPlayerContextSnapshotRpc
      {
        RequestId = requestId,
        Known = (byte)1,
        PersistedFallback = persistedFallback ? (byte)1 : (byte)0,
        Code = DimensionFixedStrings.ToFixed64(code),
        Message = DimensionFixedStrings.ToFixed128(message),
        DimensionId = DimensionFixedStrings.ToFixed64(context.DimensionId),
        AbsoluteX = context.AbsolutePosition.x,
        AbsoluteY = context.AbsolutePosition.y,
        LocalX = context.LocalPosition.x,
        LocalY = context.LocalPosition.y
      };
    }

    private DimensionPlayerContextSnapshotRpc CreateUnknownSnapshot(
        uint requestId,
        string code,
        string message)
    {
      return new DimensionPlayerContextSnapshotRpc
      {
        RequestId = requestId,
        Known = 0,
        PersistedFallback = 0,
        Code = DimensionFixedStrings.ToFixed64(code),
        Message = DimensionFixedStrings.ToFixed128(message),
        DimensionId = default,
        AbsoluteX = 0f,
        AbsoluteY = 0f,
        LocalX = 0f,
        LocalY = 0f
      };
    }

    private void BindCurrentService()
    {
      IDimensionService service;
      if (DimensionApi.TryGetService(out service) && service != null && service.IsReady)
      {
        BindService(service);
      }
      else
      {
        BindService(null);
      }
    }

    private void TryBindCurrentServiceThrottled()
    {
      double now = SystemAPI.Time.ElapsedTime;
      if (now < nextBindAttemptAt)
      {
        return;
      }

      nextBindAttemptAt = now + BindRetryIntervalSeconds;
      BindCurrentService();
    }

    private void BindService(IDimensionService service)
    {
      if (ReferenceEquals(boundService, service))
      {
        return;
      }

      if (boundService != null)
      {
        boundService.PlayerDimensionChanged -= OnPlayerDimensionChanged;
      }

      boundService = service;
      queuedChanges.Clear();

      if (boundService != null)
      {
        boundService.PlayerDimensionChanged += OnPlayerDimensionChanged;
      }
    }

  }

  [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public partial class DimensionPlayerContextClientRpcSystem : SystemBase
  {
    private EntityQuery snapshotQuery;

    protected override void OnCreate()
    {
      snapshotQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionPlayerContextSnapshotRpc>(),
          ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
      RequireForUpdate(snapshotQuery);
    }

    protected override void OnUpdate()
    {
      if (snapshotQuery.IsEmptyIgnoreFilter)
      {
        return;
      }

      using NativeArray<Entity> entities =
          snapshotQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<DimensionPlayerContextSnapshotRpc> snapshots =
          snapshotQuery.ToComponentDataArray<DimensionPlayerContextSnapshotRpc>(Allocator.Temp);

      for (int i = 0; i < entities.Length; i++)
      {
        DimensionPlayerContextNetworkState.ApplySnapshot(snapshots[i]);
        EntityManager.DestroyEntity(entities[i]);
      }
    }
  }
}
