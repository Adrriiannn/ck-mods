# Travel feedback: the toast nobody shows

Going through a portal is the framework's headline feature and the player is told nothing while it
happens. The framework does, however, already track exactly what it would say — a complete, coherent
spec for a travel toast lives in `Scripts/Networking/DimensionTravelFeedbackState.cs` and five API
types, all of them written, all of them working, and **read by nothing**.

This page is that spec, written down, so the decision about it is a decision and not an
archaeological dig. Two of the API types — `DimensionTravelFeedbackDisplay` and
`DimensionTravelFeedbackSeverity` — are deleted alongside it; the rest are left standing pending the
owner's call (D1).

## What already happens

`DimensionTravelFeedbackState.Initialize()` subscribes to five events on
`DimensionTravelNetworkState`:

```
TravelRequestStarted           TravelRequestAcknowledged      TravelRequestCompleted
TravelCancelRequestStarted     TravelCancelRequestCompleted
```

Each one publishes a fresh `DimensionTravelFeedbackSnapshot` and raises `FeedbackChanged` with the
old and the new. That half is alive and correct. The read side —
`FeedbackChanged`, `GetSnapshot`, `TryGetActiveSnapshot`, `TryGetLastSnapshot`,
`DismissTerminalSnapshot`, `GetRetentionPolicy`, `ConfigureRetentionPolicy` — has zero consumers.

## The phases

`DimensionTravelFeedbackPhase`, and which event moves you into it:

| Phase | Reached when | Active? | Terminal? |
|---|---|---|---|
| `Idle` | nothing in flight, or a terminal snapshot expired / was dismissed | no | no |
| `RequestQueued` | *declared, never published by the current writers* | — | — |
| `AwaitingServer` | `TravelRequestStarted` — the client has asked | yes | no |
| `PreparingDestination` | `TravelRequestAcknowledged` — the server said yes and is loading | yes | no |
| `Travelling` | *declared, never published by the current writers* | — | — |
| `CancelRequested` | `TravelCancelRequestStarted`, only while a snapshot is active | yes | no |
| `Completed` | `TravelRequestCompleted` with `Accepted` | no | yes |
| `Cancelled` | cancel accepted, or a completion whose code/message contains "cancel" | no | yes |
| `Failed` | any other completion | no | yes |

Two phases are declared and never written. `RequestQueued` and `Travelling` are the gap between
"asked" and "arrived" that only a UI would notice, which is consistent with a spec written before
its consumer.

**The one piece of real logic worth keeping.** `ResolveTerminalPhase` does not trust `Accepted`
alone: a rejected result whose `Code` or `Message` contains "cancel" (case-insensitive) is reported
as `Cancelled`, not `Failed`. A player who cancelled their own travel should not be shown an error.

**A refused cancel walks the phase back.** If `TravelCancelRequestCompleted` arrives with
`Accepted == false` and the snapshot is sitting in `CancelRequested`, it returns to
`PreparingDestination` — the travel is still happening and the UI must stop saying it is being
called off.

## The severities

`DimensionTravelFeedbackSeverity`: `Hidden`, `Info`, `Success`, `Warning`, `Error`.

The mapping from phase to severity lived in the deleted presenter, so it is not recoverable
verbatim. The obvious reading, and the one the phase names support: active phases are `Info`,
`Completed` is `Success`, `Cancelled` is `Warning`, `Failed` is `Error`, `Idle` is `Hidden`.

## What the toast shows

`DimensionTravelFeedbackDisplay` is the shape the UI was handed:

| Field | Meaning |
|---|---|
| `Visible` | whether to draw at all |
| `Severity` | which of the five |
| `Title` | the one-line heading |
| `Message` | the sentence |
| `Detail` | the extra line — a code, a reason, a destination |
| `PrimaryActionLabel` | the text on the one button |
| `CanCancel` | whether the button cancels the travel |
| `CanDismiss` | whether the player may close it |

`Hidden()` is the static that returns "draw nothing". `CanCancel` and `CanDismiss` being separate is
the right call: an active travel can be cancelled and not dismissed, a terminal one can be dismissed
and not cancelled.

## The retention policy

A terminal snapshot does not stay on screen forever, and how long it stays depends on which terminal
it is. `DimensionTravelFeedbackRetentionPolicy.Default()` is:

| Phase | Seconds |
|---|---:|
| `Completed` | 4 |
| `Cancelled` | 4 |
| `Failed` | 8 |

A failure is held twice as long as a success, because a failure is the one the player needs to read.
Any other phase returns 0, meaning "not subject to expiry" — an active travel is never timed out of
the UI.

Expiry is **pull-based, not ticked**: `ExpireTerminalSnapshotIfNeeded` runs at the top of
`GetSnapshot`, `TryGetActiveSnapshot` and `TryGetLastSnapshot`, and clears the snapshot when
`Now() - UpdatedAt >= retentionSeconds`. `Now()` is `Time.realtimeSinceStartupAsDouble`, so the
timer runs on wall clock and does not stop while the game is paused. There is no update loop and no
system to create — the toast expires because somebody looked at it. That is a good property and it
is why nothing had to be registered in `ExpandNullforgeModEntry` for this to work.

`ConfigureRetentionPolicy` lets a consumer replace all three numbers; negatives are clamped to zero
by the constructor.

## Terminal dismissal

`DismissTerminalSnapshot()` returns `false` and does nothing unless the snapshot is present,
**not active**, and terminal — a player cannot dismiss a travel that is still happening. When it
does apply it calls `ClearSnapshot()`, which publishes `Idle`, which raises `FeedbackChanged` one
last time so the UI knows to fade out.

## What the snapshot carries

Everything a toast could want to name, per travel: `RequestId`, `CancelRequestId`, `Phase`,
`IsPortalRequest`, `PortalId`, `TravelId`, `LoadTicketId`, `TargetDimensionId`,
`TargetLocalPosition`, `TargetAbsolutePosition`, `Code`, `Message`, `Reason`, `StartedAt`,
`UpdatedAt`, plus the three flags `HasSnapshot` / `IsActive` / `IsTerminal`.

`StartedAt` is carried forward across phase changes when the `RequestId` matches, so
`UpdatedAt - StartedAt` is the true age of the whole travel and not of the current phase — which is
what a progress indicator would need.

## The decision this informs

The write side runs on every travel today and costs a struct copy. The read side has no consumers,
and the DTOs are public `ExpandNullforge.Api` types another mod could legitimately subscribe to
right now. So the choice is: **build the one UI consumer, or retire the whole subsystem — state
class, five API DTOs and the two `ExpandNullforgeModEntry` lines — in a single commit.**

Half-deleting it is the one thing that must not happen: taking the DTOs and leaving the writer
leaves five event subscriptions publishing into nothing, and taking the writer and leaving the DTOs
leaves a published API that can never fire.
