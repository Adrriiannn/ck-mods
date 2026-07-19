namespace ExpandNullforge.Api
{
    public readonly struct DimensionPersistenceHealthSnapshot
    {
        public readonly DimensionRegistrySnapshot Registry;
        public readonly bool LastFlushHealthy;
        public readonly bool ReadyForWorldUnload;
        public readonly string Code;
        public readonly string Message;

        public DimensionPersistenceHealthSnapshot(
            DimensionRegistrySnapshot registry,
            bool lastFlushHealthy,
            bool readyForWorldUnload,
            string code,
            string message)
        {
            Registry = registry;
            LastFlushHealthy = lastFlushHealthy;
            ReadyForWorldUnload = readyForWorldUnload;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
