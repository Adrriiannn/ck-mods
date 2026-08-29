using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateCustomizerViewModel
    {
        public DimensionTemplateCustomizerViewModel(
            string dimensionId,
            string displayName,
            string activeSectionId,
            string stageId,
            string code,
            string message,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            bool readyForValidation,
            bool readyForApply,
            bool hasBlockingIssues,
            int biomeCount,
            int contentCount,
            int sectionCount,
            int actionCount,
            int detailRowCount,
            DimensionTemplateCustomizerDashboard dashboard,
            DimensionTemplateCustomizerSessionReport sessionReport,
            DimensionTemplateCustomizerNavigationModel navigation,
            DimensionTemplateCustomizerSectionDetail activeSectionDetail,
            DimensionTemplateCustomizerActionCatalog actionCatalog,
            DimensionTemplateCustomizerActionPreviewCatalog actionPreviewCatalog,
            DimensionTemplateCustomizerSearchIndex searchIndex,
            DimensionTemplateCustomizerFocusState focusState,
            DimensionTemplateCustomizerCommandPlan commandPlan,
            DimensionAuthoringCanvasModel previewCanvas,
            DimensionTemplateManifestExportPreview manifestExportPreview)
        {
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ActiveSectionId = activeSectionId ?? string.Empty;
            StageId = stageId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ReadyForManifestExport = readyForManifestExport;
            ReadyForRuntimeGeneration = readyForRuntimeGeneration;
            ReadyForValidation = readyForValidation;
            ReadyForApply = readyForApply;
            HasBlockingIssues = hasBlockingIssues;
            BiomeCount = biomeCount < 0 ? 0 : biomeCount;
            ContentCount = contentCount < 0 ? 0 : contentCount;
            SectionCount = sectionCount < 0 ? 0 : sectionCount;
            ActionCount = actionCount < 0 ? 0 : actionCount;
            DetailRowCount = detailRowCount < 0 ? 0 : detailRowCount;
            Dashboard = dashboard;
            SessionReport = sessionReport;
            Navigation = navigation;
            ActiveSectionDetail = activeSectionDetail;
            ActionCatalog = actionCatalog;
            ActionPreviewCatalog = actionPreviewCatalog;
            SearchIndex = searchIndex;
            FocusState = focusState;
            CommandPlan = commandPlan;
            PreviewCanvas = previewCanvas;
            ManifestExportPreview = manifestExportPreview;
        }

        public string DimensionId { get; private set; }

        public string DisplayName { get; private set; }

        public string ActiveSectionId { get; private set; }

        public string StageId { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool ReadyForManifestExport { get; private set; }

        public bool ReadyForRuntimeGeneration { get; private set; }

        public bool ReadyForValidation { get; private set; }

        public bool ReadyForApply { get; private set; }

        public bool HasBlockingIssues { get; private set; }

        public int BiomeCount { get; private set; }

        public int ContentCount { get; private set; }

        public int SectionCount { get; private set; }

        public int ActionCount { get; private set; }

        public int DetailRowCount { get; private set; }

        public DimensionTemplateCustomizerDashboard Dashboard { get; private set; }

        public DimensionTemplateCustomizerSessionReport SessionReport { get; private set; }

        public DimensionTemplateCustomizerNavigationModel Navigation { get; private set; }

        public DimensionTemplateCustomizerSectionDetail ActiveSectionDetail { get; private set; }

        public DimensionTemplateCustomizerActionCatalog ActionCatalog { get; private set; }

        public DimensionTemplateCustomizerActionPreviewCatalog ActionPreviewCatalog { get; private set; }

        public DimensionTemplateCustomizerSearchIndex SearchIndex { get; private set; }

        public DimensionTemplateCustomizerFocusState FocusState { get; private set; }

        public DimensionTemplateCustomizerCommandPlan CommandPlan { get; private set; }

        public DimensionAuthoringCanvasModel PreviewCanvas { get; private set; }

        public DimensionTemplateManifestExportPreview ManifestExportPreview { get; private set; }
    }

    public static class DimensionTemplateCustomizerViewModelUtility
    {
        public static DimensionTemplateCustomizerViewModel Build(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            if (workspace == null)
            {
                DimensionTemplateCustomizerSectionDetail missingDetail =
                    DimensionTemplateCustomizerSectionDetailUtility.Build(null, "overview");
                DimensionTemplateCustomizerActionCatalog missingActions =
                    DimensionTemplateCustomizerActionCatalogUtility.Build(null, "overview");

                return new DimensionTemplateCustomizerViewModel(
                    string.Empty,
                    string.Empty,
                    "overview",
                    "missing-workspace",
                    "workspace-missing",
                    "Select or create a Dimension Asset before opening the customizer.",
                    false,
                    false,
                    false,
                    false,
                    true,
                    0,
                    0,
                    1,
                    missingActions.ActionCount,
                    missingDetail.RowCount,
                    null,
                    null,
                    null,
                    missingDetail,
                    missingActions,
                    DimensionTemplateCustomizerActionPreviewUtility.Build(null, "overview"),
                    DimensionTemplateCustomizerSearchIndexUtility.Build(null),
                    DimensionTemplateCustomizerFocusUtility.Resolve(
                        null,
                        DimensionTemplateCustomizerFocusRequest.ForSection("overview")),
                    DimensionTemplateCustomizerCommandPlanUtility.Build(
                        null,
                        "overview",
                        DimensionTemplateCustomizerFocusRequest.ForSection("overview")),
                    default(DimensionAuthoringCanvasModel),
                    DimensionTemplateManifestExportPreviewBuilder.Build(null));
            }

            string activeSectionId = string.IsNullOrEmpty(sectionId)
                ? workspace.Navigation.ActiveSectionId
                : sectionId;

            DimensionTemplateCustomizerSectionDetail detail =
                workspace.GetSectionDetail(activeSectionId);
            DimensionTemplateCustomizerActionCatalog actions =
                workspace.GetActionCatalog(activeSectionId);
            DimensionTemplateCustomizerActionPreviewCatalog actionPreviews =
                workspace.GetActionPreviews(activeSectionId);
            DimensionTemplateCustomizerFocusRequest focusRequest =
                DimensionTemplateCustomizerFocusRequest.ForSection(activeSectionId);
            DimensionTemplateCustomizerSessionReport session = workspace.SessionReport;
            DimensionTemplateCustomizerNavigationModel navigation = workspace.Navigation;
            DimensionAuthoringPreviewSummary preview = workspace.Preview;

            return new DimensionTemplateCustomizerViewModel(
                session.DimensionId,
                session.DisplayName,
                activeSectionId,
                session.StageId,
                session.Code,
                session.Message,
                session.ReadyForManifestExport,
                session.ReadyForRuntimeGeneration,
                session.ReadyForValidation,
                session.ReadyForApply,
                session.BlockingManifestExportCount > 0 || session.BlockingRuntimeGenerationCount > 0,
                session.BiomeCount,
                CountPreviewContent(preview),
                navigation.SectionCount,
                actions.ActionCount,
                detail.RowCount,
                workspace.Dashboard,
                session,
                navigation,
                detail,
                actions,
                actionPreviews,
                workspace.SearchIndex,
                workspace.ResolveFocus(focusRequest),
                workspace.GetCommandPlan(activeSectionId, focusRequest),
                workspace.PreviewCanvas,
                workspace.ManifestExportPreview);
        }

        private static int CountPreviewContent(DimensionAuthoringPreviewSummary preview)
        {
            return preview.ContentEntries == null ? 0 : preview.ContentEntries.Count;
        }
    }
}
