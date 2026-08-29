# Undo-able authoring: what the customizer clusters promise

Four files in the Template Customizer family describe an authoring tool where an edit can be
previewed, batched, staged and undone. None of it is wired to anything a modder can click. The
Dimension Dashboard offers no preview, no batch, no undo and no repair-plan list.

This page records what they promise, so the decision about the ~20,000-line customizer model (D3)
is made against what it does rather than what its name suggests. **Nothing here is deleted by this
cleanup** — the files stay pending that decision.

| File | Lines |
|---|---:|
| `Scripts/Authoring/DimensionTemplateCustomizerFieldEditPreview.cs` | 1,009 |
| `Scripts/Authoring/DimensionTemplateCustomizerFieldEditBatch.cs` | 721 |
| `Scripts/Authoring/DimensionTemplateCustomizerIssueRepairPlan.cs` | 466 |
| `Scripts/Authoring/DimensionTemplateCustomizerFieldEditRollback.cs` | 229 |

## The pipeline they describe

```
FieldEditRequest  →  FieldEditPreview  →  FieldApplyPlan
     (many)        →  BatchPreview      →  BatchApplyPlan  →  RollbackPlan
```

Everything is a value. Nothing writes until an executor is handed a plan, so every stage can be
shown to a person first.

## One edit: preview then plan

`DimensionTemplateCustomizerFieldEditRequest` is a focus request, a `FieldId`, a `NewValue`, and
`AcceptWarnings`.

The **preview** answers "what would this do" without doing it:

- `State`, `Code`, `Message`, `CanApply`.
- `HasWarning` and the `Warning` text — an edit can be legal and still worth a sentence.
- `NormalizedValue` — what the value becomes after cleaning, which is what the field will actually
  hold.
- Typed parse results with their own presence flags: `ParsedBounds`/`HasParsedBounds`,
  `ParsedInteger`/`HasParsedInteger`, `ParsedBoolean`/`HasParsedBoolean`. **The preview parses once
  and hands the result on**, so the apply path never re-parses a string and cannot disagree with the
  preview about what it meant.
- The `Field` and `FieldSet` it resolved to.

The **apply plan** is the same edit expressed as work: `SectionId`, `RecordKind`, `RecordId`,
`FieldId`, `OldValue`, `NewValue`, and an ordered list of `FieldApplyStep` (`Order`, `StepId`,
`Label`, `Detail`).

`OldValue` on the plan is the load-bearing field. It is what makes undo possible at all.

## Many edits: the batch

`FieldEditBatchRequest` is a list of requests. The **batch preview** previews each one and reports
the whole:

| | |
|---|---|
| `State` | `Empty` / `Blocked` / `NeedsWarningConfirmation` / `Ready` |
| counts | `TotalCount`, `ReadyCount`, `WarningCount`, `BlockedCount`, `DuplicateTargetCount` |
| `RequiresWarningConfirmation` | at least one entry has a warning and the caller has not accepted |
| `Entries` | per edit: `Index`, `TargetKey`, `DuplicateTarget`, `CanApply`, `HasWarning`, `Code`, `Message`, and its own preview and apply plan |

**`DuplicateTarget` is the piece with no equivalent anywhere in the shipped tools.** Two edits in one
batch that write the same `TargetKey` are flagged as a pair, before either runs — the last-writer-wins
bug caught at preview time instead of found later in the asset.

The **batch apply plan** flattens the ready entries into one ordered `BatchApplyStep` list
(`Order`, `EditIndex`, `StepId`, `Label`, `Detail`), with its own state
(`MissingPreview` / `Empty` / `Blocked` / `NeedsWarningConfirmation` / `Ready`).

## Undo: the rollback plan

`FieldEditRollbackPlanUtility.Build` takes a batch apply plan — or a workspace and a request, or a
batch preview, and builds one — and returns the inverse.

A `RollbackStep` is `Order`, `EditIndex`, `TargetKey`, `FieldId`, `OldValue`, `NewValue`, `Label`,
`Detail`. `OldValue` and `NewValue` are both carried, so a rollback step is readable as a sentence
without consulting the original batch.

The state machine is deliberately narrow:

| State | When |
|---|---|
| `MissingApplyPlan` | no apply plan was supplied |
| `Empty` | the batch has nothing reversible in it |
| `NotApplicable` | **the batch is not `Ready`** |
| `Ready` | there are reversible steps |

That third row is the design decision worth keeping: **a rollback plan is only produced for a batch
that was ready to apply.** You cannot build an undo for a batch that was blocked, so an undo can
never describe an edit that never happened.

The message on a ready plan says what the code is honest about: *"the ready batch has a source-side
rollback plan for future editor transaction support"*. The plan is a description of an undo, not an
undo — something still has to execute it against the assets.

## Repairing an authoring issue

`IssueRepairPlan` is the same shape applied to validator findings. A repair carries a `RepairId`, a
`Summary`, the resolution `Hint` it came from, a `BlockedReason`, and a state:

```
None  InspectOnly  CanJumpToTarget  CanPreviewFieldEdit
NeedsContentCreation  NeedsEditorImplementation  Blocked
```

plus four honesty flags — `CanRunReadOnly`, `CanMutateAssets`, `RequiresConfirmation`,
`RequiresEditorImplementation` — and a list of `IssueRepairStep` (`StepIndex`, `StepId`, `Label`,
`Detail`, `IsRequired`, `IsAutomatic`, `RequiresEditorImplementation`).

The `IssueRepairCatalog` groups them for one section and counts them by capability: `RepairCount`,
`ReadOnlyCount`, `AssetMutationCount`, `RequiresImplementationCount`, `BlockedCount`.

**`CanJumpToTarget` and `InspectOnly` are separate states from `Blocked` on purpose.** "I cannot fix
this but I can take you to it" is a real, useful answer, and a validator UI that only knows fixable
from unfixable cannot give it.

## What is actually worth taking, if any of it is

Three ideas, in order of value:

1. **`OldValue` on the plan.** Without it there is no undo, and it costs one field.
2. **`DuplicateTarget` in a batch.** A conflict found at preview time rather than in the asset.
3. **The preview parses once and hands the typed result to the apply path.** It removes a whole
   class of "the preview said one thing and the write did another".

The rest is a lot of shape for a tool that does not exist. Deciding to keep the model is deciding to
build the tool.
