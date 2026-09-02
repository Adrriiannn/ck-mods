using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// An instrument a player can hold and play, and a sheet of music written for the six of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper has six instruments — harp, flute, cello, ocarina, drumkit, piano — and a music
    /// sheet carries a separate recording for each, so the same tune played on the cello and the
    /// flute is genuinely two pieces of audio. A custom instrument that plays nothing, or a custom
    /// sheet that only works on one instrument, is the failure this prevents.
    /// </para>
    /// <para>
    /// AN INSTRUMENT NEEDS TWO SOUNDS, not one: the note, and the note an octave up. The game picks
    /// between them by which key the player pressed, so an instrument given only the base note plays
    /// its whole upper half at the wrong pitch.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionInstrumentTemplate
    {
        [Header("As an instrument")]
        [Tooltip("A player can hold and play this.")]
        [SerializeField] private bool isAnInstrument;

        [Tooltip("Which of the game's six it behaves as. Its playing style comes from this.")]
        [SerializeField] private DimensionInstrumentKind kind = DimensionInstrumentKind.Harp;

        [Tooltip("The sound of one note, by its name. The game's own names are in DimensionSoundNames.")]
        [DimensionSoundName]
        [SerializeField] private string noteSound = string.Empty;

        [Tooltip("The same note an octave up. Without this the upper half plays at the wrong pitch.")]
        [DimensionSoundName]
        [SerializeField] private string noteSoundOctaveUp = string.Empty;

        [Tooltip("How far its lowest key sits from C5, in semitones. 0 starts at C5.")]
        [SerializeField] private int keysFromC5;

        [Header("As a music sheet")]
        [Tooltip("This is a sheet of music rather than an instrument.")]
        [SerializeField] private bool isAMusicSheet;

        [Tooltip("The recording heard when it is played on the harp.")]
        [DimensionSoundName]
        [SerializeField] private string harpTrack = string.Empty;

        [Tooltip("On the flute.")]
        [DimensionSoundName]
        [SerializeField] private string fluteTrack = string.Empty;

        [Tooltip("On the cello.")]
        [DimensionSoundName]
        [SerializeField] private string celloTrack = string.Empty;

        [Tooltip("On the ocarina.")]
        [DimensionSoundName]
        [SerializeField] private string ocarinaTrack = string.Empty;

        [Tooltip("On the drumkit.")]
        [DimensionSoundName]
        [SerializeField] private string drumkitTrack = string.Empty;

        [Tooltip("On the piano.")]
        [DimensionSoundName]
        [SerializeField] private string pianoTrack = string.Empty;

        public bool IsAnInstrument { get { return isAnInstrument; } }

        public DimensionInstrumentKind Kind { get { return kind; } }

        public string NoteSoundName { get { return noteSound ?? string.Empty; } }

        public int NoteSound { get { return DimensionSoundNames.Hash(NoteSoundName); } }

        public string NoteSoundOctaveUpName { get { return noteSoundOctaveUp ?? string.Empty; } }

        public int NoteSoundOctaveUp { get { return DimensionSoundNames.Hash(NoteSoundOctaveUpName); } }

        public int KeysFromC5 { get { return keysFromC5; } }

        public bool IsAMusicSheet { get { return isAMusicSheet; } }

        public string HarpTrackName { get { return harpTrack ?? string.Empty; } }

        public int HarpTrack { get { return DimensionSoundNames.Hash(HarpTrackName); } }

        public string FluteTrackName { get { return fluteTrack ?? string.Empty; } }

        public int FluteTrack { get { return DimensionSoundNames.Hash(FluteTrackName); } }

        public string CelloTrackName { get { return celloTrack ?? string.Empty; } }

        public int CelloTrack { get { return DimensionSoundNames.Hash(CelloTrackName); } }

        public string OcarinaTrackName { get { return ocarinaTrack ?? string.Empty; } }

        public int OcarinaTrack { get { return DimensionSoundNames.Hash(OcarinaTrackName); } }

        public string DrumkitTrackName { get { return drumkitTrack ?? string.Empty; } }

        public int DrumkitTrack { get { return DimensionSoundNames.Hash(DrumkitTrackName); } }

        public string PianoTrackName { get { return pianoTrack ?? string.Empty; } }

        public int PianoTrack { get { return DimensionSoundNames.Hash(PianoTrackName); } }

        /// <summary>Whether it is an instrument with no note to play.</summary>
        public bool PlaysNothing
        {
            get { return isAnInstrument && NoteSound == 0; }
        }

        /// <summary>Whether it has a note but no octave, so half its keys are wrong.</summary>
        public bool HasNoOctave
        {
            get { return isAnInstrument && NoteSound != 0 && NoteSoundOctaveUp == 0; }
        }

        /// <summary>How many of the six instruments this sheet has a recording for.</summary>
        public int TracksWritten
        {
            get
            {
                int written = 0;
                if (HarpTrack != 0) { written++; }
                if (FluteTrack != 0) { written++; }
                if (CelloTrack != 0) { written++; }
                if (OcarinaTrack != 0) { written++; }
                if (DrumkitTrack != 0) { written++; }
                if (PianoTrack != 0) { written++; }
                return written;
            }
        }

        /// <summary>Whether it is a sheet with nothing recorded on it.</summary>
        public bool SheetIsBlank
        {
            get { return isAMusicSheet && TracksWritten == 0; }
        }

        /// <summary>
        /// Whether the sheet only covers some instruments.
        /// </summary>
        /// <remarks>
        /// Not an error — a piece written only for the drumkit is a real thing to make. Worth saying
        /// because a player who picks it up holding the wrong instrument hears silence and has no
        /// way to know the sheet was never written for it.
        /// </remarks>
        public bool SheetIsIncomplete
        {
            get { return isAMusicSheet && TracksWritten > 0 && TracksWritten < 6; }
        }
    }

    /// <summary>
    /// Which instrument something behaves as. Core Keeper's <c>InstrumentType</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionInstrumentKind
    {
        /// <summary>Not an instrument. <c>None</c>.</summary>
        NotAnInstrument = 0,

        Harp = 1,
        Flute = 2,
        Cello = 3,
        Ocarina = 4,
        Drumkit = 5,
        Piano = 6
    }
}
