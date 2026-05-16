# Smart Splitter Mod — Professional Technical Handoff for Restructure / Rebuild

**Project:** Core Keeper Smart Splitter Mod  
**Audience:** New implementation owner / Claude / another coding agent taking over from a research prototype  
**Authoring context:** Core Keeper Mod SDK, Unity 6000.0.59f2, ECS-heavy vanilla automation internals  
**Primary goal:** Hand off everything learned so the next implementation can restructure the mod cleanly instead of extending the current messy prototype.

---

## 0. Executive Summary

The Smart Splitter project set out to create a Core Keeper splitter variant that can route items by filter rules instead of vanilla alternating/splitting behavior. The target gameplay behavior evolved into:

```text
Unpowered Smart Splitter:
    behave exactly like vanilla Conveyor Belt Splitter

Powered Smart Splitter:
    evaluate configured lane filters
    send matching items left/right without splitting stacks unnecessarily
    optionally send unmatched items forward as a passthrough/reject lane
    expose a UI similar to Robot Arm filtering UI
```

The project produced one **high-value, production-relevant backend discovery** and one **low-quality, likely-to-rebuild frontend/UI layer**.

### What is worth preserving

The strongest part of the work is the ECS/automation research:

- Vanilla splitter behavior can be redirected by mutating the ECS state vanilla automation already consumes.
- `MoversWithSharedStateBuffer`, `MoverCD.splitsIntoOnMove`, `MoverOrchestratorCD`, and `EnabledMoverFromSharedStateCD` together define safe routing state.
- One-sided routing is safe only when the shared-state buffer and split count are kept consistent.
- Moving item identity can be discovered through `DroppedItem` entities with `ContainedObjectsBuffer` and `LocalTransform`.
- Robot arm carried item identity can be discovered through the arm's inventory/entity buffer.
- A belt-item-priority model solved the rapid mixed item misrouting that happened when feeder prediction overwrote the current belt item.
- Forward passthrough became viable only after realizing that the splitter redistributes from a center state into output topology, and that a real forward conveyor mover can be temporarily repurposed.
- Electricity gating should be kept: unpowered should restore vanilla splitter behavior; powered should enable smart behavior.

This backend line of work is broadly aligned with Core Keeper's architecture because it reshapes vanilla ECS inputs and lets vanilla automation continue doing item movement, stack math, and spawning.

### What should probably be restructured

The UI/interactivity layer became hacky and should be treated as experimental:

- We tried to add RobotArm-like interactability to a splitter prefab that was never designed as a vanilla interactable.
- We built a Unity `Canvas`/`Image`/`Button` panel instead of a native Core Keeper UI object structure.
- Core Keeper's cursor is not a normal OS cursor or same-canvas UI cursor; it is a custom `UIMouse` object under the game's `UI Camera`/`Mouse` hierarchy.
- A screen-space overlay panel rendered above the vanilla cursor, so we introduced a duplicate overlay cursor. This is a hack and still has parity problems: cursor offset, held item icon, stack count, click darkening, and pixel-snapped movement.
- Attempts to parent a Unity Canvas into Core Keeper's `IngameUI` / UI camera hierarchy failed to render correctly.
- Attempts to start a SpriteRenderer/native-style rebuild were interrupted by prefab transform issues and not completed.

A serious rebuild should not simply continue patching the overlay cursor hack. It should either:

1. build the panel in a fully vanilla-compatible UI style after studying vanilla UI prefabs/components, or
2. use vanilla `FilteringUI` directly if it can be safely repurposed with a custom `IFilterStructure`/filter data model, or
3. temporarily keep the overlay panel only as a debug UI while backend/filter persistence is implemented.

---

## 1. Environment, Tools, and Local Paths

### 1.1 Official SDK / documentation

Relevant official documentation:

- Core Keeper Modding Documentation: `https://modding.corekeepergame.com/documentation`
- Core Keeper Mod SDK GitHub: `https://github.com/Pugstorm/CoreKeeperModSDK`

The official docs state that the Core Keeper Mod SDK is a Unity project opened with **Unity `6000.0.59f2`**. Earlier use of `6000.0.58f2` was wrong and caused confusion.

### 1.2 Local development paths observed during project

The following paths appeared during the work and should be treated as the user's likely local setup. Verify locally before relying on them:

```text
SDK project root:
C:\Users\MariusAlbu\Desktop\ck mods\CoreKeeperModSDK

Main mod folder inside SDK:
C:\Users\MariusAlbu\Desktop\ck mods\CoreKeeperModSDK\Assets\SmartSplitterMod

Typical scripts path:
Assets\SmartSplitterMod\Scripts
Assets\SmartSplitterMod\Scripts\Controllers
Assets\SmartSplitterMod\Scripts\Debug
Assets\SmartSplitterMod\Scripts\Visuals
Assets\SmartSplitterMod\Scripts\Components
Assets\SmartSplitterMod\Scripts\Authoring

Prefab path:
Assets\SmartSplitterMod\Prefabs

UI graphics path used during panel authoring:
Assets\SmartSplitterMod\Graphics\UI

Ripped/reference assets path mentioned in prior workflow:
Tools\CoreKeeperRippedAssets

Steam game install from logs:
E:\SteamLibrary\steamapps\common\Core Keeper

Managed assemblies from logs:
E:\SteamLibrary\steamapps\common\Core Keeper\CoreKeeper_Data\Managed

Runtime mods/output area referenced during debugging:
E:\SteamLibrary\steamapps\common\Core Keeper\CoreKeeper_Data\StreamingAssets\Mods

Temporary mod loader compile/cache path observed in logs:
C:\Users\MARIUS~1\AppData\Local\Temp\Pugstorm\Core Keeper\ModLoader\SmartSplitterMod\...
```

### 1.3 Project files and artifacts that matter

Current/known files from the prototype:

```text
SmartSplitterResearch.md
SmartSplitterAssetRegistry.cs
SmartSplitterClientInteractionController.cs
SmartSplitterVisualSwapController.cs
SmartSplitterPanelController.cs
SmartSplitterDebugSettings.cs
SmartSplitterInteractableController.cs                // Option A experiment, likely remove/rebuild
SmartSplitterInteractableBridge.cs                    // Option A experiment, likely remove/rebuild
UIManager.cs                                          // dnSpy extracted class, critical for UI behavior
UIMouse.cs                                            // dnSpy extracted class, critical for cursor behavior
RobotArm.cs                                           // dnSpy extracted class, important UI/filter reference
InteractableObject.cs                                 // dnSpy extracted class, important interaction reference
InteractablePostConverter.cs                          // dnSpy extracted class, important interaction conversion reference
EntityMonoBehaviour.cs                                // dnSpy extracted class, important GO/ECS bridge reference
PlayerController.cs                                   // dnSpy extracted class, large but useful for player input/interaction state
ConveyorBeltSplitter.cs                               // reference class where available, but actual split path was later in InventoryUtility
```

Important project note: AssetRipper exports were used as **reference only**. They are not the game itself and should not be treated as an executable/mod source project. The SDK project is the actual mod authoring environment.

---

## 2. Core Keeper / SDK Constraints That Shaped the Work

### 2.1 Unity version

Use Unity `6000.0.59f2`. The docs call out that specific version. Do not use `6000.0.58f2` unless the official documentation changes.

### 2.2 SDK-first philosophy

The project repeatedly ran into problems when guessing vanilla behavior. The safe workflow is:

1. consult official SDK documentation,
2. inspect SDK examples and project structure,
3. inspect vanilla assemblies in dnSpy,
4. run a minimal probe,
5. only then implement behavior.

