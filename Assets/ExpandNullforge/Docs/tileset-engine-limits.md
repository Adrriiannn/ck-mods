# What a custom tileset inherits from the engine, and why we leave it alone

Engine behaviour a custom tileset gets whether it wants it or not, and which this framework has
decided not to change. This is reference material, not policy the code enforces — it exists so the
next person to meet one of these finds the decision instead of rediscovering the symptom.

Harvested from `Scripts/Tilesets/DimensionTilesetEngineLimits.cs`, which held it as `<remarks>` on a
class nothing read.

## Enemy pathfinding treats a custom wall as unmineable

`PathFindSystem.AllowButAvoidWallsPolicy.ComputeStepCost` reads wall durability out of a
`NativeArray<TileDurabilityInfo>` sized 75 and filled by a `for (i = 0; i < 75)` loop, behind this
guard:

```csharp
int tileset = topDamageableTile.tileset;
if (tileset < 0 || tileset >= 75) return 1000000;
var info = _wallDurabilityInfo[tileset];
```

Every custom tileset id is above 75 (they are FNV hashes into `[1000, int.MaxValue)`), so every
custom wall takes the early return.

### Why it is not patched

The guard is load-bearing, not merely restrictive. It is what keeps `_wallDurabilityInfo[ourId]`
from reading past the end of a 75-element `NativeArray` inside a Burst job, where a release build
has safety checks compiled out — that is memory corruption, not an exception. And the bound is the
literal `75`, not `_wallDurabilityInfo.Length`, so enlarging the array would not help.

`PathFindSystem` is a `[BurstCompile] ISystem` and the policy is a struct inside its job, so there is
no managed method to hook. Reaching it would mean forcing the whole pathfinder to run un-Bursted,
for every enemy, every frame.

### Why the result is acceptable

`1000000` is not a penalty invented for unknown tilesets. It is a named constant,
`UNBREAKABLE_WALL_COST`, and it is the cost vanilla returns a few lines further down for a wall
whose damage reduction meets or exceeds the enemy's mining damage.

Stronger still: the durability table is seeded with `int.MaxValue` for every tileset with no wall
object mapped, so `miningDamage - int.MaxValue` is never positive and an **unmapped vanilla tileset
takes the identical branch**. Custom ids are not a special case; they land exactly where an unmapped
vanilla tileset lands.

The same policy's `IsBlocked` returns `false` for `TileType.wall`, so a wall is never impassable,
only expensive. The base term is `WALL_BASE_COST` = 4 — it is not free to stop and swing — which
means a detour of up to four tiles is already preferred over digging even a one-hit wall. At
unbreakable cost the detour always wins.

The single thing lost is that a strong enemy cannot chew through a deliberately soft custom wall,
which for a framework whose users build decorative structures is the safer of the two failure modes.

### What would change this

Durability is sourced from `TileWithTilesetToObjectDataMapCD`, a `NativeHashMap` keyed by
`(TileType, Tileset)` that can already hold custom ids — only the `0..74` fill loop excludes them.
If Core Keeper ever sizes that table from the map rather than the constant, custom walls gain real
durability with no work on our side.

## The two numbers

| Name | Value | What it is |
|---|---:|---|
| `UnmineableWallStepCost` | 1000000 | The step cost Core Keeper's pathfinder assigns a wall it cannot mine, and therefore the cost every custom-tileset wall is assigned. |
| `PathfindingDurabilityTableSize` | 75 | The size of the engine's wall-durability table, and the exclusive bound its guard tests against. Equal to `Tileset.MAX_VALUE`. |

## The coincidence the framework rests on

Worth knowing if this ever changes upstream: **the `75` in `ComputeStepCost`'s guard is a separate
literal from the two that size and fill the table in `OnStartRunning`.** They agree today and nothing
links them. A Core Keeper change to one and not the others would desync the guard from the array it
protects — and it is that guard being correct which makes a custom tileset id safe to hand the
pathfinder at all.

There is no check on our side that can catch this, and no error it would produce. It would show up
as a crash in a release build.
