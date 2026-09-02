# Written but never asked: the validation spec nothing runs

The authoring templates carry a set of questions about what a creator has filled in — a dish that
names no ingredients, a swing with no reach, a summoning circle with an alternative and no first
choice. Each one is a short, well-named `public bool` with a sentence explaining why it matters.

**Nothing asks any of them.** They are a validation spec that was written and never wired to a
validator, so they read as dead code and are in fact a feature gap.

This page lists them with their own words. It is the input to the D5 decision — wire them or remove
them — and it must be read before either.

## How the list was produced

A scan over all 919 `.cs` files: every `public bool` declared under `Scripts/Authoring/`, then the
whole tree searched for its name. 1,088 declared, **83 never named anywhere outside their own file.**
The scan was run outside Unity and its output is reproduced below verbatim.

## Two corrections to the census

The dead-code census listed two groups as inert that are not. Both are read, by cast, in
`Editor/Generators/Shared/DimensionObjectSpine.cs`:

| Claimed inert | Actually |
|---|---|
| `DimensionWeaponTemplate.SwingArc`, `.Flourish`, `.CastPurpose` | Read at `DimensionObjectSpine.cs:3167`, `:3168`, `:3328` — `melee.arcAngle = (ArcAngle)(int)weapon.SwingArc`, `melee.attackFXType = (AttackFXType)(int)weapon.Flourish`, `cast.useType = (CastItemUseType)(int)weapon.CastPurpose`. **Live. Do not delete.** |
| The six `DimensionInstrumentTemplate` track names (the plan said eight; there are six — `harpTrack`, `fluteTrack`, `celloTrack`, `ocarinaTrack`, `drumkitTrack`, `pianoTrack`, at `Scripts/Authoring/Objects/DimensionInstrumentTemplate.cs:49,53,57,61,65,69`) | Written at `DimensionObjectSpine.cs:7526–7531` onto the music sheet's `SFXTableIDField`s, and `SheetIsBlank` / `SheetIsIncomplete` / `HasNoOctave` / `TracksWritten` are all read at `:7523`, `:7548`, `:7555`, `:7560`, `:7563`. **Live. Do not delete.** |

These are the enum-cast pattern the plan warns about elsewhere: the member is never named because
the whole enum is converted with `(int)`, so a name-based reference check reports zero.

The one thing on the weapon template that *is* unasked is `SwingsAtNothing`, and it is a validation
predicate like the rest — it is in the list below.

## The predicates, in their own words

Blank means the member carries no `///` summary.

### Things a creator filled in that will be ignored

```
DimensionContinuousAttackTemplate
  GrowthWillBeIgnored          Whether a growth target was set on a reach that never grows.
  BreakDelayWillBeIgnored      Whether a break delay was given to something that never breaks.

DimensionLeavesBehindTemplate
  NamesSomethingItWillNeverLeave
                               Whether something was named and then set never to appear.

DimensionSummoningCircleTemplate
  AlternativeWithoutAPrimary   Whether it names an alternative to summon without naming a first
                               one. The alternative is the second choice, not a standalone. With
                               no first one the component is never attached, so the alternative is
                               never read either.

DimensionTraderTemplate
  SeasonalRuleWithoutASeason   Whether it is told to vanish out of a season it does not belong to.

DimensionItemEffectsTemplate
  HeldEffectsWithNothingToGrant
                               Whether it says effects apply while held but has no effects to apply.

DimensionWorldObjectAsset
  HurtsAndProtectsAtOnce       Whether it both hurts and protects, which cancel each other out.

DimensionContainerAsset
  RestrictsAnything            Whether this rule actually restricts anything. A rule that accepts
                               everything is worse than no rule: the game still evaluates it on
                               every item moved, and the player sees a restriction hint on a
                               container that has none.
```

Every one of these is a control that silently does nothing. That is the project's own named defect
class — a control the modder can see must do what its label says — and here are seven of them, each
already detected and never reported.

### Things that will not work at all

```
DimensionCreatureAbility
  NeedsTargetObject            Whether this kind cannot work without an object naming what it
                               produces. These four states each read an ObjectID and do nothing
                               useful without one: a mortar with no projectile fires nothing, a
                               breeder with no baby produces nothing, an evolver with nothing to
                               become never changes. Explode is the exception that still
                               half-works — it plays its timer and simply spawns no explosion.

DimensionCookingTemplate
  IsAnIngredientOfNoKind       Whether it is an ingredient with no kind, which the pot cannot
                               match on.

DimensionWeaponTemplate
  SwingsAtNothing              Whether it swings with no reach at all.

DimensionDungeonShapeTemplate
  HasNoRoomToGrow              Whether it generates a dungeon with no room to grow into.
  OutlineIsFlat                Whether the outline is shaped with no wobble to shape it.
```

### Worth saying, not an error

```
DimensionCookingTemplate
  IsADishThatNamesNoIngredients
                               Whether it is a dish that names no ingredients to take its colours
                               from. Not an error — the four colours can be set by hand, and a dish
                               that should not look like the sum of its parts is a real thing to
                               want. Worth knowing about, because the ordinary case is naming the
                               two ingredients and letting the generator do it.

DimensionConditionAsset
  LastsForeverAndCannotBeStacked
                               It never runs out and nothing stacks it, which is only right for
                               something meant to be taken away by hand.

DimensionContainerAsset
  CanBeIndestructible          Whether the unbreakable tick is available at all. Only for a
                               container the world places. Vanilla has no unbreakable container of
                               any kind — all 216 are breakable, and the 74 prefabs carrying
                               IndestructibleAuthoring are set pieces like the Core and the caveling
                               ships — so this is deliberately beyond vanilla, and the reason to
                               bound it is concrete: a crafted one can be placed in a player's own
                               base and then never removed.
```