### 2.3 Avoid Harmony / reflection-heavy approaches unless proven allowed

Early Harmony attempts failed with mod-loader/security restrictions such as illegal references to `HarmonyLib.Harmony`. Reflection/AppDomain-style approaches are also not in line with the SDK-safe approach. The current working backend deliberately avoids Harmony.

The practical result: prefer ECS systems/components/state mutation and vanilla-exposed SDK APIs over runtime method patching.

### 2.4 Core Keeper's architecture is hybrid GameObject + ECS

This is central. Many placed objects have graphical GameObjects/prefabs, but gameplay state and automation behavior live in ECS. For splitters especially, the visible splitter prefab is not the owner of routing behavior. The important runtime state lives in ECS components/buffers on orchestrator/output mover entities.

### 2.5 Unity prefabs/assets are used, but not like ordinary standalone Unity gameplay code

GameObjects/MonoBehaviours are useful for visuals and UI scaffolding, but Core Keeper frequently converts authoring data into ECS or uses custom systems/components. Do not assume adding an ordinary Unity `Button`, `Canvas`, `SpriteRenderer`, or `InteractableObject` automatically integrates into vanilla systems. It often does not.

---

## 3. Chronological Development History and Lessons

This section is intentionally long because the next owner should understand not just the final decisions, but why multiple tempting approaches failed.

---

## 4. Phase 1 — Basic Smart Splitter Concept: Dirt Left, Turf Right

### 4.1 Initial target

The first target was simple:

```text
Dirt should go left.
Turf should go right.
If the item matches one side, do not split the stack.
```

The initial intuition was to use vanilla filter components. A vanilla Robot Arm has configurable filtering, so it seemed likely a splitter's output movers could be given filters.

### 4.2 Vanilla filter model investigated

Relevant component:

```csharp
MoverFilterCD
```

Observed fields:

```csharp
FilterType filterType;
ObjectID filterObject;
int filterVariation;
ObjectCategoryTag filterCategory;
```

Observed filter types:

```text
None
Whitelist
Blacklist
CategoryWhitelist
CategoryBlacklist
```

Relevant method discovered:

```text
InventoryUtility.ItemMatchesObjectFilter(...)
```

### 4.3 Failed approach: put filters on left/right output movers

Tested concept:

```text
left output mover  = whitelist dirt
right output mover = blacklist dirt
```

Outcome:

- Dirt still split evenly.
- Splitter output distribution did not respect `MoverFilterCD`.

Conclusion:

```text
MoverFilterCD is used by automated pickup/filter systems, not by vanilla splitter output distribution.
```

This was one of the first major discoveries: vanilla splitters are not just filterable output movers.

### 4.4 Recommended future interpretation

A future rebuild should not try to make vanilla `MoverFilterCD` directly solve splitter routing. It may still be useful as a data shape for user-configured filters, but not as the routing mechanism itself.

Good direction:

```text
Use the same filter schema conceptually:
    FilterType
    ObjectID
    variation
    ObjectCategoryTag

But evaluate it in custom Smart Splitter logic before mutating splitter topology.
```

---

## 5. Phase 2 — Understanding Vanilla Splitter ECS State

### 5.1 Key assemblies involved

Automation research centered on:

```text
Pug.Automation.dll
Pug.Automation.Components.dll
Pug.Automation.Authoring.dll
Pug.Automation.Conversion.dll
Pug.ECS.Components.dll
Pug.Other.dll
Pug.Objects.dll
Pug.Base.dll
PugProperties.dll
```

### 5.2 High-level vanilla automation flow discovered

The simplified flow became:

```text
PugAutomationSystem.OnUpdate(...)
    schedules automation jobs
        ↓
CycleEnabledMoversJob
    updates enabled mover state for shared movers
        ↓
MoverMoveAndPickupJob.Execute(...)
    moves items / handles pickup / drop / split behavior
        ↓
Inventory.InventoryUtility.SplitItemAndDropFromMover(...)
    splits stacks and spawns output dropped item entities
```

Important correction: earlier research sometimes referred to `ConveyorBeltSplitter.SplitItemAndDropFromMover(...)`. Later inspection established the relevant confirmed method in the current assembly as:

```text
Inventory.InventoryUtility.SplitItemAndDropFromMover(...)
```

### 5.3 Components that actually matter

The real splitter behavior emerges from:

```text
MoverCD
MoversWithSharedStateBuffer
MoverOrchestratorCD
EnabledMoverFromSharedStateCD
contained object buffers / dropped items
vanilla automation jobs
```

#### MoverCD

Relevant fields:

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

The key field for split math is:

```csharp
splitsIntoOnMove
```

#### MoversWithSharedStateBuffer

Relevant structure:

```csharp
DynamicBuffer<MoversWithSharedStateBuffer>
```

Fields:

```csharp
Entity moverEntity;
int2 cachedDirection;
int2 cachedStart;
```

This buffer stores the active shared output movers for a splitter orchestrator/root entity.

#### MoverOrchestratorCD

Relevant fields:

```csharp
int enabledMoverIndex;
int nextMoverCycleIncrement;
```

This controls vanilla active mover cycling / alternation.

#### EnabledMoverFromSharedStateCD

This enableable component was a key missing piece. Vanilla `CycleEnabledMoversJob` uses it to enable the currently selected shared mover and disable others.

Critical conclusion:

```text
It is not enough to rewrite the shared-state buffer.
It is not enough to set enabledMoverIndex.
The selected output mover must have EnabledMoverFromSharedStateCD enabled and non-selected movers disabled.
```

### 5.4 Splitter identity / topology detection

A placed vanilla splitter can be identified by:

```text
orchestrator/root entity has MoversWithSharedStateBuffer
buffer length is usually 2 for vanilla splitter setup
output movers have MoverCD with splitsIntoOnMove = 2
```

Contrast with normal conveyor:

```text
normal conveyor: buffer length = 1, splitsIntoOnMove = 0
splitter:        buffer length = 2, output movers split into 2
```

### 5.5 Splitter root vs output movers

Observed splitter orchestrator/root:

```text
Has MoverCD = false
Has MoversWithSharedStateBuffer = true
Has MoverFilterCD = false
Has ObjectFilteringCD = false
```

Output movers:

```text
Have MoverCD = true
splitsIntoOnMove = 2
inventoryEntity = Entity.Null
indexInOrchestrator = 0 or 1
```

This matters because code that tries to inspect `inventoryEntity` on splitter output movers will not find the moving item.

---

## 6. Phase 3 — Stack Split Math and Safe One-Sided Routing

### 6.1 Split math discovered

`SplitItemAndDropFromMover(...)` effectively does:

```text
num  = amount / splits
num2 = amount % splits
```

The important parameter is the split count, tied to `MoverCD.splitsIntoOnMove` / the job argument.

### 6.2 Failed / dangerous experiment: remove one buffer output only

Test:

```text
MoversWithSharedStateBuffer = one output only
splitsIntoOnMove remains 2
```

Result:

- only one output received items,
- but vanilla still subtracted the two-way split amount,
- half the stack vanished.

This is the most important safety warning in the whole backend.

### 6.3 Successful experiment: buffer removal + split count = 1

Test:

```text
MoversWithSharedStateBuffer = one output only
all relevant splitter mover split counts = 1
```

Result:

- full stack went to the selected side,
- no item loss,
- non-selected side received nothing.

This proved safe one-sided routing.

### 6.4 Correct runtime states

#### Left only

```text
MoversWithSharedStateBuffer:
    clear
    add left mover only

MoverCD.splitsIntoOnMove:
    set relevant splitter movers to 1

MoverOrchestratorCD:
    enabledMoverIndex = left mover index
    nextMoverCycleIncrement = 0

EnabledMoverFromSharedStateCD:
    left enabled = true
    right enabled = false
```

