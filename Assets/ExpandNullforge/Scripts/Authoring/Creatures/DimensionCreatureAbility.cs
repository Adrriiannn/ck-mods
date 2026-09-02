using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// One thing a creature can do beyond walking and swinging.
    /// </summary>
    /// <remarks>
    /// Each kind is a real Core Keeper state component with its own system behind it, not a flag we
    /// interpret. The counts are how many vanilla prefabs carry each, measured across the game's
    /// prefabs — worth knowing because a kind nothing uses has no vanilla example to copy.
    /// </remarks>
    public enum DimensionCreatureAbilityKind
    {
        /// <summary>Rushes in a straight line, optionally ending in a hit. (11 vanilla prefabs)</summary>
        ChargeAttack = 0,

        /// <summary>Leaps at its target and lands on them. (13)</summary>
        JumpAttack = 1,

        /// <summary>Lobs a projectile in an arc, from further out than it can shoot. (15)</summary>
        MortarShot = 2,

        /// <summary>Blows up — on contact, on death, or on being spawned. (10)</summary>
        Explode = 3,

        /// <summary>Vanishes and reappears elsewhere. (5)</summary>
        Teleport = 4,

        /// <summary>Changes behaviour below a health threshold. (17)</summary>
        Enrage = 5,

        /// <summary>Sleeps until something wakes it. (11)</summary>
        Sleep = 6,

        /// <summary>Walks to food and eats it. (12)</summary>
        Eat = 7,

        /// <summary>Breeds after enough meals, producing young. (6)</summary>
        Breed = 8,

        /// <summary>Grows into a different creature after enough food. (6)</summary>
        Evolve = 9,

        /// <summary>Heals other creatures nearby. (2)</summary>
        HealAllies = 10,

        /// <summary>Drops its guard for a window, the way bosses do between phases. (5)</summary>
        Vulnerable = 11
    }

    /// <summary>
    /// A single ability, described in the terms the ability itself uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A LIST AND NOT TWENTY TICKBOXES. A boss is a set of things it can do, and that set is what
    /// an author is actually composing — adding a charge, giving it an enrage below half health,
    /// letting it teleport away when cornered. Modelled as a list, that reads as a kit; modelled as
    /// twenty booleans with forty settings hanging off them, the same creature is a wall of fields
    /// where nearly everything is irrelevant to nearly every creature.
    /// </para>
    /// <para>
    /// The fields are shared deliberately. Almost every one of these states has a wind-up, a duration,
    /// a cooldown range and a distance — Core Keeper names them slightly differently on each component
    /// (<c>anticipationDuration</c>, <c>anticipationTime</c>, <c>minPreFallAsleepDuration</c>) but they
    /// mean the same thing to a person. What each field maps onto per kind is documented on the
    /// accessor, and the generator is the only thing that needs to know.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureAbility
    {
        [SerializeField] private DimensionCreatureAbilityKind kind = DimensionCreatureAbilityKind.ChargeAttack;

        [Tooltip("Wind-up before it happens. The window a player gets to react in.")]
        [Min(0f)]
        [SerializeField] private float windUp = 0.5f;

        [Tooltip("How long the ability itself lasts.")]
        [Min(0f)]
        [SerializeField] private float duration = 1f;

        [Tooltip("Shortest wait before it can happen again.")]
        [Min(0f)]
        [SerializeField] private float minCooldown = 3f;

        [Tooltip("Longest wait before it can happen again.")]
        [Min(0f)]
        [SerializeField] private float maxCooldown = 6f;

        [Tooltip("How far away it works — to provoke a charge, to leap, to explode, to reach an ally.")]
        [Min(0f)]
        [SerializeField] private float range = 4f;

        [Tooltip("Damage it deals, or health it restores. 0 lets the area level decide, as vanilla does.")]
        [Min(0)]
        [SerializeField] private int power;

        [Tooltip("Speed while it happens, relative to normal. Used by charge and jump.")]
        [Min(0f)]
        [SerializeField] private float speedMultiplier = 1f;

        [Tooltip("Health fraction that triggers it — enrage below this, or refuse to explode above it.")]
        [Range(0f, 1f)]
        [SerializeField] private float healthFraction = 0.5f;

        [Tooltip("How many of something it needs: meals before breeding, food before evolving.")]
        [Min(0)]
        [SerializeField] private int amount = 3;

        [Tooltip("The object it needs: the explosion, the mortar projectile, the baby, what it evolves into.")]
        [SerializeField] private string targetObjectId = string.Empty;

        [Tooltip("It happens when the creature dies rather than on its own timer. Explode only.")]
        [SerializeField] private bool onDeath;

        [Header("Shared: the tail of the ability")]
        [Tooltip("A pause before the wind-up even starts. Vulnerable uses this.")]
        [Min(0f)]
        [SerializeField] private float preWindUp;

        [Tooltip("How long it takes to recover afterwards. Vulnerable and teleport use this.")]
        [Min(0f)]
        [SerializeField] private float recoveryAfter;

        [Header("Dropping its guard")]
        [Tooltip("It breaks terrain within this radius as it drops its guard.")]
        [Min(0f)]
        [SerializeField] private float breaksTerrainWithin;

        [Tooltip("How hard it shoves everything nearby as it does.")]
        [Min(0f)]
        [SerializeField] private float shovesNearbyWithForce;

        [Tooltip("How far that shove reaches.")]
        [Min(0f)]
        [SerializeField] private float shoveReaches;

        [Tooltip("It stops being vulnerable early once it has lost this share of its health.")]
        [Range(0f, 1f)]
        [SerializeField] private float leavesAfterLosingThisMuchHealth = 0.25f;

        [Header("Teleporting")]
        [Tooltip("It can only ever go back to where it spawned.")]
        [SerializeField] private bool onlyGoesBackToWhereItSpawned;

        [Tooltip("It can land on pits and water.")]
        [SerializeField] private bool canLandOnPitsAndWater;

        [Tooltip("It never strays further than this from where it was.")]
        [Min(0f)]
        [SerializeField] private float staysWithinThisFarOfWhereItWas;

        [Tooltip("It never lands closer to the player than this.")]
        [Min(0f)]
        [SerializeField] private float neverLandsCloserToThePlayerThan;

        [Tooltip("The corner of the area whose tiles are refreshed after it goes.")]
        [SerializeField] private Vector2Int refreshesTilesFromCorner = Vector2Int.zero;

        [Tooltip("And the opposite corner of that area.")]
        [SerializeField] private Vector2Int refreshesTilesToCorner = Vector2Int.zero;

        [Header("Blowing up")]
        [Tooltip("Which look the explosion uses.")]
        [Min(0)]
        [SerializeField] private int explosionVariation;

        [Tooltip("It still drops its loot when it blows itself up.")]
        [SerializeField] private bool dropsItsLootWhenItBlowsUp;

        [Tooltip("Flat terrain damage from the blast, when there is no tier to scale from.")]
        [Min(0)]
        [SerializeField] private int flatTerrainDamage;

        [Tooltip("How hard the blast hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float hitsThisHardForItsTier = 1f;

        [Tooltip("How much terrain the blast breaks for its tier.")]
        [Min(0f)]
        [SerializeField] private float breaksTerrainThisHardForItsTier = 1f;

        [Header("Sleeping")]
        [Tooltip("How long it takes to wake up.")]
        [Min(0f)]
        [SerializeField] private float wakeUpSeconds = 1f;

        [Tooltip("It wakes when its owner comes within this far. 0 for never.")]
        [Min(0f)]
        [SerializeField] private float wakesWhenItsOwnerIsWithin;

        [Tooltip("Once awake it stays awake until no player is in sight.")]
        [SerializeField] private bool staysAwakeWhileSeen;

        [Tooltip("Hitting something wakes it immediately on everyone's screen.")]
        [SerializeField] private bool wakesTheMomentItHitsSomething;

        [Header("Healing")]
        [Tooltip("How much it heals for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float healsThisMuchForItsTier = 1f;

        [Tooltip("It heals a share of the target's maximum health rather than a flat number.")]
        [SerializeField] private bool healsAShareOfMaxHealth;

        [Tooltip("It keeps healing until it has been hit this many times. 0 for no limit.")]
        [Min(0)]
        [SerializeField] private int keepsHealingUntilHitThisManyTimes;

        [Tooltip("It heals allies it cannot see.")]
        [SerializeField] private bool healsWhatItCannotSee;

        [Header("Leaping")]
        [Tooltip("How hard the leap hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float leapHitsThisHardForItsTier = 1f;

        [Tooltip("Its leap only ever hits enemies and players, never scenery.")]
        [SerializeField] private bool leapOnlyHitsEnemiesAndPlayers;

        [Header("Enraging, eating, breeding, spawning")]
        [Tooltip("It calms down again once its health is back above this share.")]
        [Range(0f, 1f)]
        [SerializeField] private float calmsDownAboveHealth;

        [Tooltip("How long it stands there after finishing a meal.")]
        [Min(0f)]
        [SerializeField] private float pauseAfterEating = 0.5f;

        [Tooltip("Chance a baby comes out a different variation than its parent.")]
        [Range(0f, 1f)]
        [SerializeField] private float mutationChance = 0.1f;

        [Tooltip("Which variations a mutated baby can be, and how likely each is.")]
        [SerializeField] private DimensionMutationWeight[] mutations = new DimensionMutationWeight[0];

        [Tooltip("Which way it faces as it spawns in.")]
        [SerializeField] private Vector2 facesOnSpawn = Vector2.zero;

        [Tooltip("Nudges where it clears tiles as it spawns in.")]
        [SerializeField] private Vector2 clearsTilesOffsetBy = Vector2.zero;

        [Tooltip("Where its companions appear relative to it.")]
        [SerializeField] private Vector3 companionsAppearAt = Vector3.zero;

        [Tooltip("For a mortar shot: how many shells, where they land, and how long they hang in the air.")]
        [SerializeField] private DimensionMortarBarrageTemplate mortarBarrage = new DimensionMortarBarrageTemplate();

        [Tooltip("For a charge: what happens when it connects, whether it steers, and how long it is left open afterwards.")]
        [SerializeField] private DimensionChargeShapeTemplate chargeShape = new DimensionChargeShapeTemplate();

        public DimensionCreatureAbilityKind Kind { get { return kind; } }

        /// <summary>
        /// Wind-up, in the terms each state uses.
        /// </summary>
        /// <remarks>
        /// <c>anticipationDuration</c> on charge, mortar, heal and vulnerable;
        /// <c>anticipationTime</c> on jump; <c>minPreFallAsleepDuration</c> on sleep.
        /// </remarks>
        public float WindUp { get { return windUp < 0f ? 0f : windUp; } }

        /// <summary>
        /// How long it lasts — <c>chargeDuration</c>, <c>airTime</c>, <c>explodeDuration</c>,
        /// <c>vulnerableDuration</c>, <c>healDuration</c>, <c>minSleepDuration</c>.
        /// </summary>
        public float Duration { get { return duration < 0f ? 0f : duration; } }

        public float MinCooldown { get { return minCooldown < 0f ? 0f : minCooldown; } }

        public float MaxCooldown
        {
            get { return maxCooldown < MinCooldown ? MinCooldown : maxCooldown; }
        }

        /// <summary>
        /// The distance the ability cares about — <c>distanceToProvokeCharge</c>,
        /// <c>distanceToAttack</c>, <c>distanceToExplode</c>, <c>maxReachDistance</c>,
        /// <c>radiusFromVisiblePlayerToAwake</c>, <c>distanceToEat</c>, <c>minDistanceToBreed</c>.
        /// </summary>
        public float Range { get { return range < 0f ? 0f : range; } }

        public int Power { get { return power < 0 ? 0 : power; } }

        public float PreWindUp { get { return preWindUp < 0f ? 0f : preWindUp; } }

        public float RecoveryAfter { get { return recoveryAfter < 0f ? 0f : recoveryAfter; } }

        public float BreaksTerrainWithin
        {
            get { return breaksTerrainWithin < 0f ? 0f : breaksTerrainWithin; }
        }

        public float ShovesNearbyWithForce
        {
            get { return shovesNearbyWithForce < 0f ? 0f : shovesNearbyWithForce; }
        }

        public float ShoveReaches { get { return shoveReaches < 0f ? 0f : shoveReaches; } }

        public float LeavesAfterLosingThisMuchHealth
        {
            get { return Clamp01(leavesAfterLosingThisMuchHealth); }
        }

        public bool OnlyGoesBackToWhereItSpawned { get { return onlyGoesBackToWhereItSpawned; } }

        public bool CanLandOnPitsAndWater { get { return canLandOnPitsAndWater; } }

        public float StaysWithinThisFarOfWhereItWas
        {
            get
            {
                return staysWithinThisFarOfWhereItWas < 0f ? 0f : staysWithinThisFarOfWhereItWas;
            }
        }

        public float NeverLandsCloserToThePlayerThan
        {
            get
            {
                return neverLandsCloserToThePlayerThan < 0f
                    ? 0f
                    : neverLandsCloserToThePlayerThan;
            }
        }

        public Vector2Int RefreshesTilesFromCorner { get { return refreshesTilesFromCorner; } }

        public Vector2Int RefreshesTilesToCorner { get { return refreshesTilesToCorner; } }

        public int ExplosionVariation
        {
            get { return explosionVariation < 0 ? 0 : explosionVariation; }
        }

        public bool DropsItsLootWhenItBlowsUp { get { return dropsItsLootWhenItBlowsUp; } }

        public int FlatTerrainDamage
        {
            get { return flatTerrainDamage < 0 ? 0 : flatTerrainDamage; }
        }

        public float HitsThisHardForItsTier
        {
            get { return hitsThisHardForItsTier < 0f ? 0f : hitsThisHardForItsTier; }
        }

        public float BreaksTerrainThisHardForItsTier
        {
            get
            {
                return breaksTerrainThisHardForItsTier < 0f
                    ? 0f
                    : breaksTerrainThisHardForItsTier;
            }
        }

        public float WakeUpSeconds { get { return wakeUpSeconds < 0f ? 0f : wakeUpSeconds; } }

        public float WakesWhenItsOwnerIsWithin
        {
            get { return wakesWhenItsOwnerIsWithin < 0f ? 0f : wakesWhenItsOwnerIsWithin; }
        }

        public bool StaysAwakeWhileSeen { get { return staysAwakeWhileSeen; } }

        public bool WakesTheMomentItHitsSomething
        {
            get { return wakesTheMomentItHitsSomething; }
        }

        public float HealsThisMuchForItsTier
        {
            get { return healsThisMuchForItsTier < 0f ? 0f : healsThisMuchForItsTier; }
        }

        public bool HealsAShareOfMaxHealth { get { return healsAShareOfMaxHealth; } }

        public int KeepsHealingUntilHitThisManyTimes
        {
            get
            {
                return keepsHealingUntilHitThisManyTimes < 0
                    ? 0
                    : keepsHealingUntilHitThisManyTimes;
            }
        }

        public bool HealsWhatItCannotSee { get { return healsWhatItCannotSee; } }

        public float LeapHitsThisHardForItsTier
        {
            get { return leapHitsThisHardForItsTier < 0f ? 0f : leapHitsThisHardForItsTier; }
        }

        public bool LeapOnlyHitsEnemiesAndPlayers
        {
            get { return leapOnlyHitsEnemiesAndPlayers; }
        }

        public float CalmsDownAboveHealth { get { return Clamp01(calmsDownAboveHealth); } }

        public float PauseAfterEating { get { return pauseAfterEating < 0f ? 0f : pauseAfterEating; } }

        public float MutationChance { get { return Clamp01(mutationChance); } }

        public DimensionMutationWeight[] Mutations
        {
            get { return mutations ?? new DimensionMutationWeight[0]; }
        }

        public Vector2 FacesOnSpawn { get { return facesOnSpawn; } }

        public Vector2 ClearsTilesOffsetBy { get { return clearsTilesOffsetBy; } }

        public Vector3 CompanionsAppearAt { get { return companionsAppearAt; } }

        /// <summary>Whether it can mutate with nothing to mutate into.</summary>
        public bool MutatesIntoNothing
        {
            get { return mutationChance > 0f && Mutations.Length == 0; }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        public DimensionMortarBarrageTemplate MortarBarrage
        {
            get { return mortarBarrage ?? (mortarBarrage = new DimensionMortarBarrageTemplate()); }
        }

        public DimensionChargeShapeTemplate ChargeShape
        {
            get { return chargeShape ?? (chargeShape = new DimensionChargeShapeTemplate()); }
        }

        /// <summary>Whether the area level decides the number instead of an authored one.</summary>
        /// <remarks>
        /// Same rule as the main attacks: these components recompute their damage from
        /// <c>AreaLevelAuthoring</c> in <c>OnValidate</c>, so an authored number is overwritten
        /// whenever one is present. Zero means "do what vanilla does".
        /// </remarks>

        public bool PowerFromLevel { get { return Power == 0; } }

        public float SpeedMultiplier
        {
            get { return speedMultiplier <= 0f ? 1f : speedMultiplier; }
        }

        /// <summary>
        /// The health fraction that gates it — <c>enrageAtHealthRatio</c>,
        /// <c>minHealthRatioToExplode</c>.
        /// </summary>
        public float HealthFraction
        {
            get
            {
                if (healthFraction < 0f)
                {
                    return 0f;
                }

                return healthFraction > 1f ? 1f : healthFraction;
            }
        }

        /// <summary>
        /// A count — <c>mealsToTrigger</c>, <c>foodAmountToEvolve</c>, <c>maxFoodUntilFull</c>.
        /// </summary>
        public int Amount { get { return amount < 0 ? 0 : amount; } }

        /// <summary>
        /// The object the ability needs — <c>explosionID</c>, <c>mortarProjectileID</c>,
        /// <c>babyType</c>, <c>toEvolveInto</c>.
        /// </summary>
        public string TargetObjectId { get { return targetObjectId ?? string.Empty; } }

        public bool OnDeath { get { return onDeath; } }

        /// <summary>Whether this kind cannot work without an object naming what it produces.</summary>
        /// <remarks>
        /// These four states each read an <c>ObjectID</c> and do nothing useful without one: a mortar
        /// with no projectile fires nothing, a breeder with no baby produces nothing, an evolver with
        /// nothing to become never changes. Explode is the exception that still half-works — it plays
        /// its timer and simply spawns no explosion.
        /// </remarks>
        public bool NeedsTargetObject
        {
            get
            {
                return kind == DimensionCreatureAbilityKind.MortarShot
                    || kind == DimensionCreatureAbilityKind.Breed
                    || kind == DimensionCreatureAbilityKind.Evolve
                    || kind == DimensionCreatureAbilityKind.Explode;
            }
        }

        /// <summary>Whether it needs an object and was not given one.</summary>
        public bool IsMissingTargetObject
        {
            get { return NeedsTargetObject && string.IsNullOrEmpty(TargetObjectId); }
        }
    }
}
