using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringReadinessReport
    {
        public readonly bool Ready;
        public readonly string Code;
        public readonly string Message;
        public readonly int ReadyCount;
        public readonly int PartialCount;
        public readonly int MissingCount;
        public readonly int BlockedCount;
        public readonly IReadOnlyList<DimensionAuthoringReadinessEntry> Entries;

        public DimensionAuthoringReadinessReport(
            bool ready,
            string code,
            string message,
            int readyCount,
            int partialCount,
            int missingCount,
            int blockedCount,
            IReadOnlyList<DimensionAuthoringReadinessEntry> entries)
        {
            Ready = ready;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            PartialCount = partialCount < 0 ? 0 : partialCount;
            MissingCount = missingCount < 0 ? 0 : missingCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            Entries = entries ?? new List<DimensionAuthoringReadinessEntry>();
        }
    }
}
