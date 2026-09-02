using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The detail of a charge: what happens when it connects, whether it can steer, and how long it
    /// is left exposed afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A charge is the one ability the generic ability questions cannot describe. Wind-up, duration,
    /// cooldown, range and speed give you something that runs at the player and stops;
    /// <c>ChargeAttackStateAuthoring</c> has thirty-eight fields because a real charge is a sequence
    /// — wind up, run, collide, optionally swing at the end, then stand there stunned. Six of those
    /// were reachable, so every custom charger was the same creature with different numbers.
    /// </para>
    /// <para>
    /// MEASURED ACROSS THE ELEVEN VANILLA CHARGERS: <c>endChargeWithAttack</c> 4;
    /// <c>endChargeDistanceToAttemptAttack</c>, <c>collideDuration</c>,
    /// <c>dontCollideWithObjects</c>, <c>steerTowardsTargetDuringCharge</c>,
    /// <c>endOfChargeAttackHitTiles</c>, <c>reversePushback</c> and <c>endChargeAttackDuration</c>
    /// 3 each; <c>vulnerabilityDuration</c> 2. <c>steerTowardsTargetMinDistance</c> (1.5) and
    /// <c>steerTowardsTargetMaxAngleDeg</c> (180) carry the game's own defaults, so they are offered
    /// with those values rather than zero.
    /// </para>
    /// <para>
    /// THE VULNERABILITY WINDOW IS THE INTERESTING ONE. It is how a charging boss becomes a fight
    /// rather than a chase: it slams into a wall and is open for a moment. Two vanilla creatures use
    /// it, at 0.1 and 2 seconds, and no custom creature could until now.
    /// </para>
    /// <para>
    /// EVERY FIELD ON THE COMPONENT IS OFFERED. The two
    /// <c>ChargeAttackRotateToTargetData</c> fields turned out not to be curve assets at all — each
    /// is a two-field class, a turn type and a degrees-per-second — so they are asked about as
    /// "how it turns" rather than skipped.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionChargeShapeTemplate
    {
        [Header("Damage")]
        [Tooltip("How hard the charge hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float hitsThisHardForItsTier = 1f;

        [Tooltip("How much terrain the charge breaks for its tier.")]
        [Min(0f)]
        [SerializeField] private float breaksTerrainThisHardForItsTier = 1f;

        [Tooltip("It smashes through terrain as it charges.")]
        [SerializeField] private bool ploughsThroughTerrain;

        [Header("Ending the charge")]
        [Tooltip("It swings at the end of the charge rather than just stopping. 4 of 11 do.")]
        [SerializeField] private bool endsWithASwing;

        [Tooltip("It swings at the end even when it never reached anything.")]
        [SerializeField] private bool swingsEvenIfItHitNothing;

        [Tooltip("How close it has to be at the end to try that swing. Vanilla uses 2.5.")]
        [Min(0f)]
        [SerializeField] private float swingsIfWithin;

        [Tooltip("How long that ending swing lasts.")]
        [Min(0f)]
        [SerializeField] private float endingSwingDuration;

        [Tooltip("How hard it lunges into the ending swing.")]
        [Min(0f)]
        [SerializeField] private float endingSwingLunge;

        [Tooltip("The wind-up on the ending swing specifically, separate from the charge's own.")]
        [Min(0f)]
        [SerializeField] private float endingSwingWindUp;

        [Tooltip("The ending swing breaks terrain too.")]
        [SerializeField] private bool endingSwingBreaksTerrain;

        [Header("Hitting things")]
        [Tooltip("How long it stays stuck on whatever it ran into.")]
        [Min(0f)]
        [SerializeField] private float timeStuckOnImpact;

        [Tooltip("How hard it bounces back off what it hit. Three chargers use 0.5 to 3.")]
        [Min(0f)]
        [SerializeField] private float bouncesBackThisHard;

        [Tooltip("It charges straight through scenery instead of colliding with it.")]
        [SerializeField] private bool passesThroughScenery;

        [Tooltip("Low obstacles do not stop the charge.")]
        [SerializeField] private bool lowObstaclesDoNotStopIt;

        [Tooltip("It still plays the impact animation when it hit nothing at all.")]
        [SerializeField] private bool playsImpactEvenOnAMiss;

        [Header("Steering")]
        [Tooltip("It can curve towards its target mid-charge instead of running in a straight line.")]
        [SerializeField] private bool canSteerMidCharge;

        [Tooltip("It stops steering once this close. The game's own value is 1.5.")]
        [Min(0f)]
        [SerializeField] private float stopsSteeringWithin = 1.5f;

        [Tooltip("The widest it will turn while steering, in degrees. The game's own value is 180.")]
        [Range(0f, 360f)]
        [SerializeField] private float widestSteerDegrees = 180f;

        [Tooltip("How far into the charge its facing locks. 0.9 and 0.6 in vanilla.")]
        [Range(0f, 1f)]
        [SerializeField] private float facingLocksAt = 1f;

        [Tooltip("How hard it shoves whatever it runs into.")]
        [Min(0f)]
        [SerializeField] private float pushesWhatItHits;

        [Tooltip("Flat terrain damage, used only when the creature has no tier to scale from.")]
        [Min(0)]
        [SerializeField] private int flatTerrainDamage;

        [Header("How it turns")]
        [Tooltip("How it turns towards its target while steering.")]
        [SerializeField] private DimensionChargeTurn steerTurn = DimensionChargeTurn.SnapsStraightToIt;

        [Tooltip("Degrees per second, when the steering turn is gradual.")]
        [Min(0f)]
        [SerializeField] private float steerDegreesPerSecond;

        [Tooltip("How it turns while its facing is locking.")]
        [SerializeField] private DimensionChargeTurn lockTurn = DimensionChargeTurn.SnapsStraightToIt;

        [Tooltip("Degrees per second, when the locking turn is gradual.")]
        [Min(0f)]
        [SerializeField] private float lockDegreesPerSecond;

        [Header("After it ends")]
        [Tooltip("How long it stands there exposed after the charge. This is what makes it a fight.")]
        [Min(0f)]
        [SerializeField] private float vulnerableFor;

        [Tooltip("How long it takes to recover before doing anything else.")]
        [Min(0f)]
        [SerializeField] private float recoveryAfterwards;

        [Header("Custom hitbox")]
        [Tooltip("Override the charge's reach. 0 lets the game work it out.")]
        [Min(0f)]
        [SerializeField] private float hitboxHalfLength;

        [Tooltip("Override the charge's width.")]
        [Min(0f)]
        [SerializeField] private float hitboxHalfWidth;

        [Tooltip("How far in front of it the charge connects.")]
        [Min(0f)]
        [SerializeField] private float hitReach;

        [Tooltip("How wide a circle the charge hits within.")]
        [Min(0f)]
        [SerializeField] private float hitRadius;

        [Tooltip("Nudges where the charge's hitbox sits.")]
        [SerializeField] private Vector3 hitboxOffset = Vector3.zero;

        [Tooltip("It snaps to the four compass directions rather than charging at any angle.")]
        [SerializeField] private bool onlyChargesInFourDirections;

        public float PushesWhatItHits
        {
            get { return pushesWhatItHits < 0f ? 0f : pushesWhatItHits; }
        }

        public int FlatTerrainDamage
        {
            get { return flatTerrainDamage < 0 ? 0 : flatTerrainDamage; }
        }

        public DimensionChargeTurn SteerTurn
        {
            get { return steerTurn; }
        }

        public float SteerDegreesPerSecond
        {
            get { return steerDegreesPerSecond < 0f ? 0f : steerDegreesPerSecond; }
        }

        public DimensionChargeTurn LockTurn
        {
            get { return lockTurn; }
        }

        public float LockDegreesPerSecond
        {
            get { return lockDegreesPerSecond < 0f ? 0f : lockDegreesPerSecond; }
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

        public bool PloughsThroughTerrain
        {
            get { return ploughsThroughTerrain; }
        }

        public bool EndsWithASwing
        {
            get { return endsWithASwing; }
        }

        public bool SwingsEvenIfItHitNothing
        {
            get { return swingsEvenIfItHitNothing; }
        }

        public float SwingsIfWithin
        {
            get { return swingsIfWithin < 0f ? 0f : swingsIfWithin; }
        }

        public float EndingSwingDuration
        {
            get { return endingSwingDuration < 0f ? 0f : endingSwingDuration; }
        }

        public float EndingSwingLunge
        {
            get { return endingSwingLunge < 0f ? 0f : endingSwingLunge; }
        }

        public float EndingSwingWindUp
        {
            get { return endingSwingWindUp < 0f ? 0f : endingSwingWindUp; }
        }

        public bool EndingSwingBreaksTerrain
        {
            get { return endingSwingBreaksTerrain; }
        }

        public float TimeStuckOnImpact
        {
            get { return timeStuckOnImpact < 0f ? 0f : timeStuckOnImpact; }
        }

        public float BouncesBackThisHard
        {
            get { return bouncesBackThisHard < 0f ? 0f : bouncesBackThisHard; }
        }

        public bool PassesThroughScenery
        {
            get { return passesThroughScenery; }
        }

        public bool LowObstaclesDoNotStopIt
        {
            get { return lowObstaclesDoNotStopIt; }
        }

        public bool PlaysImpactEvenOnAMiss
        {
            get { return playsImpactEvenOnAMiss; }
        }

        public bool CanSteerMidCharge
        {
            get { return canSteerMidCharge; }
        }

        public float StopsSteeringWithin
        {
            get { return stopsSteeringWithin < 0f ? 0f : stopsSteeringWithin; }
        }

        public float WidestSteerDegrees
        {
            get
            {
                if (widestSteerDegrees < 0f)
                {
                    return 0f;
                }

                return widestSteerDegrees > 360f ? 360f : widestSteerDegrees;
            }
        }

        public float FacingLocksAt
        {
            get
            {
                if (facingLocksAt < 0f)
                {
                    return 0f;
                }

                return facingLocksAt > 1f ? 1f : facingLocksAt;
            }
        }

        public float VulnerableFor
        {
            get { return vulnerableFor < 0f ? 0f : vulnerableFor; }
        }

        public float RecoveryAfterwards
        {
            get { return recoveryAfterwards < 0f ? 0f : recoveryAfterwards; }
        }

        public float HitboxHalfLength
        {
            get { return hitboxHalfLength < 0f ? 0f : hitboxHalfLength; }
        }

        public float HitboxHalfWidth
        {
            get { return hitboxHalfWidth < 0f ? 0f : hitboxHalfWidth; }
        }

        public float HitReach
        {
            get { return hitReach < 0f ? 0f : hitReach; }
        }

        public float HitRadius
        {
            get { return hitRadius < 0f ? 0f : hitRadius; }
        }

        public Vector3 HitboxOffset
        {
            get { return hitboxOffset; }
        }

        public bool OnlyChargesInFourDirections
        {
            get { return onlyChargesInFourDirections; }
        }

        /// <summary>Whether the ending-swing settings will be ignored because there is no swing.</summary>
        /// <remarks>
        /// Worth saying because it reads as configured: a charge with an ending distance, duration
        /// and lunge but with <c>endsWithASwing</c> off simply stops when it arrives, and every one
        /// of those numbers is dead.
        /// </remarks>
        public bool EndingSwingSettingsWillBeIgnored
        {
            get
            {
                return !endsWithASwing
                    && (swingsIfWithin > 0f
                        || endingSwingDuration > 0f
                        || endingSwingLunge > 0f
                        || endingSwingWindUp > 0f
                        || endingSwingBreaksTerrain
                        || swingsEvenIfItHitNothing);
            }
        }

        /// <summary>Whether steering was configured on a charge that cannot steer.</summary>
        public bool SteeringSettingsWillBeIgnored
        {
            get { return !canSteerMidCharge && stopsSteeringWithin != 1.5f; }
        }
    }

    /// <summary>
    /// How a charging creature turns towards its target. Core Keeper's
    /// <c>ChargeAttackRotateToTargetType</c>.
    /// </summary>
    /// <remarks>
    /// The order matches the game's enum exactly and must stay that way — these are cast straight
    /// across.
    /// </remarks>
    public enum DimensionChargeTurn
    {
        /// <summary>It snaps to face its target outright. FullAim.</summary>
        SnapsStraightToIt = 0,

        /// <summary>It swings round at a fixed rate, so a fast target can outrun the turn.
        /// DegreesPerSecond.</summary>
        TurnsAtAFixedRate = 1
    }
}
