# Vanilla coverage — what the framework can reach, and what it cannot

The goal is not "add features". It is: **everything Core Keeper can already do should be reachable
from the framework, and customizable there**. This file makes that measurable instead of a feeling.

## How to establish what vanilla does — read this before designing anything

There are two corpora and they answer different questions. Using the wrong one is how the container
API shipped with invented chest sizes and invented toughness tiers.

| Question | Source |
|---|---|
| Does this field/component/method exist? What calls it? | `E:\ck mods\ck-db` (decompiled code), indexed by the vault's `Members/` and `Scripts/` pages |
| **What value did Core Keeper actually author?** | `E:\Tools\CoreKeeperRippedAssets\ExportedProject\Assets\GameObject\*.prefab`, indexed by the vault's `Assets/` and `Prefabs/` pages |

Code cannot answer the second question. `HealthAuthoring.maxHealth` is baked at edit time by
`OnValidate`, so the number exists **only** in the prefab — the class shows a field and nothing more.
The prefabs are plain-text YAML and can be censused wholesale with PowerShell.

**So: census the prefabs before designing the authoring surface for a domain.** Not a sample — all of
them. The container work found that every chest in the game is one of exactly two grids, that tier
changes nothing about capacity, that no chest is destructible in the component sense, and that the
entire locked-chest mechanism was sitting unused in five unwired fields of a component the generator
was already writing. None of that is visible from the code alone, and none of it survives being
guessed at from memory of playing. Cross-check the result against the wiki: agreement between the two
is strong evidence the parse is right, and the wiki supplies the words players actually use.

## The measurement

Core Keeper describes what an object *is* through **authoring components** — MonoBehaviours a prefab
carries, each exposing a set of fields. That list is the game's own vocabulary for "what can a thing
be", so it is the honest denominator.

| | Count |
|---|---|
| Authoring components in the game | **354** |
| Public fields across them (individual knobs) | **1,374** |
| Components the framework currently writes | **147** |
| **Coverage** | **34%** |

