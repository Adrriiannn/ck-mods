using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The shot pattern a creature fires in: where the projectile leaves it, how it aims, and how a
    /// volley is arranged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The half of <c>RangeAttackStateAuthoring</c> that was out of reach. The combat template
    /// already asks the plain questions — what it fires, how far, how often. These are the ones that
    /// decide what the volley LOOKS like, and they are where every vanilla ranged boss gets its
    /// character. Nine of the component's forty-three fields were written before this.
    /// </para>
    /// <para>
    /// MEASURED ACROSS THE GAME'S 36 RANGED ATTACKERS: <c>spawnAtDistanceInfront</c> 10,
    /// <c>endDuration</c> 9, <c>allowReAimingWhileShooting</c> 9, <c>aimDegreesMax</c> 8,
    /// <c>spreadType</c> 5, then <c>onlyAttackWhenInCombat</c> / <c>spawnDirectionType</c> /
    /// <c>meleeDamageRadiusAtEntity</c> at 4 each.
    /// </para>
    /// <para>
    /// EVERY FIELD ON THE COMPONENT IS OFFERED, including the ones no vanilla prefab sets:
    /// <c>onlyAttackTargetsWeWantToAttack</c>, <c>startSpreadAngleOffset</c>, <c>animPerShot</c>,
    /// and the speed-by-distance trio. Zero vanilla users means there is no measured value to
    /// suggest — it does not mean the field does nothing, and a custom creature is exactly the
    /// place someone would want the thing the base game never built. The census still decides
    /// which fields are offered FIRST and which sit under a later heading.
    /// </para>
    /// <para>
    /// The only things left alone are <c>ceasingToShoot</c>, which is runtime state the game writes
    /// itself, and <c>projectileID</c> / <c>anticipationDuration</c> / the cooldowns, which the
    /// combat template already asks about in plainer words.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionRangedShapeTemplate
    {
        [Tooltip("How long the shooting itself lasts, separate from the wind-up and the recovery.")]
        [Min(0f)]
        [SerializeField] private float howLongItKeepsShooting;

        [Tooltip("It shoots even when it cannot see its target.")]
        [SerializeField] private bool shootsAtWhatItCannotSee;

        [Tooltip("It only shoots things it actually wants to attack, never anything else in the way.")]
        [SerializeField] private bool onlyShootsThingsItWantsToAttack;

        [Tooltip("It starts switched off, for shooting something else turns on later — a boss phase.")]
        [SerializeField] private bool startsSwitchedOff;

        [Tooltip("Which angle the spread pattern begins at, in degrees.")]
        [SerializeField] private float spreadStartsAtDegrees;

        [Header("Shot speed by distance")]
        [Tooltip("Its projectiles travel faster or slower depending how far the target is.")]
        [SerializeField] private bool speedChangesWithDistance;

        [Tooltip("The speed multiplier at the near end, then at the far end.")]
        [SerializeField] private Vector2 speedFromNearToFar = new Vector2(1f, 1f);

        [Tooltip("The near distance, then the far distance, those two multipliers apply at.")]
        [SerializeField] private Vector2 nearAndFarDistance = Vector2.zero;

        [Header("Animation")]
        [Tooltip("An animation name to play instead of the usual one. Leave blank for the usual.")]
        [SerializeField] private string animationName = string.Empty;

        [Tooltip("An animation name played once per shot.")]
        [SerializeField] private string animationPerShot = string.Empty;

        [Header("Damage")]
        [Tooltip("How hard its shots hit for its tier. 1 is the baseline; vanilla runs 0.5 to 1.")]
        [Min(0f)]
        [SerializeField] private float shotsHitThisHardForItsTier = 1f;

        [Tooltip("How fast its projectiles travel, as a multiplier. 12 creatures change this.")]
        [Min(0f)]
        [SerializeField] private float projectileSpeedMultiplier = 1f;

        [Header("Where the shot comes from")]
        [Tooltip("How far in front of it the projectile appears. 10 of 36 set this; 0.3 to 2.")]
        [Min(0f)]
        [SerializeField] private float muzzleDistance;

        [Tooltip("Random variation on that distance, so a volley does not come from one exact point.")]
        [Min(0f)]
        [SerializeField] private float muzzleDistanceVariation;

        [Tooltip("Nudges where the shot appears relative to the creature.")]
        [SerializeField] private Vector3 muzzleOffset = Vector3.zero;

        [Tooltip("It can fire at any angle rather than only along the four compass directions.")]
        [SerializeField] private bool firesAtAnyAngle;

        [Header("How it aims")]
        [Tooltip("How wide a cone it will fire into without turning, in degrees. 8 creatures set this.")]
        [Min(0f)]
        [SerializeField] private float aimConeDegrees;

        [Tooltip("It keeps tracking its target while the shots are going out. 9 creatures do.")]
        [SerializeField] private bool keepsAimingWhileShooting;

        [Tooltip("It locks its aim during the wind-up, so you can step out of the way.")]
        [SerializeField] private bool locksAimDuringTheWindUp;

        [Tooltip("It leads a moving target from this far away. 0 means it always fires where they are.")]
        [Min(0f)]
        [SerializeField] private float startsLeadingTargetsAt;

        [Tooltip("And stops leading beyond this. Read together with the one above.")]
        [Min(0f)]
        [SerializeField] private float stopsLeadingTargetsAt;

        [Header("The volley")]
        [Tooltip("How the shots in a volley are arranged.")]
        [SerializeField] private DimensionShotPattern shotPattern = DimensionShotPattern.AllTheSameWay;

        [Tooltip("The widest the spread ever opens to, in degrees. Only Random uses it.")]
        [Min(0f)]
        [SerializeField] private float widestSpreadDegrees;

        [Tooltip("Each projectile in a volley picks its own target.")]
        [SerializeField] private bool eachShotPicksItsOwnTarget;

        [Tooltip("Which look the projectile uses, when the projectile has more than one.")]
        [Min(0)]
        [SerializeField] private int projectileVariation;

        [Header("What its shots do")]
        [Tooltip("Its projectiles home in on what it fired at.")]
        [SerializeField] private bool shotsFollowTheirTarget;

        [Tooltip("It fires at itself — how the game builds auras and self-buffs out of projectiles.")]
        [SerializeField] private bool firesAtItself;

        [Tooltip("Its shots heal its own side by this share of the damage instead of hurting them.")]
        [Range(0f, 1f)]
        [SerializeField] private float healsItsOwnSideBy;

        [Tooltip("Anything standing right against it also takes melee damage in this radius.")]
        [Min(0f)]
        [SerializeField] private float alsoHurtsThingsTouchingIt;

        [Header("When it will shoot")]
        [Tooltip("It only shoots once it is actually in combat, never as an opener.")]
        [SerializeField] private bool onlyShootsWhenInCombat;

        [Tooltip("Taking a hit interrupts the shot.")]
        [SerializeField] private bool takingAHitInterruptsIt;

        [Tooltip("How long it stands there after the last shot before doing anything else.")]
        [Min(0f)]
        [SerializeField] private float recoveryAfterShooting;

        public float HowLongItKeepsShooting
        {
            get { return howLongItKeepsShooting < 0f ? 0f : howLongItKeepsShooting; }
        }

        public bool ShootsAtWhatItCannotSee
        {
            get { return shootsAtWhatItCannotSee; }
        }

        public bool OnlyShootsThingsItWantsToAttack
        {
            get { return onlyShootsThingsItWantsToAttack; }
        }

        public bool StartsSwitchedOff
        {
            get { return startsSwitchedOff; }
        }

        public float SpreadStartsAtDegrees
        {
            get { return spreadStartsAtDegrees; }
        }

        public bool SpeedChangesWithDistance
        {
            get { return speedChangesWithDistance; }
        }

        public Vector2 SpeedFromNearToFar
        {
            get { return speedFromNearToFar; }
        }

        public Vector2 NearAndFarDistance
        {
            get { return nearAndFarDistance; }
        }

        public string AnimationName
        {
            get { return animationName ?? string.Empty; }
        }

        public string AnimationPerShot
        {
            get { return animationPerShot ?? string.Empty; }
        }

        /// <summary>Whether speed-by-distance was configured without being switched on.</summary>
        public bool SpeedByDistanceWillBeIgnored
        {
            get { return !speedChangesWithDistance && nearAndFarDistance != Vector2.zero; }
        }

        /// <summary>
        /// The multiplier the game applies to the tier curve to get real ranged damage.
        /// </summary>
        /// <remarks>
        /// THE SAME TRAP AS MELEE. <c>RangeAttackStateConverter</c> discards the authored
        /// <c>rangeDamage</c> when the object carries a tier and recomputes it as
        /// <c>LevelToDamage(level, damageMultiplier)</c>. The framework wrote the flat number and
        /// never the multiplier, so a tiered ranged creature's damage was whatever the curve said.
        /// </remarks>
        public float ShotsHitThisHardForItsTier
        {
            get { return shotsHitThisHardForItsTier < 0f ? 0f : shotsHitThisHardForItsTier; }
        }

        public float ProjectileSpeedMultiplier
        {
            get { return projectileSpeedMultiplier < 0f ? 0f : projectileSpeedMultiplier; }
        }

        public float MuzzleDistance
        {
            get { return muzzleDistance < 0f ? 0f : muzzleDistance; }
        }

        public float MuzzleDistanceVariation
        {
            get { return muzzleDistanceVariation < 0f ? 0f : muzzleDistanceVariation; }
        }

        public Vector3 MuzzleOffset
        {
            get { return muzzleOffset; }
        }

        public bool FiresAtAnyAngle
        {
            get { return firesAtAnyAngle; }
        }

        public float AimConeDegrees
        {
            get { return aimConeDegrees < 0f ? 0f : aimConeDegrees; }
        }

        public bool KeepsAimingWhileShooting
        {
            get { return keepsAimingWhileShooting; }
        }

        public bool LocksAimDuringTheWindUp
        {
            get { return locksAimDuringTheWindUp; }
        }

        public float StartsLeadingTargetsAt
        {
            get { return startsLeadingTargetsAt < 0f ? 0f : startsLeadingTargetsAt; }
        }

        /// <summary>The far end of the lead range, never nearer than the near end.</summary>
        public float StopsLeadingTargetsAt
        {
            get
            {
                float far = stopsLeadingTargetsAt < 0f ? 0f : stopsLeadingTargetsAt;
                return far < StartsLeadingTargetsAt ? StartsLeadingTargetsAt : far;
            }
        }

        public DimensionShotPattern ShotPattern
        {
            get { return shotPattern; }
        }

        public float WidestSpreadDegrees
        {
            get { return widestSpreadDegrees < 0f ? 0f : widestSpreadDegrees; }
        }

        public bool EachShotPicksItsOwnTarget
        {
            get { return eachShotPicksItsOwnTarget; }
        }

        public int ProjectileVariation
        {
            get { return projectileVariation < 0 ? 0 : projectileVariation; }
        }

        public bool ShotsFollowTheirTarget
        {
            get { return shotsFollowTheirTarget; }
        }

        public bool FiresAtItself
        {
            get { return firesAtItself; }
        }

        public float HealsItsOwnSideBy
        {
            get
            {
                if (healsItsOwnSideBy < 0f)
                {
                    return 0f;
                }

                return healsItsOwnSideBy > 1f ? 1f : healsItsOwnSideBy;
            }
        }

        public float AlsoHurtsThingsTouchingIt
        {
            get { return alsoHurtsThingsTouchingIt < 0f ? 0f : alsoHurtsThingsTouchingIt; }
        }

        public bool OnlyShootsWhenInCombat
        {
            get { return onlyShootsWhenInCombat; }
        }

        public bool TakingAHitInterruptsIt
        {
            get { return takingAHitInterruptsIt; }
        }

        public float RecoveryAfterShooting
        {
            get { return recoveryAfterShooting < 0f ? 0f : recoveryAfterShooting; }
        }

        /// <summary>Whether it multiplies its own ranged damage away to nothing.</summary>
        /// <remarks>
        /// Invisible on a tiered creature for the same reason as melee: the flat damage number is
        /// discarded, so a zero multiplier is a creature that fires and never hurts anything.
        /// </remarks>
        public bool MultipliedDownToNoDamage
        {
            get { return shotsHitThisHardForItsTier <= 0f; }
        }

        /// <summary>Whether a spread width was set on a pattern that never spreads.</summary>
        public bool SpreadWidthWillBeIgnored
        {
            get
            {
                return widestSpreadDegrees > 0f && shotPattern != DimensionShotPattern.RandomSpread;
            }
        }

        /// <summary>Whether only one end of the target-leading range was given.</summary>
        public bool LeadRangeIsHalfSpecified
        {
            get { return (startsLeadingTargetsAt > 0f) != (stopsLeadingTargetsAt > 0f); }
        }
    }

    /// <summary>
    /// How the shots in a volley are arranged. Core Keeper's <c>ProjectileSpreadType</c>, named the
    /// way a person would describe what they are looking at.
    /// </summary>
    /// <remarks>
    /// The order matches the game's enum exactly and must stay that way — these are cast straight
    /// across. Five vanilla creatures use something other than the default.
    /// </remarks>
    public enum DimensionShotPattern
    {
        /// <summary>Every projectile goes the same way. ProjectileSpreadType.None.</summary>
        AllTheSameWay = 0,

        /// <summary>Each shot picks a random angle within the spread. Random.</summary>
        RandomSpread = 1,

        /// <summary>The aim sweeps one way then back. BackAndForth.</summary>
        SweepsBackAndForth = 2,

        /// <summary>The aim turns steadily, so shots trail out in a spiral. Spiral.</summary>
        Spirals = 3,

        /// <summary>Two spirals at once, in opposite directions. SpiralDouble.</summary>
        SpiralsBothWays = 4,

        /// <summary>A spiral that unwinds and rewinds. SpiralPingPong.</summary>
        SpiralsInAndOut = 5
    }
}
