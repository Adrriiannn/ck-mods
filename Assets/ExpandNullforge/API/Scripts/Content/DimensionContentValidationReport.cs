using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentValidationReport
    {
        public readonly bool Success;
        public readonly bool HasErrors;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly IReadOnlyList<DimensionContentValidationIssue> Issues;

        public DimensionContentValidationReport(
            bool success,
            bool hasErrors,
            int errorCount,
            int warningCount,
            IReadOnlyList<DimensionContentValidationIssue> issues)
        {
            Success = success;
            HasErrors = hasErrors;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Issues = issues ?? new List<DimensionContentValidationIssue>();
        }
    }
}