#### Right only

```text
MoversWithSharedStateBuffer:
    clear
    add right mover only

MoverCD.splitsIntoOnMove:
    set relevant splitter movers to 1

MoverOrchestratorCD:
    enabledMoverIndex = right mover index
    nextMoverCycleIncrement = 0

EnabledMoverFromSharedStateCD:
    right enabled = true
    left enabled = false
```

#### Both / vanilla restore

```text
MoversWithSharedStateBuffer:
    restore left mover
    restore right mover

MoverCD.splitsIntoOnMove:
    set relevant splitter movers to 2

MoverOrchestratorCD:
    nextMoverCycleIncrement = 1

EnabledMoverFromSharedStateCD:
    left enabled = true
    right enabled = true
```

### 6.5 What to preserve in a rebuild

This ECS mutation model is the most valuable part of the project. Do not throw it away. It is the closest thing we have to a clean Core Keeper-aligned backend.

---

## 7. Phase 4 — Discovering Moving Item Identity

### 7.1 First wrong assumption

The team initially looked for moving item identity on splitter output movers through `MoverCD.inventoryEntity`.

Observed:

```text
splitter output mover inventoryEntity = Entity.Null
```

Conclusion:

```text
Splitter output movers do not directly carry item stack identity.
```

### 7.2 DroppedItem entity discovery

Moving belt items are represented as `DroppedItem` entities:

```text
DroppedItem entity
├── ObjectDataCD = DroppedItem
├── ContainedObjectsBuffer
│   └── actual item stack
└── LocalTransform
```

Example:

```text
ObjectDataCD.objectID = DroppedItem
ContainedObjectsBuffer[0].objectID = WallDirtBlock
ContainedObjectsBuffer[0].variation = 0
ContainedObjectsBuffer[0].amount = 88
```

The real item identity is in `ContainedObjectsBuffer`.

### 7.3 MoveeCD discovery

There are also `MoveeCD` entities:

```text
Movee entity
├── MoveeCD
└── movement position/state
```

But the Movee entity does not contain the item identity. The correlation is positional:

```text
DroppedItem.LocalTransform.Position ≈ MoveeCD.position
```

### 7.4 Robot arm carried item discovery

Robot arms expose carried item identity through their inventory/entity buffer:

```text
Robot arm mover / arm entity
└── inventoryEntity or associated inventory buffer
    └── ContainedObjectsBuffer
        └── carried item
```

Example observed:

```text
objectID = WallDirtBlock
variation = 0
amount = 1
```

### 7.5 Debug probe framework

A permanent toggleable debug/probe system was created because temporary probe scripts caused stale compiled assemblies and mod loader/cache issues. This is a good idea and should be preserved conceptually:

```text
SmartSplitterDebugSettings
SmartSplitterDebugProbeSystem / specific probes
```

Probes confirmed the ability to inspect:

```text
splitter orchestrators
output movers
mover filters
contained objects
movees
dropped items
robot arm carried items
nearby moving entities
player entities
equipped/inventory-related object states
```

---

## 8. Phase 5 — Timing, Pre-Arming, and Belt Priority

### 8.1 Proximity-only approach was not production safe

Initial runtime strategy:

```text
detect nearby DroppedItem
set splitter LEFT_ONLY / RIGHT_ONLY / BOTH for a short window
let vanilla automation run
restore later
```

This proved useful but was not stable enough:

- route state could apply too late,
- subsequent items could enter during the wrong temporary route,
- mixed item streams misrouted,
- state was splitter-global rather than per-item.

### 8.2 Forced right-only isolation test

A forced right-only test showed:

```text
if the ECS state is already correct when vanilla automation runs,
then full-stack one-sided routing works.
```

This proved the problem was timing/arming logic, not the ECS mutation idea.

### 8.3 Robot arm feeder pre-arm solved late timing for arm-fed setups

A robot arm feeding the splitter exposes the next carried item before it reaches the splitter. Using this to pre-arm the route fixed the “too late” issue for the test setup.

### 8.4 Feeder prediction alone caused misroutes

If robot arm carried-item prediction was always allowed to overwrite splitter route state, the arm's next item could override the current belt item.

Example failure:

```text
Dirt already on belt approaching splitter.
Robot arm picks up turf.
Feeder prediction switches route to right too early.
Dirt misroutes right.
```

### 8.5 Final working priority rule

The current validated rule:

```text
1. If a DroppedItem exists in the input corridor:
       route based on the frontmost/best incoming DroppedItem.
2. Else:
       route based on feeder/robot arm carried item.
```

This fixed rapid mixed stack tests and sudden item-type switching.

### 8.6 What to preserve

Preserve this priority idea. It is not perfect for every topology, but it is a strong production concept:

```text
actual current belt item > predicted feeder next item
```

A future rebuild should generalize it across orientations and input types.

---

## 9. Phase 6 — Routing Decision Model

### 9.1 Prototype hardcoded rules

Current prototype rule shape:

```text
Left:
    ObjectID.WallDirtBlock
    variation 0

Right:
    ObjectID.WallTurfBlock
    variation 0
```

Decision model:

```text
matches left only  -> LEFT_ONLY
matches right only -> RIGHT_ONLY
matches both       -> BOTH
matches neither    -> BLOCKED / fallback
```

### 9.2 Current unresolved `Blocked` semantics

The true blocked/no-match case is not fully solved in the early two-output model.

Unsafe:

```text
buffer length = 0
or disabling outputs without understanding split math
```

Potential safe strategies:

```text
1. Keep both outputs active as fallback.
2. Add explicit forward/reject lane.
3. Disable upstream pickup/hold before splitter.
4. Use vanilla pickup/filtering behavior before the item enters the splitter.
5. Implement a real hard-block mode only after proving no deletion/deadlock.
```

The later forward passthrough research made option 2 the most promising.

---

## 10. Phase 7 — Forward Passthrough / Third Output Topology

### 10.1 Why this mattered

If an item matches neither left nor right, a smart splitter ideally should not delete it, jam permanently, or randomly split it. A forward passthrough/reject lane is a natural answer:

```text
left = matching lane A
right = matching lane B
forward = no-match / overflow / reject
```

### 10.2 Failed early attempts

Several approaches were tried or considered:

```text
teleporting items
nudging MoveeCD positions
synthetic bridge movers
temporary movee target overrides
hard-blocking at center
```

These were unstable:

- flicker,
- teleporting,
- vanilla reclaiming ownership,
- forward belt not picking up items,
- snapping back left/right,
- possible deadlocks.

### 10.3 Key topology realization

The splitter behaves more like:

```text
input mover
    -> center movement/state
    -> splitter redistribution topology
    -> output mover ownership
```

not:

```text
input directly owns final path
```

This changed the approach from “move the item forward manually” to “change the topology vanilla uses.”

### 10.4 Successful forward mover hijack concept

The breakthrough was to temporarily repurpose the real forward conveyor mover, effectively creating a third output route:

```text
center -> forward continuation
```

Important seam discovery:

```text
center -> one-tile forward stop
    caused ownership handoff failure in some cases

center -> two-tile forward stop
    worked consistently because the item landed deeper into the next conveyor ownership zone
```

But two tiles in one move caused acceleration:

```text
2 tiles / 1 moveTime = too fast
```

Fix:

```text
2 tiles / 2x moveTime = normal conveyor speed
```

### 10.5 Current validated forward behavior

For unmatched items, the prototype can:

