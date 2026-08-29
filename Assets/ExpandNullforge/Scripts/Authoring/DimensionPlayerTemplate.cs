using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The numbers a mod overrides on the one player the game owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A MOD CANNOT SHIP A PLAYER, ONLY CHANGE THE GAME'S. There is exactly one player object and
    /// the game inserts it before any mod is read, so a second one loses the race and is thrown
    /// away without a word. What does work is reaching the game's own player just before a loading
    /// world reads it and writing over the numbers on it — which is what this does, and why every
    /// answer here is an override rather than a definition.
    /// </para>
    /// <para>
    /// ONLY THE HONEST NUMBERS ARE HERE. What a brand-new character looks like was tried and
    /// dropped: the look on the player object is a placeholder the character creator overwrites
    /// before anyone ever sees it, so setting it would be a tickbox that quietly does nothing. The
    /// same goes for what runs while a thing is in hand — those are pieces of the game's own code,
    /// not values, and there is no way to name one without writing code.
    /// </para>
    /// <para>
    /// THE OVERRIDE IS PUT BACK AFTER THE WORLD HAS READ IT. The game's player object stays loaded
    /// for the whole session, so leaving it edited would carry these numbers into a vanilla world
    /// opened afterwards in the same sitting.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionPlayerTemplate
    {
        [Header("Does this override the game's player?")]
        [Tooltip("Turn on to write over the numbers below. Off leaves the player exactly as the game has them.")]
        [SerializeField] private bool overridesTheGamesPlayer;

        [Header("Turning")]
        [Tooltip("How long they carry on facing the old way after turning, in seconds.")]
        [Min(0f)]
        [SerializeField] private float turningCatchesUpAfter = 0.0666667f;

        [Header("Riding")]
        [Tooltip("How much a vehicle slides sideways as it turns, over the length of the turn.")]
        [SerializeField] private AnimationCurve vehicleDrift = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Aiming")]
        [Tooltip("Where what they are aiming at sits, relative to them.")]
        [SerializeField] private Vector3 aimSitsAt;

        public bool OverridesTheGamesPlayer { get { return overridesTheGamesPlayer; } }

        public float TurningCatchesUpAfter
        {
            get { return turningCatchesUpAfter < 0f ? 0f : turningCatchesUpAfter; }
        }

        public AnimationCurve VehicleDrift { get { return vehicleDrift; } }

        public Vector3 AimSitsAt { get { return aimSitsAt; } }

        /// <summary>
        /// A drift curve with nothing in it. The game reads the curve at every moment of a turn, so
        /// an empty one would answer zero forever and vehicles would stop drifting entirely.
        /// </summary>
        public bool DriftCurveIsEmpty
        {
            get
            {
                return overridesTheGamesPlayer &&
                       (vehicleDrift == null || vehicleDrift.length == 0);
            }
        }
    }
}
