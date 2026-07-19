using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool IsValidGenerationBounds(DimensionBounds bounds, out string error)
    {
      int2 size = bounds.Size;
      if (size.x <= 0 || size.y <= 0)
      {
        error = "The generation area must have a positive size.";
        return false;
      }

      error = string.Empty;
      return true;
    }

    private bool ValidateGenerationReservationRequest(
        DimensionGenerationReservationRequest request,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(request.ReservationId))
      {
        result = DimensionOperationResult.Failed("generation-reservation-id-empty", "A generation reservation id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(request.OwnerId))
      {
        result = DimensionOperationResult.Failed("generation-reservation-owner-empty", "A generation reservation owner id is required.");
        return false;
      }

      DimensionDefinition definition;
      if (!TryGetDimension(request.DimensionId, out definition))
      {
        result = DimensionOperationResult.Failed("dimension-not-found", "No dimension with that id is registered.");
        return false;
      }

      if (!definition.HasCapability(DimensionCapabilityFlags.Generation))
      {
        result = DimensionOperationResult.Failed("dimension-generation-disabled", "The target dimension does not allow custom generation.");
        return false;
      }

      string boundsError;
      if (!IsValidGenerationBounds(request.LocalBounds, out boundsError))
      {
        result = DimensionOperationResult.Failed("generation-reservation-bounds-invalid", boundsError);
        return false;
      }

      DimensionArea area;
      if (!TryGetArea(request.DimensionId, request.LocalBounds, out area))
      {
        result = DimensionOperationResult.Failed("generation-reservation-out-of-bounds", "The reservation area is outside the target dimension bounds.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private DimensionGenerationPreflightResult BuildGenerationPreflightResult(
        DimensionGenerationPreflightRequest request,
        bool canProceed,
        string code,
        string message,
        bool dimensionExists,
        bool hasGenerationCapability,
        bool boundsValid,
        bool areaInsideDimension,
        bool alreadyReady,
        bool activeGeneration,
        bool reservationFree,
        DimensionGenerationStatus status,
        DimensionGenerationReservation blockingReservation)
    {
      return new DimensionGenerationPreflightResult(
          canProceed,
          code,
          message,
          request.DimensionId,
          request.LocalBounds,
          dimensionExists,
          hasGenerationCapability,
          boundsValid,
          areaInsideDimension,
          alreadyReady,
          activeGeneration,
          reservationFree,
          status,
          blockingReservation);
    }

    private static DimensionGenerationPreviewResult BuildGenerationPreviewResult(
        bool canRequest,
        string code,
        string message,
        DimensionGenerationPreflightResult areaPreflight,
        DimensionGenerationPlanPreflightResult planPreflight,
        bool wouldQueueGeneration,
        bool wouldReuseExistingStatus,
        bool wouldReturnNotGenerated,
        DimensionGenerationStatus existingStatus)
    {
      return new DimensionGenerationPreviewResult(
          canRequest,
          code,
          message,
          areaPreflight,
          planPreflight,
          wouldQueueGeneration,
          wouldReuseExistingStatus,
          wouldReturnNotGenerated,
          existingStatus);
    }

    private static DimensionGenerationPlanPreflightResult SkippedGenerationPlanPreflight(
        string dimensionId,
        DimensionBounds localBounds)
    {
      return new DimensionGenerationPlanPreflightResult(
          true,
          "generation-plan-not-required",
          "Generation plan preflight was not required.",
          new DimensionGenerationPlan(
              true,
              "Generation plan preflight was not required.",
              dimensionId,
              localBounds,
              string.Empty,
              new List<DimensionGenerationPassDefinition>()),
          0,
          0,
          0,
          0,
          0,
          0,
          false);
    }

    private DimensionGenerationState NormalizeGenerationState(DimensionGenerationState state)
    {
      if (state < DimensionGenerationState.NotGenerated || state > DimensionGenerationState.Failed)
      {
        return DimensionGenerationState.Unknown;
      }

      return state;
    }

    private DimensionGenerationStatus NormalizePersistedGenerationStatus(DimensionGenerationStatus status)
    {
      DimensionGenerationState state = NormalizeGenerationState(status.State);
      if (IsTransientGenerationState(state))
      {
        return new DimensionGenerationStatus(
            status.DimensionId,
            status.LocalBounds,
            DimensionGenerationState.Failed,
            status.Progress01,
            "Generation was interrupted before it finished. Request generation again to retry.");
      }

      return new DimensionGenerationStatus(
          status.DimensionId,
          status.LocalBounds,
          state,
          status.Progress01,
          status.Message);
    }

    private bool IsTransientGenerationState(DimensionGenerationState state)
    {
      return state == DimensionGenerationState.Queued ||
             state == DimensionGenerationState.LoadingArea ||
             state == DimensionGenerationState.GeneratingTerrain ||
             state == DimensionGenerationState.StampingScenes ||
             state == DimensionGenerationState.Populating;
    }

    private bool TryGetExactGenerationStatus(
        string dimensionId,
        DimensionBounds localBounds,
        out DimensionGenerationStatus status)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        status = default(DimensionGenerationStatus);
        return false;
      }

      return generationStatuses.TryGetValue(
          GenerationStatusKey(dimensionId, localBounds),
          out status);
    }

    private bool TryFindContainingGenerationStatus(
        string dimensionId,
        DimensionBounds localBounds,
        bool readyOnly,
        out DimensionGenerationStatus status)
    {
      foreach (DimensionGenerationStatus candidate in generationStatuses.Values)
      {
        if (!string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        bool acceptableState = readyOnly
            ? candidate.State == DimensionGenerationState.Ready
            : IsTransientGenerationState(candidate.State);
        if (!acceptableState)
        {
          continue;
        }

        if (BoundsContain(candidate.LocalBounds, localBounds))
        {
          status = candidate;
          return true;
        }
      }

      status = default(DimensionGenerationStatus);
      return false;
    }

    private bool GenerationReservationMatchesQuery(
        DimensionGenerationReservation reservation,
        DimensionGenerationReservationQuery query)
    {
      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(reservation.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.OwnerId) &&
          !string.Equals(reservation.OwnerId, query.OwnerId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.RequireOverlap && !BoundsOverlap(reservation.LocalBounds, query.LocalBounds))
      {
        return false;
      }

      return true;
    }

    private bool TryFindGenerationReservationOverlap(
        string dimensionId,
        DimensionBounds localBounds,
        string ownerId,
        bool allowOverlapWithSameOwner,
        out DimensionGenerationReservation reservation)
    {
      foreach (DimensionGenerationReservation candidate in generationReservations.Values)
      {
        if (!string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (allowOverlapWithSameOwner &&
            string.Equals(candidate.OwnerId, ownerId, StringComparison.Ordinal))
        {
          continue;
        }

        if (BoundsOverlap(candidate.LocalBounds, localBounds))
        {
          reservation = candidate;
          return true;
        }
      }

      reservation = default(DimensionGenerationReservation);
      return false;
    }

    private static int CompareGenerationReservations(
        DimensionGenerationReservation left,
        DimensionGenerationReservation right)
    {
      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int priority = right.Priority.CompareTo(left.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int minX = left.LocalBounds.Min.x.CompareTo(right.LocalBounds.Min.x);
      if (minX != 0)
      {
        return minX;
      }

      int minY = left.LocalBounds.Min.y.CompareTo(right.LocalBounds.Min.y);
      if (minY != 0)
      {
        return minY;
      }

      return string.Compare(left.ReservationId, right.ReservationId, StringComparison.Ordinal);
    }

    private void RemoveGenerationReservationsForDimension(string dimensionId)
    {
      List<string> keysToRemove = new List<string>();
      foreach (KeyValuePair<string, DimensionGenerationReservation> pair in generationReservations)
      {
        if (string.Equals(pair.Value.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          keysToRemove.Add(pair.Key);
        }
      }

      for (int i = 0; i < keysToRemove.Count; i++)
      {
        DimensionGenerationReservation reservation = generationReservations[keysToRemove[i]];
        generationReservations.Remove(keysToRemove[i]);
        RaiseGenerationReservationChanged(
            reservation,
            DimensionGenerationReservationChangeKind.OwnerRemoved,
            "dimension removed");
      }
    }

    private bool ValidateGenerationPass(
        DimensionGenerationPassDefinition generationPass,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(generationPass.PassId))
      {
        result = DimensionOperationResult.Failed("generation-pass-id-empty", "A generation pass id is required.");
        return false;
      }

      if (!IsValidGenerationPassPhase(generationPass.Phase))
      {
        result = DimensionOperationResult.Failed("generation-pass-phase-invalid", "The generation pass phase is not supported.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(generationPass.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("generation-pass-dimension-not-found", "The generation pass dimension is not registered.");
        return false;
      }

      if (!string.IsNullOrEmpty(generationPass.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (zoneDefinitions.TryGetValue(generationPass.ZoneId, out zone) &&
            !string.Equals(zone.DimensionId, generationPass.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("generation-pass-zone-dimension-mismatch", "The generation pass zone belongs to another dimension.");
          return false;
        }
      }

      if (generationPass.HasLocalBounds)
      {
        if (generationPass.LocalBounds.Size.x <= 0 || generationPass.LocalBounds.Size.y <= 0)
        {
          result = DimensionOperationResult.Failed("generation-pass-bounds-invalid", "The generation pass local bounds must have a positive size.");
          return false;
        }

        if (!dimension.LocalBounds.Contains(generationPass.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(generationPass.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          result = DimensionOperationResult.Failed("generation-pass-bounds-out-of-dimension", "The generation pass bounds are outside the generation pass dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool IsValidGenerationPassPhase(DimensionGenerationPassPhase phase)
    {
      return phase == DimensionGenerationPassPhase.Terrain ||
             phase == DimensionGenerationPassPhase.Liquid ||
             phase == DimensionGenerationPassPhase.Structures ||
             phase == DimensionGenerationPassPhase.Scenes ||
             phase == DimensionGenerationPassPhase.Ore ||
             phase == DimensionGenerationPassPhase.Objects ||
             phase == DimensionGenerationPassPhase.Mobs ||
             phase == DimensionGenerationPassPhase.Bosses ||
             phase == DimensionGenerationPassPhase.Events ||
             phase == DimensionGenerationPassPhase.Polish ||
             phase == DimensionGenerationPassPhase.Custom;
    }

    private bool GenerationPassAppliesToPlan(
        DimensionGenerationPassDefinition generationPass,
        DimensionGenerationPlanRequest request,
        string effectiveZoneId)
    {
      if (!string.Equals(generationPass.DimensionId, request.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!request.IncludeDisabled && !generationPass.Enabled)
      {
        return false;
      }

      if (request.RequireRegisteredProvider &&
          (string.IsNullOrEmpty(generationPass.ProviderId) ||
           !generationProviders.ContainsKey(generationPass.ProviderId)))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(generationPass.ZoneId) &&
          !string.IsNullOrEmpty(effectiveZoneId))
      {
        if (!string.Equals(generationPass.ZoneId, effectiveZoneId, StringComparison.Ordinal))
        {
          return false;
        }
      }

      if (generationPass.HasLocalBounds &&
          !BoundsOverlap(generationPass.LocalBounds, request.LocalBounds))
      {
        return false;
      }

      return true;
    }

    private static DimensionGenerationState GenerationStateForPassPhase(
        DimensionGenerationPassPhase phase)
    {
      if (phase == DimensionGenerationPassPhase.Terrain ||
          phase == DimensionGenerationPassPhase.Liquid)
      {
        return DimensionGenerationState.GeneratingTerrain;
      }

      if (phase == DimensionGenerationPassPhase.Structures ||
          phase == DimensionGenerationPassPhase.Scenes)
      {
        return DimensionGenerationState.StampingScenes;
      }

      return DimensionGenerationState.Populating;
    }

    private static float ComputePlannedGenerationProgress(
        RuntimeGenerationRecord record,
        float passProgress01)
    {
      if (record == null || record.PlannedPasses == null || record.PlannedPasses.Count <= 0)
      {
        return math.clamp(passProgress01, 0f, 1f);
      }

      float localProgress = math.clamp(passProgress01, 0f, 1f);
      float plannedProgress =
          (record.PlannedPassIndex + localProgress) /
          (float)record.PlannedPasses.Count;
      return math.clamp(0.25f + plannedProgress * 0.70f, 0f, 1f);
    }

    private static string GenerationPassName(DimensionGenerationPassDefinition generationPass)
    {
      return string.IsNullOrEmpty(generationPass.DisplayName)
          ? generationPass.PassId
          : generationPass.DisplayName;
    }

    private bool HasFallbackGenerationProvider(
        string dimensionId,
        DimensionBounds localBounds)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        return false;
      }

      foreach (IDimensionGenerationProvider provider in generationProviders.Values)
      {
        if (provider != null && provider.CanGenerate(definition, localBounds))
        {
          return true;
        }
      }

      return false;
    }

    private static DimensionGenerationPlanPreflightResult BuildGenerationPlanPreflightResult(
        bool canExecute,
        string code,
        string message,
        DimensionGenerationPlan plan,
        int matchingPassCount,
        int enabledPassCount,
        int disabledPassCount,
        int missingProviderCount,
        int providerRejectedPassCount,
        int executablePassCount,
        bool hasFallbackProvider)
    {
      return new DimensionGenerationPlanPreflightResult(
          canExecute,
          code,
          message,
          plan,
          matchingPassCount,
          enabledPassCount,
          disabledPassCount,
          missingProviderCount,
          providerRejectedPassCount,
          executablePassCount,
          hasFallbackProvider);
    }

    private static int CompareGenerationPasses(
        DimensionGenerationPassDefinition left,
        DimensionGenerationPassDefinition right)
    {
      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int phase = left.Phase.CompareTo(right.Phase);
      if (phase != 0)
      {
        return phase;
      }

      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int zone = string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal);
      if (zone != 0)
      {
        return zone;
      }

      return string.Compare(left.PassId, right.PassId, StringComparison.Ordinal);
    }

    private bool GenerationPassEquals(
        DimensionGenerationPassDefinition a,
        DimensionGenerationPassDefinition b)
    {
      return string.Equals(a.PassId, b.PassId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             a.HasLocalBounds == b.HasLocalBounds &&
             (!a.HasLocalBounds || BoundsEqual(a.LocalBounds, b.LocalBounds)) &&
             a.Phase == b.Phase &&
             a.Priority == b.Priority &&
             string.Equals(a.ProviderId, b.ProviderId, StringComparison.Ordinal) &&
             a.Enabled == b.Enabled;
    }

    private bool GenerationStatusEquals(
        DimensionGenerationStatus a,
        DimensionGenerationStatus b)
    {
      return string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             BoundsEqual(a.LocalBounds, b.LocalBounds) &&
             a.State == b.State &&
             math.abs(a.Progress01 - b.Progress01) <= GenerationProgressChangeEpsilon &&
             string.Equals(a.Message, b.Message, StringComparison.Ordinal);
    }

    private string GenerationStatusKey(string dimensionId, DimensionBounds localBounds)
    {
      return (dimensionId ?? string.Empty) +
             "|" +
             localBounds.Min.x +
             "," +
             localBounds.Min.y +
             "," +
             localBounds.MaxExclusive.x +
             "," +
             localBounds.MaxExclusive.y;
    }

    private void RaiseGenerationStatusChanged(DimensionGenerationStatus status)
    {
      Action<DimensionGenerationStatus> handler = GenerationStatusChanged;
      if (handler != null)
      {
        handler(status);
      }
    }

    private void RaiseGenerationReservationChanged(
        DimensionGenerationReservation reservation,
        DimensionGenerationReservationChangeKind changeKind,
        string reason)
    {
      Action<DimensionGenerationReservationChangedEvent> handler = GenerationReservationChanged;
      if (handler != null)
      {
        handler(
            new DimensionGenerationReservationChangedEvent(
                reservation,
                changeKind,
                reason ?? string.Empty));
      }
    }

    private void RaiseGenerationPassChanged(
        DimensionGenerationPassDefinition generationPass,
        DimensionGenerationPassChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionGenerationPassChangedEvent> handler = GenerationPassChanged;
      if (handler != null)
      {
        handler(
            new DimensionGenerationPassChangedEvent(
                generationPass,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }
  }
}
