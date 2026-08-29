# Time-attack dungeons — design

You asked for this one to be designed before it is built. This is that design.

## What it is

A dungeon that starts a clock when the player enters and rewards them for how fast they clear it. Not
a new kind of place — the same authored rooms as any other dungeon — but with a run wrapped around it.

## Why it is worth designing first

Every other feature in this framework attaches to something Core Keeper already does. A time attack
does not: the game has no run, no clock, no leaderboard, and no notion of "cleared". So this is the
one place where the shape has to be chosen rather than discovered, and where choosing badly produces
something that works and is no fun.

Three decisions carry the whole thing.

## Decision 1 — what stops the clock

The obvious answer is "kill everything", and it is the wrong one. A player who has beaten the dungeon
and is hunting one bat behind a rock is not having a good time, and worse, the last thirty seconds of
every run are identical and dull.

Better options, in order of preference:

1. **Reach the end room.** Clean, readable, and it makes route knowledge the skill being tested. Fits
   the existing `End` room role exactly.
2. **Defeat the dungeon's boss.** Good when the dungeon has one; makes the fight the climax rather
   than an obstacle before the real ending.
3. **Collect N of something placed in the rooms.** Turns the run into a routing puzzle. Strongest
   design of the three, most work to author, and needs the objects to be placed deterministically or
   two runs are not comparable.

**Recommendation: reach the end room, with the boss as an option.** Both are one condition, both are
already expressible with what exists.

## Decision 2 — what a good time is worth

Three tiers, not a continuous curve. A curve means every run is worth slightly more or less than the
last and none of them feel like anything; tiers mean crossing a threshold is an event.

- **Gold / Silver / Bronze**, authored as three times in seconds.
- Each tier names its own loot table. Nothing else changes — the same dungeon, the same rooms, a
  better chest.
- **Every completion pays something.** A run that finishes outside bronze still gives the bronze
  table. Making a slow run worth nothing teaches players to abandon runs, which is the opposite of
  what a time attack is for.

## Decision 3 — what happens on a second run

This is where time attacks usually break.

- **The dungeon must reset.** A run through rooms the player already cleared is not a run.
- Core Keeper does not regenerate a dungeon in place, so the honest options are: place a fresh
  instance elsewhere, or restore the rooms from their scenes.
- **Restoring from scenes is the right one** and the framework already does exactly that — a scene
  stamp is idempotent, so re-stamping every room of the dungeon restores it wholesale, including its
  containers and its creatures.
- **Best time is per character, not per world**, matching how biome discovery already works. A world
  shared between friends should let each of them own their own record.

## What it needs that does not exist yet

| Piece | Status |
|---|---|
| Authored rooms, placed by the game's own generator | **Exists** (D1) |
| Re-stamping a scene to restore a room | **Exists** (A2/E1 — scene placement is idempotent) |
| A completion condition | **Exists** in shape — the quest system's `Reach` and `Defeat` objectives are exactly this |
| Per-character persistence | **Exists** — the quest journal is already per character |
| Tiered rewards by a measured value | New, small: three thresholds and three loot tables |
| A clock, and showing it | New: a start, a stop, and a visible timer |

The gap is genuinely small, and most of it is presentation.

## The shape it should take

**A time attack is a quest with a stopwatch**, not a new subsystem.

- Entering the dungeon auto-starts a quest — the journal already supports `AutoStart`.
- Its objective is the completion condition — already expressible.
- The clock is a start time recorded when the quest starts and read when it completes.
- The reward is chosen from the tier the elapsed time falls into, rather than being fixed.

That means the work is: a start time on quest progress, a tier table on the reward, and a timer in the
UI. Everything else is reuse.

## What I deliberately did not decide

- **Whether the timer is visible during the run.** It affects the feel enormously — a visible clock is
  tense, a hidden one is exploratory — and it is your call, not mine.
- **Whether a death ends the run.** Ending it is more honest as a time trial; not ending it is kinder
  and keeps the player in the dungeon. Depends on the difficulty you are aiming at.
- **Whether times are shared between players.** A leaderboard is a different feature with networking
  implications, and it should not be smuggled in under this one.

## Recommendation

Build it after playing the dungeons from D1. A time attack is a layer over a dungeon that is already
fun to run; layering it over one that has not been played yet would mean tuning three thresholds
against a guess.
