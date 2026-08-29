# Field coverage: the number that actually decides customization freedom

Run `bash Docs/audit-field-coverage.sh` (add `--detail` for the full list). It is mechanical, so it
does not depend on anyone remembering what was covered.

## Where it stands

**The sweep is finished.** Every authoring component Core Keeper ships is written by the framework,
and every field on every one of them is reachable by a creator.

| | |
|---|---:|
| Authoring types in the SDK | 377 |
| — MonoBehaviour components the framework writes | **375** |
| — not components at all, so nothing to author | 2 |
| Authorable fields on them | 1,307 |
| — reachable by a creator | **1,307** |
| — excluded as genuinely not ours | 66 |
| **Field coverage** | **100%** |

The two that are not components are `PugMod.IAuthoring` (an interface) and `PugMod.ModAPIAuthoring`
— PugMod's baking hook, a plain class that is never put on a prefab, whose two members are get-only
properties rather than serialized fields. There is nothing there for anyone to author.

## Why the component count was the wrong measure

The component count answers *"do we touch this component at all"*. It says nothing about how much of
the component a creator can reach, and a component written with three of its eleven fields **counts
as fully covered** while quietly capping what anyone can build. Where it started:

| | |
|---|---:|
| Components the framework writes | 145 |
| — fully reachable | 58 |
| — **partially reachable** | **55** |
| — markers with nothing to reach | 32 |
| Authorable fields on them | 710 |
| — reachable by a creator | 434 |
| — excluded as genuinely not ours | 38 |
| **Field coverage** | **61%** |

So the honest statement was never "147 components covered". It was: *we touch 145 components and a
creator can reach 61% of what those components can express.* That is the number that moved to 100%.

## What the audit excludes, and why

Two kinds of unwritten field are **not** gaps, and the script filters both so the number means
something:

- **Self-wired siblings.** `level`, `entityMono`, `cooldownAuth`, `weaponDamage`, `belongsToShape`
  and friends. Every one measured is `[HideInInspector]` and assigned by the component itself in
  `OnValidate`/`Reset` via `GetComponent`. Setting one from outside fights the component for a
  reference it recomputes anyway.
- **Runtime state that happens to be public.** `isAffected`, `timer`, `currentSpeed`, `ceasingToShoot`.
  There is no marker in the source that distinguishes these, so each is listed by name against its
  component in the script — a judgement, recorded as one.

## Worst first: attached with NOTHING authored

The most misleading case. The component is present, so everything downstream assumes it is
configured, and every field on it is at its default.

| Component | Fields | What is out of reach |
|---|---:|---|
| `BossAuthoring` | 5 | **`chestToSpawn`** — a boss that leaves a treasure chest. Plus the optional second chest and its offset. |
| `SupportsConditionsAuthoring` | 6 | **`initialConditions`** — a creature that starts poisoned, enraged, chilled. Plus immunity to auras, environment and healing. |
| `DurabilityAuthoring` | 5 | **`durabilityMultiplier`** — see below, this is the real dial. Plus repair and reinforce cost. |
| `ExtraInventorySizeAuthoring` | 3 | `isPouch`, category restrictions, one-size-for-all-levels. |
| `RotationAuthoring` | 3 | `rotatePhysics`, icon offset. |
| `AreaLevelAuthoring` | 2 | `areaLevel` — which biome tier the thing belongs to. |
| `AnimationAuthoring` | 2 | `orientationSupport`, animation history depth. |
| `DescriptionAuthoring` | 1 | `initialText`. |
| `CantBeAttackedAuthoring` | 1 | `removeAfterFirstFrame`. |
| `DontDestroyOnZeroHealthAuthoring` | 1 | `animate`. |
| `ImmuneToSkipLootDropAuthoring` | 1 | `ignoredInCreativeMode`. |

### The durability finding

`DurabilityAuthoring` computes durability as a **type-based base times `durabilityMultiplier`** —
90, 100, 95, 350 or 250 depending on the item type, read straight out of the decompiled source. That
is why a written `maxDurability` reads back as the SDK default: the game recomputes it.

The framework has been exposing `durabilityPoints`, warning that it cannot be baked, and **not
exposing the multiplier that actually works**. A creator who wants a sturdier pickaxe has had no way
to say so. This is the clearest example of the whole audit's point: the component was "covered".

## Ranked by how much freedom each costs

| Unreachable | Component | Have |
|---:|---|---:|
| 29 | `RangeAttackStateAuthoring` | 13/42 |
| 25 | `ChargeAttackStateAuthoring` | 13/38 |
| 23 | `ShootMortarProjectileStateAuthoring` | 12/35 |
| 17 | `ChaseStateAuthoring` | 2/19 |
| 16 | `RangeWeaponAuthoring` | 6/22 |
| 15 | `MeleeAttackStateAuthoring` | 14/29 |
| 14 | `PlaceableObjectAuthoring` | 12/26 |
| 10 | `AttackContinuouslyAuthoring` | 12/22 |
| 9 | `MortarProjectileAuthoring` | 18/27 |
| 8 | `ProjectileAuthoring` | 14/22 |
| 6 each | `VulnerableState`, `TeleportState`, `SupportsConditions`, `DropLoot` | |

**Creature combat is the elephant: 109 unreachable fields across five attack/chase states.** That is
where a custom boss stops being customizable — the framework can make it swing and chase, and cannot
touch the hitbox shape, the anticipation timing, the re-aiming, the spread pattern or the steering.

## The plan, in order

1. **Zero-coverage batch** — the eleven above. Most misleading, least work.
2. **Creature combat states** — the 109. Needs the census-first treatment: these components have
   many fields and vanilla uses a fraction of them, so the surface should be shaped by measured use
   exactly as the projectile and trap surfaces were.
