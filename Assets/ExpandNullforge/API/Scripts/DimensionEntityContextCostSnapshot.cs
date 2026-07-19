namespace ExpandNullforge.Api
{
    public readonly struct DimensionEntityContextCostSnapshot
    {
        public readonly int DimensionCount;
        public readonly int NonOverworldDimensionCount;
        public readonly int MaxDimensionBoundsChecksPerLookup;
        public readonly int ServerOnlyWorldProbeCount;
        public readonly int ClientOnlyWorldProbeCount;
        public readonly int BestAvailableWorldProbeCount;
        public readonly bool ServerWorldAttached;
        public readonly bool ClientWorldAttached;
        public readonly bool EnumeratesEntities;
        public readonly string PositionComponentName;
        public readonly string Code;
        public readonly string Message;

        public DimensionEntityContextCostSnapshot(
            int dimensionCount,
            int nonOverworldDimensionCount,
            int maxDimensionBoundsChecksPerLookup,
            int serverOnlyWorldProbeCount,
            int clientOnlyWorldProbeCount,
            int bestAvailableWorldProbeCount,
            bool serverWorldAttached,
            bool clientWorldAttached,
            bool enumeratesEntities,
            string positionComponentName,
            string code,
            string message)
        {
            DimensionCount = dimensionCount;
            NonOverworldDimensionCount = nonOverworldDimensionCount;
            MaxDimensionBoundsChecksPerLookup = maxDimensionBoundsChecksPerLookup;
            ServerOnlyWorldProbeCount = serverOnlyWorldProbeCount;
            ClientOnlyWorldProbeCount = clientOnlyWorldProbeCount;
            BestAvailableWorldProbeCount = bestAvailableWorldProbeCount;
            ServerWorldAttached = serverWorldAttached;
            ClientWorldAttached = clientWorldAttached;
            EnumeratesEntities = enumeratesEntities;
            PositionComponentName = positionComponentName ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
