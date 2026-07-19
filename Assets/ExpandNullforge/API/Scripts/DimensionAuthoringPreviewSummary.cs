using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringPreviewSummary
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly string DisplayName;
        public readonly DimensionBounds PlayableLocalBounds;
        public readonly int CoordinateShellPaddingTiles;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly int InfoCount;
        public readonly IReadOnlyList<DimensionAuthoringContentSummaryEntry> ContentEntries;
        public readonly IReadOnlyList<DimensionAuthoringPreviewEntry> Entries;
        public readonly IReadOnlyList<DimensionAuthoringIssue> Issues;

        public DimensionAuthoringPreviewSummary(
            bool success,
            string code,
            string message,
            string dimensionId,
            string displayName,
            DimensionBounds playableLocalBounds,
            int coordinateShellPaddingTiles,
            int errorCount,
            int warningCount,
            int infoCount,
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries,
            IReadOnlyList<DimensionAuthoringIssue> issues)
            : this(
                success,
                code,
                message,
                dimensionId,
                displayName,
                playableLocalBounds,
                coordinateShellPaddingTiles,
                errorCount,
                warningCount,
                infoCount,
                new List<DimensionAuthoringContentSummaryEntry>(),
                entries,
                issues)
        {
        }

        public DimensionAuthoringPreviewSummary(
            bool success,
            string code,
            string message,
            string dimensionId,
            string displayName,
            DimensionBounds playableLocalBounds,
            int coordinateShellPaddingTiles,
            int errorCount,
            int warningCount,
            int infoCount,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries,
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries,
            IReadOnlyList<DimensionAuthoringIssue> issues)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PlayableLocalBounds = playableLocalBounds;
            CoordinateShellPaddingTiles = coordinateShellPaddingTiles;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            InfoCount = infoCount;
            ContentEntries = contentEntries;
            Entries = entries;
            Issues = issues;
        }
    }
}
