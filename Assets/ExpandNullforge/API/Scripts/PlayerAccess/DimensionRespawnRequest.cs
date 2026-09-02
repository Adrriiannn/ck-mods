using Unity.Entities;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionRespawnRequest
    {
        public readonly Entity Player;
        public readonly string PreferredDimensionId;
        public readonly bool PreferPlayerDimension;
        public readonly bool AllowCheckpointFallback;
        public readonly bool AllowEntryFallback;
        public readonly bool AllowFallbackAnchors;
        public readonly bool AllowOverworldFallback;
        public readonly string Reason;

        public DimensionRespawnRequest(
            Entity player,
            string preferredDimensionId,
            bool preferPlayerDimension,
            bool allowCheckpointFallback,
            bool allowEntryFallback,
            bool allowFallbackAnchors,
            bool allowOverworldFallback,
            string reason)
        {
            Player = player;
            PreferredDimensionId = preferredDimensionId ?? string.Empty;
            PreferPlayerDimension = preferPlayerDimension;
            AllowCheckpointFallback = allowCheckpointFallback;
            AllowEntryFallback = allowEntryFallback;
            AllowFallbackAnchors = allowFallbackAnchors;
            AllowOverworldFallback = allowOverworldFallback;
            Reason = reason ?? string.Empty;
        }

        public static DimensionRespawnRequest Default(Entity player, string reason)
        {
            return new DimensionRespawnRequest(
                player,
                string.Empty,
                true,
                true,
                true,
                true,
                true,
                reason);
        }
    }
}
