using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionDiagnosticsService
    {
        event Action<DimensionDiagnosticEntry> DiagnosticEmitted;

        IReadOnlyList<DimensionDiagnosticEntry> GetRecentDiagnostics(int maxCount);

        DimensionRuntimeSnapshot GetRuntimeSnapshot();

        DimensionMultiplayerSnapshot GetMultiplayerSnapshot();

        DimensionDebugSnapshot GetDebugSnapshot(
            Entity player,
            float2 fallbackAbsolutePosition);

        DimensionEntityContextCostSnapshot GetEntityContextCostSnapshot();

        DimensionRegistrySnapshot GetRegistrySnapshot();

        IReadOnlyList<DimensionLoadTicketSnapshot> GetLoadTicketSnapshots();

        IReadOnlyList<DimensionLoadTicketSnapshot> GetLoadTicketSnapshots(
            DimensionLoadTicketSnapshotQuery query);

        bool TryGetLoadTicketSnapshot(
            string ticketId,
            out DimensionLoadTicketSnapshot snapshot);

        IReadOnlyList<DimensionTravelSnapshot> GetTravelSnapshots();

        IReadOnlyList<DimensionTravelSnapshot> GetTravelSnapshots(
            DimensionTravelSnapshotQuery query);

        bool TryGetTravelSnapshot(
            string travelId,
            out DimensionTravelSnapshot snapshot);

        DimensionContentValidationReport ValidateContent(
            DimensionContentValidationRequest request);

        void ClearDiagnostics();
    }
}
