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
  public sealed partial class NullforgeDimensionService
  {
    public DimensionTravelPreviewResult PreviewTravel(DimensionTravelRequest request)
    {
      DimensionPortalDefinition portal = default(DimensionPortalDefinition);
      bool hasPortal =
          !string.IsNullOrEmpty(request.PortalId) &&
          portals.TryGetValue(request.PortalId, out portal);

      DimensionPortalPresentationDefinition presentation = default(DimensionPortalPresentationDefinition);
      bool hasPresentation =
          hasPortal &&
          TryFindPortalPresentationForPortal(request.PortalId, out presentation);

      DimensionDefinition target;
      float2 targetAbsolutePosition = default(float2);
      List<DimensionTravelRequirementEvaluationResult> requirementResults =
          new List<DimensionTravelRequirementEvaluationResult>();
      if (TryGetDimension(request.TargetDimensionId, out target))
      {
        targetAbsolutePosition = target.ToAbsolute(request.TargetLocalPosition);

        DimensionContext sourceContext;
        if (!TryGetPlayerContext(request.Player, out sourceContext))
        {
          sourceContext = DimensionContext.Unknown(default(float2));
        }

        DimensionAccessContext accessContext =
            new DimensionAccessContext(
                request.Player,
                sourceContext,
                target,
                request.TargetLocalPosition,
                targetAbsolutePosition,
                request.RequireGeneratedArea,
                request.AllowFallbackPosition,
                request.PortalId,
                request.Reason);

        IReadOnlyList<DimensionTravelRequirementEvaluationResult> evaluations =
            EvaluateTravelRequirements(accessContext, true);
        for (int i = 0; i < evaluations.Count; i++)
        {
          requirementResults.Add(evaluations[i]);
        }
      }

      DimensionAccessResult accessResult;
      bool allowed = CanTravel(request, out accessResult);
      DimensionGenerationPreviewResult generationPreview;
      bool hasGenerationPreview =
          TryBuildTravelTargetGenerationPreview(request, out generationPreview);
      return new DimensionTravelPreviewResult(
          allowed,
          accessResult,
          request.TargetDimensionId,
          request.TargetLocalPosition,
          targetAbsolutePosition,
          hasPortal,
          hasPortal ? portal : default(DimensionPortalDefinition),
          hasPresentation,
          hasPresentation ? presentation : default(DimensionPortalPresentationDefinition),
          requirementResults,
          hasGenerationPreview,
          generationPreview);
    }

    public bool CanTravel(DimensionTravelRequest request, out DimensionAccessResult accessResult)
    {
      if (request.Player == Entity.Null)
      {
        accessResult = DimensionAccessResult.Deny("player-missing", "A player entity is required.");
        return false;
      }

      DimensionDefinition target;
      if (!TryGetDimension(request.TargetDimensionId, out target))
      {
        accessResult = DimensionAccessResult.Deny("target-dimension-not-found", "The target dimension is not registered.");
        return false;
      }

      if (!target.ContainsLocal(request.TargetLocalPosition))
      {
        accessResult = DimensionAccessResult.Deny("target-position-out-of-bounds", "The target local position is outside the target dimension.");
        return false;
      }

      if (!target.HasCapability(DimensionCapabilityFlags.PlayerTravel))
      {
        accessResult = DimensionAccessResult.Deny("target-dimension-not-travelable", "The target dimension does not allow player travel.");
        return false;
      }

      DimensionContext sourceContext;
      if (!TryGetPlayerContext(request.Player, out sourceContext))
      {
        sourceContext = DimensionContext.Unknown(default(float2));
      }

      DimensionPortalDefinition requestPortal;
      if (!TryValidatePortalTravelRequest(
          request,
          sourceContext,
          out requestPortal,
          out accessResult))
      {
        return false;
      }

      DimensionAccessContext accessContext =
          new DimensionAccessContext(
              request.Player,
              sourceContext,
              target,
              request.TargetLocalPosition,
              target.ToAbsolute(request.TargetLocalPosition),
              request.RequireGeneratedArea,
              request.AllowFallbackPosition,
              request.PortalId,
              request.Reason);

      if (!TryEvaluateAccessProviders(accessContext, out accessResult))
      {
        return false;
      }

      if (request.RequireGeneratedArea && !IsAreaGenerated(request.TargetDimensionId, LocalTileAreaAround(request.TargetLocalPosition)))
      {
        accessResult = DimensionAccessResult.Deny(TargetAreaNotGeneratedCode, "The target area has not been generated yet.");
        return false;
      }

      accessResult = DimensionAccessResult.Allow();
      return true;
    }

    public DimensionTravelResult RequestTravel(DimensionTravelRequest request)
    {
      DimensionAccessResult accessResult;
      if (!CanTravel(request, out accessResult))
      {
        DimensionTravelRequest fallbackRequest;
        DimensionAccessResult fallbackAccessResult;
        if (TryResolveTravelFallback(request, accessResult, out fallbackRequest, out fallbackAccessResult))
        {
          request = fallbackRequest;
          accessResult = fallbackAccessResult;
        }
        else
        {
          DimensionTravelResult generationQueuedResult;
          if (TryQueueTravelTargetGeneration(request, accessResult, out generationQueuedResult))
          {
            if (!TryRefreshTravelAccessAfterReadyGeneration(request, generationQueuedResult, out accessResult))
            {
              return generationQueuedResult;
            }
          }
          else
          {
            return DimensionTravelResult.Failed(accessResult.Code, accessResult.Message);
          }
        }
      }

      if (!accessResult.Allowed)
      {
        DimensionTravelResult generationQueuedResult;
        if (TryQueueTravelTargetGeneration(request, accessResult, out generationQueuedResult))
        {
          if (!TryRefreshTravelAccessAfterReadyGeneration(request, generationQueuedResult, out accessResult))
          {
            return generationQueuedResult;
          }
        }
        else
        {
          return DimensionTravelResult.Failed(accessResult.Code, accessResult.Message);
        }
      }

      if (!IsServerWorldAvailable())
      {
        return DimensionTravelResult.Failed(
            "server-world-unavailable",
            "Dimension travel must be requested on the authoritative server world.");
      }

      EntityManager entityManager = serverWorld.EntityManager;
      if (!entityManager.Exists(request.Player) ||
          !entityManager.HasComponent<Unity.Transforms.LocalTransform>(request.Player) ||
          !entityManager.HasComponent<UIActionBuffer>(request.Player))
      {
        return DimensionTravelResult.Failed(
            "player-not-ready",
            "The player entity is not ready for a vanilla teleport action.");
      }

      float2 absolutePosition;
      if (!TryToAbsolute(request.TargetDimensionId, request.TargetLocalPosition, out absolutePosition))
      {
        return DimensionTravelResult.Failed("target-position-unresolved", "The target position could not be resolved.");
      }

      string playerId;
      if (!TryGetPlayerPersistentId(request.Player, out playerId))
      {
        return DimensionTravelResult.Failed(
            "player-identity-unavailable",
            "The player entity does not expose a stable player identity yet.");
      }

      Unity.Transforms.LocalTransform playerTransform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(request.Player);
      float2 previousAbsolute = new float2(playerTransform.Position.x, playerTransform.Position.z);
      DimensionContext previousContext = GetContextForAbsolute(previousAbsolute);
      string previousDimensionId = previousContext.IsKnown ? previousContext.DimensionId : DimensionIds.Overworld;

      DimensionDefinition targetDefinition;
      if (!TryGetDimension(request.TargetDimensionId, out targetDefinition))
      {
        return DimensionTravelResult.Failed(
            "target-dimension-not-registered",
            "The target dimension is not registered.");
      }

      double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
      bool preloadDestination = ShouldPreloadTravelDestination(targetDefinition);
      string loadTicketId = string.Empty;
      double loadRequestedAt = 0.0d;

      if (preloadDestination)
      {
        DimensionBounds preloadBounds = LocalTravelPreloadAreaAround(request.TargetLocalPosition);
        DimensionLoadTicket loadTicket =
            RequestLoad(
                new DimensionLoadRequest(
                    "dimension-travel:" + playerId,
                    request.TargetDimensionId,
                    preloadBounds,
                    true,
                    true,
                    TravelLoadTimeoutSeconds,
                    string.IsNullOrEmpty(request.Reason)
                        ? "Dimension travel destination preload."
                        : request.Reason));

        if (!loadTicket.IsValid)
        {
          return DimensionTravelResult.Failed("travel-preload-failed", loadTicket.Message);
        }

        loadTicketId = loadTicket.TicketId;
        loadRequestedAt = now;
      }
      else if (!string.Equals(targetDefinition.Id, DimensionIds.Overworld, StringComparison.Ordinal))
      {
        return DimensionTravelResult.Failed(
            "travel-preload-unsupported",
            "The target dimension does not support destination preloading.");
      }

      PendingTravelRecord existingTravel;
      if (pendingTravelByPlayerId.TryGetValue(playerId, out existingTravel))
      {
        ReleaseTravelLoadTicket(existingTravel);
        existingTravel.State = DimensionTravelState.Cancelled;
        existingTravel.Message = "Replaced by a newer travel request.";
        existingTravel.UpdatedAt = UnityEngine.Time.realtimeSinceStartupAsDouble;
        RaiseTravelUpdated(existingTravel);
      }

      PendingTravelRecord record = new PendingTravelRecord
      {
        TravelId = CreateTravelId(),
        Player = request.Player,
        PlayerId = playerId,
        PreviousDimensionId = previousDimensionId,
        PreviousAbsolutePosition = previousAbsolute,
        TargetDimensionId = request.TargetDimensionId,
        TargetLocalPosition = request.TargetLocalPosition,
        TargetAbsolutePosition = absolutePosition,
        LoadTicketId = loadTicketId,
        PortalId = request.PortalId,
        Reason = request.Reason,
        RequireGeneratedArea = request.RequireGeneratedArea,
        AllowFallbackPosition = request.AllowFallbackPosition,
        FallbackAttempted = false,
        State = DimensionTravelState.WaitingForDestinationLoad,
        Message = preloadDestination
            ? "Waiting for destination area to load."
            : "Overworld destination uses vanilla world streaming; preload skipped.",
        CreatedAt = now,
        UpdatedAt = now,
        LoadRequestedAt = loadRequestedAt,
        TeleportQueuedAt = 0,
        NextTeleportRetryAt = 0,
        TeleportAttemptCount = 0,
        ScheduledTick = Unity.NetCode.NetworkTick.Invalid
      };

      pendingTravelByPlayerId[playerId] = record;

      // The departure is committed, so the portal's offering is taken now. The ledger no-ops for
      // portals without offering slots, and the server system removes only the entries flagged to
      // be consumed — a museum-piece requirement ("show me the crown") keeps its crown.
      Portals.DimensionPortalOfferingLedger.QueueConsumption(request.PortalId);

      if (preloadDestination)
      {
        RaiseTravelUpdated(record);
        AddDiagnostic(
            DimensionDiagnosticSeverity.Info,
            request.TargetDimensionId,
            "Dimension travel queued for player " + playerId + " to local target " + request.TargetLocalPosition + ".");

        return DimensionTravelResult.AcceptedRequest(
            record.TravelId,
            record.LoadTicketId,
            request.TargetDimensionId,
            request.TargetLocalPosition,
            absolutePosition);
      }

      QueueVanillaTeleport(record, now);
      if (record.State == DimensionTravelState.Failed)
      {
        return DimensionTravelResult.Failed(
            "travel-teleport-queue-failed",
            record.Message);
      }

      AddDiagnostic(
          DimensionDiagnosticSeverity.Info,
          request.TargetDimensionId,
          "Dimension travel queued immediately for player " + playerId + " to local target " + request.TargetLocalPosition + ".");

      return DimensionTravelResult.AcceptedRequest(
          record.TravelId,
          record.LoadTicketId,
          request.TargetDimensionId,
          request.TargetLocalPosition,
          absolutePosition);
    }

    public bool TryCancelTravel(
        DimensionTravelCancelRequest request,
        out DimensionOperationResult result)
    {
      PendingTravelRecord record;
      if (!TryResolvePendingTravelForCancellation(request, out record, out result))
      {
        return false;
      }

      string reason = string.IsNullOrEmpty(request.Reason)
          ? "Travel cancelled."
          : request.Reason;
      CancelPendingTravel(record, reason);
      result = new DimensionOperationResult(true, string.Empty, reason);
      return true;
    }

    public bool TryCreatePortalTravelRequest(
        DimensionPortalTravelRequest request,
        out DimensionTravelRequest travelRequest,
        out DimensionOperationResult result)
    {
      travelRequest = default(DimensionTravelRequest);
      if (string.IsNullOrEmpty(request.PortalId))
      {
        result = DimensionOperationResult.Failed("portal-id-empty", "A portal id is required.");
        return false;
      }

      DimensionPortalDefinition portal;
      if (!portals.TryGetValue(request.PortalId, out portal))
      {
        result = DimensionOperationResult.Failed("portal-not-found", "The portal is not registered.");
        return false;
      }

      DimensionAccessResult accessResult;
      if (!IsPortalStateTravelable(portal.State, out accessResult))
      {
        result = DimensionOperationResult.Failed(accessResult.Code, accessResult.Message);
        return false;
      }

      string reason = string.IsNullOrEmpty(request.Reason)
          ? "Dimension portal activation."
          : request.Reason;
      float2 targetLocalPosition;
      if (!TryResolveContextualPortalTarget(request, portal, out targetLocalPosition))
      {
        targetLocalPosition = portal.ToLocalPosition;
      }

      travelRequest =
          new DimensionTravelRequest(
              request.Player,
              portal.ToDimensionId,
              targetLocalPosition,
              request.RequireGeneratedArea,
              request.AllowFallbackPosition,
              portal.PortalId,
              reason);
      result = new DimensionOperationResult(true, string.Empty, "Portal travel request created.");
      return true;
    }

    public DimensionTravelPreviewResult PreviewPortalTravel(DimensionPortalTravelRequest request)
    {
      DimensionTravelRequest travelRequest;
      DimensionOperationResult result;
      if (TryCreatePortalTravelRequest(request, out travelRequest, out result))
      {
        return PreviewTravel(travelRequest);
      }

      DimensionPortalDefinition portal = default(DimensionPortalDefinition);
      bool hasPortal =
          !string.IsNullOrEmpty(request.PortalId) &&
          portals.TryGetValue(request.PortalId, out portal);

      DimensionPortalPresentationDefinition presentation =
          default(DimensionPortalPresentationDefinition);
      bool hasPresentation =
          hasPortal &&
          TryFindPortalPresentationForPortal(request.PortalId, out presentation);

      float2 absolutePosition = default(float2);
      if (hasPortal)
      {
        TryToAbsolute(portal.ToDimensionId, portal.ToLocalPosition, out absolutePosition);
      }

      return new DimensionTravelPreviewResult(
          false,
          DimensionAccessResult.Deny(result.Code, result.Message),
          hasPortal ? portal.ToDimensionId : string.Empty,
          hasPortal ? portal.ToLocalPosition : default(float2),
          absolutePosition,
          hasPortal,
          hasPortal ? portal : default(DimensionPortalDefinition),
          hasPresentation,
          hasPresentation ? presentation : default(DimensionPortalPresentationDefinition),
          new List<DimensionTravelRequirementEvaluationResult>());
    }

    public DimensionTravelResult RequestPortalTravel(DimensionPortalTravelRequest request)
    {
      DimensionTravelRequest travelRequest;
      DimensionOperationResult result;
      if (!TryCreatePortalTravelRequest(request, out travelRequest, out result))
      {
        return DimensionTravelResult.Failed(result.Code, result.Message);
      }

      return RequestTravel(travelRequest);
    }

    private bool TryValidatePortalTravelRequest(
        DimensionTravelRequest request,
        DimensionContext sourceContext,
        out DimensionPortalDefinition portal,
        out DimensionAccessResult accessResult)
    {
      portal = default(DimensionPortalDefinition);
      if (string.IsNullOrEmpty(request.PortalId))
      {
        accessResult = DimensionAccessResult.Allow();
        return true;
      }

      if (!portals.TryGetValue(request.PortalId, out portal))
      {
        accessResult =
            DimensionAccessResult.Deny(
                "portal-not-found",
                "The requested portal route is not registered.");
        return false;
      }

      if (!IsPortalStateTravelable(portal.State, out accessResult))
      {
        return false;
      }

      if (!string.Equals(portal.ToDimensionId, request.TargetDimensionId, StringComparison.Ordinal))
      {
        accessResult =
            DimensionAccessResult.Deny(
                "portal-target-mismatch",
                "The travel request target does not match the registered portal route.");
        return false;
      }

      if (!SameLocalPosition(portal.ToLocalPosition, request.TargetLocalPosition) &&
          !IsContextualReturnPortalTarget(portal, sourceContext, request.TargetDimensionId))
      {
        accessResult =
            DimensionAccessResult.Deny(
                "portal-target-mismatch",
                "The travel request target does not match the registered portal route.");
        return false;
      }

      if (sourceContext.IsKnown &&
          !string.IsNullOrEmpty(portal.FromDimensionId) &&
          !string.Equals(portal.FromDimensionId, sourceContext.DimensionId, StringComparison.Ordinal))
      {
        accessResult =
            DimensionAccessResult.Deny(
                "portal-source-mismatch",
                "The player is not in the portal's source dimension.");
        return false;
      }

      accessResult = DimensionAccessResult.Allow();
      return true;
    }
  }
}