```text
1. detect unmatched item
2. suppress left/right routing
3. temporarily repurpose forward belt mover
4. create center -> forward passthrough route
5. increase moveTime proportionally
6. restore normal topology afterward
```

Result observed:

```text
smooth forward passthrough
no flicker
no teleport
no ownership failure
normal conveyor speed
```

### 10.6 Recommendation for rebuild

If Claude is rebuilding, the forward topology work should be considered valuable, but it needs a clean abstraction:

```csharp
SmartSplitterTopologySnapshot
SmartSplitterRouteApplication
SmartSplitterForwardPassthroughState
```

Do not leave temporary mover hijacking scattered through one large system. Every mutation must be paired with safe cleanup and entity existence checks.

---

## 11. Phase 8 — Electricity Gating

### 11.1 Requirement

The Smart Splitter should integrate with Core Keeper automation/electricity:

```text
Unpowered:
    vanilla splitter behavior

Powered:
    smart routing behavior
```

### 11.2 Discovery

The splitter orchestrator did not simply expose a single obvious `HasElectricity` boolean. Electricity had to be inferred from nearby electricity/automation entities and/or components.

### 11.3 Radius problem

Early detection radius was too large, causing splitters to be powered by unrelated nearby automation. The intended behavior is closer to:

```text
power must be adjacent / connected within about one tile
```

### 11.4 Current direction

Keep electricity gating. It is a good game design and a good separation between vanilla and smart behavior.

When unpowered, force restore:

```text
buffer = original left + original right
splitsIntoOnMove = 2
both output movers enabled
orchestrator cycling restored
```

When powered, allow:

```text
LEFT_ONLY
RIGHT_ONLY
BOTH
FORWARD_ONLY / passthrough
```

### 11.5 Rebuild suggestion

Make electricity detection a dedicated service/helper, not mixed with route application:

```csharp
SmartSplitterElectricityService
SmartSplitterPowerStateCache
```

It should return a stable boolean and diagnostic reason:

```text
PoweredByAdjacentGenerator
PoweredByAdjacentElectricityConnection
UnpoweredNoAdjacentSource
UnknownComponentShape
```

---

## 12. Phase 9 — Visual Powered/Unpowered Smart Splitter Asset Swap

### 12.1 Goal

When powered, the splitter should visually look like a Smart Splitter. When unpowered, it should look like a vanilla splitter.

### 12.2 First confusion: creating Unity asset vs changing SpriteAsset

A lot of time went into understanding the relationship between:

```text
Unity prefab / GameObject
SpriteObject component
Pug Sprite/SpriteAsset
regular Unity SpriteRenderer
DataBlockRef<SpriteAsset>
```

The game uses custom `SpriteObject`/Pug sprite structures extensively. The user wanted `smartsplitter.asset` to be usable at runtime without it immediately inheriting the vanilla `conveyorbeltsplitter.asset` sprite.

### 12.3 Final practical visual approach

A prefab-authored powered visual swap was implemented:

```text
SmartSplitterAssetRegistry
SmartSplitterVisualSwapController
SmartSplitterVisual.prefab
```

General idea:

```text
when splitter is powered:
    instantiate/use SmartSplitterVisual prefab or custom sprite asset/material
    align it over the vanilla splitter graphical object
    hide/suppress vanilla visible sprite where needed

when unpowered:
    restore vanilla visual
```

### 12.4 Registry lesson

The registry must not create an empty runtime object that overwrites valid prefab-assigned data.

Good pattern used:

```csharp
SmartSplitterAssetRegistry.RegisterLoadedObject(Object obj)
CaptureRegistryData(source, "ModObjectLoaded")
TryGetSmartVisualPrefab(...)
TryGetSmartPanelPrefab(...)
```

Important warning from code comments:

```text
Do not AddComponent() an empty SmartSplitterAssetRegistry at runtime.
An empty Awake()/instance can overwrite valid mod prefab data captured from ModObjectLoaded().
```

### 12.5 What to preserve

The registry concept is good. Keep it, but clean it:

```text
AssetRegistry should only register authored assets/prefabs.
No accidental empty runtime registry creation.
Separate visual prefab, panel prefab, materials, sprite assets.
```

### 12.6 What to improve

Visual swapping should not be entangled with UI interaction. The current `SmartSplitterVisualSwapController` began taking on too much responsibility: detecting power, applying visual swap, and calling interaction registration. A rebuild should split:

```text
SmartSplitterVisualSystem / Controller
SmartSplitterPowerDetector
SmartSplitterInteractionRegistration
SmartSplitterPanelRuntime
```

---

## 13. Phase 10 — Interactability and Highlighting

### 13.1 Initial desired behavior

The powered Smart Splitter should be interactable like a Robot Arm:

```text
look/aim at powered splitter within interaction range
white outline appears
press E to open filtering panel
```

### 13.2 Option A: bridge RobotArm/InteractableObject behavior

Attempted approach:

```text
add a dynamic InteractableObject / bridge object to the splitter graphical root
try to assign it to EntityMonoBehaviour.interactable
use RobotArm-like callbacks
```

Files involved:

```text
SmartSplitterInteractableController.cs
SmartSplitterInteractableBridge.cs
```

Problems:

- splitter vanilla prefab did not already have RobotArm-style interactability,
- dynamically added `InteractableObject` was not seen by vanilla scanning the way a RobotArm is,
- highlighting did not appear,
- `Use()` / E press callbacks did not trigger reliably or at all,
- `EntityMonoBehaviour`/post-conversion pathways likely require authoring-time conversion data, not late runtime injection.

Conclusion:

```text
Option A was not a reliable path.
The dynamic InteractableObject bridge should be considered experimental/dead code unless rebuilt from authoring/post-conversion principles.
```

### 13.3 Option B: custom selector/highlight system

A custom client interaction controller was built:

```text
SmartSplitterClientInteractionController.cs
```

It detected powered Smart Splitter candidates, approximated the Core Keeper line-of-sight/interact selection, and toggled a white highlight.

Observed player-facing selection behavior from Robot Arm:

```text
It is not pure mouse hover.
The player looks toward the cursor.
The interactable must be near/in front of the player along the player→cursor direction.
Distance must be roughly 1 tile or close.
```

### 13.4 Highlight bugs and fixes

Early custom selection had bugs:

- selected only from top or side depending on axis assumptions,
- stayed highlighted when moving away,
- stayed highlighted when looking away,
- vertical axis handling differed from horizontal,
- use key only worked once every many presses because selection was sampled too rarely or flickered.

Fix direction:

```text
strict aim-line selector
hard distance gate
no sticky selection for highlight
highlighted = selected = usable
input sampled every frame via runtime InteractionDriver MonoBehaviour
```

This eventually made:

```text
E responsive when highlighted
E opens panel reliably
E closes panel if open
ESC closes panel
moving away closes panel
```

### 13.5 What to preserve

The final principle is good:

```text
If highlighted, selected.
If selected, E must work.
Do not rely on exact-frame selection buffers.
Sample input every frame.
```

### 13.6 What to improve

A rebuild should consider integrating with vanilla selection if possible, but if not, a custom selector is acceptable. However, keep it clean:

```text
SmartSplitterSelectionService
SmartSplitterHighlightRenderer
SmartSplitterInputDriver
```

Do not mix selection, panel spawning, cursor duplication, visual swap, and power detection in one file.

---

## 14. Phase 11 — UI Panel Creation

### 14.1 User-authored Photoshop panel

The user created a panel based visually on RobotArm's filtering panel:

```text
Header: Filtering
Columns: Left / Center / Right
Electricity icon near Right
Filter silhouettes
Six slot buttons
Hover highlight sprites
Button hover highlight sprites
Cursor sprite
```

