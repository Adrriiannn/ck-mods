# The identity gates: where a custom object is quietly skipped

Some of Core Keeper's systems ask **which object is this** before they act. A custom object carrying
the right block reaches the system, fails the question, and is skipped — no error, no warning,
nothing in the log. The block is on the prefab and does nothing.

This is a measured census of every gate that touches something the framework offers, so the ceiling
is a known one rather than a surprise found in a live game.

## Why they cannot simply be patched

Every one of these systems is Burst-compiled at struct level. A Harmony patch on a Burst-compiled
job does not take — the compiled code is what runs, and it never calls the managed method. The
options are therefore:

1. **Leave it and say so** — the honest default. The tooltip and the generation warning tell the
   author exactly what a custom object gets and what it misses.
2. **A narrow companion system** — replicate the behaviour for registered objects only, early-outing
   on an empty registry so it costs nothing when unused. This is the pattern already shipped as
   `DimensionHazardConditionSystem`.
3. **Use the game's own companion object** — several gates are not on the boss at all but on a thing
   it spawns. Point the kit at Core Keeper's own stone, tentacle or segment and the gate passes.

De-Bursting is not on the list. `EquipmentUpdateSystem` was de-Bursted because it runs per player;
these run over every matching entity every tick, and taxing everyone forever to serve one custom
boss is the wrong trade.

## The census

Reading every system that compares against a specific `ObjectID`, and keeping only those a custom
object can actually reach.

### No gate at all — a custom one gets the whole behaviour

| Kit | System |
|---|---|
| The Wall | `WallBossSystem` |
| The Scarab | `ScarabBossSystem` |
| The Larva | `LarvaBossSystem` |
| The Shaman | `ShamanBossSystem` |
| The Cicada | `CicadaGiantBossSystem` |

Nothing to do. These were built general and stayed general.

### Gated on a companion object, not on the boss

The system asks about the thing the boss *spawns*, never about the boss. A custom boss gets the
whole behaviour as long as it spawns Core Keeper's own object for that part.

| Kit | What it asks about | What to point at |
|---|---|---|
| The Bird | `BirdBossStone` | the stones it drops |
| The Octopus | `OctopusTentacle` | the tentacles it sends up |
| The Snake | `SnakeBossSegment` | its body segments |
| The Core | `CrystalMeteorBoulder`, `MapMarker` | the boulder it throws, its map marker |

This is a real, reachable answer, and it is what the tooltips now say.

### Variant flavour only — the base works, the extras do not

`HydraBossSystem` runs for anything carrying the Hydra block. What it gates on identity is the
per-variant flourish: the Sea Hydra's ice shards, the Nature Hydra's stalactite mortar, the Void
Hydra's fourth head, the Desert Hydra's pattern timing.

A custom hydra gets heads, burrowing, surfacing, the head-loss cycle and the base attack pattern. It
does not get the four variant flourishes. Nothing is broken; there is simply less of it.

### Whole behaviour gated — the block does nothing

| Kit | System | Gate |
|---|---|---|
| The Slime King's shot cycling | `SlimeBossSystem` | `ObjectID.LavaSlimeBoss` only |

Everything the system does sits inside one `if`. A custom object carrying the block is skipped
entirely. The framework says so at generation time rather than letting it ship silent.

### Progression is recorded for named bosses only

`UpdateWorldInfoSystem` writes "this boss has been defeated" into the world's own record for eight
objects: the Bird, the Core, the Giant Cicada, the Octopus, the Robot, the Scarab, the Wall, and the
Slime Merchant. **A custom boss's defeat is not recorded there.** Anything a mod wants to unlock off
a custom boss has to track that itself rather than reading the world record.

### Custom walls are solid to enemy pathing, never diggable

Not an `ObjectID` gate — a tileset one, and it belongs here because it fails the same silent way.
`PathFindSystem.ComputeStepCost` asks how expensive it is to walk into a wall, and answers by
looking that wall up in a durability table:

```csharp
int tileset = topDamageableTile.tileset;              // PathFindSystem.cs:636-641
if (tileset < 0 || tileset >= 75) { return 1000000; }
PathFindSystem.TileDurabilityInfo info = this._wallDurabilityInfo[tileset];
```

