# What you can build with the Dimensions API

This is the honest product statement for the framework: what a person can actually make with it
today, said in the words a player would use, and — with equal weight — what it cannot do and why.

It exists because "there is a field for it" and "it reaches the game" are different claims, and the
second one is the only one worth making. Every capability below was walked from the box you type in
to the code the game runs. Where the walk stopped short, this document says so instead of rounding
up.

## How to read this

Each capability carries the same maturity word the framework uses internally
(`API/Scripts/DimensionCapabilityRegistry.cs`), so the badge on a Studio page, this document, and
the code all say one thing:

| Badge | What it means for you |
|---|---|
| **Stable** | The whole path exists and there is running evidence for it. |
| **Implemented (unproven)** | The whole path exists and is locked by tests. Nobody has watched it work in a running game yet. |
| **Partial slice** | A narrow road works. The wider promise the name suggests does not follow. |
| **Provider required** | A seam another mod can plug into. The framework does not supply the content itself. |
| **Preview (authoring only)** | You can fill the form in. It does not become anything yet. |
| **Experimental** | Substantial, recently changed, and not yet cleared. Expect to find things. |
| **Not implemented** | A name and a known plan. Nothing behind it. |

### The one ceiling that applies to everything

**Nothing in this framework has been run inside Core Keeper yet.** All four projects build clean,
1,050 tests across 93 edit-mode test files are written and compiling, and every chain in this
document was traced through real code. That is evidence of correctness on paper. It is not evidence
of a working mod, and it is worth being exact about the gap: the tests are **edit-mode** tests over
generator and registry logic, so even a fully green run would say nothing about persistence, load
order, world switching or multiplayer. Wherever you read "Implemented (unproven)", the missing proof
is the same one every time: somebody starting the game and looking.

Nothing here is rated **Stable**, and that is deliberate.

## How a mod is made

The authoring window walks eleven studios in order
(`Editor/UI/DimensionJourney.cs:50-60`). Each one owns a part of the mod, and the last one builds
it:

**Dimension → Portal Studio → Tileset Studio → Biome Studio → Layout Studio → Item Studio →
Gardening Studio → Monster Studio → Dungeon Studio → World Generation → Review and Build.**

You fill in assets; "Review and Build" runs every generator once, writes prefabs, sprite sheets, a
localization table and a generated bootstrap file, and that bootstrap is what registers your content
with the game at load. There is no C# to write and no Unity component to place by hand.

---

# What you can build

## Things a player holds

### Items — *Implemented (unproven)*

Gear, tools, materials, treasure. You give an item a name, a description, an icon, a rarity and a
template ("what kind of item is this"), and the framework writes a real Core Keeper object: how it
sits in the inventory, how it is placed, what it damages, how long it lasts, what it is worth to
repair. Names and tooltips are merged into the mod's own localization table, so the game shows your
words and not a raw id.

**Known limits.** Durability is derived by the game at runtime, so a durability number you type is
not baked into the prefab — the generator warns you. An item's craft *cost* is not part of the
recipe; Core Keeper stores it on the item that comes out, so it is stamped there instead.

### Weapons and things that fly — *Implemented (unproven)*

