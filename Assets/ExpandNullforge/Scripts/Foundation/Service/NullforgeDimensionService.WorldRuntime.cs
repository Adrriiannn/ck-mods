using ExpandNullforge.Api;
using Unity.Entities;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public void SetServerWorld(World world)
    {
      DestroyAllRuntimeLoadAnchors();
      ResetRuntimeLoadState();
      serverWorld = world;
      CreateServerQueries();
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Server world attached to dimension service.");
    }

    public void SetClientWorld(World world)
    {
      clientWorld = world;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Client world attached to dimension service.");
    }

    public void ClearServerWorld()
    {
      MarkAllRuntimeGenerationFailed("The server world was detached before generation finished.");
      MarkAllPendingTravelFailed("The server world was detached before travel finished.");
      ResetRuntimeGenerationState();
      DestroyAllRuntimeLoadAnchors();
      ResetRuntimeLoadState();
      serverWorld = null;
      serverQueriesCreated = false;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Server world detached from dimension service.");
    }

    public void UpdateRuntimeLoading()
    {
      if (runtimeLoadRecords.Count == 0)
      {
        return;
      }

      ReconcileRuntimeLoadingIfDue(Time.realtimeSinceStartupAsDouble);
    }

    public void UpdateRuntimeTravel()
    {
      if (pendingTravelByPlayerId.Count == 0)
      {
        return;
      }

      ProcessPendingTravel(Time.realtimeSinceStartupAsDouble);
    }

    public void UpdateRuntimeGeneration()
    {
      if (runtimeGenerationRecords.Count == 0)
      {
        return;
      }

      double now = Time.realtimeSinceStartupAsDouble;
      if (now < nextRuntimeGenerationTickAt)
      {
        return;
      }

      nextRuntimeGenerationTickAt = now + RuntimeGenerationTickIntervalSeconds;
      ProcessRuntimeGeneration(now);
    }

    public void UpdateRuntimePlayerContexts()
    {
      double now = Time.realtimeSinceStartupAsDouble;
      if (now < nextPlayerContextTrackAt)
      {
        return;
      }

      nextPlayerContextTrackAt = now + GetPlayerContextTrackIntervalSeconds();
      TrackServerPlayerContexts();
    }

    public void ClearClientWorld()
    {
      clientWorld = null;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Client world detached from dimension service.");
    }

    private void ReconcileRuntimeLoadingIfDue(double now)
    {
      if (runtimeLoadRecords.Count == 0 || now < nextRuntimeLoadReconcileAt)
      {
        return;
      }

      if (CanUseSettledRuntimeLoadMaintenance())
      {
        nextRuntimeLoadReconcileAt = now + SettledRuntimeLoadHealthIntervalSeconds;
        MaintainSettledRuntimeLoadRecords();
        return;
      }

      nextRuntimeLoadReconcileAt = now + RuntimeLoadReconcileIntervalSeconds;
      ReconcileRuntimeLoadRecords(now);
    }
  }
}
