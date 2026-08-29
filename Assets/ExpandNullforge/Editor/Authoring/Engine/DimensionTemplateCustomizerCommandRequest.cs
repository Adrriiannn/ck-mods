using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerCommandRequestMode
    {
        PreviewOnly = 0,
        PrepareExecution = 1
    }

    public enum DimensionTemplateCustomizerPreparedCommandState
    {
        MissingWorkspace = 0,
        MissingAction = 1,
        PreviewReady = 2,
        ReadyToRun = 3,
        NeedsSelection = 4,
        NeedsConfirmation = 5,
        NeedsInput = 6,
        NeedsEditorImplementation = 7,
        MutationNotAllowed = 8,
        Blocked = 9
    }

    public sealed class DimensionTemplateCustomizerCommandRequest
    {
        public DimensionTemplateCustomizerCommandRequest(
            string sectionId,
            string actionId,
            DimensionTemplateCustomizerCommandRequestMode mode,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            bool confirmed,
            bool allowAssetMutation,
            bool allowRuntimeMutation,
            string inputValue)
        {
            SectionId = sectionId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Mode = mode;
            FocusRequest = focusRequest;
            Confirmed = confirmed;
            AllowAssetMutation = allowAssetMutation;
            AllowRuntimeMutation = allowRuntimeMutation;
            InputValue = inputValue ?? string.Empty;
        }

        public string SectionId { get; private set; }

        public string ActionId { get; private set; }

        public DimensionTemplateCustomizerCommandRequestMode Mode { get; private set; }

        public DimensionTemplateCustomizerFocusRequest FocusRequest { get; private set; }

        public bool Confirmed { get; private set; }

        public bool AllowAssetMutation { get; private set; }

        public bool AllowRuntimeMutation { get; private set; }

        public string InputValue { get; private set; }

        public static DimensionTemplateCustomizerCommandRequest Preview(
            string sectionId,
            string actionId,
            DimensionTemplateCustomizerFocusRequest focusRequest)
        {
            return new DimensionTemplateCustomizerCommandRequest(
                sectionId,
                actionId,
                DimensionTemplateCustomizerCommandRequestMode.PreviewOnly,
                focusRequest,
                false,
                false,
                false,
                string.Empty);
        }

        public static DimensionTemplateCustomizerCommandRequest Prepare(
            string sectionId,
            string actionId,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            bool confirmed,
            bool allowAssetMutation,
            bool allowRuntimeMutation,
            string inputValue)
        {
            return new DimensionTemplateCustomizerCommandRequest(
                sectionId,
                actionId,
                DimensionTemplateCustomizerCommandRequestMode.PrepareExecution,
                focusRequest,
                confirmed,
                allowAssetMutation,
                allowRuntimeMutation,
                inputValue);
        }
    }

    public sealed class DimensionTemplateCustomizerPreparedCommand
    {
        public DimensionTemplateCustomizerPreparedCommand(
            DimensionTemplateCustomizerPreparedCommandState state,
            string code,
            string message,
            bool canRun,
            bool canPreview,
            bool requiresConfirmation,
            bool requiresInput,
            bool mutatesAssets,
            bool touchesRuntimeState,
            string inputHint,
            string expectedResult,
            DimensionTemplateCustomizerCommand command,
            DimensionTemplateCustomizerCommandPlan commandPlan)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanRun = canRun;
            CanPreview = canPreview;
            RequiresConfirmation = requiresConfirmation;
            RequiresInput = requiresInput;
            MutatesAssets = mutatesAssets;
            TouchesRuntimeState = touchesRuntimeState;
            InputHint = inputHint ?? string.Empty;
            ExpectedResult = expectedResult ?? string.Empty;
            Command = command;
            CommandPlan = commandPlan;
        }

        public DimensionTemplateCustomizerPreparedCommandState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanRun { get; private set; }

        public bool CanPreview { get; private set; }

        public bool RequiresConfirmation { get; private set; }

        public bool RequiresInput { get; private set; }

        public bool MutatesAssets { get; private set; }

        public bool TouchesRuntimeState { get; private set; }

        public string InputHint { get; private set; }

        public string ExpectedResult { get; private set; }

        public DimensionTemplateCustomizerCommand Command { get; private set; }

        public DimensionTemplateCustomizerCommandPlan CommandPlan { get; private set; }
    }

    public static class DimensionTemplateCustomizerCommandRequestUtility
    {
        public static DimensionTemplateCustomizerPreparedCommand Prepare(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerCommandRequest request)
        {
            if (workspace == null)
            {
                return CreateMissingWorkspaceResult(request);
            }

            string sectionId = ResolveSectionId(workspace, request);
            DimensionTemplateCustomizerFocusRequest focusRequest =
                ResolveFocusRequest(sectionId, request);
            DimensionTemplateCustomizerCommandPlan plan =
                workspace.GetCommandPlan(sectionId, focusRequest);
            DimensionTemplateCustomizerCommand command =
                ResolveCommand(plan, request == null ? string.Empty : request.ActionId);

            if (command == null)
            {
                return new DimensionTemplateCustomizerPreparedCommand(
                    DimensionTemplateCustomizerPreparedCommandState.MissingAction,
                    "action-missing",
                    "The requested customizer action was not found in the active command plan.",
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    string.Empty,
                    string.Empty,
                    null,
                    plan);
            }

            return PrepareCommand(command, plan, request);
        }

        private static DimensionTemplateCustomizerPreparedCommand CreateMissingWorkspaceResult(
            DimensionTemplateCustomizerCommandRequest request)
        {
            DimensionTemplateCustomizerCommandPlan plan =
                DimensionTemplateCustomizerCommandPlanUtility.Build(
                    null,
                    request == null ? "overview" : request.SectionId,
                    request == null
                        ? DimensionTemplateCustomizerFocusRequest.ForSection("overview")
                        : request.FocusRequest);

            return new DimensionTemplateCustomizerPreparedCommand(
                DimensionTemplateCustomizerPreparedCommandState.MissingWorkspace,
                "workspace-missing",
                "Select or create a Dimension Asset before running customizer commands.",
                false,
                plan != null && plan.CommandCount > 0,
                false,
                false,
                false,
                false,
                string.Empty,
                string.Empty,
                plan == null ? null : plan.PrimaryCommand,
                plan);
        }

        private static DimensionTemplateCustomizerPreparedCommand PrepareCommand(
            DimensionTemplateCustomizerCommand command,
            DimensionTemplateCustomizerCommandPlan plan,
            DimensionTemplateCustomizerCommandRequest request)
        {
            DimensionTemplateCustomizerActionDescriptor action = command.Action;
            DimensionTemplateCustomizerActionPreview preview = command.Preview;
            bool previewOnly = request == null ||
                request.Mode == DimensionTemplateCustomizerCommandRequestMode.PreviewOnly;
            bool hasInput = request != null && !string.IsNullOrEmpty(request.InputValue);
            bool requiresInput = !string.IsNullOrEmpty(preview.InputHint) && !hasInput;
            bool mutatesAssets = action.MutatesAssets;
            bool touchesRuntime = action.TouchesRuntimeState;

            if (previewOnly)
            {
                return new DimensionTemplateCustomizerPreparedCommand(
                    DimensionTemplateCustomizerPreparedCommandState.PreviewReady,
                    "preview-ready",
                    preview.Summary,
                    false,
                    true,
                    action.RequiresConfirmation,
                    requiresInput,
                    mutatesAssets,
                    touchesRuntime,
                    preview.InputHint,
                    preview.ExpectedResult,
                    command,
                    plan);
            }

            DimensionTemplateCustomizerPreparedCommandState blockedState;
            string blockedCode;
            string blockedMessage;
            if (!CanRun(command, request, requiresInput, out blockedState, out blockedCode, out blockedMessage))
            {
                return new DimensionTemplateCustomizerPreparedCommand(
                    blockedState,
                    blockedCode,
                    blockedMessage,
                    false,
                    true,
                    action.RequiresConfirmation,
                    requiresInput,
                    mutatesAssets,
                    touchesRuntime,
                    preview.InputHint,
                    preview.ExpectedResult,
                    command,
                    plan);
            }

            return new DimensionTemplateCustomizerPreparedCommand(
                DimensionTemplateCustomizerPreparedCommandState.ReadyToRun,
                "ready-to-run",
                string.IsNullOrEmpty(preview.ExpectedResult)
                    ? "The command is ready for the editor implementation to run."
                    : preview.ExpectedResult,
                true,
                true,
                false,
                false,
                mutatesAssets,
                touchesRuntime,
                preview.InputHint,
                preview.ExpectedResult,
                command,
                plan);
        }

        private static bool CanRun(
            DimensionTemplateCustomizerCommand command,
            DimensionTemplateCustomizerCommandRequest request,
            bool requiresInput,
            out DimensionTemplateCustomizerPreparedCommandState state,
            out string code,
            out string message)
        {
            state = DimensionTemplateCustomizerPreparedCommandState.ReadyToRun;
            code = "ready-to-run";
            message = string.Empty;

            if (command.State == DimensionTemplateCustomizerCommandState.NeedsImplementation)
            {
                state = DimensionTemplateCustomizerPreparedCommandState.NeedsEditorImplementation;
                code = "needs-editor-implementation";
                message = command.BlockedReason;
                return false;
            }

            if (command.State == DimensionTemplateCustomizerCommandState.NeedsSelection)
            {
                state = DimensionTemplateCustomizerPreparedCommandState.NeedsSelection;
                code = "needs-selection";
                message = command.BlockedReason;
                return false;
            }

            if (command.State == DimensionTemplateCustomizerCommandState.Blocked ||
                command.State == DimensionTemplateCustomizerCommandState.Disabled)
            {
                state = DimensionTemplateCustomizerPreparedCommandState.Blocked;
                code = command.State == DimensionTemplateCustomizerCommandState.Disabled
                    ? "action-disabled"
                    : "action-blocked";
                message = command.BlockedReason;
                return false;
            }

            if (command.Action.RequiresConfirmation && !request.Confirmed)
            {
                state = DimensionTemplateCustomizerPreparedCommandState.NeedsConfirmation;
                code = "needs-confirmation";
                message = string.IsNullOrEmpty(command.Preview.SafetyMessage)
                    ? "Confirm this command before running it."
                    : command.Preview.SafetyMessage;
                return false;
            }

            if (requiresInput)
            {
                state = DimensionTemplateCustomizerPreparedCommandState.NeedsInput;
                code = "needs-input";
                message = command.Preview.InputHint;
                return false;
            }

            if (command.Action.MutatesAssets && !request.AllowAssetMutation)
            {
                state = DimensionTemplateCustomizerPreparedCommandState.MutationNotAllowed;
                code = "asset-mutation-not-allowed";
                message = "This command would mutate authoring assets, but asset mutation was not allowed by the request.";
                return false;
            }

            if (command.Action.TouchesRuntimeState && !request.AllowRuntimeMutation)
            {
                state = DimensionTemplateCustomizerPreparedCommandState.MutationNotAllowed;
                code = "runtime-mutation-not-allowed";
                message = "This command would touch runtime state, but runtime mutation was not allowed by the request.";
                return false;
            }

            return true;
        }

        private static string ResolveSectionId(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerCommandRequest request)
        {
            if (request != null && !string.IsNullOrEmpty(request.SectionId))
            {
                return request.SectionId;
            }

            return workspace.Navigation.ActiveSectionId;
        }

        private static DimensionTemplateCustomizerFocusRequest ResolveFocusRequest(
            string sectionId,
            DimensionTemplateCustomizerCommandRequest request)
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

        private static DimensionTemplateCustomizerCommand ResolveCommand(
            DimensionTemplateCustomizerCommandPlan plan,
            string actionId)
        {
            if (plan == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                return plan.PrimaryCommand;
            }

            IReadOnlyList<DimensionTemplateCustomizerCommand> commands = plan.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].CommandId == actionId)
                {
                    return commands[i];
                }
            }

            return null;
        }
    }
}
