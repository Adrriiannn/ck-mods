using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Entities;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool IsLoadTicketReady(DimensionLoadTicket ticket)
    {
      return ticket.IsValid &&
          (ticket.State == DimensionLoadState.Resident ||
           ticket.State == DimensionLoadState.Simulating);
    }

    public DimensionLoadTicket RequestLoad(DimensionLoadRequest request)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(request.DimensionId, out definition))
      {
        return DimensionLoadTicket.Invalid("The target dimension is not registered.");
      }

      if (!request.KeepTilesResident && !request.EnableSimulation)
      {
        return DimensionLoadTicket.Invalid("A load request must keep tiles resident, enable simulation, or both.");
      }

      if (request.KeepTilesResident && !definition.HasCapability(DimensionCapabilityFlags.AreaLoading))
      {
        return DimensionLoadTicket.Invalid("The target dimension does not allow area residency loading.");
      }

      if (request.EnableSimulation && !definition.HasCapability(DimensionCapabilityFlags.SimulationLoading))
      {
        return DimensionLoadTicket.Invalid("The target dimension does not allow simulation loading.");
      }

      if (!IsServerWorldAvailable())
      {
        return DimensionLoadTicket.Invalid("The server world is not available.");
      }

      string boundsError;
      if (!IsValidLoadBounds(request.LocalBounds, out boundsError))
      {
        return DimensionLoadTicket.Invalid(boundsError);
      }

      DimensionArea area;
      if (!TryGetArea(request.DimensionId, request.LocalBounds, out area))
      {
        return DimensionLoadTicket.Invalid("The requested area is outside the target dimension bounds.");
      }

      string budgetError;
      if (!IsRuntimeLoadBudgetAvailable(request.LocalBounds, out budgetError))
      {
        AddDiagnostic(DimensionDiagnosticSeverity.Warning, request.DimensionId, budgetError);
        return DimensionLoadTicket.Invalid(budgetError);
      }

      string ticketId = CreateLoadTicketId();
      RuntimeLoadRecord record = new RuntimeLoadRecord
      {
        TicketId = ticketId,
        TicketHash = HashTicketId(ticketId),
        Area = area,
        KeepTilesResident = request.KeepTilesResident,
        EnableSimulation = request.EnableSimulation,
        TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : DefaultLoadTimeoutSeconds,
        Anchor = Entity.Null,
        CreatedAt = Time.realtimeSinceStartupAsDouble,
        SubMapsObservedAt = 0,
        ImmediateLoadEnabled = request.KeepTilesResident,
        State = DimensionLoadState.Requested,
        Message = string.IsNullOrEmpty(request.Reason) ? "Runtime load requested." : request.Reason
      };

      PopulateRequiredSubMaps(area.AbsoluteBounds, record.RequiredSubMaps);
      Entity anchor = CreateRuntimeLoadAnchor(record);
      if (anchor == Entity.Null)
      {
        AddDiagnostic(DimensionDiagnosticSeverity.Error, request.DimensionId, "Failed to create runtime load anchor.");
        return DimensionLoadTicket.Invalid("The runtime load anchor could not be created.");
      }

      record.Anchor = anchor;
      record.State = DimensionLoadState.Loading;
      record.Message = "Runtime load anchor created.";
      runtimeLoadRecords[ticketId] = record;
      runtimeLoadTopologyDirty = true;
      nextRuntimeLoadReconcileAt = 0;
      UpdateLoadTicket(record);
      AddDiagnostic(DimensionDiagnosticSeverity.Info, request.DimensionId, "Runtime load ticket created: " + ticketId + ".");
      return loadTickets[ticketId];
    }

    public bool TryGetLoadStatus(string ticketId, out DimensionLoadTicket ticket)
    {
      if (string.IsNullOrEmpty(ticketId))
      {
        ticket = DimensionLoadTicket.Invalid("A ticket id is required.");
        return false;
      }

      ReconcileRuntimeLoadingIfDue(Time.realtimeSinceStartupAsDouble);
      return loadTickets.TryGetValue(ticketId, out ticket);
    }

    public DimensionOperationResult ReleaseLoadTicket(string ticketId)
    {
      if (string.IsNullOrEmpty(ticketId))
      {
        return DimensionOperationResult.Failed("load-ticket-id-empty", "A load ticket id is required.");
      }

      if (!loadTickets.ContainsKey(ticketId) && !runtimeLoadRecords.ContainsKey(ticketId))
      {
        return DimensionOperationResult.Failed("load-ticket-not-found", "No load ticket with that id is registered.");
      }

      RuntimeLoadRecord record;
      if (runtimeLoadRecords.TryGetValue(ticketId, out record))
      {
        record.State = DimensionLoadState.Released;
        record.Message = "Runtime load ticket released.";
        UpdateLoadTicket(record);
        DestroyRuntimeLoadAnchor(record);
        runtimeLoadRecords.Remove(ticketId);
        runtimeLoadTopologyDirty = true;
        nextRuntimeLoadReconcileAt = 0;
      }

      loadTickets.Remove(ticketId);
      RefreshMergedSimulationRegions();
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Runtime load ticket released: " + ticketId + ".");
      return DimensionOperationResult.Ok();
    }

    public IReadOnlyList<DimensionLoadTicketSnapshot> GetLoadTicketSnapshots()
    {
      return GetLoadTicketSnapshots(default(DimensionLoadTicketSnapshotQuery));
    }

    public IReadOnlyList<DimensionLoadTicketSnapshot> GetLoadTicketSnapshots(
        DimensionLoadTicketSnapshotQuery query)
    {
      List<DimensionLoadTicketSnapshot> result =
          new List<DimensionLoadTicketSnapshot>(runtimeLoadRecords.Count);

      foreach (RuntimeLoadRecord record in runtimeLoadRecords.Values)
      {
        DimensionLoadTicketSnapshot snapshot = CreateLoadTicketSnapshot(record);
        if (LoadTicketSnapshotMatchesQuery(snapshot, query))
        {
          result.Add(snapshot);
        }
      }

      result.Sort(CompareLoadTicketSnapshots);
      return result;
    }

    public bool TryGetLoadTicketSnapshot(
        string ticketId,
        out DimensionLoadTicketSnapshot snapshot)
    {
      snapshot = default(DimensionLoadTicketSnapshot);
      if (string.IsNullOrEmpty(ticketId))
      {
        return false;
      }

      RuntimeLoadRecord record;
      if (!runtimeLoadRecords.TryGetValue(ticketId, out record))
      {
        return false;
      }

      snapshot = CreateLoadTicketSnapshot(record);
      return true;
    }

    private DimensionLoadTicketSnapshot CreateLoadTicketSnapshot(RuntimeLoadRecord record)
    {
      DimensionLoadTicket ticket;
      DimensionLoadState state = record.State;
      string message = record.Message;
      if (loadTickets.TryGetValue(record.TicketId, out ticket))
      {
        state = ticket.State;
        message = ticket.Message;
      }

      return new DimensionLoadTicketSnapshot(
          record.TicketId,
          record.Area.DimensionId,
          record.Area.LocalBounds,
          record.Area.AbsoluteBounds,
          record.KeepTilesResident,
          record.EnableSimulation,
          state,
          message,
          record.CreatedAt,
          record.SubMapsObservedAt,
          record.RequiredSubMaps.Count);
    }

    private bool LoadTicketSnapshotMatchesQuery(
        DimensionLoadTicketSnapshot snapshot,
        DimensionLoadTicketSnapshotQuery query)
    {
      if (!string.IsNullOrEmpty(query.TicketId) &&
          !string.Equals(snapshot.TicketId, query.TicketId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(snapshot.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.MatchLocalBounds && !BoundsEqual(snapshot.LocalBounds, query.LocalBounds))
      {
        return false;
      }

      if (query.MatchState && snapshot.State != query.State)
      {
        return false;
      }

      if (query.MatchKeepTilesResident &&
          snapshot.KeepTilesResident != query.KeepTilesResident)
      {
        return false;
      }

      if (query.MatchEnableSimulation &&
          snapshot.EnableSimulation != query.EnableSimulation)
      {
        return false;
      }

      return true;
    }

    private static int CompareLoadTicketSnapshots(
        DimensionLoadTicketSnapshot left,
        DimensionLoadTicketSnapshot right)
    {
      int created = left.CreatedAtSeconds.CompareTo(right.CreatedAtSeconds);
      if (created != 0)
      {
        return created;
      }

      return string.Compare(left.TicketId, right.TicketId, StringComparison.Ordinal);
    }
  }
}
