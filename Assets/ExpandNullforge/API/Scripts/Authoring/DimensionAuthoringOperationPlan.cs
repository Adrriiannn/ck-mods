using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public enum DimensionAuthoringOperationKind
    {
        None = 0,
        Inspect = 1,
        AddRequiredContent = 2,
        ConfigureContent = 3,
        FixBlockingIssue = 4,
        ReviewWarning = 5,
        ResolveSpatialConflict = 6,
        ExportManifest = 7,
        PrepareRuntimeGeneration = 8
    }

    public readonly struct DimensionAuthoringOperationItem
    {
        public readonly DimensionAuthoringOperationKind Kind;
        public readonly DimensionAuthoringReadinessState State;
        public readonly DimensionAuthoringSeverity Severity;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string RecordKind;
        public readonly string RecordId;
        public readonly DimensionAuthoringPreviewLayerKind LayerKind;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly string Title;
        public readonly string Message;
        public readonly string PrimaryActionId;
        public readonly int Priority;
        public readonly bool Recommended;
        public readonly bool BlocksManifestExport;
        public readonly bool BlocksRuntimeGeneration;

        public DimensionAuthoringOperationItem(
            DimensionAuthoringOperationKind kind,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            string dimensionId,
            string biomeId,
            string recordKind,
            string recordId,
            DimensionAuthoringPreviewLayerKind layerKind,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            string title,
            string message,
            string primaryActionId,
            int priority,
            bool recommended,
            bool blocksManifestExport,
            bool blocksRuntimeGeneration)
        {
            Kind = kind;
            State = state;
            Severity = severity;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            LayerKind = layerKind;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            Priority = priority < 0 ? 0 : priority;
            Recommended = recommended;
            BlocksManifestExport = blocksManifestExport;
            BlocksRuntimeGeneration = blocksRuntimeGeneration;
        }
    }

    public readonly struct DimensionAuthoringOperationPlan
    {
        public readonly bool ReadyForManifestExport;
        public readonly bool ReadyForRuntimeGeneration;
        public readonly string Code;
        public readonly string Message;
        public readonly int OperationCount;
        public readonly int RecommendedCount;
        public readonly int BlockingManifestExportCount;
        public readonly int BlockingRuntimeGenerationCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly IReadOnlyList<DimensionAuthoringOperationItem> Operations;

        public DimensionAuthoringOperationPlan(
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            string code,
            string message,
            int operationCount,
            int recommendedCount,
            int blockingManifestExportCount,
            int blockingRuntimeGenerationCount,
            int errorCount,
            int warningCount,
            IReadOnlyList<DimensionAuthoringOperationItem> operations)
        {
            ReadyForManifestExport = readyForManifestExport;
            ReadyForRuntimeGeneration = readyForRuntimeGeneration;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            OperationCount = operationCount < 0 ? 0 : operationCount;
            RecommendedCount = recommendedCount < 0 ? 0 : recommendedCount;
            BlockingManifestExportCount = blockingManifestExportCount < 0 ? 0 : blockingManifestExportCount;
            BlockingRuntimeGenerationCount = blockingRuntimeGenerationCount < 0 ? 0 : blockingRuntimeGenerationCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Operations = operations ?? new List<DimensionAuthoringOperationItem>();
        }
    }

    public static class DimensionAuthoringOperationPlanUtility
    {
        public static DimensionAuthoringOperationPlan BuildPlan(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringReadinessReport readiness)
        {
            return BuildPlan(
                summary,
                readiness,
                DimensionAuthoringSpatialConflictUtility.BuildConflicts(summary));
        }

        public static DimensionAuthoringOperationPlan BuildPlan(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringReadinessReport readiness,
            IReadOnlyList<DimensionAuthoringSpatialConflict> spatialConflicts)
        {
            List<DimensionAuthoringOperationItem> operations =
                new List<DimensionAuthoringOperationItem>();

            AddReadinessOperations(summary, readiness, operations);
            AddIssueOperations(summary, operations);
            AddSpatialConflictOperations(summary, spatialConflicts, operations);

            int recommended;
            int exportBlockers;
            int runtimeBlockers;
            int errors;
            int warnings;
            CountOperations(
                operations,
                out recommended,
                out exportBlockers,
                out runtimeBlockers,
                out errors,
                out warnings);

            bool readyForExport = readiness.Ready && exportBlockers == 0;
            bool readyForRuntime = readyForExport && runtimeBlockers == 0;

            AddExportOperation(summary, operations, readyForExport, readyForRuntime, exportBlockers, runtimeBlockers);
            operations.Sort(CompareOperations);

            CountOperations(
                operations,
                out recommended,
                out exportBlockers,
                out runtimeBlockers,
                out errors,
                out warnings);

            string code = ResolveCode(readyForExport, readyForRuntime, exportBlockers, runtimeBlockers, warnings);
            string message = ResolveMessage(readyForExport, readyForRuntime, exportBlockers, runtimeBlockers, warnings);

            return new DimensionAuthoringOperationPlan(
                readyForExport,
                readyForRuntime,
                code,
                message,
                operations.Count,
                recommended,
                exportBlockers,
                runtimeBlockers,
                errors,
                warnings,
                operations);
        }

        private static void AddReadinessOperations(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringReadinessReport readiness,
            List<DimensionAuthoringOperationItem> operations)
        {
            IReadOnlyList<DimensionAuthoringReadinessEntry> entries = readiness.Entries;
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringReadinessEntry entry = entries[i];
                if (entry.State == DimensionAuthoringReadinessState.Ready)
                {
                    continue;
                }

                DimensionAuthoringOperationKind kind = ResolveReadinessKind(entry);
                bool blocksExport = CategoryBlocksManifestExport(entry.Category) &&
                    (entry.State == DimensionAuthoringReadinessState.Blocked ||
                        entry.State == DimensionAuthoringReadinessState.Missing);
                bool blocksRuntime = blocksExport ||
                    (CategoryBlocksRuntime(entry.Category) &&
                        entry.State != DimensionAuthoringReadinessState.Ready);
                DimensionAuthoringSeverity severity = ResolveReadinessSeverity(entry);

                operations.Add(new DimensionAuthoringOperationItem(
                    kind,
                    entry.State,
                    severity,
                    string.IsNullOrEmpty(entry.DimensionId) ? summary.DimensionId : entry.DimensionId,
                    entry.BiomeId,
                    entry.Category.ToString(),
                    entry.Code,
                    DimensionAuthoringPreviewLayerKind.PlayableBounds,
                    false,
                    default(DimensionBounds),
                    BuildReadinessTitle(entry),
                    entry.Message,
                    BuildReadinessActionId(entry),
                    BuildReadinessPriority(entry),
                    ShouldRecommendReadinessOperation(entry),
                    blocksExport,
                    blocksRuntime));
            }
        }

        private static void AddIssueOperations(
            DimensionAuthoringPreviewSummary summary,
            List<DimensionAuthoringOperationItem> operations)
        {
            IReadOnlyList<DimensionAuthoringIssue> issues = summary.Issues;
            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                bool error = issue.Severity == DimensionAuthoringSeverity.Error;
                operations.Add(new DimensionAuthoringOperationItem(
                    error
                        ? DimensionAuthoringOperationKind.FixBlockingIssue
                        : DimensionAuthoringOperationKind.ReviewWarning,
                    error
                        ? DimensionAuthoringReadinessState.Blocked
                        : DimensionAuthoringReadinessState.Partial,
                    issue.Severity,
                    summary.DimensionId,
                    string.Empty,
                    issue.RecordKind,
                    issue.RecordId,
                    DimensionAuthoringPreviewLayerKind.PlayableBounds,
                    issue.HasLocalBounds,
                    issue.LocalBounds,
                    error ? "Fix " + DisplayIssueTarget(issue) : "Review " + DisplayIssueTarget(issue),
                    issue.Message,
                    error ? "fix-authoring-issue" : "review-authoring-warning",
                    error ? 0 : 30,
                    true,
                    error,
                    error));
            }
        }

        private static void AddSpatialConflictOperations(
            DimensionAuthoringPreviewSummary summary,
            IReadOnlyList<DimensionAuthoringSpatialConflict> conflicts,
            List<DimensionAuthoringOperationItem> operations)
        {
            if (conflicts == null)
            {
                return;
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                DimensionAuthoringSpatialConflict conflict = conflicts[i];
                bool error = conflict.Severity == DimensionAuthoringSeverity.Error;
                operations.Add(new DimensionAuthoringOperationItem(
                    DimensionAuthoringOperationKind.ResolveSpatialConflict,
                    error
                        ? DimensionAuthoringReadinessState.Blocked
                        : DimensionAuthoringReadinessState.Partial,
                    conflict.Severity,
                    string.IsNullOrEmpty(conflict.DimensionId) ? summary.DimensionId : conflict.DimensionId,
                    conflict.BiomeId,
                    conflict.Kind.ToString(),
                    conflict.PrimaryRecordId,
                    conflict.PrimaryLayer,
                    conflict.HasLocalBounds,
                    conflict.LocalBounds,
                    error ? "Fix placement conflict" : "Review placement conflict",
                    conflict.Message,
                    error ? "fix-spatial-conflict" : "review-spatial-conflict",
                    error ? 5 : 35,
                    true,
                    error,
                    error));
            }
        }

        private static void AddExportOperation(
            DimensionAuthoringPreviewSummary summary,
            List<DimensionAuthoringOperationItem> operations,
            bool readyForExport,
            bool readyForRuntime,
            int exportBlockers,
            int runtimeBlockers)
        {
            string title;
            string message;
            string action;
            DimensionAuthoringOperationKind kind;
            DimensionAuthoringReadinessState state;
            DimensionAuthoringSeverity severity;
            int priority;
            bool recommended;

            if (!readyForExport)
            {
                title = "Export blocked";
                message = exportBlockers == 1
                    ? "Fix 1 blocking authoring operation before exporting this dimension."
                    : "Fix " + exportBlockers + " blocking authoring operations before exporting this dimension.";
                action = "inspect-blockers";
                kind = DimensionAuthoringOperationKind.ExportManifest;
                state = DimensionAuthoringReadinessState.Blocked;
                severity = DimensionAuthoringSeverity.Error;
                priority = 1;
                recommended = false;
            }
            else if (!readyForRuntime)
            {
                title = "Prepare runtime generation";
                message = runtimeBlockers == 1
                    ? "Manifest export is possible, but 1 runtime generation requirement still needs attention."
                    : "Manifest export is possible, but " + runtimeBlockers + " runtime generation requirements still need attention.";
                action = "inspect-runtime-readiness";
                kind = DimensionAuthoringOperationKind.PrepareRuntimeGeneration;
                state = DimensionAuthoringReadinessState.Partial;
                severity = DimensionAuthoringSeverity.Info;
                priority = 15;
                recommended = false;
            }
            else
            {
                title = "Export manifest";
                message = "The dimension authoring graph is ready for manifest export and runtime generation.";
                action = "export-manifest";
                kind = DimensionAuthoringOperationKind.ExportManifest;
                state = DimensionAuthoringReadinessState.Ready;
                severity = DimensionAuthoringSeverity.Info;
                priority = 90;
                recommended = true;
            }

            operations.Add(new DimensionAuthoringOperationItem(
                kind,
                state,
                severity,
                summary.DimensionId,
                string.Empty,
                "Dimension",
                summary.DimensionId,
                DimensionAuthoringPreviewLayerKind.PlayableBounds,
                IsValidBounds(summary.PlayableLocalBounds),
                summary.PlayableLocalBounds,
                title,
                message,
                action,
                priority,
                recommended,
                !readyForExport,
                false));
        }

        private static DimensionAuthoringOperationKind ResolveReadinessKind(
            DimensionAuthoringReadinessEntry entry)
        {
            if (entry.State == DimensionAuthoringReadinessState.Blocked)
            {
                return DimensionAuthoringOperationKind.FixBlockingIssue;
            }

            if (entry.State == DimensionAuthoringReadinessState.Missing)
            {
                return DimensionAuthoringOperationKind.AddRequiredContent;
            }

            return DimensionAuthoringOperationKind.ConfigureContent;
        }

        private static string BuildReadinessTitle(
            DimensionAuthoringReadinessEntry entry)
        {
            string category = entry.Category.ToString();
            if (!string.IsNullOrEmpty(entry.BiomeId))
            {
                return category + " for " + entry.BiomeId;
            }

            return category;
        }

        private static string BuildReadinessActionId(
            DimensionAuthoringReadinessEntry entry)
        {
            if (entry.State == DimensionAuthoringReadinessState.Blocked)
            {
                return "fix-readiness-blocker";
            }

            if (entry.State == DimensionAuthoringReadinessState.Missing)
            {
                return "add-required-content";
            }

            return "configure-content";
        }

        private static int BuildReadinessPriority(
            DimensionAuthoringReadinessEntry entry)
        {
            if (entry.State == DimensionAuthoringReadinessState.Blocked)
            {
                return 0;
            }

            if (entry.State == DimensionAuthoringReadinessState.Missing)
            {
                return CategoryBlocksRuntime(entry.Category) ? 10 : 20;
            }

            return 50;
        }

        private static DimensionAuthoringSeverity ResolveReadinessSeverity(
            DimensionAuthoringReadinessEntry entry)
        {
            if (entry.State == DimensionAuthoringReadinessState.Blocked)
            {
                return DimensionAuthoringSeverity.Error;
            }

            if (entry.State == DimensionAuthoringReadinessState.Missing &&
                CategoryBlocksManifestExport(entry.Category))
            {
                return DimensionAuthoringSeverity.Warning;
            }

            return DimensionAuthoringSeverity.Info;
        }

        private static bool ShouldRecommendReadinessOperation(
            DimensionAuthoringReadinessEntry entry)
        {
            return entry.State == DimensionAuthoringReadinessState.Blocked ||
                (entry.State == DimensionAuthoringReadinessState.Missing &&
                    CategoryBlocksManifestExport(entry.Category));
        }

        private static bool CategoryBlocksManifestExport(
            DimensionAuthoringReadinessCategory category)
        {
            return category == DimensionAuthoringReadinessCategory.Dimension ||
                category == DimensionAuthoringReadinessCategory.Layout ||
                category == DimensionAuthoringReadinessCategory.Biome ||
                category == DimensionAuthoringReadinessCategory.Terrain ||
                category == DimensionAuthoringReadinessCategory.Validation;
        }

        private static bool CategoryBlocksRuntime(
            DimensionAuthoringReadinessCategory category)
        {
            return category == DimensionAuthoringReadinessCategory.Dimension ||
                category == DimensionAuthoringReadinessCategory.Layout ||
                category == DimensionAuthoringReadinessCategory.Biome ||
                category == DimensionAuthoringReadinessCategory.Terrain ||
                category == DimensionAuthoringReadinessCategory.GenerationPasses ||
                category == DimensionAuthoringReadinessCategory.Validation;
        }

        private static string DisplayIssueTarget(
            DimensionAuthoringIssue issue)
        {
            if (!string.IsNullOrEmpty(issue.RecordId))
            {
                return issue.RecordId;
            }

            if (!string.IsNullOrEmpty(issue.RecordKind))
            {
                return issue.RecordKind;
            }

            if (!string.IsNullOrEmpty(issue.Code))
            {
                return issue.Code;
            }

            return "authoring issue";
        }

        private static bool IsValidBounds(
            DimensionBounds bounds)
        {
            return bounds.MaxExclusive.x > bounds.Min.x &&
                bounds.MaxExclusive.y > bounds.Min.y;
        }

        private static void CountOperations(
            IReadOnlyList<DimensionAuthoringOperationItem> operations,
            out int recommended,
            out int exportBlockers,
            out int runtimeBlockers,
            out int errors,
            out int warnings)
        {
            recommended = 0;
            exportBlockers = 0;
            runtimeBlockers = 0;
            errors = 0;
            warnings = 0;

            if (operations == null)
            {
                return;
            }

            for (int i = 0; i < operations.Count; i++)
            {
                DimensionAuthoringOperationItem item = operations[i];
                if (item.Recommended)
                {
                    recommended++;
                }

                if (item.BlocksManifestExport)
                {
                    exportBlockers++;
                }

                if (item.BlocksRuntimeGeneration)
                {
                    runtimeBlockers++;
                }

                if (item.Severity == DimensionAuthoringSeverity.Error)
                {
                    errors++;
                }
                else if (item.Severity == DimensionAuthoringSeverity.Warning)
                {
                    warnings++;
                }
            }
        }

        private static string ResolveCode(
            bool readyForExport,
            bool readyForRuntime,
            int exportBlockers,
            int runtimeBlockers,
            int warnings)
        {
            if (exportBlockers > 0 || !readyForExport)
            {
                return "export-blocked";
            }

            if (runtimeBlockers > 0 || !readyForRuntime)
            {
                return "runtime-not-ready";
            }

            if (warnings > 0)
            {
                return "ready-with-warnings";
            }

            return "ready";
        }

        private static string ResolveMessage(
            bool readyForExport,
            bool readyForRuntime,
            int exportBlockers,
            int runtimeBlockers,
            int warnings)
        {
            if (exportBlockers == 1 || (!readyForExport && exportBlockers <= 1))
            {
                return "Fix 1 blocking authoring operation before exporting this dimension.";
            }

            if (exportBlockers > 1 || !readyForExport)
            {
                return "Fix " + exportBlockers + " blocking authoring operations before exporting this dimension.";
            }

            if (runtimeBlockers == 1 || (!readyForRuntime && runtimeBlockers <= 1))
            {
                return "The manifest can be exported, but 1 runtime generation requirement still needs attention.";
            }

            if (runtimeBlockers > 1 || !readyForRuntime)
            {
                return "The manifest can be exported, but " + runtimeBlockers + " runtime generation requirements still need attention.";
            }

            if (warnings > 0)
            {
                return "The dimension is exportable; review warnings before publishing.";
            }

            return "The dimension is ready for manifest export and runtime generation.";
        }

        private static int CompareOperations(
            DimensionAuthoringOperationItem left,
            DimensionAuthoringOperationItem right)
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

            int kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0)
            {
                return kind;
            }

            return string.CompareOrdinal(left.Title, right.Title);
        }
    }
}
