# Smart Splitter Research

## Verified environment

- Core Keeper Mod SDK works in Unity 6000.0.59f2.
- SmartSplitterMod builds and installs through the SDK.
- Runtime mod loading confirmed in Player.log.
- IMod lifecycle confirmed:
  - EarlyInit
  - ModObjectLoaded
  - Init

## Relevant assemblies

Main automation assemblies:

- Pug.Automation.dll
- Pug.Automation.Components.dll
- Pug.Automation.Authoring.dll
- Pug.Automation.Conversion.dll
- Pug.ECS.Components.dll
- Pug.Other.dll
- Pug.Objects.dll

## Vanilla filter model

Relevant component:

- MoverFilterCD

Fields:

- FilterType filterType
- ObjectID filterObject
- int filterVariation
- ObjectCategoryTag filterCategory

FilterType values:

- None
- Whitelist
- Blacklist
- CategoryWhitelist
- CategoryBlacklist

Important method:

- InventoryUtility.ItemMatchesObjectFilter(...)

Observed object-filter behavior:

- FilterType.None allows all.
- Whitelist allows matching object/variation.
- Blacklist blocks matching object/variation.
- Objects tagged CanBeUpgraded are excluded from object-filter matching.

## Vanilla splitter behavior

Relevant class:

- ConveyorBeltSplitter

Relevant methods:

- SplitItemAndDropFromMover(...)
- SplitIntoItem(...)

Observed behavior:

- SplitItemAndDropFromMover takes a stack from the mover inventory.
- It checks the item is valid and stackable.
- It divides the stack by the number of splits.
- It loops through shared output movers.
- It calls SplitIntoItem(...) for each output mover.
- Vanilla splitter does not appear to filter by item type.

## Mover data

Relevant component:

- MoverCD

Important fields:

- int2 start
- int2 stop
- int moveTime
- int cooldownTime
- Entity inventoryEntity
- Entity moverOrchestratorEntity
- int splitsIntoOnMove
- bool cycleEnabledMoverAfterActivation
- bool enableAllMoversAfterActivation
- bool allowPickupFromInventories
- int indexInOrchestrator

## Shared mover buffer

Relevant buffer:

- MoversWithSharedStateBuffer

Fields:

- Entity moverEntity
- int2 cachedDirection
- int2 cachedStart

This likely lets us classify splitter outputs as left/right based on direction and position.

## Initial Smart Splitter concept

Add custom persisted state:

- left filter
- right filter

Each side should mirror the vanilla filter shape:

- FilterType
- ObjectID
- variation
- ObjectCategoryTag

Runtime behavior:

- Detect item being split.
- Determine output side from shared mover data.
- Check item against left filter and right filter.
- Route only to matching output side.
- Preserve vanilla fallback behavior until we decide exact UX rules.

## Open questions

- How to persist custom filter state on the splitter entity.
- How to expose two eyedropper/filter controls in UI.
- Whether to patch ConveyorBeltSplitter.SplitItemAndDropFromMover directly.
- Whether electricity state already exists on conveyor splitter or must be added.
- How local filter state syncs in multiplayer.
