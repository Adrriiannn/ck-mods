using System;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionPlayerService
    {
        event Action<DimensionChangedEvent> PlayerDimensionChanged;

        bool TryGetPlayerContext(Entity player, out DimensionContext context);

        bool TryGetPersistedPlayerContext(Entity player, out DimensionContext context);

        bool TryGetPersistedPlayerContext(string playerId, out DimensionContext context);

        DimensionResolveResult ResolvePlayerLocalTarget(Entity player, float2 localPosition);
    }
}
