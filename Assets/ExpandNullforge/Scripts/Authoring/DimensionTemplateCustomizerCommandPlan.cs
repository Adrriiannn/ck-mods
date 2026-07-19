using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerCommandState
    {
        Ready = 0,
        Warning = 1,
        NeedsSelection = 2,
        NeedsConfirmation = 3,
        NeedsImplementation = 4,
        Disabled = 5,
        Blocked = 6
    }

    public sealed class DimensionTemplateCustomizerCommand
    {
        public DimensionTemplateCustomizerCommand(
            DimensionTemplateCustomizerActionDescriptor action,
            DimensionTemplateCustomizerActionPreview preview,
            DimensionTemplateCustomizerFocusState focus,
            DimensionTemplateCustomizerCommandState state,
            string commandId,
            string blockedReason,
            bool canExecute,
            bool isPrimary,
            bool matchesFocus,
            int priority)
        {
            Action = action;
            Preview = preview;
            Focus = focus;
            State = state;
            CommandId = commandId ?? string.Empty;
            BlockedReason = blockedReason ?? string.Empty;
            CanExecute = canExecute;
            IsPrimary = isPrimary;
            MatchesFocus = matchesFocus;
            Priority = priority < 0 ? 0 : priority;
        }

        public DimensionTemplateCustomizerActionDescriptor Action { get; private set; }

        public DimensionTemplateCustomizerActionPreview Preview { get; private set; }

        public DimensionTemplateCustomizerFocusState Focus { get; private set; }

        public DimensionTemplateCustomizerCommandState State { get; private set; }

        public string CommandId { get; private set; }

        public string BlockedReason { get; private set; }

        public bool CanExecute { get; private set; }

        public bool IsPrimary { get; private set; }

        public bool MatchesFocus { get; private set; }

        public int Priority { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerCommandPlan
    {
        public DimensionTemplateCustomizerCommandPlan(
            string activeSectionId,
            string primaryActionId,
            int commandCount,
            int readyCount,
            int warningCount,
            int blockedCount,
            int implementationCount,
            DimensionTemplateCustomizerFocusState focus,
            DimensionTemplateCustomizerCommand primaryCommand,
            IReadOnlyList<DimensionTemplateCustomizerCommand> commands)
        {
            ActiveSectionId = activeSectionId ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            CommandCount = commandCount < 0 ? 0 : commandCount;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            ImplementationCount = implementationCount < 0 ? 0 : implementationCount;
            Focus = focus;
            PrimaryCommand = primaryCommand;
            Commands = commands ?? new List<DimensionTemplateCustomizerCommand>();
        }

        public string ActiveSectionId { get; private set; }

        public string PrimaryActionId { get; private set; }

        public int CommandCount { get; private set; }

        public int ReadyCount { get; private set; }

        public int WarningCount { get; private set; }

        public int BlockedCount { get; private set; }

        public int ImplementationCount { get; private set; }

        public DimensionTemplateCustomizerFocusState Focus { get; private set; }

        public DimensionTemplateCustomizerCommand PrimaryCommand { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerCommand> Commands { get; private set; }
    }

    public static class DimensionTemplateCustomizerCommandPlanUtility
    {
        public static DimensionTemplateCustomizerCommandPlan Build(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId,
            DimensionTemplateCustomizerFocusRequest focusRequest)
        {
            DimensionTemplateCustomizerActionCatalog actions =
                workspace == null
                    ? DimensionTemplateCustomizerActionCatalogUtility.Build(null, sectionId)
                    : workspace.GetActionCatalog(sectionId);
            DimensionTemplateCustomizerActionPreviewCatalog previews =
                workspace == null
                    ? DimensionTemplateCustomizerActionPreviewUtility.Build(null, sectionId)
                    : workspace.GetActionPreviews(sectionId);
            DimensionTemplateCustomizerFocusState focus =
                workspace == null
                    ? DimensionTemplateCustomizerFocusUtility.Resolve(null, focusRequest)
                    : workspace.ResolveFocus(focusRequest);

            List<DimensionTemplateCustomizerCommand> commands =
                new List<DimensionTemplateCustomizerCommand>();
            IReadOnlyList<DimensionTemplateCustomizerActionPreview> previewList =
                previews.Previews;
            for (int i = 0; i < previewList.Count; i++)
            {
                DimensionTemplateCustomizerActionPreview preview = previewList[i];
                DimensionTemplateCustomizerCommand command =
                    CreateCommand(preview.Action, preview, focus, actions.PrimaryActionId);
                commands.Add(command);
            }

            commands.Sort(CompareCommands);
            DimensionTemplateCustomizerCommand primaryCommand =
                SelectPrimary(commands, actions.PrimaryActionId);

            int ready;
            int warning;
            int blocked;
            int implementation;
            CountCommands(commands, out ready, out warning, out blocked, out implementation);

            return new DimensionTemplateCustomizerCommandPlan(
                actions.ActiveSectionId,
                actions.PrimaryActionId,
                commands.Count,
                ready,
                warning,
                blocked,
                implementation,
                focus,
                primaryCommand,
                commands);
        }

        private static DimensionTemplateCustomizerCommand CreateCommand(
            DimensionTemplateCustomizerActionDescriptor action,
            DimensionTemplateCustomizerActionPreview preview,
            DimensionTemplateCustomizerFocusState focus,
            string primaryActionId)
        {
            bool matchesFocus = MatchesFocus(action, focus);
            string blockedReason;
            DimensionTemplateCustomizerCommandState state =
                ResolveState(action, preview, focus, matchesFocus, out blockedReason);
            bool canExecute = state == DimensionTemplateCustomizerCommandState.Ready ||
                state == DimensionTemplateCustomizerCommandState.Warning ||
                state == DimensionTemplateCustomizerCommandState.NeedsConfirmation;
            bool isPrimary = action.ActionId == primaryActionId;

            return new DimensionTemplateCustomizerCommand(
                action,
                preview,
                focus,
                state,
                action.ActionId,
                blockedReason,
                canExecute,
                isPrimary,
                matchesFocus,
                action.Priority);
        }

        private static DimensionTemplateCustomizerCommandState ResolveState(
            DimensionTemplateCustomizerActionDescriptor action,
            DimensionTemplateCustomizerActionPreview preview,
            DimensionTemplateCustomizerFocusState focus,
            bool matchesFocus,
            out string blockedReason)
        {
            blockedReason = string.Empty;

            if (preview.RequiresEditorImplementation)
            {
                blockedReason = "This command is described but still needs an editor implementation.";
                return DimensionTemplateCustomizerCommandState.NeedsImplementation;
            }

            if (action.Availability == DimensionTemplateCustomizerActionAvailability.Blocked)
            {
                blockedReason = action.Tooltip;
                return DimensionTemplateCustomizerCommandState.Blocked;
            }

            if (action.Availability == DimensionTemplateCustomizerActionAvailability.Disabled)
            {
                blockedReason = action.Tooltip;
                return DimensionTemplateCustomizerCommandState.Disabled;
            }

            if (action.RequiresSelection &&
                (focus == null || !focus.HasFocus || !matchesFocus))
            {
                blockedReason = "Select a matching section, biome, record, or search result first.";
                return DimensionTemplateCustomizerCommandState.NeedsSelection;
            }

            if (action.RequiresConfirmation)
            {
                blockedReason = preview.SafetyMessage;
                return DimensionTemplateCustomizerCommandState.NeedsConfirmation;
            }

            if (action.Availability == DimensionTemplateCustomizerActionAvailability.Warning)
            {
                blockedReason = action.Tooltip;
                return DimensionTemplateCustomizerCommandState.Warning;
            }

            return DimensionTemplateCustomizerCommandState.Ready;
        }

        private static bool MatchesFocus(
            DimensionTemplateCustomizerActionDescriptor action,
            DimensionTemplateCustomizerFocusState focus)
        {
            if (focus == null || !focus.HasFocus)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(action.SectionId) &&
                focus.SectionId != action.SectionId)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(action.BiomeId) &&
                focus.BiomeId != action.BiomeId)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(action.RecordKind) &&
                focus.RecordKind != action.RecordKind)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(action.RecordId) &&
                focus.RecordId != action.RecordId)
            {
                return false;
            }

            return true;
        }

        private static DimensionTemplateCustomizerCommand SelectPrimary(
            IReadOnlyList<DimensionTemplateCustomizerCommand> commands,
            string primaryActionId)
        {
            if (commands == null || commands.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].CommandId == primaryActionId)
                {
                    return commands[i];
                }
            }

            return commands[0];
        }

        private static void CountCommands(
            IReadOnlyList<DimensionTemplateCustomizerCommand> commands,
            out int ready,
            out int warning,
            out int blocked,
            out int implementation)
        {
            ready = 0;
            warning = 0;
            blocked = 0;
            implementation = 0;
            if (commands == null)
            {
                return;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                DimensionTemplateCustomizerCommandState state = commands[i].State;
                if (state == DimensionTemplateCustomizerCommandState.Ready)
                {
                    ready++;
                }
                else if (state == DimensionTemplateCustomizerCommandState.Warning ||
                    state == DimensionTemplateCustomizerCommandState.NeedsConfirmation)
                {
                    warning++;
                }
                else if (state == DimensionTemplateCustomizerCommandState.NeedsImplementation)
                {
                    implementation++;
                }
                else if (state == DimensionTemplateCustomizerCommandState.Blocked ||
                    state == DimensionTemplateCustomizerCommandState.Disabled ||
                    state == DimensionTemplateCustomizerCommandState.NeedsSelection)
                {
                    blocked++;
                }
            }
        }

        private static int CompareCommands(
            DimensionTemplateCustomizerCommand left,
            DimensionTemplateCustomizerCommand right)
        {
            if (left.IsPrimary != right.IsPrimary)
            {
                return left.IsPrimary ? -1 : 1;
            }

            if (left.CanExecute != right.CanExecute)
            {
                return left.CanExecute ? -1 : 1;
            }

            int state = left.State.CompareTo(right.State);
            if (state != 0)
            {
                return state;
            }

            return left.Priority.CompareTo(right.Priority);
        }
    }
}
