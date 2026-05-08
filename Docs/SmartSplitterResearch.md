# Smart Splitter Research

## Verified environment

* Core Keeper Mod SDK works in Unity 6000.0.59f2.
* SmartSplitterMod builds and installs through the SDK.
* Runtime mod loading confirmed in Player.log.
* IMod lifecycle confirmed:

  * EarlyInit
  * ModObjectLoaded
  * Init

---

## Relevant assemblies

Main automation assemblies:

* Pug.Automation.dll
* Pug.Automation.Components.dll
* Pug.Automation.Authoring.dll
* Pug.Automation.Conversion.dll
* Pug.ECS.Components.dll
* Pug.Other.dll
* Pug.Objects.dll

---

## Vanilla filter model

### Relevant component

* `MoverFilterCD`

### Fields

* `FilterType filterType`
* `ObjectID filterObject`
* `int filterVariation`
* `ObjectCategoryTag filterCategory`

### FilterType values

* None
* Whitelist
* Blacklist
* CategoryWhitelist
* CategoryBlacklist

### Important method

* `InventoryUtility.ItemMatchesObjectFilter(...)`

### Observed object-filter behavior

* `FilterType.None` allows all.
* Whitelist allows matching object/variation.
* Blacklist blocks matching object/variation.
* Objects tagged `CanBeUpgraded` are excluded from object-filter matching.

---

## Vanilla splitter behavior

### Relevant class

* `ConveyorBeltSplitter`

### Relevant methods

* `SplitItemAndDropFromMover(...)`
* `SplitIntoItem(...)`

### Observed behavior

* `SplitItemAndDropFromMover` takes a stack from the mover inventory.
* It checks the item is valid and stackable.
* It divides the stack by the number of splits.
* It loops through shared output movers.
* It calls `SplitIntoItem(...)` for each output mover.
* Vanilla splitter does not appear to filter by item type.

---

## Mover data

### Relevant component

* `MoverCD`

### Important fields

* `int2 start`
* `int2 stop`
* `int moveTime`
* `int cooldownTime`
* `Entity inventoryEntity`
* `Entity moverOrchestratorEntity`
* `int splitsIntoOnMove`
* `bool cycleEnabledMoverAfterActivation`
* `bool enableAllMoversAfterActivation`
* `bool allowPickupFromInventories`
* `int indexInOrchestrator`

---

## Shared mover buffer

### Relevant buffer

* `MoversWithSharedStateBuffer`

### Fields

* `Entity moverEntity`
* `int2 cachedDirection`
* `int2 cachedStart`

This likely lets us classify splitter outputs as left/right based on direction and position.

---

## Initial Smart Splitter concept

### Add custom persisted state

* left filter
* right filter

Each side should mirror the vanilla filter shape:

* `FilterType`
* `ObjectID`
* `variation`
* `ObjectCategoryTag`

### Runtime behavior

* Detect item being split.
* Determine output side from shared mover data.
* Check item against left filter and right filter.
* Route only to matching output side.
* Preserve vanilla fallback behavior until exact UX rules are decided.

---

## ECS runtime discoveries

### Splitter orchestrator discovery

Confirmed that conveyor splitters are represented by entities containing:

* `MoversWithSharedStateBuffer`

### Observed runtime behavior

#### Normal conveyors

* `buffer length = 1`
* `splitsIntoOnMove = 0`

#### Splitters

* `buffer length = 2`
* `splitsIntoOnMove = 2`

This provides a reliable runtime discriminator for identifying splitter entities.

### Example observed runtime state

```text
buffer length = 2

Entry 0:
direction = int2(1, 0)
indexInOrchestrator = 0

Entry 1:
direction = int2(-1, 0)
indexInOrchestrator = 1
```

### Meaning

* Splitter outputs are represented by two mover entities.
* Output side can likely be determined from:

  * `cachedDirection`
  * `mover.stop`
  * `indexInOrchestrator`

---

## Splitter mover observations

### Observed splitter mover properties

```text
splitsIntoOnMove = 2
inventoryEntity = Entity.Null
```

### Important finding

* The splitter movers themselves do NOT appear to temporarily hold moving items.
* Moving item inventories instead appear on regular upstream/downstream conveyor movers.

### Observed moving-item example

