using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Music that plays when a player comes near an object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>MusicAreaAuthoring</c>, on 36 vanilla prefabs. It is how a boss arena changes the music
    /// without the biome changing, and it sits beside the biome music the framework already ships:
    /// that one is "what this whole place sounds like", this one is "what standing near this thing
    /// sounds like".
    /// </para>
    /// <para>
    /// THE TWO DISTANCES ARE THE MECHANISM, and they are not a range. The music starts when a
    /// player comes within the first and stops when they pass the second, so the stop distance is
    /// the larger of the two. Setting them the other way round means a player walking up crosses
    /// the stop line before the start line and the music never begins.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionMusicAreaTemplate
    {
        [Tooltip("Which music. A MusicRosterType name — BOSS, MYSTERY, HOME_BASE and so on.")]
        [SerializeField] private string musicId = string.Empty;

        [Tooltip("The music starts when a player is this close.")]
        [Min(0f)]
        [SerializeField] private float startsWithin = 20f;

        [Tooltip("It stops when they get this far away. Larger than the start distance.")]
        [Min(0f)]
        [SerializeField] private float stopsBeyond = 30f;

        [Tooltip("How long it takes to fade in and out, in seconds.")]
        [Min(0f)]
        [SerializeField] private float fadeSeconds = 0.5f;

        [Tooltip("Which music wins when two areas overlap. Higher wins.")]
        [SerializeField] private int priority;

        [Tooltip("It only plays while the thing is fighting.")]
        [SerializeField] private bool onlyInCombat;

        [Tooltip("Different music once the fighting starts. Empty means the same music throughout.")]
        [SerializeField] private string combatMusicId = string.Empty;

        [Tooltip("It goes quiet while the creature is in a particular state.")]
        [SerializeField] private bool goesQuietInAState;

        [Tooltip("Which state that is, by the game's own name.")]
        [SerializeField] private string quietInThisState = string.Empty;

        [Tooltip("Your own tracks, when the music name above is not one of the game's rosters. " +
                 "Clip keys, the way sounds are named — the name becomes your own roster made " +
                 "of these.")]
        [SerializeField] private string[] customTrackKeys = new string[0];

        [Tooltip("Shortest wait between plays.")]
        [Min(0f)]
        [SerializeField] private float minWaitBetweenPlays;

        [Tooltip("Longest wait between plays.")]
        [Min(0f)]
        [SerializeField] private float maxWaitBetweenPlays;

        public bool GoesQuietInAState { get { return goesQuietInAState; } }

        public string QuietInThisState { get { return quietInThisState ?? string.Empty; } }

        public float MinWaitBetweenPlays
        {
            get { return minWaitBetweenPlays < 0f ? 0f : minWaitBetweenPlays; }
        }

        public float MaxWaitBetweenPlays
        {
            get
            {
                float longest = maxWaitBetweenPlays < 0f ? 0f : maxWaitBetweenPlays;
                return longest < MinWaitBetweenPlays ? MinWaitBetweenPlays : longest;
            }
        }

        public string MusicId
        {
            get { return musicId ?? string.Empty; }
        }

        /// <summary>The mod's own tracks, when the name is not a vanilla roster.</summary>
        public string[] CustomTrackKeys
        {
            get { return customTrackKeys ?? new string[0]; }
        }

        /// <summary>
        /// The numbers Core Keeper's own first boss ships with: the BOSS roster, striking up at
        /// ten tiles, silent past twenty-five, and only while the fight is on.
        /// </summary>
        public static DimensionMusicAreaTemplate BossFightDefaults()
        {
            DimensionMusicAreaTemplate template = new DimensionMusicAreaTemplate();
            template.musicId = "BOSS";
            template.startsWithin = 10f;
            template.stopsBeyond = 25f;
            template.onlyInCombat = true;
            return template;
        }

        public bool PlaysMusic
        {
            get { return !string.IsNullOrEmpty(MusicId); }
        }

        public float StartsWithin
        {
            get { return startsWithin < 0f ? 0f : startsWithin; }
        }

        public float StopsBeyond
        {
            get { return stopsBeyond < 0f ? 0f : stopsBeyond; }
        }

        public float FadeSeconds
        {
            get { return fadeSeconds < 0f ? 0f : fadeSeconds; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool OnlyInCombat
        {
            get { return onlyInCombat; }
        }

        public string CombatMusicId
        {
            get { return combatMusicId ?? string.Empty; }
        }

        public bool HasCombatMusic
        {
            get { return PlaysMusic && !string.IsNullOrEmpty(CombatMusicId); }
        }

        /// <summary>Whether the two distances are the wrong way round.</summary>
        /// <remarks>
        /// A player walking towards it crosses the stop line before the start line, so the music
        /// never begins. Nothing errors; the object simply seems to have no music.
        /// </remarks>
        public bool CanNeverStart
        {
            get { return PlaysMusic && StartsWithin >= StopsBeyond; }
        }
    }
}
