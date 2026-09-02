using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Loading;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private void QueueRuntimeGeneration(
        DimensionGenerationRequest request,
        DimensionGenerationStatus queuedStatus)
    {
      string key = GenerationStatusKey(request.DimensionId, request.LocalBounds);
      RuntimeGenerationRecord existing;
      if (runtimeGenerationRecords.TryGetValue(key, out existing))
      {
        EnsureRuntimeGenerationReservation(existing);
        existing.Request = request;
        existing.State = queuedStatus.State;
        existing.UpdatedAt = Time.realtimeSinceStartupAsDouble;
        existing.ProviderId = string.Empty;
        if (existing.PlannedPasses == null)
        {
          existing.PlannedPasses = new List<DimensionGenerationPassDefinition>();
        }
        else
        {
          existing.PlannedPasses.Clear();
        }

        existing.PlannedPassIndex = 0;
        existing.UsePlannedPasses = false;
        existing.ProviderStartedAt = 0;
        return;
      }

      double now = Time.realtimeSinceStartupAsDouble;
      string reservationId = RuntimeGenerationReservationId(key);
      DimensionGenerationReservation reservation;
      DimensionOperationResult reservationResult;
      if (!TryReserveGenerationArea(
          new DimensionGenerationReservationRequest(
              reservationId,
              request.DimensionId,
              request.LocalBounds,
              RuntimeGenerationOwnerId(request.RequesterId),
              "runtime generation",
              request.Priority,
              false,
              string.IsNullOrEmpty(request.Reason) ? "generation queued" : request.Reason),
          out reservation,
          out reservationResult))
      {
        SetGenerationStatus(
            request.DimensionId,
            request.LocalBounds,
            DimensionGenerationState.Failed,
            0f,
            reservationResult.Message);
        return;
      }

      runtimeGenerationRecords[key] = new RuntimeGenerationRecord
      {
        Key = key,
        Request = request,
        LoadTicketId = string.Empty,
        ReservationId = reservation.ReservationId,
        ProviderId = string.Empty,
        PlannedPasses = new List<DimensionGenerationPassDefinition>(),
        PlannedPassIndex = 0,
        UsePlannedPasses = false,
        State = queuedStatus.State,
        CreatedAt = now,
        UpdatedAt = now,
        ProviderStartedAt = 0
      };
    }

    private void ProcessRuntimeGeneration(double now)
    {
      if (!IsServerWorldAvailable())
      {
        MarkAllRuntimeGenerationFailed("The server world is not available.");
        return;
      }

      runtimeGenerationKeys.Clear();
      foreach (string key in runtimeGenerationRecords.Keys)
      {
        runtimeGenerationKeys.Add(key);
      }

      for (int i = 0; i < runtimeGenerationKeys.Count; i++)
      {
        RuntimeGenerationRecord record;
        if (runtimeGenerationRecords.TryGetValue(runtimeGenerationKeys[i], out record))
        {
          ProcessRuntimeGenerationRecord(record, now);
        }
      }

      runtimeGenerationKeys.Clear();
    }

    private void ProcessRuntimeGenerationRecord(RuntimeGenerationRecord record, double now)
    {
      DimensionGenerationStatus current;
      if (!TryGetGenerationStatus(record.Request.DimensionId, record.Request.LocalBounds, out current))
      {
        FailRuntimeGeneration(record, "Generation status disappeared.");
        return;
      }

      if (current.State == DimensionGenerationState.Ready ||
          current.State == DimensionGenerationState.Failed ||
          current.State == DimensionGenerationState.NotGenerated)
      {
        CompleteRuntimeGenerationRecord(record, "generation state finalized");
        return;
      }

      if (current.State == DimensionGenerationState.Queued)
      {
        if (ShouldPreloadBeforeRuntimeGeneration(record))
        {
          StartRuntimeGenerationLoad(record);
        }
        else
        {
          StartRuntimeGenerationProvider(record, now);
        }

        return;
      }

      if (current.State == DimensionGenerationState.LoadingArea)
      {
        ContinueRuntimeGenerationLoad(record, now);
        return;
      }

      TickRuntimeGenerationProvider(record, current, now);
    }

    private void StartRuntimeGenerationLoad(RuntimeGenerationRecord record)
    {
      DimensionLoadTicket ticket =
          RequestLoad(
              new DimensionLoadRequest(
                  "dimension-generation:" + record.Request.RequesterId,
                  record.Request.DimensionId,
                  record.Request.LocalBounds,
                  true,
                  false,
                  GenerationLoadTimeoutSeconds,
                  "Generation area preload."));

      if (!ticket.IsValid)
      {
        FailRuntimeGeneration(record, ticket.Message);
        return;
      }

      record.LoadTicketId = ticket.TicketId;
      record.State = DimensionGenerationState.LoadingArea;
      record.UpdatedAt = Time.realtimeSinceStartupAsDouble;
      SetGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          DimensionGenerationState.LoadingArea,
          0.10f,
          "Loading generation area.");
    }

    private void ContinueRuntimeGenerationLoad(RuntimeGenerationRecord record, double now)
    {
      DimensionLoadTicket ticket;
      if (string.IsNullOrEmpty(record.LoadTicketId) ||
          !TryGetLoadStatus(record.LoadTicketId, out ticket) ||
          !ticket.IsValid)
      {
        FailRuntimeGeneration(record, "Generation area load ticket disappeared.");
        return;
      }

      if (ticket.State == DimensionLoadState.Failed)
      {
        FailRuntimeGeneration(record, ticket.Message);
        return;
      }

      if (now - record.CreatedAt > GenerationLoadTimeoutSeconds)
      {
        FailRuntimeGeneration(record, "Generation area did not load in time.");
        return;
      }

      if (!IsLoadTicketReady(ticket))
      {
        return;
      }

      StartRuntimeGenerationProvider(record, now);
    }

    private bool ShouldPreloadBeforeRuntimeGeneration(RuntimeGenerationRecord record)
    {
      if (record == null)
      {
        return false;
      }

      DimensionGenerationStatus containingReadyStatus;
      if (!TryFindContainingGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          true,
          out containingReadyStatus))
      {
        return false;
      }

      return containingReadyStatus.State == DimensionGenerationState.Ready;
    }

    private void FailRuntimeGeneration(RuntimeGenerationRecord record, string message)
    {
      CancelRuntimeGenerationRecord(
          record,
          string.IsNullOrEmpty(message) ? "Generation failed." : message,
          true,
          null);
    }

    private void CancelRuntimeGenerationRecordsForProvider(
        string providerId,
        IDimensionGenerationProvider provider,
        string message)
    {
      runtimeGenerationKeys.Clear();
      foreach (string key in runtimeGenerationRecords.Keys)
      {
        runtimeGenerationKeys.Add(key);
      }

      for (int i = 0; i < runtimeGenerationKeys.Count; i++)
      {
        RuntimeGenerationRecord record;
        if (runtimeGenerationRecords.TryGetValue(runtimeGenerationKeys[i], out record) &&
            string.Equals(record.ProviderId, providerId, StringComparison.Ordinal))
        {
          CancelRuntimeGenerationRecord(record, message, true, provider);
        }
      }

      runtimeGenerationKeys.Clear();
    }

    private void CancelRuntimeGenerationRecord(
        RuntimeGenerationRecord record,
        string message,
        bool writeFailedStatus,
        IDimensionGenerationProvider providerOverride)
    {
      if (record == null)
      {
        return;
      }

      NotifyGenerationProviderCancelled(record, message, providerOverride);
      CompleteRuntimeGenerationRecord(record, message);
      if (writeFailedStatus)
      {
        SetGenerationStatus(
            record.Request.DimensionId,
            record.Request.LocalBounds,
            DimensionGenerationState.Failed,
            0f,
            string.IsNullOrEmpty(message) ? "Generation failed." : message);
      }
    }

    private void NotifyGenerationProviderCancelled(
        RuntimeGenerationRecord record,
        string reason,
        IDimensionGenerationProvider providerOverride)
    {
      if (record == null)
      {
        return;
      }

      IDimensionGenerationProvider provider = providerOverride;
      if (provider == null &&
          !string.IsNullOrEmpty(record.ProviderId))
      {
        generationProviders.TryGetValue(record.ProviderId, out provider);
      }

      IDimensionGenerationControlProvider controlProvider =
          provider as IDimensionGenerationControlProvider;
      if (controlProvider == null)
      {
        return;
      }

      DimensionDefinition definition;
      DimensionArea area;
      if (!TryGetDimension(record.Request.DimensionId, out definition) ||
          !TryGetArea(record.Request.DimensionId, record.Request.LocalBounds, out area))
      {
        return;
      }

      try
      {
        controlProvider.TryCancelGeneration(
            definition,
            area,
            string.IsNullOrEmpty(reason) ? "Generation cancelled." : reason);
      }
      catch (Exception ex)
      {
        AddDiagnostic(
            DimensionDiagnosticSeverity.Warning,
            record.Request.DimensionId,
            "Generation provider cancellation callback failed: " + ex.Message);
      }
    }

    private void MarkAllRuntimeGenerationFailed(string message)
    {
      runtimeGenerationKeys.Clear();
      foreach (string key in runtimeGenerationRecords.Keys)
      {
        runtimeGenerationKeys.Add(key);
      }

      for (int i = 0; i < runtimeGenerationKeys.Count; i++)
      {
        RuntimeGenerationRecord record;
        if (runtimeGenerationRecords.TryGetValue(runtimeGenerationKeys[i], out record))
        {
          FailRuntimeGeneration(record, message);
        }
      }

      runtimeGenerationKeys.Clear();
    }

    private void ResetRuntimeGenerationState()
    {
      CompleteAllRuntimeGenerationRecords("runtime generation state reset");
      runtimeGenerationKeys.Clear();
      nextRuntimeGenerationTickAt = 0;
    }

    private void CompleteAllRuntimeGenerationRecords(string reason)
    {
      runtimeGenerationKeys.Clear();
      foreach (string key in runtimeGenerationRecords.Keys)
      {
        runtimeGenerationKeys.Add(key);
      }

      for (int i = 0; i < runtimeGenerationKeys.Count; i++)
      {
        RuntimeGenerationRecord record;
        if (runtimeGenerationRecords.TryGetValue(runtimeGenerationKeys[i], out record))
        {
          CompleteRuntimeGenerationRecord(record, reason);
        }
      }

      runtimeGenerationKeys.Clear();
    }

    private void CompleteRuntimeGenerationRecord(
        RuntimeGenerationRecord record,
        string reason)
    {
      if (record == null)
      {
        return;
      }

      ReleaseRuntimeGenerationLoadTicket(record);
      ReleaseRuntimeGenerationReservation(record, reason);
      runtimeGenerationRecords.Remove(record.Key);
    }

    private void ReleaseRuntimeGenerationLoadTicket(RuntimeGenerationRecord record)
    {
      if (record == null || string.IsNullOrEmpty(record.LoadTicketId))
      {
        return;
      }

      ReleaseLoadTicket(record.LoadTicketId);
      record.LoadTicketId = string.Empty;
    }
  }
}
