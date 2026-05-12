# Smart Splitter Research

## Purpose of this document

This document is the current checkpoint for the Smart Splitter mod and for future Core Keeper automation/ECS modding work.

It records:

- what has been tested
- what worked
- what failed
- what was proven wrong
- what is currently believed to be true
- how our Smart Splitter runtime system interacts with Core Keeper's vanilla ECS automation systems

The goal is to avoid re-testing failed approaches and to provide a reliable starting point for future automation mods.

---

## Current status

The Smart Splitter ECS routing prototype is currently working.

Validated behavior:

- Dirt routes left.
- Turf routes right.
- Full stacks route without splitting.
- Rapid item-type changes route correctly.
- Mixed stack stress tests route correctly.
- Vanilla alternating behavior is suppressed when the Smart Splitter route is armed.
- The current working solution does not require Harmony.

Current implementation style:

- ECS-only runtime mutation.
- ServerSimulation system.
- No Harmony patching.
- No direct replacement of vanilla automation jobs.
- Vanilla automation is redirected by changing the ECS state it already consumes.

Current limitations:

- No-match blocking is not solved yet.
- UI/config persistence is not solved yet.
- Multiplayer sync/persistence needs future validation.
- Current filters are hardcoded prototype rules.
- Current hold durations are intentionally conservative and should later be refined.

---

# 1. Verified environment

## Unity and SDK

Verified working environment:

- Core Keeper Mod SDK
- Unity `6000.0.59f2`

Important notes:

- Earlier guidance around Unity `6000.0.58f2` was wrong for the current SDK.
- The current SDK requires Unity `6000.0.59f2`.
- Using the wrong Unity version caused confusion and can cause crashes or SDK import problems.

## Runtime mod lifecycle

Confirmed lifecycle methods:

- `EarlyInit`
- `ModObjectLoaded`
- `Init`

Runtime mod loading confirmed through `Player.log`.

## SDK alignment

The current work follows the SDK/documentation-first approach:

- Do not assume vanilla behavior.
- Verify through SDK documentation, dnSpy inspection, and controlled runtime tests.
- Avoid unsupported patching unless proven necessary and allowed by the mod loader/security model.

---

# 2. Relevant assemblies

Main assemblies involved in automation research:

- `Pug.Automation.dll`
- `Pug.Automation.Components.dll`
- `Pug.Automation.Authoring.dll`
- `Pug.Automation.Conversion.dll`
- `Pug.ECS.Components.dll`
- `Pug.Other.dll`
- `Pug.Objects.dll`
- `Pug.Base.dll`
- `PugProperties.dll`

Important namespaces observed:

- `Pug.Automation`
- `Pug.Automation.Components`
- `Pug.ECS.Components`
- `Pug.Properties`
- `Inventory`

---

# 3. Vanilla automation ECS architecture

## High-level vanilla automation flow

The simplified vanilla movement/splitter flow is:

```text
PugAutomationSystem.OnUpdate(...)
    schedules automation jobs
        ↓
CycleEnabledMoversJob
    updates enabled mover state for shared movers
        ↓
MoverMoveAndPickupJob.Execute(...)
    moves items and handles pickup/drop/split behavior
        ↓
InventoryUtility.SplitItemAndDropFromMover(...)
    splits stacks and spawns output dropped item entities
```

Important conclusion:

The splitter object itself is not the full owner of split behavior.

The real behavior emerges from:

- `MoverCD`
- `MoversWithSharedStateBuffer`
- `MoverOrchestratorCD`
- `EnabledMoverFromSharedStateCD`
- contained item buffers
- vanilla `PugAutomationSystem` jobs

---

# 4. Important vanilla ECS components

## 4.1 MoverCD

`MoverCD` is the central automation movement component.

It represents/participates in:

- conveyor belts
- splitter output movers
- robot arm movement
- automation transfer behavior

Important observed fields:

```csharp
int2 start;
int2 stop;

int moveTime;
int cooldownTime;

Entity inventoryEntity;
Entity moverOrchestratorEntity;

int splitsIntoOnMove;

bool cycleEnabledMoverAfterActivation;
bool enableAllMoversAfterActivation;
bool allowPickupFromInventories;

int indexInOrchestrator;
```

