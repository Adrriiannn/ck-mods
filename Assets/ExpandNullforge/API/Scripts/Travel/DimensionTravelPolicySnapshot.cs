namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelPolicySnapshot
    {
        public readonly int TravelPreloadSideTiles;
        public readonly float TravelLoadTimeoutSeconds;
        public readonly float TravelArrivalTimeoutSeconds;
        public readonly float TravelArrivalDistanceSquared;
        public readonly float TravelTeleportRetryIntervalSeconds;
        public readonly float DefaultLoadTimeoutSeconds;
        public readonly float GenerationLoadTimeoutSeconds;
        public readonly float RuntimeLoadReconcileIntervalSeconds;
        public readonly double RuntimeGenerationTickIntervalSeconds;
        public readonly double PlayerContextTrackIntervalSeconds;
        public readonly int ReferenceStarterAreaSideTiles;

        public DimensionTravelPolicySnapshot(
            int travelPreloadSideTiles,
            float travelLoadTimeoutSeconds,
            float travelArrivalTimeoutSeconds,
            float travelArrivalDistanceSquared,
            float travelTeleportRetryIntervalSeconds,
            float defaultLoadTimeoutSeconds,
            float generationLoadTimeoutSeconds,
            float runtimeLoadReconcileIntervalSeconds,
            double runtimeGenerationTickIntervalSeconds,
            double playerContextTrackIntervalSeconds,
            int referenceStarterAreaSideTiles)
        {
            TravelPreloadSideTiles = travelPreloadSideTiles;
            TravelLoadTimeoutSeconds = travelLoadTimeoutSeconds;
            TravelArrivalTimeoutSeconds = travelArrivalTimeoutSeconds;
            TravelArrivalDistanceSquared = travelArrivalDistanceSquared;
            TravelTeleportRetryIntervalSeconds = travelTeleportRetryIntervalSeconds;
            DefaultLoadTimeoutSeconds = defaultLoadTimeoutSeconds;
            GenerationLoadTimeoutSeconds = generationLoadTimeoutSeconds;
            RuntimeLoadReconcileIntervalSeconds = runtimeLoadReconcileIntervalSeconds;
            RuntimeGenerationTickIntervalSeconds = runtimeGenerationTickIntervalSeconds;
            PlayerContextTrackIntervalSeconds = playerContextTrackIntervalSeconds;
            ReferenceStarterAreaSideTiles = referenceStarterAreaSideTiles;
        }
    }
}