### Plain "is anything set" questions

These are the cheap ones. Most are one line and several read as the natural guard a UI would want
before drawing a section.

```
DimensionInitialConditionsTemplate  SaysAnything, StartsWithAnything, NamesACondition
DimensionItemEffectsTemplate        GrantsAnything
DimensionTileOutcomeTemplate        ChangesTheTile
DimensionSceneObjectTemplate        HasContents
DimensionDropSource                 NamesASource
DimensionPlantAsset                 HasVersions
DimensionPlantArtTemplate           WashesColour
DimensionPursuitTemplate            KeepsItsDistance
DimensionMeleeShapeTemplate         OverridesTheHitbox
DimensionCreatureStatsTemplate      HasDetectionRadius
DimensionCreatureLifecycleTemplate  NamesAnAnimation
DimensionCreatureAnimationTemplate  Repeats
DimensionImpactFeedbackTemplate     NamesAPuff
DimensionObjectBasicsTemplate       TurnsToFaceThings
SceneTemplateAsset                  UsesRadialPlacement, HasSceneObjects
```

### Not templates — three other groups in the same scan

**`DimensionContentValidation`** — `IsVanillaObject`, `IsOneOfYourOwn`, `ResolvesAsItem`,
`ResolvesAsLootTable`, `ResolvesAsBiome`, `ResolvesAsObjectOrItem`, `ResolvesDropSource`,
`LootTableReferencedByCreature`. These are private-by-convention helpers of the validator itself,
used inside their own file — each occurs two or more times, so the scan sees them only because it
looks outside the file. They are *not* the unwired group and must not be swept up with it. The one
exception was `ResolvesSpawnSubject`, which occurred exactly once in the whole tree — its own
declaration — and is deleted. `IsOneOfYourOwn` in particular carries a load-bearing note: the mod's own list is
asked first, because a creator may name an item exactly as the game names one of its own, and the
recipe emitter decides ownership the same way — asking "is it vanilla" first would let a recipe
validate clean and then register against a different object.

**`DimensionVisualAssetValidation`** — `VisualRuntimeAsset`, `RequiresUgcSpriteObjectLitReview`,
`RequiresRuntimeSwapReview`, `RequiresLightingReview`. Same shape, same caveat.

**`DimensionPortalVisualProfileAsset`** — `CenterParticlesEnabled`,
`CenterParticlesFollowCenterPalette`, `CenterParticleTrailsEnabled`, and eight more
`CenterParticle*` accessors the bool-only scan did not reach (`CenterParticleSprite`,
`CenterParticleTexture`, `CenterParticleSizeMultiplier`, `CenterParticleLifetimeMultiplier`,
`CenterParticleOrbitSpeedMultiplier`, `CenterParticleRadialSpeedMultiplier`,
`CenterParticleRadiusMultiplier`, `CenterParticleTrailLifetimeMultiplier`). All eleven **are** dead
and are deleted by this cleanup; the five that had readers (`CenterParticleTint`,
`CenterParticleEmissionMultiplier`, `CenterParticleOffsetPixels`, `CenterParticleScale`,
`CenterParticleRotationDegrees`) stay. **Their backing `[SerializeField]` fields are alive and are
read by string** through `SerializedProperty` (`DimensionPortalAppearanceStudio.cs`,
`DimensionPortalPackageEditorUtility.cs`). Delete the accessors; never the fields, never rename a
field. Nine of the fields now warn CS0414 as a result, and a comment on the field block says why —
the warning is the only honest signal that the block is reached by name and not by code.

**The customizer and wizard model** — 23 more (`BlocksRun`, `HasBlockingSteps`,
`AllowAssetMutation`, `AllowRuntimeMutation`, `RequiresInput`, `CanRunAutomatically`,
`MutatesSession`, `CreatesWorkspace`, `HasBlockedActions`, `DuplicateTarget`, `CanRollback`,
`AllowsEmpty`, `AllowsFreeText`, `HasFixedOptions`, `CanRunAction`, `HasSearchQuery`,
`CanMutateAssets`, `CanRunReadOnly`, `IsAutomatic`, `IsRequired`, `HasGraph`, `HasBlockingIssues`,
`HasBlockedEdits`). These belong to the unreached authoring model and are covered by
`authoring-rollback-spec.md` and `authoring-staged-execution-note.md`, not here.

## What wiring them would take

Nothing structural. The validator already walks every asset and already emits
`DimensionAuthoringIssue` values with a severity, a code, a message, a record kind and a record id.
Each predicate above is one `if` and one issue, and the message is already written — it is the
`<summary>` line.

The three severities the list divides into are exactly the three the validator already has:

| Group above | Severity |
|---|---|
| Things that will not work at all | `Error` |
| Things that will be ignored | `Warning` |
| Worth saying, not an error | `Info` |

The reason this is a decision rather than a task: turning them all on at once will light up warnings
on assets people have already authored and shipped, and some of those warnings will be correct about
content somebody deliberately made that way. That is worth doing, and it is worth doing on purpose.