Important meanings:

- `start` / `stop` describe the mover's tile endpoints.
- `inventoryEntity` may point to a carried/storage inventory for some automation objects, especially robot arms.
- `moverOrchestratorEntity` links output movers to their orchestrator.
- `splitsIntoOnMove` is the split count used by splitter logic.
- `indexInOrchestrator` is used by shared mover/orchestrator logic.

## 4.2 MoversWithSharedStateBuffer

Relevant buffer:

```csharp
DynamicBuffer<MoversWithSharedStateBuffer>
```

Fields:

```csharp
Entity moverEntity;
int2 cachedDirection;
int2 cachedStart;
```

Meaning:

- This buffer is stored on the splitter orchestrator/root entity.
- It lists the currently active shared output movers.
- For vanilla splitters, the buffer normally has two entries.
- For forced one-sided routing, the buffer is rewritten to one entry.

Critical discovery:

`MoversWithSharedStateBuffer` controls where spawned split items are sent, but it does not by itself control split math.

## 4.3 MoverOrchestratorCD

Relevant fields observed:

```csharp
int enabledMoverIndex;
int nextMoverCycleIncrement;
```

Meaning:

- `enabledMoverIndex` is vanilla's selected active mover index.
- `nextMoverCycleIncrement` drives output cycling/alternation.
- Vanilla splitters use this to alternate between outputs.

Important runtime behavior:

For forced one-sided routing, we set:

```text
enabledMoverIndex = selected output index
nextMoverCycleIncrement = 0
```

For restored vanilla two-output behavior, we restore cycling with:

```text
nextMoverCycleIncrement = 1
```

## 4.4 EnabledMoverFromSharedStateCD

This was the final missing piece.

Vanilla `CycleEnabledMoversJob` uses this enableable component to activate only the selected shared mover.

Observed logic from dnSpy:

```csharp
EnabledMoverFromSharedStateLookup.SetComponentEnabled(
    dynamicBuffer[num].moverEntity,
    num == orchestrator.enabledMoverIndex);
```

Meaning:

- It is not enough to rewrite the buffer.
- It is not enough to rewrite `enabledMoverIndex`.
- We must also explicitly enable the selected mover and disable the other output mover through `EnabledMoverFromSharedStateCD`.

Working forced route behavior:

```text
selected output:
    EnabledMoverFromSharedStateCD enabled = true

non-selected output:
    EnabledMoverFromSharedStateCD enabled = false
```

For restored both-output behavior:

```text
left output enabled = true
right output enabled = true
```

---

# 5. Vanilla filter model

## Relevant component

`MoverFilterCD`

Fields:

```csharp
FilterType filterType;
ObjectID filterObject;
int filterVariation;
ObjectCategoryTag filterCategory;
```

Observed `FilterType` values:

- `None`
- `Whitelist`
- `Blacklist`
- `CategoryWhitelist`
- `CategoryBlacklist`

Important method:

```text
InventoryUtility.ItemMatchesObjectFilter(...)
```

Observed behavior:

- `FilterType.None` allows all.
- `Whitelist` allows matching object/variation.
- `Blacklist` blocks matching object/variation.
- Objects tagged `CanBeUpgraded` are excluded from object-filter matching.

Confirmed usage:

`InventoryUtility.ItemMatchesObjectFilter(...)` is used by automated pickup logic.

Observed call chain:

```text
InventoryUtility.ItemMatchesObjectFilter(...)
InventoryUtility.IsValidItemForAutomatedPickup(...)
InventoryUtility.AutomatedPickup(...)
PugAutomationSystem.OnUpdate(...)
```

Important conclusion:

`MoverFilterCD` does not control vanilla splitter output routing.

Tested and failed approach:

- Left output mover: whitelist dirt.
- Right output mover: blacklist dirt.
- Result: dirt still split evenly.
- Conclusion: splitter output distribution ignores `MoverFilterCD`.

---

# 6. Vanilla splitter behavior

## Relevant class/methods

Relevant class/methods observed in `Pug.Other.dll` / `Inventory`:

```text
InventoryUtility.SplitItemAndDropFromMover(...)
InventoryUtility.SplitIntoItem(...)
```

Earlier naming sometimes referred to `ConveyorBeltSplitter`, but the actual confirmed method in current assemblies is:

