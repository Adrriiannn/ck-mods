using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace ExpandNullforge.Networking
{
  public readonly struct DimensionTravelNetworkPendingRequest
  {
    public readonly uint RequestId;
    public readonly bool IsPortalRequest;
    public readonly string PortalId;
    public readonly string TargetDimensionId;
    public readonly float2 TargetLocalPosition;
    public readonly bool RequireGeneratedArea;
    public readonly bool AllowFallbackPosition;
    public readonly string Reason;

    public DimensionTravelNetworkPendingRequest(
        uint requestId,
        bool isPortalRequest,
        string portalId,
        string targetDimensionId,
        float2 targetLocalPosition,
        bool requireGeneratedArea,
        bool allowFallbackPosition,
        string reason)
    {
      RequestId = requestId;
      IsPortalRequest = isPortalRequest;
      PortalId = portalId ?? string.Empty;
      TargetDimensionId = targetDimensionId ?? string.Empty;
      TargetLocalPosition = targetLocalPosition;
      RequireGeneratedArea = requireGeneratedArea;
      AllowFallbackPosition = allowFallbackPosition;
      Reason = reason ?? string.Empty;
    }
  }

  public readonly struct DimensionTravelNetworkResult
  {
    public readonly uint RequestId;
    public readonly bool Accepted;
    public readonly bool IsFinal;
    public readonly string Code;
    public readonly string Message;
    public readonly string TravelId;
    public readonly string LoadTicketId;
    public readonly string TargetDimensionId;
    public readonly float2 TargetLocalPosition;
    public readonly float2 TargetAbsolutePosition;

    public DimensionTravelNetworkResult(
        uint requestId,
        bool accepted,
        bool isFinal,
        string code,
        string message,
        string travelId,
        string loadTicketId,
        string targetDimensionId,
        float2 targetLocalPosition,
        float2 targetAbsolutePosition)
    {
      RequestId = requestId;
      Accepted = accepted;
      IsFinal = isFinal;
      Code = code ?? string.Empty;
      Message = message ?? string.Empty;
      TravelId = travelId ?? string.Empty;
      LoadTicketId = loadTicketId ?? string.Empty;
      TargetDimensionId = targetDimensionId ?? string.Empty;
      TargetLocalPosition = targetLocalPosition;
      TargetAbsolutePosition = targetAbsolutePosition;
    }
  }

  public readonly struct DimensionTravelCancelNetworkResult
  {
    public readonly uint RequestId;
    public readonly bool Accepted;
    public readonly string Code;
    public readonly string Message;
    public readonly string TravelId;

    public DimensionTravelCancelNetworkResult(
        uint requestId,
        bool accepted,
        string code,
        string message,
        string travelId)
    {
      RequestId = requestId;
      Accepted = accepted;
      Code = code ?? string.Empty;
      Message = message ?? string.Empty;
      TravelId = travelId ?? string.Empty;
    }
  }

  public static class DimensionTravelNetworkState
  {
    private const double DeferredClientRequestRetrySeconds = 0.20d;
    private const double DeferredClientRequestTimeoutSeconds = 8.00d;

    private static uint nextRequestId;
    private static readonly Dictionary<uint, DimensionTravelNetworkPendingRequest> PendingRequests =
        new Dictionary<uint, DimensionTravelNetworkPendingRequest>();
    private static readonly Dictionary<uint, string> AcknowledgedTravelIdsByRequestId =
        new Dictionary<uint, string>();
    private static bool hasDeferredPortalTravelRequest;
    private static DimensionPortalTravelRequestRpc deferredPortalTravelRpc;
    private static DimensionTravelNetworkPendingRequest deferredPortalTravelRequest;
    private static double deferredPortalTravelCreatedAt;
    private static double nextDeferredPortalTravelAttemptAt;

    public static event Action<DimensionTravelNetworkPendingRequest> TravelRequestStarted;

    public static event Action<DimensionTravelNetworkResult> TravelRequestAcknowledged;

    public static event Action<DimensionTravelNetworkResult> TravelRequestCompleted;

    public static event Action<uint> TravelCancelRequestStarted;

    public static event Action<DimensionTravelCancelNetworkResult> TravelCancelRequestCompleted;

    public static uint RequestTravel(
        string targetDimensionId,
        float2 targetLocalPosition,
        bool requireGeneratedArea,
        bool allowFallbackPosition,
        string reason)
    {
      if (!TryGetClientEntityManager(out EntityManager entityManager))
      {
        return 0;
      }

      uint requestId = NextRequestId();
      Entity entity = entityManager.CreateEntity(
          typeof(DimensionTravelRequestRpc),
          typeof(SendRpcCommandRequest));
      entityManager.SetComponentData(entity, new DimensionTravelRequestRpc
      {
        RequestId = requestId,
        TargetDimensionId = ToFixed64(targetDimensionId),
        TargetLocalX = targetLocalPosition.x,
        TargetLocalY = targetLocalPosition.y,
        RequireGeneratedArea = requireGeneratedArea ? (byte)1 : (byte)0,
        AllowFallbackPosition = allowFallbackPosition ? (byte)1 : (byte)0,
        Reason = ToFixed128(reason)
      });

      RememberPending(
          new DimensionTravelNetworkPendingRequest(
              requestId,
              false,
              string.Empty,
              targetDimensionId,
              targetLocalPosition,
              requireGeneratedArea,
              allowFallbackPosition,
              reason));
      return requestId;
    }

    public static uint CancelTravel(
        string travelId,
        string reason)
    {
      if (string.IsNullOrEmpty(travelId))
      {
        return 0;
      }

      if (!TryGetClientEntityManager(out EntityManager entityManager))
      {
        return 0;
      }

      uint requestId = NextRequestId();
      Entity entity = entityManager.CreateEntity(
          typeof(DimensionTravelCancelRequestRpc),
          typeof(SendRpcCommandRequest));
      entityManager.SetComponentData(entity, new DimensionTravelCancelRequestRpc
      {
        RequestId = requestId,
        TravelId = ToFixed64(travelId),
        Reason = ToFixed128(reason)
      });

      Action<uint> startedHandler = TravelCancelRequestStarted;
      if (startedHandler != null)
      {
        startedHandler(requestId);
      }

      return requestId;
    }

    public static uint CancelTravel(
        uint originalRequestId,
        string reason)
    {
      string travelId;
      if (!TryGetAcknowledgedTravelId(originalRequestId, out travelId))
      {
        return 0;
      }

      return CancelTravel(travelId, reason);
    }

    public static uint RequestTravel(DimensionTravelRequest request)
    {
      return RequestTravel(
          request.TargetDimensionId,
          request.TargetLocalPosition,
          request.RequireGeneratedArea,
          request.AllowFallbackPosition,
          request.Reason);
    }

    public static uint RequestPortalTravel(
        string portalId,
        bool requireGeneratedArea,
        bool allowFallbackPosition,
        string reason)
    {
      if (string.IsNullOrEmpty(portalId))
      {
        Debug.LogWarning("[ExpandNullforge] Ignored portal travel request with an empty portal id.");
        return 0;
      }

      uint requestId = NextRequestId();
      DimensionPortalTravelRequestRpc rpc = new DimensionPortalTravelRequestRpc
      {
        RequestId = requestId,
        PortalId = ToFixed64(portalId),
        RequireGeneratedArea = requireGeneratedArea ? (byte)1 : (byte)0,
        AllowFallbackPosition = allowFallbackPosition ? (byte)1 : (byte)0,
        Reason = ToFixed128(reason)
      };

      DimensionTravelNetworkPendingRequest pending =
          new DimensionTravelNetworkPendingRequest(
              requestId,
              true,
              portalId,
              string.Empty,
              default(float2),
              requireGeneratedArea,
              allowFallbackPosition,
              reason);
      RememberPending(pending);

      if (!TryGetClientEntityManager(out EntityManager entityManager))
      {
        DeferPortalTravelRequest(rpc, pending);
        return requestId;
      }

      SendPortalTravelRpc(entityManager, rpc);
      return requestId;
    }

    public static uint RequestPortalTravel(DimensionPortalTravelRequest request)
    {
      return RequestPortalTravel(
          request.PortalId,
          request.RequireGeneratedArea,
          request.AllowFallbackPosition,
          request.Reason);
    }

    public static void CompleteRequest(DimensionTravelResultRpc rpc)
    {
      DimensionTravelNetworkResult result =
          new DimensionTravelNetworkResult(
              rpc.RequestId,
              rpc.Accepted != 0,
              rpc.Final != 0,
              rpc.Code.ToString(),
              rpc.Message.ToString(),
              rpc.TravelId.ToString(),
              rpc.LoadTicketId.ToString(),
              rpc.TargetDimensionId.ToString(),
              new float2(rpc.TargetLocalX, rpc.TargetLocalY),
              new float2(rpc.TargetAbsoluteX, rpc.TargetAbsoluteY));

      if (result.Accepted && !result.IsFinal)
      {
        if (DimensionFrameworkLog.VerboseRuntimeLogging)
        {
          DimensionFrameworkLog.Verbose(
              "[ExpandNullforge] Dimension travel request acknowledged. requestId=" +
              result.RequestId +
              " travelId=" +
              result.TravelId +
              " targetDimension=" +
              result.TargetDimensionId);
        }

        if (!string.IsNullOrEmpty(result.TravelId))
        {
          AcknowledgedTravelIdsByRequestId[rpc.RequestId] = result.TravelId;
        }

        Action<DimensionTravelNetworkResult> acknowledgedHandler = TravelRequestAcknowledged;
        if (acknowledgedHandler != null)
        {
          acknowledgedHandler(result);
        }

        return;
      }

      PendingRequests.Remove(rpc.RequestId);
      AcknowledgedTravelIdsByRequestId.Remove(rpc.RequestId);

      if (result.Accepted && result.IsFinal)
      {
        DimensionPlayerContextNetworkState.StartCurrentContextHydration(
            true,
            "Dimension travel completed; refresh current dimension context.");
      }

      if (DimensionFrameworkLog.VerboseRuntimeLogging)
      {
        DimensionFrameworkLog.Verbose(
            "[ExpandNullforge] Dimension travel request completed. requestId=" +
            result.RequestId +
            " accepted=" +
            result.Accepted +
            " final=" +
            result.IsFinal +
            " code=" +
            result.Code +
            " message=" +
            result.Message +
            " targetDimension=" +
            result.TargetDimensionId);
      }

      Action<DimensionTravelNetworkResult> handler = TravelRequestCompleted;
      if (handler != null)
      {
        handler(result);
      }
    }

    public static void CompleteCancelRequest(DimensionTravelCancelResultRpc rpc)
    {
      DimensionTravelCancelNetworkResult result =
          new DimensionTravelCancelNetworkResult(
              rpc.RequestId,
              rpc.Accepted != 0,
              rpc.Code.ToString(),
              rpc.Message.ToString(),
              rpc.TravelId.ToString());

      Action<DimensionTravelCancelNetworkResult> handler =
          TravelCancelRequestCompleted;
      if (handler != null)
      {
        handler(result);
      }
    }

    public static void UpdateDeferredClientRequests()
    {
      if (!hasDeferredPortalTravelRequest)
      {
        return;
      }

      double now = Time.realtimeSinceStartupAsDouble;
      if (now < nextDeferredPortalTravelAttemptAt)
      {
        return;
      }

      if (now - deferredPortalTravelCreatedAt > DeferredClientRequestTimeoutSeconds)
      {
        FailDeferredPortalTravelRequest(
            "client-rpc-not-ready",
            "The client was not ready to send the dimension portal request.");
        return;
      }

      if (!TryGetClientEntityManager(out EntityManager entityManager))
      {
        nextDeferredPortalTravelAttemptAt = now + DeferredClientRequestRetrySeconds;
        return;
      }

      SendPortalTravelRpc(entityManager, deferredPortalTravelRpc);
      hasDeferredPortalTravelRequest = false;
      deferredPortalTravelRpc = default(DimensionPortalTravelRequestRpc);
      deferredPortalTravelRequest = default(DimensionTravelNetworkPendingRequest);
      deferredPortalTravelCreatedAt = 0.0d;
      nextDeferredPortalTravelAttemptAt = 0.0d;
    }

    public static bool TryGetPendingRequest(
        uint requestId,
        out DimensionTravelNetworkPendingRequest request)
    {
      return PendingRequests.TryGetValue(requestId, out request);
    }

    public static bool TryGetAcknowledgedTravelId(
        uint requestId,
        out string travelId)
    {
      return AcknowledgedTravelIdsByRequestId.TryGetValue(requestId, out travelId);
    }

    public static void CopyPendingRequests(
        List<DimensionTravelNetworkPendingRequest> destination)
    {
      if (destination == null)
      {
        return;
      }

      destination.Clear();
      foreach (DimensionTravelNetworkPendingRequest request in PendingRequests.Values)
      {
        destination.Add(request);
      }
    }

    public static void Reset()
    {
      PendingRequests.Clear();
      AcknowledgedTravelIdsByRequestId.Clear();
      hasDeferredPortalTravelRequest = false;
      deferredPortalTravelRpc = default(DimensionPortalTravelRequestRpc);
      deferredPortalTravelRequest = default(DimensionTravelNetworkPendingRequest);
      deferredPortalTravelCreatedAt = 0.0d;
      nextDeferredPortalTravelAttemptAt = 0.0d;
      TravelRequestStarted = null;
      TravelRequestAcknowledged = null;
      TravelRequestCompleted = null;
      TravelCancelRequestStarted = null;
      TravelCancelRequestCompleted = null;
    }

    private static void RememberPending(DimensionTravelNetworkPendingRequest request)
    {
      PendingRequests[request.RequestId] = request;
      Action<DimensionTravelNetworkPendingRequest> handler = TravelRequestStarted;
      if (handler != null)
      {
        handler(request);
      }
    }

    private static void DeferPortalTravelRequest(
        DimensionPortalTravelRequestRpc rpc,
        DimensionTravelNetworkPendingRequest request)
    {
      if (hasDeferredPortalTravelRequest &&
          deferredPortalTravelRequest.RequestId != request.RequestId)
      {
        FailDeferredPortalTravelRequest(
            "client-rpc-replaced",
            "A newer dimension portal request replaced this pending request.");
      }

      hasDeferredPortalTravelRequest = true;
      deferredPortalTravelRpc = rpc;
      deferredPortalTravelRequest = request;
      deferredPortalTravelCreatedAt = Time.realtimeSinceStartupAsDouble;
      nextDeferredPortalTravelAttemptAt =
          deferredPortalTravelCreatedAt + DeferredClientRequestRetrySeconds;
    }

    private static void FailDeferredPortalTravelRequest(string code, string message)
    {
      if (!hasDeferredPortalTravelRequest)
      {
        return;
      }

      uint requestId = deferredPortalTravelRequest.RequestId;
      PendingRequests.Remove(requestId);
      AcknowledgedTravelIdsByRequestId.Remove(requestId);

      Action<DimensionTravelNetworkResult> handler = TravelRequestCompleted;
      if (handler != null)
      {
        handler(
            new DimensionTravelNetworkResult(
                requestId,
                false,
                true,
                code,
                message,
                string.Empty,
                string.Empty,
                string.Empty,
                default(float2),
                default(float2)));
      }

      Debug.LogWarning(
          "[ExpandNullforge] Dimension portal travel request could not be sent. requestId=" +
          requestId +
          " code=" +
          code +
          " message=" +
          message);

      hasDeferredPortalTravelRequest = false;
      deferredPortalTravelRpc = default(DimensionPortalTravelRequestRpc);
      deferredPortalTravelRequest = default(DimensionTravelNetworkPendingRequest);
      deferredPortalTravelCreatedAt = 0.0d;
      nextDeferredPortalTravelAttemptAt = 0.0d;
    }

    private static void SendPortalTravelRpc(
        EntityManager entityManager,
        DimensionPortalTravelRequestRpc rpc)
    {
      Entity entity = entityManager.CreateEntity(
          typeof(DimensionPortalTravelRequestRpc),
          typeof(SendRpcCommandRequest));
      entityManager.SetComponentData(entity, rpc);
    }

    private static uint NextRequestId()
    {
      nextRequestId++;
      if (nextRequestId == 0)
      {
        nextRequestId++;
      }

      return nextRequestId;
    }

    private static FixedString64Bytes ToFixed64(string value)
    {
      FixedString64Bytes result = default;
      if (string.IsNullOrEmpty(value))
      {
        return result;
      }

      int count = Math.Min(value.Length, 63);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }

      return result;
    }

    private static FixedString128Bytes ToFixed128(string value)
    {
      FixedString128Bytes result = default;
      if (string.IsNullOrEmpty(value))
      {
        return result;
      }

      int count = Math.Min(value.Length, 127);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }

      return result;
    }

    private static bool TryGetClientEntityManager(out EntityManager entityManager)
    {
      entityManager = default;
      return DimensionClientRpcReadiness.TryGetReadyEntityManager(out entityManager);
    }
  }
}
