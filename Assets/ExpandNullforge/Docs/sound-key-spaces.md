# Three kinds of string address sound, and they do not mix

Every sound field in this framework takes a string. Three different sorts of string, and pasting one
into another's box is the failure that has cost the most time here: it generates cleanly, it looks
right in the inspector, and in the game it plays nothing at all.

| What it is | Looks like | How the game resolves it | Where it is asked for |
|---|---|---|---|
| **A sound name** | `hydraBossBiteAnticipation` | `Animator.StringToHash` gives the number, `SfxTable.GetSfxInfo` gives the sound | Every creature, attack, plant, vehicle, instrument and impact field — 25 of them |
| **An audio file address** | `assets/audio/storm.ogg` | Addressables loads the file at that address | Biome and area ambience, the four portal sounds, music tracks |
| **A puff name** | `Leaves` | `Enum.TryParse<PuffID>` | A plant's ripening burst, an explosion's burst |

## Why they cannot be swapped

A **sound name** is never stored. What is stored is a 32-bit hash of it, and *every* string hashes to
something — so `assets/audio/storm.ogg` typed into a sound-name field produces a perfectly valid
number that no sound in the game answers to. Nothing throws. Nothing is logged. The creature swings
in silence.

An **audio file address** is a real path into the game's own bundles. Browsing them needs an
installed copy of the game, which is why the Sound Library asks for the game folder and the sound
name list does not.

A **puff name** is an enum member, and the only one of the three the game itself rejects: a name that
is not a `PuffID` falls back to leaves.

## What the framework does about it

- `DimensionSoundNames.All` holds all 1,413 names Core Keeper ships a sound under, harvested by
  `Docs/harvest-sound-names.sh` from the shipped sound elements and the game's own `SfxTableID`.
  The **Browse** button beside every sound-name field lists them.
- `DimensionSoundNames.WarnIfUnknown` says so at generate time when a name is neither one of those
  1,413 nor blank — and says something different when the value has a slash or a file extension in
  it, because that is the wrong sort of string rather than a misspelling.
- It is always a warning and never a refusal. A mod that ships a sound of its own gives it a name
  this list cannot know, and that name has to stay typeable.

## What cannot be done

**A sound name cannot be previewed in the editor.** The two spaces do not map to each other: from a
name there is no way back to the file, so the name list can offer and check and never play. The
Sound Library, which browses files, can play everything it lists and writes addresses rather than
names.
