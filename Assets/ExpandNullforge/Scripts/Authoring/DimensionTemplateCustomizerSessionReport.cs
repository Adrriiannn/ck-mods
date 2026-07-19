using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateCustomizerSessionReport
    {
        public DimensionTemplateCustomizerSessionReport(
            bool hasWorkspace,
            bool hasGraph,
            bool hasDimensionTemplate,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            bool readyForValidation,
            bool readyForApply,
            string stageId,
            string code,
            string message,
            string dimensionId,
            string displayName,
            string primaryActionId,
            string primaryActionTitle,
            string primaryActionMessage,
            int biomeCount,
            int biomeRecipeCount,
            int savePlanEntryCount,
            int requiredSavePlanEntryCount,
            int advisoryMessageCount,
            int exportNoteCount,
            int operationCount,
            int recommendedOperationCount,
            int blockingManifestExportCount,
            int blockingRuntimeGenerationCount,
            int visualAssetReferenceCount,
            int visualAssetReadyCount,
            int visualAssetAdvisoryCount,
            int visualAssetBlockedCount,
            int errorCount,
            int warningCount,
            DimensionTemplateCustomizerDashboard dashboard,
            DimensionAuthoringCanvasModel previewCanvas,
            DimensionAuthoringOperationPlan operationPlan,
            DimensionTemplateManifestExportPreview manifestExportPreview,
            DimensionVisualAssetValidationReport visualAssetValidation,
            DimensionTemplateAssetSavePlan savePlan,
            IReadOnlyList<string> advisoryMessages,
            IReadOnlyList<string> uiNotes,
            IReadOnlyList<DimensionAuthoringOperationItem> recommendedOperations)
        {
            HasWorkspace = hasWorkspace;
            HasGraph = hasGraph;
            HasDimensionTemplate = hasDimensionTemplate;
            ReadyForManifestExport = readyForManifestExport;
            ReadyForRuntimeGeneration = readyForRuntimeGeneration;
            ReadyForValidation = readyForValidation;
            ReadyForApply = readyForApply;
            StageId = stageId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            PrimaryActionTitle = primaryActionTitle ?? string.Empty;
            PrimaryActionMessage = primaryActionMessage ?? string.Empty;
            BiomeCount = biomeCount < 0 ? 0 : biomeCount;
            BiomeRecipeCount = biomeRecipeCount < 0 ? 0 : biomeRecipeCount;
            SavePlanEntryCount = savePlanEntryCount < 0 ? 0 : savePlanEntryCount;
            RequiredSavePlanEntryCount = requiredSavePlanEntryCount < 0 ? 0 : requiredSavePlanEntryCount;
            AdvisoryMessageCount = advisoryMessageCount < 0 ? 0 : advisoryMessageCount;
            ExportNoteCount = exportNoteCount < 0 ? 0 : exportNoteCount;
            OperationCount = operationCount < 0 ? 0 : operationCount;
            RecommendedOperationCount = recommendedOperationCount < 0 ? 0 : recommendedOperationCount;
            BlockingManifestExportCount = blockingManifestExportCount < 0 ? 0 : blockingManifestExportCount;
            BlockingRuntimeGenerationCount = blockingRuntimeGenerationCount < 0 ? 0 : blockingRuntimeGenerationCount;
            VisualAssetReferenceCount = visualAssetReferenceCount < 0 ? 0 : visualAssetReferenceCount;
            VisualAssetReadyCount = visualAssetReadyCount < 0 ? 0 : visualAssetReadyCount;
            VisualAssetAdvisoryCount = visualAssetAdvisoryCount < 0 ? 0 : visualAssetAdvisoryCount;
            VisualAssetBlockedCount = visualAssetBlockedCount < 0 ? 0 : visualAssetBlockedCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Dashboard = dashboard;
            PreviewCanvas = previewCanvas;
            OperationPlan = operationPlan;
            ManifestExportPreview = manifestExportPreview;
            VisualAssetValidation = visualAssetValidation;
            SavePlan = savePlan;
            AdvisoryMessages = advisoryMessages ?? new List<string>();
            UiNotes = uiNotes ?? new List<string>();
            RecommendedOperations = recommendedOperations ?? new List<DimensionAuthoringOperationItem>();
        }

        public bool HasWorkspace { get; private set; }

        public bool HasGraph { get; private set; }

        public bool HasDimensionTemplate { get; private set; }

        public bool ReadyForManifestExport { get; private set; }

        public bool ReadyForRuntimeGeneration { get; private set; }

        public bool ReadyForValidation { get; private set; }

        public bool ReadyForApply { get; private set; }

        public string StageId { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public string DimensionId { get; private set; }

        public string DisplayName { get; private set; }

        public string PrimaryActionId { get; private set; }

        public string PrimaryActionTitle { get; private set; }

        public string PrimaryActionMessage { get; private set; }

        public int BiomeCount { get; private set; }

        public int BiomeRecipeCount { get; private set; }

        public int SavePlanEntryCount { get; private set; }

        public int RequiredSavePlanEntryCount { get; private set; }

        public int AdvisoryMessageCount { get; private set; }

        public int ExportNoteCount { get; private set; }

        public int OperationCount { get; private set; }

        public int RecommendedOperationCount { get; private set; }

        public int BlockingManifestExportCount { get; private set; }

        public int BlockingRuntimeGenerationCount { get; private set; }

        public int VisualAssetReferenceCount { get; private set; }

        public int VisualAssetReadyCount { get; private set; }

        public int VisualAssetAdvisoryCount { get; private set; }

        public int VisualAssetBlockedCount { get; private set; }

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public DimensionTemplateCustomizerDashboard Dashboard { get; private set; }

        public DimensionAuthoringCanvasModel PreviewCanvas { get; private set; }

        public DimensionAuthoringOperationPlan OperationPlan { get; private set; }

        public DimensionTemplateManifestExportPreview ManifestExportPreview { get; private set; }

        public DimensionVisualAssetValidationReport VisualAssetValidation { get; private set; }

        public DimensionTemplateAssetSavePlan SavePlan { get; private set; }

        public IReadOnlyList<string> AdvisoryMessages { get; private set; }

        public IReadOnlyList<string> UiNotes { get; private set; }

        public IReadOnlyList<DimensionAuthoringOperationItem> RecommendedOperations { get; private set; }
    }

    public static class DimensionTemplateCustomizerSessionReportUtility
    {
        public static DimensionTemplateCustomizerSessionReport Build(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            if (workspace == null)
            {
                return CreateMissingWorkspaceReport();
            }

            DimensionTemplateStarterGraph graph = workspace.Graph;
            DimensionTemplateAsset dimension = graph == null ? null : graph.Dimension;
            DimensionTemplateStarterAssessment assessment = workspace.Assessment;
            DimensionTemplateCustomizerDashboard dashboard = workspace.Dashboard;
            DimensionAuthoringOperationPlan operationPlan = workspace.OperationPlan;
            DimensionTemplateManifestExportPreview exportPreview = workspace.ManifestExportPreview;
            DimensionVisualAssetValidationReport visualAssetValidation =
                workspace.VisualAssetValidation;
            DimensionTemplateAssetSavePlan savePlan = workspace.SavePlan;

            IReadOnlyList<DimensionAuthoringOperationItem> recommendedOperations =
                CollectRecommendedOperations(operationPlan.Operations);
            IReadOnlyList<string> advisoryMessages = assessment == null
                ? new List<string>()
                : assessment.AdvisoryMessages ?? new List<string>();
            IReadOnlyList<string> uiNotes = BuildUiNotes(
                workspace,
                exportPreview,
                visualAssetValidation);

            string dimensionId = ResolveDimensionId(dimension, dashboard, exportPreview);
            string displayName = ResolveDisplayName(dimension, dashboard, exportPreview, dimensionId);
            string primaryActionId;
            string primaryActionTitle;
            string primaryActionMessage;
            string stageId = ResolveStage(
                workspace,
                exportPreview,
                recommendedOperations,
                out primaryActionId,
                out primaryActionTitle,
                out primaryActionMessage);

            return new DimensionTemplateCustomizerSessionReport(
                true,
                graph != null,
                dimension != null,
                workspace.ReadyForManifestExport,
                workspace.ReadyForRuntimeGeneration,
                exportPreview.ReadyForValidation,
                exportPreview.ReadyForApply,
                stageId,
                ResolveCode(workspace, exportPreview, recommendedOperations),
                ResolveMessage(workspace, exportPreview, recommendedOperations, primaryActionMessage),
                dimensionId,
                displayName,
                primaryActionId,
                primaryActionTitle,
                primaryActionMessage,
                Count(workspace.BiomeOverviews),
                Count(workspace.BiomeRecipes),
                Count(savePlan == null ? null : savePlan.Entries),
                CountRequiredSaveEntries(savePlan),
                Count(advisoryMessages),
                Count(exportPreview.Notes),
                operationPlan.OperationCount,
                Count(recommendedOperations),
                operationPlan.BlockingManifestExportCount,
                operationPlan.BlockingRuntimeGenerationCount,
                visualAssetValidation == null ? 0 : visualAssetValidation.EntryCount,
                visualAssetValidation == null ? 0 : visualAssetValidation.ReadyCount,
                visualAssetValidation == null ? 0 : visualAssetValidation.AdvisoryCount,
                visualAssetValidation == null ? 0 : visualAssetValidation.BlockedCount,
                operationPlan.ErrorCount + exportPreview.ErrorCount +
                    (visualAssetValidation == null ? 0 : visualAssetValidation.BlockedCount),
                operationPlan.WarningCount + exportPreview.WarningCount +
                    (visualAssetValidation == null ? 0 : visualAssetValidation.AdvisoryCount),
                dashboard,
                workspace.PreviewCanvas,
                operationPlan,
                exportPreview,
                visualAssetValidation,
                savePlan,
                advisoryMessages,
                uiNotes,
                recommendedOperations);
        }

        private static DimensionTemplateCustomizerSessionReport CreateMissingWorkspaceReport()
        {
            return new DimensionTemplateCustomizerSessionReport(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                "workspace-missing",
                "workspace-missing",
                "No dimension authoring workspace is available.",
                string.Empty,
                string.Empty,
                "select-dimension-template",
                "Select a Dimension Asset",
                "Select or create a Dimension Asset before opening the customizer.",
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                null,
                default(DimensionAuthoringCanvasModel),
                default(DimensionAuthoringOperationPlan),
                default(DimensionTemplateManifestExportPreview),
                null,
                null,
                new List<string>(),
                new List<string>
                {
                    "No UI state can be prepared until a workspace exists."
                },
                new List<DimensionAuthoringOperationItem>());
        }

        private static IReadOnlyList<DimensionAuthoringOperationItem> CollectRecommendedOperations(
            IReadOnlyList<DimensionAuthoringOperationItem> operations)
        {
            List<DimensionAuthoringOperationItem> recommended =
                new List<DimensionAuthoringOperationItem>();
            if (operations == null)
            {
                return recommended;
            }

            for (int i = 0; i < operations.Count; i++)
            {
                DimensionAuthoringOperationItem item = operations[i];
                if (item.Recommended)
                {
                    recommended.Add(item);
                }
            }

            return recommended;
        }

        private static IReadOnlyList<string> BuildUiNotes(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview exportPreview,
            DimensionVisualAssetValidationReport visualAssetValidation)
        {
            List<string> notes = new List<string>();
            if (workspace == null)
            {
                notes.Add("Workspace is missing.");
                return notes;
            }

            if (!workspace.ReadyForManifestExport)
            {
                notes.Add("Manifest export should remain disabled until blocking authoring issues are fixed.");
            }

            if (workspace.ReadyForManifestExport && !workspace.ReadyForRuntimeGeneration)
            {
                notes.Add("Runtime generation is not configured yet; manifest export can still proceed.");
            }

            if (exportPreview.ManifestBuilt && !exportPreview.ReadyForApply)
            {
                notes.Add("Manifest preview is built, but apply should stay disabled until validation/export blockers are resolved.");
            }

            IReadOnlyList<string> exportNotes = exportPreview.Notes;
            if (exportNotes != null)
            {
                for (int i = 0; i < exportNotes.Count; i++)
                {
                    string note = exportNotes[i];
                    if (!string.IsNullOrEmpty(note))
                    {
                        notes.Add(note);
                    }
                }
            }

            AddVisualAssetNotes(notes, visualAssetValidation);

            if (notes.Count == 0)
            {
                notes.Add("Workspace is ready for customizer display.");
            }

            return notes;
        }

        private static void AddVisualAssetNotes(
            List<string> notes,
            DimensionVisualAssetValidationReport visualAssetValidation)
        {
            if (notes == null || visualAssetValidation == null)
            {
                return;
            }

            if (visualAssetValidation.BlockedCount > 0)
            {
                notes.Add(
                    "Visual asset preflight blocked " +
                    visualAssetValidation.BlockedCount +
                    " asset reference(s). Fix missing IDs/resource keys before export.");
            }
            else if (visualAssetValidation.AdvisoryCount > 0)
            {
                notes.Add(
                    "Visual asset preflight found " +
                    visualAssetValidation.AdvisoryCount +
                    " asset reference(s) that need UGC material, runtime SpriteObject, or lighting review.");
            }
            else if (visualAssetValidation.EntryCount > 0)
            {
                notes.Add(
                    "Visual asset preflight passed for " +
                    visualAssetValidation.EntryCount +
                    " asset reference(s).");
            }

            IReadOnlyList<DimensionAuthoringIssue> issues = visualAssetValidation.Issues;
            if (issues == null)
            {
                return;
            }

            int maxIssueNotes = issues.Count > 5 ? 5 : issues.Count;
            for (int i = 0; i < maxIssueNotes; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                if (!string.IsNullOrEmpty(issue.Message))
                {
                    notes.Add(issue.Message);
                }
            }
        }

        private static string ResolveDimensionId(
            DimensionTemplateAsset dimension,
            DimensionTemplateCustomizerDashboard dashboard,
            DimensionTemplateManifestExportPreview exportPreview)
        {
            if (dimension != null && !string.IsNullOrEmpty(dimension.DimensionId))
            {
                return dimension.DimensionId;
            }

            if (dashboard != null && !string.IsNullOrEmpty(dashboard.DimensionId))
            {
                return dashboard.DimensionId;
            }

            return exportPreview.DimensionId;
        }

        private static string ResolveDisplayName(
            DimensionTemplateAsset dimension,
            DimensionTemplateCustomizerDashboard dashboard,
            DimensionTemplateManifestExportPreview exportPreview,
            string dimensionId)
        {
            if (dimension != null && !string.IsNullOrEmpty(dimension.DisplayName))
            {
                return dimension.DisplayName;
            }

            if (dashboard != null && !string.IsNullOrEmpty(dashboard.DisplayName))
            {
                return dashboard.DisplayName;
            }

            if (!string.IsNullOrEmpty(exportPreview.DisplayName))
            {
                return exportPreview.DisplayName;
            }

            return dimensionId ?? string.Empty;
        }

        private static string ResolveStage(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview exportPreview,
            IReadOnlyList<DimensionAuthoringOperationItem> recommendedOperations,
            out string primaryActionId,
            out string primaryActionTitle,
            out string primaryActionMessage)
        {
            DimensionAuthoringOperationItem operation;
            if (TryGetFirstRecommendedOperation(recommendedOperations, out operation))
            {
                primaryActionId = string.IsNullOrEmpty(operation.PrimaryActionId)
                    ? operation.Kind.ToString()
                    : operation.PrimaryActionId;
                primaryActionTitle = string.IsNullOrEmpty(operation.Title)
                    ? "Review recommended action"
                    : operation.Title;
                primaryActionMessage = operation.Message;

                if (operation.BlocksManifestExport)
                {
                    return "fix-export-blocker";
                }

                if (operation.BlocksRuntimeGeneration)
                {
                    return "configure-runtime-generation";
                }

                return "review-recommended-action";
            }

            if (!workspace.ReadyForManifestExport)
            {
                primaryActionId = "fix-authoring";
                primaryActionTitle = "Fix authoring blockers";
                primaryActionMessage = "Resolve blocking authoring issues before exporting the manifest.";
                return "fix-authoring";
            }

            if (!exportPreview.ReadyForValidation)
            {
                primaryActionId = "preview-manifest";
                primaryActionTitle = "Preview manifest";
                primaryActionMessage = "Build and review the manifest preview before applying it.";
                return "preview-manifest";
            }

            if (!exportPreview.ReadyForApply)
            {
                primaryActionId = "validate-manifest";
                primaryActionTitle = "Validate manifest";
                primaryActionMessage = "Validate the manifest preview before applying it.";
                return "validate-manifest";
            }

            if (!workspace.ReadyForRuntimeGeneration)
            {
                primaryActionId = "apply-manifest";
                primaryActionTitle = "Export manifest";
                primaryActionMessage = "The manifest is exportable. Runtime generation providers can be configured later in Generation.";
                return "ready";
            }

            primaryActionId = "ready";
            primaryActionTitle = "Ready";
            primaryActionMessage = "The dimension is ready for manifest export and runtime generation.";
            return "ready";
        }

        private static bool TryGetFirstRecommendedOperation(
            IReadOnlyList<DimensionAuthoringOperationItem> recommendedOperations,
            out DimensionAuthoringOperationItem operation)
        {
            if (recommendedOperations != null && recommendedOperations.Count > 0)
            {
                operation = recommendedOperations[0];
                return true;
            }

            operation = default(DimensionAuthoringOperationItem);
            return false;
        }

        private static string ResolveCode(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview exportPreview,
            IReadOnlyList<DimensionAuthoringOperationItem> recommendedOperations)
        {
            if (workspace == null)
            {
                return "workspace-missing";
            }

            if (recommendedOperations != null && recommendedOperations.Count > 0)
            {
                DimensionAuthoringOperationItem operation = recommendedOperations[0];
                if (!string.IsNullOrEmpty(operation.PrimaryActionId))
                {
                    return operation.PrimaryActionId;
                }
            }

            if (!string.IsNullOrEmpty(exportPreview.Code))
            {
                return exportPreview.Code;
            }

            return workspace.ReadyForRuntimeGeneration
                ? "ready"
                : workspace.ReadyForManifestExport
                    ? "runtime-not-ready"
                    : "export-not-ready";
        }

        private static string ResolveMessage(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview exportPreview,
            IReadOnlyList<DimensionAuthoringOperationItem> recommendedOperations,
            string primaryActionMessage)
        {
            if (!string.IsNullOrEmpty(primaryActionMessage))
            {
                return primaryActionMessage;
            }

            if (recommendedOperations != null && recommendedOperations.Count > 0)
            {
                DimensionAuthoringOperationItem operation = recommendedOperations[0];
                if (!string.IsNullOrEmpty(operation.Message))
                {
                    return operation.Message;
                }
            }

            if (!string.IsNullOrEmpty(exportPreview.Message))
            {
                return exportPreview.Message;
            }

            return workspace == null
                ? "No dimension authoring workspace is available."
                : "Dimension authoring workspace is ready.";
        }

        private static int Count<T>(IReadOnlyList<T> values)
        {
            return values == null ? 0 : values.Count;
        }

        private static int CountRequiredSaveEntries(
            DimensionTemplateAssetSavePlan savePlan)
        {
            IReadOnlyList<DimensionTemplateAssetSavePlanEntry> entries =
                savePlan == null ? null : savePlan.Entries;
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Required)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
