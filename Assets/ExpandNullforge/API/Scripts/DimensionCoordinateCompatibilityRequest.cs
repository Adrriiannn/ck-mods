using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionCoordinateCompatibilityRequest
    {
        public readonly Entity Player;
        public readonly string DimensionId;
        public readonly float2 Position;
        public readonly bool PositionIsLocal;
        public readonly bool PreferPlayerCurrentDimension;
        public readonly string OperationId;
        public readonly string Reason;

        public DimensionCoordinateCompatibilityRequest(
            Entity player,
            string dimensionId,
            float2 position,
            bool positionIsLocal,
            bool preferPlayerCurrentDimension,
            string operationId,
            string reason)
        {
            Player = player;
            DimensionId = dimensionId ?? string.Empty;
            Position = position;
            PositionIsLocal = positionIsLocal;
            PreferPlayerCurrentDimension = preferPlayerCurrentDimension;
            OperationId = operationId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
