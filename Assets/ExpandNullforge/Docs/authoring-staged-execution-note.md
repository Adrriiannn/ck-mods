# The staged, confirmable authoring run the wizard does not do

Two files in `Scripts/Authoring/` — `DimensionTemplateCustomizerExecutionPlan.cs` (639 lines) and
`DimensionTemplateCustomizerSessionController.cs` (539) — described a way of running an authoring
operation that nothing ever called. The type names occur in one file each: their own. They are
deleted; this is what they said, kept because the live Dimension Dashboard offers none of it and
somebody will want it back.

## The idea

**A destructive authoring operation is planned, shown, confirmed, and only then run.** The plan is a
value you can hand to a UI and read out loud before anything is written to disk.

An operation is one of four intents:

| Intent | What it does |
|---|---|
| `SaveAssets` | Writes the starter assets a template needs into the project. |
| `ValidateManifest` | Checks the content manifest without writing anything. |
| `ExportManifest` | Writes the manifest out. |
| `ApplyManifest` | Pushes the manifest into the live runtime service. |

Asking for one returns a plan, never an action. The plan carries:

- `State` — one of `MissingWorkspace`, `Blocked`, `NeedsConfirmation`, `NeedsEditorImplementation`,
  `NeedsRuntimeService`, `Ready`.
- `CanRun`, and the three reasons it might not: `RequiresConfirmation`,
  `RequiresEditorImplementation`, `RequiresRuntimeService`.
- Two honesty flags a UI can colour by: `MutatesAssets` and `TouchesRuntimeState`.
- Counts to show before committing: `RequiredSaveEntryCount`, `OptionalSaveEntryCount`,
  `ManifestContentCount`.
- A `Code` and a `Message` — a stable machine string plus a sentence for a person.

## The steps

The part with no equivalent anywhere in the live tool. A plan is a **list of named steps**, each one
of which can block the run on its own:

```
StepId                        Label                          Blocks?
resolve-save-plan             Resolve save plan              when there is no plan
check-required-assets         Check required assets          when nothing is required
editor-save-implementation    Editor save implementation     always
build-manifest-preview        Build manifest preview
check-export-readiness        Check export readiness
validate-manifest-request     Validate manifest request
confirm-export                Confirm export                 until confirmed
editor-export-implementation  Editor export implementation   always
confirm-apply                 Confirm runtime apply          until confirmed
runtime-manifest-service      Runtime manifest service       always
resolve-workspace             Resolve workspace
resolve-operation             Resolve operation
```

Each step has a `State` (`Ready`, `Blocked`, `NeedsConfirmation`, `NeedsEditorImplementation`,
`NeedsRuntimeService`, `Skipped`), a `Message` written for a person, and `BlocksExecution`.

That last flag is the whole point: **a UI can show every reason the button is greyed out at once**,
in order, instead of showing one error at a time and making the author guess what comes next.

## Confirmation

`DimensionTemplateCustomizerExecutionRequest` carries `Confirmed` and a free-text `Reason`. An
unconfirmed request for `ExportManifest` or `ApplyManifest` comes back
`NeedsConfirmation` with the message *"Applying a manifest mutates registered runtime content and
must be confirmed."* Confirming is re-asking with `Confirmed = true`, which means the same code path
produces both the "are you sure" screen and the run — they cannot drift apart.

## The session on top

`DimensionTemplateCustomizerSessionController` wrapped a workspace and kept:

- `Status` — `MissingWorkspace` / `Clean` / `Dirty`.
- `Version`, bumped on every change, and `AppliedEditCount`.
- `Dirty`, cleared only by `MarkSaved()`.
- `History` — an append-only list of `SessionEvent`, one per thing that happened
  (`WorkspaceAssigned`, `InteractionRefreshed`, `FieldEditPreviewed`, `FieldEditBatchPreviewed`,
  `ExecutionPlanned`, `FieldEditApplied`, `FieldEditBatchApplied`, `MarkedSaved`, `HistoryReset`),
  each carrying `RequestedCount` / `AppliedCount` / `BlockedCount` / `UnsupportedCount` and the
  preview or plan it came from.
- `GetSnapshot()` — the whole session as one immutable value.

**Note what that gives that the shipped tools do not: a dirty flag with a defined owner.** The
Portal Appearance Studio's `HasUnsavedProfileWork` was found dead in the same audit, so the Studio
has no unsaved-work guard at all today. This model had one, at the session level, where it belongs.

## Why it is worth writing down rather than keeping

The code was an island: 1,178 lines, no caller, and both of its consumers
(`…SessionStatus`, `…ExecutionPlan`) referenced only from inside the island. Keeping unreachable code
because it is a good idea is how this project got the "drawn but dead" bug class. The idea is worth
more than the lines, and this is the idea.

If it is rebuilt, the two pieces to keep are **the step list with per-step `BlocksExecution`** and
**the same request type serving both the confirmation prompt and the run**.
