using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Instruments, music areas, and doors that open to a melody.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// Music that plays near an object.
        /// </summary>
        /// <remarks>
        /// The distances are the whole mechanism: it starts when a player comes within one and stops
        /// when they pass the other. A start distance smaller than the stop distance means it can
        /// never begin, which is why it is checked rather than assumed.
        /// </remarks>
        public static void ApplyMusicArea(
            GameObject root,
            DimensionMusicAreaTemplate music,
            System.Action<string> report)
        {
            if (music == null || !music.PlaysMusic)
            {
                RemoveComponentIfPresent<MusicAreaAuthoring>(root);
                return;
            }

            MusicRosterType roster;
            if (!System.Enum.TryParse(music.MusicId, false, out roster))
            {
                // Not a vanilla roster. With authored tracks, the name IS the mod's own cue:
                // its id derives from the name, the runtime registry appends the roster, and
                // the play hook feeds the clips. Without tracks it is just a name the game
                // does not have, and saying so beats silence.
                if (music.CustomTrackKeys.Length > 0)
                {
                    roster = (MusicRosterType)ExpandNullforge.Zones.DimensionMusicRosterIds.For(music.MusicId);
                }
                else
                {
                    if (report != null)
                    {
                        report(
                            "plays '" + music.MusicId + "' nearby, which is not music the game has, " +
                            "and it lists no tracks of its own, so nothing will play. Name a game " +
                            "roster like BOSS, or add track clip keys to make it your own music.");
                    }

                    RemoveComponentIfPresent<MusicAreaAuthoring>(root);
                    return;
                }
            }

            if (music.CanNeverStart && report != null)
            {
                report(
                    "starts its music closer than it stops it, so a player walking up to it passes " +
                    "the stop distance first and the music never begins.");
            }

            MusicAreaAuthoring area = EnsureComponent<MusicAreaAuthoring>(root);
            area.musicRosterType = roster;
            area.startAtDistance = music.StartsWithin;
            area.stopAtDistance = music.StopsBeyond;
            area.fadeTime = music.FadeSeconds;
            area.prio = music.Priority;
            area.minCooldownToPlay = music.MinWaitBetweenPlays;
            area.maxCooldownToPlay = music.MaxWaitBetweenPlays;
            area.deactivateWhenEntityIsInState = music.GoesQuietInAState;

            StateID quietState;
            if (music.GoesQuietInAState &&
                !string.IsNullOrEmpty(music.QuietInThisState) &&
                System.Enum.TryParse(music.QuietInThisState, false, out quietState))
            {
                area.stateToDeactivateIn = quietState;
            }
            else if (music.GoesQuietInAState && report != null)
            {
                area.deactivateWhenEntityIsInState = false;
                report(
                    "goes quiet in state '" + music.QuietInThisState + "', which the game does " +
                    "not have, so its music plays throughout instead.");
            }
            area.activeWhenEntityIsInCombat = music.OnlyInCombat;
            area.playOtherMusicWhenInCombat = music.HasCombatMusic;

            if (music.HasCombatMusic)
            {
                MusicRosterType combat;
                if (System.Enum.TryParse(music.CombatMusicId, false, out combat))
                {
                    area.otherMusicRosterType = combat;
                    area.otherFadeTime = music.FadeSeconds;
                }
                else if (report != null)
                {
                    report(
                        "plays '" + music.CombatMusicId + "' in combat, which is not music the game " +
                        "has, so the ordinary music keeps playing instead.");
                }
            }
        }

        /// <summary>
        /// Makes something an instrument a player can play, or a sheet of music for one.
        /// </summary>
        public static void ApplyInstrument(
            GameObject root,
            DimensionInstrumentTemplate music,
            System.Action<string> report)
        {
            if (root == null || music == null)
            {
                return;
            }

            if (music.IsAnInstrument && !music.PlaysNothing)
            {
                InstrumentAuthoring instrument = EnsureComponent<InstrumentAuthoring>(root);
                instrument.instrumentType = (InstrumentType)(int)music.Kind;
                instrument.noteSound = new SFXTableIDField { value = music.NoteSound };
                instrument.noteSoundOctave = new SFXTableIDField { value = music.NoteSoundOctaveUp };
                instrument.keyOffsetFromC5 = music.KeysFromC5;
            }
            else
            {
                RemoveComponentIfPresent<InstrumentAuthoring>(root);
            }

            if (music.IsAMusicSheet && !music.SheetIsBlank)
            {
                MusicSheetAuthoring sheet = EnsureComponent<MusicSheetAuthoring>(root);
                sheet.harpTrack = new SFXTableIDField { value = music.HarpTrack };
                sheet.fluteTrack = new SFXTableIDField { value = music.FluteTrack };
                sheet.celloTrack = new SFXTableIDField { value = music.CelloTrack };
                sheet.ocarinaTrack = new SFXTableIDField { value = music.OcarinaTrack };
                sheet.drumkitTrack = new SFXTableIDField { value = music.DrumkitTrack };
                sheet.pianoTrack = new SFXTableIDField { value = music.PianoTrack };
            }
            else
            {
                RemoveComponentIfPresent<MusicSheetAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (music.PlaysNothing)
            {
                report("is an instrument with no note sound, so playing it is silent.");
            }

            if (music.HasNoOctave)
            {
                report(
                    "has a note but no octave-up note, so the upper half of its keys will play at " +
                    "the wrong pitch.");
            }

            if (music.SheetIsBlank)
            {
                report("is a music sheet with nothing recorded on it, so it plays silence.");
            }

            if (music.SheetIsIncomplete)
            {
                report(
                    "is a music sheet written for only " + music.TracksWritten + " of the six " +
                    "instruments. A player holding one of the others hears nothing.");
            }
        }

        /// <summary>
        /// The one writer of <c>AffectObjectWhenMelodyPlayedAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// TWO AUTHORING BLOCKS, ONE COMPONENT. A world object can say "this opens to a melody" in
        /// its gate block and "this responds to a melody" in its melody block, and Core Keeper
        /// carries both on the same component, so ONE pass has to write it. Two passes in a row —
        /// the gate writing the listener, then this removing it again whenever the melody block is
        /// empty — leave a door authored to open on a tune never opening, and the whole gate melody
        /// surface dead unless an unrelated block happens to be filled in as well.
        /// </para>
        /// <para>
        /// So the gate's tunes arrive here instead. Both lists are heard; where the two blocks
        /// answer the same question, the melody block wins because it is the more detailed one,
        /// and the clash is reported rather than silently resolved.
        /// </para>
        /// </remarks>
        public static void ApplyMelodyResponse(
            GameObject root,
            DimensionMelodyResponseTemplate melody,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null,
            DimensionGateTemplate gate = null)
        {
            System.Collections.Generic.List<MelodyID> gateTunes =
                new System.Collections.Generic.List<MelodyID>();
            if (root != null && gate != null)
            {
                string[] gateNames = gate.OpensToMelodies;
                for (int i = 0; i < gateNames.Length; i++)
                {
                    MelodyID gateTune;
                    if (!string.IsNullOrEmpty(gateNames[i]) &&
                        System.Enum.TryParse(gateNames[i], false, out gateTune) &&
                        gateTune != MelodyID.None)
                    {
                        gateTunes.Add(gateTune);
                    }
                    else if (!string.IsNullOrEmpty(gateNames[i]) && report != null)
                    {
                        report(
                            "opens to melody '" + gateNames[i] + "', which the game does not " +
                            "have, so that tune does nothing.");
                    }
                }
            }

            if (root == null || ((melody == null || !melody.HasAnySetting) && gateTunes.Count == 0))
            {
                RemoveComponentIfPresent<AffectObjectWhenMelodyPlayedAuthoring>(root);
                return;
            }

            if (melody == null || !melody.HasAnySetting)
            {
                ApplyGateMelodyOnly(root, gate, gateTunes, resolveObject, report);
                return;
            }

            AffectObjectWhenMelodyPlayedAuthoring listener =
                EnsureComponent<AffectObjectWhenMelodyPlayedAuthoring>(root);

            listener.melodyIDList = new System.Collections.Generic.List<MelodyID>();
            string[] names = melody.Melodies;
            for (int i = 0; i < names.Length; i++)
            {
                MelodyID tune;
                if (System.Enum.TryParse(names[i], false, out tune) && tune != MelodyID.None)
                {
                    listener.melodyIDList.Add(tune);
                }
                else if (report != null)
                {
                    report(
                        "listens for melody '" + names[i] + "', which the game does not have, so " +
                        "that one will never reach it.");
                }
            }

            // The gate's tunes join the list rather than replacing it or being replaced by it.
            for (int i = 0; i < gateTunes.Count; i++)
            {
                if (!listener.melodyIDList.Contains(gateTunes[i]))
                {
                    listener.melodyIDList.Add(gateTunes[i]);
                }
            }

            if (gateTunes.Count > 0 && report != null &&
                !string.IsNullOrEmpty(gate.MelodyTurnsItInto) &&
                melody.BecomesADifferentObject)
            {
                report(
                    "opens into '" + gate.MelodyTurnsItInto + "' in its gate settings and turns " +
                    "into '" + melody.BecomesObjectId + "' in its melody settings. An object can " +
                    "only become one thing, so the melody settings are used. Clear one of the two.");
            }

            // THE OTHER FIELD BOTH BLOCKS ANSWER, and it went unsaid. The look a gate opens to and
            // the look a melody response changes to are one field on the component
            // (newVariation), and the melody block's answer is written below whatever the gate
            // asked for. Only the "becomes a different object" clash was reported, so an author who
            // set an open look in the gate block and any variation in the melody block watched the
            // door open to the wrong picture with nothing said.
            if (gateTunes.Count > 0 && report != null &&
                gate.MelodyOpenLook != 0 &&
                gate.MelodyOpenLook != melody.BecomesVariation)
            {
                report(
                    "opens to look " + gate.MelodyOpenLook + " in its gate settings and changes to " +
                    "look " + melody.BecomesVariation + " in its melody settings. An object has " +
                    "one look at a time, so the melody settings are used. Clear one of the two.");
            }

            if (listener.melodyIDList.Count == 0)
            {
                RemoveComponentIfPresent<AffectObjectWhenMelodyPlayedAuthoring>(root);
                if (report != null)
                {
                    report(
                        "listens for melodies the game does not have, so it has been left as an " +
                        "ordinary object rather than one that listens for nothing.");
                }

                return;
            }

            listener.hearRange = melody.HearingRange;
            listener.humCooldown = melody.HumCooldown;
            listener.listening = melody.StartsListening;
            listener.weakenWhenAffected = melody.WeakensWhenItHears;
            listener.newVariation = melody.BecomesVariation;
            listener.removeMelodyListener = melody.OnlyRespondsOnce;
            listener.removeOldColliders = melody.ClearsItsOldColliders;

            listener.changeObjectID = melody.BecomesADifferentObject && !melody.BecomesNothing;
            listener.newObjectId = ObjectID.None;
            if (listener.changeObjectID)
            {
                listener.newObjectId = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(melody.BecomesObjectId);
                if (listener.newObjectId == ObjectID.None &&
                    !(isDeferred != null && isDeferred(melody.BecomesObjectId)))
                {
                    listener.changeObjectID = false;
                    if (report != null)
                    {
                        report(
                            "turns into '" + melody.BecomesObjectId + "' when it hears its melody, " +
                            "which is neither one of this mod's objects nor one the game has, so " +
                            "it only changes its look.");
                    }
                }

                // changeObjectID STAYS TRUE for one of the mod's own. The converter writes
                // AffectObjectWhenMelodyPlayedCD either way, and the link hydration fills the id in
                // at load — but the flag is what makes the game read that id at all.
            }

            LootTableID table;
            if (!string.IsNullOrEmpty(melody.LootTableId) &&
                DimensionEditorLootTables.TryResolve(melody.LootTableId, out table))
            {
                listener.tableLoot = table;
            }
            else if (!string.IsNullOrEmpty(melody.LootTableId) && report != null)
            {
                report(
                    "rolls loot table '" + melody.LootTableId + "' when it hears its melody, which " +
                    "is neither the game's nor this mod's, so it gives nothing from a table.");
            }

            listener.customLoot = new System.Collections.Generic.List<ObjectData>();
            DimensionMelodyReward[] contents = melody.Contents;
            for (int i = 0; i < contents.Length; i++)
            {
                ObjectID held = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(contents[i].ObjectId);
                if (held == ObjectID.None)
                {
                    if (report != null)
                    {
                        // ONE OF THE MOD'S OWN IS NOT A MISTAKE, and must not be reported as one.
                        // What a melody leaves behind is a baked ObjectData list on the prefab and
                        // there is no field for a load-time pass to fill, so the entry really is
                        // lost — but the author needs the reason and the way round, not an
                        // accusation about an item they spelled correctly.
                        bool oursWithNoNumberYet =
                            isDeferred != null && isDeferred(contents[i].ObjectId);
                        report(oursWithNoNumberYet
                            ? "holds '" + contents[i].ObjectId + "' after it changes, one of your " +
                              "own items. What a melody leaves behind is fixed when the game " +
                              "builds its objects, before your items have numbers, so that one " +
                              "cannot go in there. Use one of the game's items, or give it a loot " +
                              "table instead."
                            : "holds '" + contents[i].ObjectId + "' after it changes, which is " +
                              "neither one of this mod's items nor one the game has, so that one " +
                              "is left out. Check the spelling.");
                    }

                    continue;
                }

                listener.customLoot.Add(new ObjectData
                {
                    objectID = held,
                    amount = contents[i].Amount,
                    variation = contents[i].Variation
                });
            }

            if (report == null)
            {
                return;
            }

            if (melody.BecomesNothing)
            {
                report(
                    "is set to become a different object when it hears its melody without naming " +
                    "which, so it only changes its look.");
            }

            if (melody.ObjectChangeWillBeIgnored)
            {
                report(
                    "names an object to become when it hears its melody without being told to " +
                    "become one, so it only changes its look.");
            }

            if (melody.LeavesItsOldCollidersBehind)
            {
                report(
                    "turns into a different object without clearing its old colliders, so an " +
                    "invisible wall will be left standing where it was.");
            }
        }

        /// <summary>
        /// The melody listener for an object whose only melody authoring is its gate block.
        /// </summary>
        /// <remarks>
        /// The gate's own listener, written here so one pass owns the component.
        /// A gate hears its tune, opens to a look, and may become a different object.
        /// </remarks>
        private static void ApplyGateMelodyOnly(
            GameObject root,
            DimensionGateTemplate gate,
            System.Collections.Generic.List<MelodyID> gateTunes,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            AffectObjectWhenMelodyPlayedAuthoring listener =
                EnsureComponent<AffectObjectWhenMelodyPlayedAuthoring>(root);
            listener.melodyIDList = gateTunes;
            listener.listening = true;
            listener.hearRange = gate.HearingRange;
            listener.newVariation = gate.MelodyOpenLook;
            listener.removeOldColliders = true;

            ObjectID becomes = string.IsNullOrEmpty(gate.MelodyTurnsItInto) || resolveObject == null
                ? ObjectID.None
                : resolveObject(gate.MelodyTurnsItInto);
            listener.changeObjectID = becomes != ObjectID.None;
            listener.newObjectId = becomes;
            if (becomes == ObjectID.None && !string.IsNullOrEmpty(gate.MelodyTurnsItInto) &&
                report != null)
            {
                report(
                    "should turn into '" + gate.MelodyTurnsItInto + "' when its melody plays, " +
                    "which is not a known object, so it changes its look but stays itself.");
            }
        }
    }
}
