# Where things live

Measured 2026-09-02: 1,195 `.cs` files in 130 folders across four assemblies. This is the folder key.
Read it before guessing; several folder names here do not say what they hold, and every one of those
is written down below rather than left for you to discover.

---

## The four assemblies

| Assembly | Root | Files | Ships? |
|---|---|---|---|
| `ExpandNullforge.API` | `API/Scripts` | 277 | yes |
| `ExpandNullforge` | `Scripts` | 466 | yes |
| `ExpandNullforge.Editor` | `Editor` (minus `Editor/Tests`) | 311 | no |
| `ExpandNullforge.Editor.Tests` | `Editor/Tests` | 141 | no |

The ship set is 743 files — `API/Scripts` plus `Scripts`. Anything inside a folder named `Editor` or
`CodeGen` is left out of a built mod by PugMod, which is the whole reason the boundary is a folder
name and not a setting. `DimensionSandboxGuard.ShipSet.cs` walks it the same way.

---

## The shape of a feature

A feature almost always runs through four places, in this order. If you are chasing one field from
where a modder types it to where the game reads it, this is the path:

1. **`Scripts/Authoring/…`** — the ScriptableObject the modder fills in. One asset per thing a
   modder makes. The `[Tooltip]` text here is the framework's voice; match it.
2. **`Editor/Generators/…`** — the pass that reads that asset and writes Core Keeper components onto
   a prefab. This is the only layer that knows component names.
3. **`Editor/Bootstrap/…`** — what gets written into the consumer's own mod: registrations, and the
   C# the framework emits into `Scripts/Generated` in their project.
4. **`Scripts/<feature>/…`** — the runtime: registries, systems and Harmony patches that make the
   generated content do something in a running game.

---

## `API/Scripts` — the contract

Interfaces, definitions and events. Nothing here generates and nothing here patches. Split by
subject: `Travel` (31), `Generation` (37), `PlayerAccess` (30), `Content` (29), `Core` (25),
`Authoring` (23), `MapAndMarkers` (19), `Encounters` (18), `Portals` (11), `RuntimeState` (11),
`Scenes` (9), `Assets` (8), `Biomes` (7), `Loading` (6), `Zones` (6).

`Zones` and `Biomes` are two different things and both are real: a **biome** is authored terrain and
identity, a **zone** is a catalogued place the travel and map layers address.

---

## `Scripts` — the runtime

| Folder | Files | What is in it |
|---|---|---|
| `Authoring` | 162 | Every asset a modder edits. Subfolders: `Creatures` 47, `Objects` 34, `Assets` 29, `Content` 16, `World` 15, `Layout` 10, `Player` 5, `Fields` 3, `Plants` 2, `Portals` 1. |
| `Foundation` | 88 | `Service` 73 (the `NullforgeDimensionService` partials — the service that answers the whole API), `ObjectIdentity` 6, `Logging` 5, and four loose utilities. |
| `Portals` | 32 | Portal runtime: the component, the drop registry, the item portal. |
| `Creatures` | 24 | Creature runtime and the presentation binder. |
| **`Zones`** | 20 | **Not zones.** Biome identity and atmosphere, region titles, music overrides, ambient and creature spawn, boss phases, layout pinning, triggered tiles. Its namespace is `ExpandNullforge.Zones`. The actual zone runtime is `Foundation/Service/NullforgeDimensionService.Zones.cs`. Its tests are in `Editor/Tests/Biomes`, which is where you would look for them. |
| `Tilesets` | 19 | Tileset registry and the render patches. |
| `Generation` | 17 | Generation providers and passes. |
| `Scenes` | 15 | Custom scene injection. |
| `Plants` | 14 | Crop runtime. |
| `Objects` | 12 | The `EntityMonoBehaviour` views: shop, sign, NPC, cattle, crafting bench, lights. |
| **`Networking`** | 10 | **This is the runtime half of Travel.** Seven of the ten are named `*Travel*`. `API/Scripts/Travel` is the other half. |
| `Diagnostics` | 8 | The self-audit, the patch roster, the companion table. |
| `Persistence` | 8 | Saving. |
| `WorldRules` | 7 | Fishing, set bonuses, environment events, backgrounds, terrain rules. |
| `Explosives` | 5 | Blast fire and explosive hydration. |
| `Skills` | 5 | The custom skill runtime. |
| `Conditions` | 4 | The conditions table patch. |
| `Food` | 3 | Cooking and pairing. |
| `Arenas` | 2 | Arena runtime. |
| `Bootstrap` | 2 | `ExpandNullforgeModEntry` — the mod's own entry point. |
| **`Core`** | 2 | `DimensionFixedStrings`, `DimensionFnv`. **A synonym for `Foundation`**, and there is no rule that tells you which of the two a shared utility belongs in. Four more of the same kind sit loose in `Foundation`. |
| `UI` | 2 | Runtime UI hooks. |
| `Loading`, `Loot` | 1 each | |
| **`Containers`**, **`Vehicles`** | 1 each | One view apiece — `DimensionContainerView`, `DimensionVehicleView`. Their five siblings are in `Objects`. |

---

## `Editor` — the authoring side