Assets were split into PNG files in:

```text
Assets/SmartSplitterMod/Graphics/UI
```

Examples:

```text
smart_crafting_panel.png
smart_crafting_panel_header.png
smart_crafting_Filtering_text.png
smart_crafting_Left_text.png
smart_crafting_Center_text.png
smart_crafting_Right_text.png
smart_crafting_divider.png
smart_crafting_electricity_on.png
smart_crafting_electricity_off.png
smart_crafting_colorpicker_icon.png
smart_crafting_eraser_icon.png
smart_crafting_button.png
smart_crafting_button_highlight.png
smart_crafting_filter_silhouette_icon.png
smart_crafting_filter_highlight.png
cursor_Inv.png
```

### 14.2 Canvas prefab structure built

A Unity UI prefab was built manually:

```text
SmartSplitterPanel
└── Canvas
    ├── PanelRoot
    │   ├── PanelBody
    │   ├── Header
    │   ├── FilteringText
    │   ├── LeftText
    │   ├── CenterText
    │   ├── RightText
    │   ├── DividerLeft
    │   ├── DividerRight
    │   ├── LeftFilterSilhouette
    │   ├── CenterFilterSilhouette
    │   ├── RightFilterSilhouette
    │   ├── LeftFilterHighlight          inactive by default
    │   ├── CenterFilterHighlight        inactive by default
    │   ├── RightFilterHighlight         inactive by default
    │   ├── LeftFilterButton             invisible Image + Button
    │   ├── CenterFilterButton           invisible Image + Button
    │   ├── RightFilterButton            invisible Image + Button
    │   ├── ElectricityIconOff           inactive by default
    │   ├── ElectricityIconOn
    │   ├── LeftSlot0
    │   │   ├── HoverHighlight           inactive by default
    │   │   └── Icon
    │   ├── LeftSlot1
    │   │   ├── HoverHighlight           inactive by default
    │   │   └── Icon
    │   ├── CenterSlot0
    │   ├── CenterSlot1
    │   ├── RightSlot0
    │   └── RightSlot1
    └── SmartSplitterCursorOverlay       later hack, not in initial design
```

Slots had `Button` components with:

```text
Transition = None
Navigation = None
Raycast Target = true on slot background
Raycast Target = false on icon/highlight children
```

### 14.3 PanelController reference holder

A controller script was added:

```text
SmartSplitterPanelController.cs
```

It held serialized references:

```csharp
Canvas canvas;
RectTransform panelRoot;
GameObject electricityIconOn;
GameObject electricityIconOff;
LaneView leftLane;
LaneView centerLane;
LaneView rightLane;
```

Nested view models:

```csharp
SlotView:
    Button Button
    Image Background
    Image Icon
    GameObject HoverHighlight

LaneView:
    string LaneName
    Button FilterButton
    GameObject FilterHighlight
    Image FilterSilhouette
    SlotView Slot0
    SlotView Slot1
```

### 14.4 What was good here

The visual decomposition and serialized reference model are good. They make a lot of sense for a normal Unity UI panel.

If using overlay UI, this is productive.

### 14.5 What was bad here

Core Keeper's UI is not normal Unity UI. The panel was built as:

```text
Screen Space Overlay Canvas + Unity Images + Buttons
```

Core Keeper's cursor and vanilla UI panels live in a custom UI camera / GameObject/SpriteObject hierarchy, not the same overlay canvas. This caused the panel to render above the vanilla cursor.

---

## 15. Phase 12 — Vanilla UI Behavior Integration

### 15.1 UIManager methods investigated

The `UIManager.cs` dnSpy file revealed key vanilla methods:

```csharp
Manager.ui.OnPlayerInventoryOpen();
Manager.ui.OnFilterWindowOpen();
Manager.ui.HideAllInventoryAndCraftingUI(true);
```

RobotArm path uses:

```csharp
Manager.main.player.SetActiveFilterStructure(this);
Manager.ui.OnFilterWindowOpen();
```

Close path observed:

```csharp
Manager.ui.HideAllInventoryAndCraftingUI(true);
player.SetActiveFilterStructure(null);
```

### 15.2 Important behavior

`OnFilterWindowOpen()` opens vanilla filtering UI. We did not want that because we had custom UI.

`OnPlayerInventoryOpen()` opens the bottom inventory and activates the vanilla inventory-mode behavior:

```text
movement blocked
mouse controls UI rather than aiming
left/right click gameplay actions suppressed
inventory/backpack visible
```

`HideAllInventoryAndCraftingUI(true)` closes inventory/crafting/filter-related UI.

### 15.3 Integration attempted

When custom Smart Splitter panel opens:

```csharp
Manager.ui.OnPlayerInventoryOpen();
SmartSplitterPanelController.Open(...);
```

When it closes:

```csharp
SmartSplitterPanelController.Close(...);
Manager.ui.HideAllInventoryAndCraftingUI(true);
```

This mostly achieved the vanilla panel behavior:

```text
player cannot move
left/right click no longer breaks splitter
inventory opens
ESC/TAB close behavior can be mapped
```

### 15.4 Creative mode side effect

In creative mode, `OnPlayerInventoryOpen()` can also show creative UI panels. This is vanilla behavior but undesirable for smart splitter testing because it can overlap/obscure the custom panel.

Future options:

```text
1. Accept creative side panel in creative worlds.
2. Explicitly hide creative side UI after OnPlayerInventoryOpen(), if a safe vanilla field/method exists.
3. Use a more precise vanilla UI state/mode if discovered.
```

### 15.5 Inventory-open parity issue

Vanilla RobotArm behavior:

```text
If inventory is already open and player presses E on RobotArm:
    inventory closes first
    RobotArm filter panel does not open on that same press

Press E again:
    RobotArm filter panel opens with bottom inventory
```

Smart Splitter originally opened on top of already-open inventory.

Fix goal:

```text
If any normal inventory/crafting UI is already open and Smart Splitter is not open:
    pressing E on Smart Splitter should close existing UI only
    next E press opens Smart Splitter panel
```

This was partially added in later `SmartSplitterPanelController` attempts but should be reimplemented cleanly.

---

## 16. Phase 13 — Cursor Rendering Problem

### 16.1 Core problem

With the custom panel as `Screen Space - Overlay`, the panel rendered above Core Keeper's vanilla cursor. This is a dealbreaker because players cannot see where they are pointing inside the filter UI.

### 16.2 UIMouse investigation

`UIMouse.cs` showed Core Keeper's cursor is not a normal OS cursor and not a Unity `Image` in the same overlay canvas.

Relevant facts:

```text
UIMouse has:
    Transform pointer
    SpriteObject pointerSR
    SpriteRenderer grabbedItemSR
    SpriteRenderer grabbedItemOverlaySR
    SpriteRenderer grabbedItemUnderlaySR
    amountGrabbedNumber
    amountGrabbedNumberOutline
```

Cursor position:

```csharp
this.pointer.localPosition = RoundToPixelPerfectPosition.RoundPosition(
    this.GetMouseUIViewPosition(),
    16f
);
```

Mouse view conversion:

```csharp
PugCamera pugCamera = camera.GetPugCamera();
Vector2 vector = Input.mousePosition;
return pugCamera.TransformMousePosition(vector) / 16f;
```

Click darkening:

```csharp
this.pointerSR.color = (flag ? UIMouse.mouseDownColor : Color.white);
public static readonly Color mouseDownColor = Color.white * 0.7f;
```

Pointer sprite offset:

```csharp
pointerSR.transform.parent.localPosition = new Vector3(0.25f, -0.25f, 0f);
```

Held item rendering:

