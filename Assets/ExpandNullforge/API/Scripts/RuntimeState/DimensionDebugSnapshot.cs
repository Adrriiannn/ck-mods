using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionDebugSnapshot
    {
        public readonly bool IsKnown;
        public readonly string DimensionId;
        public readonly float2 AbsolutePosition;
        public readonly float2 LocalPosition;
        public readonly string ZoneId;
        public readonly string ZoneName;
        public readonly bool IsCurrentTileGenerated;
        public readonly int PendingTravelCount;
        public readonly int RuntimeLoadRecordCount;
        public readonly ulong RegistryRevision;
        public readonly bool PersistenceReadyForUnload;
        public readonly string Code;
        public readonly string Message;

        public DimensionDebugSnapshot(
            bool isKnown,
            string dimensionId,
            float2 absolutePosition,
            float2 localPosition,
            string zoneId,
            string zoneName,
            bool isCurrentTileGenerated,
            int pendingTravelCount,
            int runtimeLoadRecordCount,
            ulong registryRevision,
            bool persistenceReadyForUnload,
            string code,
            string message)
        {
            IsKnown = isKnown;
            DimensionId = dimensionId ?? string.Empty;
            AbsolutePosition = absolutePosition;
            LocalPosition = localPosition;
            ZoneId = zoneId ?? string.Empty;
            ZoneName = zoneName ?? string.Empty;
            IsCurrentTileGenerated = isCurrentTileGenerated;
            PendingTravelCount = pendingTravelCount;
            RuntimeLoadRecordCount = runtimeLoadRecordCount;
            RegistryRevision = registryRevision;
            PersistenceReadyForUnload = persistenceReadyForUnload;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
