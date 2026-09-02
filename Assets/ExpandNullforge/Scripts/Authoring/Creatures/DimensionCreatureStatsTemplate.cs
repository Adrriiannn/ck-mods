using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Where a creature's numbers come from.
    /// </summary>
    /// <remarks>
    /// Core Keeper normally derives a creature's health and damage reduction from the area level it
    /// spawns in, through fixed curves — a boss is <c>300 x level^2.45</c>, an ordinary enemy is
    /// <c>135 + 15 x (level-1)^2</c>. That is right for the base game, where everything has to sit on
    /// one difficulty ramp. It is wrong as a default for a mod, because it means the author does not
    /// actually choose their creature's health: they choose a multiplier on a curve somebody else drew.
    /// </remarks>
    public enum DimensionCreatureStatSource
    {
        /// <summary>
        /// The numbers below are the numbers, exactly as typed.
        /// </summary>
        /// <remarks>
        /// The default, deliberately. An author who types 450 health gets a creature with 450 health.
        /// </remarks>
        Authored = 0,

        /// <summary>
        /// Health and damage reduction come from Core Keeper's own level curves, as vanilla creatures do.
        /// </summary>
        /// <remarks>
        /// Worth having rather than forbidding: a creature meant to sit alongside vanilla ones in a
        /// vanilla-difficulty area should scale the way its neighbours do, and reproducing those curves
        /// by hand would drift the moment the game retuned them.
        /// </remarks>
        AreaLevelCurve = 1
    }

    /// <summary>
    /// Every number that decides what a creature is, with nothing derived unless it is asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The design rule here is that there are no invisible numbers. Each field maps to a value Core
    /// Keeper actually reads, and generation writes exactly what is typed — including switching OFF
    /// the level-derived paths, which would otherwise silently overwrite health and damage reduction
    /// at bake time no matter what the author entered.
    /// </para>
    /// <para>
    /// Fields left at zero mean "the game's own default for this creature shape", not "zero" — a
    /// creature with no authored chase distance uses the behaviour's own, rather than refusing to
    /// chase. Where zero is a meaningful value in its own right there is an explicit toggle beside it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureStatsTemplate
    {
        [Tooltip("Authored uses your numbers exactly. Area level curve makes the creature scale the " +
                 "way vanilla creatures do.")]
        [SerializeField] private DimensionCreatureStatSource statSource = DimensionCreatureStatSource.Authored;

        [Header("Survivability")]
        [Tooltip("Total health. Used verbatim unless the stat source is the area level curve.")]
        [SerializeField] private int maxHealth = 100;

        [Tooltip("Health it spawns with, as a fraction of maximum. 1 means undamaged.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float startHealthFraction = 1f;

        [Tooltip("Flat damage subtracted from every hit it takes.")]
        [SerializeField] private int damageReduction;

        [Tooltip("Most damage any single hit can do to it. 0 means no cap.")]
        [SerializeField] private int maxDamagePerHit;

        [Tooltip("Least damage any single hit can do to it, before reduction can take it lower.")]
        [SerializeField] private int minDamagePerHit;

        [Header("Offence")]
        [Tooltip("Damage it deals on contact or with its basic attack.")]
        [SerializeField] private int damage = 10;

        [Tooltip("Multiplies the damage of whatever attack its borrowed behaviour uses.")]
        [SerializeField] private float damageMultiplier = 1f;

        [Header("Movement and awareness")]
        [Tooltip("How fast it moves.")]
        [SerializeField] private float moveSpeed = 8f;

        [Tooltip("How far away it notices a player. 0 keeps its behaviour's own range.")]
        [SerializeField] private float detectionRadius;

        [Tooltip("How wide its body is, in tiles, for hitting it and being blocked by it. " +
            "1 is a caveling. A boss that fills five tiles and is left at 1 can only be hit " +
            "when the player is standing almost inside it.")]
        [Min(0f)]
        [SerializeField] private float bodyWidthInTiles = 1f;

        [Tooltip("How wide it looks to spike traps, summoning circles and anything else that " +
            "watches for a creature nearby. 0 uses its body width. Core Keeper's own small " +
            "creatures are often noticed from further away than they can be hit.")]
        [Min(0f)]
        [SerializeField] private float noticedWidthInTiles;

        [Tooltip("How close it tries to get before attacking. 0 keeps its behaviour's own distance.")]
        [SerializeField] private float chaseAtDistance;

        [Tooltip("Multiplies its speed while chasing. 0 keeps its behaviour's own multiplier.")]
        [SerializeField] private float chaseSpeedMultiplier;

        [Header("Death")]
        [Tooltip("Override how long the body lingers before it is removed.")]
        [SerializeField] private bool overrideDeathTiming;

        [Tooltip("Seconds before the body disappears.")]
        [SerializeField] private float timeBeforeDestroy = 1f;

        [Tooltip("Seconds before its loot drops.")]
        [SerializeField] private float timeBeforeLootDrop = 0.5f;

        [Tooltip("Skip the death animation entirely.")]
        [SerializeField] private bool skipDeathAnimation;

        public DimensionCreatureStatSource StatSource
        {
            get { return statSource; }
        }

        /// <summary>Whether the author has taken full control of the numbers.</summary>
        public bool UsesAuthoredNumbers
        {
            get { return statSource == DimensionCreatureStatSource.Authored; }
        }

        public int MaxHealth
        {
            get { return maxHealth < 1 ? 1 : maxHealth; }
        }

        public float StartHealthFraction
        {
            get { return Mathf.Clamp(startHealthFraction, 0.01f, 1f); }
        }

        /// <summary>Whether it spawns already hurt, which is worth knowing before writing the field.</summary>
        public bool HasStartHealthOverride
        {
            get { return StartHealthFraction < 0.999f; }
        }

        public int DamageReduction
        {
            get { return damageReduction < 0 ? 0 : damageReduction; }
        }

        public int MaxDamagePerHit
        {
            get { return maxDamagePerHit < 0 ? 0 : maxDamagePerHit; }
        }

        public int MinDamagePerHit
        {
            get { return minDamagePerHit < 0 ? 0 : minDamagePerHit; }
        }

        public int Damage
        {
            get { return damage < 0 ? 0 : damage; }
        }

        public float DamageMultiplier
        {
            get { return damageMultiplier < 0f ? 0f : damageMultiplier; }
        }

        public float MoveSpeed
        {
            get { return moveSpeed < 0f ? 0f : moveSpeed; }
        }

        public float DetectionRadius
        {
            get { return detectionRadius < 0f ? 0f : detectionRadius; }
        }

        public bool HasDetectionRadius
        {
            get { return DetectionRadius > 0f; }
        }

        /// <summary>
        /// How wide the creature's own body is, in tiles.
        /// </summary>
        /// <remarks>
        /// It becomes the Category04 ball's radius at 0.375 per tile, which is the commonest
        /// vanilla creature body — 31 of the 64 that carry one. Before this control existed both
        /// call sites handed in a hard 1, so a five-tile boss and a rat were the same 0.75 across.
        /// </remarks>
        public float BodyWidthInTiles
        {
            get { return bodyWidthInTiles > 0f ? bodyWidthInTiles : 1f; }
        }

        /// <summary>
        /// How wide the creature looks to whatever watches for one nearby.
        /// </summary>
        /// <remarks>
        /// The Category03 trigger, sized separately from the body. 63 vanilla creatures carry both
        /// shapes and 24 of them use two different radii — <c>LarvaEntity</c> is 0.25 against
        /// 0.375 — so welding the two together made that arrangement unbuildable. Zero keeps them
        /// the same, which is the other 39.
        /// </remarks>
        public float NoticedWidthInTiles
        {
            get { return noticedWidthInTiles > 0f ? noticedWidthInTiles : BodyWidthInTiles; }
        }

        public float ChaseAtDistance
        {
            get { return chaseAtDistance < 0f ? 0f : chaseAtDistance; }
        }

        public bool HasChaseAtDistance
        {
            get { return ChaseAtDistance > 0f; }
        }

        public float ChaseSpeedMultiplier
        {
            get { return chaseSpeedMultiplier < 0f ? 0f : chaseSpeedMultiplier; }
        }

        public bool HasChaseSpeedMultiplier
        {
            get { return ChaseSpeedMultiplier > 0f; }
        }

        public bool OverrideDeathTiming
        {
            get { return overrideDeathTiming; }
        }

        public float TimeBeforeDestroy
        {
            get { return timeBeforeDestroy < 0f ? 0f : timeBeforeDestroy; }
        }

        public float TimeBeforeLootDrop
        {
            get { return timeBeforeLootDrop < 0f ? 0f : timeBeforeLootDrop; }
        }

        public bool SkipDeathAnimation
        {
            get { return skipDeathAnimation; }
        }

        /// <summary>
        /// A copy of these stats scaled into an elite's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Produces a plain authored stat block whatever the parent used, and that is deliberate. An
        /// elite derived from a level-scaled parent would be scaled twice — once by the curve and once
        /// by the multiplier — and the number the author sees in the dashboard would not be the number
        /// the game uses. Resolving the parent's health first makes the elite's total explicit.
        /// </para>
        /// <para>
        /// Written as a factory rather than mutating in place because both stat blocks have to exist
        /// at once: the ordinary creature and its elite are two prefabs generated in the same pass.
        /// </para>
        /// <para>
        /// A LEVEL-SCALED PARENT KEEPS ITS CURVE. Turning one into fixed numbers resolves
        /// the parent's health as if it were level 1 and multiplies that — which makes the elite of
        /// a deep-biome creature WEAKER than the creature it is meant to be a harder copy of, and
        /// drops its <c>AreaLevelAuthoring</c> so its attack damage stops scaling too. The
        /// level-scaled path stays level-scaled, and the elite's extra strength rides on the
        /// rarity level bump instead, which is how Core Keeper says the same thing.
        /// </para>
        /// </remarks>
        public DimensionCreatureStatsTemplate ScaledForElite(
            DimensionEliteVariantTemplate elite,
            int areaLevel,
            bool isEnemy,
            bool isBoss)
        {
            DimensionCreatureStatsTemplate scaled = new DimensionCreatureStatsTemplate();
            if (elite == null)
            {
                return scaled;
            }

            scaled.statSource = statSource;
            if (UsesAuthoredNumbers)
            {
                scaled.maxHealth = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        ResolveMaxHealth(areaLevel, isEnemy, isBoss) * elite.HealthMultiplier));
            }

            scaled.startHealthFraction = StartHealthFraction;
            scaled.damageReduction = DamageReduction + elite.ExtraDamageReduction;
            scaled.maxDamagePerHit = MaxDamagePerHit;
            scaled.minDamagePerHit = MinDamagePerHit;
            scaled.damage = Mathf.RoundToInt(Damage * elite.DamageMultiplier);
            scaled.damageMultiplier = DamageMultiplier * elite.DamageMultiplier;
            scaled.moveSpeed = MoveSpeed * elite.SpeedMultiplier;
            scaled.detectionRadius = detectionRadius;
            scaled.chaseAtDistance = chaseAtDistance;
            scaled.chaseSpeedMultiplier = chaseSpeedMultiplier;
            scaled.overrideDeathTiming = overrideDeathTiming;
            scaled.timeBeforeDestroy = timeBeforeDestroy;
            scaled.timeBeforeLootDrop = timeBeforeLootDrop;
            scaled.skipDeathAnimation = skipDeathAnimation;
            return scaled;
        }

        /// <summary>
        /// The health this creature will actually have in game.
        /// </summary>
        /// <remarks>
        /// Reproduces Core Keeper's own curves for the level-scaled mode so the dashboard can show the
        /// real number rather than the multiplier. Seeing "level 3 boss: 4,133 health" before building
        /// is the difference between choosing a difficulty and guessing at one.
        /// </remarks>
        public int ResolveMaxHealth(int areaLevel, bool isEnemy, bool isBoss)
        {
            if (UsesAuthoredNumbers)
            {
                return MaxHealth;
            }

            int level = areaLevel < 1 ? 1 : areaLevel;
            if (isBoss)
            {
                return Mathf.Max(1, Mathf.RoundToInt(300f * Mathf.Pow(level, 2.45f)));
            }

            if (isEnemy)
            {
                return Mathf.Max(1, Mathf.RoundToInt(135f + 15f * Mathf.Pow(level - 1, 2f)));
            }

            return Mathf.Max(1, Mathf.RoundToInt(100f + 50f * Mathf.Pow(level - 1, 1.2f)));
        }
    }
}