> Counted mechanically, not by hand: distinct `*Authoring` types named by any
> `EnsureComponent`/`RemoveComponentIfPresent`/`AddComponent` call in `Editor/` and `Scripts/`.
> Re-run that count rather than editing this number by memory.
>
> 43 → 49 came from the container rebuild and the creature combat work. The creature half added
> twelve components at once, of which the load-bearing ones are `MeleeAttackState`,
> `RangeAttackState`, `NearbyEntitiesTracker`, `Faction` and `RandomWalkState` — together the
> difference between a creature that chases you and stands there, and one that behaves like an enemy.
>
> 49 → 53 is plants: `PlantAuthoring`, `SeedAuthoring`, `RootPlantAuthoring`,
> `CanBeRemovedByWaterAuthoring`.
>
>
>
>
>
>
> 85 -> 88 is item effects: `GivesConditionsWhenEquipped` (529 prefabs — the whole of equipment
> buffs), `GivesConditionsWhenConsumed`, `CooldownAuthoring`. Composable as
> `DimensionItemEffectsTemplate`. **`dontCalculateValuesFromLevel` is the THIRD instance of the level
> trap**, after health and attack damage — leave it false with an area level present and an authored
> +5 mining silently becomes whatever the curve says.
>
> 76 -> 85 is the world-object long tail: doors, beds, trophies, lights, decoration. `Door`, `Bed`,
> `Trophy`, `GlowLight`, `TableItemLightSource`, `ActAsLightSourceWhenHeldInHand`, `CantBeAttacked`,
> `DestroyTimer`, `SurfacePriority`. **Lighting turned out to be three separate components** and a
> torch carries two of them and not the third — see the asset docs; collapsing them would produce
> lamps that glow and leave the room black.
>
> 75 -> 76 is `ElectricityAuthoring` — the wiring system, as a composable `DimensionWiringTemplate`
> any generator can attach. Censused first, and the roles fall straight out of the data: `isWire` is
> on exactly ONE prefab in the game, `isLever` on the 15 things a player or creature can trip, and
> `circuitType` is None on 113 of 125. The roles are NOT exclusive in vanilla — a lever is a switch
> AND a source AND blocks current — so the role picks the shape and the fields stay reachable.
>
> 74 -> 75 is `CraftingAuthoring`: a custom crafting station that actually crafts. The workbench asset
> could already group recipes and point them at an object, but nothing wrote the component, so a
> custom bench was a decoration and the recipes had to land on a vanilla one. Measured first: 50 of
> the game's 85 stations are plain `Simple` benches, which is the case made easy.
>
> 65 -> 74 is the universal spine: the components vanilla puts on essentially every object of a kind
> and we put on none. `AnimationAuthoring`, `PaintableObject`, `Rotation`, `Description`,
> `AutomatedStorage` (conveyors can now feed a custom chest), `Diggable`, `AutomatedHarvestablePlant`,
> `IgnoreVertexOffsets`, `AreaLevel`, plus `Health` on plants. Defined once in
> `Editor/DimensionObjectSpine.cs` rather than repeated per generator.
>
> **`AreaLevelAuthoring` is deliberately CONDITIONAL.** `HealthAuthoring` has an opt-out
> (`dontCalculateHealthFromLevel`); the attack components have none — their `OnValidate` overwrites
> damage unconditionally whenever a level is present. Adding it to every creature would have made
> authored attack damage impossible to keep, which is the exact failure this framework exists to
> prevent. It now follows the author's existing stat-source choice, and two tests pin that.
>
> 53 → 65 is creature abilities — the twelve optional states a creature can carry beyond walking and
> swinging: charge, jump, mortar, explode, teleport, enrage, sleep, eat, breed, evolve, heal-allies,
> vulnerable. Authored as a **list of abilities** rather than twelve tickboxes with fifty settings
> hanging off them, because a boss is a kit of things it can do and that is what an author composes.

Of the 311 we do not touch:

- **21** are engine plumbing no modder should reach — PS5 haptics proxies, source-generated types,
  player GUIDs, network sync internals, submap streaming.
- **26** are *named vanilla content* rather than mechanisms — `HydraBossAuthoring`,
  `OctopusBossAuthoring`, `TheCoreAuthoring`. A modder does not want to *be* the Hydra. (Their
  **state** components are a different matter and are counted below — `SlimeBossJumpState` is a
  reusable jump attack, not a slime boss.)
- **264 are real mechanisms we should expose.**

So the honest target is **88 → 307**, and the real number today is **33% of what matters**.

## What to do next, ranked by how much of the game actually uses it

Measured 2026-08-16 by counting how many vanilla prefabs carry each component (from the vault's
per-prefab tables) and subtracting what we already write. This is a better work order than the domain
list below, because it is weighted by what Core Keeper actually leans on.

| Prefabs | Component | Why it matters |
|---:|---|---|
| 1446 | `AnimationAuthoring` | On nearly every object in the game. Without it nothing we generate animates at all. |
| 1184 | `AreaLevelAuthoring` | Drives the health and damage curves. Its absence is why our creatures' authored damage is the only damage they can have. |
| 837 | `IgnoreVertexOffsetsAuthoring` | Opts out of CK's vertex wobble — we already expose this idea for tilesets as "rigid surface". |
| 529 | `GivesConditionsWhenEquippedAuthoring` | The whole of equipment buffs. |
| 524 | `CooldownAuthoring` | |
| 509 | `SecondaryUseAuthoring` | Right-click behaviour on items. |
| 437 | `DiggableAuthoring` | Whether a shovel works on it. Plants carry it. |
| 394 | `LocalInteractableAuthoring` | **Not a free add.** Its converter reads the GRAPHICAL prefab's `InteractableObject.onUseActions` and logs an error when none are wired, so it only works on an object whose look already has interaction events. Needs an in-game check before we write it. |
| 306 | `TileEffectAuthoring` | |
| 243 | `PaintableObjectAuthoring` | Vanilla chests are paintable; ours are not. |
| 209 | `RotationAuthoring` | |
| 143 | `AutomatedStorageAuthoring` | Lets conveyors feed a container. Vanilla chests have it. |
| 125 | `ElectricityAuthoring` | The whole wiring/automation system. |
| 85 | `CraftingAuthoring` | Workbenches — what makes a station able to craft anything. |