| Folder | Files | What is in it |
|---|---|---|
| `Tests` | 141 | See below. |
| `Generators` | 73 | `Shared` 38 (the object spine and the query companions, both split into partials), `Objects` 13, `Creatures` 9, `Plants` 7, `World` 6. **Every generator pass that writes a Core Keeper component is here.** |
| `Authoring` | 64 | `Engine` 38 (the authoring engine), `Assets` 7 (**the utility that WRITES the modder's assets — not the assets themselves, which are `Scripts/Authoring/Assets`**), `Catalogs` 7 (the harvested vanilla tables), `Identity` 4 (rename and delete), `Validation` 4, `Guides` 3, `Migration` 1. |
| `UI` | 55 | `Pages` 20, `Windows` 20, `Drawers` 6, `Shell` 6, `Controls` 3. The authoring window and the eleven studios. |
| `Portals` | 45 | `Studio` 19, `Art` 16, `Packages` 10. |
| `Bootstrap` | 32 | The `DimensionRuntimeConsumerBootstrapUtility` partials — everything written into a consumer's mod, including the C# the framework emits. |
| `Tilesets` | 23 | `Sheets` 9, `Preview` 6, `Studio` 5, `Authoring` 3. |
| `Core` | 13 | The sandbox guard, the asset folder constants, the editor save utility. |
| `Loot` | 3 | Loot table editing. |
| `Localization` | 2 | The CSV writer. |
| `Import` | 1 | The default-art texture importer. |
| `Icons`, `Shaders`, `VanillaPortalReference` | 0 `.cs` | Asset drop-targets, live. `Editor/Icons` is written to by `Editor/UI/Shell/DimensionSectionIcons.cs` and read by the importer; it is empty, not dead. |

---

## `Editor/Tests`

141 files. `Objects` 31, `Creatures` 20, `Guardrails` 15, `Content` 13, `Generation` 10,
`Player` 10, `Tilesets` 8, `Api` 7, `Portals` 7, `Core` 6, `Biomes` 4, `Plants` 3, `Scenes` 3,
`Zones` 2, plus `DimensionFrameworkSourceScanner.cs` and `DimensionTestScratchFolder.cs` loose at the
root. `Guardrails` holds the two whole-tree guards — the file-name one and the citation one.

**The tests were regrouped and the code was not.** The test folders use merged concern names that
`Scripts/` never adopted, so the two halves disagree on purpose-built vocabulary:

| Looking for a test of… | It is in | Because the code is in |
|---|---|---|
| food and cooking | `Editor/Tests/Objects` | `Scripts/Food` |
| containers | `Editor/Tests/Objects` | `Scripts/Containers` |
| vehicles | `Editor/Tests/Objects` | `Scripts/Vehicles` |
| explosives | `Editor/Tests/Objects` | `Scripts/Explosives` |
| skills and conditions | `Editor/Tests/Player` | `Scripts/Skills`, `Scripts/Conditions` |
| biome atmosphere, region titles | `Editor/Tests/Biomes` | `Scripts/Zones` |
| creature spawning | `Editor/Tests/Creatures` | `Scripts/Zones` |
| layout versions | `Editor/Tests/Zones` | `Scripts/Zones` |
| the map paint panel | `Editor/Tests/Zones` | `Editor/UI/Pages` |

`Editor/Tests/Zones` and `Editor/Tests/Biomes` are the pair worth knowing: neither name predicts its
contents, and between them they cover one runtime folder that is named after neither.

**Two namespaces, and a run needs both.** 69 files are `ExpandNullforge.EditorTests`, 72 are
`ExpandNullforge.EditorTools`, one of those being `ExpandNullforge.EditorTools.Tests`. They are
mixed inside nearly every folder. A filtered batchmode run on one name silently misses half the
suite and still reports a pass. `Docs/running-the-tests.md` has the command and the trap.

---

## Two words that mean two things

- **`Authoring/Assets`** appears twice. `Scripts/Authoring/Assets` is what a modder creates;
  `Editor/Authoring/Assets` is the utility that creates them.
- **`VanillaPortalReference`** appears twice, at the framework root and under `Editor`. Both hold
  reference art.

---

## What is deliberately not here

- No CI. `Docs/running-the-tests.md` says why.
- No line numbers in any citation. A comment names a member or a partial; the numbers rotted
  silently through two file splits and `DimensionCitationTests` now fails on a new one.
- The folder mismatches above are recorded, not fixed. Renaming `Scripts/Zones` means renaming the
  `ExpandNullforge.Zones` namespace and every reference to it, and folding `Scripts/Core` into
  `Scripts/Foundation` means the same. Both are changes to working code across the whole tree, and
  the runtime half they touch — travel, persistence, the arena and RPC systems — has almost no test
  coverage, so a compile is nearly all the evidence a run could give. Writing the mismatch down
  costs a reader one page and risks nothing; the moves are worth doing when there is a play-mode
  assembly to catch what a compile cannot.
- **Namespace does not mirror folder, and has not for a long time.** Measured over the tree as it
  stood right after the restructure: 973 of 1,190 files sat in a folder whose leaf name differs
  from their namespace leaf, across 87 folder-to-namespace pairs; the worst is
  `Scripts/Foundation/Service`, 73 files of `ExpandNullforge.Foundation`. It was 717 before, and
  the restructure moved files without touching code, so it moved the count further from the rule
  rather than towards it. Nothing depends on the mirror — Unity does not, and neither
  does the compiler — so this is a note about what the folder tree does NOT tell you, not a defect
  waiting on a fix. Use the tables above rather than a namespace to find a file.
- `Docs/` still holds eleven `.sh` and `.pl` harvest scripts, and Unity imports each of them as a
  `TextAsset` on every domain reload. They stay because the code and the docs name them by that
  path; moving them out of `Assets/` would break every reference for a small import saving.
- Two folders named `VanillaPortalReference`, one loose runtime file
  (`Scripts/DimensionRuntimeTestAssemblyInfo.cs`) and `DimensionPortalEditorTestAssemblyInfo.cs`
  sitting in `Editor/Core` are all known and all left. Moving an assembly-info file is the kind of
  change whose only failure mode is silent.