```csharp
ContainedObjectsBuffer containedObjectData = mouseInventory.GetContainedObjectData(0);
PugDatabase.TryGetObjectInfo(containedObjectData.objectID, out objectInfo, containedObjectData.variation);
Sprite iconOverride = Manager.ui.itemOverridesTable.GetIconOverride(containedObjectData.objectData, false);
grabbedItemSR.sprite = iconOverride ?? objectInfo.icon;
Manager.ui.ApplyAnyIconGradientMap(containedObjectData, grabbedItemSR);
ShouldShowCageOverlay(containedObjectData) toggles grabbedItemOverlaySR / UnderlaySR.
Stack amount is rendered through amountGrabbedNumber / Outline.
```

### 16.3 Attempted fix A: parent panel/canvas into vanilla UI hierarchy

A runtime UI hierarchy probe found:

```text
UI Camera
├── IngameUI
│   ├── PlayerInventoryUI
│   ├── FilteringUI
│   └── other vanilla panels
└── Mouse
    └── Pointer
```

The intuitive fix was to put our panel under `IngameUI` so the cursor under `Mouse` would render above it.

Attempts:

```text
1. switch Canvas to Screen Space - Camera using UI Camera
2. parent Canvas under IngameUI as World Space
3. copy layer from IngameUI
4. position using FilteringUI.localPosition
```

Outcome:

- logs showed panel opened and attached,
- no exceptions,
- still invisible.

Conclusion:

```text
Unity Image/Canvas content did not render correctly inside Core Keeper's vanilla IngameUI world/camera layer.
```

Important caveat discovered later:

```text
The prefab Canvas at one point had width/height 0 and scale 0,0,0.
This could have contaminated some tests, but retesting still did not produce a good native-canvas result.
```

### 16.4 Attempted fix B: rebuild as Core Keeper-style SpriteRenderer/SpriteObject UI

Considered approach:

```text
SmartSplitterPanelWorld
└── PanelRoot
    ├── PanelBody              SpriteRenderer / PugSprite / SpriteObject
    ├── Header
    ├── FilteringText
    ├── Slot backgrounds
    ├── Hover highlights
    └── Icons
```

No Canvas, no Unity `Button`; manual hit testing using vanilla mouse UI coordinates.

This is probably the cleanest long-term native solution, but it was not completed. Early tests were confused by the broken Canvas transform and UI baggage on copied objects. A proper rebuild would require studying vanilla UI object components/materials and likely using `SpriteObject`/Pug sprite components, not just regular `SpriteRenderer`.

### 16.5 Attempted fix C: duplicate overlay cursor

Practical hack:

```text
Keep visible Screen Space Overlay panel.
Add SmartSplitterCursorOverlay as last child of the same Canvas.
Hide/suppress vanilla cursor while Smart Splitter panel is open.
Move overlay cursor with Input.mousePosition.
Mirror vanilla click darkening, held item icon, stack amount, cage overlays as much as possible.
```

This made a cursor visible above the panel, but introduced parity bugs:

- both vanilla and overlay cursor visible at times,
- overlay cursor offset slightly wrong,
- held item icon position/size wrong,
- movement still too smooth rather than pixel-perfect,
- duplicating vanilla held item visuals is complex.

### 16.6 Recommendation

For a professional rebuild, do not settle on overlay cursor unless it is explicitly accepted as a temporary bridge. The better paths are:

```text
Best long-term:
    Build a native-style panel under Core Keeper's UI camera hierarchy using the same rendering/input conventions as vanilla UI.

Potentially better if possible:
    Reuse/extend vanilla FilteringUI instead of building custom Canvas UI.

Temporary only:
    Overlay Canvas + duplicate overlay cursor.
```

---

## 17. Current Prototype Quality Assessment

### 17.1 Backend routing quality

**Status:** strong research prototype; likely salvageable.

Good:

- ECS-only.
- No Harmony.
- Preserves vanilla automation as executor.
- Understands safe split math.
- Understands shared output topology.
- Belt priority over feeder prediction is correct.
- Forward passthrough topology is promising.
- Electricity gating is good design.

Needs cleanup:

- Split into services/systems.
- Ensure every ECS write checks `Exists()` and `HasComponent<T>()`.
- Validate all orientations.
- Validate multiplayer/server authority.
- Replace hardcoded dirt/turf with persistent filters.
- Make route hold times principled rather than conservative magic values.
- Make forward passthrough state restoration robust.

### 17.2 Visual swap quality

**Status:** usable but should be cleaned.

Good:

- Asset registry pattern is useful.
- Powered/unpowered visual behavior is good design.
- Keeping vanilla visual when unpowered is correct.

Needs cleanup:

- Separate power detection, visual swapping, and interaction registration.
- Avoid runtime empty registry overwrite.
- Verify material/sprite asset assignments cleanly.
- Use authored prefab references rather than runtime guesses.

### 17.3 Interaction/highlight quality

**Status:** acceptable custom workaround; possible to keep if vanilla integration remains impractical.

Good:

- Final principle “highlighted = selected = usable” is correct.
- Per-frame input sampling fixed E reliability.
- Strict aim-line/distance gating approximated Core Keeper behavior reasonably.

Needs cleanup:

- Axis math and orientation logic should be centralized/tested.
- Candidate lifecycle and panel state should be separated.
- Do not clear selection just because inventory mode opens after panel open unless intended.
- Implement full vanilla parity for pre-open inventory behavior.

### 17.4 UI quality

**Status:** not production-ready.

Good:

- Art/layout concept is good.
- Slot/lane model is good.
- Serialized references are useful.

Bad:

- Overlay Canvas does not integrate naturally with Core Keeper cursor/UI.
- Duplicate cursor is a hack.
- Native UI parenting attempts failed.
- Held-item cursor parity is hard to duplicate.
- Creative/inventory UI parity still inconsistent.

Recommended restructure:

```text
Either rebuild UI natively using vanilla UI conventions,
or repurpose vanilla FilteringUI.
Do not continue piling hacks onto overlay cursor unless only for debug/testing.
```

---

## 18. Recommended Rebuild Architecture

### 18.1 Suggested high-level modules

```text
SmartSplitterMod.cs
    lifecycle only: EarlyInit / ModObjectLoaded / Init
    registers systems and asset registry

SmartSplitterAssetRegistry.cs
    authored references only
    SmartSplitterVisualPrefab
    SmartSplitterPanelPrefab / native UI prefab
    materials / sprite assets

SmartSplitterRuntimeSystem.cs
    ServerSimulation ECS routing
    does not know UI details

SmartSplitterDiscoveryService.cs
    identifies splitter orchestrators
    caches original topology
    maps left/right/forward movers

SmartSplitterPowerService.cs
    computes powered/unpowered state
    exposes diagnostics

SmartSplitterRoutingService.cs
    evaluates item against filter config
    returns LEFT_ONLY / RIGHT_ONLY / BOTH / FORWARD / BLOCKED

SmartSplitterTopologyApplier.cs
    applies ECS mutations safely
    contains all Exists/HasComponent guards
    owns restore logic

SmartSplitterItemObservationService.cs
    finds DroppedItem in input corridor
    finds feeder carried item fallback
    handles belt priority

SmartSplitterConfigStore.cs
    per-splitter persisted filter settings
    serialization/save/load/multiplayer sync

SmartSplitterClientVisualController.cs
    client-side powered visual swap
    no routing logic

SmartSplitterSelectionController.cs
    client-side highlight/selection/input
    no routing logic

SmartSplitterPanelController.cs / NativeUIController
    UI only
    no ECS route mutation directly
```

### 18.2 Data model proposal

Per splitter persisted config should eventually look like:

