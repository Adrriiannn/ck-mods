using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Loading;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// The reservation a runtime generation holds, and letting a stale one go.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
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
  }
}