Swing arcs, charged attacks, the shape of a melee hit, and projectiles with their own flight,
bounce, spread and impact. Attack, impact, wind-up and heavy-swing sounds are chosen **by name** —
you type the sound's name and the framework hashes it the same way the game does
(`Scripts/Authoring/DimensionSoundNames.cs:29-31`, Unity's own `Animator.StringToHash`), so all
1,400-odd of the game's sounds are available without a lookup table.

### Bombs — *Implemented (unproven)*

One thing you author becomes two objects: the bomb you place and the blast it turns into. Three
triggers, all built from the game's own data — a countdown, something coming close, and a wire
giving it power — plus "somebody broke it", which every explosive gets free and is what makes a pile
of bombs chain.

**Known limits.** Terrain only breaks within 4 tiles no matter how far the blast reaches — that is a
hard-coded 9x9 block in the game. Throwing is not offered. The remote detonator is wired to the
game's own remote explosive and cannot be pointed at yours.

### Armour that changes how you look — *Implemented (unproven)*

A piece of armour can carry its own paperdoll art. The art is a separate data block the item points
at by address, which is a thing mods are allowed to register
(`Editor/DimensionEquipmentSkinGenerator.cs:46`). The sheet is a fixed 234x156 layout.

### Instruments and music sheets — *Implemented (unproven)*

An object can be an instrument (note sound, octave-up sound, which key it starts on) or a music
sheet naming a track per instrument. Both write the game's own components
(`Editor/DimensionObjectSpine.cs:6304-6340`), and the generator tells you if you made a silent
instrument or a blank sheet.

## Recipes and workbenches

### Recipes, workbenches and loot — *Implemented (unproven)*

An authored recipe reaches a crafting station three ways, all through one registry: at one of the
game's own benches, at a Workbench you made, or **by hand** — which in Core Keeper means the player
is themselves a crafting station carrying six recipes.

**Known limits.** The by-hand list holds twelve on top of the game's six and cannot page. A recipe
offered at a Workbench that generates no object is refused rather than silently dropped. A recipe
whose output is one of the *game's* items is refused, because the price would have to be written
onto an object you do not own.

### Loot tables — *Implemented (unproven)*

Your own drop tables become real game tables. Ids are minted from the name and appended at the
game's own conversion seam, so nothing is patched at the moment loot is rolled. Every field that
names a table — creature drops, dungeon chest loot, container loot, melody rewards, on-use loot —
resolves the game's names first and yours second.

### Where things drop from — *Implemented (unproven)*

You can answer "what drops this" on the item itself, naming mobs, chests or blocks and how often,
rather than editing a table per source.

## Living things

### Monsters, animals, critters and bosses — *Implemented (unproven)*

Every kind generates as a real creature: stats, states, attacks, habits, lifecycle, and where it
lives (surface, chance, amount, whether it comes in groups, which biomes allow it). Every kind gets
a body — a sprite sheet built from the animation strips you supply, a view shaped like the game's own
caveling, and hit and death sounds. Temperament is a plain choice: it hunts you, it ignores you
until hit, or it never fights.

You can also **borrow** one of the game's own behaviours wholesale by name — 280 vanilla attacks,
chases and wanders were harvested into named presets, so a monster can move and fight exactly like
something already in the game.

**Known limits.** The framework's creature body is one shared pooled type per kind, so anything that
differs between two creatures is re-derived when each one spawns rather than baked into a prefab.
Taming is not exposed at all.

### Creatures that keep coming back — *Implemented (unproven)*

A mob can name the tile surface that breeds it — your tileset or the game's; ground, nest, slime
coat or water — and ride the game's own periodic respawn sweep. This is vanilla's dungeon
repopulation mechanism, and by the game's own design it runs inside spawn-blocked dungeons.

### Boss presence — *Implemented (unproven)*

Map pins, a floating name, fight music (the game's rosters or your own), summoning a boss with an
item by name, and a real respawn cooldown.

**Known limit that matters.** The game records "this boss has been defeated" for eight named bosses
only. **A custom boss's defeat is not written into the world record**, so anything you want to unlock
off your boss has to track that itself.

### Bosses built from the game's own boss kits

*(Part of **Custom creatures** and **Boss presence** above; it has no separate badge because it is
not a separate path — it is the same generated boss pointed at one of the game's behaviours.)*

You can point a boss at one of Core Keeper's boss behaviours. How much you get depends on the kit,
and the full census is in `Docs/IdentityGates.md`:

- **The Wall, Scarab, Larva, Shaman, Cicada** — no identity check at all. You get the whole
  behaviour.
- **The Bird, Octopus, Snake, Core** — the check is on the thing the boss *spawns*, not the boss. Point
  it at the game's own stone, tentacle, segment or boulder and you get everything.
- **The Hydra** — heads, burrowing, surfacing and the base attack pattern all work. The four
  per-variant flourishes do not.
- **The Slime King's shot cycling** — gated entirely. A custom object carrying the block is skipped.
  The framework says so when you build, rather than shipping it silent.

## Growing things

### Plants, crops and food — *Implemented (unproven)*

A crop is authored once and generates its seed, its growing stages, its ripe form and its rarer
versions, and it is **visible**: one picture per stage plus the seed in the soil become a sprite
sheet, and the art, colour, shadow and glow are re-chosen per plant from its id and variation, which
is what gives a rare version a look of its own.

Cooking is authored on the item — what it is (plant, fish, meat), the four shades a dish borrows
from its icon, what eating it raw gives, what it gives cooked, its golden version, and the dish it
makes.

**Known limits.** A dish is built from two ingredients, because that is the pot's rule. A modded crop
cannot be shown inside a planter box, whose display is keyed to the game's own list.

## Places

### Custom tilesets — *Experimental*

You can invent a block: its art, how it renders, how it is placed, what it looks like on the map,
and a "{name} Block" item that places it. Identity is a hash so two mods do not collide.

**The known limit worth planning around:** enemies treat a custom wall as one they **cannot mine**.
The game's pathfinder reads wall durability from a fixed 75-entry table inside a Burst-compiled job,
and every custom id is above that bound, so the cost comes back effectively infinite. Enemies route
around your wall and only tunnel when there is no other path. This is usually what a builder wants,
and it cannot be patched: the job is compiled, so no hook reaches it.

Two more from the same family, both handled with warnings rather than surprises: a tileset used only
as scene decoration (with its block items switched off) **loses every tile of itself inside dungeon
rooms**, because dungeon rooms resolve tiles through an object that claims the tile; and a custom
tileset used as **water** would index two fixed arrays (75 and 12 entries) out of bounds the moment a
bobber lands, so the rule is that the liquid layer of a custom area uses one of the game's own
waters.

### Biomes — *Implemented (unproven)*

A biome gets a name, a map colour, a title card when a player discovers it, its own ambience and its
own music, and the blocks that signify it — which is how the framework knows an area is yours. Which
ores can appear is a per-biome list that gates ore generation.

**Known limits.** Core Keeper's biomes are radial; you cannot draw one an arbitrary shape at the
biome layer (shape lives at the tile layer instead). Five fields on the biome asset — its
environment profile, palette, spawn table, resource table and world-event table — are carried in the
framework's own record but have **no effect in the game**; they are not offered in the Biome Studio
for that reason.

### Ore veins — *Implemented (unproven)*

Per-ore abundance and vein size grow deterministic, wall-safe veins on both painted and generated
terrain, gated by the biome's ore list.

### Painted terrain and layouts — *Implemented (unproven)*

You can paint a tile map and have it generate, and a world **pins** the layout it was made with, so
an existing save does not silently regenerate into a different shape when you edit the mod. The pin,
the fingerprint and the archive of past layouts are real.

### Scenes — *Implemented (unproven)*

A scene is a piece of hand-made place — tiles, objects and triggers — that the generator drops into
the world. Placement policy (mode, how far out, weight, unique, required) reaches a real generation
pass with a vanilla-shaped search for a spot.

### Dungeons — *Implemented (unproven)*

Rooms, paths, fill, room fillings, swaps, single-scene mode, placement inside your dimension, binding
to the Overworld's random tables, and pinning a unique dungeon at a spot in the Overworld. Alignment
is engineered rather than hoped: scenes register an explicit centre, room radii default to the exact
fit, corridor widths snap to the odd band the game actually carves, and an undersized room warns you
with the number to use.

### Named areas — *Implemented (unproven)*

One asset gives a place a title card, ambience and music, fanned out to its signature blocks.

**Known limit.** Areas have no border — they exist wherever their blocks stand, exactly like the
game's own.

### Dimension types — *Implemented (unproven)*

World, Dungeon, Arena and Room each enforce real behaviour: whether ambient monsters spawn, dungeon
music, arena reset, and exits that arm on victory. Any dimension can also name its own music, which
outranks the type's default and yields to a boss fight.

### Portals — *Experimental*
### Portal Studio — *Experimental*

Extensive, recently reworked, and the part of the framework most likely to surprise you. The visual
Studio (previews, profiles, layers, packages, profile-owned baking, a parity validator) is real
work; none of it has been checked against a running game's own portal.

## Furniture, props and machines

### Containers — *Implemented (unproven)*

Chests with real grids (the game's own 6x3 and 12x3), contents that live on the placed chest rather
than the item, two-hits-with-anything to break, and lockable chests that read as a one-slot key
socket.

### World objects — *Implemented (unproven)*

Props, decorations and functional objects: what they look like, how they are placed, what they do
when used, what they leave behind when broken, whether they turn to face you.

### Signs, chest names and name tags — *Implemented (unproven)*

Words that float above a thing in the world: a sign shows what is written on it, a chest shows the
name a player gave it, a tended animal wears a name tag. This uses the game's own label components
and was recorded as impossible three separate times before it was found to work.

### Doors and gates — *Implemented (unproven)*

Something opens when you hold the right item, when the right object is nearby, or when you play the
right melody.

### Automation, wiring and traders

*(All three are surfaces on a world object, and share its **Props, decorations and machines**
badge.)*

Objects that take part in the game's power and conveyor systems — what needs power, what pushes,
what picks up, what may reach into inventories — and shopkeepers with stock, unlocks and seasonal
rules.

### Things a player rides — *Implemented (unproven)*

A vehicle is a whole object: an item with an icon, placement, two hits to break with the vehicle
given back, and a body you walk up to and use. Getting on runs Core Keeper's own code and consults
no object id, so your vehicle is driven by the same system as the game's.

**Known limits.** There are exactly **three** kinds and a fourth is impossible — the player has three
riding states and the dispatch is compiled. Custom rails are impossible (the rail tile is compared
against a literal inside a Burst job). A go-kart's ram damage is a hardcoded 10. A generated vehicle
has no dust, smoke or engine loop, because the game's own components read a hand-built particle
hierarchy with no null check.

## Rules of the world

### Custom stat effects — *Implemented (unproven)*

You can invent an effect the game does not have — the 358th — and it becomes a real condition
through a guarded patch on the game's own conditions table. A talent can grant one.

### What upgrading costs — *Implemented (unproven)*

Per-level prices, including prices paid in your own bars. The shared table is deep-copied, swapped in
for the call, and put back — it is never mutated.

### What fishing catches — *Implemented (unproven)*

Per-biome and per-water catches ship as the game's own config files, which it merges at start-up with
no code of ours involved. Fight patterns take whichever of two roads can carry them.

**Known limits.** A custom biome or custom ground **cannot be keyed** — the baked table has 12 biome
and 75 ground slots. Every row needs a junk table, because the game reads a row without one as no row
at all.

### What talents give — *Implemented (unproven)*

Talent names, effects and per-point values, shipped as the game's own config.

**Known limits.** The twelve skills are the game's and a thirteenth cannot be added. The game merges
by position, so a skill's talents are listed whole.

### Overrides on the game's own player — *Partial slice*

Exactly three numbers — turning delay, vehicle drift and aim offset — written over the game's player
just before a loading world reads it, then put back. A mod cannot ship a player.

### Extra things saved inside containers — *Not implemented*

The seam is known and real, but nothing has needed it, so there is no form to fill in. It will be
built when a feature asks for it.

## Words and sound

### Names and tooltips in the player's language — *Implemented (unproven)*

Everything you name is collected into one localization table that ships with the mod, on a single
generate path, with a coverage check that fails the build rather than shipping a raw id.

### Sound by name — *Implemented (unproven)*

Every sound in the framework is chosen by typing its name. The framework hashes it exactly as the
game does, and a name that is neither the game's nor blank is called out at generate time so a typo
does not ship as silence.

---

# Things that are seams, not features

### Third-party tileset providers — *Provider required*

There is a priority-arbitrated registry so that this framework and a *different* tileset mod can be
installed together without fighting over the tile registry
(`API/Scripts/DimensionTilesetProviderRegistry.cs`). **No provider ships, including ours** — nothing
in the framework routes through it today. It is an offer to another mod author, not a feature you
can use. It is listed here so that reading the interface does not lead anyone to think registering
with it will do something.

### The service and API layer — *Implemented (unproven)* / *Partial slice*

Dimension identity, coordinate translation, cross-mod slot allocation, per-world persistence,
loaded-area orchestration, travel and networking, content manifests and ownership. These are real and
used by everything above, but they are plumbing rather than something you author, and the specific
proofs they lack (load order, migration, world switching, many mods at once, multiplayer, corruption)
are named per row in the capability registry.

---

# The shape of the limits

Almost every hard limit in this framework has one of four causes. Knowing which one you have hit
tells you whether there is a way around it.

**1. The game asks which object this is.** Some systems check a specific object id before acting. A
custom object reaches the system, fails the question, and is skipped — with no error and nothing in
the log. Every one of these that touches something the framework offers is censused in
`Docs/IdentityGates.md`. Where a workaround exists (point the kit at the game's own companion
object), the tooltips say so.

**2. A fixed-size table.** Wall durability is 75 entries. Fishing is 75 grounds and 12 biomes. The
tile-deserialization gate is 75. Custom ids are above these bounds by construction, so the answer is
either a fallback or a rule ("use the game's own water").

**3. Compiled code.** Most of these tables are read inside Burst-compiled jobs, where a hook does not
take — the compiled code is what runs and never calls the patched method. De-Bursting is possible but
usually the wrong trade: the pathfinder runs for every enemy in the world.

**4. A count the game hard-codes.** Three riding states. Twelve skills. Two ingredients in the pot.
Six recipes on the player. These are not bounds to work around; they are the shape of the game.

---

# How to check this document is still true

Five mechanical sweeps catch the failure this framework keeps having — a surface that exists,
compiles, is tested, and is connected to nothing:

1. Every `Append*Registrations` has a production caller. *(22/22)*
2. Every system class is explicitly created in the mod entry point, guarded permanently by
   `Editor/Tests/DimensionSystemLivenessTests.cs`. *(29/29)*
3. Every `[HarmonyPatch]` names a method that exists in the game, guarded by
   `Editor/Tests/DimensionHarmonyPatchTargetTests.cs`. *(40/40)*
4. Every field row in the authoring catalog resolves to a real field on the asset its page edits.
   *(210/210)*
5. Every registry has both a producer and a reader.

A capability that fails any of these is not a capability. That is the standard this document was
written to.