```text
Inventory.InventoryUtility::SplitItemAndDropFromMover(...)
```

Full method signature observed in dnSpy:

```text
System.Void Inventory.InventoryUtility::SplitItemAndDropFromMover(
    Unity.Entities.Entity,
    Unity.Entities.DynamicBuffer<Pug.Automation.MoversWithSharedStateBuffer>,
    Unity.Entities.ComponentLookup<Pug.Automation.MoverCD>,
    System.Int32,
    UnityEngine.Vector3,
    System.Int32,
    Pug.Automation.MoverCD&,
    Unity.Entities.BufferLookup<ContainedObjectsBuffer>,
    Unity.Entities.EntityCommandBuffer,
    PugDatabase/DatabaseBankCD,
    System.Int32&)
```

## Observed split behavior

`SplitItemAndDropFromMover(...)`:

- reads the source inventory/container buffer
- checks item validity
- checks stackability
- calculates split amounts using the `splits` argument
- subtracts the calculated total from the source stack
- loops over `MoversWithSharedStateBuffer`
- calls `SplitIntoItem(...)` for each active output mover

Important:

It does not check item filters.

## Split math

Observed logic:

```text
num = amount / splits
num2 = amount % splits
```

Meaning:

- `splits = 2` splits stacks into two outputs.
- `splits = 1` keeps the stack whole.

Critical item-loss failure:

If buffer length is 1 but `splits` is still 2:

- vanilla subtracts enough for two outputs
- only one output is spawned
- the missing half disappears

Therefore:

```text
buffer length and split count must always stay consistent.
```

---

# 7. Splitter topology discovery

## Splitter orchestrator discovery

Vanilla splitter orchestrator/root entities were identified by:

```text
HasBuffer<MoversWithSharedStateBuffer>
buffer length = 2 during vanilla setup
output movers have splitsIntoOnMove = 2
```

Observed distinction:

```text
Normal conveyor:
    buffer length = 1
    splitsIntoOnMove = 0

Splitter:
    buffer length = 2
    output movers splitsIntoOnMove = 2
```

## Splitter root/orchestrator entity

Observed facts:

```text
Has MoverCD = false
Has MoversWithSharedStateBuffer = true
Has MoverFilterCD = false
Has ObjectFilteringCD = false
```

Meaning:

- The splitter root/orchestrator is not itself a mover.
- Routing is driven by child/output movers and orchestration state.
- Filter state is not attached to the vanilla splitter root by default.

## Splitter output movers

Observed topology:

```text
Splitter root/orchestrator entity
└── MoversWithSharedStateBuffer
    ├── output mover A
    └── output mover B
```

Observed output mover facts:

```text
Has MoverCD = true
splitsIntoOnMove = 2
inventoryEntity = Entity.Null
indexInOrchestrator = 0 or 1
```

Important:

Splitter output movers do not carry item identity through `inventoryEntity`.

## Left/right mapping

Observed stable test setup:

```text
index 0 = visual left
index 1 = visual right
```

The implementation also caches:

- mover entity
- `cachedDirection`
- `cachedStart`
- `indexInOrchestrator`
- output stop position

This is important because future rotation/orientation handling may need stronger mapping logic.

---

# 8. Item runtime representation

## Moving belt items

Major discovery:

Items moving on belts are represented as `DroppedItem` entities.

Observed structure:

```text
DroppedItem entity
├── ObjectDataCD = DroppedItem
├── ContainedObjectsBuffer
│   └── actual item stack
└── LocalTransform
```

The actual item identity is inside `ContainedObjectsBuffer`.

Example:

```text
ObjectDataCD.objectID = DroppedItem

ContainedObjectsBuffer[0]:
    objectID = WallDirtBlock
    variation = 0
    amount = 88
```

## MoveeCD

A separate moving entity type exists:

```text
MoveeCD
```

Observed facts:

- contains movement/position state
- does not contain item identity
- is separate from the `DroppedItem` entity

Observed relationship:

```text
DroppedItem entity != Movee entity
DroppedItem.LocalTransform.Position ≈ MoveeCD.position
```

This correlation helped identify moving belt items.

## Robot arm carried items

Robot arms expose carried items through an inventory/container buffer.

