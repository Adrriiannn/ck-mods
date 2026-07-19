using System;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Networking
{
  public static class DimensionTravelFeedbackState
  {
    private static bool initialized;
    private static DimensionTravelFeedbackSnapshot current =
        DimensionTravelFeedbackSnapshot.Idle(0);
    private static DimensionTravelFeedbackRetentionPolicy retentionPolicy =
        DimensionTravelFeedbackRetentionPolicy.Default();

    public static event Action<DimensionTravelFeedbackChangedEvent> FeedbackChanged;

    public static void Initialize()
    {
      if (initialized)
      {
        return;
      }

      DimensionTravelNetworkState.TravelRequestStarted += OnTravelRequestStarted;
      DimensionTravelNetworkState.TravelRequestAcknowledged += OnTravelRequestAcknowledged;
      DimensionTravelNetworkState.TravelRequestCompleted += OnTravelRequestCompleted;
      DimensionTravelNetworkState.TravelCancelRequestStarted += OnTravelCancelRequestStarted;
      DimensionTravelNetworkState.TravelCancelRequestCompleted += OnTravelCancelRequestCompleted;
      initialized = true;
    }

    public static void Reset()
    {
      if (initialized)
      {
        DimensionTravelNetworkState.TravelRequestStarted -= OnTravelRequestStarted;
        DimensionTravelNetworkState.TravelRequestAcknowledged -= OnTravelRequestAcknowledged;
        DimensionTravelNetworkState.TravelRequestCompleted -= OnTravelRequestCompleted;
        DimensionTravelNetworkState.TravelCancelRequestStarted -= OnTravelCancelRequestStarted;
        DimensionTravelNetworkState.TravelCancelRequestCompleted -= OnTravelCancelRequestCompleted;
      }

      initialized = false;
      current = DimensionTravelFeedbackSnapshot.Idle(Now());
      retentionPolicy = DimensionTravelFeedbackRetentionPolicy.Default();
      FeedbackChanged = null;
    }

    public static DimensionTravelFeedbackRetentionPolicy GetRetentionPolicy()
    {
      Initialize();
      return retentionPolicy;
    }

    public static void ConfigureRetentionPolicy(DimensionTravelFeedbackRetentionPolicy policy)
    {
      Initialize();
      retentionPolicy = policy;
    }

    public static DimensionTravelFeedbackSnapshot GetSnapshot()
    {
      Initialize();
      ExpireTerminalSnapshotIfNeeded();
      return current;
    }

    public static bool TryGetActiveSnapshot(out DimensionTravelFeedbackSnapshot snapshot)
    {
      Initialize();
      ExpireTerminalSnapshotIfNeeded();
      snapshot = current;
      return current.HasSnapshot && current.IsActive;
    }

    public static bool TryGetLastSnapshot(out DimensionTravelFeedbackSnapshot snapshot)
    {
      Initialize();
      ExpireTerminalSnapshotIfNeeded();
      snapshot = current;
      return current.HasSnapshot;
    }

    public static bool DismissTerminalSnapshot()
    {
      Initialize();
      if (!current.HasSnapshot || current.IsActive || !current.IsTerminal)
      {
        return false;
      }

      ClearSnapshot();
      return true;
    }

    public static void ClearSnapshot()
    {
      Initialize();
      if (!current.HasSnapshot)
      {
        return;
      }

      Publish(DimensionTravelFeedbackSnapshot.Idle(Now()));
    }

    private static void OnTravelRequestStarted(DimensionTravelNetworkPendingRequest request)
    {
      double now = Now();
      Publish(
          new DimensionTravelFeedbackSnapshot(
              true,
              true,
              false,
              request.RequestId,
              0,
              DimensionTravelFeedbackPhase.AwaitingServer,
              request.IsPortalRequest,
              request.PortalId,
              string.Empty,
              string.Empty,
              request.TargetDimensionId,
              request.TargetLocalPosition,
              default(float2),
              string.Empty,
              string.Empty,
              request.Reason,
              now,
              now));
    }

    private static void OnTravelRequestAcknowledged(DimensionTravelNetworkResult result)
    {
      DimensionTravelNetworkPendingRequest pending;
      bool hasPending = DimensionTravelNetworkState.TryGetPendingRequest(result.RequestId, out pending);
      double now = Now();
      double startedAt = current.RequestId == result.RequestId ? current.StartedAt : now;
      Publish(
          new DimensionTravelFeedbackSnapshot(
              true,
              true,
              false,
              result.RequestId,
              current.RequestId == result.RequestId ? current.CancelRequestId : 0,
              DimensionTravelFeedbackPhase.PreparingDestination,
              hasPending && pending.IsPortalRequest,
              hasPending ? pending.PortalId : string.Empty,
              result.TravelId,
              result.LoadTicketId,
              result.TargetDimensionId,
              result.TargetLocalPosition,
              result.TargetAbsolutePosition,
              result.Code,
              result.Message,
              hasPending ? pending.Reason : string.Empty,
              startedAt,
              now));
    }

    private static void OnTravelRequestCompleted(DimensionTravelNetworkResult result)
    {
      DimensionTravelNetworkPendingRequest pending;
      bool hasPending = DimensionTravelNetworkState.TryGetPendingRequest(result.RequestId, out pending);
      double now = Now();
      double startedAt = current.RequestId == result.RequestId ? current.StartedAt : now;
      DimensionTravelFeedbackPhase phase = ResolveTerminalPhase(result);
      Publish(
          new DimensionTravelFeedbackSnapshot(
              true,
              false,
              true,
              result.RequestId,
              current.RequestId == result.RequestId ? current.CancelRequestId : 0,
              phase,
              hasPending && pending.IsPortalRequest,
              hasPending ? pending.PortalId : current.PortalId,
              result.TravelId,
              result.LoadTicketId,
              result.TargetDimensionId,
              result.TargetLocalPosition,
              result.TargetAbsolutePosition,
              result.Code,
              result.Message,
              hasPending ? pending.Reason : current.Reason,
              startedAt,
              now));
    }

    private static void OnTravelCancelRequestStarted(uint cancelRequestId)
    {
      if (!current.HasSnapshot || !current.IsActive)
      {
        return;
      }

      Publish(
          new DimensionTravelFeedbackSnapshot(
              true,
              true,
              false,
              current.RequestId,
              cancelRequestId,
              DimensionTravelFeedbackPhase.CancelRequested,
              current.IsPortalRequest,
              current.PortalId,
              current.TravelId,
              current.LoadTicketId,
              current.TargetDimensionId,
              current.TargetLocalPosition,
              current.TargetAbsolutePosition,
              current.Code,
              "Cancelling travel...",
              current.Reason,
              current.StartedAt,
              Now()));
    }

    private static void OnTravelCancelRequestCompleted(DimensionTravelCancelNetworkResult result)
    {
      if (!current.HasSnapshot)
      {
        return;
      }

      bool matchesActiveTravel =
          !string.IsNullOrEmpty(result.TravelId) &&
          string.Equals(result.TravelId, current.TravelId, StringComparison.Ordinal);
      bool matchesCancelRequest = result.RequestId != 0 && result.RequestId == current.CancelRequestId;
      if (!matchesActiveTravel && !matchesCancelRequest)
      {
        return;
      }

      if (result.Accepted)
      {
        Publish(
            new DimensionTravelFeedbackSnapshot(
                true,
                false,
                true,
                current.RequestId,
                result.RequestId,
                DimensionTravelFeedbackPhase.Cancelled,
                current.IsPortalRequest,
                current.PortalId,
                result.TravelId,
                current.LoadTicketId,
                current.TargetDimensionId,
                current.TargetLocalPosition,
                current.TargetAbsolutePosition,
                result.Code,
                result.Message,
                current.Reason,
                current.StartedAt,
                Now()));
        return;
      }

      Publish(
          new DimensionTravelFeedbackSnapshot(
              true,
              current.IsActive,
              current.IsTerminal,
              current.RequestId,
              result.RequestId,
              current.Phase == DimensionTravelFeedbackPhase.CancelRequested
                  ? DimensionTravelFeedbackPhase.PreparingDestination
                  : current.Phase,
              current.IsPortalRequest,
              current.PortalId,
              current.TravelId,
              current.LoadTicketId,
              current.TargetDimensionId,
              current.TargetLocalPosition,
              current.TargetAbsolutePosition,
              result.Code,
              result.Message,
              current.Reason,
              current.StartedAt,
              Now()));
    }

    private static DimensionTravelFeedbackPhase ResolveTerminalPhase(
        DimensionTravelNetworkResult result)
    {
      if (result.Accepted)
      {
        return DimensionTravelFeedbackPhase.Completed;
      }

      if (ContainsCancelSignal(result.Code) || ContainsCancelSignal(result.Message))
      {
        return DimensionTravelFeedbackPhase.Cancelled;
      }

      return DimensionTravelFeedbackPhase.Failed;
    }

    private static bool ContainsCancelSignal(string value)
    {
      return !string.IsNullOrEmpty(value) &&
             value.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void Publish(DimensionTravelFeedbackSnapshot next)
    {
      DimensionTravelFeedbackSnapshot previous = current;
      current = next;
      Action<DimensionTravelFeedbackChangedEvent> handler = FeedbackChanged;
      if (handler != null)
      {
        handler(new DimensionTravelFeedbackChangedEvent(previous, next));
      }
    }

    private static void ExpireTerminalSnapshotIfNeeded()
    {
      if (!current.HasSnapshot || current.IsActive || !current.IsTerminal)
      {
        return;
      }

      double retentionSeconds = retentionPolicy.GetRetentionSeconds(current.Phase);
      if (retentionSeconds <= 0)
      {
        return;
      }

      if (Now() - current.UpdatedAt >= retentionSeconds)
      {
        ClearSnapshot();
      }
    }

    private static double Now()
    {
      return Time.realtimeSinceStartupAsDouble;
    }
  }
}
