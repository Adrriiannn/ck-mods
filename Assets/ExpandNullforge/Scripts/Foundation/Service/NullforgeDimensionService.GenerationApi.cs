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
            "Generation status changed. dimension=" +
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

    /// <summary>
    /// The arena reset's forget: every generated-area record of one dimension, starter
    /// protection bypassed.
    /// </summary>
    /// <remarks>
    /// The public forget refuses starter areas because forgetting one under a live dimension
    /// strands its entry. The arena reset is the one caller that MEANS it — it forgets so the
    /// starter re-queues and regenerates a clean floor. Terrain and entity clearing are the
    /// reset system's job; this only clears the bookkeeping that would otherwise make
    /// RequestGeneration return Ready without doing anything.
    /// </remarks>
    internal void ForgetGeneratedAreasForReset(string dimensionId)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        return;
      }

      string prefix = dimensionId + "|";
      List<string> keys = new List<string>();
      List<DimensionGenerationStatus> statuses = new List<DimensionGenerationStatus>();
      foreach (KeyValuePair<string, DimensionGenerationStatus> entry in generationStatuses)
      {
        if (entry.Key.StartsWith(prefix, StringComparison.Ordinal))
        {
          keys.Add(entry.Key);
          statuses.Add(entry.Value);
        }
      }

      for (int i = 0; i < keys.Count; i++)
      {
        RuntimeGenerationRecord record;
        if (runtimeGenerationRecords.TryGetValue(keys[i], out record))
        {
          CancelRuntimeGenerationRecord(record, "Arena reset forgot this generated area.", false, null);
        }

        generationStatuses.Remove(keys[i]);
        RemovePersistedGeneratedAreaIfWorldRegistryLoaded(dimensionId, statuses[i].LocalBounds);
      }

      if (keys.Count > 0)
      {
        // Once, not per area: the lifecycle refresh walks the starter state and re-queues
        // the entry area, and doing that per forgotten record is quadratic noise.
        RefreshStarterLifecycleForGeneratedArea(
            dimensionId,
            statuses[0].LocalBounds,
            "arena reset");
        AddDiagnostic(
            DimensionDiagnosticSeverity.Info,
            dimensionId,
            "Arena reset forgot " + keys.Count + " generated area(s); the starter will regenerate.");
      }
    }
  }
}