Observed robot arm carried-item structure:

```text
Robot arm mover
└── inventoryEntity
    └── ContainedObjectsBuffer
        └── carried item
```

Example observed:

```text
ObjectDataCD: objectID = RobotArm
ContainedObjectsBuffer length = 1

Contained item:
    objectID = WallDirtBlock
    amount = 1
    variation = 0
```

This became important for pre-arming splitters before the item reaches the belt/split point.

---

# 9. Runtime-control experiments and outcomes

## Experiment 1: MoverFilterCD output filtering

Test:

```text
output index 0: Whitelist WallDirtBlock
output index 1: Blacklist WallDirtBlock
```

Result:

- Dirt still split evenly to both outputs.

Conclusion:

- Vanilla splitter distribution does not consult `MoverFilterCD`.

Status:

```text
FAILED / NOT USEFUL FOR SPLITTER ROUTING
```

## Experiment 2: Changing only one output mover split count

Test:

```text
one output mover splitsIntoOnMove = 1
other output unchanged
```

Result:

- stacks still split evenly

Conclusion:

- changing one output mover alone is insufficient

Status:

```text
FAILED
```

## Experiment 3: Buffer removal only

Test:

```text
remove one output from MoversWithSharedStateBuffer
leave splitsIntoOnMove = 2
```

Result:

- removed side stopped receiving items
- remaining side received items
- half the stack vanished

Conclusion:

- buffer controls spawn destinations
- split math still used two-way split count

Status:

```text
FAILED / DANGEROUS
```

## Experiment 4: Buffer removal plus split count = 1

Test:

```text
buffer length = 1
splitsIntoOnMove = 1
```

Result:

- one side received full stack
- other side received nothing
- no item loss

Conclusion:

- safe one-sided routing is possible

Status:

```text
MAJOR SUCCESS
```

## Experiment 5: Proximity pre-router

Test:

- detect nearby `DroppedItem`
- decide route
- temporarily switch splitter globally

Result:

- partially worked
- timing issues caused wrong items to route
- stacks sometimes split before state was applied
- route could stick too long or apply too late

Conclusion:

- proximity-only prediction is not production safe

Status:

```text
RESEARCH ONLY / SUPERSEDED
```

## Experiment 6: Forced right-only mode

Test:

- force splitter to `RIGHT_ONLY` every update
- send turf stack of 4

Result:

- stack did not split
- full stack routed right

Conclusion:

- ECS mutation model is valid
- failure was timing/arming logic, not the ECS state itself

Status:

```text
SUCCESS / IMPORTANT ISOLATION TEST
```

## Experiment 7: Feeder carried-item pre-arm

Test:

- detect robot arm carried item
- arm splitter before stack reaches split point
- hold route long enough

Result:

- stack routed correctly
- no split
- no item loss

Conclusion:

- pre-arming from feeder inventory solves the “too late” problem

Status:

```text
SUCCESS
```

## Experiment 8: Feeder prediction without belt priority

Test:

- rapid mixed items
- route based on robot arm carried item

Result:

- robot arm’s next item could override the route while previous item was still on the belt
- wrong side routing occurred

Conclusion:

- feeder prediction must not override already-on-belt items

Status:

```text
FAILED / FIXED BY BELT PRIORITY
```

## Experiment 9: Belt item priority over feeder carried item

Final working rule:

```text
1. If a DroppedItem exists in the input corridor:
       route based on the frontmost/best incoming DroppedItem.
2. Else:
       route based on feeder/robot arm carried item.
```

Result:

- rapid mixed stack stress test worked
- sudden item-type switching worked
- full stacks did not split
- routing matched item type

Conclusion:

- this is the current validated ECS architecture

Status:

```text
WORKING PROTOTYPE / CURRENT ARCHITECTURE
```

---

# 10. Current working Smart Splitter runtime architecture

## High-level flow

Current system order inside `SmartSplitterRuntimeSystem.OnUpdate()`:

```text
RefreshSmartSplitterConfig(...)
PruneRecentlyArmedEntities(...)
RegisterSplitters(...)

if ForceRightOnlyTestMode:
    ForceAllSplittersRightOnly(...)
else:
    ObserveSplitters(...)               // DroppedItem / belt item priority
    ObserveFeederCarriedItems(...)      // robot arm fallback only if no belt item exists
    ApplyArmedRoutes(...)
```

