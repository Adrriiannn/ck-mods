using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace ExpandNullforge.Networking
{
  internal sealed class DimensionTravelResultRelay
  {
    private const double StaleReplyTimeoutSeconds = 300.0d;
    private const double StaleReplyCleanupIntervalSeconds = 5.0d;

    private readonly Dictionary<string, PendingTravelReply> pendingRepliesByTravelId =
        new Dictionary<string, PendingTravelReply>();

    private readonly List<DimensionTravelSnapshot> queuedUpdates =
        new List<DimensionTravelSnapshot>();

    private readonly List<string> staleTravelIds =
        new List<string>();

    private IDimensionService boundService;
    private double nextStaleCleanupAt;

    public bool HasPendingWork(double now)
    {
      return queuedUpdates.Count > 0 ||
             ShouldCleanupStaleReplies(now);
    }

    public void Bind(IDimensionService service)
    {
      if (object.ReferenceEquals(boundService, service))
      {
        return;
      }

      if (boundService != null)
      {
        boundService.DimensionTravelUpdated -= OnDimensionTravelUpdated;
      }

      boundService = service;

      if (boundService != null)
      {
        boundService.DimensionTravelUpdated += OnDimensionTravelUpdated;
      }
    }

    public void Dispose()
    {
      Bind(null);
      pendingRepliesByTravelId.Clear();
      queuedUpdates.Clear();
      staleTravelIds.Clear();
      nextStaleCleanupAt = 0.0d;
    }

    public void Track(
        DimensionTravelResult result,
        Entity targetConnection,
        uint requestId,
        double now)
    {
      if (!result.Accepted ||
          string.IsNullOrEmpty(result.TravelId) ||
          targetConnection == Entity.Null ||
          requestId == 0)
      {
        return;
      }

      pendingRepliesByTravelId[result.TravelId] =
          new PendingTravelReply(targetConnection, requestId, now);
    }

    public void Flush(
        EntityManager entityManager,
        EntityArchetype resultArchetype,
        double now)
    {
      for (int i = 0; i < queuedUpdates.Count; i++)
      {
        FlushUpdate(entityManager, resultArchetype, queuedUpdates[i]);
      }

      queuedUpdates.Clear();
      if (ShouldCleanupStaleReplies(now))
      {
        nextStaleCleanupAt = now + StaleReplyCleanupIntervalSeconds;
        CleanupStaleReplies(entityManager, now);
      }
    }

    private bool ShouldCleanupStaleReplies(double now)
    {
      return pendingRepliesByTravelId.Count > 0 &&
             (nextStaleCleanupAt <= 0.0d || now >= nextStaleCleanupAt);
    }

    private void OnDimensionTravelUpdated(DimensionTravelSnapshot snapshot)
    {
      queuedUpdates.Add(snapshot);
    }

    private void FlushUpdate(
        EntityManager entityManager,
        EntityArchetype resultArchetype,
        DimensionTravelSnapshot snapshot)
    {
      if (!IsFinalState(snapshot.State) || string.IsNullOrEmpty(snapshot.TravelId))
      {
        return;
      }

      PendingTravelReply reply;
      if (!pendingRepliesByTravelId.TryGetValue(snapshot.TravelId, out reply))
      {
        return;
      }

      pendingRepliesByTravelId.Remove(snapshot.TravelId);

      if (reply.TargetConnection == Entity.Null ||
          !entityManager.Exists(reply.TargetConnection))
      {
        return;
      }

      Entity entity = entityManager.CreateEntity(resultArchetype);
      entityManager.SetComponentData(entity, ToRpc(reply.RequestId, snapshot));
      entityManager.SetComponentData(entity, new SendRpcCommandRequest
      {
        TargetConnection = reply.TargetConnection
      });
    }

    private void CleanupStaleReplies(
        EntityManager entityManager,
        double now)
    {
      staleTravelIds.Clear();

      foreach (KeyValuePair<string, PendingTravelReply> pair in pendingRepliesByTravelId)
      {
        if (pair.Value.TargetConnection == Entity.Null ||
            !entityManager.Exists(pair.Value.TargetConnection) ||
            now - pair.Value.CreatedAt >= StaleReplyTimeoutSeconds)
        {
          staleTravelIds.Add(pair.Key);
        }
      }

      for (int i = 0; i < staleTravelIds.Count; i++)
      {
        pendingRepliesByTravelId.Remove(staleTravelIds[i]);
      }

      staleTravelIds.Clear();
    }

    private static DimensionTravelResultRpc ToRpc(
        uint requestId,
        DimensionTravelSnapshot snapshot)
    {
      bool success = snapshot.State == DimensionTravelState.Completed ||
          snapshot.State == DimensionTravelState.Arrived;

      return new DimensionTravelResultRpc
      {
        RequestId = requestId,
        Accepted = success ? (byte)1 : (byte)0,
        Final = 1,
        Code = ToFixed64(ToCode(snapshot.State)),
        Message = ToFixed128(snapshot.Message),
        TravelId = ToFixed64(snapshot.TravelId),
        LoadTicketId = ToFixed64(snapshot.LoadTicketId),
        TargetDimensionId = ToFixed64(snapshot.TargetDimensionId),
        TargetLocalX = snapshot.TargetLocalPosition.x,
        TargetLocalY = snapshot.TargetLocalPosition.y,
        TargetAbsoluteX = snapshot.TargetAbsolutePosition.x,
        TargetAbsoluteY = snapshot.TargetAbsolutePosition.y
      };
    }

    private static string ToCode(DimensionTravelState state)
    {
      switch (state)
      {
        case DimensionTravelState.Completed:
          return "travel-completed";
        case DimensionTravelState.Arrived:
          return "travel-arrived";
        case DimensionTravelState.Cancelled:
          return "travel-cancelled";
        case DimensionTravelState.Failed:
          return "travel-failed";
        default:
          return "travel-finished";
      }
    }

    private static bool IsFinalState(DimensionTravelState state)
    {
      return state == DimensionTravelState.Arrived ||
          state == DimensionTravelState.Completed ||
          state == DimensionTravelState.Failed ||
          state == DimensionTravelState.Cancelled;
    }

    private static FixedString64Bytes ToFixed64(string value)
    {
      FixedString64Bytes result = default;
      if (string.IsNullOrEmpty(value))
      {
        return result;
      }

      int count = math.min(value.Length, 63);
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

      int count = math.min(value.Length, 127);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }

      return result;
    }

    private readonly struct PendingTravelReply
    {
      public readonly Entity TargetConnection;
      public readonly uint RequestId;
      public readonly double CreatedAt;

      public PendingTravelReply(
          Entity targetConnection,
          uint requestId,
          double createdAt)
      {
        TargetConnection = targetConnection;
        RequestId = requestId;
        CreatedAt = createdAt;
      }
    }
  }
}
