using System;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;
using Pug.UnityExtensions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private static bool ShouldPreloadTravelDestination(DimensionDefinition definition)
    {
      return definition.HasCapability(DimensionCapabilityFlags.AreaLoading) &&
          definition.HasCapability(DimensionCapabilityFlags.SimulationLoading);
    }

    private void ProcessPendingTravel(double now)
    {
      if (!IsServerWorldAvailable())
      {
        MarkAllPendingTravelFailed("The server world is no longer available.");
        return;
      }

      pendingTravelKeys.Clear();
      foreach (string playerId in pendingTravelByPlayerId.Keys)
      {
        pendingTravelKeys.Add(playerId);
      }

      for (int i = 0; i < pendingTravelKeys.Count; i++)
      {
        string playerId = pendingTravelKeys[i];
        PendingTravelRecord record;
        if (!pendingTravelByPlayerId.TryGetValue(playerId, out record))
        {
          continue;
        }

        ProcessPendingTravelRecord(record, now);
      }
    }

    private void ProcessPendingTravelRecord(PendingTravelRecord record, double now)
    {
      EntityManager entityManager = serverWorld.EntityManager;
      if (record.Player == Entity.Null || !entityManager.Exists(record.Player))
      {
        FailPendingTravel(record, "player-missing", "The player entity no longer exists.");
        return;
      }

      if (record.State == DimensionTravelState.WaitingForDestinationLoad)
      {
        if (record.WaitingForGeneration)
        {
          ProcessPendingTravelGeneration(record, now);
          return;
        }

        DimensionLoadTicket ticket;
        if (!TryGetLoadStatus(record.LoadTicketId, out ticket) || !ticket.IsValid)
        {
          FailPendingTravel(record, "travel-preload-missing", "The travel destination load ticket disappeared.");
          return;
        }

        if (ticket.State == DimensionLoadState.Failed)
        {
          FailPendingTravel(record, "travel-preload-failed", ticket.Message);
          return;
        }

        if (now - record.LoadRequestedAt > TravelLoadTimeoutSeconds)
        {
          FailPendingTravel(record, "travel-preload-timeout", "The travel destination did not load in time.");
          return;
        }

        if (IsLoadTicketReady(ticket))
        {
          QueueVanillaTeleport(record, now);
        }

        return;
      }

      if (record.State == DimensionTravelState.TeleportQueued)
      {
        if (!entityManager.HasComponent<LocalTransform>(record.Player))
        {
          FailPendingTravel(record, "player-transform-missing", "The player transform disappeared during travel.");
          return;
        }

        LocalTransform transform = entityManager.GetComponentData<LocalTransform>(record.Player);
        float2 currentAbsolute = new float2(transform.Position.x, transform.Position.z);
        if (math.lengthsq(currentAbsolute - record.TargetAbsolutePosition) <= TravelArrivalDistanceSquared)
        {
          CompletePendingTravel(record, currentAbsolute, now);
          return;
        }

        if (record.TeleportQueuedAt > 0 && now - record.TeleportQueuedAt > TravelArrivalTimeoutSeconds)
        {
          if (TryRetargetPendingTravelToFallback(
              record,
              "travel-arrival-timeout",
              "The vanilla teleport did not reach the destination in time.",
              now))
          {
            return;
          }

          FailPendingTravel(record, "travel-arrival-timeout", "The vanilla teleport did not reach the destination in time.");
          return;
        }

        if (record.TeleportAttemptCount < TravelTeleportAttemptLimit &&
            now >= record.NextTeleportRetryAt)
        {
          QueueVanillaTeleport(record, now);
        }
      }
    }

    private void ProcessPendingTravelGeneration(PendingTravelRecord record, double now)
    {
      DimensionBounds generationBounds =
          IsValidPendingGenerationBounds(record.GenerationBounds)
              ? record.GenerationBounds
              : LocalTileAreaAround(record.TargetLocalPosition);

      DimensionGenerationStatus status;
      if (!TryGetGenerationStatus(record.TargetDimensionId, generationBounds, out status))
      {
        FailPendingTravel(
            record,
            "target-generation-status-missing",
            "The target generation status could not be resolved.");
        return;
      }

      record.GenerationBounds = status.LocalBounds;

      if (status.State == DimensionGenerationState.Failed)
      {
        FailPendingTravel(
            record,
            "target-area-generation-failed",
            string.IsNullOrEmpty(status.Message)
                ? "The target area generation failed."
                : status.Message);
        return;
      }

      if (status.State == DimensionGenerationState.Ready)
      {
        record.WaitingForGeneration = false;
        DimensionTravelRequest request =
            new DimensionTravelRequest(
                record.Player,
                record.TargetDimensionId,
                record.TargetLocalPosition,
                record.RequireGeneratedArea,
                record.AllowFallbackPosition,
                record.PortalId,
                record.Reason);

        DimensionAccessResult accessResult;
        if (!CanTravel(request, out accessResult))
        {
          if (TryRetargetPendingTravelToFallback(
              record,
              accessResult.Code,
              accessResult.Message,
              now))
          {
            return;
          }

          FailPendingTravel(record, accessResult.Code, accessResult.Message);
          return;
        }

        string failureCode;
        string failureMessage;
        if (!TryStartPendingTravelDestinationLoad(
            record,
            now,
            out failureCode,
            out failureMessage) &&
            record.State != DimensionTravelState.Failed)
        {
          FailPendingTravel(record, failureCode, failureMessage);
        }

        return;
      }

      if (!IsTransientGenerationState(status.State))
      {
        FailPendingTravel(
            record,
            "target-area-generation-not-active",
            string.IsNullOrEmpty(status.Message)
                ? "The target area generation is not active."
                : status.Message);
        return;
      }

      if (now - record.CreatedAt > GenerationLoadTimeoutSeconds)
      {
        FailPendingTravel(
            record,
            "target-area-generation-timeout",
            "The target area generation did not finish in time.");
        return;
      }

      string message = string.IsNullOrEmpty(status.Message)
          ? "Waiting for target area generation."
          : status.Message;
      if (!string.Equals(record.Message, message, StringComparison.Ordinal))
      {
        record.Message = message;
        record.UpdatedAt = now;
        RaiseTravelUpdated(record);
      }
    }

    private bool TryStartPendingTravelDestinationLoad(
        PendingTravelRecord record,
        double now,
        out string failureCode,
        out string failureMessage)
    {
      failureCode = string.Empty;
      failureMessage = string.Empty;

      DimensionDefinition targetDefinition;
      if (!TryGetDimension(record.TargetDimensionId, out targetDefinition))
      {
        failureCode = "target-dimension-not-registered";
        failureMessage = "The target dimension is not registered.";
        return false;
      }

      ReleaseTravelLoadTicket(record);
      bool preloadDestination = ShouldPreloadTravelDestination(targetDefinition);
      if (preloadDestination)
      {
        DimensionBounds preloadBounds =
            LocalTravelPreloadAreaAround(record.TargetLocalPosition);
        DimensionLoadTicket loadTicket =
            RequestLoad(
                new DimensionLoadRequest(
                    "dimension-travel:" + record.PlayerId,
                    record.TargetDimensionId,
                    preloadBounds,
                    true,
                    true,
                    TravelLoadTimeoutSeconds,
                    string.IsNullOrEmpty(record.Reason)
                        ? "Dimension travel destination preload."
                        : record.Reason));

        if (!loadTicket.IsValid)
        {
          failureCode = "travel-preload-failed";
          failureMessage = loadTicket.Message;
          return false;
        }

        record.LoadTicketId = loadTicket.TicketId;
        record.LoadRequestedAt = now;
        record.WaitingForGeneration = false;
        record.GenerationBounds = default(DimensionBounds);
        record.State = DimensionTravelState.WaitingForDestinationLoad;
        record.Message = "Waiting for destination area to load.";
        record.UpdatedAt = now;
        record.TeleportQueuedAt = 0;
        record.NextTeleportRetryAt = 0;
        record.TeleportAttemptCount = 0;
        record.ScheduledTick = NetworkTick.Invalid;
        RaiseTravelUpdated(record);
        AddDiagnostic(
            DimensionDiagnosticSeverity.Info,
            record.TargetDimensionId,
            "Dimension travel " + record.TravelId +
            " target generation completed; destination preload queued.");
        return true;
      }

      if (!string.Equals(targetDefinition.Id, DimensionIds.Overworld, StringComparison.Ordinal))
      {
        failureCode = "travel-preload-unsupported";
        failureMessage = "The target dimension does not support destination preloading.";
        return false;
      }

      record.WaitingForGeneration = false;
      record.GenerationBounds = default(DimensionBounds);
      QueueVanillaTeleport(record, now);
      return record.State != DimensionTravelState.Failed;
    }

    private static bool IsValidPendingGenerationBounds(DimensionBounds bounds)
    {
      int2 size = bounds.Size;
      return size.x > 0 && size.y > 0;
    }

    private void QueueVanillaTeleport(PendingTravelRecord record, double now)
    {
      EntityManager entityManager = serverWorld.EntityManager;
      if (!entityManager.HasComponent<UIActionBuffer>(record.Player))
      {
        FailPendingTravel(record, "player-action-buffer-missing", "The player is missing its UI action buffer.");
        return;
      }

      NetworkTick tick;
      if (!TryGetNextServerActionTick(out tick))
      {
        FailPendingTravel(record, "network-time-unavailable", "The server network time is not available.");
        return;
      }

      DynamicBuffer<UIActionBuffer> actionBuffer = entityManager.GetBuffer<UIActionBuffer>(record.Player);
      actionBuffer.Add(new UIActionBuffer
      {
        tick = tick,
        actionData = new UIInputActionData
        {
          action = UIInputAction.Teleport,
          position = record.TargetAbsolutePosition
        }
      });

      record.ScheduledTick = tick;
      record.TeleportQueuedAt = now;
      record.TeleportAttemptCount++;
      record.NextTeleportRetryAt = now + TravelTeleportRetryIntervalSeconds;
      record.UpdatedAt = now;
      record.State = DimensionTravelState.TeleportQueued;
      record.Message = record.TeleportAttemptCount <= 1
          ? "Vanilla teleport action queued."
          : "Vanilla teleport action retried (" + record.TeleportAttemptCount + "/" + TravelTeleportAttemptLimit + ").";
      RaiseTravelUpdated(record);
      AddDiagnostic(
          DimensionDiagnosticSeverity.Info,
          record.TargetDimensionId,
          "Vanilla teleport queued for dimension travel " + record.TravelId + " attempt " + record.TeleportAttemptCount + ".");
    }

    private bool TryRetargetPendingTravelToFallback(
        PendingTravelRecord record,
        string failureCode,
        string failureMessage,
        double now)
    {
      if (record == null ||
          !record.AllowFallbackPosition ||
          record.FallbackAttempted)
      {
        return false;
      }

      DimensionAnchorDefinition anchor;
      if (!TryResolveTravelFallbackAnchor(record.TargetDimensionId, out anchor))
      {
        return false;
      }

      if (anchor.LocalPosition.x == record.TargetLocalPosition.x &&
          anchor.LocalPosition.y == record.TargetLocalPosition.y)
      {
        return false;
      }

      DimensionDefinition target;
      if (!TryGetDimension(record.TargetDimensionId, out target) ||
          !target.ContainsLocal(anchor.LocalPosition))
      {
        return false;
      }

      DimensionBounds fallbackBounds = LocalTravelPreloadAreaAround(anchor.LocalPosition);
      if (record.RequireGeneratedArea &&
          !IsAreaGenerated(record.TargetDimensionId, LocalTileAreaAround(anchor.LocalPosition)))
      {
        AddDiagnostic(
            DimensionDiagnosticSeverity.Warning,
            record.TargetDimensionId,
            "Travel " + record.TravelId + " could not use fallback anchor " + anchor.AnchorId +
            " because the fallback target is not generated.");
        return false;
      }

      ReleaseTravelLoadTicket(record);
      DimensionLoadTicket loadTicket =
          RequestLoad(
              new DimensionLoadRequest(
                  "dimension-travel-fallback:" + record.PlayerId,
                  record.TargetDimensionId,
                  fallbackBounds,
                  true,
                  true,
                  TravelLoadTimeoutSeconds,
                  string.IsNullOrEmpty(record.Reason)
                      ? "Dimension travel fallback destination preload."
                      : record.Reason + " Fallback destination preload."));

      if (!loadTicket.IsValid)
      {
        AddDiagnostic(
            DimensionDiagnosticSeverity.Warning,
            record.TargetDimensionId,
            "Travel " + record.TravelId + " fallback anchor " + anchor.AnchorId +
            " could not be preloaded: " + loadTicket.Message);
        return false;
      }

      record.TargetLocalPosition = anchor.LocalPosition;
      record.TargetAbsolutePosition = target.ToAbsolute(anchor.LocalPosition);
      record.LoadTicketId = loadTicket.TicketId;
      record.FallbackAttempted = true;
      record.WaitingForGeneration = false;
      record.GenerationBounds = default(DimensionBounds);
      record.State = DimensionTravelState.WaitingForDestinationLoad;
      record.Message =
          "Retargeted to fallback landing anchor " + anchor.AnchorId + " after " +
          (string.IsNullOrEmpty(failureCode) ? "travel failure" : failureCode) + ".";
      record.UpdatedAt = now;
      record.LoadRequestedAt = now;
      record.TeleportQueuedAt = 0;
      record.NextTeleportRetryAt = 0;
      record.TeleportAttemptCount = 0;
      record.ScheduledTick = NetworkTick.Invalid;
      RaiseTravelUpdated(record);
      AddDiagnostic(
          DimensionDiagnosticSeverity.Warning,
          record.TargetDimensionId,
          "Dimension travel " + record.TravelId +
          (string.IsNullOrEmpty(record.PortalId) ? string.Empty : " from portal " + record.PortalId) +
          " retargeted to fallback anchor " +
          anchor.AnchorId + " after " + (string.IsNullOrEmpty(failureMessage) ? failureCode : failureMessage) + ".");
      return true;
    }

    private void CompletePendingTravel(PendingTravelRecord record, float2 currentAbsolute, double now)
    {
      record.State = DimensionTravelState.Completed;
      record.Message = "Travel completed.";
      record.UpdatedAt = now;
      RaiseTravelUpdated(record);

      if (string.Equals(record.PreviousDimensionId, DimensionIds.Overworld, StringComparison.Ordinal) &&
          !string.Equals(record.TargetDimensionId, DimensionIds.Overworld, StringComparison.Ordinal))
      {
        DimensionWorldRegistry.UpsertPlayerVisit(
            record.PlayerId,
            DimensionIds.Overworld,
            record.PreviousAbsolutePosition,
            record.PreviousAbsolutePosition);
      }

      DimensionWorldRegistry.UpsertPlayerState(
          record.PlayerId,
          record.TargetDimensionId,
          record.TargetLocalPosition,
          currentAbsolute);

      DimensionWorldRegistry.UpsertPlayerVisit(
          record.PlayerId,
          record.TargetDimensionId,
          record.TargetLocalPosition,
          currentAbsolute);

      RaisePlayerDimensionChanged(
          new DimensionChangedEvent(
              record.Player,
              record.PreviousDimensionId,
              record.TargetDimensionId,
              record.PreviousAbsolutePosition,
              currentAbsolute));

      AddDiagnostic(
          DimensionDiagnosticSeverity.Info,
          record.TargetDimensionId,
          "Dimension travel completed for player " + record.PlayerId + ".");

      ReleaseTravelLoadTicket(record);
      pendingTravelByPlayerId.Remove(record.PlayerId);
    }

    private void FailPendingTravel(PendingTravelRecord record, string code, string message)
    {
      record.State = DimensionTravelState.Failed;
      record.Message = string.IsNullOrEmpty(message) ? code : message;
      record.UpdatedAt = Time.realtimeSinceStartupAsDouble;
      RaiseTravelUpdated(record);
      AddDiagnostic(
          DimensionDiagnosticSeverity.Warning,
          record.TargetDimensionId,
          "Dimension travel failed for player " + record.PlayerId + ": " + record.Message);

      ReleaseTravelLoadTicket(record);
      pendingTravelByPlayerId.Remove(record.PlayerId);
    }

    private void CancelPendingTravel(PendingTravelRecord record, string message)
    {
      record.State = DimensionTravelState.Cancelled;
      record.Message = string.IsNullOrEmpty(message) ? "Travel cancelled." : message;
      record.UpdatedAt = Time.realtimeSinceStartupAsDouble;
      RaiseTravelUpdated(record);
      AddDiagnostic(
          DimensionDiagnosticSeverity.Info,
          record.TargetDimensionId,
          "Dimension travel cancelled for player " + record.PlayerId + ": " + record.Message);

      ReleaseTravelLoadTicket(record);
      pendingTravelByPlayerId.Remove(record.PlayerId);
    }

    private void MarkAllPendingTravelFailed(string message)
    {
      pendingTravelKeys.Clear();
      foreach (string playerId in pendingTravelByPlayerId.Keys)
      {
        pendingTravelKeys.Add(playerId);
      }

      for (int i = 0; i < pendingTravelKeys.Count; i++)
      {
        PendingTravelRecord record;
        if (pendingTravelByPlayerId.TryGetValue(pendingTravelKeys[i], out record))
        {
          FailPendingTravel(record, "travel-world-unavailable", message);
        }
      }
    }

    private bool TryResolvePendingTravelForCancellation(
        DimensionTravelCancelRequest request,
        out PendingTravelRecord record,
        out DimensionOperationResult result)
    {
      record = null;
      if (request.Player == Entity.Null && string.IsNullOrEmpty(request.TravelId))
      {
        result =
            DimensionOperationResult.Failed(
                "travel-cancel-target-empty",
                "A player entity or travel id is required to cancel dimension travel.");
        return false;
      }

      PendingTravelRecord recordByTravelId;
      bool hasRecordByTravelId =
          TryFindPendingTravelByTravelId(request.TravelId, out recordByTravelId);

      if (request.Player != Entity.Null)
      {
        string playerId;
        if (!TryGetPlayerPersistentId(request.Player, out playerId))
        {
          if (hasRecordByTravelId)
          {
            record = recordByTravelId;
            result = DimensionOperationResult.Ok();
            return true;
          }

          result =
              DimensionOperationResult.Failed(
                  "player-identity-unavailable",
                  "The player entity does not expose a stable player identity yet.");
          return false;
        }

        if (hasRecordByTravelId)
        {
          if (!string.Equals(recordByTravelId.PlayerId, playerId, StringComparison.Ordinal))
          {
            result =
                DimensionOperationResult.Failed(
                    "travel-cancel-player-mismatch",
                    "The requested travel id belongs to a different player.");
            return false;
          }

          record = recordByTravelId;
          result = DimensionOperationResult.Ok();
          return true;
        }

        if (pendingTravelByPlayerId.TryGetValue(playerId, out record))
        {
          result = DimensionOperationResult.Ok();
          return true;
        }

        result =
            DimensionOperationResult.Failed(
                "travel-not-found",
                "No pending dimension travel exists for the requested player.");
        return false;
      }

      if (hasRecordByTravelId)
      {
        record = recordByTravelId;
        result = DimensionOperationResult.Ok();
        return true;
      }

      result =
          DimensionOperationResult.Failed(
              "travel-not-found",
              "No pending dimension travel exists for the requested travel id.");
      return false;
    }

    private bool TryFindPendingTravelByTravelId(
        string travelId,
        out PendingTravelRecord record)
    {
      record = null;
      if (string.IsNullOrEmpty(travelId))
      {
        return false;
      }

      foreach (PendingTravelRecord pendingRecord in pendingTravelByPlayerId.Values)
      {
        if (string.Equals(pendingRecord.TravelId, travelId, StringComparison.Ordinal))
        {
          record = pendingRecord;
          return true;
        }
      }

      return false;
    }

    private void ReleaseTravelLoadTicket(PendingTravelRecord record)
    {
      if (record == null || string.IsNullOrEmpty(record.LoadTicketId))
      {
        return;
      }

      ReleaseLoadTicket(record.LoadTicketId);
      record.LoadTicketId = string.Empty;
    }

    private bool TryGetNextServerActionTick(out NetworkTick tick)
    {
      tick = NetworkTick.Invalid;
      if (!IsServerWorldAvailable() || !serverQueriesCreated)
      {
        return false;
      }

      NetworkTime networkTime;
      if (!networkTimeQuery.TryGetSingleton<NetworkTime>(out networkTime))
      {
        return false;
      }

      tick = networkTime.ServerTick;
      tick.Add((uint)UIInputActionData.EXECUTION_TICK_OFFSET);
      return tick.IsValid;
    }

    private bool TryGetPlayerPersistentId(Entity player, out string playerId)
    {
      playerId = string.Empty;
      if (!IsServerWorldAvailable() || player == Entity.Null)
      {
        return false;
      }

      EntityManager entityManager = serverWorld.EntityManager;
      if (!entityManager.Exists(player) || !entityManager.HasComponent<PlayerGhost>(player))
      {
        return false;
      }

      PlayerGhost playerGhost = entityManager.GetComponentData<PlayerGhost>(player);
      ulong persistentId = playerGhost.onlineId;
      if (persistentId == 0UL)
      {
        persistentId = HashPlayerGuid(playerGhost.playerGuid);
      }

      if (persistentId == 0UL)
      {
        persistentId = unchecked((ulong)(playerGhost.playerIndex + 1));
      }

      playerId = "player:" + persistentId;
      return true;
    }

    private ulong HashPlayerGuid(Unity.Entities.Hash128 guid)
    {
      string value = guid.ToString();
      if (string.IsNullOrEmpty(value))
      {
        return 0UL;
      }

      const ulong offset = 14695981039346656037UL;
      const ulong prime = 1099511628211UL;
      ulong hash = offset;
      for (int i = 0; i < value.Length; i++)
      {
        hash ^= value[i];
        hash *= prime;
      }

      return hash == 0UL ? 1UL : hash;
    }

    private DimensionBounds LocalTravelPreloadAreaAround(float2 localPosition)
    {
      int2 tile = new int2((int)math.floor(localPosition.x), (int)math.floor(localPosition.y));
      int half = TravelPreloadSideTiles / 2;
      int2 min = tile - new int2(half, half);
      return new DimensionBounds(min, min + new int2(TravelPreloadSideTiles, TravelPreloadSideTiles));
    }

    private string CreateTravelId()
    {
      return "travel-" + Guid.NewGuid().ToString("N");
    }

    private DimensionTravelSnapshot ToTravelSnapshot(PendingTravelRecord record)
    {
      return new DimensionTravelSnapshot(
          record.TravelId,
          record.Player,
          record.PlayerId,
          record.PreviousDimensionId,
          record.TargetDimensionId,
          record.PreviousAbsolutePosition,
          record.TargetLocalPosition,
          record.TargetAbsolutePosition,
          record.LoadTicketId,
          record.State,
          record.Message,
          record.CreatedAt,
          record.UpdatedAt);
    }

    private bool TravelSnapshotMatchesQuery(
        DimensionTravelSnapshot snapshot,
        DimensionTravelSnapshotQuery query)
    {
      if (!string.IsNullOrEmpty(query.TravelId) &&
          !string.Equals(snapshot.TravelId, query.TravelId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.PlayerId) &&
          !string.Equals(snapshot.PlayerId, query.PlayerId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.TargetDimensionId) &&
          !string.Equals(snapshot.TargetDimensionId, query.TargetDimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.MatchState && snapshot.State != query.State)
      {
        return false;
      }

      return true;
    }

    private static int CompareTravelSnapshots(
        DimensionTravelSnapshot left,
        DimensionTravelSnapshot right)
    {
      int created = left.CreatedAt.CompareTo(right.CreatedAt);
      if (created != 0)
      {
        return created;
      }

      return string.Compare(left.TravelId, right.TravelId, StringComparison.Ordinal);
    }

    private void RaiseTravelUpdated(PendingTravelRecord record)
    {
      Action<DimensionTravelSnapshot> handler = DimensionTravelUpdated;
      if (handler != null && record != null)
      {
        handler(ToTravelSnapshot(record));
      }
    }
  }
}