## Why this order matters

`ObserveSplitters(...)` runs before `ObserveFeederCarriedItems(...)`.

This ensures:

```text
frontmost belt item wins
robot arm next-carried item cannot override current belt item
```

This fixed the final major bug.

---

# 11. Smart Splitter routing decision model

Current prototype hardcoded filters:

```text
Left:
    ObjectID.WallDirtBlock
    variation 0

Right:
    ObjectID.WallTurfBlock
    variation 0
```

Decision logic:

```text
matches left only  -> LEFT_ONLY
matches right only -> RIGHT_ONLY
matches both       -> BOTH
matches neither    -> BLOCKED fallback currently routes BOTH
```

Important:

The current `Blocked` behavior is intentionally not a true block yet.

Blocking is unresolved.

---

# 12. SmartSplitterArmedRouteCD

Temporary runtime state used to bridge observation and application.

Stores:

```text
HasArmedRoute
AppliedOnce
VerifiedHoldState
ArmedEntity
Decision
ItemObject
ItemVariation
ItemAmount
ArmedAt
ExpiresAt
RouteAppliedAt
HoldUntil
```

Meaning:

- `HasArmedRoute`: route exists
- `AppliedOnce`: route has been applied to ECS state
- `VerifiedHoldState`: debug verification flag
- `ArmedEntity`: item/feeder entity that caused route
- `Decision`: left/right/both/blocked
- `ItemObject`, `ItemVariation`, `ItemAmount`: route item identity
- timing fields: hold/expiry behavior

---

# 13. Runtime mutation details

## LEFT_ONLY

The system applies:

```text
MoversWithSharedStateBuffer:
    clear
    add left mover only

MoverCD.splitsIntoOnMove:
    set all splitter movers to 1

MoverOrchestratorCD:
    enabledMoverIndex = left mover index
    nextMoverCycleIncrement = 0

EnabledMoverFromSharedStateCD:
    left enabled = true
    right enabled = false
```

## RIGHT_ONLY

The system applies:

```text
MoversWithSharedStateBuffer:
    clear
    add right mover only

MoverCD.splitsIntoOnMove:
    set all splitter movers to 1

MoverOrchestratorCD:
    enabledMoverIndex = right mover index
    nextMoverCycleIncrement = 0

EnabledMoverFromSharedStateCD:
    right enabled = true
    left enabled = false
```

## BOTH / restore

The system applies:

```text
MoversWithSharedStateBuffer:
    restore left mover
    restore right mover

MoverCD.splitsIntoOnMove:
    set all splitter movers to 2

MoverOrchestratorCD:
    nextMoverCycleIncrement = 1

EnabledMoverFromSharedStateCD:
    left enabled = true
    right enabled = true
```

---

# 14. Interaction with vanilla ECS

## What our mod changes

Our mod does not spawn the split output items itself.

Instead, it changes ECS state before vanilla automation consumes it.

Then vanilla `PugAutomationSystem` continues normally.

## What vanilla consumes

When vanilla runs:

```text
MoverMoveAndPickupJob.Execute(...)
```

it reads:

```text
MoversWithSharedStateBuffer
MoverCD.splitsIntoOnMove
MoverOrchestratorCD
EnabledMoverFromSharedStateCD
```

Because our mod has already rewritten those values, vanilla naturally produces the desired routing result.

## Why this is safer than patching

We are not replacing vanilla logic.

We are changing its inputs.

This reduces risk because:

- vanilla still handles item spawning
- vanilla still handles stack transfer
- vanilla still handles movement and ECB writes
- we avoid Harmony/Burst/job patching issues

---

# 15. Logging and debugging

Useful logs during research:

```text
[SmartSplitterRuntime] route=RIGHT_ONLY ...
[SmartSplitterRuntime] route=LEFT_ONLY ...
[SmartSplitterRuntime] verify after-apply ...
[SmartSplitterRuntime] verify held-next-tick ...
```

Now that routing works, recommended defaults:

```csharp
EnableDecisionLogs = false;
EnableRoutingLogs = false;
ForceRightOnlyTestMode = false;
```

