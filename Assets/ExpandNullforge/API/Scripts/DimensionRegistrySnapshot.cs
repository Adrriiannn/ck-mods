namespace ExpandNullforge.Api
{
    public readonly struct DimensionRegistrySnapshot
    {
        public readonly bool IsLoaded;
        public readonly bool IsDirty;
        public readonly bool HasPendingFlush;
        public readonly int SchemaVersion;
        public readonly long Generation;
        public readonly ulong Revision;
        public readonly int ConsecutiveFlushFailures;
        public readonly string WorldKey;
        public readonly int DimensionCount;
        public readonly int DimensionSlotCount;
        public readonly int PlayerStateCount;
        public readonly int PlayerVisitCount;
        public readonly int PortalCount;
        public readonly int MarkerCount;
        public readonly int AnchorCount;
        public readonly int SceneCount;
        public readonly int ProgressFlagCount;
        public readonly int GeneratedAreaCount;
        public readonly int ContentOwnershipBindingCount;

        public DimensionRegistrySnapshot(
            bool isLoaded,
            bool isDirty,
            bool hasPendingFlush,
            int schemaVersion,
            long generation,
            ulong revision,
            int consecutiveFlushFailures,
            string worldKey,
            int dimensionCount,
            int dimensionSlotCount,
            int playerStateCount,
            int playerVisitCount,
            int portalCount,
            int markerCount,
            int anchorCount,
            int sceneCount,
            int progressFlagCount,
            int generatedAreaCount,
            int contentOwnershipBindingCount)
        {
            IsLoaded = isLoaded;
            IsDirty = isDirty;
            HasPendingFlush = hasPendingFlush;
            SchemaVersion = schemaVersion;
            Generation = generation;
            Revision = revision;
            ConsecutiveFlushFailures = consecutiveFlushFailures;
            WorldKey = worldKey ?? string.Empty;
            DimensionCount = dimensionCount;
            DimensionSlotCount = dimensionSlotCount;
            PlayerStateCount = playerStateCount;
            PlayerVisitCount = playerVisitCount;
            PortalCount = portalCount;
            MarkerCount = markerCount;
            AnchorCount = anchorCount;
            SceneCount = sceneCount;
            ProgressFlagCount = progressFlagCount;
            GeneratedAreaCount = generatedAreaCount;
            ContentOwnershipBindingCount = contentOwnershipBindingCount;
        }
    }
}
