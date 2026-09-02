using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The shape and timing of a creature's melee swing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The half of <c>MeleeAttackStateAuthoring</c> that was out of reach. The combat template
    /// already asks the plain questions — how much damage, how far, how often. These are the ones
    /// that decide how the swing <i>feels</i>: when in the animation the damage lands, how hard the
    /// creature lunges into it, and whether it can turn while swinging.
    /// </para>
    /// <para>
    /// MEASURED ACROSS THE GAME'S MELEE CREATURES: <c>lockOrientationDuringHit</c> 31,
    /// <c>skipVisibilityCheck</c> 28, <c>canOnlyAttackEnemiesAndPlayer</c> 12,
    /// <c>lockOrientationDuringAnticipation</c> 7, <c>hitInDiscreteDirections</c> 5.
    /// <c>durationBeforeDamageDeal</c> is 0.05 on 29 of them — a real and consistent dial rather
    /// than a default nobody touched. <c>moveForceForward</c> clusters at 15 and 13, which is the
    /// lunge every vanilla melee creature does and ours never did.
    /// </para>
    /// <para>
    /// The custom hitbox (<c>hitBoxHalfLength</c>, <c>hitBoxHalfWidth</c>) is zero on 45 of 50 —
    /// the game derives a sensible box when they are — so it is offered as an override rather than
    /// as a number to fill in.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionMeleeShapeTemplate
    {
        [Header("Damage")]
        [Tooltip("How hard it hits for its tier. 1 is the baseline; vanilla runs 0.6 to 1.5.")]
        [Min(0f)]
        [SerializeField] private float hitsThisHardForItsTier = 1f;

        [Tooltip("How much terrain a swing breaks for its tier. 999 is the vanilla 'breaks anything' idiom.")]
        [Min(0f)]
        [SerializeField] private float breaksTerrainThisHardForItsTier = 1f;

        [Header("Timing")]
        [Tooltip("How far into the swing the damage lands, in seconds. 0.05 is what most of the game uses.")]
        [Min(0f)]
        [SerializeField] private float damageLandsAfter = 0.05f;

        [Tooltip("How hard it lunges forward as it swings. Vanilla melee creatures use 13 to 15.")]
        [Min(0f)]
        [SerializeField] private float lungeForce;

        [Tooltip("It always lunges at full force rather than scaling with distance.")]
        [SerializeField] private bool alwaysLungesAtFullForce;

        [Header("Where it can turn")]
        [Tooltip("It cannot turn once the swing lands. What most vanilla melee creatures do.")]
        [SerializeField] private bool cannotTurnDuringTheHit = true;

        [Tooltip("It cannot turn during the wind-up either.")]
        [SerializeField] private bool cannotTurnDuringTheWindUp;

        [Tooltip("It snaps to the four compass directions rather than swinging at any angle.")]
        [SerializeField] private bool onlySwingsInFourDirections;

        [Header("What it will hit")]
        [Tooltip("It swings even when it cannot see its target.")]
        [SerializeField] private bool swingsAtWhatItCannotSee;

        [Tooltip("It only ever hits enemies and players, never scenery or other creatures.")]
        [SerializeField] private bool onlyHitsEnemiesAndPlayers;

        [Tooltip("It gives up on a player it cannot reach after this long. 0 means it never does.")]
        [Min(0f)]
        [SerializeField] private float givesUpOnAPlayerAfter;

        [Tooltip("Its swing also hits low things like traps and pressure plates. 8 of 70 vanilla melee attackers do.")]
        [SerializeField] private bool hitsLowObstacles;

        [Tooltip("It ignores the per-hit damage cap. 4 of 71 do — the very heavy hitters.")]
        [SerializeField] private bool ignoresTheDamageCap;

        [Header("Custom hitbox")]
        [Tooltip("Override the swing's reach. 0 lets the game work it out, which 45 of 50 do.")]
        [Min(0f)]
        [SerializeField] private float hitboxHalfLength;

        [Tooltip("Override the swing's width.")]
        [Min(0f)]
        [SerializeField] private float hitboxHalfWidth;

        [Tooltip("Nudges where the hitbox sits relative to the creature.")]
        [SerializeField] private Vector3 hitboxOffset = Vector3.zero;

        [Tooltip("What it leaves where its swing broke terrain. Empty leaves nothing.")]
        [SerializeField] private string spawnsOnBrokenTilesId = string.Empty;

        /// <summary>
        /// The multiplier the game applies to the tier curve to get real melee damage.
        /// </summary>
        /// <remarks>
        /// THIS IS THE ONLY MELEE DAMAGE DIAL THAT SURVIVES A TIER. <c>MeleeAttackStateConverter</c>
        /// throws the authored <c>meleeDamage</c> away outright when the object carries a tier and
        /// replaces it with <c>LevelToDamage(level, meleeDamageMultiplier)</c>. A creature given a
        /// tier and a flat damage number gets the number nobody asked for; the multiplier is how it
        /// is actually made to hit softer or harder than its tier baseline.
        /// </remarks>
        public float HitsThisHardForItsTier
        {
            get { return hitsThisHardForItsTier < 0f ? 0f : hitsThisHardForItsTier; }
        }

        public float BreaksTerrainThisHardForItsTier
        {
            get
            {
                return breaksTerrainThisHardForItsTier < 0f ? 0f : breaksTerrainThisHardForItsTier;
            }
        }

        /// <summary>Whether the multiplier silently zeroes its damage on a tiered creature.</summary>
        /// <remarks>
        /// Worth saying out loud because it is invisible: on a tiered creature the flat damage is
        /// discarded, so a zero multiplier is a creature that swings and never hurts anything, with
        /// a perfectly reasonable-looking damage number sitting next to it.
        /// </remarks>
        public bool MultipliedDownToNoDamage
        {
            get { return hitsThisHardForItsTier <= 0f; }
        }

        public float DamageLandsAfter
        {
            get { return damageLandsAfter < 0f ? 0f : damageLandsAfter; }
        }

        public float LungeForce
        {
            get { return lungeForce < 0f ? 0f : lungeForce; }
        }

        public bool AlwaysLungesAtFullForce
        {
            get { return alwaysLungesAtFullForce; }
        }

        public bool CannotTurnDuringTheHit
        {
            get { return cannotTurnDuringTheHit; }
        }

        public bool CannotTurnDuringTheWindUp
        {
            get { return cannotTurnDuringTheWindUp; }
        }

        public bool OnlySwingsInFourDirections
        {
            get { return onlySwingsInFourDirections; }
        }

        public bool SwingsAtWhatItCannotSee
        {
            get { return swingsAtWhatItCannotSee; }
        }

        public bool OnlyHitsEnemiesAndPlayers
        {
            get { return onlyHitsEnemiesAndPlayers; }
        }

        public float GivesUpOnAPlayerAfter
        {
            get { return givesUpOnAPlayerAfter < 0f ? 0f : givesUpOnAPlayerAfter; }
        }

        public bool HitsLowObstacles
        {
            get { return hitsLowObstacles; }
        }

        public bool IgnoresTheDamageCap
        {
            get { return ignoresTheDamageCap; }
        }

        public float HitboxHalfLength
        {
            get { return hitboxHalfLength < 0f ? 0f : hitboxHalfLength; }
        }

        public float HitboxHalfWidth
        {
            get { return hitboxHalfWidth < 0f ? 0f : hitboxHalfWidth; }
        }

        /// <summary>Whether the hitbox was overridden rather than left to the game.</summary>
        public bool OverridesTheHitbox
        {
            get { return HitboxHalfLength > 0f || HitboxHalfWidth > 0f; }
        }

        public Vector3 HitboxOffset
        {
            get { return hitboxOffset; }
        }

        public string SpawnsOnBrokenTilesId
        {
            get { return spawnsOnBrokenTilesId ?? string.Empty; }
        }

        /// <summary>Whether only one half of the hitbox override was given.</summary>
        /// <remarks>
        /// The two are read together. Setting a length and leaving the width at zero gives a swing
        /// with reach and no width, which connects with nothing.
        /// </remarks>
        public bool HitboxIsHalfSpecified
        {
            get
            {
                return (HitboxHalfLength > 0f) != (HitboxHalfWidth > 0f);
            }
        }
    }
}
