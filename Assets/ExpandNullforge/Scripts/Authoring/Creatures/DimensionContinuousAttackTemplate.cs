using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A placed thing that hurts whatever comes near it — a spike, a drill, a turret.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>AttackContinuouslyAuthoring</c>, on 31 vanilla prefabs. It has twenty-three public fields
    /// and the census says most of them are decoration:
    /// </para>
    /// <list type="bullet">
    /// <item><b>24 of 31 need power.</b> That is the headline — most of the game's continuous
    /// attackers are wired machines, not passive spikes, so the power tick belongs near the top and
    /// pairs with the wiring template.</item>
    /// <item><c>isStatic</c> 14, <c>cantDamageObjectsHangingOnWalls</c> 13,
    /// <c>skipLootDropIfDestroyPlants</c> 12, <c>canHitLowTriggers</c> 8.</item>
    /// <item><b>Zero</b>: <c>ignoreDamageReduction</c>, <c>hitRadiusGrowOverTime</c>,
    /// <c>breakAfterSuccessfulHit</c>, <c>canDamageOnlyEnemyAndPlayer</c>. The whole
    /// growing-hit-radius sub-system — three fields — has no user in the game and is not offered,
    /// on the same principle as the projectile's shatter-on-collision.</item>
    /// </list>
    /// <para>
    /// Measured values: a hit radius near 0.5, an attack about every second, and no pushback on 45
    /// of the prefabs that carry a pushback field at all.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionContinuousAttackTemplate
    {
        /// <summary>The hit radius most of the game's continuous attackers use.</summary>
        public const float OrdinaryHitRadius = 0.5f;

        [Tooltip("It hurts whatever comes near it.")]
        [SerializeField] private bool hurtsWhatComesNear;

        [Tooltip("How much it hurts.")]
        [Min(0)]
        [SerializeField] private int damage = 10;

        [Tooltip("Damage multiplier on top of the level curve.")]
        [Min(0f)]
        [SerializeField] private float damageMultiplier = 1f;

        [Tooltip("How far it reaches. 0.5 is what most of the game uses.")]
        [Min(0f)]
        [SerializeField] private float reach = OrdinaryHitRadius;

        [Tooltip("How long a swing takes, in seconds.")]
        [Min(0f)]
        [SerializeField] private float attackSeconds = 1f;

        [Tooltip("How long it waits after a hit, in seconds.")]
        [Min(0f)]
        [SerializeField] private float restSeconds = 1f;

        [Tooltip("How hard it shoves what it hits.")]
        [Min(0f)]
        [SerializeField] private float pushback;

        [Tooltip("It only works while wired and powered. What most of the game's machines do.")]
        [SerializeField] private bool needsPower = true;

        [Header("What it will not hurt")]
        [Tooltip("It stays put rather than turning to face what it attacks.")]
        [SerializeField] private bool staysPut = true;

        [Tooltip("It leaves things mounted on walls alone.")]
        [SerializeField] private bool sparesThingsOnWalls;

        [Tooltip("It reaches low things like pressure plates.")]
        [SerializeField] private bool reachesLowThings;

        [Tooltip("Plants it destroys drop nothing, so it does not farm for the player.")]
        [SerializeField] private bool plantsItBreaksDropNothing;

        [Header("A growing reach")]
        [Tooltip("Its reach grows while it attacks, the way a spreading hazard does.")]
        [SerializeField] private bool reachGrowsWhileAttacking;

        [Tooltip("What the reach grows to.")]
        [Min(0f)]
        [SerializeField] private float reachGrowsTo = 1f;

        [Tooltip("How fast it grows, in units per second.")]
        [Min(0f)]
        [SerializeField] private float reachGrowthRate = 2f;

        [Header("What it hits, and what happens after")]
        [Tooltip("It only ever hits enemies and players, never scenery.")]
        [SerializeField] private bool onlyHitsEnemiesAndPlayers;

        [Tooltip("Among things that are not enemies, it only hits certain ones.")]
        [SerializeField] private bool onlyHitsCertainNonEnemies;

        [Tooltip("Its damage goes through armour and resistances untouched.")]
        [SerializeField] private bool ignoresDamageReduction;

        [Tooltip("What the damage looks and behaves like — plain, or electrical.")]
        [SerializeField] private DimensionDamageFlavour damageFlavour = DimensionDamageFlavour.Plain;

        [Tooltip("It breaks itself once it lands a hit — a single-use trap.")]
        [SerializeField] private bool breaksAfterALandedHit;

        [Tooltip("How long after that hit before it breaks.")]
        [Min(0f)]
        [SerializeField] private float breaksAfterThisLong;

        [Tooltip("It plays its idle animation the moment it starts attacking.")]
        [SerializeField] private bool playsIdleOnStarting;

        [Tooltip("The animation it plays when it hits.")]
        [SerializeField] private string hitAnimation = "attack";

        public bool HurtsWhatComesNear
        {
            get { return hurtsWhatComesNear; }
        }

        public int Damage
        {
            get { return damage < 0 ? 0 : damage; }
        }

        public float DamageMultiplier
        {
            get { return damageMultiplier < 0f ? 0f : damageMultiplier; }
        }

        public float Reach
        {
            get { return reach < 0f ? 0f : reach; }
        }

        public float AttackSeconds
        {
            get { return attackSeconds < 0f ? 0f : attackSeconds; }
        }

        public float RestSeconds
        {
            get { return restSeconds < 0f ? 0f : restSeconds; }
        }

        public float Pushback
        {
            get { return pushback < 0f ? 0f : pushback; }
        }

        public bool NeedsPower
        {
            get { return needsPower; }
        }

        public bool StaysPut
        {
            get { return staysPut; }
        }

        public bool SparesThingsOnWalls
        {
            get { return sparesThingsOnWalls; }
        }

        public bool ReachesLowThings
        {
            get { return reachesLowThings; }
        }

        public bool PlantsItBreaksDropNothing
        {
            get { return plantsItBreaksDropNothing; }
        }

        public bool ReachGrowsWhileAttacking { get { return reachGrowsWhileAttacking; } }

        public float ReachGrowsTo { get { return reachGrowsTo < 0f ? 0f : reachGrowsTo; } }

        public float ReachGrowthRate { get { return reachGrowthRate < 0f ? 0f : reachGrowthRate; } }

        public bool OnlyHitsEnemiesAndPlayers { get { return onlyHitsEnemiesAndPlayers; } }

        public bool OnlyHitsCertainNonEnemies { get { return onlyHitsCertainNonEnemies; } }

        public bool IgnoresDamageReduction { get { return ignoresDamageReduction; } }

        public DimensionDamageFlavour DamageFlavour { get { return damageFlavour; } }

        public bool BreaksAfterALandedHit { get { return breaksAfterALandedHit; } }

        public float BreaksAfterThisLong
        {
            get { return breaksAfterThisLong < 0f ? 0f : breaksAfterThisLong; }
        }

        public bool PlaysIdleOnStarting { get { return playsIdleOnStarting; } }

        /// <summary>Whether a growth target was set on a reach that never grows.</summary>
        public bool GrowthWillBeIgnored
        {
            get { return !reachGrowsWhileAttacking && reachGrowthRate != 2f; }
        }

        /// <summary>Whether a break delay was given to something that never breaks.</summary>
        public bool BreakDelayWillBeIgnored
        {
            get { return !breaksAfterALandedHit && breaksAfterThisLong > 0f; }
        }

        public string HitAnimation
        {
            get { return string.IsNullOrEmpty(hitAnimation) ? "attack" : hitAnimation; }
        }

        /// <summary>Whether it attacks and does no damage when it does.</summary>
        public bool AttacksHarmlessly
        {
            get { return hurtsWhatComesNear && Damage == 0; }
        }

        /// <summary>Whether it reaches nothing at all.</summary>
        public bool ReachesNothing
        {
            get { return hurtsWhatComesNear && Reach <= 0f; }
        }

        /// <summary>Whether it needs power and nothing has been wired to give it any.</summary>
        /// <remarks>
        /// Checked against the wiring template rather than assumed, because the answer lives on a
        /// different template. A trap that needs power and is not wired never fires once, and looks
        /// completely correct on its own.
        /// </remarks>
        public bool NeedsPowerButIsNotWired(DimensionWiringTemplate wiring)
        {
            if (!hurtsWhatComesNear || !needsPower)
            {
                return false;
            }

            return wiring == null || wiring.Role == DimensionWiringRole.NotWired;
        }
    }

    /// <summary>
    /// What a hit looks and behaves like. Core Keeper's <c>DamageEffectType</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionDamageFlavour
    {
        /// <summary>Ordinary damage. None.</summary>
        Plain = 0,

        /// <summary>Electrical damage, with the game's own arcing effect. Electricity.</summary>
        Electrical = 1
    }
}