Keep the verification code available, but disabled for normal testing.

---

# 16. Harmony investigation status

Harmony is not currently required.

Earlier Harmony attempts failed due to mod loader/security restrictions:

Observed error:

```text
Illegal reference to disallowed type: HarmonyLib.Harmony
```

Conclusion:

- Harmony should be considered off-limits or unsupported in normal SDK-safe mod code unless the project later uses elevated access and confirms it works.
- Current successful architecture avoids Harmony entirely.

---

# 17. Current known unresolved items

## 17.1 True blocked/no-match behavior

Current `Blocked` fallback routes both to avoid deletion.

Still unresolved:

```text
If neither output matches, how do we safely stop/hold the item?
```

Potential future directions:

- disable pickup into splitter
- hold item upstream
- use vanilla filter/pickup behavior before the splitter
- route to overflow output
- keep both outputs active as fallback
- add explicit “reject lane”

Must avoid:

- buffer length 0 with unsafe split math
- deleting stacks
- deadlocking automation networks

## 17.2 Persistence

Current runtime config is hardcoded.

Needed later:

- authoring/config component
- persisted left/right filters
- UI for filter assignment
- save/load behavior

## 17.3 Multiplayer/server authority

System is currently `ServerSimulation`.

Need future validation for:

- host/client behavior
- dedicated server behavior
- save reload behavior
- client visuals
- mod mismatch behavior

## 17.4 Orientation support

Current tests use one orientation.

Need future tests for:

- rotated splitters
- vertical splitters
- different input directions
- multiple feeder styles
- non-robot-arm input

---

# 18. Backend lessons for future automation mods

## Lesson 1: ECS buffers often define topology

For automation, buffers such as:

```text
MoversWithSharedStateBuffer
```

can define active graph connections.

## Lesson 2: ECS component values may control math

`MoverCD.splitsIntoOnMove` controls stack split math.

Changing graph topology without changing math causes item loss.

## Lesson 3: Orchestrators manage shared state

`MoverOrchestratorCD` coordinates shared movers and cycling.

Output alternation is not random; it is orchestrator state.

## Lesson 4: Enableable components matter

`EnabledMoverFromSharedStateCD` was essential.

Future automation mods should inspect enableable components, not just regular data fields.

## Lesson 5: Timing matters more than proximity

A correct ECS mutation applied too late still fails.

Successful routing required:

```text
detect before vanilla split execution
hold state long enough
allow already-on-belt item to override feeder prediction
```

## Lesson 6: Prefer reshaping vanilla ECS inputs over patching

Current best practice:

```text
change ECS state so vanilla performs the desired behavior naturally
```

Instead of:

```text
replace or patch vanilla logic
```

---

# 19. Current recommended implementation direction

Short term:

- keep the current ECS architecture
- disable noisy logs
- continue stress testing
- test rotated splitter orientations
- test more item types
- test higher stack sizes
- test non-matching item behavior
- test multiple splitters nearby

Medium term:

- add actual UI/filter config
- persist filter config
- clean up debug-only code
- make route hold smarter
- add optional diagnostics toggle

Long term:

- solve no-match blocking
- validate multiplayer/dedicated server behavior
- generalize for arbitrary filters/categories
- document reusable automation helper utilities

---

# 20. Final checkpoint summary

## Proven

- Runtime splitter buffer mutation works.
- Safe one-sided routing works.
- Full stacks can route to one side without splitting.
- Moving item identity can be discovered.
- Robot arm carried item identity can be discovered.
- Vanilla splitter behavior can be safely redirected by ECS state mutation.
- `EnabledMoverFromSharedStateCD` is required for deterministic routing.
- Belt item priority over feeder prediction is required for rapid mixed inputs.

## Superseded / wrong

- `MoverFilterCD` does not solve splitter routing.
- Buffer-only routing is unsafe.
- Split-count-only routing is insufficient.
- Proximity-only routing is unreliable.
- Harmony is not required for the current working prototype.

## Current architecture

```text
Observe belt item first
else observe feeder carried item
arm route
apply ECS mutations
vanilla automation executes naturally
```

## Current status

```text
WORKING ECS PROTOTYPE
```

## Confidence level

High for the tested scenario:

