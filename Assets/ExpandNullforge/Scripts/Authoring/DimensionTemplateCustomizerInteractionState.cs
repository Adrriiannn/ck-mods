using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionTemplateCustomizerInteractionRequest
    {
        public DimensionTemplateCustomizerInteractionRequest(
            string sectionId,
            string searchQuery,
            string searchBiomeId,
            int searchMaxResults,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            DimensionTemplateCustomizerCommandRequest commandRequest)
            : this(
                sectionId,
                searchQuery,
                searchBiomeId,
                searchMaxResults,
                focusRequest,
                commandRequest,
                null)
        {
        }

        public DimensionTemplateCustomizerInteractionRequest(
            string sectionId,
            string searchQuery,
            string searchBiomeId,
            int searchMaxResults,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            DimensionTemplateCustomizerCommandRequest commandRequest,
            DimensionTemplateCustomizerFieldEditRequest fieldEditRequest)
            : this(
                sectionId,
                searchQuery,
                searchBiomeId,
                searchMaxResults,
                focusRequest,
                commandRequest,
                fieldEditRequest,
                null)
        {
        }

        public DimensionTemplateCustomizerInteractionRequest(
            string sectionId,
            string searchQuery,
            string searchBiomeId,
            int searchMaxResults,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            DimensionTemplateCustomizerCommandRequest commandRequest,
            DimensionTemplateCustomizerFieldEditRequest fieldEditRequest,
            DimensionTemplateCustomizerFieldEditBatchRequest fieldEditBatchRequest)
        {
            SectionId = sectionId ?? string.Empty;
            SearchQuery = searchQuery ?? string.Empty;
            SearchBiomeId = searchBiomeId ?? string.Empty;
            SearchMaxResults = searchMaxResults <= 0 ? 25 : searchMaxResults;
            FocusRequest = focusRequest;
            CommandRequest = commandRequest;
            FieldEditRequest = fieldEditRequest;
            FieldEditBatchRequest = fieldEditBatchRequest;
        }

        public string SectionId { get; private set; }

        public string SearchQuery { get; private set; }

        public string SearchBiomeId { get; private set; }

        public int SearchMaxResults { get; private set; }

        public DimensionTemplateCustomizerFocusRequest FocusRequest { get; private set; }

        public DimensionTemplateCustomizerCommandRequest CommandRequest { get; private set; }

        public DimensionTemplateCustomizerFieldEditRequest FieldEditRequest { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchRequest FieldEditBatchRequest { get; private set; }

        public static DimensionTemplateCustomizerInteractionRequest ForSection(string sectionId)
        {
            return new DimensionTemplateCustomizerInteractionRequest(
                sectionId,
                string.Empty,
                string.Empty,
                25,
                DimensionTemplateCustomizerFocusRequest.ForSection(sectionId),
                null);
        }
    }

    public sealed class DimensionTemplateCustomizerInteractionState
    {
        public DimensionTemplateCustomizerInteractionState(
            string sectionId,
            bool hasSearchQuery,
            int searchResultCount,
            DimensionTemplateCustomizerViewModel viewModel,
            DimensionTemplateCustomizerFocusState focusState,
            DimensionTemplateCustomizerFieldSet fieldSet,
            DimensionTemplateCustomizerFieldEditPreview fieldEditPreview,
            DimensionTemplateCustomizerFieldApplyPlan fieldApplyPlan,
            DimensionTemplateCustomizerFieldEditBatchPreview fieldEditBatchPreview,
            DimensionTemplateCustomizerFieldEditBatchApplyPlan fieldEditBatchApplyPlan,
            DimensionTemplateCustomizerFieldEditChangeSet fieldEditChangeSet,
            DimensionTemplateCustomizerFieldEditRollbackPlan fieldEditRollbackPlan,
            DimensionTemplateCustomizerPreparedCommand preparedCommand,
            DimensionTemplateCustomizerCommandPipeline commandPipeline,
            IReadOnlyList<DimensionTemplateCustomizerSearchEntry> searchResults)
        {
            SectionId = sectionId ?? string.Empty;
            HasSearchQuery = hasSearchQuery;
            SearchResultCount = searchResultCount < 0 ? 0 : searchResultCount;
            ViewModel = viewModel;
            FocusState = focusState;
            FieldSet = fieldSet;
            FieldEditPreview = fieldEditPreview;
            FieldApplyPlan = fieldApplyPlan;
            FieldEditBatchPreview = fieldEditBatchPreview;
            FieldEditBatchApplyPlan = fieldEditBatchApplyPlan;
            FieldEditChangeSet = fieldEditChangeSet;
            FieldEditRollbackPlan = fieldEditRollbackPlan;
            PreparedCommand = preparedCommand;
            CommandPipeline = commandPipeline;
            SearchResults = searchResults ?? new List<DimensionTemplateCustomizerSearchEntry>();
        }

        public string SectionId { get; private set; }

        public bool HasSearchQuery { get; private set; }

        public int SearchResultCount { get; private set; }

        public DimensionTemplateCustomizerViewModel ViewModel { get; private set; }

        public DimensionTemplateCustomizerFocusState FocusState { get; private set; }

        public DimensionTemplateCustomizerFieldSet FieldSet { get; private set; }

        public DimensionTemplateCustomizerFieldEditPreview FieldEditPreview { get; private set; }

        public DimensionTemplateCustomizerFieldApplyPlan FieldApplyPlan { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchPreview FieldEditBatchPreview { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchApplyPlan FieldEditBatchApplyPlan { get; private set; }

        public DimensionTemplateCustomizerFieldEditChangeSet FieldEditChangeSet { get; private set; }

        public DimensionTemplateCustomizerFieldEditRollbackPlan FieldEditRollbackPlan { get; private set; }

        public DimensionTemplateCustomizerPreparedCommand PreparedCommand { get; private set; }

        public DimensionTemplateCustomizerCommandPipeline CommandPipeline { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerSearchEntry> SearchResults { get; private set; }
    }

    public static class DimensionTemplateCustomizerInteractionStateUtility
    {
        public static DimensionTemplateCustomizerInteractionState Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerInteractionRequest request)
        {
            string sectionId = ResolveSectionId(workspace, request);
            DimensionTemplateCustomizerFocusRequest focusRequest =
                ResolveFocusRequest(sectionId, request);
            DimensionTemplateCustomizerCommandRequest commandRequest =
                ResolveCommandRequest(sectionId, focusRequest, request);

            DimensionTemplateCustomizerViewModel viewModel =
                workspace == null
                    ? DimensionTemplateCustomizerViewModelUtility.Build(null, sectionId)
                    : workspace.GetCustomizerViewModel(sectionId);
            DimensionTemplateCustomizerFocusState focus =
                workspace == null
                    ? DimensionTemplateCustomizerFocusUtility.Resolve(null, focusRequest)
                    : workspace.ResolveFocus(focusRequest);
            DimensionTemplateCustomizerFieldSet fieldSet =
                workspace == null
                    ? DimensionTemplateCustomizerFieldModelUtility.Build(null, focus)
                    : workspace.GetFieldSet(focus);
            IReadOnlyList<DimensionTemplateCustomizerSearchEntry> searchResults =
                BuildSearchResults(workspace, sectionId, request);
            DimensionTemplateCustomizerPreparedCommand prepared =
                DimensionTemplateCustomizerCommandRequestUtility.Prepare(workspace, commandRequest);
            DimensionTemplateCustomizerCommandPipeline pipeline =
                DimensionTemplateCustomizerCommandPipelineUtility.Build(prepared);
            DimensionTemplateCustomizerFieldEditPreview fieldEditPreview =
                BuildFieldEditPreview(workspace, request);
            DimensionTemplateCustomizerFieldApplyPlan fieldApplyPlan =
                BuildFieldApplyPlan(fieldEditPreview);
            DimensionTemplateCustomizerFieldEditBatchPreview fieldEditBatchPreview =
                BuildFieldEditBatchPreview(workspace, request);
            DimensionTemplateCustomizerFieldEditBatchApplyPlan fieldEditBatchApplyPlan =
                BuildFieldEditBatchApplyPlan(fieldEditBatchPreview);
            DimensionTemplateCustomizerFieldEditChangeSet fieldEditChangeSet =
                BuildFieldEditChangeSet(fieldEditBatchPreview);
            DimensionTemplateCustomizerFieldEditRollbackPlan fieldEditRollbackPlan =
                BuildFieldEditRollbackPlan(fieldEditBatchApplyPlan);

            return new DimensionTemplateCustomizerInteractionState(
                sectionId,
                request != null && !string.IsNullOrEmpty(request.SearchQuery),
                searchResults == null ? 0 : searchResults.Count,
                viewModel,
                focus,
                fieldSet,
                fieldEditPreview,
                fieldApplyPlan,
                fieldEditBatchPreview,
                fieldEditBatchApplyPlan,
                fieldEditChangeSet,
                fieldEditRollbackPlan,
                prepared,
                pipeline,
                searchResults);
        }

        private static DimensionTemplateCustomizerFieldEditPreview BuildFieldEditPreview(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerInteractionRequest request)
        {
            if (request == null || request.FieldEditRequest == null)
            {
                return null;
            }

            return DimensionTemplateCustomizerFieldEditPreviewUtility.Preview(
                workspace,
                request.FieldEditRequest);
        }

        private static DimensionTemplateCustomizerFieldApplyPlan BuildFieldApplyPlan(
            DimensionTemplateCustomizerFieldEditPreview preview)
        {
            if (preview == null)
            {
                return null;
            }

            return DimensionTemplateCustomizerFieldApplyPlanUtility.Build(preview);
        }

        private static DimensionTemplateCustomizerFieldEditBatchPreview BuildFieldEditBatchPreview(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerInteractionRequest request)
        {
            if (request == null || request.FieldEditBatchRequest == null)
            {
                return null;
            }

            return DimensionTemplateCustomizerFieldEditBatchUtility.Preview(
                workspace,
                request.FieldEditBatchRequest);
        }

        private static DimensionTemplateCustomizerFieldEditBatchApplyPlan BuildFieldEditBatchApplyPlan(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            if (preview == null)
            {
                return null;
            }

            return DimensionTemplateCustomizerFieldEditBatchApplyPlanUtility.Build(preview);
        }

        private static DimensionTemplateCustomizerFieldEditChangeSet BuildFieldEditChangeSet(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            if (preview == null)
            {
                return null;
            }

            return DimensionTemplateCustomizerFieldEditChangeSetUtility.Build(preview);
        }

        private static DimensionTemplateCustomizerFieldEditRollbackPlan BuildFieldEditRollbackPlan(
            DimensionTemplateCustomizerFieldEditBatchApplyPlan applyPlan)
        {
            if (applyPlan == null)
            {
                return null;
            }

            return DimensionTemplateCustomizerFieldEditRollbackPlanUtility.Build(applyPlan);
        }

        private static IReadOnlyList<DimensionTemplateCustomizerSearchEntry> BuildSearchResults(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId,
            DimensionTemplateCustomizerInteractionRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.SearchQuery))
            {
                return new List<DimensionTemplateCustomizerSearchEntry>();
            }

            if (workspace == null)
            {
                return DimensionTemplateCustomizerSearchIndexUtility.Search(
                    DimensionTemplateCustomizerSearchIndexUtility.Build(null),
                    request.SearchQuery,
                    sectionId,
                    request.SearchBiomeId,
                    request.SearchMaxResults);
            }

            return workspace.Search(
                request.SearchQuery,
                sectionId,
                request.SearchBiomeId,
                request.SearchMaxResults);
        }

        private static string ResolveSectionId(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerInteractionRequest request)
        {
            if (request != null && !string.IsNullOrEmpty(request.SectionId))
            {
                return request.SectionId;
            }

            if (workspace != null)
            {
                return workspace.Navigation.ActiveSectionId;
            }

            return "overview";
        }

        private static DimensionTemplateCustomizerFocusRequest ResolveFocusRequest(
            string sectionId,
            DimensionTemplateCustomizerInteractionRequest request)
        {
            if (request != null && HasFocusRequest(request.FocusRequest))
            {
                return request.FocusRequest;
            }

            return DimensionTemplateCustomizerFocusRequest.ForSection(sectionId);
        }

        private static bool HasFocusRequest(DimensionTemplateCustomizerFocusRequest request)
        {
            return !string.IsNullOrEmpty(request.SectionId)
                || !string.IsNullOrEmpty(request.BiomeId)
                || !string.IsNullOrEmpty(request.ZoneId)
                || !string.IsNullOrEmpty(request.RecordKind)
                || !string.IsNullOrEmpty(request.RecordId)
                || !string.IsNullOrEmpty(request.ActionId)
                || request.PreferBounds;
        }

        private static DimensionTemplateCustomizerCommandRequest ResolveCommandRequest(
            string sectionId,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            DimensionTemplateCustomizerInteractionRequest request)
        {
            if (request != null && request.CommandRequest != null)
            {
                return request.CommandRequest;
            }

            return DimensionTemplateCustomizerCommandRequest.Preview(
                sectionId,
                string.Empty,
                focusRequest);
        }
    }
}
