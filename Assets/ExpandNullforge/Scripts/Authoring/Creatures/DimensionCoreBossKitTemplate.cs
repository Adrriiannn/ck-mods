using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Three more borrowable boss kits: the Core's orbiting fight, the Wall's slithering body, and
    /// the Scarab's burrow-and-charge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second half of the borrow door. Each of these is a whole fight a custom boss can take on
    /// and then adjust — orbs that circle and fire, a wall of segments that slithers past you, a
    /// scarab that vanishes underground and comes up charging.
    /// </para>
    /// <para>
    /// THE CORE'S PHASE THRESHOLD IS THE MOST BORROWED THING HERE. <c>phase1HealthThreshold</c> is
    /// where the fight changes gear, and it is the one number that turns a health bar into a fight
    /// with a shape. It is 0.5 in vanilla — halfway.
    /// </para>
    /// <para>
    /// THE WALL IS MEASURED FROM THE CORE, not from itself. <c>distanceFromCore</c> is 875 in
    /// vanilla, which is why the Wall is a thing you travel to rather than a thing that finds you.
    /// A custom Wall placed without changing that will appear the same distance out.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCoreBossKitTemplate
    {
        [Header("The Core's kit — orbs and phases")]
        [Tooltip("It fights like the Core: orbiting orbs, a phase change, and a vulnerable window. The two parts that ask about identity are the boulder it throws and its map marker, so use Core Keeper's own CrystalMeteorBoulder and MapMarker for those.")]
        [SerializeField] private bool fightsLikeTheCore;

        [Tooltip("How many orbs circle it.")]
        [Min(0)]
        [SerializeField] private int orbCount = 4;

        [Tooltip("How fast they circle.")]
        [Min(0f)]
        [SerializeField] private float orbSpeed = 6f;

        [Tooltip("How close the orbs come.")]
        [Min(0f)]
        [SerializeField] private float orbMinDistance = 6f;

        [Tooltip("How far out they drift.")]
        [Min(0f)]
        [SerializeField] private float orbMaxDistance = 9f;

        [Tooltip("Health share where the fight changes gear. 0.5 is halfway, as vanilla uses.")]
        [Range(0f, 1f)]
        [SerializeField] private float phaseChangesAtHealth = 0.5f;

        [Tooltip("How long that change takes.")]
        [Min(0f)]
        [SerializeField] private float phaseChangeSeconds = 1f;

        [Tooltip("How long it cannot be hurt while changing.")]
        [Min(0f)]
        [SerializeField] private float untouchableForSeconds = 1f;

        [Tooltip("Flat damage from its whirlwind shots.")]
        [Min(0)]
        [SerializeField] private int whirlwindDamage;

        [Tooltip("How hard whirlwinds hit for its tier.")]
        [Min(0f)]
        [SerializeField] private float whirlwindMultiplier = 1f;

        [Tooltip("Flat damage from its homing shots.")]
        [Min(0)]
        [SerializeField] private int homingDamage;

        [Tooltip("How hard homing shots hit for its tier.")]
        [Min(0f)]
        [SerializeField] private float homingMultiplier = 1f;

        [Header("The Core's summons")]
        [Tooltip("It summons void creatures during the fight.")]
        [SerializeField] private bool summonsVoid = true;

        [Tooltip("How long that summon lasts.")]
        [Min(0f)]
        [SerializeField] private float voidSummonSeconds = 1f;

        [Tooltip("Wind-up before the void summon.")]
        [Min(0f)]
        [SerializeField] private float voidSummonWindUp;

        [Tooltip("Recovery after it.")]
        [Min(0f)]
        [SerializeField] private float voidSummonRecovery;

        [Tooltip("Shortest wait between void summons.")]
        [Min(0f)]
        [SerializeField] private float voidSummonMinCooldown = 5f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float voidSummonMaxCooldown = 10f;

        [Tooltip("It summons beams during the fight.")]
        [SerializeField] private bool summonsBeams = true;

        [Tooltip("Wind-up before a beam summon.")]
        [Min(0f)]
        [SerializeField] private float beamSummonWindUp;

        [Tooltip("Recovery after it.")]
        [Min(0f)]
        [SerializeField] private float beamSummonRecovery;

        [Tooltip("Shortest wait between beam summons.")]
        [Min(0f)]
        [SerializeField] private float beamSummonMinCooldown = 5f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float beamSummonMaxCooldown = 10f;

        [Tooltip("It is one of the orbs that circles the Core, rather than the Core itself.")]
        [SerializeField] private bool isOneOfTheCoresOrbs;

        [Tooltip("It is The Core — the thing every world is built around, that the game reads its starting rules from.")]
        [SerializeField] private bool isTheCoreItself;

        [Tooltip("It is the marker that draws a player's attention to the Core. The game only draws this for the Core boss and the crystal meteor, so on anything else it is carried but nothing acts on it.")]
        [SerializeField] private bool isTheAttentionMarker;

        [Header("The Wall's kit — a slithering body")]
        [Tooltip("It fights like the Wall: a long body of segments that slithers past.")]
        [SerializeField] private bool fightsLikeTheWall;

        [Tooltip("How far out from the Core it sits. Vanilla is 875 — a place you travel to.")]
        [Min(0f)]
        [SerializeField] private float distanceFromTheCore = 875f;

        [Tooltip("How many segments the body has.")]
        [Min(0)]
        [SerializeField] private int segments = 16;

        [Tooltip("How wide each segment is.")]
        [Min(0f)]
        [SerializeField] private float segmentRadius = 2.5f;

        [Tooltip("How wide the whole wall is.")]
        [Min(0f)]
        [SerializeField] private float totalWidth = 50f;

        [Tooltip("How long one attack lasts.")]
        [Min(0f)]
        [SerializeField] private float wallAttackSeconds = 1f;

        [Tooltip("How long between attacks.")]
        [Min(0f)]
        [SerializeField] private float wallAttackCooldown = 1f;

        [Tooltip("How fast the body slithers.")]
        [Min(0f)]
        [SerializeField] private float slitherSpeed = 1f;

        [Tooltip("How long each slither wave is.")]
        [Min(0f)]
        [SerializeField] private float slitherWavelength = 1f;

        [Tooltip("How tall each slither wave is.")]
        [Min(0f)]
        [SerializeField] private float slitherHeight = 1f;

        [Tooltip("Pause before its bulbs come out.")]
        [Min(0f)]
        [SerializeField] private float pauseBeforeBulbs;

        [Tooltip("Pause before its head comes out.")]
        [Min(0f)]
        [SerializeField] private float pauseBeforeHead;

        [Tooltip("How long it stays open to attack.")]
        [Min(0f)]
        [SerializeField] private float wallVulnerableSeconds = 15f;

        [Tooltip("How much taking damage shortens that window.")]
        [Min(0f)]
        [SerializeField] private float wallVulnerableCutShort = 5f;

        [Tooltip("Where the head sits along the body.")]
        [SerializeField] private float headOffset = -1f;

        [Tooltip("How it moves depending on how many players are still standing. One entry per count.")]
        [SerializeField] private DimensionWallMovement[] movementByPlayersAlive = new DimensionWallMovement[0];

        [Tooltip("Where the bulbs sit along the body.")]
        [SerializeField] private float bulbOffset = -1f;

        [Tooltip("It is the Wall's head — the segment that leads, and that a player fights.")]
        [SerializeField] private bool isTheWallsHead;

        [Header("The Scarab's kit — burrow and charge")]
        [Tooltip("It fights like the Scarab: vanishing underground and coming up charging.")]
        [SerializeField] private bool fightsLikeTheScarab;

        [Tooltip("How long it takes to appear.")]
        [Min(0f)]
        [SerializeField] private float scarabAppearSeconds = 1f;

        [Tooltip("How long it takes to bury itself.")]
        [Min(0f)]
        [SerializeField] private float scarabBurySeconds = 1f;

        [Tooltip("How long it takes to come back up.")]
        [Min(0f)]
        [SerializeField] private float scarabSurfaceSeconds = 1f;

        [Tooltip("Shortest wait between charges.")]
        [Min(0f)]
        [SerializeField] private float scarabMinChargeCooldown = 3f;

        [Tooltip("Longest wait between charges.")]
        [Min(0f)]
        [SerializeField] private float scarabMaxChargeCooldown = 6f;

        [Tooltip("Flat charge damage.")]
        [Min(0)]
        [SerializeField] private int scarabChargeDamage;

        [Tooltip("How hard the charge hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float scarabChargeMultiplier = 1f;

        [Tooltip("Wind-up before it releases bomb scarabs.")]
        [Min(0f)]
        [SerializeField] private float bombScarabWindUp;

        [Tooltip("How long that release lasts.")]
        [Min(0f)]
        [SerializeField] private float bombScarabSeconds = 1f;

        [Tooltip("Recovery after it.")]
        [Min(0f)]
        [SerializeField] private float bombScarabRecovery;

        [Tooltip("Shortest wait between bomb scarab releases.")]
        [Min(0f)]
        [SerializeField] private float bombScarabMinCooldown = 8f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float bombScarabMaxCooldown = 14f;

        public bool FightsLikeTheCore { get { return fightsLikeTheCore; } }

        public int OrbCount { get { return orbCount < 0 ? 0 : orbCount; } }

        public float OrbSpeed { get { return NotBelowZero(orbSpeed); } }

        public float OrbMinDistance { get { return NotBelowZero(orbMinDistance); } }

        /// <summary>The outer orbit, never inside the inner one.</summary>
        public float OrbMaxDistance
        {
            get
            {
                float outer = NotBelowZero(orbMaxDistance);
                return outer < OrbMinDistance ? OrbMinDistance : outer;
            }
        }

        public float PhaseChangesAtHealth { get { return Clamp01(phaseChangesAtHealth); } }

        public float PhaseChangeSeconds { get { return NotBelowZero(phaseChangeSeconds); } }

        public float UntouchableForSeconds { get { return NotBelowZero(untouchableForSeconds); } }

        public int WhirlwindDamage { get { return whirlwindDamage < 0 ? 0 : whirlwindDamage; } }

        public float WhirlwindMultiplier { get { return NotBelowZero(whirlwindMultiplier); } }

        public int HomingDamage { get { return homingDamage < 0 ? 0 : homingDamage; } }

        public float HomingMultiplier { get { return NotBelowZero(homingMultiplier); } }

        public bool SummonsVoid { get { return summonsVoid; } }

        public float VoidSummonSeconds { get { return NotBelowZero(voidSummonSeconds); } }

        public float VoidSummonWindUp { get { return NotBelowZero(voidSummonWindUp); } }

        public float VoidSummonRecovery { get { return NotBelowZero(voidSummonRecovery); } }

        public float VoidSummonMinCooldown { get { return NotBelowZero(voidSummonMinCooldown); } }

        public float VoidSummonMaxCooldown
        {
            get { return AtLeast(voidSummonMaxCooldown, VoidSummonMinCooldown); }
        }

        public bool SummonsBeams { get { return summonsBeams; } }

        public float BeamSummonWindUp { get { return NotBelowZero(beamSummonWindUp); } }

        public float BeamSummonRecovery { get { return NotBelowZero(beamSummonRecovery); } }

        public float BeamSummonMinCooldown { get { return NotBelowZero(beamSummonMinCooldown); } }

        public float BeamSummonMaxCooldown
        {
            get { return AtLeast(beamSummonMaxCooldown, BeamSummonMinCooldown); }
        }

        public bool IsOneOfTheCoresOrbs { get { return isOneOfTheCoresOrbs; } }

        public bool IsTheCoreItself { get { return isTheCoreItself; } }

        public bool IsTheAttentionMarker { get { return isTheAttentionMarker; } }

        public bool IsTheWallsHead { get { return isTheWallsHead; } }

        public bool FightsLikeTheWall { get { return fightsLikeTheWall; } }

        public float DistanceFromTheCore { get { return NotBelowZero(distanceFromTheCore); } }

        public int Segments { get { return segments < 0 ? 0 : segments; } }

        public float SegmentRadius { get { return NotBelowZero(segmentRadius); } }

        public float TotalWidth { get { return NotBelowZero(totalWidth); } }

        public float WallAttackSeconds { get { return NotBelowZero(wallAttackSeconds); } }

        public float WallAttackCooldown { get { return NotBelowZero(wallAttackCooldown); } }

        public float SlitherSpeed { get { return NotBelowZero(slitherSpeed); } }

        public float SlitherWavelength { get { return NotBelowZero(slitherWavelength); } }

        public float SlitherHeight { get { return NotBelowZero(slitherHeight); } }

        public float PauseBeforeBulbs { get { return NotBelowZero(pauseBeforeBulbs); } }

        public float PauseBeforeHead { get { return NotBelowZero(pauseBeforeHead); } }

        public float WallVulnerableSeconds { get { return NotBelowZero(wallVulnerableSeconds); } }

        public float WallVulnerableCutShort
        {
            get { return NotBelowZero(wallVulnerableCutShort); }
        }

        public float HeadOffset { get { return headOffset; } }

        public float BulbOffset { get { return bulbOffset; } }

        public DimensionWallMovement[] MovementByPlayersAlive
        {
            get { return movementByPlayersAlive ?? new DimensionWallMovement[0]; }
        }

        public bool FightsLikeTheScarab { get { return fightsLikeTheScarab; } }

        public float ScarabAppearSeconds { get { return NotBelowZero(scarabAppearSeconds); } }

        public float ScarabBurySeconds { get { return NotBelowZero(scarabBurySeconds); } }

        public float ScarabSurfaceSeconds { get { return NotBelowZero(scarabSurfaceSeconds); } }

        public float ScarabMinChargeCooldown
        {
            get { return NotBelowZero(scarabMinChargeCooldown); }
        }

        public float ScarabMaxChargeCooldown
        {
            get { return AtLeast(scarabMaxChargeCooldown, ScarabMinChargeCooldown); }
        }

        public int ScarabChargeDamage
        {
            get { return scarabChargeDamage < 0 ? 0 : scarabChargeDamage; }
        }

        public float ScarabChargeMultiplier { get { return NotBelowZero(scarabChargeMultiplier); } }

        public float BombScarabWindUp { get { return NotBelowZero(bombScarabWindUp); } }

        public float BombScarabSeconds { get { return NotBelowZero(bombScarabSeconds); } }

        public float BombScarabRecovery { get { return NotBelowZero(bombScarabRecovery); } }

        public float BombScarabMinCooldown { get { return NotBelowZero(bombScarabMinCooldown); } }

        public float BombScarabMaxCooldown
        {
            get { return AtLeast(bombScarabMaxCooldown, BombScarabMinCooldown); }
        }

        /// <summary>
        /// Whether the Core kit changes phase at full or empty health.
        /// </summary>
        /// <remarks>
        /// At 1 it changes gear the instant the fight starts; at 0 it never changes at all. Both are
        /// legal numbers and neither is a fight.
        /// </remarks>
        public bool PhaseThresholdMakesNoFight
        {
            get
            {
                return fightsLikeTheCore
                    && (phaseChangesAtHealth >= 1f || phaseChangesAtHealth <= 0f);
            }
        }

        /// <summary>Whether the Wall has a width but nothing to fill it with.</summary>
        public bool WallHasNoSegments
        {
            get { return fightsLikeTheWall && segments <= 0; }
        }

        private static float NotBelowZero(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float AtLeast(float value, float floor)
        {
            float clamped = NotBelowZero(value);
            return clamped < floor ? floor : clamped;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    /// <summary>
    /// How a Wall-kit boss moves when a given number of players are still alive.
    /// </summary>
    /// <remarks>
    /// The Wall gets faster as a party thins out, which is what stops a solo run being a walk. One
    /// entry per surviving-player count; the game picks the matching one.
    /// </remarks>
    [Serializable]
    public struct DimensionWallMovement
    {
        [Tooltip("How many players still standing this applies to.")]
        [Min(0)]
        [SerializeField] private int whenThisManyAlive;

        [Tooltip("How fast it can go.")]
        [Min(0f)]
        [SerializeField] private float topSpeed;

        [Tooltip("How quickly it gets up to speed.")]
        [Min(0f)]
        [SerializeField] private float acceleration;

        [Tooltip("How quickly it slows down.")]
        [Min(0f)]
        [SerializeField] private float deceleration;

        [Tooltip("How long it spends slowing as it enters this state.")]
        [Min(0f)]
        [SerializeField] private float slowingSecondsOnEntering;

        public int WhenThisManyAlive { get { return whenThisManyAlive < 0 ? 0 : whenThisManyAlive; } }

        public float TopSpeed { get { return topSpeed < 0f ? 0f : topSpeed; } }

        public float Acceleration { get { return acceleration < 0f ? 0f : acceleration; } }

        public float Deceleration { get { return deceleration < 0f ? 0f : deceleration; } }

        public float SlowingSecondsOnEntering
        {
            get { return slowingSecondsOnEntering < 0f ? 0f : slowingSecondsOnEntering; }
        }
    }
}
