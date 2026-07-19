using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeAuthoringOverview
    {
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string DisplayName;
        public readonly int ContentEntryCount;
        public readonly int PreviewEntryCount;
        public readonly int IssueCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly int InfoCount;
        public readonly int ReadyCount;
        public readonly int PartialCount;
        public readonly int MissingCount;
        public readonly int BlockedCount;
        public readonly IReadOnlyList<DimensionAuthoringContentSummaryEntry> ContentEntries;
        public readonly IReadOnlyList<DimensionAuthoringPreviewEntry> PreviewEntries;
        public readonly IReadOnlyList<DimensionAuthoringIssue> Issues;
        public readonly IReadOnlyList<DimensionAuthoringReadinessEntry> ReadinessEntries;
        public readonly IReadOnlyList<DimensionBiomeCustomizerCapabilityStatus> CapabilityStatuses;
        public readonly IReadOnlyList<DimensionBiomeCustomizerGuidanceItem> GuidanceItems;
        public readonly IReadOnlyList<DimensionAuthoringSpatialConflict> SpatialConflicts;

        public DimensionBiomeAuthoringOverview(
            string dimensionId,
            string biomeId,
            string displayName,
            int contentEntryCount,
            int previewEntryCount,
            int issueCount,
            int errorCount,
            int warningCount,
            int infoCount,
            int readyCount,
            int partialCount,
            int missingCount,
            int blockedCount,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries,
            IReadOnlyList<DimensionAuthoringPreviewEntry> previewEntries,
            IReadOnlyList<DimensionAuthoringIssue> issues,
            IReadOnlyList<DimensionAuthoringReadinessEntry> readinessEntries)
            : this(
                dimensionId,
                biomeId,
                displayName,
                contentEntryCount,
                previewEntryCount,
                issueCount,
                errorCount,
                warningCount,
                infoCount,
                readyCount,
                partialCount,
                missingCount,
                blockedCount,
                contentEntries,
                previewEntries,
                issues,
                readinessEntries,
                null)
        {
        }

        public DimensionBiomeAuthoringOverview(
            string dimensionId,
            string biomeId,
            string displayName,
            int contentEntryCount,
            int previewEntryCount,
            int issueCount,
            int errorCount,
            int warningCount,
            int infoCount,
            int readyCount,
            int partialCount,
            int missingCount,
            int blockedCount,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries,
            IReadOnlyList<DimensionAuthoringPreviewEntry> previewEntries,
            IReadOnlyList<DimensionAuthoringIssue> issues,
            IReadOnlyList<DimensionAuthoringReadinessEntry> readinessEntries,
            IReadOnlyList<DimensionBiomeCustomizerCapabilityStatus> capabilityStatuses)
            : this(
                dimensionId,
                biomeId,
                displayName,
                contentEntryCount,
                previewEntryCount,
                issueCount,
                errorCount,
                warningCount,
                infoCount,
                readyCount,
                partialCount,
                missingCount,
                blockedCount,
                contentEntries,
                previewEntries,
                issues,
                readinessEntries,
                capabilityStatuses,
                null)
        {
        }

        public DimensionBiomeAuthoringOverview(
            string dimensionId,
            string biomeId,
            string displayName,
            int contentEntryCount,
            int previewEntryCount,
            int issueCount,
            int errorCount,
            int warningCount,
            int infoCount,
            int readyCount,
            int partialCount,
            int missingCount,
            int blockedCount,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries,
            IReadOnlyList<DimensionAuthoringPreviewEntry> previewEntries,
            IReadOnlyList<DimensionAuthoringIssue> issues,
            IReadOnlyList<DimensionAuthoringReadinessEntry> readinessEntries,
            IReadOnlyList<DimensionBiomeCustomizerCapabilityStatus> capabilityStatuses,
            IReadOnlyList<DimensionBiomeCustomizerGuidanceItem> guidanceItems)
            : this(
                dimensionId,
                biomeId,
                displayName,
                contentEntryCount,
                previewEntryCount,
                issueCount,
                errorCount,
                warningCount,
                infoCount,
                readyCount,
                partialCount,
                missingCount,
                blockedCount,
                contentEntries,
                previewEntries,
                issues,
                readinessEntries,
                capabilityStatuses,
                guidanceItems,
                null)
        {
        }

        public DimensionBiomeAuthoringOverview(
            string dimensionId,
            string biomeId,
            string displayName,
            int contentEntryCount,
            int previewEntryCount,
            int issueCount,
            int errorCount,
            int warningCount,
            int infoCount,
            int readyCount,
            int partialCount,
            int missingCount,
            int blockedCount,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries,
            IReadOnlyList<DimensionAuthoringPreviewEntry> previewEntries,
            IReadOnlyList<DimensionAuthoringIssue> issues,
            IReadOnlyList<DimensionAuthoringReadinessEntry> readinessEntries,
            IReadOnlyList<DimensionBiomeCustomizerCapabilityStatus> capabilityStatuses,
            IReadOnlyList<DimensionBiomeCustomizerGuidanceItem> guidanceItems,
            IReadOnlyList<DimensionAuthoringSpatialConflict> spatialConflicts)
        {
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ContentEntryCount = contentEntryCount < 0 ? 0 : contentEntryCount;
            PreviewEntryCount = previewEntryCount < 0 ? 0 : previewEntryCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            InfoCount = infoCount < 0 ? 0 : infoCount;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            PartialCount = partialCount < 0 ? 0 : partialCount;
            MissingCount = missingCount < 0 ? 0 : missingCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            ContentEntries = contentEntries ?? new List<DimensionAuthoringContentSummaryEntry>();
            PreviewEntries = previewEntries ?? new List<DimensionAuthoringPreviewEntry>();
            Issues = issues ?? new List<DimensionAuthoringIssue>();
            ReadinessEntries = readinessEntries ?? new List<DimensionAuthoringReadinessEntry>();
            CapabilityStatuses = capabilityStatuses ?? new List<DimensionBiomeCustomizerCapabilityStatus>();
            GuidanceItems = guidanceItems ?? new List<DimensionBiomeCustomizerGuidanceItem>();
            SpatialConflicts = spatialConflicts ?? new List<DimensionAuthoringSpatialConflict>();
        }
    }
}
