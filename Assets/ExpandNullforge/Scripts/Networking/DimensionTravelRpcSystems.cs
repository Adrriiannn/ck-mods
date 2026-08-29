using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Portals;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using ExpandNullforge.Core;

namespace ExpandNullforge.Networking
{
  [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public partial class DimensionTravelServerRpcSystem : SystemBase
  {
    private EntityQuery requestQuery;
    private EntityQuery portalRequestQuery;
    private EntityQuery cancelRequestQuery;
    private EntityQuery playerQuery;
    private EntityQuery portalQuery;
    private EntityArchetype resultArchetype;
    private EntityArchetype cancelResultArchetype;
    private readonly DimensionTravelResultRelay travelResultRelay =
        new DimensionTravelResultRelay();

    protected override void OnCreate()
    {
      requestQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionTravelRequestRpc>(),
          ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
      portalRequestQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionPortalTravelRequestRpc>(),
          ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
      cancelRequestQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionTravelCancelRequestRpc>(),
          ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
      playerQuery = GetEntityQuery(
          ComponentType.ReadOnly<PlayerGhost>());
      portalQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionPortalCD>());
      resultArchetype = EntityManager.CreateArchetype(
          typeof(DimensionTravelResultRpc),
          typeof(SendRpcCommandRequest));
      cancelResultArchetype = EntityManager.CreateArchetype(
          typeof(DimensionTravelCancelResultRpc),
          typeof(SendRpcCommandRequest));
    }

    protected override void OnDestroy()
    {
      travelResultRelay.Dispose();
    }

    protected override void OnUpdate()
    {
      double now = World.Time.ElapsedTime;
      bool hasTravelRequests = !requestQuery.IsEmptyIgnoreFilter;
      bool hasPortalRequests = !portalRequestQuery.IsEmptyIgnoreFilter;
      bool hasCancelRequests = !cancelRequestQuery.IsEmptyIgnoreFilter;
      bool hasTravelResultRelayWork = travelResultRelay.HasPendingWork(now);
      if (!hasTravelRequests &&
          !hasPortalRequests &&
          !hasCancelRequests &&
          !hasTravelResultRelayWork)
      {
        return;
      }

      BindTravelResultRelay();
      if (hasTravelResultRelayWork)
      {
        travelResultRelay.Flush(EntityManager, resultArchetype, now);
      }

      if (hasTravelRequests)
      {
        using NativeArray<Entity> entities =
            requestQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<DimensionTravelRequestRpc> requests =
            requestQuery.ToComponentDataArray<DimensionTravelRequestRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> sources =
            requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
          HandleTravelRequest(requests[i], sources[i].SourceConnection);
          EntityManager.DestroyEntity(entities[i]);
        }
      }

      if (hasPortalRequests)
      {
        using NativeArray<Entity> portalRequestEntities =
            portalRequestQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<DimensionPortalTravelRequestRpc> portalRequests =
            portalRequestQuery.ToComponentDataArray<DimensionPortalTravelRequestRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> portalSources =
            portalRequestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < portalRequestEntities.Length; i++)
        {
          HandlePortalTravelRequest(portalRequests[i], portalSources[i].SourceConnection);
          EntityManager.DestroyEntity(portalRequestEntities[i]);
        }
      }

