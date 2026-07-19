namespace ExpandNullforge.Api
{
    public readonly struct DimensionMultiplayerSnapshot
    {
        public readonly bool HasServerWorld;
        public readonly bool HasClientWorld;
        public readonly bool ServerQueriesCreated;
        public readonly int TrackedPlayerContextCount;
        public readonly int ObservedPlayerCount;
        public readonly int PendingTravelCount;
        public readonly int LoadTicketCount;
        public readonly int RuntimeLoadRecordCount;
        public readonly bool RegistryLoaded;
        public readonly string WorldKey;

        public DimensionMultiplayerSnapshot(
            bool hasServerWorld,
            bool hasClientWorld,
            bool serverQueriesCreated,
            int trackedPlayerContextCount,
            int observedPlayerCount,
            int pendingTravelCount,
            int loadTicketCount,
            int runtimeLoadRecordCount,
            bool registryLoaded,
            string worldKey)
        {
            HasServerWorld = hasServerWorld;
            HasClientWorld = hasClientWorld;
            ServerQueriesCreated = serverQueriesCreated;
            TrackedPlayerContextCount = trackedPlayerContextCount;
            ObservedPlayerCount = observedPlayerCount;
            PendingTravelCount = pendingTravelCount;
            LoadTicketCount = loadTicketCount;
            RuntimeLoadRecordCount = runtimeLoadRecordCount;
            RegistryLoaded = registryLoaded;
            WorldKey = worldKey ?? string.Empty;
        }
    }
}
