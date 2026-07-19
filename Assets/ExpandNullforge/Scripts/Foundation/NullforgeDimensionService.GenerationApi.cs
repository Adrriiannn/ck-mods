using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public DimensionGenerationStatus RequestGeneration(DimensionGenerationRequest request)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(request.DimensionId, out definition))
      {
        return new DimensionGenerationStatus(
            request.DimensionId,
            request.LocalBounds,
            DimensionGenerationState.Failed,
            0f,
            "The target dimension is not registered.");
      }

      if (!definition.HasCapability(DimensionCapabilityFlags.Generation))
      {
        return new DimensionGenerationStatus(
            request.DimensionId,
            request.LocalBounds,
            DimensionGenerationState.Failed,
            0f,
            "The target dimension does not allow custom generation.");
      }

      string boundsError;
      if (!IsValidGenerationBounds(request.LocalBounds, out boundsError))
      {
        return new DimensionGenerationStatus(
            request.DimensionId,
            request.LocalBounds,
            DimensionGenerationState.Failed,
            0f,
            boundsError);
      }

      DimensionArea area;
      if (!TryGetArea(request.DimensionId, request.LocalBounds, out area))
      {
        return new DimensionGenerationStatus(
            request.DimensionId,
            request.LocalBounds,
            DimensionGenerationState.Failed,
            0f,
            "The requested generation area is outside the target dimension bounds.");
      }

      DimensionGenerationStatus existing;
      bool hasExisting = TryGetGenerationStatus(request.DimensionId, request.LocalBounds, out existing);
      if (hasExisting && existing.State == DimensionGenerationState.Ready)
      {
        return existing;
      }

      if (hasExisting && IsTransientGenerationState(existing.State))
      {
        if (!runtimeGenerationRecords.ContainsKey(GenerationStatusKey(request.DimensionId, request.LocalBounds)))
        {
          QueueRuntimeGeneration(request, existing);
          AddDiagnostic(
              DimensionDiagnosticSeverity.Info,
              request.DimensionId,
              "Recovered runtime generation worker for an existing transient generation status.");
        }

        return existing;
      }

      if (!request.CreateIfMissing)
      {
        return new DimensionGenerationStatus(
            request.DimensionId,
            request.LocalBounds,
            DimensionGenerationState.NotGenerated,
            0f,
            "The requested area has not been generated.");
      }

      DimensionGenerationReservation overlappingReservation;
      if (TryFindGenerationReservationOverlap(
          request.DimensionId,
          request.LocalBounds,
          RuntimeGenerationOwnerId(request.RequesterId),
          false,
          out overlappingReservation))
      {
        DimensionGenerationStatus activeRuntimeStatus;
        if (TryGetRuntimeGenerationStatusForReservation(
            overlappingReservation,
            request.LocalBounds,
            out activeRuntimeStatus) &&
            (activeRuntimeStatus.State == DimensionGenerationState.Ready ||
             IsTransientGenerationState(activeRuntimeStatus.State)))
        {
          return activeRuntimeStatus;
        }

        if (!TryReleaseStaleRuntimeGenerationReservation(
            overlappingReservation,
            "stale runtime reservation cleared before generation request") ||
            TryFindGenerationReservationOverlap(
                request.DimensionId,
                request.LocalBounds,
                RuntimeGenerationOwnerId(request.RequesterId),
                false,
                out overlappingReservation))
        {
          return new DimensionGenerationStatus(
              request.DimensionId,
              request.LocalBounds,
              DimensionGenerationState.Failed,
              0f,
              "The requested generation area overlaps active reservation " + overlappingReservation.ReservationId + ".");
        }
      }

      DimensionGenerationStatus queued =
          SetGenerationStatus(
              request.DimensionId,
              request.LocalBounds,
              DimensionGenerationState.Queued,
              0f,
              string.IsNullOrEmpty(request.Reason) ? "Generation queued." : request.Reason);
      QueueRuntimeGeneration(request, queued);
      AddDiagnostic(DimensionDiagnosticSeverity.Info, request.DimensionId, "Generation queued for local bounds.");
      return queued;
    }

    public DimensionGenerationStatus SetGenerationStatus(
        string dimensionId,
        DimensionBounds localBounds,
        DimensionGenerationState state,
        float progress01,
        string message)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        return new DimensionGenerationStatus(
            dimensionId,
            localBounds,
            DimensionGenerationState.Failed,
            0f,
            "The target dimension is not registered.");
      }

      string boundsError;
      if (!IsValidGenerationBounds(localBounds, out boundsError))
      {
        return new DimensionGenerationStatus(
            dimensionId,
            localBounds,
            DimensionGenerationState.Failed,
            0f,
            boundsError);
      }

      DimensionArea area;
      if (!TryGetArea(dimensionId, localBounds, out area))
      {
        return new DimensionGenerationStatus(
            dimensionId,
            localBounds,
            DimensionGenerationState.Failed,
            0f,
            "The generation area is outside the target dimension bounds.");
      }

      DimensionGenerationStatus status =
          new DimensionGenerationStatus(
              dimensionId,
              localBounds,
              NormalizeGenerationState(state),
              math.clamp(progress01, 0f, 1f),
              message ?? string.Empty);
      string key = GenerationStatusKey(dimensionId, localBounds);
      DimensionGenerationStatus previous;
      bool hadPrevious = generationStatuses.TryGetValue(key, out previous);
      bool changed = !hadPrevious || !GenerationStatusEquals(previous, status);
      generationStatuses[key] = status;
      if (changed)
      {
        PersistGenerationStatusIfWorldRegistryLoaded(status);
        RaiseGenerationStatusChanged(status);
      }

      if (!hadPrevious || previous.State != status.State)
      {
        AddDiagnostic(DimensionDiagnosticSeverity.Info, dimensionId, "Generation status changed to " + status.State + ".");
        DimensionFrameworkLog.Verbose(
            "[ExpandNullforge] Generation status changed. dimension=" +
            dimensionId +
            " bounds=(" +
            localBounds.Min.x +
            "," +
            localBounds.Min.y +
            ")-(" +
            localBounds.MaxExclusive.x +
            "," +
            localBounds.MaxExclusive.y +
            ")" +
            " state=" +
            status.State +
            " progress=" +
            status.Progress01.ToString("0.00") +
            " message=" +
            status.Message);
        RefreshStarterLifecycleForGeneratedArea(
            dimensionId,
            localBounds,
            "generation status changed to " + status.State);
      }

      return status;
    }

    public bool TryGetGenerationStatus(
        string dimensionId,
        DimensionBounds localBounds,
        out DimensionGenerationStatus status)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        status = default(DimensionGenerationStatus);
        return false;
      }

      if (definition.Id == DimensionIds.Overworld)
      {
        status = new DimensionGenerationStatus(
            dimensionId,
            localBounds,
            DimensionGenerationState.Ready,
            1f,
            "Vanilla overworld area is owned by the base game.");
        return true;
      }

      if (TryFindContainingGenerationStatus(dimensionId, localBounds, true, out status))
      {
        return true;
      }

      string key = GenerationStatusKey(dimensionId, localBounds);
      if (generationStatuses.TryGetValue(key, out status))
      {
        return true;
      }

      if (TryFindContainingGenerationStatus(dimensionId, localBounds, false, out status))
      {
        return true;
      }

      status = new DimensionGenerationStatus(
          dimensionId,
          localBounds,
          DimensionGenerationState.NotGenerated,
          0f,
          "The requested area has not been generated.");
      return true;
    }

    public IReadOnlyList<DimensionGenerationStatus> GetGenerationStatuses(string dimensionId)
    {
      List<DimensionGenerationStatus> result = new List<DimensionGenerationStatus>();
      foreach (DimensionGenerationStatus status in generationStatuses.Values)
      {
        if (string.IsNullOrEmpty(dimensionId) || string.Equals(status.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          result.Add(status);
        }
      }

      return result;
    }

    public bool IsAreaGenerated(string dimensionId, DimensionBounds localBounds)
    {
      DimensionGenerationStatus status;
      return TryGetGenerationStatus(dimensionId, localBounds, out status) &&
             status.State == DimensionGenerationState.Ready;
    }

    public DimensionGenerationPreflightResult PreflightGeneration(
        DimensionGenerationPreflightRequest request)
    {
      DimensionDefinition definition;
      bool dimensionExists = TryGetDimension(request.DimensionId, out definition);
      bool hasGenerationCapability =
          dimensionExists &&
          definition.HasCapability(DimensionCapabilityFlags.Generation);

      string boundsError;
      bool boundsValid = IsValidGenerationBounds(request.LocalBounds, out boundsError);

      bool areaInsideDimension = false;
      if (dimensionExists && boundsValid)
      {
        DimensionArea area;
        areaInsideDimension = TryGetArea(request.DimensionId, request.LocalBounds, out area);
      }

      DimensionGenerationStatus status =
          new DimensionGenerationStatus(
              request.DimensionId,
              request.LocalBounds,
              DimensionGenerationState.Unknown,
              0f,
              "Generation preflight has not resolved a status.");
      if (dimensionExists && boundsValid)
      {
        TryGetGenerationStatus(request.DimensionId, request.LocalBounds, out status);
      }

      bool alreadyReady = status.State == DimensionGenerationState.Ready;
      bool activeGeneration = IsTransientGenerationState(status.State);

      DimensionGenerationReservation blockingReservation =
          default(DimensionGenerationReservation);
      bool reservationFree = true;
      if (request.RequireReservationFree && dimensionExists && boundsValid)
      {
        bool allowSameOwner =
            request.AllowReservationOverlapWithSameOwner &&
            !string.IsNullOrEmpty(request.OwnerId);
        reservationFree =
            !TryFindGenerationReservationOverlap(
                request.DimensionId,
                request.LocalBounds,
                request.OwnerId,
                allowSameOwner,
                out blockingReservation);
      }

      if (!dimensionExists)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "dimension-not-found",
            "No dimension with that id is registered.",
            false,
            false,
            boundsValid,
            false,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (request.RequireGenerationCapability && !hasGenerationCapability)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "dimension-generation-disabled",
            "The target dimension does not allow custom generation.",
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

      if (!boundsValid)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-bounds-invalid",
            boundsError,
            dimensionExists,
            hasGenerationCapability,
            false,
            false,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (request.RequireAreaInsideDimension && !areaInsideDimension)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-area-out-of-bounds",
            "The requested generation area is outside the target dimension bounds.",
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

      if (alreadyReady && !request.AllowReadyArea)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-area-ready",
            "The requested generation area is already ready.",
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

      if (activeGeneration && !request.AllowActiveGeneration)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-area-active",
            "The requested generation area already has an active generation job.",
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

      if (!reservationFree)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-reservation-overlap",
            "The requested generation area overlaps " + blockingReservation.ReservationId + ".",
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

      return BuildGenerationPreflightResult(
          request,
          true,
          string.Empty,
          "Generation preflight passed.",
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

    public DimensionGenerationPreviewResult PreviewGenerationRequest(
        DimensionGenerationPreviewRequest request)
    {
      DimensionGenerationRequest generationRequest = request.GenerationRequest;
      DimensionGenerationPreflightResult areaPreflight =
          PreflightGeneration(
              new DimensionGenerationPreflightRequest(
                  generationRequest.RequesterId,
                  generationRequest.DimensionId,
                  generationRequest.LocalBounds,
                  RuntimeGenerationOwnerId(generationRequest.RequesterId),
                  true,
                  true,
                  request.AllowReadyArea,
                  request.AllowActiveGeneration,
                  false,
                  false,
                  string.IsNullOrEmpty(request.Reason) ? generationRequest.Reason : request.Reason));

      DimensionGenerationPlanPreflightResult planPreflight =
          SkippedGenerationPlanPreflight(
              generationRequest.DimensionId,
              generationRequest.LocalBounds);

      if (!areaPreflight.CanProceed)
      {
        return BuildGenerationPreviewResult(
            false,
            areaPreflight.Code,
            areaPreflight.Message,
            areaPreflight,
            planPreflight,
            false,
            false,
            false,
            areaPreflight.Status);
      }

      bool wouldReuseExistingStatus =
          areaPreflight.AlreadyReady ||
          areaPreflight.ActiveGeneration;

      if (!wouldReuseExistingStatus && request.RequireReservationFree)
      {
        areaPreflight =
            PreflightGeneration(
                new DimensionGenerationPreflightRequest(
                    generationRequest.RequesterId,
                    generationRequest.DimensionId,
                    generationRequest.LocalBounds,
                    RuntimeGenerationOwnerId(generationRequest.RequesterId),
                    true,
                    true,
                    request.AllowReadyArea,
                    request.AllowActiveGeneration,
                    true,
                    false,
                    string.IsNullOrEmpty(request.Reason) ? generationRequest.Reason : request.Reason));
        if (!areaPreflight.CanProceed)
        {
          return BuildGenerationPreviewResult(
              false,
              areaPreflight.Code,
              areaPreflight.Message,
              areaPreflight,
              planPreflight,
              false,
              false,
              false,
              areaPreflight.Status);
        }
      }

      bool wouldReturnNotGenerated =
          !generationRequest.CreateIfMissing &&
          !wouldReuseExistingStatus &&
          areaPreflight.Status.State == DimensionGenerationState.NotGenerated;
      if (wouldReturnNotGenerated)
      {
        return BuildGenerationPreviewResult(
            true,
            string.Empty,
            "Generation request would return NotGenerated without queueing work.",
            areaPreflight,
            planPreflight,
            false,
            false,
            true,
            areaPreflight.Status);
      }

      if (wouldReuseExistingStatus)
      {
        return BuildGenerationPreviewResult(
            true,
            string.Empty,
            "Generation request would reuse the existing generated-area status.",
            areaPreflight,
            planPreflight,
            false,
            true,
            false,
            areaPreflight.Status);
      }

      if (generationRequest.CreateIfMissing && request.RequireExecutablePlan)
      {
        planPreflight =
            PreflightGenerationPlan(
                new DimensionGenerationPlanPreflightRequest(
                    new DimensionGenerationPlanRequest(
                        generationRequest.DimensionId,
                        generationRequest.LocalBounds,
                        string.Empty,
                        false,
                        false,
                        true),
                    true,
                    true,
                    true,
                    request.AllowFallbackProvider,
                    string.IsNullOrEmpty(request.Reason) ? generationRequest.Reason : request.Reason));
        if (!planPreflight.CanExecute)
        {
          return BuildGenerationPreviewResult(
              false,
              planPreflight.Code,
              planPreflight.Message,
              areaPreflight,
              planPreflight,
              false,
              false,
              false,
              areaPreflight.Status);
        }
      }

      return BuildGenerationPreviewResult(
          true,
          string.Empty,
          generationRequest.CreateIfMissing
              ? "Generation request would queue work."
              : "Generation request is valid and would not queue work.",
          areaPreflight,
          planPreflight,
          generationRequest.CreateIfMissing,
          false,
          false,
          areaPreflight.Status);
    }

    public bool TryCancelGeneration(
        string dimensionId,
        DimensionBounds localBounds,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionGenerationStatus status;
      if (!TryGetExactGenerationStatus(dimensionId, localBounds, out status))
      {
        result = DimensionOperationResult.Failed("generation-status-not-found", "No exact generated-area record exists for those bounds.");
        return false;
      }

      if (!IsTransientGenerationState(status.State))
      {
        result = DimensionOperationResult.Failed("generation-not-active", "Only active generation jobs can be cancelled.");
        return false;
      }

      if (IsProtectedGeneratedArea(dimensionId, localBounds))
      {
        result = DimensionOperationResult.Failed("generated-area-protected", "Starter generation areas cannot be cancelled while the starter is enabled.");
        return false;
      }

      string key = GenerationStatusKey(dimensionId, localBounds);
      RuntimeGenerationRecord record;
      if (runtimeGenerationRecords.TryGetValue(key, out record))
      {
        CancelRuntimeGenerationRecord(
            record,
            string.IsNullOrEmpty(reason) ? "Generation cancelled." : "Generation cancelled. " + reason,
            true,
            null);
      }
      else
      {
        SetGenerationStatus(
            dimensionId,
            localBounds,
            DimensionGenerationState.Failed,
            0f,
            string.IsNullOrEmpty(reason) ? "Generation cancelled." : "Generation cancelled. " + reason);
      }

      AddDiagnostic(DimensionDiagnosticSeverity.Info, dimensionId, "Generation cancelled for local bounds.");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryForgetGeneratedArea(
        string dimensionId,
        DimensionBounds localBounds,
        out DimensionOperationResult result)
    {
      DimensionGenerationStatus status;
      if (!TryGetExactGenerationStatus(dimensionId, localBounds, out status))
      {
        result = DimensionOperationResult.Failed("generation-status-not-found", "No exact generated-area record exists for those bounds.");
        return false;
      }

      if (IsProtectedGeneratedArea(dimensionId, localBounds))
      {
        result = DimensionOperationResult.Failed("generated-area-protected", "Starter generation areas cannot be forgotten while the starter is enabled.");
        return false;
      }

      string key = GenerationStatusKey(dimensionId, localBounds);
      RuntimeGenerationRecord record;
      if (runtimeGenerationRecords.TryGetValue(key, out record))
      {
        CancelRuntimeGenerationRecord(record, "Generated-area status was forgotten.", false, null);
      }

      generationStatuses.Remove(key);
      RemovePersistedGeneratedAreaIfWorldRegistryLoaded(dimensionId, localBounds);
      RefreshStarterLifecycleForGeneratedArea(dimensionId, localBounds, "generated area forgotten");
      AddDiagnostic(DimensionDiagnosticSeverity.Info, dimensionId, "Generated-area status forgotten for local bounds. Existing terrain was not removed.");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryGetGenerationReservation(
        string reservationId,
        out DimensionGenerationReservation reservation)
    {
      if (string.IsNullOrEmpty(reservationId))
      {
        reservation = default(DimensionGenerationReservation);
        return false;
      }

      return generationReservations.TryGetValue(reservationId, out reservation);
    }

    public IReadOnlyList<DimensionGenerationReservation> GetGenerationReservations(
        DimensionGenerationReservationQuery query)
    {
      List<DimensionGenerationReservation> result =
          new List<DimensionGenerationReservation>();
      foreach (DimensionGenerationReservation reservation in generationReservations.Values)
      {
        if (!GenerationReservationMatchesQuery(reservation, query))
        {
          continue;
        }

        result.Add(reservation);
      }

      result.Sort(CompareGenerationReservations);
      return result;
    }

    public bool TryReserveGenerationArea(
        DimensionGenerationReservationRequest request,
        out DimensionGenerationReservation reservation,
        out DimensionOperationResult result)
    {
      reservation = default(DimensionGenerationReservation);
      if (!ValidateGenerationReservationRequest(request, out result))
      {
        return false;
      }

      if (generationReservations.ContainsKey(request.ReservationId))
      {
        result = DimensionOperationResult.Failed("generation-reservation-already-exists", "A generation reservation with that id already exists.");
        return false;
      }

      DimensionGenerationReservation overlappingReservation;
      if (TryFindGenerationReservationOverlap(
          request.DimensionId,
          request.LocalBounds,
          request.OwnerId,
          request.AllowOverlapWithSameOwner,
          out overlappingReservation))
      {
        result =
            DimensionOperationResult.Failed(
                "generation-reservation-overlap",
                "The requested generation reservation overlaps " + overlappingReservation.ReservationId + ".");
        return false;
      }

      reservation =
          new DimensionGenerationReservation(
              request.ReservationId,
              request.DimensionId,
              request.LocalBounds,
              request.OwnerId,
              request.Purpose,
              request.Priority,
              UnityEngine.Time.realtimeSinceStartupAsDouble);
      generationReservations[reservation.ReservationId] = reservation;
      RaiseGenerationReservationChanged(
          reservation,
          DimensionGenerationReservationChangeKind.Reserved,
          string.IsNullOrEmpty(request.Reason) ? "reserved" : request.Reason);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryReleaseGenerationReservation(
        string reservationId,
        string reason,
        out DimensionGenerationReservation reservation,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(reservationId))
      {
        reservation = default(DimensionGenerationReservation);
        result = DimensionOperationResult.Failed("generation-reservation-id-empty", "A generation reservation id is required.");
        return false;
      }

      if (!generationReservations.TryGetValue(reservationId, out reservation))
      {
        result = DimensionOperationResult.Failed("generation-reservation-not-found", "No generation reservation with that id exists.");
        return false;
      }

      generationReservations.Remove(reservationId);
      RaiseGenerationReservationChanged(
          reservation,
          DimensionGenerationReservationChangeKind.Released,
          string.IsNullOrEmpty(reason) ? "released" : reason);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRegisterGenerationProvider(
        IDimensionGenerationProvider provider,
        out DimensionOperationResult result)
    {
      if (provider == null)
      {
        result = DimensionOperationResult.Failed("generation-provider-null", "A generation provider is required.");
        return false;
      }

      if (string.IsNullOrEmpty(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("generation-provider-id-empty", "A generation provider id is required.");
        return false;
      }

      if (generationProviders.ContainsKey(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("generation-provider-duplicate", "A generation provider with that id is already registered.");
        return false;
      }

      generationProviders[provider.ProviderId] = provider;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Generation provider registered: " + provider.ProviderId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveGenerationProvider(
        string providerId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(providerId))
      {
        result = DimensionOperationResult.Failed("generation-provider-id-empty", "A generation provider id is required.");
        return false;
      }

      IDimensionGenerationProvider provider;
      if (!generationProviders.TryGetValue(providerId, out provider))
      {
        result = DimensionOperationResult.Failed("generation-provider-not-found", "No generation provider with that id is registered.");
        return false;
      }

      CancelRuntimeGenerationRecordsForProvider(
          providerId,
          provider,
          "Generation provider was removed.");
      generationProviders.Remove(providerId);
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Generation provider removed: " + providerId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<string> GetGenerationProviderIds()
    {
      List<string> result = new List<string>(generationProviders.Count);
      foreach (string providerId in generationProviders.Keys)
      {
        result.Add(providerId);
      }

      result.Sort(StringComparer.Ordinal);
      return result;
    }

    public IReadOnlyList<DimensionGenerationPassDefinition> GetGenerationPasses(
        string dimensionId,
        string zoneId,
        bool includeDisabled)
    {
      List<DimensionGenerationPassDefinition> result =
          new List<DimensionGenerationPassDefinition>();

      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(generationPass.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!string.IsNullOrEmpty(zoneId) &&
            !string.IsNullOrEmpty(generationPass.ZoneId) &&
            !string.Equals(generationPass.ZoneId, zoneId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!includeDisabled && !generationPass.Enabled)
        {
          continue;
        }

        result.Add(generationPass);
      }

      result.Sort(CompareGenerationPasses);
      return result;
    }

    public bool TryGetGenerationPass(
        string passId,
        out DimensionGenerationPassDefinition generationPass)
    {
      if (string.IsNullOrEmpty(passId))
      {
        generationPass = default(DimensionGenerationPassDefinition);
        return false;
      }

      return generationPasses.TryGetValue(passId, out generationPass);
    }

    public bool TryRegisterGenerationPass(
        DimensionGenerationPassDefinition generationPass,
        out DimensionOperationResult result)
    {
      if (!ValidateGenerationPass(generationPass, out result))
      {
        return false;
      }

      if (generationPasses.ContainsKey(generationPass.PassId))
      {
        result = DimensionOperationResult.Failed("generation-pass-already-registered", "A generation pass with that id is already registered.");
        return false;
      }

      generationPasses[generationPass.PassId] = generationPass;
      RaiseGenerationPassChanged(
          generationPass,
          DimensionGenerationPassChangeKind.Registered,
          false,
          generationPass.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateGenerationPass(
        DimensionGenerationPassDefinition generationPass,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionGenerationPassDefinition previous;
      if (!generationPasses.TryGetValue(generationPass.PassId, out previous))
      {
        result = DimensionOperationResult.Failed("generation-pass-not-found", "No generation pass with that id is registered.");
        return false;
      }

      if (!ValidateGenerationPass(generationPass, out result))
      {
        return false;
      }

      if (GenerationPassEquals(previous, generationPass))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      generationPasses[generationPass.PassId] = generationPass;
      RaiseGenerationPassChanged(
          generationPass,
          previous.Enabled == generationPass.Enabled
              ? DimensionGenerationPassChangeKind.Updated
              : DimensionGenerationPassChangeKind.EnabledChanged,
          previous.Enabled,
          generationPass.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetGenerationPassEnabled(
        string passId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(passId))
      {
        result = DimensionOperationResult.Failed("generation-pass-id-empty", "A generation pass id is required.");
        return false;
      }

      DimensionGenerationPassDefinition generationPass;
      if (!generationPasses.TryGetValue(passId, out generationPass))
      {
        result = DimensionOperationResult.Failed("generation-pass-not-found", "No generation pass with that id is registered.");
        return false;
      }

      if (generationPass.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionGenerationPassDefinition updated =
          new DimensionGenerationPassDefinition(
              generationPass.PassId,
              generationPass.DisplayName,
              generationPass.DimensionId,
              generationPass.ZoneId,
              generationPass.HasLocalBounds,
              generationPass.LocalBounds,
              generationPass.Phase,
              generationPass.Priority,
              generationPass.ProviderId,
              enabled);

      generationPasses[passId] = updated;
      RaiseGenerationPassChanged(
          updated,
          DimensionGenerationPassChangeKind.EnabledChanged,
          generationPass.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveGenerationPass(
        string passId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(passId))
      {
        result = DimensionOperationResult.Failed("generation-pass-id-empty", "A generation pass id is required.");
        return false;
      }

      DimensionGenerationPassDefinition generationPass;
      if (!generationPasses.TryGetValue(passId, out generationPass))
      {
        result = DimensionOperationResult.Failed("generation-pass-not-found", "No generation pass with that id is registered.");
        return false;
      }

      generationPasses.Remove(passId);
      RaiseGenerationPassChanged(
          generationPass,
          DimensionGenerationPassChangeKind.Removed,
          generationPass.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public DimensionGenerationPlan BuildGenerationPlan(DimensionGenerationPlanRequest request)
    {
      DimensionDefinition dimension;
      if (!TryGetDimension(request.DimensionId, out dimension))
      {
        return new DimensionGenerationPlan(
            false,
            "The target dimension is not registered.",
            request.DimensionId,
            request.LocalBounds,
            request.ZoneId,
            new List<DimensionGenerationPassDefinition>());
      }

      string boundsError;
      if (!IsValidGenerationBounds(request.LocalBounds, out boundsError))
      {
        return new DimensionGenerationPlan(
            false,
            boundsError,
            request.DimensionId,
            request.LocalBounds,
            request.ZoneId,
            new List<DimensionGenerationPassDefinition>());
      }

      DimensionArea area;
      if (!TryGetArea(request.DimensionId, request.LocalBounds, out area))
      {
        return new DimensionGenerationPlan(
            false,
            "The requested generation area is outside the target dimension bounds.",
            request.DimensionId,
            request.LocalBounds,
            request.ZoneId,
            new List<DimensionGenerationPassDefinition>());
      }

      string effectiveZoneId = request.ZoneId ?? string.Empty;
      if (request.ResolveZoneFromArea && string.IsNullOrEmpty(effectiveZoneId))
      {
        float2 center =
            new float2(
                (request.LocalBounds.Min.x + request.LocalBounds.MaxExclusive.x) * 0.5f,
                (request.LocalBounds.Min.y + request.LocalBounds.MaxExclusive.y) * 0.5f);
        DimensionZoneDefinition zone;
        if (TryFindZoneDefinitionAtLocal(request.DimensionId, center, out zone))
        {
          effectiveZoneId = zone.ZoneId;
        }
      }

      List<DimensionGenerationPassDefinition> passes =
          new List<DimensionGenerationPassDefinition>();
      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        if (GenerationPassAppliesToPlan(generationPass, request, effectiveZoneId))
        {
          passes.Add(generationPass);
        }
      }

      passes.Sort(CompareGenerationPasses);
      return new DimensionGenerationPlan(
          true,
          passes.Count == 0
              ? "No generation passes matched the requested area."
              : "Generation plan built.",
          request.DimensionId,
          request.LocalBounds,
          effectiveZoneId,
          passes);
    }

    public DimensionGenerationPlanPreflightResult PreflightGenerationPlan(
        DimensionGenerationPlanPreflightRequest request)
    {
      DimensionGenerationPlanRequest inclusivePlanRequest =
          new DimensionGenerationPlanRequest(
              request.PlanRequest.DimensionId,
              request.PlanRequest.LocalBounds,
              request.PlanRequest.ZoneId,
              true,
              request.PlanRequest.ResolveZoneFromArea,
              false);
      DimensionGenerationPlan plan = BuildGenerationPlan(inclusivePlanRequest);
      if (!plan.Success)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-invalid",
            plan.Message,
            plan,
            0,
            0,
            0,
            0,
            0,
            0,
            false);
      }

      DimensionDefinition definition;
      bool hasDimension = TryGetDimension(plan.DimensionId, out definition);
      int matchingPassCount = plan.Passes == null ? 0 : plan.Passes.Count;
      int enabledPassCount = 0;
      int disabledPassCount = 0;
      int missingProviderCount = 0;
      int providerRejectedPassCount = 0;
      int executablePassCount = 0;

      for (int i = 0; i < matchingPassCount; i++)
      {
        DimensionGenerationPassDefinition generationPass = plan.Passes[i];
        if (!generationPass.Enabled)
        {
          disabledPassCount++;
          continue;
        }

        enabledPassCount++;
        IDimensionGenerationProvider provider;
        if (string.IsNullOrEmpty(generationPass.ProviderId) ||
            !generationProviders.TryGetValue(generationPass.ProviderId, out provider) ||
            provider == null)
        {
          missingProviderCount++;
          continue;
        }

        if (request.RequireProviderCanGenerate &&
            (!hasDimension || !provider.CanGenerate(definition, plan.LocalBounds)))
        {
          providerRejectedPassCount++;
          continue;
        }

        executablePassCount++;
      }

      bool hasFallbackProvider =
          request.AllowFallbackProvider &&
          HasFallbackGenerationProvider(plan.DimensionId, plan.LocalBounds);

      if (request.RequireRegisteredProviders && missingProviderCount > 0)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-missing-provider",
            "One or more matching generation passes reference a missing provider.",
            plan,
            matchingPassCount,
            enabledPassCount,
            disabledPassCount,
            missingProviderCount,
            providerRejectedPassCount,
            executablePassCount,
            hasFallbackProvider);
      }

      if (request.RequireAnyPass && matchingPassCount <= 0 && !hasFallbackProvider)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-empty",
            "No generation passes or fallback providers matched the requested area.",
            plan,
            matchingPassCount,
            enabledPassCount,
            disabledPassCount,
            missingProviderCount,
            providerRejectedPassCount,
            executablePassCount,
            hasFallbackProvider);
      }

      if (request.RequireProviderCanGenerate &&
          executablePassCount <= 0 &&
          !hasFallbackProvider)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-no-executable-provider",
            "No matching generation provider can generate the requested area.",
            plan,
            matchingPassCount,
            enabledPassCount,
            disabledPassCount,
            missingProviderCount,
            providerRejectedPassCount,
            executablePassCount,
            hasFallbackProvider);
      }

      return BuildGenerationPlanPreflightResult(
          true,
          string.Empty,
          "Generation plan preflight passed.",
          plan,
          matchingPassCount,
          enabledPassCount,
          disabledPassCount,
          missingProviderCount,
          providerRejectedPassCount,
          executablePassCount,
          hasFallbackProvider);
    }


  }
}
