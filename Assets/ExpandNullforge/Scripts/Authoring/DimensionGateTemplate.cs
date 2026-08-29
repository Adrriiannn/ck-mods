using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What makes this object open — the game's own diegetic gates, one tick each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper has exactly four ways a sealed thing opens, and every locked door, hidden
    /// passage and singing wall in the game is one of them: an item put INSIDE it (that one
    /// lives on containers — a locked chest is a one-slot container that accepts a key), a
    /// player standing near while HOLDING something, a particular object PLACED nearby, and a
    /// MELODY played within earshot. This template authors the last three; each is its own
    /// vanilla component, and they stack.
    /// </para>
    /// <para>
    /// "Opening" means changing to another look of the same object — the look the author drew
    /// as open — optionally dropping its collider so the player walks through. The nearby-object
    /// gate can close again when the object is taken away, unless told to stay open; the
    /// held-item gate closes when the player walks off; the melody gate can transform the
    /// object outright.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionGateTemplate
    {
        [Tooltip("It opens while a player stands near holding this item.")]
        [SerializeField] private string opensWhenHolding = string.Empty;

        [Tooltip("How near, in tiles.")]
        [Min(0.5f)]
        [SerializeField] private float holdingReach = 3f;

        [Tooltip("Which look of it is the open one, for the holding gate.")]
        [Min(0)]
        [SerializeField] private int holdingOpenLook = 1;

        [Tooltip("While open this way, players can walk through it. The game only takes a hitbox " +
                 "away for a gate that opens when something is put inside it, so on a holding " +
                 "gate this is refused with a note and the look still changes.")]
        [SerializeField] private bool holdingOpensTheWay = true;

        [Tooltip("It opens while this object stands nearby.")]
        [SerializeField] private string opensWhenObjectNearby = string.Empty;

        [Tooltip("How near, in tiles.")]
        [Min(0.5f)]
        [SerializeField] private float nearbyReach = 3f;

        [Tooltip("Which look of it is the open one, for the nearby gate.")]
        [Min(0)]
        [SerializeField] private int nearbyOpenLook = 1;

        [Tooltip("Once opened this way it stays open, even if the object is removed.")]
        [SerializeField] private bool nearbyStaysOpen;

        [Tooltip("It answers to melodies played within earshot. Melody names from the game — " +
                 "the ones ocarinas and horns play.")]
        [SerializeField] private string[] opensToMelodies = new string[0];

        [Tooltip("How far it hears, in tiles.")]
        [Min(1f)]
        [SerializeField] private float hearingRange = 10f;

        [Tooltip("Which look the melody changes it to.")]
        [Min(0)]
        [SerializeField] private int melodyOpenLook = 1;

        [Tooltip("The melody turns it into a different object entirely. Empty keeps it itself.")]
        [SerializeField] private string melodyTurnsItInto = string.Empty;

        public string OpensWhenHolding { get { return opensWhenHolding ?? string.Empty; } }

        public float HoldingReach { get { return holdingReach < 0.5f ? 0.5f : holdingReach; } }

        public int HoldingOpenLook { get { return holdingOpenLook < 0 ? 0 : holdingOpenLook; } }

        public bool HoldingOpensTheWay { get { return holdingOpensTheWay; } }

        public string OpensWhenObjectNearby { get { return opensWhenObjectNearby ?? string.Empty; } }

        public float NearbyReach { get { return nearbyReach < 0.5f ? 0.5f : nearbyReach; } }

        public int NearbyOpenLook { get { return nearbyOpenLook < 0 ? 0 : nearbyOpenLook; } }

        public bool NearbyStaysOpen { get { return nearbyStaysOpen; } }

        public string[] OpensToMelodies { get { return opensToMelodies ?? new string[0]; } }

        public float HearingRange { get { return hearingRange < 1f ? 1f : hearingRange; } }

        public int MelodyOpenLook { get { return melodyOpenLook < 0 ? 0 : melodyOpenLook; } }

        public string MelodyTurnsItInto { get { return melodyTurnsItInto ?? string.Empty; } }

        public bool HasAnyGate
        {
            get
            {
                return !string.IsNullOrEmpty(OpensWhenHolding) ||
                       !string.IsNullOrEmpty(OpensWhenObjectNearby) ||
                       OpensToMelodies.Length > 0;
            }
        }
    }
}