The table is a fixed 75 entries, so the bound is protecting a real array rather than being fussy.
Every custom id is above it, and the cost that comes back is effectively infinite. In practice an
enemy that would happily mine through a vanilla wall treats a custom wall as solid and paths around
it instead.

That is usually what a builder wants, and it is written down here so it is a known ceiling rather
than a surprise. It is emphatically NOT worth de-Bursting for: `PathFindSystem` runs pathing for
every enemy in the world, where `EquipmentUpdateSystem` runs over at most eight players.

Two sibling guards share the same literal and are already handled: `EntityUtility.AddTile`,
the placement path, covered by the single de-Burst the framework allows; and
`DeserializeComponentsSystem`, covered by bracketing in `DimensionCustomTileRescue`.

### Dungeon rooms drop tiles no object claims

`DungeonGenerateRoomsSystem.SpawnCustomScene` resolves every room tile through
`PugDatabase.GetObjectData(tileset, tileType)` — a linear scan for the first object authored with
that exact pair. When nothing matches, the tile is **silently dropped**: the room looks perfect in
the open world (direct placement writes tiles straight into the map) and arrives holed inside a
dungeon. The only thing that registers a `(tileset, tileType)` pair is an object that IS that tile
— the generated block items — so a tileset used purely as scene decoration, with both block
toggles off, loses every tile of it in dungeon rooms.

Handled: `DimensionDungeonAssembler.VerifyDungeonTilesResolve` preflights every room scene at
assembly and warns per `(tileset, tileType)` pair, naming the switched-off block items as the
likely cause. Warn, never refuse — a dropped tile degrades one room; refusing drops the dungeon.

### Dungeon fill stamps only tiles or prefabs

The other direction of the same gate: `DungeonApplySpawnedObjectsSystem` takes each fill/outline
`ObjectID` to its `EntityObjectInfo` and either writes a tile (`tileType != none`) or spawns a
prefab — an object that is neither is **silently dropped**. The same preflight checks every
authored outline block and warns when a layer would stamp nothing.

### Fishing indexes fixed arrays by tileset and biome, unchecked, in Burst

The fishing blob allocates `fishingInfoByWaterTileset` at a hardcoded 75 and
`fishingInfoByBiome` at 12, and `FishingTableCD` indexes both by raw `(int)tileset` /
`(int)biome` with no bounds check, inside Bursted call sites. A custom tileset id (≥1000)
used as WATER, or a custom biome over water, reads out-of-bounds memory the moment a bobber
lands. Every gameplay call site is Burst-inlined, so no patch reaches it.

Handled: content validation warns on any custom tileset whose type is water, with the honest
rule — the liquid layer of a custom area uses one of the game's OWN waters (which also picks
the fishing tier: Sea water fishes at Sea tier), and custom blocks stay on ground and walls.

**The same two ceilings bound what a mod may WRITE, and there the failure is louder.**
`FishingTableConverter.CreateBlob` allocates the same 12 and 75 and then writes
`array[(int)biome]` / `array[(int)tileset]` for every entry in the managed table — including
entries a mod added from its Conf files. So a mod file keyed to a custom biome (≥1000) or a
custom water ground does not merely go unread: it writes past the end of the blob array as the
world loads, which is a crash rather than a silent skip.

Handled at the authoring edge instead of the read edge. The World Rules stage's fishing block
refuses a biome or a ground it cannot prove is one of Core Keeper's own, and the generation
warning names the fix — key the rule to one of the game's waters, or to one of its biomes.
Nothing is written, so no such file can ever reach a player's game.

**A fish's fight has NO such ceiling, and as of this pass no limit either.** The struggle array
is allocated at the table's own length (`FishingTableConverter.CreateBlob:46`) and read by a
linear scan comparing object numbers (`FishingTableCD.cs:45-66`), so a mod object's number —
far above Core Keeper's own — is simply another value the scan can match, and a fish with no
fight falls through to the table's default rather than breaking.

