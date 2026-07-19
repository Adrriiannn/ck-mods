using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCreationWizardActionIntent
    {
        Preview = 0,
        CreateWorkspace = 1,
        ResetRequest = 2,
        MarkClean = 3,
        SaveAssets = 4,
        ExportManifest = 5
    }

    public enum DimensionTemplateCreationWizardActionAvailability
    {
        Available = 0,
        Disabled = 1,
        NeedsReview = 2,
        Blocked = 3,
        RequiresEditorImplementation = 4
    }

    public sealed class DimensionTemplateCreationWizardAction
    {
        public DimensionTemplateCreationWizardAction(
            string actionId,
            string displayName,
            string description,
            DimensionTemplateCreationWizardActionIntent intent,
            DimensionTemplateCreationWizardActionAvailability availability,
            bool primary,
            bool mutatesSession,
            bool createsWorkspace,
            bool requiresEditorImplementation,
            string code,
            string message,
            IReadOnlyList<string> blockers)
        {
            ActionId = actionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Intent = intent;
            Availability = availability;
            Primary = primary;
            MutatesSession = mutatesSession;
            CreatesWorkspace = createsWorkspace;
            RequiresEditorImplementation = requiresEditorImplementation;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Blockers = blockers ?? new List<string>();
        }

        public string ActionId { get; private set; }

        public string DisplayName { get; private set; }

        public string Description { get; private set; }

        public DimensionTemplateCreationWizardActionIntent Intent { get; private set; }

        public DimensionTemplateCreationWizardActionAvailability Availability { get; private set; }

        public bool Primary { get; private set; }

        public bool MutatesSession { get; private set; }

        public bool CreatesWorkspace { get; private set; }

        public bool RequiresEditorImplementation { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public IReadOnlyList<string> Blockers { get; private set; }

        public bool CanRunInSourceController
        {
            get
            {
                return Availability == DimensionTemplateCreationWizardActionAvailability.Available &&
                    !RequiresEditorImplementation;
            }
        }
    }

    public sealed class DimensionTemplateCreationWizardActionPlan
    {
        public DimensionTemplateCreationWizardActionPlan(
            DimensionTemplateCreationWizardSessionSnapshot snapshot,
            IReadOnlyList<DimensionTemplateCreationWizardAction> actions,
            DimensionTemplateCreationWizardAction primaryAction,
            IReadOnlyList<string> notes)
        {
            Snapshot = snapshot;
            Actions = actions ?? new List<DimensionTemplateCreationWizardAction>();
            PrimaryAction = primaryAction;
            Notes = notes ?? new List<string>();
        }

        public DimensionTemplateCreationWizardSessionSnapshot Snapshot { get; private set; }

        public IReadOnlyList<DimensionTemplateCreationWizardAction> Actions { get; private set; }

        public DimensionTemplateCreationWizardAction PrimaryAction { get; private set; }

        public IReadOnlyList<string> Notes { get; private set; }

        public int ActionCount
        {
            get
            {
                return Actions == null ? 0 : Actions.Count;
            }
        }

        public bool HasSourceRunnablePrimaryAction
        {
            get
            {
                return PrimaryAction != null && PrimaryAction.CanRunInSourceController;
            }
        }

        public bool HasBlockedActions
        {
            get
            {
                if (Actions == null)
                {
                    return false;
                }

                for (int i = 0; i < Actions.Count; i++)
                {
                    if (Actions[i].Availability ==
                        DimensionTemplateCreationWizardActionAvailability.Blocked)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public sealed class DimensionTemplateCreationWizardActionResult
    {
        public DimensionTemplateCreationWizardActionResult(
            bool executed,
            bool mutatedSession,
            bool createdWorkspace,
            string actionId,
            string code,
            string message,
            DimensionTemplateCreationWizardAction action,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCreationWizardSessionSnapshot snapshot,
            DimensionTemplateCreationWizardActionPlan actionPlan,
            IReadOnlyList<string> blockers)
        {
            Executed = executed;
            MutatedSession = mutatedSession;
            CreatedWorkspace = createdWorkspace;
            ActionId = actionId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Action = action;
            Workspace = workspace;
            Snapshot = snapshot;
            ActionPlan = actionPlan;
            Blockers = blockers ?? new List<string>();
        }

        public bool Executed { get; private set; }

        public bool MutatedSession { get; private set; }

        public bool CreatedWorkspace { get; private set; }

        public string ActionId { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public DimensionTemplateCreationWizardAction Action { get; private set; }

        public DimensionTemplateAuthoringWorkspace Workspace { get; private set; }

        public DimensionTemplateCreationWizardSessionSnapshot Snapshot { get; private set; }

        public DimensionTemplateCreationWizardActionPlan ActionPlan { get; private set; }

        public IReadOnlyList<string> Blockers { get; private set; }
    }

    public static class DimensionTemplateCreationWizardActionPlanUtility
    {
        public const string RefreshPreviewActionId = "wizard.refresh-preview";
        public const string CreateWorkspaceActionId = "wizard.create-workspace";
        public const string ResetToMinimalActionId = "wizard.reset-minimal";
        public const string MarkCleanActionId = "wizard.mark-clean";
        public const string SaveAssetsActionId = "wizard.save-assets";
        public const string ExportManifestActionId = "wizard.export-manifest";

        public static DimensionTemplateCreationWizardActionPlan Build(
            DimensionTemplateCreationWizardSessionController controller)
        {
            return Build(controller == null ? null : controller.GetSnapshot());
        }

        public static DimensionTemplateCreationWizardActionPlan Build(
            DimensionTemplateCreationWizardSessionSnapshot snapshot)
        {
            DimensionTemplateCreationWizardSessionSnapshot resolved =
                snapshot ?? CreateEmptySnapshot();
            DimensionTemplateCreationWizardPreview preview = resolved.Preview;
            List<DimensionTemplateCreationWizardAction> actions =
                new List<DimensionTemplateCreationWizardAction>();
            List<string> notes = new List<string>();

            AddPreviewAction(actions, resolved);
            AddCreateWorkspaceAction(actions, preview);
            AddResetAction(actions);
            AddMarkCleanAction(actions, resolved);
            AddSaveAssetsAction(actions, resolved, preview);
            AddExportManifestAction(actions, resolved, preview);

            if (preview != null)
            {
                AddPreviewNotes(notes, preview);
            }
            else
            {
                notes.Add("No preview exists yet. Refresh the preview before creating a workspace.");
            }

            return new DimensionTemplateCreationWizardActionPlan(
                resolved,
                actions,
                ResolvePrimaryAction(actions),
                notes);
        }

        public static DimensionTemplateCreationWizardActionResult RunSourceAction(
            DimensionTemplateCreationWizardSessionController controller,
            string actionId)
        {
            if (controller == null)
            {
                return new DimensionTemplateCreationWizardActionResult(
                    false,
                    false,
                    false,
                    actionId,
                    "session-missing",
                    "No wizard session exists.",
                    null,
                    null,
                    null,
                    Build((DimensionTemplateCreationWizardSessionSnapshot)null),
                    new List<string> { "Create a wizard session first." });
            }

            DimensionTemplateCreationWizardActionPlan beforePlan =
                controller.GetActionPlan();
            DimensionTemplateCreationWizardAction action =
                FindAction(beforePlan == null ? null : beforePlan.Actions, actionId);
            if (action == null)
            {
                return CreateResult(
                    controller,
                    false,
                    false,
                    false,
                    actionId,
                    "action-not-found",
                    "The requested wizard action is not part of the current action plan.",
                    null,
                    null,
                    new List<string> { "Refresh the action plan and try again." });
            }

            if (!action.CanRunInSourceController)
            {
                return CreateResult(
                    controller,
                    false,
                    false,
                    false,
                    action.ActionId,
                    action.Code,
                    action.Message,
                    action,
                    null,
                    action.Blockers);
            }

            if (action.ActionId == RefreshPreviewActionId)
            {
                controller.RefreshPreview();
                return CreateResult(
                    controller,
                    true,
                    true,
                    false,
                    action.ActionId,
                    "preview-refreshed",
                    "Dimension template creation preview refreshed.",
                    action,
                    null,
                    null);
            }

            if (action.ActionId == CreateWorkspaceActionId)
            {
                DimensionTemplateAuthoringWorkspace workspace =
                    controller.CreateWorkspace();
                return CreateResult(
                    controller,
                    workspace != null,
                    true,
                    workspace != null,
                    action.ActionId,
                    workspace == null ? "workspace-missing" : "workspace-created",
                    workspace == null
                        ? "The wizard action ran, but no workspace was created."
                        : "Dimension Asset authoring workspace created in memory.",
                    action,
                    workspace,
                    workspace == null
                        ? new List<string> { "Review the current preview and validation messages." }
                        : null);
            }

            if (action.ActionId == ResetToMinimalActionId)
            {
                controller.SetRequest(new DimensionTemplateCreationWizardRequest());
                return CreateResult(
                    controller,
                    true,
                    true,
                    false,
                    action.ActionId,
                    "request-reset",
                    "Dimension Asset creation request reset to the minimal starter preset.",
                    action,
                    null,
                    null);
            }

            if (action.ActionId == MarkCleanActionId)
            {
                controller.MarkClean();
                return CreateResult(
                    controller,
                    true,
                    true,
                    false,
                    action.ActionId,
                    "marked-clean",
                    "Dimension template creation session marked clean.",
                    action,
                    null,
                    null);
            }

            return CreateResult(
                controller,
                false,
                false,
                false,
                action.ActionId,
                "not-source-runnable",
                "The requested wizard action requires an editor implementation.",
                action,
                null,
                new List<string> { "Use editor-only code to perform this action." });
        }

        private static DimensionTemplateCreationWizardSessionSnapshot CreateEmptySnapshot()
        {
            DimensionTemplateCreationWizardRequest request =
                new DimensionTemplateCreationWizardRequest();
            DimensionTemplateCreationWizardModel model =
                DimensionTemplateCreationWizardUtility.BuildModel(request);
            DimensionTemplateCreationWizardPreview preview =
                DimensionTemplateCreationWizardUtility.Preview(request);
            return new DimensionTemplateCreationWizardSessionSnapshot(
                DimensionTemplateCreationWizardSessionStatus.Empty,
                0,
                false,
                request,
                model,
                preview,
                null,
                null);
        }

        private static void AddPreviewAction(
            List<DimensionTemplateCreationWizardAction> actions,
            DimensionTemplateCreationWizardSessionSnapshot snapshot)
        {
            actions.Add(new DimensionTemplateCreationWizardAction(
                RefreshPreviewActionId,
                "Refresh Preview",
                "Rebuild the source-side preview from the current wizard fields.",
                DimensionTemplateCreationWizardActionIntent.Preview,
                snapshot == null
                    ? DimensionTemplateCreationWizardActionAvailability.Blocked
                    : DimensionTemplateCreationWizardActionAvailability.Available,
                false,
                true,
                false,
                false,
                snapshot == null ? "snapshot-missing" : "available",
                snapshot == null
                    ? "No wizard session exists."
                    : "Preview can be refreshed without saving assets or touching runtime state.",
                snapshot == null
                    ? new List<string> { "Create a wizard session first." }
                    : null));
        }

        private static void AddCreateWorkspaceAction(
            List<DimensionTemplateCreationWizardAction> actions,
            DimensionTemplateCreationWizardPreview preview)
        {
            DimensionTemplateCreationWizardActionAvailability availability =
                DimensionTemplateCreationWizardActionAvailability.Available;
            string code = "available";
            string message = "Create an in-memory authoring workspace from the preview.";
            List<string> blockers = new List<string>();

            if (preview == null)
            {
                availability = DimensionTemplateCreationWizardActionAvailability.Blocked;
                code = "preview-missing";
                message = "A preview is required before creating a workspace.";
                blockers.Add("Refresh the preview first.");
            }
            else if (!preview.CanCreate)
            {
                availability = DimensionTemplateCreationWizardActionAvailability.NeedsReview;
                code = preview.Code;
                message = preview.Message;
                blockers.Add(preview.Message);
                AddWarnings(blockers, preview.Warnings);
            }

            actions.Add(new DimensionTemplateCreationWizardAction(
                CreateWorkspaceActionId,
                "Create Workspace",
                "Build the editable authoring workspace in memory.",
                DimensionTemplateCreationWizardActionIntent.CreateWorkspace,
                availability,
                availability == DimensionTemplateCreationWizardActionAvailability.Available,
                true,
                true,
                false,
                code,
                message,
                blockers));
        }

        private static void AddResetAction(
            List<DimensionTemplateCreationWizardAction> actions)
        {
            actions.Add(new DimensionTemplateCreationWizardAction(
                ResetToMinimalActionId,
                "Reset",
                "Reset the wizard request to the built-in minimal Dimension Asset preset.",
                DimensionTemplateCreationWizardActionIntent.ResetRequest,
                DimensionTemplateCreationWizardActionAvailability.Available,
                false,
                true,
                false,
                false,
                "available",
                "Reset is a session-only edit.",
                null));
        }

        private static void AddMarkCleanAction(
            List<DimensionTemplateCreationWizardAction> actions,
            DimensionTemplateCreationWizardSessionSnapshot snapshot)
        {
            bool dirty = snapshot != null && snapshot.Dirty;
            actions.Add(new DimensionTemplateCreationWizardAction(
                MarkCleanActionId,
                "Mark Clean",
                "Clear the wizard dirty flag after external editor code has saved the requested assets.",
                DimensionTemplateCreationWizardActionIntent.MarkClean,
                dirty
                    ? DimensionTemplateCreationWizardActionAvailability.Available
                    : DimensionTemplateCreationWizardActionAvailability.Disabled,
                false,
                true,
                false,
                false,
                dirty ? "available" : "not-dirty",
                dirty
                    ? "The session can be marked clean."
                    : "The session is already clean.",
                null));
        }

        private static void AddSaveAssetsAction(
            List<DimensionTemplateCreationWizardAction> actions,
            DimensionTemplateCreationWizardSessionSnapshot snapshot,
            DimensionTemplateCreationWizardPreview preview)
        {
            bool hasWorkspace = snapshot != null && snapshot.LastCreatedWorkspace != null;
            bool hasValidPreview = preview != null && preview.CanCreate;
            DimensionTemplateCreationWizardActionAvailability availability =
                hasWorkspace && hasValidPreview
                    ? DimensionTemplateCreationWizardActionAvailability.RequiresEditorImplementation
                    : DimensionTemplateCreationWizardActionAvailability.Blocked;
            List<string> blockers = new List<string>();
            if (!hasValidPreview)
            {
                blockers.Add("Create a valid preview and workspace before saving assets.");
            }
            else if (!hasWorkspace)
            {
                blockers.Add("Create the in-memory workspace first.");
            }

            actions.Add(new DimensionTemplateCreationWizardAction(
                SaveAssetsActionId,
                "Save Assets",
                "Save the generated authoring assets into the selected mod folder.",
                DimensionTemplateCreationWizardActionIntent.SaveAssets,
                availability,
                false,
                false,
                false,
                true,
                hasWorkspace && hasValidPreview ? "editor-save-ready" : "workspace-missing",
                hasWorkspace && hasValidPreview
                    ? "The Unity editor window can now create the Dimension Asset files from this save plan."
                    : "No in-memory workspace has been created yet.",
                blockers));
        }

        private static void AddExportManifestAction(
            List<DimensionTemplateCreationWizardAction> actions,
            DimensionTemplateCreationWizardSessionSnapshot snapshot,
            DimensionTemplateCreationWizardPreview preview)
        {
            bool hasWorkspace = snapshot != null && snapshot.LastCreatedWorkspace != null;
            bool hasValidPreview = preview != null && preview.CanCreate;
            bool readyForManifestExport =
                preview != null &&
                preview.Assessment != null &&
                preview.Assessment.ReadyForManifestExport;
            DimensionTemplateCreationWizardActionAvailability availability =
                hasWorkspace && hasValidPreview && readyForManifestExport
                    ? DimensionTemplateCreationWizardActionAvailability.RequiresEditorImplementation
                    : DimensionTemplateCreationWizardActionAvailability.Blocked;
            List<string> blockers = new List<string>();
            if (!hasValidPreview)
            {
                blockers.Add("Create a valid preview and workspace before exporting a manifest.");
            }
            else if (!hasWorkspace)
            {
                blockers.Add("Create the in-memory workspace first.");
            }
            else if (!readyForManifestExport)
            {
                blockers.Add("Finish the Dimensions API setup steps before exporting the runtime manifest.");
            }

            actions.Add(new DimensionTemplateCreationWizardAction(
                ExportManifestActionId,
                "Export Manifest",
                "Create a public content manifest from the workspace through editor-only code.",
                DimensionTemplateCreationWizardActionIntent.ExportManifest,
                availability,
                false,
                false,
                false,
                true,
                hasWorkspace && hasValidPreview && readyForManifestExport
                    ? "editor-implementation-required"
                    : hasWorkspace && hasValidPreview
                        ? "manifest-not-ready"
                        : "workspace-missing",
                hasWorkspace && hasValidPreview && readyForManifestExport
                    ? "Manifest export requires an editor implementation to choose destination assets/files."
                    : "Manifest export is locked until the Dimension Asset has a complete setup.",
                blockers));
        }

        private static void AddPreviewNotes(
            List<string> notes,
            DimensionTemplateCreationWizardPreview preview)
        {
            if (!string.IsNullOrEmpty(preview.Message))
            {
                notes.Add(preview.Message);
            }

            AddWarnings(notes, preview.Warnings);
            if (preview.SavePlan == null)
            {
                notes.Add("No save plan is currently available for this preview.");
            }
        }

        private static void AddWarnings(
            List<string> target,
            IReadOnlyList<string> warnings)
        {
            if (target == null || warnings == null)
            {
                return;
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                if (!string.IsNullOrEmpty(warnings[i]))
                {
                    target.Add(warnings[i]);
                }
            }
        }

        private static DimensionTemplateCreationWizardAction ResolvePrimaryAction(
            IReadOnlyList<DimensionTemplateCreationWizardAction> actions)
        {
            if (actions == null)
            {
                return null;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Primary)
                {
                    return actions[i];
                }
            }

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].CanRunInSourceController)
                {
                    return actions[i];
                }
            }

            return actions.Count > 0 ? actions[0] : null;
        }

        private static DimensionTemplateCreationWizardAction FindAction(
            IReadOnlyList<DimensionTemplateCreationWizardAction> actions,
            string actionId)
        {
            if (actions == null || string.IsNullOrEmpty(actionId))
            {
                return null;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].ActionId == actionId)
                {
                    return actions[i];
                }
            }

            return null;
        }

        private static DimensionTemplateCreationWizardActionResult CreateResult(
            DimensionTemplateCreationWizardSessionController controller,
            bool executed,
            bool mutatedSession,
            bool createdWorkspace,
            string actionId,
            string code,
            string message,
            DimensionTemplateCreationWizardAction action,
            DimensionTemplateAuthoringWorkspace workspace,
            IReadOnlyList<string> blockers)
        {
            DimensionTemplateCreationWizardSessionSnapshot snapshot =
                controller == null ? null : controller.GetSnapshot();
            return new DimensionTemplateCreationWizardActionResult(
                executed,
                mutatedSession,
                createdWorkspace,
                actionId,
                code,
                message,
                action,
                workspace,
                snapshot,
                Build(snapshot),
                blockers);
        }
    }
}