The pattern worth noticing: several of these are components vanilla puts on **essentially every object
of a given kind**, and we put on none. A generated chest is missing the paint, rotation, automation
and animation that every vanilla chest has. Closing that "universal spine" gap is cheaper per
component than any new domain and removes a class of "looks finished, behaves oddly" bugs.

## Where the 264 live

Counts are approximate at the edges — several components belong to more than one domain.

### Creature behaviour and combat (~55)

> **Censused 2026-08-16** across the 115 prefabs carrying `EnemyAuthoring`. Two corrections to what
> this section originally claimed, both from measuring rather than reading the class list:
>
> - **`RandomWalkState` is what enemies actually roam with, not `RoamingState`.** `RoamingState`
>   appears on 4 prefabs; `RandomWalkState` is the third most common behaviour component on enemies.
>   `RoamingPathAuthoring` (an authored patrol route) is a separate, third thing — the one worth
>   reaching for when a boss should walk a fixed path.
> - **`BeamAttackState` and `AlertEmoteState` are on ZERO prefabs**, despite each having a full
>   converter and system (`BeamAttackStateSystem`, `AlertEmoteStateSystem`). They are working
>   mechanisms vanilla never shipped a user of. Worth exposing, but flag them: there is no vanilla
>   creature to copy settings from, so a modder is authoring blind.
>
> **The measured enemy spine**, in frequency order — this is what "a creature" means in Core Keeper
> and therefore the backbone the authoring surface should cover first:
> `EnemyAuthoring` · `HealthAuthoring` · `AreaLevelAuthoring` · `FactionAuthoring` ·
> `MovementSpeedAuthoring` · `BehaviourTagsAuthoring` · `StateAuthoring` + `Idle`/`Death`/`TookDamage`
> · `SupportsConditionsAuthoring` · `NearbyEntitiesTrackerAuthoring` · `DropLootAuthoring` ·
> `RandomWalkState` · `ChaseState` · `CombatRadiusAuthoring` · `MeleeAttackState`/`RangeAttackState` ·
> `DetectCollisionAuthoring` · `DamageEffectAuthoring` · `ImmuneToPushBackAuthoring` ·
> `ScaleHealthByPlayerCountAuthoring`. Bosses add `BossAuthoring`, `MusicAreaAuthoring`,
> `SpawnCompanionsAuthoring`, `EnrageState`, `RoamingPathAuthoring`, `HasSpawnPointAuthoring`.
>
> Attack-state usage, measured: `MeleeAttack` and `RangeAttack` dominate; then `ShootMortarProjectile`,
> `JumpAttack`, `ChargeAttack`. `SnakeMovementState` and `FollowPheromoneState` are commoner than
> expected and are what make specific creatures feel distinct.

The single biggest gap, and the one your boss example was about. Vanilla has a complete state
vocabulary we expose almost none of: `RandomWalkState`, `SleepState`, `EatState`, `BreedState`,
`EvolveState`, `ExplodeState`, `TeleportState`, `SpawnState`, `VulnerableState`, `EnrageState`,
`IdleInCombatState`, `IdleWhenNearbyPlayerState`, `HatchWhenPlayerNearbyState`,
`HealOtherEntityState`, `FollowPheromoneState`, `RandomFollowState`, `PetWalkState`,
`SnakeMovementState`, `MoveToPositionFromCommandState`, `PlaceObjectState`, plus the attack set —
`MeleeAttackState`, `RangeAttackState`, `ChargeAttackState`, `JumpAttackState`, `BeamAttackState`,
`ShootMortarProjectileState`, `TouchAttack`, `AttackContinuously`.

