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

    private void StartRuntimeGenerationProvider(RuntimeGenerationRecord record, double now)
    {
      IDimensionGenerationProvider provider;
      if (TryPreparePlannedGeneration(record, now, out provider))
      {
        return;
      }

      if (!TryGetGenerationProvider(record.Request.DimensionId, record.Request.LocalBounds, out provider))
      {
        FailRuntimeGeneration(record, "No registered generation provider can generate this area.");
        return;
      }

      record.ProviderId = provider.ProviderId;
      record.ProviderStartedAt = now;
      record.State = DimensionGenerationState.GeneratingTerrain;
      record.UpdatedAt = now;
      SetGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          DimensionGenerationState.GeneratingTerrain,
          0.25f,
          "Generation provider started: " + provider.ProviderId + ".");
    }

    private void TickRuntimeGenerationProvider(
        RuntimeGenerationRecord record,
        DimensionGenerationStatus current,
        double now)
    {
      if (record.UsePlannedPasses)
      {
        TickRuntimeGenerationPassProvider(record, current, now);
        return;
      }

      IDimensionGenerationProvider provider;
      if (string.IsNullOrEmpty(record.ProviderId) ||
          !generationProviders.TryGetValue(record.ProviderId, out provider))
      {
        if (!TryGetGenerationProvider(record.Request.DimensionId, record.Request.LocalBounds, out provider))
        {
          FailRuntimeGeneration(record, "Generation provider is no longer available.");
          return;
        }

        record.ProviderId = provider.ProviderId;
      }

      DimensionDefinition definition;
      DimensionArea area;
      if (!TryGetDimension(record.Request.DimensionId, out definition) ||
          !TryGetArea(record.Request.DimensionId, record.Request.LocalBounds, out area))
      {
        FailRuntimeGeneration(record, "Generation target dimension or area is no longer valid.");
        return;
      }

      DimensionGenerationProviderResult providerResult =
          provider.TickGeneration(
              new DimensionGenerationContext(
                  serverWorld,
                  definition,
                  area,
                  current,
                  Math.Max(0.0d, now - record.ProviderStartedAt)));

      DimensionGenerationState state =
          NormalizeGenerationState(providerResult.State);
      if (state == DimensionGenerationState.Unknown ||
          state == DimensionGenerationState.NotGenerated ||
          state == DimensionGenerationState.Queued ||
          state == DimensionGenerationState.LoadingArea)
      {
        state = DimensionGenerationState.GeneratingTerrain;
      }

      record.State = state;
      record.UpdatedAt = now;
      SetGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          state,
          providerResult.Progress01,
          providerResult.Message);

      if (state == DimensionGenerationState.Ready ||
          state == DimensionGenerationState.Failed)
      {
        if (state == DimensionGenerationState.Failed)
        {
          NotifyGenerationProviderCancelled(
              record,
              "Generation provider reported failure.",
              provider);
        }

        CompleteRuntimeGenerationRecord(record, "generation provider finished");
      }
    }

    private bool TryPreparePlannedGeneration(
        RuntimeGenerationRecord record,
        double now,
        out IDimensionGenerationProvider provider)
    {
      provider = null;
      if (record.PlannedPasses == null)
      {
        record.PlannedPasses = new List<DimensionGenerationPassDefinition>();
      }
      else
      {
        record.PlannedPasses.Clear();
      }

      record.PlannedPassIndex = 0;
      record.UsePlannedPasses = false;

      DimensionGenerationPlan plan =
          BuildGenerationPlan(
              new DimensionGenerationPlanRequest(
                  record.Request.DimensionId,
                  record.Request.LocalBounds,
                  string.Empty,
                  false,
                  false,
                  true));
      if (!plan.Success || plan.Passes.Count == 0)
      {
        return false;
      }

      DimensionDefinition definition;
      if (!TryGetDimension(record.Request.DimensionId, out definition))
      {
        return false;
      }

      for (int i = 0; i < plan.Passes.Count; i++)
      {
        DimensionGenerationPassDefinition generationPass = plan.Passes[i];
        IDimensionGenerationProvider candidate;
        if (!generationProviders.TryGetValue(generationPass.ProviderId, out candidate) ||
            candidate == null ||
            !(candidate is IDimensionGenerationPassProvider) ||
            !candidate.CanGenerate(definition, record.Request.LocalBounds))
        {
          continue;
        }

        record.PlannedPasses.Add(generationPass);
      }

      if (record.PlannedPasses.Count == 0)
      {
        return false;
      }

      DimensionGenerationPassDefinition firstPass = record.PlannedPasses[0];
      if (!generationProviders.TryGetValue(firstPass.ProviderId, out provider) || provider == null)
      {
        return false;
      }

      record.ProviderId = provider.ProviderId;
      record.ProviderStartedAt = now;
      record.PlannedPassIndex = 0;
      record.UsePlannedPasses = true;
      record.State = GenerationStateForPassPhase(firstPass.Phase);
      record.UpdatedAt = now;
      SetGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          record.State,
          ComputePlannedGenerationProgress(record, 0f),
          "Generation pass started: " + GenerationPassName(firstPass) + ".");
      return true;
    }

    private void TickRuntimeGenerationPassProvider(
        RuntimeGenerationRecord record,
        DimensionGenerationStatus current,
        double now)
    {
      if (record.PlannedPasses == null ||
          record.PlannedPassIndex < 0 ||
          record.PlannedPassIndex >= record.PlannedPasses.Count)
      {
        FailRuntimeGeneration(record, "The generation plan is no longer valid.");
        return;
      }

      DimensionDefinition definition;
      DimensionArea area;
      if (!TryGetDimension(record.Request.DimensionId, out definition) ||
          !TryGetArea(record.Request.DimensionId, record.Request.LocalBounds, out area))
      {
        FailRuntimeGeneration(record, "Generation target dimension or area is no longer valid.");
        return;
      }

      int completedPassesThisTick = 0;
      while (completedPassesThisTick < MaxPlannedGenerationPassesPerTick)
      {
        if (record.PlannedPassIndex < 0 ||
            record.PlannedPassIndex >= record.PlannedPasses.Count)
        {
          FailRuntimeGeneration(record, "The generation plan is no longer valid.");
          return;
        }

        DimensionGenerationPassDefinition generationPass =
            record.PlannedPasses[record.PlannedPassIndex];

        IDimensionGenerationProvider provider;
        if (string.IsNullOrEmpty(record.ProviderId) ||
            !string.Equals(record.ProviderId, generationPass.ProviderId, StringComparison.Ordinal) ||
            !generationProviders.TryGetValue(generationPass.ProviderId, out provider) ||
            provider == null)
        {
          FailRuntimeGeneration(record, "Generation pass provider is no longer available.");
          return;
        }

        IDimensionGenerationPassProvider passProvider =
            provider as IDimensionGenerationPassProvider;
        if (passProvider == null)
        {
          FailRuntimeGeneration(record, "Generation pass provider does not support pass execution.");
          return;
        }

        DimensionGenerationProviderResult providerResult =
            passProvider.TickGenerationPass(
                new DimensionGenerationPassContext(
                    new DimensionGenerationContext(
                        serverWorld,
                        definition,
                        area,
                        current,
                        Math.Max(0.0d, now - record.ProviderStartedAt)),
                    generationPass,
                    record.PlannedPassIndex,
                    record.PlannedPasses.Count));

        DimensionGenerationState state =
            NormalizeGenerationState(providerResult.State);

        if (state == DimensionGenerationState.Failed)
        {
          record.State = DimensionGenerationState.Failed;
          record.UpdatedAt = now;
          SetGenerationStatus(
              record.Request.DimensionId,
              record.Request.LocalBounds,
              DimensionGenerationState.Failed,
              ComputePlannedGenerationProgress(record, providerResult.Progress01),
              providerResult.Message);
          NotifyGenerationProviderCancelled(
              record,
              "Generation pass provider reported failure.",
              provider);
          CompleteRuntimeGenerationRecord(record, "generation pass failed");
          return;
        }

        if (state == DimensionGenerationState.Ready)
        {
          completedPassesThisTick++;
          if (record.PlannedPassIndex + 1 >= record.PlannedPasses.Count)
          {
            record.State = DimensionGenerationState.Ready;
            record.UpdatedAt = now;
            SetGenerationStatus(
                record.Request.DimensionId,
                record.Request.LocalBounds,
                DimensionGenerationState.Ready,
                1.0f,
                string.IsNullOrEmpty(providerResult.Message)
                    ? "Generation plan completed."
                    : providerResult.Message);
            CompleteRuntimeGenerationRecord(record, "generation plan completed");
            return;
          }

          record.PlannedPassIndex++;
          DimensionGenerationPassDefinition nextPass =
              record.PlannedPasses[record.PlannedPassIndex];
          IDimensionGenerationProvider nextProvider;
          if (!generationProviders.TryGetValue(nextPass.ProviderId, out nextProvider) || nextProvider == null)
          {
            FailRuntimeGeneration(record, "Next generation pass provider is no longer available.");
            return;
          }

          record.ProviderId = nextProvider.ProviderId;
          record.ProviderStartedAt = now;
          record.State = GenerationStateForPassPhase(nextPass.Phase);
          record.UpdatedAt = now;
          current =
              new DimensionGenerationStatus(
                  record.Request.DimensionId,
                  record.Request.LocalBounds,
                  record.State,
                  ComputePlannedGenerationProgress(record, 0f),
                  "Generation pass started: " + GenerationPassName(nextPass) + ".");
          SetGenerationStatus(
              current.DimensionId,
              current.LocalBounds,
              current.State,
              current.Progress01,
              current.Message);
          continue;
        }

        DimensionGenerationState passState = GenerationStateForPassPhase(generationPass.Phase);
        if (state == DimensionGenerationState.Unknown ||
            state == DimensionGenerationState.NotGenerated ||
            state == DimensionGenerationState.Queued ||
            state == DimensionGenerationState.LoadingArea)
        {
          state = passState;
        }

        record.State = state;
        record.UpdatedAt = now;
        SetGenerationStatus(
            record.Request.DimensionId,
            record.Request.LocalBounds,
            state,
            ComputePlannedGenerationProgress(record, providerResult.Progress01),
            string.IsNullOrEmpty(providerResult.Message)
                ? "Running generation pass: " + GenerationPassName(generationPass) + "."
                : providerResult.Message);
        return;
      }
    }

    private bool TryGetGenerationProvider(
        string dimensionId,
        DimensionBounds localBounds,
        out IDimensionGenerationProvider provider)
    {
      provider = null;
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        return false;
      }

      DimensionGenerationPlan plan =
          BuildGenerationPlan(
              new DimensionGenerationPlanRequest(
                  dimensionId,
                  localBounds,
                  string.Empty,
                  false,
                  false,
                  true));
      if (plan.Success)
      {
        for (int i = 0; i < plan.Passes.Count; i++)
        {
          DimensionGenerationPassDefinition generationPass = plan.Passes[i];
          IDimensionGenerationProvider plannedProvider;
          if (generationProviders.TryGetValue(generationPass.ProviderId, out plannedProvider) &&
              plannedProvider != null &&
              plannedProvider.CanGenerate(definition, localBounds))
          {
            provider = plannedProvider;
            return true;
          }
        }
      }

      IReadOnlyList<string> providerIds = GetGenerationProviderIds();
      for (int i = 0; i < providerIds.Count; i++)
      {
        IDimensionGenerationProvider candidate;
        generationProviders.TryGetValue(providerIds[i], out candidate);
        if (candidate != null && candidate.CanGenerate(definition, localBounds))
        {
          provider = candidate;
          return true;
        }
      }

      return false;
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

    private void EnsureRuntimeGenerationReservation(RuntimeGenerationRecord record)
    {
      if (record == null || !string.IsNullOrEmpty(record.ReservationId))
      {
        return;
      }

      string reservationId = RuntimeGenerationReservationId(record.Key);
      DimensionGenerationReservation reservation;
      DimensionOperationResult result;
      if (TryReserveGenerationArea(
          new DimensionGenerationReservationRequest(
              reservationId,
              record.Request.DimensionId,
              record.Request.LocalBounds,
              RuntimeGenerationOwnerId(record.Request.RequesterId),
              "runtime generation",
              record.Request.Priority,
              false,
              "runtime reservation restored"),
          out reservation,
          out result))
      {
        record.ReservationId = reservation.ReservationId;
        return;
      }

      AddDiagnostic(
          DimensionDiagnosticSeverity.Warning,
          record.Request.DimensionId,
          "Failed to restore runtime generation reservation: " + result.Message);
    }

    private bool TryGetRuntimeGenerationStatusForReservation(
        DimensionGenerationReservation reservation,
        DimensionBounds requestedBounds,
        out DimensionGenerationStatus status)
    {
      RuntimeGenerationRecord record;
      if (!TryFindRuntimeGenerationRecordForReservation(reservation, out record) ||
          !BoundsContain(record.Request.LocalBounds, requestedBounds))
      {
        status = default(DimensionGenerationStatus);
        return false;
      }

      if (TryGetGenerationStatus(record.Request.DimensionId, record.Request.LocalBounds, out status))
      {
        return true;
      }

      status =
          new DimensionGenerationStatus(
              record.Request.DimensionId,
              record.Request.LocalBounds,
              record.State,
              0f,
              "Runtime generation is active for the containing area.");
      return true;
    }

    private bool TryFindRuntimeGenerationRecordForReservation(
        DimensionGenerationReservation reservation,
        out RuntimeGenerationRecord record)
    {
      if (string.IsNullOrEmpty(reservation.ReservationId))
      {
        record = null;
        return false;
      }

      foreach (RuntimeGenerationRecord candidate in runtimeGenerationRecords.Values)
      {
        if (candidate == null)
        {
          continue;
        }

        if (string.Equals(candidate.ReservationId, reservation.ReservationId, StringComparison.Ordinal) ||
            (BoundsEqual(candidate.Request.LocalBounds, reservation.LocalBounds) &&
             string.Equals(candidate.Request.DimensionId, reservation.DimensionId, StringComparison.Ordinal)))
        {
          record = candidate;
          return true;
        }
      }

      record = null;
      return false;
    }

    private bool TryReleaseStaleRuntimeGenerationReservation(
        DimensionGenerationReservation reservation,
        string reason)
    {
      if (!IsRuntimeGenerationReservation(reservation))
      {
        return false;
      }

      RuntimeGenerationRecord record;
      if (TryFindRuntimeGenerationRecordForReservation(reservation, out record))
      {
        DimensionGenerationStatus status;
        if (!TryGetGenerationStatus(record.Request.DimensionId, record.Request.LocalBounds, out status) ||
            status.State == DimensionGenerationState.Ready ||
            status.State == DimensionGenerationState.Failed ||
            status.State == DimensionGenerationState.NotGenerated)
        {
          CompleteRuntimeGenerationRecord(record, reason);
          return true;
        }

        return false;
      }

      DimensionGenerationReservation releasedReservation;
      DimensionOperationResult releaseResult;
      return TryReleaseGenerationReservation(
          reservation.ReservationId,
          string.IsNullOrEmpty(reason) ? "stale runtime generation reservation cleared" : reason,
          out releasedReservation,
          out releaseResult);
    }

    private static bool IsRuntimeGenerationReservation(
        DimensionGenerationReservation reservation)
    {
      return !string.IsNullOrEmpty(reservation.ReservationId) &&
             reservation.ReservationId.StartsWith(
                 "expandnullforge:runtime-generation:",
                 StringComparison.Ordinal);
    }

    private void ReleaseRuntimeGenerationReservation(
        RuntimeGenerationRecord record,
        string reason)
    {
      if (record == null || string.IsNullOrEmpty(record.ReservationId))
      {
        return;
      }

      DimensionGenerationReservation reservation;
      DimensionOperationResult result;
      TryReleaseGenerationReservation(
          record.ReservationId,
          string.IsNullOrEmpty(reason) ? "runtime generation finished" : reason,
          out reservation,
          out result);
      record.ReservationId = string.Empty;
    }

    private static string RuntimeGenerationReservationId(string generationKey)
    {
      return "expandnullforge:runtime-generation:" + (generationKey ?? string.Empty);
    }

    private static string RuntimeGenerationOwnerId(string requesterId)
    {
      return string.IsNullOrEmpty(requesterId)
          ? "expandnullforge:runtime-generation"
          : "expandnullforge:runtime-generation:" + requesterId;
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
