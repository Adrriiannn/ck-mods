using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerExecutionIntent
    {
        Unknown = 0,
        SaveAssets = 1,
        ValidateManifest = 2,
        ExportManifest = 3,
        ApplyManifest = 4
    }

    public enum DimensionTemplateCustomizerExecutionState
    {
        MissingWorkspace = 0,
        Blocked = 1,
        NeedsConfirmation = 2,
        NeedsEditorImplementation = 3,
        NeedsRuntimeService = 4,
        Ready = 5
    }

    public enum DimensionTemplateCustomizerExecutionStepState
    {
        Ready = 0,
        Blocked = 1,
        NeedsConfirmation = 2,
        NeedsEditorImplementation = 3,
        NeedsRuntimeService = 4,
        Skipped = 5
    }

    public sealed class DimensionTemplateCustomizerExecutionRequest
    {
        public DimensionTemplateCustomizerExecutionRequest(
            DimensionTemplateCustomizerExecutionIntent intent)
            : this(intent, false, string.Empty)
        {
        }

        public DimensionTemplateCustomizerExecutionRequest(
            DimensionTemplateCustomizerExecutionIntent intent,
            bool confirmed,
            string reason)
        {
            Intent = intent;
            Confirmed = confirmed;
            Reason = reason ?? string.Empty;
        }

        public DimensionTemplateCustomizerExecutionIntent Intent { get; private set; }

        public bool Confirmed { get; private set; }

        public string Reason { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerExecutionStep
    {
        public DimensionTemplateCustomizerExecutionStep(
            string stepId,
            string label,
            string message,
            DimensionTemplateCustomizerExecutionStepState state,
            bool blocksExecution)
        {
            StepId = stepId ?? string.Empty;
            Label = label ?? string.Empty;
            Message = message ?? string.Empty;
            State = state;
            BlocksExecution = blocksExecution;
        }

        public string StepId { get; private set; }

        public string Label { get; private set; }

        public string Message { get; private set; }

        public DimensionTemplateCustomizerExecutionStepState State { get; private set; }

        public bool BlocksExecution { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerExecutionPlan
    {
        public DimensionTemplateCustomizerExecutionPlan(
            DimensionTemplateCustomizerExecutionIntent intent,
            DimensionTemplateCustomizerExecutionState state,
            string code,
            string message,
            bool canRun,
            bool mutatesAssets,
            bool touchesRuntimeState,
            bool requiresConfirmation,
            bool requiresEditorImplementation,
            bool requiresRuntimeService,
            int requiredSaveEntryCount,
            int optionalSaveEntryCount,
            int manifestContentCount,
            DimensionContentManifestRequest manifestRequest,
            DimensionTemplateAssetSavePlan savePlan,
            DimensionTemplateManifestExportPreview manifestPreview,
            IReadOnlyList<DimensionTemplateCustomizerExecutionStep> steps)
        {
            Intent = intent;
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanRun = canRun;
            MutatesAssets = mutatesAssets;
            TouchesRuntimeState = touchesRuntimeState;
            RequiresConfirmation = requiresConfirmation;
            RequiresEditorImplementation = requiresEditorImplementation;
            RequiresRuntimeService = requiresRuntimeService;
            RequiredSaveEntryCount = requiredSaveEntryCount < 0 ? 0 : requiredSaveEntryCount;
            OptionalSaveEntryCount = optionalSaveEntryCount < 0 ? 0 : optionalSaveEntryCount;
            ManifestContentCount = manifestContentCount < 0 ? 0 : manifestContentCount;
            ManifestRequest = manifestRequest;
            SavePlan = savePlan;
            ManifestPreview = manifestPreview;
            Steps = steps ?? new List<DimensionTemplateCustomizerExecutionStep>();
        }

        public DimensionTemplateCustomizerExecutionIntent Intent { get; private set; }

        public DimensionTemplateCustomizerExecutionState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanRun { get; private set; }

        public bool MutatesAssets { get; private set; }

        public bool TouchesRuntimeState { get; private set; }

        public bool RequiresConfirmation { get; private set; }

        public bool RequiresEditorImplementation { get; private set; }

        public bool RequiresRuntimeService { get; private set; }

        public int RequiredSaveEntryCount { get; private set; }

        public int OptionalSaveEntryCount { get; private set; }

        public int ManifestContentCount { get; private set; }

        public DimensionContentManifestRequest ManifestRequest { get; private set; }

        public DimensionTemplateAssetSavePlan SavePlan { get; private set; }

        public DimensionTemplateManifestExportPreview ManifestPreview { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerExecutionStep> Steps { get; private set; }
    }

    public static class DimensionTemplateCustomizerExecutionPlanUtility
    {
        public static DimensionTemplateCustomizerExecutionPlan Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerExecutionRequest request)
        {
            DimensionTemplateCustomizerExecutionIntent intent =
                request == null ? DimensionTemplateCustomizerExecutionIntent.Unknown : request.Intent;

            if (workspace == null)
            {
                return CreateMissingWorkspacePlan(intent);
            }

            switch (intent)
            {
                case DimensionTemplateCustomizerExecutionIntent.SaveAssets:
                    return BuildSaveAssetsPlan(workspace, request);
                case DimensionTemplateCustomizerExecutionIntent.ValidateManifest:
                    return BuildManifestPlan(
                        workspace,
                        request,
                        DimensionTemplateCustomizerExecutionIntent.ValidateManifest);
                case DimensionTemplateCustomizerExecutionIntent.ExportManifest:
                    return BuildManifestPlan(
                        workspace,
                        request,
                        DimensionTemplateCustomizerExecutionIntent.ExportManifest);
                case DimensionTemplateCustomizerExecutionIntent.ApplyManifest:
                    return BuildManifestPlan(
                        workspace,
                        request,
                        DimensionTemplateCustomizerExecutionIntent.ApplyManifest);
                default:
                    return CreateBlockedPlan(
                        workspace,
                        intent,
                        "execution-intent-unknown",
                        "Choose an authoring operation before executing it.");
            }
        }

        private static DimensionTemplateCustomizerExecutionPlan BuildSaveAssetsPlan(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerExecutionRequest request)
        {
            DimensionTemplateAssetSavePlan savePlan = workspace.SavePlan;
            int required;
            int optional;
            CountSaveEntries(savePlan, out required, out optional);

            List<DimensionTemplateCustomizerExecutionStep> steps =
                new List<DimensionTemplateCustomizerExecutionStep>();
            AddStep(
                steps,
                "resolve-save-plan",
                "Resolve save plan",
                savePlan == null
                    ? "No starter asset save plan is available."
                    : savePlan.Message,
                savePlan == null
                    ? DimensionTemplateCustomizerExecutionStepState.Blocked
                    : DimensionTemplateCustomizerExecutionStepState.Ready,
                savePlan == null);
            AddStep(
                steps,
                "check-required-assets",
                "Check required assets",
                required > 0
                    ? required + " required asset record(s) are ready for editor save."
                    : "No required asset records are available to save.",
                required > 0
                    ? DimensionTemplateCustomizerExecutionStepState.Ready
                    : DimensionTemplateCustomizerExecutionStepState.Blocked,
                required == 0);
            AddStep(
                steps,
                "editor-save-implementation",
                "Editor save implementation",
                "The source framework has produced the save plan; the Unity editor layer must create folders and save assets.",
                DimensionTemplateCustomizerExecutionStepState.NeedsEditorImplementation,
                true);

            return CreatePlan(
                DimensionTemplateCustomizerExecutionIntent.SaveAssets,
                DimensionTemplateCustomizerExecutionState.NeedsEditorImplementation,
                "save-assets-needs-editor",
                "Asset saving is planned but must be performed by editor-only code.",
                false,
                true,
                false,
                false,
                true,
                false,
                required,
                optional,
                0,
                default(DimensionContentManifestRequest),
                savePlan,
                workspace.ManifestExportPreview,
                steps);
        }

        private static DimensionTemplateCustomizerExecutionPlan BuildManifestPlan(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerExecutionRequest request,
            DimensionTemplateCustomizerExecutionIntent intent)
        {
            DimensionTemplateManifestExportPreview preview = workspace.ManifestExportPreview;
            List<DimensionTemplateCustomizerExecutionStep> steps =
                new List<DimensionTemplateCustomizerExecutionStep>();

            bool manifestReady = preview.ManifestBuilt && preview.ReadyForManifestExport;
            bool validationReady = manifestReady && preview.ReadyForValidation;
            bool applyReady = manifestReady && preview.ReadyForApply;
            bool confirmed = request != null && request.Confirmed;

            AddStep(
                steps,
                "build-manifest-preview",
                "Build manifest preview",
                preview.Message,
                preview.ManifestBuilt
                    ? DimensionTemplateCustomizerExecutionStepState.Ready
                    : DimensionTemplateCustomizerExecutionStepState.Blocked,
                !preview.ManifestBuilt);
            AddStep(
                steps,
                "check-export-readiness",
                "Check export readiness",
                manifestReady
                    ? "Manifest has no blocking export issues."
                    : "Manifest export is blocked by authoring or validation issues.",
                manifestReady
                    ? DimensionTemplateCustomizerExecutionStepState.Ready
                    : DimensionTemplateCustomizerExecutionStepState.Blocked,
                !manifestReady);

            if (intent == DimensionTemplateCustomizerExecutionIntent.ValidateManifest)
            {
                return BuildValidateManifestPlan(workspace, preview, validationReady, steps);
            }

            if (intent == DimensionTemplateCustomizerExecutionIntent.ExportManifest)
            {
                return BuildExportManifestPlan(workspace, preview, manifestReady, confirmed, steps);
            }

            return BuildApplyManifestPlan(workspace, preview, applyReady, confirmed, steps);
        }

        private static DimensionTemplateCustomizerExecutionPlan BuildValidateManifestPlan(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview preview,
            bool validationReady,
            List<DimensionTemplateCustomizerExecutionStep> steps)
        {
            AddStep(
                steps,
                "validate-manifest-request",
                "Prepare validation request",
                validationReady
                    ? "Validation request is ready for a runtime manifest service."
                    : "Validation request is unavailable until manifest export blockers are resolved.",
                validationReady
                    ? DimensionTemplateCustomizerExecutionStepState.Ready
                    : DimensionTemplateCustomizerExecutionStepState.Blocked,
                !validationReady);

            return CreatePlan(
                DimensionTemplateCustomizerExecutionIntent.ValidateManifest,
                validationReady
                    ? DimensionTemplateCustomizerExecutionState.Ready
                    : DimensionTemplateCustomizerExecutionState.Blocked,
                validationReady ? "validate-manifest-ready" : "validate-manifest-blocked",
                validationReady
                    ? "Manifest validation request is ready."
                    : "Manifest validation is blocked.",
                validationReady,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                CountManifestContent(preview),
                preview.ValidationRequest,
                workspace.SavePlan,
                preview,
                steps);
        }

        private static DimensionTemplateCustomizerExecutionPlan BuildExportManifestPlan(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview preview,
            bool manifestReady,
            bool confirmed,
            List<DimensionTemplateCustomizerExecutionStep> steps)
        {
            AddStep(
                steps,
                "confirm-export",
                "Confirm export",
                confirmed
                    ? "Export confirmation supplied."
                    : "Manifest export should be confirmed before editor code writes output.",
                confirmed
                    ? DimensionTemplateCustomizerExecutionStepState.Ready
                    : DimensionTemplateCustomizerExecutionStepState.NeedsConfirmation,
                !confirmed);
            AddStep(
                steps,
                "editor-export-implementation",
                "Editor export implementation",
                "The source framework can package the manifest; the Unity editor layer must write the exported asset/file.",
                DimensionTemplateCustomizerExecutionStepState.NeedsEditorImplementation,
                true);

            return CreatePlan(
                DimensionTemplateCustomizerExecutionIntent.ExportManifest,
                !manifestReady
                    ? DimensionTemplateCustomizerExecutionState.Blocked
                    : !confirmed
                        ? DimensionTemplateCustomizerExecutionState.NeedsConfirmation
                        : DimensionTemplateCustomizerExecutionState.NeedsEditorImplementation,
                !manifestReady
                    ? "export-manifest-blocked"
                    : !confirmed
                        ? "export-manifest-needs-confirmation"
                        : "export-manifest-needs-editor",
                !manifestReady
                    ? "Manifest export is blocked."
                    : !confirmed
                        ? "Manifest export needs confirmation."
                        : "Manifest export is ready for editor-only write code.",
                false,
                true,
                false,
                !confirmed,
                true,
                false,
                0,
                0,
                CountManifestContent(preview),
                preview.ValidationRequest,
                workspace.SavePlan,
                preview,
                steps);
        }

        private static DimensionTemplateCustomizerExecutionPlan BuildApplyManifestPlan(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview preview,
            bool applyReady,
            bool confirmed,
            List<DimensionTemplateCustomizerExecutionStep> steps)
        {
            AddStep(
                steps,
                "confirm-apply",
                "Confirm runtime apply",
                confirmed
                    ? "Runtime apply confirmation supplied."
                    : "Applying a manifest mutates registered runtime content and must be confirmed.",
                confirmed
                    ? DimensionTemplateCustomizerExecutionStepState.Ready
                    : DimensionTemplateCustomizerExecutionStepState.NeedsConfirmation,
                !confirmed);
            AddStep(
                steps,
                "runtime-manifest-service",
                "Runtime manifest service",
                "The execution plan contains the request; the caller must pass it to a manifest service instance.",
                DimensionTemplateCustomizerExecutionStepState.NeedsRuntimeService,
                true);

            return CreatePlan(
                DimensionTemplateCustomizerExecutionIntent.ApplyManifest,
                !applyReady
                    ? DimensionTemplateCustomizerExecutionState.Blocked
                    : !confirmed
                        ? DimensionTemplateCustomizerExecutionState.NeedsConfirmation
                        : DimensionTemplateCustomizerExecutionState.NeedsRuntimeService,
                !applyReady
                    ? "apply-manifest-blocked"
                    : !confirmed
                        ? "apply-manifest-needs-confirmation"
                        : "apply-manifest-needs-runtime-service",
                !applyReady
                    ? "Manifest runtime apply is blocked."
                    : !confirmed
                        ? "Manifest runtime apply needs confirmation."
                        : "Manifest runtime apply is ready for a manifest service call.",
                false,
                false,
                true,
                !confirmed,
                false,
                true,
                0,
                0,
                CountManifestContent(preview),
                preview.ApplyRequest,
                workspace.SavePlan,
                preview,
                steps);
        }

        private static DimensionTemplateCustomizerExecutionPlan CreateMissingWorkspacePlan(
            DimensionTemplateCustomizerExecutionIntent intent)
        {
            List<DimensionTemplateCustomizerExecutionStep> steps =
                new List<DimensionTemplateCustomizerExecutionStep>();
            AddStep(
                steps,
                "resolve-workspace",
                "Resolve workspace",
                "Select or create a Dimension Asset before running authoring operations.",
                DimensionTemplateCustomizerExecutionStepState.Blocked,
                true);
            return CreatePlan(
                intent,
                DimensionTemplateCustomizerExecutionState.MissingWorkspace,
                "workspace-missing",
                "No dimension authoring workspace is assigned.",
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                default(DimensionContentManifestRequest),
                null,
                default(DimensionTemplateManifestExportPreview),
                steps);
        }

        private static DimensionTemplateCustomizerExecutionPlan CreateBlockedPlan(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerExecutionIntent intent,
            string code,
            string message)
        {
            List<DimensionTemplateCustomizerExecutionStep> steps =
                new List<DimensionTemplateCustomizerExecutionStep>();
            AddStep(
                steps,
                "resolve-operation",
                "Resolve operation",
                message,
                DimensionTemplateCustomizerExecutionStepState.Blocked,
                true);
            return CreatePlan(
                intent,
                DimensionTemplateCustomizerExecutionState.Blocked,
                code,
                message,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                default(DimensionContentManifestRequest),
                workspace == null ? null : workspace.SavePlan,
                workspace == null
                    ? default(DimensionTemplateManifestExportPreview)
                    : workspace.ManifestExportPreview,
                steps);
        }

        private static DimensionTemplateCustomizerExecutionPlan CreatePlan(
            DimensionTemplateCustomizerExecutionIntent intent,
            DimensionTemplateCustomizerExecutionState state,
            string code,
            string message,
            bool canRun,
            bool mutatesAssets,
            bool touchesRuntimeState,
            bool requiresConfirmation,
            bool requiresEditorImplementation,
            bool requiresRuntimeService,
            int requiredSaveEntryCount,
            int optionalSaveEntryCount,
            int manifestContentCount,
            DimensionContentManifestRequest manifestRequest,
            DimensionTemplateAssetSavePlan savePlan,
            DimensionTemplateManifestExportPreview manifestPreview,
            IReadOnlyList<DimensionTemplateCustomizerExecutionStep> steps)
        {
            return new DimensionTemplateCustomizerExecutionPlan(
                intent,
                state,
                code,
                message,
                canRun,
                mutatesAssets,
                touchesRuntimeState,
                requiresConfirmation,
                requiresEditorImplementation,
                requiresRuntimeService,
                requiredSaveEntryCount,
                optionalSaveEntryCount,
                manifestContentCount,
                manifestRequest,
                savePlan,
                manifestPreview,
                steps);
        }

        private static void AddStep(
            List<DimensionTemplateCustomizerExecutionStep> steps,
            string stepId,
            string label,
            string message,
            DimensionTemplateCustomizerExecutionStepState state,
            bool blocksExecution)
        {
            if (steps == null)
            {
                return;
            }

            steps.Add(new DimensionTemplateCustomizerExecutionStep(
                stepId,
                label,
                message,
                state,
                blocksExecution));
        }

        private static void CountSaveEntries(
            DimensionTemplateAssetSavePlan savePlan,
            out int required,
            out int optional)
        {
            required = 0;
            optional = 0;
            if (savePlan == null || savePlan.Entries == null)
            {
                return;
            }

            for (int i = 0; i < savePlan.Entries.Count; i++)
            {
                if (savePlan.Entries[i].Required)
                {
                    required++;
                }
                else
                {
                    optional++;
                }
            }
        }

        private static int CountManifestContent(
            DimensionTemplateManifestExportPreview preview)
        {
            return preview.ContentPackCount
                + preview.DimensionCount
                + preview.ZoneCount
                + preview.BiomeCount
                + preview.SceneTemplateCount
                + preview.SceneCount
                + preview.SpawnRuleCount
                + preview.GenerationPassCount
                + preview.ResourceNodeCount
                + preview.EnvironmentProfileCount
                + preview.GenerationTableCount
                + preview.GenerationTableEntryCount
                + preview.OwnershipBindingCount
                + preview.AssetReferenceCount;
        }
    }
}