```text
robot arm feeding splitter via belt,
dirt left,
turf right,
stacked mixed input.
```

Medium for generalized production mod until more testing covers:

```text
rotations,
multiple splitters,
unmatched items,
persistence,
multiplayer.
```


---

# 21. Electricity-gated Smart Splitter architecture

## Goal

The Smart Splitter must behave as:

```text
UNPOWERED:
    vanilla splitter behavior

POWERED:
    Smart Splitter behavior
```

This requirement became extremely important because:

- vanilla alternating split behavior is useful by itself
- users should be able to disable Smart Splitter logic through world automation/electricity
- Smart Splitter should integrate naturally into existing automation systems

---

## Electricity research findings

### Important discovery

The splitter itself does not directly expose a simple:

```text
HasElectricity = true/false
```

state on the orchestrator entity.

Instead, electricity state must be inferred from nearby automation/electricity ECS entities.

### Final practical implementation

The current implementation performs:

```text
small-radius nearby mover/electricity scan
```

around the splitter center.

Observed successful detection sources:

- generators
- powered robot arms
- nearby powered automation entities

### Critical refinement

Originally the electricity radius was too large.

This caused:

```text
splitter became powered by unrelated nearby automation
```

Final validated direction:

```text
Only detect electricity within approximately one tile of the splitter.
```

This better matches user expectations:

```text
power must actually connect to the splitter
```

---

## Powered vs unpowered runtime behavior

### Unpowered

The runtime restores pure vanilla splitter state:

```text
MoversWithSharedStateBuffer:
    restore left mover
    restore right mover

splitsIntoOnMove:
    restore 2

EnabledMoverFromSharedStateCD:
    both enabled

MoverOrchestratorCD:
    cycling restored
```

Meaning:

```text
vanilla alternation
vanilla stack splitting
vanilla routing
```

### Powered

The runtime allows:

```text
LEFT_ONLY
RIGHT_ONLY
BOTH
FORWARD_ONLY (prototype passthrough topology)
```

depending on item/filter matching.

---

# 22. Forward passthrough / third-direction topology research

## Original blocker problem

The original unresolved problem:

```text
What happens when neither left nor right output matches?
```

The Smart Splitter needed a safe behavior that:

- does not delete items
- does not jam automation permanently
- supports multiple splitters on one belt
- supports future large-scale filter systems

---

## Earlier rejected approaches

### Approach: true blocking

Idea:

```text
leave item in splitter center
```

Problems:

- items accumulated on ground
- FPS/performance risk
- deadlocks
- ugly behavior
- difficult recovery

Status:

```text
REJECTED
```

---

### Approach: feeder-side filtering

Idea:

```text
robot arm only inserts valid items
```

Problems:

- many feeder types exist
- impossible to centralize reliably
- timing conflicts
- multiple splitters become difficult
- non-robot-arm automation unsupported

Status:

```text
REJECTED / TOO COMPLEX
```

---

### Approach: teleport/nudge/synthetic movers

Earlier runtime prototypes attempted:

```text
teleporting
movee nudging
synthetic bridge movers
temporary movee target overrides
```

Observed results:

- flickering
- teleporting
- vanilla reclaiming ownership
- unstable movement
- forward belt not recognizing ownership
- item snapping back left/right

Critical discovery:

```text
Vanilla splitter redistribution still executed AFTER movement completion.
```

Meaning:

```text
belt movement phase
!=
splitter redistribution phase
```

This became the key architectural discovery.

Status:

```text
SUPERSEDED
```

---

# 23. Splitter orientation and topology discoveries

## Orientation probe findings

Dedicated topology/orientation probes confirmed:

```text
north/south lane behaves as input corridor
left/right are orchestrated shared outputs
```

Observed example:

```text
south=(11,4)->(11,5)
west =(11,5)->(10,5)
east =(11,5)->(12,5)
```

Meaning:

```text
input reaches center first
then splitter redistributes afterward
```

This explained why input-mover hijacking alone failed.

---

## Massive architectural discovery

The splitter internally behaves more like:

```text
INPUT
    ->
CENTER BUFFER/STATE
    ->
OUTPUT REDISTRIBUTION
```

not:

```text
INPUT directly owns output path
```

This discovery changed the roadmap completely.

