using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerPipelineStepState
    {
        Pending = 0,
        Ready = 1,
        NeedsInput = 2,
        NeedsSelection = 3,
        NeedsConfirmation = 4,
        NeedsPermission = 5,
        NeedsImplementation = 6,
        Blocked = 7,
        Skipped = 8
    }

    public sealed class DimensionTemplateCustomizerPipelineStep
    {
        public DimensionTemplateCustomizerPipelineStep(
            string stepId,
            string label,
            string message,
            DimensionTemplateCustomizerPipelineStepState state,
            bool blocksRun,
            bool mutatesAssets,
            bool touchesRuntimeState,
            int priority)
        {
            StepId = stepId ?? string.Empty;
            Label = label ?? string.Empty;
            Message = message ?? string.Empty;
            State = state;
            BlocksRun = blocksRun;
            MutatesAssets = mutatesAssets;
            TouchesRuntimeState = touchesRuntimeState;
            Priority = priority < 0 ? 0 : priority;
        }

        public string StepId { get; private set; }

        public string Label { get; private set; }

        public string Message { get; private set; }

        public DimensionTemplateCustomizerPipelineStepState State { get; private set; }

        public bool BlocksRun { get; private set; }

        public bool MutatesAssets { get; private set; }

        public bool TouchesRuntimeState { get; private set; }

        public int Priority { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerCommandPipeline
    {
        public DimensionTemplateCustomizerCommandPipeline(
            string actionId,
            string label,
            bool canRun,
            bool hasBlockingSteps,
            int stepCount,
            int readyCount,
            int blockedCount,
            int permissionCount,
            int implementationCount,
            DimensionTemplateCustomizerPreparedCommand preparedCommand,
            IReadOnlyList<DimensionTemplateCustomizerPipelineStep> steps)
        {
            ActionId = actionId ?? string.Empty;
            Label = label ?? string.Empty;
            CanRun = canRun;
            HasBlockingSteps = hasBlockingSteps;
            StepCount = stepCount < 0 ? 0 : stepCount;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            PermissionCount = permissionCount < 0 ? 0 : permissionCount;
            ImplementationCount = implementationCount < 0 ? 0 : implementationCount;
            PreparedCommand = preparedCommand;
            Steps = steps ?? new List<DimensionTemplateCustomizerPipelineStep>();
        }

        public string ActionId { get; private set; }

        public string Label { get; private set; }

        public bool CanRun { get; private set; }

        public bool HasBlockingSteps { get; private set; }

        public int StepCount { get; private set; }

        public int ReadyCount { get; private set; }

        public int BlockedCount { get; private set; }

        public int PermissionCount { get; private set; }

        public int ImplementationCount { get; private set; }

        public DimensionTemplateCustomizerPreparedCommand PreparedCommand { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerPipelineStep> Steps { get; private set; }
    }

    public static class DimensionTemplateCustomizerCommandPipelineUtility
    {
        public static DimensionTemplateCustomizerCommandPipeline Build(
            DimensionTemplateCustomizerPreparedCommand preparedCommand)
        {
            if (preparedCommand == null || preparedCommand.Command == null)
            {
                return BuildMissingPipeline(preparedCommand);
            }

            List<DimensionTemplateCustomizerPipelineStep> steps =
                new List<DimensionTemplateCustomizerPipelineStep>();
            DimensionTemplateCustomizerCommand command = preparedCommand.Command;
            DimensionTemplateCustomizerActionDescriptor action = command.Action;

            AddFocusStep(steps, command);
            AddInputStep(steps, preparedCommand);
            AddConfirmationStep(steps, preparedCommand, action);
            AddImplementationStep(steps, preparedCommand);
            AddPermissionSteps(steps, preparedCommand, action);
            AddExecutionStep(steps, preparedCommand, action);

            int ready;
            int blocked;
            int permissions;
            int implementations;
            CountSteps(steps, out ready, out blocked, out permissions, out implementations);

            return new DimensionTemplateCustomizerCommandPipeline(
                command.CommandId,
                action.Label,
                preparedCommand.CanRun,
                blocked > 0,
                steps.Count,
                ready,
                blocked,
                permissions,
                implementations,
                preparedCommand,
                steps);
        }

        public static DimensionTemplateCustomizerCommandPipeline Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerCommandRequest request)
        {
            return Build(DimensionTemplateCustomizerCommandRequestUtility.Prepare(workspace, request));
        }

        private static DimensionTemplateCustomizerCommandPipeline BuildMissingPipeline(
            DimensionTemplateCustomizerPreparedCommand preparedCommand)
        {
            List<DimensionTemplateCustomizerPipelineStep> steps =
                new List<DimensionTemplateCustomizerPipelineStep>();
            steps.Add(new DimensionTemplateCustomizerPipelineStep(
                "resolve-command",
                "Resolve command",
                preparedCommand == null
                    ? "No command request has been prepared."
                    : preparedCommand.Message,
                DimensionTemplateCustomizerPipelineStepState.Blocked,
                true,
                false,
                false,
                0));

            return new DimensionTemplateCustomizerCommandPipeline(
                string.Empty,
                string.Empty,
                false,
                true,
                steps.Count,
                0,
                1,
                0,
                0,
                preparedCommand,
                steps);
        }

        private static void AddFocusStep(
            List<DimensionTemplateCustomizerPipelineStep> steps,
            DimensionTemplateCustomizerCommand command)
        {
            if (!command.Action.RequiresSelection)
            {
                steps.Add(new DimensionTemplateCustomizerPipelineStep(
                    "resolve-focus",
                    "Resolve focus",
                    "This command does not require a selected section, biome, or record.",
                    DimensionTemplateCustomizerPipelineStepState.Skipped,
                    false,
                    false,
                    false,
                    0));
                return;
            }

            bool ready = command.MatchesFocus;
            steps.Add(new DimensionTemplateCustomizerPipelineStep(
                "resolve-focus",
                "Resolve focus",
                ready
                    ? "The current focus matches this command."
                    : "Select a matching section, biome, record, or search result first.",
                ready
                    ? DimensionTemplateCustomizerPipelineStepState.Ready
                    : DimensionTemplateCustomizerPipelineStepState.NeedsSelection,
                !ready,
                false,
                false,
                0));
        }

        private static void AddInputStep(
            List<DimensionTemplateCustomizerPipelineStep> steps,
            DimensionTemplateCustomizerPreparedCommand preparedCommand)
        {
            if (string.IsNullOrEmpty(preparedCommand.InputHint))
            {
                steps.Add(new DimensionTemplateCustomizerPipelineStep(
                    "collect-input",
                    "Collect input",
                    "This command does not need additional input.",
                    DimensionTemplateCustomizerPipelineStepState.Skipped,
                    false,
                    false,
                    false,
                    10));
                return;
            }

            bool needsInput =
                preparedCommand.State == DimensionTemplateCustomizerPreparedCommandState.NeedsInput;
            steps.Add(new DimensionTemplateCustomizerPipelineStep(
                "collect-input",
                "Collect input",
                preparedCommand.InputHint,
                needsInput
                    ? DimensionTemplateCustomizerPipelineStepState.NeedsInput
                    : DimensionTemplateCustomizerPipelineStepState.Ready,
                needsInput,
                false,
                false,
                10));
        }

        private static void AddConfirmationStep(
            List<DimensionTemplateCustomizerPipelineStep> steps,
            DimensionTemplateCustomizerPreparedCommand preparedCommand,
            DimensionTemplateCustomizerActionDescriptor action)
        {
            if (!action.RequiresConfirmation)
            {
                steps.Add(new DimensionTemplateCustomizerPipelineStep(
                    "confirm",
                    "Confirm",
                    "This command does not need confirmation.",
                    DimensionTemplateCustomizerPipelineStepState.Skipped,
                    false,
                    false,
                    false,
                    20));
                return;
            }

            bool needsConfirmation =
                preparedCommand.State == DimensionTemplateCustomizerPreparedCommandState.NeedsConfirmation;
            steps.Add(new DimensionTemplateCustomizerPipelineStep(
                "confirm",
                "Confirm",
                preparedCommand.Message,
                needsConfirmation
                    ? DimensionTemplateCustomizerPipelineStepState.NeedsConfirmation
                    : DimensionTemplateCustomizerPipelineStepState.Ready,
                needsConfirmation,
                false,
                false,
                20));
        }

        private static void AddImplementationStep(
            List<DimensionTemplateCustomizerPipelineStep> steps,
            DimensionTemplateCustomizerPreparedCommand preparedCommand)
        {
            bool needsImplementation =
                preparedCommand.State ==
                DimensionTemplateCustomizerPreparedCommandState.NeedsEditorImplementation;
            steps.Add(new DimensionTemplateCustomizerPipelineStep(
                "editor-implementation",
                "Editor implementation",
                needsImplementation
                    ? preparedCommand.Message
                    : "The command preparation layer has a valid implementation route.",
                needsImplementation
                    ? DimensionTemplateCustomizerPipelineStepState.NeedsImplementation
                    : DimensionTemplateCustomizerPipelineStepState.Ready,
                needsImplementation,
                false,
                false,
                30));
        }

        private static void AddPermissionSteps(
            List<DimensionTemplateCustomizerPipelineStep> steps,
            DimensionTemplateCustomizerPreparedCommand preparedCommand,
            DimensionTemplateCustomizerActionDescriptor action)
        {
            AddPermissionStep(
                steps,
                "asset-mutation-permission",
                "Asset mutation permission",
                action.MutatesAssets,
                preparedCommand.State == DimensionTemplateCustomizerPreparedCommandState.MutationNotAllowed &&
                    preparedCommand.Code == "asset-mutation-not-allowed",
                preparedCommand.Message,
                true,
                false,
                40);
            AddPermissionStep(
                steps,
                "runtime-mutation-permission",
                "Runtime mutation permission",
                action.TouchesRuntimeState,
                preparedCommand.State == DimensionTemplateCustomizerPreparedCommandState.MutationNotAllowed &&
                    preparedCommand.Code == "runtime-mutation-not-allowed",
                preparedCommand.Message,
                false,
                true,
                50);
        }

        private static void AddPermissionStep(
            List<DimensionTemplateCustomizerPipelineStep> steps,
            string id,
            string label,
            bool needed,
            bool blocked,
            string blockedMessage,
            bool mutatesAssets,
            bool touchesRuntime,
            int priority)
        {
            if (!needed)
            {
                steps.Add(new DimensionTemplateCustomizerPipelineStep(
                    id,
                    label,
                    "This command does not require this permission.",
                    DimensionTemplateCustomizerPipelineStepState.Skipped,
                    false,
                    false,
                    false,
                    priority));
                return;
            }

            steps.Add(new DimensionTemplateCustomizerPipelineStep(
                id,
                label,
                blocked
                    ? blockedMessage
                    : "Permission has been explicitly allowed by the command request.",
                blocked
                    ? DimensionTemplateCustomizerPipelineStepState.NeedsPermission
                    : DimensionTemplateCustomizerPipelineStepState.Ready,
                blocked,
                mutatesAssets,
                touchesRuntime,
                priority));
        }

        private static void AddExecutionStep(
            List<DimensionTemplateCustomizerPipelineStep> steps,
            DimensionTemplateCustomizerPreparedCommand preparedCommand,
            DimensionTemplateCustomizerActionDescriptor action)
        {
            bool blocked = preparedCommand.State == DimensionTemplateCustomizerPreparedCommandState.Blocked ||
                preparedCommand.State == DimensionTemplateCustomizerPreparedCommandState.MissingAction ||
                preparedCommand.State == DimensionTemplateCustomizerPreparedCommandState.MissingWorkspace;
            steps.Add(new DimensionTemplateCustomizerPipelineStep(
                "run-" + action.Mutability.ToString().ToLowerInvariant(),
                "Run " + action.Mutability,
                blocked
                    ? preparedCommand.Message
                    : preparedCommand.ExpectedResult,
                blocked
                    ? DimensionTemplateCustomizerPipelineStepState.Blocked
                    : DimensionTemplateCustomizerPipelineStepState.Pending,
                blocked,
                action.MutatesAssets,
                action.TouchesRuntimeState,
                60));
        }

        private static void CountSteps(
            IReadOnlyList<DimensionTemplateCustomizerPipelineStep> steps,
            out int ready,
            out int blocked,
            out int permissions,
            out int implementations)
        {
            ready = 0;
            blocked = 0;
            permissions = 0;
            implementations = 0;
            if (steps == null)
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                DimensionTemplateCustomizerPipelineStep step = steps[i];
                if (step.State == DimensionTemplateCustomizerPipelineStepState.Ready)
                {
                    ready++;
                }

                if (step.BlocksRun)
                {
                    blocked++;
                }

                if (step.State == DimensionTemplateCustomizerPipelineStepState.NeedsPermission)
                {
                    permissions++;
                }

                if (step.State == DimensionTemplateCustomizerPipelineStepState.NeedsImplementation)
                {
                    implementations++;
                }
            }
        }
    }
}