```csharp
public struct SmartSplitterConfig
{
    public SmartSplitterLaneFilter Left;
    public SmartSplitterLaneFilter Right;
    public SmartSplitterLaneFilter CenterOrForward; // optional/pass-through behavior
    public SmartSplitterNoMatchMode NoMatchMode;
    public bool RequiresPower;
}

public struct SmartSplitterLaneFilter
{
    public FilterType FilterType;
    public ObjectID Object;
    public int Variation;
    public ObjectCategoryTag Category;
}

public enum SmartSplitterNoMatchMode
{
    VanillaBoth,
    ForwardPassthrough,
    BlockSafely,
    DropOrReject // only if intentionally designed
}
```

Persistence is unresolved. Investigate Core Keeper save/mod persistence patterns in the SDK before choosing ECS component vs custom save file vs authoring data.

### 18.3 Runtime route result proposal

```csharp
public enum SmartSplitterRouteDecision
{
    RestoreVanillaBoth,
    LeftOnly,
    RightOnly,
    ForwardOnly,
    BlockedSafe,
}
```

The route applier should be the only code allowed to mutate:

```text
MoversWithSharedStateBuffer
MoverCD.splitsIntoOnMove
MoverOrchestratorCD
EnabledMoverFromSharedStateCD
forward conveyor mover temporary state
```

### 18.4 Safety checklist for every ECS write

Before writing:

```csharp
if (!entityManager.Exists(entity)) return;
if (!entityManager.HasComponent<T>(entity)) return;
```

For buffers:

```csharp
if (!entityManager.Exists(entity)) return;
if (!entityManager.HasBuffer<MoversWithSharedStateBuffer>(entity)) return;
```

For enableable components:

```csharp
if (!entityManager.Exists(entity)) return;
if (!entityManager.HasComponent<EnabledMoverFromSharedStateCD>(entity)) return;
```

This became mandatory after crashes when belts/splitters were broken during active routing.

---

## 19. Better UI Paths to Investigate Before Implementing Again

### 19.1 Path A — Reuse vanilla FilteringUI

This is potentially the most Core Keeper-aligned path.

Investigate:

```text
RobotArm.cs
UIManager.OnFilterWindowOpen()
filteringUI class
IFilterStructure / active filter structure pattern
Manager.main.player.SetActiveFilterStructure(...)
```

Question:

```text
Can Smart Splitter implement the same interface/structure expected by vanilla FilteringUI?
```

If yes, this would solve:

- cursor rendering,
- inventory behavior,
- held item cursor,
- filter picking mode,
- UI closing behavior,
- vanilla visual parity.

Likely challenge:

- The vanilla filtering UI may be designed for one filter or RobotArm-specific semantics, while Smart Splitter needs left/right/center lane filters.
- It may require extending UI or swapping context tabs.

This path should be investigated before continuing the custom overlay UI.

### 19.2 Path B — Native Core Keeper style custom UI under IngameUI

Build the UI using the same render components as vanilla UI, likely `SpriteObject`/Pug Sprite components, not Unity Canvas `Image`.

Needs investigation:

```text
Vanilla filtering UI prefab hierarchy from AssetRipper
PlayerInventoryUI hierarchy
UI Button/slot components used by vanilla
materials/shaders/layers
how vanilla UI receives mouse hit testing
how UIElement/currentSelectedUIElement works
```

Pros:

- cursor naturally appears above or in same render stack,
- resolution positioning likely easier,
- no duplicate cursor.

Cons:

- more initial work,
- manual hit testing/click handling if not using vanilla UI elements,
- may need unfamiliar Pug UI classes.

### 19.3 Path C — Overlay Canvas + duplicate cursor

This is the current hack.

Pros:

- fast,
- visible,
- Unity Buttons work,
- existing prefab work preserved.

Cons:

- duplicate cursor parity is difficult,
- held item visuals require mirroring vanilla logic,
- possible conflicts with vanilla inventory drag/drop,
- feels fragile and non-native.

Use only as temporary debug UI unless deadlines force it.

---

## 20. Specific UI/Interaction Issues Still Open

### 20.1 Cursor duplication

Problem:

```text
Both vanilla cursor and SmartSplitterCursorOverlay can be visible.
```

Likely because hiding only `pointerSR` is not enough; `UIMouse` updates pointer state every frame/LateUpdate, and held item sprites are separate.

A robust overlay hack must suppress:

```text
Manager.ui.mouse.pointerSR
Manager.ui.mouse.grabbedItemSR
Manager.ui.mouse.grabbedItemOverlaySR
Manager.ui.mouse.grabbedItemUnderlaySR
amount text renderers if accessible
```

But this is fragile.

### 20.2 Cursor offset

Vanilla pointer has internal pivot/offset via `SRPivot` local `(0.25, -0.25, 0)`. Overlay cursor needs empirical pixel offset. Current attempts were close but not exact.

### 20.3 Pixel-perfect cursor movement

Vanilla uses `RoundToPixelPerfectPosition.RoundPosition(..., 16f)` in UI camera coordinates. Overlay canvas using `Input.mousePosition` needs a different conversion. Simple integer snapping was not enough to mimic vanilla exactly.

### 20.4 Held item icon

Vanilla held item is not just an image:

```text
grabbedItemSR.sprite = icon override or objectInfo.icon
ApplyAnyIconGradientMap(...)
ShouldShowCageOverlay(...)
underlay/overlay sprites
stack amount renderers
broken item color
```

Duplicating this in Unity UI `Image` is nontrivial and may never match perfectly.

### 20.5 Inventory-open parity

When inventory is already open:

```text
Smart Splitter E press should close inventory first, not open custom panel on top.
Next E press should open Smart Splitter panel.
```

Add this to the clean selection/input controller, not buried in cursor/UI code.

---

## 21. Current Known Good / Bad Decisions

### Good decisions

```text
✓ SDK/documentation-first workflow
✓ Unity 6000.0.59f2
✓ ECS-only backend, no Harmony
✓ Preserve vanilla automation execution
✓ Mutate topology/split count/enableable movers consistently
✓ Permanent toggleable probes
✓ Belt item priority over feeder prediction
✓ Electricity-gated smart behavior
✓ Restore vanilla behavior when unpowered
✓ Asset registry captured from authored mod object
✓ Visual powered/unpowered swap concept
✓ Per-frame input driver for E responsiveness
```

### Bad or questionable decisions

```text
✗ Trying to solve splitter routing with MoverFilterCD
✗ Buffer-only routing without split-count changes
✗ Proximity-only global route windows as production model
✗ Dynamic InteractableObject bridge added at runtime
✗ Combining visual swap, power detection, and interaction responsibilities
✗ Building production UI as Screen Space Overlay without understanding Core Keeper cursor
✗ Duplicating vanilla cursor as a long-term solution
✗ Continuing to patch huge files instead of splitting services
```

---

## 22. Recommended First Steps for Claude / New Agent

### Step 1 — Freeze and preserve research

Do not delete:

```text
SmartSplitterResearch.md
current working routing prototype
working visual swap prefab/assets
working highlight version
```

Make a branch/backup before rewriting.

### Step 2 — Identify current source state

Search the actual Unity project for:

```text
SmartSplitterRuntimeSystem
SmartSplitterArmedRouteCD
SmartSplitterClientInteractionController
SmartSplitterVisualSwapController
SmartSplitterPanelController
SmartSplitterAssetRegistry
SmartSplitterInteractableController
SmartSplitterInteractableBridge
```

Classify files:

```text
Backend/routing: preserve/refactor
Visual swap: preserve/refactor
Interaction Option B: preserve/refactor
Interactable Option A: remove/archive
Canvas UI/cursor hack: archive or keep as temporary debug UI only
```

