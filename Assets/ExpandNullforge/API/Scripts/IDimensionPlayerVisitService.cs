using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionPlayerVisitService
    {
        event Action<DimensionPlayerVisitChangedEvent> PlayerVisitChanged;

        IReadOnlyList<DimensionPlayerVisitRecord> GetPlayerVisits(string playerId);

        bool TryGetPlayerVisit(
            string playerId,
            string dimensionId,
            out DimensionPlayerVisitRecord visit);

        bool TryUpsertPlayerVisit(
            string playerId,
            string dimensionId,
            float2 localPosition,
            string reason,
            out DimensionOperationResult result);

        bool TryRemovePlayerVisit(
            string playerId,
            string dimensionId,
            string reason,
            out DimensionOperationResult result);
    }
}