```text
inventory = Entity(27521:5)
```

on a normal conveyor mover:

```text
buffer length = 1
splitsIntoOnMove = 0
```

### Implication

* Splitter routing is orchestrated through mover relationships.
* Actual item transfer/storage likely occurs outside the splitter output movers.

---

## Splitter entity structure

### Observed splitter shared-state entities

```text
Entity(26958:3)
Entity(26977:3)
```

### Observed component facts

```text
HasComponent<MoverCD> = false
HasBuffer<MoversWithSharedStateBuffer> = true
HasComponent<MoverFilterCD> = false
HasComponent<ObjectFilteringCD> = false
```

### Implications

* Splitter root entity is primarily an orchestrator/shared-state entity.
* Filter state is NOT currently attached to the orchestrator entity.
* Actual routing/filter behavior likely exists:

  * on mover entities
  * or entirely inside `ConveyorBeltSplitter` logic

---

## Confirmed splitter mover topology

### Observed topology

```text
Splitter entity
└── MoversWithSharedStateBuffer
    ├── moverEntity A (index 0)
    └── moverEntity B (index 1)
```

### Observed directions

```text
index 0 = int2(1, 0)
index 1 = int2(-1, 0)
```

### Meaning

* Splitter outputs are deterministic.
* Side identity can likely be persisted using:

  * mover index
  * or cachedDirection

This is a major discovery because it gives a stable left/right output identity.

---

---

---

## Failed runtime-control experiments

### MoverFilterCD output filtering test

Tested setting splitter output movers directly:

- output index 0: `FilterType.Whitelist`, `ObjectID.WallDirtBlock`
- output index 1: `FilterType.Blacklist`, `ObjectID.WallDirtBlock`

Result:

- Dirt still split evenly to both outputs.

Conclusion:

- `MoverFilterCD` exists on splitter output movers.
- However, vanilla splitter output distribution does not consult `MoverFilterCD`.
- `MoverFilterCD` appears to be used by automated pickup logic, not splitter output routing.

Supporting dnSpy finding:

- `InventoryUtility.ItemMatchesObjectFilter(...)` is used by:
  - `InventoryUtility.IsValidItemForAutomatedPickup(...)`
  - `InventoryUtility.AutomatedPickup(...)`
  - `PugAutomationSystem.OnUpdate(...)`

It does not appear to be called by:

- `ConveyorBeltSplitter.SplitItemAndDropFromMover(...)`
- `ConveyorBeltSplitter.SplitIntoItem(...)`

### splitsIntoOnMove runtime mutation test

Tested changing one splitter output mover:

`text`
`indexInOrchestrator = 1`
`splitsIntoOnMove = 1`

while leaving the other side unchanged.

### Result:

Stack splitting remained even.
Example: stacks of 4 still split 2 / 2.
Larger stacks still split evenly.

### Conclusion:

Changing `MoverCD.splitsIntoOnMove` live after setup does not affect vanilla splitter distribution.
The splitter likely uses the splits argument passed into `SplitItemAndDropFromMover(...)`, not the modified output mover value in a way we can exploit safely.
Runtime buffer manipulation is the next possible experiment, but it is riskier than component mutation.


## Current likely implementation strategy

### Most likely implementation path

1. Identify splitter orchestrator entity through:

   * `MoversWithSharedStateBuffer`
   * `buffer length == 2`
   * all movers having `splitsIntoOnMove == 2`

2. Persist custom Smart Splitter state:

   * left filter
   * right filter

3. Patch vanilla splitter execution:

   * likely `ConveyorBeltSplitter.SplitItemAndDropFromMover(...)`

4. During split:

   * inspect outgoing item
   * evaluate against left/right filters
   * choose output mover based on filter match

5. Preserve fallback behavior:

   * if no side matches
   * or both sides match
   * use deterministic/default vanilla routing rules

---

## Important unresolved questions

Still unresolved:

* Which exact method decides WHICH output mover receives an item.
* Whether `indexInOrchestrator` is guaranteed stable after world reload.
* Whether left/right orientation depends on world rotation.
* Best persistence approach:

  * ECS component
  * authoring data
  * external save
* UI implementation strategy for dual filters.
* Multiplayer synchronization requirements for custom filter state.
