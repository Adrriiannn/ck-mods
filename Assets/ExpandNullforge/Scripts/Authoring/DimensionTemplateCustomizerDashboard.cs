using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateCustomizerDashboard
    {
        public DimensionTemplateCustomizerDashboard(
            string dimensionId,
            string displayName,
            string code,
            string message,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            int biomeCount,
            int contentEntryCount,
            int previewEntryCount,
            int issueCount,
            int errorCount,
            int warningCount,
            int infoCount,
            int guidanceCount,
            int blockingGuidanceCount,
            int runtimeBlockingGuidanceCount,
            int spatialConflictCount,
            int spatialConflictErrorCount,
            int spatialConflictWarningCount,
            IReadOnlyList<DimensionBiomeCustomizerGuidanceItem> guidanceItems)
        {
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ReadyForManifestExport = readyForManifestExport;
            ReadyForRuntimeGeneration = readyForRuntimeGeneration;
            BiomeCount = biomeCount < 0 ? 0 : biomeCount;
            ContentEntryCount = contentEntryCount < 0 ? 0 : contentEntryCount;
            PreviewEntryCount = previewEntryCount < 0 ? 0 : previewEntryCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            InfoCount = infoCount < 0 ? 0 : infoCount;
            GuidanceCount = guidanceCount < 0 ? 0 : guidanceCount;
            BlockingGuidanceCount = blockingGuidanceCount < 0 ? 0 : blockingGuidanceCount;
            RuntimeBlockingGuidanceCount = runtimeBlockingGuidanceCount < 0 ? 0 : runtimeBlockingGuidanceCount;
            SpatialConflictCount = spatialConflictCount < 0 ? 0 : spatialConflictCount;
            SpatialConflictErrorCount = spatialConflictErrorCount < 0 ? 0 : spatialConflictErrorCount;
            SpatialConflictWarningCount = spatialConflictWarningCount < 0 ? 0 : spatialConflictWarningCount;
            GuidanceItems = guidanceItems ?? new List<DimensionBiomeCustomizerGuidanceItem>();
        }

        public string DimensionId { get; private set; }

        public string DisplayName { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool ReadyForManifestExport { get; private set; }

        public bool ReadyForRuntimeGeneration { get; private set; }

        public int BiomeCount { get; private set; }

        public int ContentEntryCount { get; private set; }

        public int PreviewEntryCount { get; private set; }

        public int IssueCount { get; private set; }

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public int InfoCount { get; private set; }

        public int GuidanceCount { get; private set; }

        public int BlockingGuidanceCount { get; private set; }

        public int RuntimeBlockingGuidanceCount { get; private set; }

        public int SpatialConflictCount { get; private set; }

        public int SpatialConflictErrorCount { get; private set; }

        public int SpatialConflictWarningCount { get; private set; }

        public IReadOnlyList<DimensionBiomeCustomizerGuidanceItem> GuidanceItems { get; private set; }
    }

    public static class DimensionTemplateCustomizerDashboardUtility
    {
        public static DimensionTemplateCustomizerDashboard Build(
            DimensionTemplateStarterAssessment assessment,
            IReadOnlyList<DimensionBiomeAuthoringOverview> biomeOverviews)
        {
            DimensionTemplateStarterGraph graph = assessment == null ? null : assessment.Graph;
            DimensionTemplateAsset dimension = graph == null ? null : graph.Dimension;
            string dimensionId = dimension == null ? string.Empty : dimension.DimensionId;
            string displayName = dimension == null ? string.Empty : dimension.DisplayName;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = dimensionId;
            }

            int biomeCount = CountBiomes(biomeOverviews);
            int contentEntries = 0;
            int previewEntries = 0;
            int issues = 0;
            int errors = 0;
            int warnings = 0;
            int infos = 0;
            int blockers = 0;
            int runtimeBlockers = 0;
            int spatialConflicts = 0;
            int spatialConflictErrors = 0;
            int spatialConflictWarnings = 0;
            List<DimensionBiomeCustomizerGuidanceItem> guidance =
                new List<DimensionBiomeCustomizerGuidanceItem>();

            if (biomeOverviews != null)
            {
                for (int i = 0; i < biomeOverviews.Count; i++)
                {
                    DimensionBiomeAuthoringOverview overview = biomeOverviews[i];
                    contentEntries += overview.ContentEntryCount;
                    previewEntries += overview.PreviewEntryCount;
                    issues += overview.IssueCount;
                    errors += overview.ErrorCount;
                    warnings += overview.WarningCount;
                    infos += overview.InfoCount;
                    CollectGuidance(overview.GuidanceItems, guidance, ref blockers, ref runtimeBlockers);
                    CountSpatialConflicts(
                        overview.SpatialConflicts,
                        ref spatialConflicts,
                        ref spatialConflictErrors,
                        ref spatialConflictWarnings);
                }
            }

            guidance.Sort(CompareGuidance);

            bool readyForExport = assessment != null && assessment.ReadyForManifestExport;
            bool readyForRuntime = assessment != null && assessment.ReadyForRuntimeGeneration;
            string code = ResolveCode(assessment, readyForExport, readyForRuntime, blockers, runtimeBlockers);
            string message = ResolveMessage(assessment, readyForExport, readyForRuntime, blockers, runtimeBlockers);

            return new DimensionTemplateCustomizerDashboard(
                dimensionId,
                displayName,
                code,
                message,
                readyForExport,
                readyForRuntime,
                biomeCount,
                contentEntries,
                previewEntries,
                issues,
                errors,
                warnings,
                infos,
                guidance.Count,
                blockers,
                runtimeBlockers,
                spatialConflicts,
                spatialConflictErrors,
                spatialConflictWarnings,
                guidance);
        }

        private static int CountBiomes(
            IReadOnlyList<DimensionBiomeAuthoringOverview> biomeOverviews)
        {
            return biomeOverviews == null ? 0 : biomeOverviews.Count;
        }

        private static void CollectGuidance(
            IReadOnlyList<DimensionBiomeCustomizerGuidanceItem> source,
            List<DimensionBiomeCustomizerGuidanceItem> target,
            ref int blockers,
            ref int runtimeBlockers)
        {
            if (source == null || target == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                DimensionBiomeCustomizerGuidanceItem item = source[i];
                target.Add(item);

                if (item.BlocksExport)
                {
                    blockers++;
                }

                if (item.BlocksRuntimeGeneration)
                {
                    runtimeBlockers++;
                }
            }
        }

        private static void CountSpatialConflicts(
            IReadOnlyList<DimensionAuthoringSpatialConflict> conflicts,
            ref int total,
            ref int errors,
            ref int warnings)
        {
            if (conflicts == null)
            {
                return;
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                total++;
                DimensionAuthoringSeverity severity = conflicts[i].Severity;
                if (severity == DimensionAuthoringSeverity.Error)
                {
                    errors++;
                }
                else if (severity == DimensionAuthoringSeverity.Warning)
                {
                    warnings++;
                }
            }
        }

        private static string ResolveCode(
            DimensionTemplateStarterAssessment assessment,
            bool readyForExport,
            bool readyForRuntime,
            int blockers,
            int runtimeBlockers)
        {
            if (assessment == null)
            {
                return "workspace-missing";
            }

            if (blockers > 0)
            {
                return "blocked";
            }

            if (!readyForExport)
            {
                return string.IsNullOrEmpty(assessment.Code) ? "export-not-ready" : assessment.Code;
            }

            if (!readyForRuntime || runtimeBlockers > 0)
            {
                return "runtime-not-ready";
            }

            return "ready";
        }

        private static string ResolveMessage(
            DimensionTemplateStarterAssessment assessment,
            bool readyForExport,
            bool readyForRuntime,
            int blockers,
            int runtimeBlockers)
        {
            if (assessment == null)
            {
                return "No customizer workspace data is available.";
            }

            if (blockers == 1)
            {
                return "Fix 1 blocking biome authoring issue before exporting this dimension.";
            }

            if (blockers > 1)
            {
                return "Fix " + blockers + " blocking biome authoring issues before exporting this dimension.";
            }

            if (!readyForExport)
            {
                return string.IsNullOrEmpty(assessment.Message)
                    ? "The dimension is not ready for manifest export."
                    : assessment.Message;
            }

            if (!readyForRuntime)
            {
                return string.IsNullOrEmpty(assessment.Message)
                    ? "The dimension can be edited/exported, but is not ready for runtime generation."
                    : assessment.Message;
            }

            if (runtimeBlockers > 0)
            {
                return "The dimension can be exported, but runtime generation still has required content to configure.";
            }

            return "The dimension is ready for manifest export and runtime generation.";
        }

        private static int CompareGuidance(
            DimensionBiomeCustomizerGuidanceItem left,
            DimensionBiomeCustomizerGuidanceItem right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int biome = string.CompareOrdinal(left.BiomeId, right.BiomeId);
            if (biome != 0)
            {
                return biome;
            }

            return string.CompareOrdinal(left.Title, right.Title);
        }
    }
}
