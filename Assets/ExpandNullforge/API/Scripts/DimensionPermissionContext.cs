using Unity.Entities;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionPermissionContext
    {
        public readonly Entity Player;
        public readonly string PlayerId;
        public readonly string DimensionId;
        public readonly string OperationId;
        public readonly DimensionPermissionKind Kind;
        public readonly bool IsServerSide;
        public readonly string Reason;

        public DimensionPermissionContext(
            Entity player,
            string playerId,
            string dimensionId,
            string operationId,
            DimensionPermissionKind kind,
            bool isServerSide,
            string reason)
        {
            Player = player;
            PlayerId = playerId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            Kind = kind;
            IsServerSide = isServerSide;
            Reason = reason ?? string.Empty;
        }
    }
}
