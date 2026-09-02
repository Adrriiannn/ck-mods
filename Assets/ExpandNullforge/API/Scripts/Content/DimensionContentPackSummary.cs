using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentPackSummary
    {
        public readonly string ContentPackId;
        public readonly bool ContentPackExists;
        public readonly DimensionContentPackDefinition ContentPack;
        public readonly DimensionContentPackReadinessResult Readiness;
        public readonly int OwnedRecordCount;
        public readonly int OrphanedRecordCount;
        public readonly int ValidationErrorCount;
        public readonly int ValidationWarningCount;
        public readonly IReadOnlyList<DimensionContentPackRecordCount> RecordCounts;

        public DimensionContentPackSummary(
            string contentPackId,
            bool contentPackExists,
            DimensionContentPackDefinition contentPack,
            DimensionContentPackReadinessResult readiness,
            int ownedRecordCount,
            int orphanedRecordCount,
            int validationErrorCount,
            int validationWarningCount,
            IReadOnlyList<DimensionContentPackRecordCount> recordCounts)
        {
            ContentPackId = contentPackId ?? string.Empty;
            ContentPackExists = contentPackExists;
            ContentPack = contentPack;
            Readiness = readiness;
            OwnedRecordCount = ownedRecordCount;
            OrphanedRecordCount = orphanedRecordCount;
            ValidationErrorCount = validationErrorCount;
            ValidationWarningCount = validationWarningCount;
            RecordCounts = recordCounts ?? new List<DimensionContentPackRecordCount>();
        }
    }
}