Also here: `RoamingPathAuthoring` (the roaming path you named), `CombatRadius`,
`OverrideLeaveCombatTime`, `ForceInCombatIfPlayerNearbySpawnPoint`, `ScaleHealthByPlayerCount`,
`SpawnCompanions`, `SpawnOnDeath`, `DestroyNearbyOnDeath`, `Faction`, `Minion`/`MinionOrbit`,
`Shield`, `Mana`/`ConsumesMana`, `Souls`/`SoulOrb`, `HealNearbyEntities`, `AddForceToNearbyEntities`,
`ImmunityZone`, `AuraDistanceOverride`.

### Items, equipment and weapons (~40)
`Equipment`, `EquipmentSkin`, `EquippedObject`, `OffHand`, `MeleeWeapon`, `RangeWeapon`,
`BeamWeapon`, `MoveFreelyWeapon`, `CommandMinionWeapon`, `Projectile` (+ `IndirectProjectile`,
`GroundBouncableProjectile`, `MortarProjectile`, `ExplodeOnImpact`), `Explosion`/`Explosive`/
`SequenceExplosive`, `Potion`, `CookedFood`/`CookingIngredient`/`Fullness`, `Jewelry`, `CastItem`,
`Instrument`/`MusicSheet`/`PlayingInstrument`, `PaintTool`, `Drill`, `Scanner`, `Trophy`,
`WeaponSkillGainedMultiplier`, `PrioritizedRepairMaterial`, `CantBeSold`.

### Containers, crafting and trade (~20)
Your chest example lives here. `Inventory` (slot count), `ExtraInventorySize`, `SlotRequirement`,
`UpgradeSlot`, `VanitySlots`, `SellSlots`, `Crafting`, `Recipe`, `Anvil`, `UpgradeCostsTable`,
`Merchant`/`MerchantItemInfo`, `VendingMachine`, `TrashCan`, `CoinAmount`,
`ChangeVariationWhenContainingObject`, `InventoryAuxDataPrefabs`, `Occupiable`, `Owner`.

### Plants, farming and water (~12)
`Plant`, `GrowingPlant`, `GrowingSettings`, `RootPlant`, `Seed`, `Flower`, `Bush`,
`WaterSource`, `WaterSpreader`, `Sprinkler`, `CanBeRemovedByWater`, `Extractable`.

### Tiles, world objects and machinery (~35)
`Door`, `FenceGate`, `GlowLight`, `TableItemLightSource`, `ActAsLightSourceWhenHeldInHand`,
`ConvertToTile`, `PseudoTile`, `ResizableTileSize`, `RemoveTileOnDeath`, `SpawnTileOnExplosion`,
`GroundDecoration`, `CornerSmoothing`, `SurfacePriority`, `AncientElectricityConnection`,
`ActivatedByElectricityState`, `ElectricOrb`, `ManaBarrier`, `Bed`/`CanClaimBed`, `Sittable`,
`MapMarker`/`DisableMapMarkerOnDeath`, `Spawner`/`EnemySpawnerPlatform`, the territory spawners,
`ProximityTrigger`, `EventTerminal`, `TitanShrine`, `BossStatue`, `BossSpawnLocation`,
`SummonArea`/`SummoningItem`.

### Pets, animals and NPCs (~12)
`Pet`/`PetData`/`PetOwner`/`PetCandy`, `Cattle`, `BreedToggle`, `IsHabitableIdol`, `Fish`/`FishShoal`,
`FishingTable`, `FishingNetVisual`, `BaitOnAPole`, `Critter`.

