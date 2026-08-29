# The gap list, resolved

Every authoring component the framework does not write, taken in order of how many vanilla prefabs
carry it, with a decision and a reason. Counts are from the vault's per-prefab component tables.

This exists because "not covered" was being used as a to-do list without anyone having asked, of each
entry, whether it is ours to write at all. Several are not — and a component that is deliberately not
ours should say so once, here, rather than be re-investigated every time the list is re-sorted.

## How to re-derive this list

```bash
grep -rhoP '(?:EnsureComponent|RemoveComponentIfPresent|AddComponent|GetComponent|ApplyComponent|Toggle)<\K[\w.]*?\K\w+Authoring' Editor/*.cs Editor/**/*.cs Scripts/**/*.cs | sed 's/.*\.//' | sort -u
```

All six helper names, and the `[\w.]*` is not optional — `EnsureComponent<Pug.Automation.AutomatedStorageAuthoring>`
is namespaced and a naive pattern misses it. Counting without those undercounted the framework by
five components for a while, which put `DurabilityAuthoring` (343 prefabs) on the to-do list while it
was being written correctly all along.

## Ours to write

| Prefabs | Component | What it actually is |
|---:|---|---|
| 487 | `AlwaysDropVariationZero` | Bare marker read by `DropSelfJob` and `DropTilesJob`: when it drops itself, drop variation 0 rather than the variation it was. Only matters for an object with more than one variation. |
| 262 | `OverrideNetworkSyncDistance` | One float. How far away it still syncs. Read by `GraphicalObjectConversion`. |
| 86 | `AffectedByAutomation` | Bare marker. Conveyors and robot arms may act on it. Pairs with the `AutomatedStorage` we already write on containers. |
| 71 | `CustomDisable` | `alwaysEnabled`, default true. |
| 54 | `DestroyIfNotOnTile` | Where a thing may stand: a tile object it needs, plus water/lava/pit/any-walkable ticks. |
| 53 | `RandomWalkGravity` | Gravity wells that bend a wandering creature's path. **Config half only** — `isAffected`, `position` and `timer` are runtime state that happens to be public. |
| 50 | `Critter` | Flying, continuous spawning, spawn type, biomes, tilesets, persistence. **We have a `DimensionCritterAsset` with a dashboard and no generator** — see below. |
| 49 | `SpawnCompanions` | It arrives with friends. Needs the companion prefabs to exist first, so it runs after creature generation. |
| 40 | `MapMarker` | Marker type, unique id, hide-when-discovered. Pairs with landmarks and dungeons. |
| 40 | `GroundDecoration` | Bare marker read by the digging and seeding placement handlers: grass-like things that get cleared when you dig or plant. |
| 40 | `CritterCatcherCatchable` | Bare marker. The critter catcher works on it. |
| 38 | `Explosive` | Damage, mining damage, what explosion it makes, pushback level. Bombs. |
| 36 | `MusicArea` | A music roster near an object, with combat and state variants. Pairs with the biome music already shipped. |
| 33 | `CanBeScanned` | The scanner finds it. |

## Blocked on an in-game check

| Prefabs | Component | Why it is not a free add |
|---:|---|---|
| 394 | `LocalInteractable` | Its converter reads the **graphical** prefab's `InteractableObject.onUseActions` and `onTriggerExitActions`, and calls `Debug.LogError("No local interaction events registered on entity")` when neither has any. So it only works on an object whose *look* already carries wired interaction events, which is not something our generator produces. Needs a live check before we write it. |
| 59 | `ChangeVariationTrigger` | Same family, same converter shape, same dependency on wired interaction events. Resolves with the one above. |

## Not ours

| Prefabs | Component | Why not |
|---:|---|---|
| 198 | `CustomScenePrefab` | A bare marker `PugDatabase` uses to **exclude** a prefab from `objectPrefabEntityLookup` — it says "this prefab belongs to a scene, do not treat it as a spawnable object". Our scenes carry their contents as `CustomSceneBlob`, so nothing we generate is that kind of prefab. Writing it would make our objects unspawnable. |
| 69 | `TriggerEffect` | PS5 DualSense adaptive-trigger haptics, via `RewiredPS5TriggerEffect*` proxies. Platform controller feedback with no modding surface, and the proxy types are not in the SDK. |
| 57 | `AdaptiveEntityBuffer` | An object picking a variation from its neighbouring tiles (`AdaptiveCondition`: variation, matches needed, left and right `TileCondition`). Genuinely interesting and genuinely adjacent to the tileset work — **deferred, not declined**. It wants designing alongside the tileset adjacency model rather than bolted on as a field list. |

## The second unreachable generator

`DimensionCritterAsset` has an Add button, a dashboard editor, a template collection and an id that
the drop-source checker already knows about — and **nothing generates a critter prefab**.
`CritterAuthoring` is written nowhere. A creator can author critters, see them counted on the
dashboard, generate, and have them not exist.

This is the same failure as the plants/workbenches/world-objects/vehicles one, found the same way:
by asking the three reachability questions rather than trusting a component count. It is worth
writing down that the count found it **twice**, in a codebase where everything else was green.