What used to stop a modder's fish was the *file*, not the table: `Conf/Fishing` names the fish
by `ObjectID` **number**, and a mod's objects have no number until the mod is loaded, which is
after every Conf file has been read. That is now routed around rather than accepted.
`Scripts/WorldRules/DimensionFishFightRegistry.cs` appends the fight to
`Manager.mod.FishingTable.fishStruggleInfos` in a `FishingTableConverter.Convert` prefix — the
one moment after mod objects have registered their names (conversion is a FIFO queue and
`ECSManager.Init:86-93` enqueues mod objects before the object carrying the fishing table) and
before the table is baked. Generation picks the road per fish without being asked: Core Keeper's
own fish keep the settings file, this mod's fish become a registration in the bootstrap.

**Related, and not an identity gate but the same silent shape: the junk table is how the game
asks whether a fishing rule exists at all.** `FishingTableCD.GetFishingStats` reads a rule, tests
`lootTableID == LootTableID.Empty` — the JUNK table, not the fish table — and on finding it blank
moves on to the next place to look, ending at the biome's rule
(`ck-db\Pug.ECS.Components\FishingTableCD.cs:8-27`, and `LootTableID.Empty` is 0, which is also
what an unwritten blob slot holds). A rule naming only fish is therefore dropped on the floor:
the water goes on catching what it always caught and nothing is logged. Handled at the authoring
edge — `DimensionWorldRulesGenerator.ResolveFishingTables` refuses any biome or water row without
a junk table and names the fix, and the tooltips say so.

### AreaLevel is per-prefab, and its converters are switches

There is no position→AreaLevel map anywhere: danger is authored per prefab and baked at
conversion. `LevelScaling.GetLevelFromAreaLevelAndRarity` and `WaterTilesetToAreaLevel` are
switches over vanilla values — a custom AreaLevel int falls to level ≈ 0, an unknown water
tileset falls to Slime tier. The honest "danger tier" for custom content is therefore the
framework's existing surface: bake the tier into the creatures and objects you place (the
health/damage curves take any int), never a positional dial.

### Custom biome ints and vanilla's two ceilings

The framework mints custom Biome ints in [1000, int.MaxValue) — save-stable by the identity
ruling, and correct for every equality-compared channel (titles, dungeon tables, discovery,
loot conditions, atmosphere). Two vanilla channels have hard ceilings custom ints can never
enter: ambient-spawner biome filters use a 64-bit mask (`1UL << biome` — a custom int aliases
an arbitrary bit), and the world's biome-sample grid stores one BYTE per cell (< 256). Custom
biomes therefore work through the framework's own channels and are invisible to those two —
a known limit, not a bug to fix, because renumbering would orphan every existing world's
records.

### A custom condition's NUMBER is also its name

Core Keeper reads a condition's one line of text from `"Conditions/" + conditionID.ToString()`
(`ck-db` `Pug.Other/ConditionUI.cs:191, 217, 223`, and again in `SlotUIBase.cs:1510`,
`SkillTalentUIElement.cs:277`, `SoulsUIElement.cs:104`, `UIMouse.cs:1106`). A number the framework
minted has no name in the game's enum, so `ToString` returns the digits and the term really is
`Conditions/358`. The row the generator now writes is keyed by that number.

That makes the row share the number's fate: numbers are handed out in name order across whatever
`DimensionConditionRegistry` holds, so the same mod alone always produces the same numbers, and the
row always matches. Two Dimensions API mods claiming into one registry would shift each other's
numbers — and would shift the numbers already baked into their prefabs in exactly the same way, so
the line and the effect stay together or drift together. There is no ceiling to check here: the
numbers start above `ConditionID.MAX_VALUES` and the framework grows the table to reach them.

## What this changes in the authoring surface

- Every kit's tooltip now says which of the four cases it is.
- The generation report warns for the whole-behaviour gate, naming what will not happen.
- The companion-object cases are documented as an instruction rather than a limitation, because
  that is what they are.

## Re-running the census

```bash
grep -rlP '(objectID|uniqueMarkerId)\s*[!=]=\s*ObjectID\.' ck-db --include=*System.cs
```

58 systems compare against a specific `ObjectID`. Most are `== ObjectID.None` null checks, or ask
about a vanilla item in a way no custom object reaches. The ones above are the ones that touch what
the framework offers; re-check when a new kit is added.
