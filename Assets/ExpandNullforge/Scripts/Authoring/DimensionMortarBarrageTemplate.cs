using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A mortar barrage: how many shells, where they land, how long they hang in the air, and what
    /// makes the creature stop firing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ShootMortarProjectileStateAuthoring</c> is thirty-five fields and six were reachable. The
    /// generic ability questions gave a creature that lobbed one shell on a timer, which is not what
    /// any vanilla mortar user actually does.
    /// </para>
    /// <para>
    /// THE LINE BARRAGE IS THE BIG ONE. <c>lineFromShooterToTarget</c> is set on 7 of the 15 vanilla
    /// mortar users and <c>lineBendTowardTarget</c> on 6: the shells land in a line walking out from
    /// the creature towards you, optionally curving to follow. That is the attack you dodge sideways
    /// from, and it was completely unreachable — every custom mortar boss dropped shells in a random
    /// scatter instead.
    /// </para>
    /// <para>
    /// FLIGHT TIMES, MEASURED: <c>airTime</c> is set on 10 of 15 (0.45, 0.6), <c>explodeTime</c> on 8
    /// (2, 0.2), <c>goDownTime</c> on 5 (0.5, 1), <c>goUpTime</c> on 2 (0.5, 0.3). Together they are
    /// the arc, and they are what gives a player time to read the shadow and move.
    /// </para>
    /// <para>
    /// Every field on the component is offered. <c>keepShootingUntilTakingDamageXTimes</c> has no
    /// vanilla users, which means no measured value to suggest rather than no effect — it is a
    /// perfectly good way to build a boss you interrupt by hitting it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionMortarBarrageTemplate
    {
        [Header("The barrage")]
        [Tooltip("Fewest shells in a barrage.")]
        [Min(0)]
        [SerializeField] private int fewestShells = 1;

        [Tooltip("Most shells in a barrage.")]
        [Min(0)]
        [SerializeField] private int mostShells = 1;

        [Tooltip("How many go up at once in each wave.")]
        [Min(0)]
        [SerializeField] private int shellsPerWave = 1;

        [Tooltip("Multiplier on that wave size, for scaling a barrage up without retyping it.")]
        [Min(0f)]
        [SerializeField] private float shellsPerWaveMultiplier = 1f;

        [Tooltip("Gap between shells going up.")]
        [Min(0f)]
        [SerializeField] private float timeBetweenShells;

        [Tooltip("Two shells never land on the same spot.")]
        [SerializeField] private bool neverOverlapsShots;

        [Header("Where they land")]
        [Tooltip("They land in a line walking out from the creature towards its target. 7 of 15 do.")]
        [SerializeField] private bool landsInALineTowardsTheTarget;

        [Tooltip("That line curves to follow the target rather than staying straight.")]
        [SerializeField] private bool theLineBendsToFollow;

        [Tooltip("How far the line reaches, as a multiplier of the distance to the target.")]
        [Min(0f)]
        [SerializeField] private float lineLengthMultiplier;

        [Tooltip("Closest a scattered shell lands to the target.")]
        [Min(0f)]
        [SerializeField] private float scatterFrom;

        [Tooltip("Furthest a scattered shell lands from the target.")]
        [Min(0f)]
        [SerializeField] private float scatterTo;

        [Tooltip("It drops the barrage on itself rather than on a target.")]
        [SerializeField] private bool shellsLandOnItself;

        [Header("The arc")]
        [Tooltip("How long a shell takes to rise. Vanilla uses 0.3 to 0.5.")]
        [Min(0f)]
        [SerializeField] private float riseTime;

        [Tooltip("How long it hangs at the top. 10 of 15 set this, at 0.45 or 0.6.")]
        [Min(0f)]
        [SerializeField] private float hangTime;

        [Tooltip("How long it takes to come down — the window a player has to read the shadow.")]
        [Min(0f)]
        [SerializeField] private float fallTime;

        [Tooltip("How long it sits on the ground before going off. Vanilla uses 0.2 or 2.")]
        [Min(0f)]
        [SerializeField] private float fuseTime;

        [Header("When it fires")]
        [Tooltip("Closest it will fire from.")]
        [Min(0f)]
        [SerializeField] private float firesFromAtLeast;

        // The furthest it fires from is NOT asked here. The ability already asks "how far away does
        // this work", in plainer words, and writes maxDistanceToTargetToShoot from it. Two controls
        // over one field is worse than one control in the wrong place.

        [Tooltip("It only fires once its health is at or below this share. 1 means always.")]
        [Range(0f, 1f)]
        [SerializeField] private float onlyFiresBelowHealth = 1f;

        [Tooltip("It keeps firing until it has been hit this many times. 0 means the count is unused.")]
        [Min(0)]
        [SerializeField] private int keepsFiringUntilHitThisManyTimes;

        [Tooltip("It only fires once it is actually in combat.")]
        [SerializeField] private bool onlyFiresWhenInCombat;

        [Tooltip("It fires even when it cannot see its target.")]
        [SerializeField] private bool firesAtWhatItCannotSee;

        [Tooltip("Firing does not interrupt its other attacks.")]
        [SerializeField] private bool doesNotInterruptItsOtherAttacks;

        [Header("Damage")]
        [Tooltip("How hard a shell hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float hitsThisHardForItsTier = 1f;

        [Tooltip("How much terrain a shell breaks for its tier.")]
        [Min(0f)]
        [SerializeField] private float breaksTerrainThisHardForItsTier = 1f;

        [Tooltip("Its shells break terrain where they land.")]
        [SerializeField] private bool shellsBreakTerrain;

        [Tooltip("Flat terrain damage, used only when the creature has no tier to scale from.")]
        [Min(0)]
        [SerializeField] private int flatTerrainDamage;

        [Header("Animation")]
        [Tooltip("An animation name to play instead of the usual one.")]
        [SerializeField] private string animationName = string.Empty;

        [Tooltip("It plays its firing animation for each shell.")]
        [SerializeField] private bool playsTheFiringAnimation = true;

        public int FewestShells { get { return fewestShells < 0 ? 0 : fewestShells; } }

        /// <summary>The upper end of the barrage size, never below the lower end.</summary>
        public int MostShells
        {
            get
            {
                int most = mostShells < 0 ? 0 : mostShells;
                return most < FewestShells ? FewestShells : most;
            }
        }

        public int ShellsPerWave { get { return shellsPerWave < 0 ? 0 : shellsPerWave; } }

        public float ShellsPerWaveMultiplier
        {
            get { return shellsPerWaveMultiplier < 0f ? 0f : shellsPerWaveMultiplier; }
        }

        public float TimeBetweenShells
        {
            get { return timeBetweenShells < 0f ? 0f : timeBetweenShells; }
        }

        public bool NeverOverlapsShots { get { return neverOverlapsShots; } }

        public bool LandsInALineTowardsTheTarget { get { return landsInALineTowardsTheTarget; } }

        public bool TheLineBendsToFollow { get { return theLineBendsToFollow; } }

        public float LineLengthMultiplier
        {
            get { return lineLengthMultiplier < 0f ? 0f : lineLengthMultiplier; }
        }

        public float ScatterFrom { get { return scatterFrom < 0f ? 0f : scatterFrom; } }

        /// <summary>The outer scatter radius, never inside the inner one.</summary>
        public float ScatterTo
        {
            get
            {
                float outer = scatterTo < 0f ? 0f : scatterTo;
                return outer < ScatterFrom ? ScatterFrom : outer;
            }
        }

        public bool ShellsLandOnItself { get { return shellsLandOnItself; } }

        public float RiseTime { get { return riseTime < 0f ? 0f : riseTime; } }

        public float HangTime { get { return hangTime < 0f ? 0f : hangTime; } }

        public float FallTime { get { return fallTime < 0f ? 0f : fallTime; } }

        public float FuseTime { get { return fuseTime < 0f ? 0f : fuseTime; } }

        public float FiresFromAtLeast
        {
            get { return firesFromAtLeast < 0f ? 0f : firesFromAtLeast; }
        }

        public float OnlyFiresBelowHealth
        {
            get
            {
                if (onlyFiresBelowHealth < 0f)
                {
                    return 0f;
                }

                return onlyFiresBelowHealth > 1f ? 1f : onlyFiresBelowHealth;
            }
        }

        public int KeepsFiringUntilHitThisManyTimes
        {
            get
            {
                return keepsFiringUntilHitThisManyTimes < 0 ? 0 : keepsFiringUntilHitThisManyTimes;
            }
        }

        public bool OnlyFiresWhenInCombat { get { return onlyFiresWhenInCombat; } }

        public bool FiresAtWhatItCannotSee { get { return firesAtWhatItCannotSee; } }

        public bool DoesNotInterruptItsOtherAttacks
        {
            get { return doesNotInterruptItsOtherAttacks; }
        }

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

        public bool ShellsBreakTerrain { get { return shellsBreakTerrain; } }

        public int FlatTerrainDamage
        {
            get { return flatTerrainDamage < 0 ? 0 : flatTerrainDamage; }
        }

        public string AnimationName { get { return animationName ?? string.Empty; } }

        public bool PlaysTheFiringAnimation { get { return playsTheFiringAnimation; } }

        /// <summary>Whether the line settings were given on a barrage that does not fire in a line.</summary>
        public bool LineSettingsWillBeIgnored
        {
            get
            {
                return !landsInALineTowardsTheTarget
                    && (theLineBendsToFollow || lineLengthMultiplier > 0f);
            }
        }

        /// <summary>Whether a line barrage was asked for without a length to walk along.</summary>
        /// <remarks>
        /// Worth saying because it looks configured: the shells fire in a line whose length
        /// multiplier is zero, so every one of them lands on the same spot.
        /// </remarks>
        public bool LineHasNoLength
        {
            get { return landsInALineTowardsTheTarget && lineLengthMultiplier <= 0f; }
        }
    }
}
