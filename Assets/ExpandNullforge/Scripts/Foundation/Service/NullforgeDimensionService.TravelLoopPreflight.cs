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
  /// Checking that a dimension can be left again before anyone is sent into it.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
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
  }
}