### Presentation and variation (~25)
`Name`, `Description`, `AnimationSpeed`, `Rotation`, `DirectionBasedOnVariation`,
`ChangeVariationAfterTime`, `ChangeVariationWhenTookDamage`, `ChangeVariationWhenObjectNearby`,
`ChangeVariationWhenPlayerHoldObjectNearby`, `PaintableObject`, `VisualSmoothFollow`, `Trail`/
`LeaveTrail`, `DamageEffect`, `TriggerEffect`, `CustomAttackSound`, `MusicArea`, `SeasonObject`,
`CanBeScanned`, `CanBeDiscovered`, `AchievementTracker`, `TriggerAchievementOnDeath`.

### Loot and drops (~10)
`LootTable`, `CustomLoot`, `OnUseLootDrops`, `LootDropsWhenDamaged`, `SeasonAndLoots`,
`AlwaysDropOne`, `AlwaysDropVariationZero`, `DropAllItemsOnHit`, `SpawnDroppedItem`,
`MergeDroppedItem`, `ImmuneToSkipLootDrop`.

### Lifecycle and rules (~25)
`DestroyTimer`, `DestroyWhenNoNearbyPlayer`, `DestroyEntityIfPlacementNotValid`,
`DestroyIfNotOnTile`, `EnsureSameGroundTileBeneathEntity`, `BeDestroyedAlongWithOwner`,
`ActAsDestructibleWhileAboveHealthThreshold`, `NonHittable`, `CantBeAttacked`, `ImmuneToDamage`,
`ImmuneToRangeDamage`, `IgnoreImmuneZone`, `DisablePhysics`, `DetectCollision`, `IsFlying`,
`DontBlockDigging`, `HasSpawnPoint`, `EntityPart`, `CanBeControlledByOtherEntity`, `Warmup`,
`VelocityAffector`, `RandomWalkGravity`.

### Tables (the shared data, ~8)
`BiomesTable`, `ConditionsTable`, `EnvironmentSpawnObjectsTable`, `FishingTable`, `LootTable`,
`SkillTalentsTable`, `UpgradeCostsTable`, `SpawnGroupTable`. These are the game-wide registries —
reaching them is how a mod adds to the game rather than only to its own dimension.

## How this list should be worked

**Not one component at a time.** They cluster: a modder does not want "`SeedAuthoring` support", they
want *to make a plant*, which needs `Plant` + `GrowingPlant` + `GrowingSettings` + `Seed` +
`LootTable` + `CanBeRemovedByWater` together, with one coherent authoring surface over the lot.

So the unit of work is **a thing a modder wants to make**, and the checklist is which components it
needs. Suggested order, most-wanted first:

1. **Containers** — chests, and everything about them (your example). ~20 components.
2. **Creature behaviour** — the state vocabulary, so a creature can do more than chase. ~55.
3. **Items and equipment** — weapons, tools, food, potions. ~40.
4. **Plants and farming** — the full growth cycle. ~12.
5. **World objects** — doors, lights, beds, machinery, spawners. ~35.
6. **Pets and animals** — taming, breeding, fishing. ~12.
7. **Presentation** — variation, effects, sounds, seasons. ~25.
8. **Lifecycle rules** — the modifiers that apply to everything above. ~25.
9. **Tables** — adding to the game's shared registries. ~8.

## The rule that has held so far

Every one of these is *data on a prefab* or *an entry in a table*. Nothing in this list requires
reimplementing a game system — it requires exposing one. That is why the target is realistic despite
its size, and it is why the customization stage that follows can be as deep as you want: the knobs
already exist, we are only building the surface that reaches them.

## Status

Written 2026-08-16, against the decompiled game at `E:\ck mods\ck-db`. Regenerate the counts by
listing `ck-db/Pug.ECS.Authoring/*.cs` and diffing against the `*Authoring` names referenced under
`Assets/ExpandNullforge/{Scripts,Editor}`.