### Step 3 — Stabilize backend separately from UI

Before touching UI, validate:

```text
unpowered vanilla splitter still behaves vanilla
powered dirt left/turf right works
full stacks do not split when routed one side
rapid mixed input works
forward passthrough works
breaking belts/splitters does not crash
```

### Step 4 — Refactor backend into services

The backend is the most valuable part. Split it before adding features.

Minimum split:

```text
Discovery
Power detection
Item observation
Filter evaluation
Topology application
Forward passthrough state
Diagnostics
```

### Step 5 — Investigate vanilla UI reuse before writing custom UI

Specifically inspect:

```text
RobotArm.cs
FilteringUI class/prefab
UIManager.OnFilterWindowOpen
Player.SetActiveFilterStructure
IFilterStructure or equivalent
UIMouse.UpdateMouseUIInput
UIElement / currentSelectedUIElement
```

The best solution is likely to become a valid vanilla filter structure if the API allows it.

### Step 6 — Only if vanilla UI cannot support left/right/center filtering, build native custom UI

Use AssetRipper to inspect the actual vanilla filtering UI prefab hierarchy and duplicate its component style. Do not start from Unity `Canvas > Image > Button` unless accepting overlay cursor hacks.

### Step 7 — Implement persistence last, but design for it now

Do not hardcode dirt/turf into production. The UI should write per-splitter filter config into a real persistence model.

---

## 23. Open Technical Questions

Backend:

```text
- Are splitter output indices stable across reloads?
- Is index 0 always visual left in every orientation?
- How should topology mapping work for all rotations?
- How should forward passthrough identify the correct forward conveyor in every orientation?
- Can multiple smart splitters in close proximity interfere with electricity/item scans?
- Does ServerSimulation routing replicate correctly to clients?
- How should filter config persist in save files?
- How should mod mismatch/multiplayer be handled?
```

UI:

```text
- Can Smart Splitter implement vanilla filter structure contracts?
- Can vanilla FilteringUI support multiple lanes, or only one filter context?
- What exact components/materials does vanilla FilteringUI use?
- Can a custom panel be built from vanilla UI elements and participate in UIMouse hit testing?
- How does Core Keeper register/select UI elements?
- What is the safe way to suppress creative UI side panels if using OnPlayerInventoryOpen()?
```

Visuals:

```text
- Should powered visual be a SpriteAsset swap or whole prefab overlay?
- How should rotation/orientation affect powered visual alignment?
- Are sprite materials and gradient maps correct for all paint/colors?
```

---

## 24. Practical Warnings

### 24.1 Do not write ECS components after entity destruction

Breaking a belt/splitter while routing can destroy entities. Always guard writes.

### 24.2 Do not set buffer length and split count inconsistently

This can delete items.

### 24.3 Do not assume UI Canvas render order can beat Core Keeper cursor

Core Keeper cursor is in a different rendering system.

### 24.4 Do not assume AssetRipper project can be run like the game

Use it as reference only.

### 24.5 Do not keep all diagnostics enabled

The project produced many logs. Production/default config should keep logs off and expose targeted toggles.

### 24.6 Do not reuse old Option A files blindly

`SmartSplitterInteractableController` and `SmartSplitterInteractableBridge` were part of a failed interaction bridge experiment. Archive or delete unless rebuilding from a correct authoring/post-conversion basis.

---

## 25. Recommended “Clean Rebuild” Roadmap

### Milestone 1 — Backend core

```text
- Identify splitters and cache original topology.
- Detect power adjacent to splitter.
- Restore vanilla when unpowered.
- Apply LEFT_ONLY / RIGHT_ONLY / BOTH safely when powered.
- Validate dirt/turf full-stack routing.
```

### Milestone 2 — Input item observation

```text
- Detect DroppedItem in input corridor.
- Detect feeder carried item only when no belt item exists.
- Generalize to rotations.
- Add diagnostics UI/logs for item chosen and why.
```

### Milestone 3 — Forward passthrough

```text
- Cleanly identify forward conveyor mover.
- Apply temporary center -> two-tile forward route.
- Double moveTime for two-tile route.
- Restore topology safely.
- Validate no flicker/no acceleration/no crashes.
```

### Milestone 4 — Visual/power feedback

```text
- Powered visual swap cleanly separated from routing.
- Unpowered visual restore.
- Electricity icon state available to UI.
```

### Milestone 5 — UI investigation

```text
- Try vanilla FilteringUI extension/reuse.
- If impossible, prototype native Core Keeper-style custom UI under IngameUI.
- Only use overlay Canvas as temporary debug UI.
```

### Milestone 6 — Config persistence

```text
- Store left/right/forward filters per splitter.
- Save/load.
- Server/client sync.
```

### Milestone 7 — Production hardening

```text
- Orientation matrix tests.
- Multiple splitter tests.
- Multiplayer tests.
- Break/remove while active tests.
- Long-duration throughput tests.
- Logging cleanup.
```

---

## 26. Minimal “Do This / Don’t Do This” for New Agent

### Do this

```text
DO keep ECS routing discoveries.
DO mutate vanilla ECS inputs rather than patch vanilla methods.
DO restore vanilla state when unpowered.
DO guard every ECS write.
DO separate power, discovery, routing, topology, UI.
DO investigate vanilla FilteringUI before custom UI.
DO treat UI as currently experimental.
```

### Do not do this

```text
DO NOT use MoverFilterCD as the routing mechanism.
DO NOT remove shared buffer outputs without changing split count.
DO NOT use Harmony unless separately proven allowed and stable.
DO NOT build production UI as overlay Canvas + duplicate cursor unless accepted as a hack.
DO NOT mix UI cursor code into backend routing.
DO NOT assume one orientation proves all orientations.
DO NOT delete research/probe code without preserving why it existed.
```

---

## 27. Source Notes Consulted for This Handoff

Local/project sources:

```text
SmartSplitterResearch.md
Rules-to-follow.txt
SmartSplitterAssetRegistry.cs
SmartSplitterClientInteractionController.cs
SmartSplitterVisualSwapController.cs
SmartSplitterPanelController.cs
UIManager.cs
UIMouse.cs
RobotArm.cs
InteractableObject.cs
InteractablePostConverter.cs
EntityMonoBehaviour.cs
PlayerController.cs
Player.log
```

External/official sources:

```text
Core Keeper Modding Documentation:
https://modding.corekeepergame.com/documentation

Core Keeper Mod SDK GitHub:
https://github.com/Pugstorm/CoreKeeperModSDK
```

Important exact environment note:

```text
Unity 6000.0.59f2 is the required SDK version per official documentation and observed runtime logs.
```

---

## 28. Final Handoff Opinion

The Smart Splitter project is not a failure. The backend research uncovered enough Core Keeper automation internals to build a real mod. The problem is that the project accumulated experimental UI/interaction hacks while trying to make a custom panel feel vanilla.

A professional continuation should treat the project as:

```text
Backend: valuable prototype to refactor.
Visual swap: useful but needs separation.
Interaction: usable custom fallback, but should be cleaned.
UI: experimental; probably needs redesign around vanilla UI conventions.
```

The safest next product path is:

```text
1. Lock down ECS routing and forward passthrough.
2. Implement persistent splitter filters.
3. Investigate/reuse vanilla FilteringUI or build native Core Keeper-style UI.
4. Avoid the overlay cursor hack unless it is only a temporary debug bridge.
```

If the new agent starts from scratch, it should not discard the ECS discoveries. It should discard the assumption that ordinary Unity UI will integrate cleanly with Core Keeper's UI/cursor stack.

