using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Generating the far side of a journey before the traveller arrives.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    private bool TryQueueTravelTargetGeneration(
        DimensionTravelRequest request,
        DimensionAccessResult accessResult,
        out DimensionTravelResult result)
    {
      result = default(DimensionTravelResult);
      if (!request.RequireGeneratedArea ||
          !string.Equals(accessResult.Code, TargetAreaNotGeneratedCode, StringComparison.Ordinal))
      {
        return false;
      }

      if (!IsServerWorldAvailable())
      {
        result = DimensionTravelResult.Failed(
            "server-world-unavailable",
            "Dimension target generation must be requested on the authoritative server world.");
        return true;
      }

      string requesterId;
      if (!TryGetPlayerPersistentId(request.Player, out requesterId))
      {
        requesterId = "dimension-travel";
      }

      DimensionBounds bounds = TravelTargetGenerationAreaAround(request);
      DimensionGenerationStatus generationStatus =
          RequestGeneration(
              new DimensionGenerationRequest(
                  requesterId,
                  request.TargetDimensionId,
                  bounds,
                  0,
                  true,
                  "Travel target generation."));

      if (generationStatus.State == DimensionGenerationState.Failed)
      {
        result = DimensionTravelResult.Failed(
            "target-area-generation-failed",
            generationStatus.Message);
        return true;
      }

      if (generationStatus.State == DimensionGenerationState.Ready)
      {
        result = DimensionTravelResult.Failed(
            TargetAreaGenerationReadyRetryCode,
            "The target area is generated now. Try the travel request again.");
        return true;
      }

      AddDiagnostic(
          DimensionDiagnosticSeverity.Info,
          request.TargetDimensionId,
          "Queued generation for travel target " + request.TargetLocalPosition + ".");

      PendingTravelRecord record;
      DimensionTravelResult failureResult;
      if (!TryCreatePendingTravelRecord(
          request,
          string.Empty,
          0.0d,
          true,
          bounds,
          string.IsNullOrEmpty(generationStatus.Message)
              ? "Waiting for target area generation."
              : generationStatus.Message,
          out record,
          out failureResult))
      {
        result = failureResult;
        return true;
      }

      result =
          DimensionTravelResult.AcceptedRequest(
              record.TravelId,
              record.LoadTicketId,
              record.TargetDimensionId,
              record.TargetLocalPosition,
              record.TargetAbsolutePosition);
      return true;
    }

    private bool TryCreatePendingTravelRecord(
        DimensionTravelRequest request,
        string loadTicketId,
        double loadRequestedAt,
        bool waitingForGeneration,
        DimensionBounds generationBounds,
        string message,
        out PendingTravelRecord record,
        out DimensionTravelResult failureResult)
    {
      record = null;
      failureResult = default(DimensionTravelResult);
      if (!IsServerWorldAvailable())
      {
        failureResult =
            DimensionTravelResult.Failed(
                "server-world-unavailable",
                "Dimension travel must be requested on the authoritative server world.");
        return false;
      }

      EntityManager entityManager = serverWorld.EntityManager;
      if (!entityManager.Exists(request.Player) ||
          !entityManager.HasComponent<Unity.Transforms.LocalTransform>(request.Player) ||
          !entityManager.HasComponent<UIActionBuffer>(request.Player))
      {
        failureResult =
            DimensionTravelResult.Failed(
                "player-not-ready",
                "The player entity is not ready for a vanilla teleport action.");
        return false;
      }

      float2 absolutePosition;
      if (!TryToAbsolute(request.TargetDimensionId, request.TargetLocalPosition, out absolutePosition))
      {
        failureResult =
            DimensionTravelResult.Failed(
                "target-position-unresolved",
                "The target position could not be resolved.");
        return false;
      }

      string playerId;
      if (!TryGetPlayerPersistentId(request.Player, out playerId))
      {
        failureResult =
            DimensionTravelResult.Failed(
                "player-identity-unavailable",
                "The player entity does not expose a stable player identity yet.");
        return false;
      }

      Unity.Transforms.LocalTransform playerTransform =
          entityManager.GetComponentData<Unity.Transforms.LocalTransform>(request.Player);
      float2 previousAbsolute =
          new float2(playerTransform.Position.x, playerTransform.Position.z);
      DimensionContext previousContext = GetContextForAbsolute(previousAbsolute);
      string previousDimensionId =
          previousContext.IsKnown ? previousContext.DimensionId : DimensionIds.Overworld;

      double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
      PendingTravelRecord existingTravel;
      if (pendingTravelByPlayerId.TryGetValue(playerId, out existingTravel))
      {
        ReleaseTravelLoadTicket(existingTravel);
        existingTravel.State = DimensionTravelState.Cancelled;
        existingTravel.Message = "Replaced by a newer travel request.";
        existingTravel.UpdatedAt = now;
        RaiseTravelUpdated(existingTravel);
      }

      record = new PendingTravelRecord
      {
        TravelId = CreateTravelId(),
        Player = request.Player,
        PlayerId = playerId,
        PreviousDimensionId = previousDimensionId,
        PreviousAbsolutePosition = previousAbsolute,
        TargetDimensionId = request.TargetDimensionId,
        TargetLocalPosition = request.TargetLocalPosition,
        TargetAbsolutePosition = absolutePosition,
        LoadTicketId = loadTicketId ?? string.Empty,
        PortalId = request.PortalId,
        Reason = request.Reason,
        RequireGeneratedArea = request.RequireGeneratedArea,
        AllowFallbackPosition = request.AllowFallbackPosition,
        FallbackAttempted = false,
        WaitingForGeneration = waitingForGeneration,
        GenerationBounds = generationBounds,
        State = DimensionTravelState.WaitingForDestinationLoad,
        Message = string.IsNullOrEmpty(message)
            ? "Waiting for destination area to become ready."
            : message,
        CreatedAt = now,
        UpdatedAt = now,
        LoadRequestedAt = loadRequestedAt,
        TeleportQueuedAt = 0,
        NextTeleportRetryAt = 0,
        TeleportAttemptCount = 0,
        ScheduledTick = Unity.NetCode.NetworkTick.Invalid
      };

      pendingTravelByPlayerId[playerId] = record;
      RaiseTravelUpdated(record);
      AddDiagnostic(
          DimensionDiagnosticSeverity.Info,
          request.TargetDimensionId,
          "Dimension travel accepted for player " + playerId +
          (waitingForGeneration
              ? " and waiting for generation at local target "
              : " to local target ") +
          request.TargetLocalPosition +
          ".");
      return true;
    }

    private bool TryRefreshTravelAccessAfterReadyGeneration(
        DimensionTravelRequest request,
        DimensionTravelResult generationResult,
        out DimensionAccessResult accessResult)
    {
      accessResult = default(DimensionAccessResult);
      if (!string.Equals(generationResult.Code, TargetAreaGenerationReadyRetryCode, StringComparison.Ordinal))
      {
        return false;
      }

      return CanTravel(request, out accessResult) && accessResult.Allowed;
    }

    private bool TryBuildTravelTargetGenerationPreview(
        DimensionTravelRequest request,
        out DimensionGenerationPreviewResult preview)
    {
      preview = default(DimensionGenerationPreviewResult);
      if (!request.RequireGeneratedArea)
      {
        return false;
      }

      string requesterId;
      if (!TryGetPlayerPersistentId(request.Player, out requesterId))
      {
        requesterId = "dimension-travel";
      }

      string reason = string.IsNullOrEmpty(request.Reason)
          ? "Travel target generation preview."
          : request.Reason;
      preview =
          PreviewGenerationRequest(
              new DimensionGenerationPreviewRequest(
                  new DimensionGenerationRequest(
                      requesterId,
                      request.TargetDimensionId,
                      TravelTargetGenerationAreaAround(request),
                      0,
                      true,
                      reason),
                  true,
                  true,
                  true,
                  true,
                  true,
                  reason));
      return true;
    }

    private bool TryResolveTravelFallback(
        DimensionTravelRequest request,
        DimensionAccessResult accessResult,
        out DimensionTravelRequest fallbackRequest,
        out DimensionAccessResult fallbackAccessResult)
    {
      fallbackRequest = default(DimensionTravelRequest);
      fallbackAccessResult = default(DimensionAccessResult);

      if (!request.AllowFallbackPosition || !IsTravelFallbackEligible(accessResult.Code))
      {
        return false;
      }

      DimensionAnchorDefinition anchor;
      if (!TryResolveTravelFallbackAnchor(request.TargetDimensionId, out anchor))
      {
        return false;
      }

      if (anchor.LocalPosition.x == request.TargetLocalPosition.x &&
          anchor.LocalPosition.y == request.TargetLocalPosition.y)
      {
        return false;
      }

      fallbackRequest =
          new DimensionTravelRequest(
              request.Player,
              request.TargetDimensionId,
              anchor.LocalPosition,
              request.RequireGeneratedArea,
              false,
              request.PortalId,
              string.IsNullOrEmpty(request.Reason)
                  ? "Fallback dimension travel via anchor " + anchor.AnchorId + "."
                  : request.Reason + " Fallback anchor: " + anchor.AnchorId + ".");

      if (CanTravel(fallbackRequest, out fallbackAccessResult))
      {
        AddDiagnostic(
            DimensionDiagnosticSeverity.Info,
            request.TargetDimensionId,
            "Travel target fell back to anchor " + anchor.AnchorId + ".");
        return true;
      }

      if (string.Equals(fallbackAccessResult.Code, TargetAreaNotGeneratedCode, StringComparison.Ordinal))
      {
        AddDiagnostic(
            DimensionDiagnosticSeverity.Info,
            request.TargetDimensionId,
            "Travel fallback anchor " + anchor.AnchorId + " requires generation before travel.");
        return true;
      }

      return false;
    }

    private bool IsTravelFallbackEligible(string accessCode)
    {
      return string.Equals(accessCode, "target-position-out-of-bounds", StringComparison.Ordinal) ||
             string.Equals(accessCode, TargetAreaNotGeneratedCode, StringComparison.Ordinal);
    }

    private bool TryResolveTravelFallbackAnchor(
        string dimensionId,
        out DimensionAnchorDefinition anchor)
    {
      if (TryResolveBestAnchor(dimensionId, DimensionAnchorKind.Fallback, out anchor))
      {
        return true;
      }

      return TryResolveBestAnchor(dimensionId, DimensionAnchorKind.Entry, out anchor);
    }
  }
}