      if (hasCancelRequests)
      {
        using NativeArray<Entity> cancelRequestEntities =
            cancelRequestQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<DimensionTravelCancelRequestRpc> cancelRequests =
            cancelRequestQuery.ToComponentDataArray<DimensionTravelCancelRequestRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> cancelSources =
            cancelRequestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < cancelRequestEntities.Length; i++)
        {
          HandleCancelTravelRequest(cancelRequests[i], cancelSources[i].SourceConnection);
          EntityManager.DestroyEntity(cancelRequestEntities[i]);
        }
      }

      if (travelResultRelay.HasPendingWork(now))
      {
        travelResultRelay.Flush(EntityManager, resultArchetype, now);
      }
    }

    private void HandleTravelRequest(
        DimensionTravelRequestRpc rpc,
        Entity sourceConnection)
    {
      Entity player;
      if (!TryResolvePlayerEntity(sourceConnection, out player))
      {
        SendResult(
            sourceConnection,
            rpc.RequestId,
            DimensionTravelResult.Failed(
                "player-not-found",
                "No server player entity is associated with the requesting connection."));
        return;
      }

      IDimensionService service;
      if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
      {
        SendResult(
            sourceConnection,
            rpc.RequestId,
            DimensionTravelResult.Failed(
                "dimension-service-unavailable",
                "The dimension service is not ready."));
        return;
      }

      DimensionTravelRequest request =
          new DimensionTravelRequest(
              player,
              rpc.TargetDimensionId.ToString(),
              new float2(rpc.TargetLocalX, rpc.TargetLocalY),
              rpc.RequireGeneratedArea != 0,
              rpc.AllowFallbackPosition != 0,
              rpc.Reason.ToString());

      DimensionTravelResult result = service.RequestTravel(request);
      SendResult(sourceConnection, rpc.RequestId, result, !result.Accepted);
      travelResultRelay.Track(result, sourceConnection, rpc.RequestId, World.Time.ElapsedTime);
    }

    private void HandlePortalTravelRequest(
        DimensionPortalTravelRequestRpc rpc,
        Entity sourceConnection)
    {
      string portalId = rpc.PortalId.ToString();
      if (DimensionFrameworkLog.VerboseRuntimeLogging)
      {
        DimensionFrameworkLog.Verbose(
            "Received dimension portal travel RPC. requestId=" +
            rpc.RequestId +
            " portalId=" +
            portalId);
      }

      Entity player;
      if (!TryResolvePlayerEntity(sourceConnection, out player))
      {
        DimensionLog.Problem(DimensionLogChannels.Travel, null, 
            "Portal travel RPC rejected: player entity could not be resolved. requestId=" +
            rpc.RequestId);
        SendResult(
            sourceConnection,
            rpc.RequestId,
            DimensionTravelResult.Failed(
                "player-not-found",
                "No server player entity is associated with the requesting connection."));
        return;
      }

      if (string.IsNullOrEmpty(portalId))
      {
        DimensionLog.Problem(DimensionLogChannels.Travel, null, 
            "Portal travel RPC rejected: portal id empty. requestId=" +
            rpc.RequestId);
        SendResult(
            sourceConnection,
            rpc.RequestId,
            DimensionTravelResult.Failed(
                "portal-id-empty",
                "A portal id is required."));
        return;
      }

      Entity portalEntity;
      if (!TryFindPortalEntity(portalId, out portalEntity))
      {
        DimensionLog.Problem(DimensionLogChannels.Travel, null, 
            "Portal travel RPC rejected: no placed portal entity found. requestId=" +
            rpc.RequestId +
            " portalId=" +
            portalId);
        SendResult(
            sourceConnection,
            rpc.RequestId,
            DimensionTravelResult.Failed(
                "portal-entity-not-found",
                "The portal is registered but no placed portal entity is currently available."));
        return;
      }

      uint queuedRequestId;
      string reason = rpc.Reason.ToString();
      if (!DimensionPortalRuntime.TryQueueActivation(
          World,
          portalEntity,
          player,
          sourceConnection,
          rpc.RequestId,
          string.IsNullOrEmpty(reason)
              ? "Dimension portal RPC activation."
              : reason,
          out queuedRequestId))
      {
        DimensionLog.Problem(DimensionLogChannels.Travel, null, 
            "Portal travel RPC rejected: activation queue failed. requestId=" +
            rpc.RequestId +
            " portalId=" +
            portalId);
        SendResult(
            sourceConnection,
            rpc.RequestId,
            DimensionTravelResult.Failed(
                "portal-activation-queue-failed",
                "The portal activation could not be queued."));
        return;
      }

      if (DimensionFrameworkLog.VerboseRuntimeLogging)
      {
        DimensionFrameworkLog.Verbose(
            "Queued dimension portal activation from portal RPC. requestId=" +
            rpc.RequestId +
            " queuedRequestId=" +
            queuedRequestId +
            " portalId=" +
            portalId);
      }
    }

    private void HandleCancelTravelRequest(
        DimensionTravelCancelRequestRpc rpc,
        Entity sourceConnection)
    {
      Entity player;
      if (!TryResolvePlayerEntity(sourceConnection, out player))
      {
        SendCancelResult(
            sourceConnection,
            rpc.RequestId,
            false,
            "player-not-found",
            "No server player entity is associated with the requesting connection.",
            rpc.TravelId.ToString());
        return;
      }

      IDimensionService service;
      if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
      {
        SendCancelResult(
            sourceConnection,
            rpc.RequestId,
            false,
            "dimension-service-unavailable",
            "The dimension service is not ready.",
            rpc.TravelId.ToString());
        return;
      }

      string travelId = rpc.TravelId.ToString();
      string reason = rpc.Reason.ToString();
      DimensionOperationResult result;
      if (service.TryCancelTravel(
          new DimensionTravelCancelRequest(
              player,
              travelId,
              string.IsNullOrEmpty(reason)
                  ? "Client requested dimension travel cancellation."
                  : reason),
          out result))
      {
        SendCancelResult(
            sourceConnection,
            rpc.RequestId,
            true,
            result.Code,
            result.Message,
            travelId);
        return;
      }

      SendCancelResult(
          sourceConnection,
          rpc.RequestId,
          false,
          result.Code,
          result.Message,
          travelId);
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

    private bool TryFindPortalEntity(string portalId, out Entity portalEntity)
    {
      portalEntity = Entity.Null;
      if (string.IsNullOrEmpty(portalId))
      {
        return false;
      }

      using NativeArray<Entity> portalEntities =
          portalQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<DimensionPortalCD> portals =
          portalQuery.ToComponentDataArray<DimensionPortalCD>(Allocator.Temp);
      FixedString64Bytes portalIdFixed = DimensionFixedStrings.ToFixed64(portalId);

      for (int i = 0; i < portals.Length; i++)
      {
        if (portals[i].PortalId.Equals(portalIdFixed))
        {
          portalEntity = portalEntities[i];
          return true;
        }
      }

      return false;
    }

    private void SendResult(
        Entity targetConnection,
        uint requestId,
        DimensionTravelResult result)
    {
      SendResult(targetConnection, requestId, result, !result.Accepted);
    }

    private void SendResult(
        Entity targetConnection,
        uint requestId,
        DimensionTravelResult result,
        bool isFinal)
    {
      if (targetConnection == Entity.Null)
      {
        DimensionLog.Problem(DimensionLogChannels.Travel, null, "Could not send dimension travel result because the target connection is null.");
        return;
      }

      Entity entity = EntityManager.CreateEntity(resultArchetype);
      EntityManager.SetComponentData(entity, new DimensionTravelResultRpc
      {
        RequestId = requestId,
        Accepted = result.Accepted ? (byte)1 : (byte)0,
        Final = isFinal ? (byte)1 : (byte)0,
        Code = DimensionFixedStrings.ToFixed64(result.Code),
        Message = DimensionFixedStrings.ToFixed128(result.Message),
        TravelId = DimensionFixedStrings.ToFixed64(result.TravelId),
        LoadTicketId = DimensionFixedStrings.ToFixed64(result.LoadTicketId),
        TargetDimensionId = DimensionFixedStrings.ToFixed64(result.TargetDimensionId),
        TargetLocalX = result.TargetLocalPosition.x,
        TargetLocalY = result.TargetLocalPosition.y,
        TargetAbsoluteX = result.TargetAbsolutePosition.x,
        TargetAbsoluteY = result.TargetAbsolutePosition.y
      });
      EntityManager.SetComponentData(entity, new SendRpcCommandRequest
      {
        TargetConnection = targetConnection
      });
    }

    private void SendCancelResult(
        Entity targetConnection,
        uint requestId,
        bool accepted,
        string code,
        string message,
        string travelId)
    {
      if (targetConnection == Entity.Null)
      {
        DimensionLog.Problem(DimensionLogChannels.Travel, null, "Could not send dimension travel cancel result because the target connection is null.");
        return;
      }

      Entity entity = EntityManager.CreateEntity(cancelResultArchetype);
      EntityManager.SetComponentData(entity, new DimensionTravelCancelResultRpc
      {
        RequestId = requestId,
        Accepted = accepted ? (byte)1 : (byte)0,
        Code = DimensionFixedStrings.ToFixed64(code),
        Message = DimensionFixedStrings.ToFixed128(message),
        TravelId = DimensionFixedStrings.ToFixed64(travelId)
      });
      EntityManager.SetComponentData(entity, new SendRpcCommandRequest
      {
        TargetConnection = targetConnection
      });
    }

    private void BindTravelResultRelay()
    {
      IDimensionService service;
      if (DimensionApi.TryGetService(out service) && service != null && service.IsReady)
      {
        travelResultRelay.Bind(service);
      }
      else
      {
        travelResultRelay.Bind(null);
      }
    }

  }

  [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public partial class DimensionTravelClientRpcSystem : SystemBase
  {
    private EntityQuery resultQuery;
    private EntityQuery cancelResultQuery;

    protected override void OnCreate()
    {
      resultQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionTravelResultRpc>(),
          ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
      cancelResultQuery = GetEntityQuery(
          ComponentType.ReadOnly<DimensionTravelCancelResultRpc>(),
          ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
      RequireAnyForUpdate(resultQuery, cancelResultQuery);
    }

    protected override void OnUpdate()
    {
      bool hasResults = !resultQuery.IsEmptyIgnoreFilter;
      bool hasCancelResults = !cancelResultQuery.IsEmptyIgnoreFilter;
      if (!hasResults && !hasCancelResults)
      {
        return;
      }

      if (hasResults)
      {
        using NativeArray<Entity> entities =
            resultQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<DimensionTravelResultRpc> results =
            resultQuery.ToComponentDataArray<DimensionTravelResultRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
          DimensionTravelNetworkState.CompleteRequest(results[i]);
          EntityManager.DestroyEntity(entities[i]);
        }
      }

      if (hasCancelResults)
      {
        using NativeArray<Entity> cancelEntities =
            cancelResultQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<DimensionTravelCancelResultRpc> cancelResults =
            cancelResultQuery.ToComponentDataArray<DimensionTravelCancelResultRpc>(Allocator.Temp);

        for (int i = 0; i < cancelEntities.Length; i++)
        {
          DimensionTravelNetworkState.CompleteCancelRequest(cancelResults[i]);
          EntityManager.DestroyEntity(cancelEntities[i]);
        }
      }
    }
  }
}