---

# 24. Forward topology prototype

## Final successful architecture

Instead of trying to fight vanilla redistribution through hacks:

```text
we extended topology itself
```

This became the first successful forward passthrough implementation.

---

## Core idea

The splitter temporarily becomes:

```text
LEFT
RIGHT
FORWARD
```

instead of:

```text
LEFT
RIGHT
```

for blocked/unmatched items.

---

## Key implementation

### Forward mover hijack

The runtime temporarily repurposes the real forward conveyor mover:

```text
center -> forward continuation
```

instead of creating synthetic fake ownership.

This was the major breakthrough.

---

## Important detail: conveyor ownership seam

Critical discovery:

```text
(11,5)->(11,6)
```

caused ownership handoff failure.

The forward belt did not fully "pick up" the item.

But:

```text
(11,5)->(11,7)
```

worked consistently because the item landed deeply enough inside the next conveyor ownership zone.

---

## Final speed fix

The two-tile passthrough caused acceleration because:

```text
2 tiles
1 moveTime
```

Result:

```text
item launched forward too quickly
```

Final validated fix:

```text
keep:
    center -> two-tile forward stop

but:
    double moveTime
```

Meaning:

```text
2 tiles
2x move duration
```

which restored normal conveyor speed while preserving proper ownership transfer.

---

# 25. Final working forward topology behavior

## Blocked/unmatched items

Current validated prototype:

```text
1. detect unmatched item
2. suppress left/right routing
3. temporarily repurpose forward belt mover
4. create center -> forward passthrough route
5. increase moveTime proportionally
6. restore normal topology afterward
```

Result:

```text
smooth forward passthrough
no flicker
no teleport
no ownership failure
normal conveyor speed
```

---

## Matched items

### Left-only match

```text
route LEFT_ONLY
splitsIntoOnMove = 1
left enabled
right disabled
```

### Right-only match

```text
route RIGHT_ONLY
splitsIntoOnMove = 1
right enabled
left disabled
```

### Both-sides match

```text
restore BOTH
splitsIntoOnMove = 2
vanilla alternation restored
```

---

# 26. Important safety discoveries

## Topology cleanup crashes

Breaking belts/splitters during active routing caused ECS crashes:

```text
NullReferenceException
SetComponentData(...)
```

Root cause:

```text
cleanup attempted writing to destroyed entities/components
```

Final required rule:

Before ANY runtime ECS write:

```csharp
if (!EntityManager.Exists(entity))
    return;

if (!EntityManager.HasComponent<T>(entity))
    return;
```

especially for:

- SmartSplitterArmedRouteCD
- MoverCD
- orchestrator entities
- temporary forward mover ownership

This became mandatory for safe runtime topology mutation.

---

# 27. Current validated Smart Splitter capabilities

Current prototype now supports:

```text
✓ Electricity-gated smart behavior
✓ Vanilla fallback when unpowered
✓ Left-only routing
✓ Right-only routing
✓ Both-output vanilla alternation
✓ Full-stack preservation
✓ Rapid mixed-item routing
✓ Forward passthrough topology
✓ Third-direction reject lane
✓ No Harmony
✓ ECS-only runtime mutation
✓ Real conveyor ownership transfer
✓ Runtime topology mutation
```

---

# 28. Updated architecture understanding

The current understanding of vanilla automation is now:

```text
Input mover
    ->
center movement state
    ->
splitter redistribution topology
    ->
output mover ownership
```

Meaning:

```text
topology matters more than geometry
```

This became the single most important lesson from the passthrough research.

---

# 29. Recommended future directions

## Short term

Continue validating:

- rotated splitter orientations
- larger automation networks
- multiple chained splitters
- throughput stress tests
- non-robot-arm feeders
- long-duration runtime stability

## Medium term

Add:

- proper filter UI
- persistent saved filters
- configurable reject lane behavior
- optional hard-block mode
- player-configurable passthrough rules

## Long term

Potential future systems now enabled by this research:

```text
smart overflow lanes
priority sorting
multi-stage logistics
multi-way topology routers
advanced conveyor intersections
automation balancing systems
dynamic rerouting networks
```

The topology discoveries made during Smart Splitter development are likely reusable for many future Core Keeper automation mods.