3. **`PlaceableObjectAuthoring`** (14) — placement is a shared spine, so this one widens every
   placed thing at once.
4. **`RangeWeaponAuthoring` mortar half** (16) — now that artillery projectiles exist, the weapon
   side that aims them is reachable work rather than a dead sub-system.
5. **The rest of the tail.**

## Rule going forward

A coverage item is not done when the component is attached. It is done when a creator can reach
every field on it that is theirs to reach — and the audit script says so.

---

## Correction: the number was inflated, and is now 63%

The audit counted a field as reached if `.fieldName` appeared anywhere in our sources. Core Keeper
reuses field names heavily — `damageMultiplier` is on eight components, `skipVisibilityCheck` on
five, `canHitLowTriggers` on two — so writing one component's field scored it as reached on **every**
component that has a field by that name.

Two examples found by hand before the fix:

| Component | Audit said | Actually was | Why |
|---|---|---|---|
| `MeleeAttackStateAuthoring` | 14/29 | 12/29 | `canHitLowTriggers` and `bypassMaxDamagePerHit` were written on `AttackContinuouslyAuthoring` and `MortarProjectileAuthoring` |
| `RangeAttackStateAuthoring` | 14/42 | 9/43 | `damageMultiplier`, `speedMultiplier` and `skipVisibilityCheck` were written on other components entirely |

`audit-field-coverage.sh` now resolves every write to the declared type of the variable it goes
through, per file, and matches `Component<TAB>field` pairs. The reported figure fell from 70% to
**63%** on the same code — the work was real, the measurement was not.

This mattered beyond the number: `RangeAttackStateAuthoring.damageMultiplier` is the only ranged
damage a tiered creature keeps, and it scored as covered while being unreachable.

## The other audit: can a creator reach it at all

`audit-authoring-surface.sh` answers the question that comes before field coverage — whether any
panel draws the value. It found whole features with no editor: creature combat and stats on mobs,
animals and bosses; dungeons; quests. All built, generated and tested, none reachable by clicking.

`DrawSerializedAsset` now draws every serialized field a curated list omits, under an "Everything
else" foldout, so a curated list means ordering rather than permission. The dashboard is a draft and
will be redesigned; this makes the redesign a rearrangement rather than a hunt for what was lost.

---

## 100%

Every authorable field on every component the framework writes — 710 of 710 across 145 components —
is reachable by a creator. Zero partial.

The climb was not monotonic, and most of the movement was the measurement getting honest rather than
work landing. Four separate flaws were found in the auditor, each by noticing that a component it
called a gap was already fully written:

| Flaw | What it did | Found by |
|---|---|---|
| Global `.field` grep | Scored a field as covered on every component sharing the name | `RangeAttackStateAuthoring.damageMultiplier` reading covered while unreachable |
| Per-file type map | Two variables named `ranged` in one file collided | `RangeWeaponAuthoring` reading 1/22 with 7 written a few lines away |
| Lambda parameters unseen | `ApplyComponent<T>(…, c => c.field = …)` invisible | Four item components reading as gaps |
| Lambdas folded into the method | Every lambda names its parameter `component`, so the last won | `DurabilityAuthoring` reading 2/5 with all five written |

Reported figure over the session: 58% → 61% → 64% → 70% → **63%** (first honest measurement) → 69%
→ 77% → 81% → 83% → 89% → 91% → 96% → 98% → **100%**.

The two drops are the interesting ones. A metric that only ever goes up is not measuring anything.

### The rules that changed on the way

- **Zero vanilla users means no measured value to suggest, not "do not offer".** Six fields had been
  declined on that basis. A custom creature is exactly where someone wants the thing the base game
  never built; those fields are now offered, placed lower in the panel with no suggested default.
- **Check the type before declaring a field unauthorable.** Two charge fields were written off as
  "curve assets, nothing sensible to type". They are a two-field class — a turn type and a
  degrees-per-second — and they are how a charge becomes something a fast player can outrun.
- **One control per field.** The mortar barrage does not ask how far it fires from, because the
  ability already asks that in plainer words. Two controls fighting over one field is worse than one
  control in the wrong place.

---

## What is left: the components the framework has never written

100% coverage means every field on every component we *write* is reachable. It says nothing about
components we have never attached. Core Keeper has 377 authoring components; we write 145.

Of the 232 untouched:

- **84 are markers** with no serialized fields at all. `LocalInteractableAuthoring` — on 1,516
  vanilla prefabs, the most-used component in the game — is one of them. Adding it changes field
  coverage by zero; what it adds is a capability. Its converter needs the object's *graphical*
  prefab to carry an `InteractableObject` with at least one persistent `UnityEvent` listener, which
  is authorable from an editor script via `UnityEventTools` but needs a target method to bind to.
- **148 have fields**, 625 of them in total.

Ranked by how many vanilla prefabs use them (counted from the dictionary vault, which resolves
component names per prefab):

| Uses | Fields | Component | What it is |
|---:|---:|---|---|
| 75 | 12 | `AffectObjectWhenMelodyPlayed` | the ocarina — objects that react to played music |
| 51 | 35 | `SnakeMovementState` | multi-segment creatures |
| 66 | 2 | `Extractable` | things a machine can pull resources out of |
| 42 | 17 | `RoamingPath` | patrol routes |
| 60 / 42 | 1 / 4 | `PetWalkState` / `Pet` | pets |
| 45 | 7 | `ActivatedByElectricityState` | powered machinery |
| 33 | 11 | `SummonArea` | summoning circles |
| 45 | 6 | `EnsureSameGroundTileBeneathEntity` | objects that keep their own floor |

The three biggest by usage — `LocalInteractable` (1516), `CustomScenePrefab` (594),
`TriggerEffect` (207) — are markers or CK-owned systems rather than field surface.
