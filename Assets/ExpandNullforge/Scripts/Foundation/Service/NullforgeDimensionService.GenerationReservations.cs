using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Holding an area while it is being generated so nothing else claims it.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
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
  }
}
