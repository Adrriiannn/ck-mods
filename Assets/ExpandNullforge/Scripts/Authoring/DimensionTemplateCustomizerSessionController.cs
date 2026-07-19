using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerSessionStatus
    {
        MissingWorkspace = 0,
        Clean = 1,
        Dirty = 2
    }

    public enum DimensionTemplateCustomizerSessionEventKind
    {
        WorkspaceAssigned = 0,
        InteractionRefreshed = 1,
        FieldEditPreviewed = 2,
        FieldEditBatchPreviewed = 3,
        ExecutionPlanned = 4,
        FieldEditApplied = 5,
        FieldEditBatchApplied = 6,
        MarkedSaved = 7,
        HistoryReset = 8
    }

    public sealed class DimensionTemplateCustomizerSessionEvent
    {
        public DimensionTemplateCustomizerSessionEvent(
            int version,
            DimensionTemplateCustomizerSessionEventKind kind,
            string code,
            string message,
            int requestedCount,
            int appliedCount,
            int blockedCount,
            int unsupportedCount,
            bool dirtyAfterEvent,
            DimensionTemplateCustomizerInteractionState interactionState,
            DimensionTemplateCustomizerFieldEditPreview fieldEditPreview,
            DimensionTemplateCustomizerFieldEditBatchPreview fieldEditBatchPreview,
            DimensionTemplateCustomizerExecutionPlan executionPlan,
            DimensionTemplateCustomizerFieldEditExecutionResult executionResult)
        {
            Version = version < 0 ? 0 : version;
            Kind = kind;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RequestedCount = requestedCount < 0 ? 0 : requestedCount;
            AppliedCount = appliedCount < 0 ? 0 : appliedCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            UnsupportedCount = unsupportedCount < 0 ? 0 : unsupportedCount;
            DirtyAfterEvent = dirtyAfterEvent;
            InteractionState = interactionState;
            FieldEditPreview = fieldEditPreview;
            FieldEditBatchPreview = fieldEditBatchPreview;
            ExecutionPlan = executionPlan;
            ExecutionResult = executionResult;
        }

        public int Version { get; private set; }

        public DimensionTemplateCustomizerSessionEventKind Kind { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public int RequestedCount { get; private set; }

        public int AppliedCount { get; private set; }

        public int BlockedCount { get; private set; }

        public int UnsupportedCount { get; private set; }

        public bool DirtyAfterEvent { get; private set; }

        public DimensionTemplateCustomizerInteractionState InteractionState { get; private set; }

        public DimensionTemplateCustomizerFieldEditPreview FieldEditPreview { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchPreview FieldEditBatchPreview { get; private set; }

        public DimensionTemplateCustomizerExecutionPlan ExecutionPlan { get; private set; }

        public DimensionTemplateCustomizerFieldEditExecutionResult ExecutionResult { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerSessionSnapshot
    {
        public DimensionTemplateCustomizerSessionSnapshot(
            DimensionTemplateCustomizerSessionStatus status,
            int version,
            bool dirty,
            int appliedEditCount,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerInteractionRequest interactionRequest,
            DimensionTemplateCustomizerInteractionState interactionState,
            DimensionTemplateCustomizerFieldEditExecutionResult lastExecutionResult,
            IReadOnlyList<DimensionTemplateCustomizerSessionEvent> history)
        {
            Status = status;
            Version = version < 0 ? 0 : version;
            Dirty = dirty;
            AppliedEditCount = appliedEditCount < 0 ? 0 : appliedEditCount;
            Workspace = workspace;
            InteractionRequest = interactionRequest;
            InteractionState = interactionState;
            LastExecutionResult = lastExecutionResult;
            History = history ?? new List<DimensionTemplateCustomizerSessionEvent>();
        }

        public DimensionTemplateCustomizerSessionStatus Status { get; private set; }

        public int Version { get; private set; }

        public bool Dirty { get; private set; }

        public int AppliedEditCount { get; private set; }

        public DimensionTemplateAuthoringWorkspace Workspace { get; private set; }

        public DimensionTemplateCustomizerInteractionRequest InteractionRequest { get; private set; }

        public DimensionTemplateCustomizerInteractionState InteractionState { get; private set; }

        public DimensionTemplateCustomizerFieldEditExecutionResult LastExecutionResult { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerSessionEvent> History { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerSessionController
    {
        private const int MaxHistoryEntries = 64;

        private readonly List<DimensionTemplateCustomizerSessionEvent> history =
            new List<DimensionTemplateCustomizerSessionEvent>();

        public DimensionTemplateCustomizerSessionController(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            SetWorkspace(workspace);
        }

        public DimensionTemplateAuthoringWorkspace Workspace { get; private set; }

        public DimensionTemplateCustomizerInteractionRequest LastInteractionRequest { get; private set; }

        public DimensionTemplateCustomizerInteractionState InteractionState { get; private set; }

        public DimensionTemplateCustomizerFieldEditExecutionResult LastExecutionResult { get; private set; }

        public bool Dirty { get; private set; }

        public int Version { get; private set; }

        public int AppliedEditCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerSessionEvent> History
        {
            get
            {
                return new List<DimensionTemplateCustomizerSessionEvent>(history);
            }
        }

        public DimensionTemplateCustomizerSessionStatus Status
        {
            get
            {
                if (Workspace == null)
                {
                    return DimensionTemplateCustomizerSessionStatus.MissingWorkspace;
                }

                return Dirty
                    ? DimensionTemplateCustomizerSessionStatus.Dirty
                    : DimensionTemplateCustomizerSessionStatus.Clean;
            }
        }

        public DimensionGenerationBudgetSummary GenerationBudget
        {
            get
            {
                return Workspace == null
                    ? DimensionGenerationBudgetUtility.BuildSummary(null)
                    : Workspace.GenerationBudget;
            }
        }

        public DimensionGenerationBudgetGuidanceCatalog GenerationBudgetGuidance
        {
            get
            {
                return Workspace == null
                    ? DimensionGenerationBudgetGuidanceUtility.Build(GenerationBudget)
                    : Workspace.GenerationBudgetGuidance;
            }
        }

        public DimensionTemplateCustomizerSessionSnapshot GetSnapshot()
        {
            return new DimensionTemplateCustomizerSessionSnapshot(
                Status,
                Version,
                Dirty,
                AppliedEditCount,
                Workspace,
                LastInteractionRequest,
                InteractionState,
                LastExecutionResult,
                History);
        }

        public void SetWorkspace(DimensionTemplateAuthoringWorkspace workspace)
        {
            Workspace = workspace;
            LastExecutionResult = null;
            Dirty = false;
            AppliedEditCount = 0;
            Version++;
            Refresh(DefaultInteractionRequest(workspace));
            AddEvent(
                DimensionTemplateCustomizerSessionEventKind.WorkspaceAssigned,
                workspace == null ? "workspace-missing" : "workspace-assigned",
                workspace == null
                    ? "No dimension authoring workspace is assigned."
                    : "Dimension authoring workspace assigned.",
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null);
        }

        public DimensionTemplateCustomizerInteractionState Refresh(
            DimensionTemplateCustomizerInteractionRequest request)
        {
            LastInteractionRequest = request ?? DefaultInteractionRequest(Workspace);
            InteractionState = DimensionTemplateCustomizerInteractionStateUtility.Build(
                Workspace,
                LastInteractionRequest);
            AddEvent(
                DimensionTemplateCustomizerSessionEventKind.InteractionRefreshed,
                "interaction-refreshed",
                "Customizer interaction state refreshed.",
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null);
            return InteractionState;
        }

        public DimensionTemplateCustomizerFieldOptionCatalog GetFieldOptionCatalog(
            DimensionTemplateCustomizerFocusRequest request)
        {
            return Workspace == null
                ? DimensionTemplateCustomizerFieldOptionCatalogUtility.Build(null, request)
                : Workspace.GetFieldOptionCatalog(request);
        }

        public DimensionTemplateCustomizerFieldOptionCatalog GetFieldOptionCatalog(
            DimensionTemplateCustomizerFocusState focus)
        {
            return Workspace == null
                ? DimensionTemplateCustomizerFieldOptionCatalogUtility.Build(null, focus)
                : Workspace.GetFieldOptionCatalog(focus);
        }

        public DimensionTemplateCustomizerFieldOptionSet GetFieldOptions(
            DimensionTemplateCustomizerFocusRequest request,
            string fieldId)
        {
            return Workspace == null
                ? DimensionTemplateCustomizerFieldOptionCatalogUtility.BuildFieldOptions(
                    null,
                    request,
                    fieldId)
                : Workspace.GetFieldOptions(request, fieldId);
        }

        public DimensionTemplateCustomizerFieldOptionSet GetFieldOptions(
            DimensionTemplateCustomizerFocusState focus,
            string fieldId)
        {
            return Workspace == null
                ? DimensionTemplateCustomizerFieldOptionCatalogUtility.BuildFieldOptions(
                    null,
                    focus,
                    fieldId)
                : Workspace.GetFieldOptions(focus, fieldId);
        }

        public DimensionTemplateCustomizerIssueResolutionCatalog GetIssueResolutionCatalog()
        {
            return Workspace == null
                ? DimensionTemplateCustomizerIssueResolutionUtility.Build(null)
                : Workspace.GetIssueResolutionCatalog();
        }

        public DimensionTemplateCustomizerIssueResolutionCatalog GetIssueResolutionCatalog(
            string sectionId)
        {
            return Workspace == null
                ? DimensionTemplateCustomizerIssueResolutionUtility.Build(null, sectionId)
                : Workspace.GetIssueResolutionCatalog(sectionId);
        }

        public DimensionTemplateCustomizerIssueRepairCatalog GetIssueRepairCatalog()
        {
            return Workspace == null
                ? DimensionTemplateCustomizerIssueRepairPlanUtility.Build(null)
                : Workspace.GetIssueRepairCatalog();
        }

        public DimensionTemplateCustomizerIssueRepairCatalog GetIssueRepairCatalog(
            string sectionId)
        {
            return Workspace == null
                ? DimensionTemplateCustomizerIssueRepairPlanUtility.Build(null, sectionId)
                : Workspace.GetIssueRepairCatalog(sectionId);
        }

        public DimensionTemplateCustomizerFieldEditPreview PreviewFieldEdit(
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            DimensionTemplateCustomizerFieldEditPreview preview =
                DimensionTemplateCustomizerFieldEditPreviewUtility.Preview(Workspace, request);
            AddEvent(
                DimensionTemplateCustomizerSessionEventKind.FieldEditPreviewed,
                preview == null ? "field-preview-missing" : preview.Code,
                preview == null ? "No field edit preview was created." : preview.Message,
                request == null ? 0 : 1,
                0,
                preview != null && preview.CanApply ? 0 : 1,
                0,
                preview,
                null,
                null,
                null);
            return preview;
        }

        public DimensionTemplateCustomizerFieldEditBatchPreview PreviewFieldEditBatch(
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            DimensionTemplateCustomizerFieldEditBatchPreview preview =
                DimensionTemplateCustomizerFieldEditBatchUtility.Preview(Workspace, request);
            AddEvent(
                DimensionTemplateCustomizerSessionEventKind.FieldEditBatchPreviewed,
                preview == null ? "field-batch-preview-missing" : preview.Code,
                preview == null ? "No field edit batch preview was created." : preview.Message,
                preview == null ? 0 : preview.TotalCount,
                0,
                preview == null ? 0 : preview.BlockedCount,
                0,
                null,
                preview,
                null,
                null);
            return preview;
        }

        public DimensionTemplateCustomizerFieldEditExecutionResult ApplyFieldEdit(
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            DimensionTemplateCustomizerFieldEditExecutionResult result =
                DimensionTemplateCustomizerFieldEditExecutor.Apply(Workspace, request);
            ApplyExecutionResult(
                DimensionTemplateCustomizerSessionEventKind.FieldEditApplied,
                result);
            return result;
        }

        public DimensionTemplateCustomizerFieldEditExecutionResult ApplyFieldEditBatch(
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            DimensionTemplateCustomizerFieldEditExecutionResult result =
                DimensionTemplateCustomizerFieldEditExecutor.ApplyBatch(Workspace, request);
            ApplyExecutionResult(
                DimensionTemplateCustomizerSessionEventKind.FieldEditBatchApplied,
                result);
            return result;
        }

        public DimensionTemplateCustomizerPreparedCommand PrepareCommand(
            DimensionTemplateCustomizerCommandRequest request)
        {
            return DimensionTemplateCustomizerCommandRequestUtility.Prepare(Workspace, request);
        }

        public DimensionTemplateCustomizerCommandPipeline GetCommandPipeline(
            DimensionTemplateCustomizerCommandRequest request)
        {
            return DimensionTemplateCustomizerCommandPipelineUtility.Build(
                PrepareCommand(request));
        }

        public DimensionTemplateCustomizerExecutionPlan PreviewExecutionPlan(
            DimensionTemplateCustomizerExecutionRequest request)
        {
            DimensionTemplateCustomizerExecutionPlan plan =
                DimensionTemplateCustomizerExecutionPlanUtility.Build(Workspace, request);
            AddEvent(
                DimensionTemplateCustomizerSessionEventKind.ExecutionPlanned,
                plan == null ? "execution-plan-missing" : plan.Code,
                plan == null ? "No execution plan was created." : plan.Message,
                0,
                0,
                plan != null && plan.CanRun ? 0 : 1,
                0,
                null,
                null,
                plan,
                null);
            return plan;
        }

        public void MarkSaved()
        {
            Dirty = false;
            Version++;
            AddEvent(
                DimensionTemplateCustomizerSessionEventKind.MarkedSaved,
                "session-marked-saved",
                "Customizer session dirty state cleared by the caller.",
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null);
        }

        public void ResetHistory()
        {
            history.Clear();
            Version++;
            AddEvent(
                DimensionTemplateCustomizerSessionEventKind.HistoryReset,
                "session-history-reset",
                "Customizer session history reset.",
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null);
        }

        private void ApplyExecutionResult(
            DimensionTemplateCustomizerSessionEventKind kind,
            DimensionTemplateCustomizerFieldEditExecutionResult result)
        {
            LastExecutionResult = result;
            if (result != null && result.Workspace != null)
            {
                Workspace = result.Workspace;
            }

            if (result != null && result.AppliedCount > 0)
            {
                Dirty = true;
                AppliedEditCount += result.AppliedCount;
            }

            Version++;
            Refresh(LastInteractionRequest);
            AddEvent(
                kind,
                result == null ? "field-execution-missing" : result.Code,
                result == null ? "No field edit execution result was created." : result.Message,
                result == null ? 0 : result.RequestedCount,
                result == null ? 0 : result.AppliedCount,
                result == null ? 0 : result.BlockedCount,
                result == null ? 0 : result.UnsupportedCount,
                null,
                null,
                null,
                result);
        }

        private void AddEvent(
            DimensionTemplateCustomizerSessionEventKind kind,
            string code,
            string message,
            int requestedCount,
            int appliedCount,
            int blockedCount,
            int unsupportedCount,
            DimensionTemplateCustomizerFieldEditPreview fieldEditPreview,
            DimensionTemplateCustomizerFieldEditBatchPreview fieldEditBatchPreview,
            DimensionTemplateCustomizerExecutionPlan executionPlan,
            DimensionTemplateCustomizerFieldEditExecutionResult executionResult)
        {
            history.Add(new DimensionTemplateCustomizerSessionEvent(
                Version,
                kind,
                code,
                message,
                requestedCount,
                appliedCount,
                blockedCount,
                unsupportedCount,
                Dirty,
                InteractionState,
                fieldEditPreview,
                fieldEditBatchPreview,
                executionPlan,
                executionResult));

            while (history.Count > MaxHistoryEntries)
            {
                history.RemoveAt(0);
            }
        }

        private static DimensionTemplateCustomizerInteractionRequest DefaultInteractionRequest(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            string sectionId = workspace == null
                ? "overview"
                : workspace.Navigation.ActiveSectionId;
            return DimensionTemplateCustomizerInteractionRequest.ForSection(sectionId);
        }
    }
}
