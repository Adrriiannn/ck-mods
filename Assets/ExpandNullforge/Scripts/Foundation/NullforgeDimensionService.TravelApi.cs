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

    public DimensionTravelLoopPreflightResult PreflightStarterTravelLoop(string starterId)
    {
      DimensionStarterDefinition starter;
      if (!TryGetStarter(starterId, out starter))
      {
        DimensionTravelLoopPreflightRequest missingRequest =
            new DimensionTravelLoopPreflightRequest(
                DimensionIds.Overworld,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                default(DimensionBounds),
                true,
                true,
                true,
                true,
                true,
                true);
        return new DimensionTravelLoopPreflightResult(
            false,
            "starter-not-found",
            "No dimension starter with that id is registered.",
            missingRequest,
            new List<DimensionTravelLoopCheck>
            {
              new DimensionTravelLoopCheck(
                  DimensionTravelLoopCheckKind.Lifecycle,
                  starterId,
                  false,
                  "starter-not-found",
                  "No dimension starter with that id is registered.")
            },
            false,
            default(DimensionGenerationStatus));
      }

      if (!starter.Enabled)
      {
        return new DimensionTravelLoopPreflightResult(
            false,
            "starter-disabled",
            "The requested dimension starter is disabled.",
            starter.TravelLoopPreflightRequest,
            new List<DimensionTravelLoopCheck>
            {
              new DimensionTravelLoopCheck(
                  DimensionTravelLoopCheckKind.Lifecycle,
                  starter.StarterId,
                  false,
                  "starter-disabled",
                  "The requested dimension starter is disabled.")
            },
            false,
            default(DimensionGenerationStatus));
      }

      return PreflightTravelLoop(starter.TravelLoopPreflightRequest);
    }

    public DimensionTravelLoopPreflightResult PreflightTravelLoop(
        DimensionTravelLoopPreflightRequest request)
    {
      List<DimensionTravelLoopCheck> checks = new List<DimensionTravelLoopCheck>();
      DimensionGenerationStatus generationStatus = default(DimensionGenerationStatus);
      bool hasGenerationStatus = false;

      DimensionDefinition sourceDimension;
      bool hasSource =
          TryGetDimension(request.SourceDimensionId, out sourceDimension);
      AddTravelLoopCheck(
          checks,
          DimensionTravelLoopCheckKind.Dimension,
          request.SourceDimensionId,
          hasSource,
          hasSource ? string.Empty : "source-dimension-missing",
          hasSource
              ? "Source dimension is registered."
              : "The source dimension is not registered.");

      DimensionDefinition targetDimension;
      bool hasTarget =
          TryGetDimension(request.TargetDimensionId, out targetDimension);
      AddTravelLoopCheck(
          checks,
          DimensionTravelLoopCheckKind.Dimension,
          request.TargetDimensionId,
          hasTarget,
          hasTarget ? string.Empty : "target-dimension-missing",
          hasTarget
              ? "Target dimension is registered."
              : "The target dimension is not registered.");

      bool landingBoundsValid =
          hasTarget &&
          IsValidLocalArea(request.TargetLandingBounds) &&
          targetDimension.LocalBounds.Contains(request.TargetLandingBounds.Min) &&
          targetDimension.LocalBounds.Contains(request.TargetLandingBounds.MaxExclusive - new int2(1, 1));
      AddTravelLoopCheck(
          checks,
          DimensionTravelLoopCheckKind.GeneratedArea,
          request.TargetDimensionId,
          landingBoundsValid,
          landingBoundsValid ? string.Empty : "landing-bounds-invalid",
          landingBoundsValid
              ? "Target landing bounds are inside the target dimension."
              : "Target landing bounds are invalid or outside the target dimension.");

      CheckTravelLoopPortal(
          checks,
          request.EntryPortalId,
          request.SourceDimensionId,
          request.TargetDimensionId,
          true);

      if (request.RequireReturnPortal)
      {
        CheckTravelLoopPortal(
            checks,
            request.ReturnPortalId,
            request.TargetDimensionId,
            request.SourceDimensionId,
            true);
      }

      if (request.RequireSourceAnchor)
      {
        CheckTravelLoopAnchor(
            checks,
            request.SourceAnchorId,
            request.SourceDimensionId,
            default(DimensionBounds),
            false);
      }

      if (request.RequireTargetAnchor)
      {
        CheckTravelLoopAnchor(
            checks,
            request.TargetAnchorId,
            request.TargetDimensionId,
            request.TargetLandingBounds,
            true);
      }

      if (request.RequireMarkers)
      {
        CheckTravelLoopMarker(
            checks,
            request.SourceMarkerId,
            request.SourceDimensionId);
        CheckTravelLoopMarker(
            checks,
            request.TargetMarkerId,
            request.TargetDimensionId);
      }

      if (request.RequireMapLayers)
      {
        CheckTravelLoopMapLayer(
            checks,
            request.SourceDimensionId);
        CheckTravelLoopMapLayer(
            checks,
            request.TargetDimensionId);
      }

      if (request.RequireTargetAreaReady)
      {
        hasGenerationStatus =
            TryGetGenerationStatus(
                request.TargetDimensionId,
                request.TargetLandingBounds,
                out generationStatus);
        bool ready =
            hasGenerationStatus &&
            generationStatus.State == DimensionGenerationState.Ready;
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.GeneratedArea,
            request.TargetDimensionId,
            ready,
            ready ? string.Empty : "landing-area-not-ready",
            ready
                ? "Target landing area is generated and ready."
                : hasGenerationStatus
                    ? "Target landing area is " + generationStatus.State + "."
                    : "Target landing area has no generation status yet.");
      }

      if (hasTarget)
      {
        bool lifecycleReady =
            targetDimension.LifecycleState == DimensionLifecycleState.Ready;
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Lifecycle,
            request.TargetDimensionId,
            lifecycleReady,
            lifecycleReady ? string.Empty : "target-lifecycle-not-ready",
            lifecycleReady
                ? "Target dimension lifecycle is ready."
                : "Target dimension lifecycle is " + targetDimension.LifecycleState + ".");
      }

      bool overallReady = true;
      string firstCode = string.Empty;
      string firstMessage = string.Empty;
      for (int i = 0; i < checks.Count; i++)
      {
        if (checks[i].Passed)
        {
          continue;
        }

        overallReady = false;
        if (string.IsNullOrEmpty(firstCode))
        {
          firstCode = checks[i].Code;
          firstMessage = checks[i].Message;
        }
      }

      return new DimensionTravelLoopPreflightResult(
          overallReady,
          overallReady ? string.Empty : firstCode,
          overallReady ? "Travel loop preflight is ready." : firstMessage,
          request,
          checks,
          hasGenerationStatus,
          generationStatus);
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

    private void CheckTravelLoopPortal(
        List<DimensionTravelLoopCheck> checks,
        string portalId,
        string expectedSourceDimensionId,
        string expectedTargetDimensionId,
        bool requireAvailable)
    {
      DimensionPortalDefinition portal;
      if (string.IsNullOrEmpty(portalId) ||
          !TryGetPortal(portalId, out portal))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Portal,
            portalId,
            false,
            "portal-missing",
            "A required travel-loop portal is not registered.");
        return;
      }

      if (!string.Equals(portal.FromDimensionId, expectedSourceDimensionId, StringComparison.Ordinal) ||
          !string.Equals(portal.ToDimensionId, expectedTargetDimensionId, StringComparison.Ordinal))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Portal,
            portalId,
            false,
            "portal-route-mismatch",
            "The portal route does not match the expected travel-loop direction.");
        return;
      }

      if (requireAvailable)
      {
        DimensionAccessResult accessResult;
        if (!IsPortalStateTravelable(portal.State, out accessResult))
        {
          AddTravelLoopCheck(
              checks,
              DimensionTravelLoopCheckKind.Portal,
              portalId,
              false,
              accessResult.Code,
              accessResult.Message);
          return;
        }
      }

      AddTravelLoopCheck(
          checks,
          DimensionTravelLoopCheckKind.Portal,
          portalId,
          true,
          string.Empty,
          "Portal route is registered and travelable.");
    }

    private void CheckTravelLoopAnchor(
        List<DimensionTravelLoopCheck> checks,
        string anchorId,
        string expectedDimensionId,
        DimensionBounds expectedBounds,
        bool requireInsideBounds)
    {
      DimensionAnchorDefinition anchor;
      if (string.IsNullOrEmpty(anchorId) ||
          !TryGetAnchor(anchorId, out anchor))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Anchor,
            anchorId,
            false,
            "anchor-missing",
            "A required travel-loop anchor is not registered.");
        return;
      }

      if (!anchor.Enabled)
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Anchor,
            anchorId,
            false,
            "anchor-disabled",
            "The travel-loop anchor is disabled.");
        return;
      }

      if (!string.Equals(anchor.DimensionId, expectedDimensionId, StringComparison.Ordinal))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Anchor,
            anchorId,
            false,
            "anchor-dimension-mismatch",
            "The anchor belongs to a different dimension than expected.");
        return;
      }

      if (requireInsideBounds && !expectedBounds.Contains(anchor.LocalPosition))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Anchor,
            anchorId,
            false,
            "anchor-outside-landing-area",
            "The target anchor is outside the expected landing area.");
        return;
      }

      AddTravelLoopCheck(
          checks,
          DimensionTravelLoopCheckKind.Anchor,
          anchorId,
          true,
          string.Empty,
          "Anchor is registered and valid.");
    }

    private void CheckTravelLoopMarker(
        List<DimensionTravelLoopCheck> checks,
        string markerId,
        string expectedDimensionId)
    {
      DimensionMapMarker marker;
      if (string.IsNullOrEmpty(markerId) ||
          !markers.TryGetValue(markerId, out marker))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Marker,
            markerId,
            false,
            "marker-missing",
            "A required travel-loop map marker is not registered.");
        return;
      }

      if (!marker.Visible)
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Marker,
            markerId,
            false,
            "marker-hidden",
            "The travel-loop map marker is hidden.");
        return;
      }

      if (!string.Equals(marker.DimensionId, expectedDimensionId, StringComparison.Ordinal))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.Marker,
            markerId,
            false,
            "marker-dimension-mismatch",
            "The map marker belongs to a different dimension than expected.");
        return;
      }

      AddTravelLoopCheck(
          checks,
          DimensionTravelLoopCheckKind.Marker,
          markerId,
          true,
          string.Empty,
          "Map marker is registered and visible.");
    }

    private void CheckTravelLoopMapLayer(
        List<DimensionTravelLoopCheck> checks,
        string dimensionId)
    {
      DimensionMapLayerDefinition layer;
      if (!TryFindMapLayerForDimension(dimensionId, out layer))
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.MapLayer,
            dimensionId,
            false,
            "map-layer-missing",
            "No map layer is registered for the dimension.");
        return;
      }

      if (!layer.Visible || !layer.Selectable)
      {
        AddTravelLoopCheck(
            checks,
            DimensionTravelLoopCheckKind.MapLayer,
            layer.LayerId,
            false,
            "map-layer-disabled",
            "The dimension map layer is not visible and selectable.");
        return;
      }

      AddTravelLoopCheck(
          checks,
          DimensionTravelLoopCheckKind.MapLayer,
          layer.LayerId,
          true,
          string.Empty,
          "Map layer is registered, visible, and selectable.");
    }

    private static void AddTravelLoopCheck(
        List<DimensionTravelLoopCheck> checks,
        DimensionTravelLoopCheckKind kind,
        string subjectId,
        bool passed,
        string code,
        string message)
    {
      checks.Add(
          new DimensionTravelLoopCheck(
              kind,
              subjectId,
              passed,
              code,
              message));
    }

    private static bool IsValidLocalArea(DimensionBounds bounds)
    {
      return bounds.MaxExclusive.x > bounds.Min.x &&
             bounds.MaxExclusive.y > bounds.Min.y;
    }

    private bool IsPortalStateTravelable(
        DimensionPortalState state,
        out DimensionAccessResult accessResult)
    {
      if (state == DimensionPortalState.Available)
      {
        accessResult = DimensionAccessResult.Allow();
        return true;
      }

      string stateName = state.ToString();
      accessResult =
          DimensionAccessResult.Deny(
              "portal-not-available",
              "The portal is not available for travel. Current state: " + stateName + ".");
      return false;
    }

    private static bool SameLocalPosition(float2 left, float2 right)
    {
      return math.lengthsq(left - right) <= 0.0001f;
    }

    private bool TryResolveContextualPortalTarget(
        DimensionPortalTravelRequest request,
        DimensionPortalDefinition portal,
        out float2 targetLocalPosition)
    {
      targetLocalPosition = portal.ToLocalPosition;
      if (!IsReturnToOverworldPortal(portal) ||
          request.Player == Entity.Null)
      {
        return false;
      }

      string playerId;
      if (!TryGetPlayerPersistentId(request.Player, out playerId))
      {
        return false;
      }

      DimensionPlayerVisitRecord visit;
      if (!DimensionWorldRegistry.TryGetPlayerVisit(
          playerId,
          DimensionIds.Overworld,
          out visit) ||
          string.IsNullOrEmpty(visit.PlayerId))
      {
        return false;
      }

      targetLocalPosition = visit.LocalPosition;
      return true;
    }

    private static bool IsContextualReturnPortalTarget(
        DimensionPortalDefinition portal,
        DimensionContext sourceContext,
        string targetDimensionId)
    {
      return sourceContext.IsKnown &&
          IsReturnToOverworldPortal(portal) &&
          string.Equals(portal.FromDimensionId, sourceContext.DimensionId, StringComparison.Ordinal) &&
          string.Equals(targetDimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
    }

    private static bool IsReturnToOverworldPortal(DimensionPortalDefinition portal)
    {
      return !string.IsNullOrEmpty(portal.FromDimensionId) &&
          !string.Equals(portal.FromDimensionId, DimensionIds.Overworld, StringComparison.Ordinal) &&
          string.Equals(portal.ToDimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
    }

    private DimensionLandingValidationResult ValidateLandingTargetInternal(
        DimensionLandingValidationRequest request,
        bool usedFallback,
        string fallbackAnchorId)
    {
      DimensionDefinition target;
      DimensionBounds requiredBounds =
          CenteredLocalTileAreaAround(
              request.TargetLocalPosition,
              request.AreaSideTiles <= 0 ? 1 : request.AreaSideTiles);

      if (!TryGetDimension(request.TargetDimensionId, out target))
      {
        return CreateLandingValidationFailure(
            request,
            requiredBounds,
            "target-dimension-not-found",
            "The target dimension is not registered.");
      }

      if (!target.ContainsLocal(request.TargetLocalPosition))
      {
        DimensionLandingValidationResult fallbackResult;
        if (TryValidateLandingFallback(
            request,
            "target-position-out-of-bounds",
            out fallbackResult))
        {
          return fallbackResult;
        }

        return CreateLandingValidationFailure(
            request,
            requiredBounds,
            "target-position-out-of-bounds",
            "The target local position is outside the target dimension.");
      }

      if (!target.ContainsLocal((float2)requiredBounds.Min) ||
          !target.ContainsLocal((float2)(requiredBounds.MaxExclusive - new int2(1, 1))))
      {
        return CreateLandingValidationFailure(
            request,
            requiredBounds,
            "target-area-out-of-bounds",
            "The requested landing area extends outside the target dimension.");
      }

      if (request.RequirePlayerTravelCapability &&
          !target.HasCapability(DimensionCapabilityFlags.PlayerTravel))
      {
        return CreateLandingValidationFailure(
            request,
            requiredBounds,
            "target-dimension-not-travelable",
            "The target dimension does not allow player travel.");
      }

      if (request.EvaluateAccessProviders)
      {
        DimensionAccessResult accessResult;
        if (!TryValidateLandingAccess(request, target, out accessResult))
        {
          return CreateLandingValidationFailure(
              request,
              requiredBounds,
              accessResult.Code,
              accessResult.Message);
        }
      }

      bool generationReady =
          !request.RequireGeneratedArea ||
          IsAreaGenerated(request.TargetDimensionId, requiredBounds);
      if (!generationReady)
      {
        DimensionLandingValidationResult fallbackResult;
        if (TryValidateLandingFallback(
            request,
            TargetAreaNotGeneratedCode,
            out fallbackResult))
        {
          return fallbackResult;
        }

        return CreateLandingValidationFailure(
            request,
            requiredBounds,
            TargetAreaNotGeneratedCode,
            "The target area has not been generated yet.");
      }

      float2 absolutePosition = target.ToAbsolute(request.TargetLocalPosition);
      return new DimensionLandingValidationResult(
          true,
          string.Empty,
          string.Empty,
          request.TargetDimensionId,
          request.TargetLocalPosition,
          request.TargetLocalPosition,
          absolutePosition,
          requiredBounds,
          true,
          usedFallback,
          fallbackAnchorId);
    }

    private bool TryValidateLandingAccess(
        DimensionLandingValidationRequest request,
        DimensionDefinition target,
        out DimensionAccessResult accessResult)
    {
      if (request.Player == Entity.Null)
      {
        accessResult = DimensionAccessResult.Deny("player-missing", "A player entity is required for access-provider validation.");
        return false;
      }

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
              target.ToAbsolute(request.TargetLocalPosition),
              request.RequireGeneratedArea,
              request.AllowFallbackPosition,
              request.PortalId,
              request.Reason);

      return TryEvaluateAccessProviders(accessContext, out accessResult);
    }

    private bool TryValidateLandingFallback(
        DimensionLandingValidationRequest request,
        string failureCode,
        out DimensionLandingValidationResult result)
    {
      result = default(DimensionLandingValidationResult);
      if (!request.AllowFallbackPosition || !IsTravelFallbackEligible(failureCode))
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

      DimensionLandingValidationRequest fallbackRequest =
          new DimensionLandingValidationRequest(
              request.Player,
              request.TargetDimensionId,
              anchor.LocalPosition,
              request.RequireGeneratedArea,
              request.RequirePlayerTravelCapability,
              request.EvaluateAccessProviders,
              false,
              request.AreaSideTiles,
              request.PortalId,
              string.IsNullOrEmpty(request.Reason)
                  ? "Fallback landing validation via anchor " + anchor.AnchorId + "."
                  : request.Reason + " Fallback anchor: " + anchor.AnchorId + ".");

      DimensionLandingValidationResult fallbackResult =
          ValidateLandingTargetInternal(fallbackRequest, true, anchor.AnchorId);
      if (!fallbackResult.Valid)
      {
        return false;
      }

      result = fallbackResult;
      return true;
    }

    private DimensionLandingValidationResult CreateLandingValidationFailure(
        DimensionLandingValidationRequest request,
        DimensionBounds requiredBounds,
        string code,
        string message)
    {
      return new DimensionLandingValidationResult(
          false,
          string.IsNullOrEmpty(code) ? "landing-validation-failed" : code,
          message ?? string.Empty,
          request.TargetDimensionId,
          request.TargetLocalPosition,
          request.TargetLocalPosition,
          default(float2),
          requiredBounds,
          false,
          false,
          string.Empty);
    }

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
