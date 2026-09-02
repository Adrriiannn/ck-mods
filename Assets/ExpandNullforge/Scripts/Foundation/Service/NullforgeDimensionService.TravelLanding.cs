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
  /// Where a traveller lands, and what to do when the tile they asked for will not have them.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
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
  }
}
