namespace ExpandNullforge.Api
{
    public readonly struct DimensionLoadRequest
    {
        public readonly string RequesterId;
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly bool KeepTilesResident;
        public readonly bool EnableSimulation;
        public readonly float TimeoutSeconds;
        public readonly string Reason;

        public DimensionLoadRequest(
            string requesterId,
            string dimensionId,
            DimensionBounds localBounds,
            bool keepTilesResident,
            bool enableSimulation,
            float timeoutSeconds,
            string reason)
        {
            RequesterId = requesterId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            KeepTilesResident = keepTilesResident;
            EnableSimulation = enableSimulation;
            TimeoutSeconds = timeoutSeconds;
            Reason = reason ?? string.Empty;
        }
    }
}
