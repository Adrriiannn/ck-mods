using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>How a creature fights.</summary>
    /// <remarks>
    /// Measured across the 115 prefabs carrying <c>EnemyAuthoring</c>: melee and ranged are far and
    /// away the two Core Keeper actually uses, and a fair number of creatures carry both. The rarer
    /// states — charge, jump, mortar — are separate components rather than variations on these, so
    /// they belong in their own question rather than crammed in here as a longer list.
    /// </remarks>
    public enum DimensionCreatureAttackKind
    {
        /// <summary>It never attacks. For critters and anything purely decorative.</summary>
        None = 0,

        /// <summary>It hits things next to it.</summary>
        Melee = 1,

        /// <summary>It shoots things from a distance.</summary>
        Ranged = 2,

        /// <summary>It does both, picking whichever suits the distance.</summary>
        MeleeAndRanged = 3
    }

    /// <summary>What a creature does when nothing is happening.</summary>
    /// <remarks>
    /// <c>RandomWalkState</c> is what vanilla enemies actually wander with — it is the third most
    /// common behaviour component on enemies, while <c>RoamingState</c> is on four prefabs in the
    /// whole game. A creature with neither simply stands where it spawned, which is right for a
    /// turret or a plant and wrong for almost anything with legs.
    /// </remarks>
    public enum DimensionCreatureIdleMovement
    {
        /// <summary>It stays exactly where it spawned.</summary>
        StandStill = 0,

        /// <summary>It wanders a short distance around where it spawned.</summary>
        WanderNearby = 1
    }

    /// <summary>
    /// How a creature notices things, moves about, and fights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Creature authoring already covered health, damage reduction, movement speed
    /// and chasing — but nothing that lets a creature actually <em>hit</em> anything. A generated
    /// creature would run at the player and then stand there. Core Keeper puts the attack on its own
    /// component (<c>MeleeAttackStateAuthoring</c> / <c>RangeAttackStateAuthoring</c>), and neither
    /// was being written.
    /// </para>
    /// <para>
    /// The field set is taken from a real enemy rather than invented: <c>CavelingBruteEntity</c>
    /// carries 29 components, and the ones that make it a functioning melee enemy are perception
    /// (<c>NearbyEntitiesTracker</c>), hostility (<c>Faction</c> + <c>BehaviourTags</c>), a wander
    /// state, a chase state, and a melee attack. Those are the questions below.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureCombatTemplate
    {
        [Header("Robot patroller")]
        [Tooltip("Only read by the robot patroller behaviour: how hard its oil mortar hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float oilMortarMultiplier = 1f;

        [Tooltip("And how much terrain that oil mortar breaks.")]
        [Min(0f)]
        [SerializeField] private float oilMortarTerrainMultiplier = 1f;

        [Tooltip("Flat oil mortar damage, when there is no tier to scale from.")]
        [Min(0)]
        [SerializeField] private int oilMortarFlatDamage;

        [Tooltip("Flat oil mortar terrain damage.")]
        [Min(0)]
        [SerializeField] private int oilMortarFlatTerrainDamage;

        [Tooltip("How hard its fire mortar hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float fireMortarMultiplier = 1f;

        [Tooltip("And how much terrain that fire mortar breaks.")]
        [Min(0f)]
        [SerializeField] private float fireMortarTerrainMultiplier = 1f;

        [Tooltip("Flat fire mortar damage.")]
        [Min(0)]
        [SerializeField] private int fireMortarFlatDamage;

        [Tooltip("Flat fire mortar terrain damage.")]
        [Min(0)]
        [SerializeField] private int fireMortarFlatTerrainDamage;

        [Header("Noticing things")]
        [Tooltip("Which physics layers it notices things on. Leave it at 0 and it watches the " +
            "layer the game's own creatures watch.")]
        [Min(0)]
        [SerializeField] private int noticesThingsOnLayers;

        [Tooltip("It re-checks what is nearby every frame instead of on the usual cooldown.")]
        [SerializeField] private bool checksForThingsEveryFrame;

        [Tooltip("How far away it spots something worth reacting to.")]
        [Min(0f)]
        [SerializeField] private float detectionRadius = 12f;

        [Tooltip("How close counts as 'in melee range of me' for the game's own combat checks.")]
        [Min(0f)]
        [SerializeField] private float combatRadius = 0.5f;

        [Tooltip("Which side it is on. A FactionID name — Caveling, Slime, Neutral, OnlyAttacksPlayer, and so on.")]
        [SerializeField] private string factionId = "OnlyAttacksPlayer";

        // The old tooltip here said "leave empty to use only the faction", which is false: the
        // faction check runs in ADDITION to the tag check, never instead of it, so an empty list
        // means it chooses nobody at all (`ck-db\Pug.Other\ChaseStateRequest.cs:216`).
        [Tooltip("Category tags it will go after — Player and HostileCreature are what the game's " +
                 "own enemies list. Leaving this empty means it chooses nobody: its faction " +
                 "cannot carry that on its own. Temperament fills this in for you unless you set " +
                 "it to Custom.")]
        [SerializeField] private string[] wantsToAttackTags = new string[0];

        [Tooltip("Category tags it will never go after, even when the faction says otherwise. " +
                 "This is the list that stops it swinging at something already next to it, so it " +
                 "is what actually makes a creature harmless.")]
        [SerializeField] private string[] cantAttackTags = new string[0];

        // WITHOUT THIS A CREATURE CANNOT EAT. BehaviourTagsCD.Eats is a bit test against this
        // list, and every route into eating asks it first: EatStateRequest looks for food the
        // creature is willing to eat, breeding will not start until the creature has eaten its
        // fill, and evolving counts the meals. Shipped empty with no way to
        // fill it, the eat, breed and evolve answers generate cleanly and never once fire.
        [Tooltip("Category tags it will eat. CattlePlantFood is what the game's own animals eat. " +
                 "Leaving this empty means it eats nothing, so eating, breeding and evolving " +
                 "never happen.")]
        [SerializeField] private string[] eatsTags = new string[0];

        [Header("When nothing is happening")]
        [SerializeField] private DimensionCreatureIdleMovement idleMovement = DimensionCreatureIdleMovement.WanderNearby;

        [Tooltip("Shortest wander step.")]
        [Min(0f)]
        [SerializeField] private float minWanderDistance = 0.5f;

        [Tooltip("Longest wander step.")]
        [Min(0f)]
        [SerializeField] private float maxWanderDistance = 2f;

        [Tooltip("How long it pauses between wander steps, at least.")]
        [Min(0f)]
        [SerializeField] private float minWanderPause = 0.5f;

        [Tooltip("How long it pauses between wander steps, at most.")]
        [Min(0f)]
        [SerializeField] private float maxWanderPause = 1f;

        [Tooltip("A walk pattern asset to follow instead of plain wandering. None for plain.")]
        [SerializeField] private WalkPatternBehaviourDefinition walkPattern;

        [Tooltip("Longest it walks in one go before stopping to idle.")]
        [Min(0f)]
        [SerializeField] private float maxWanderDuration = 3f;

        [Tooltip("Its own wander numbers win over any walk pattern it was given.")]
        [SerializeField] private bool ownNumbersBeatTheWalkPattern;

        [Tooltip("Speed while wandering, relative to its normal speed.")]
        [Min(0f)]
        [SerializeField] private float wanderSpeedMultiplier = 1f;

        [Header("Fighting")]
        [Tooltip("Whether it hits things next to it, shoots them from a distance, or does both.")]
        [SerializeField] private DimensionCreatureAttackKind attackKind = DimensionCreatureAttackKind.Melee;

        [Header("Melee")]
        [Tooltip("Damage per hit. Leave at 0 to let the area level decide it, the way vanilla enemies do.")]
        [Min(0)]
        [SerializeField] private int meleeDamage;

        [Tooltip("How close it has to get before it will try to swing at all.")]
        [Min(0f)]
        [SerializeField] private float meleeReach = 1f;

        [Tooltip("How far in front of itself the swing actually lands. Every creature in the game sets this shorter than the distance it closes to.")]
        [Min(0f)]
        [SerializeField] private float meleeSwingLandsAhead;

        [Tooltip("How wide the swing is.")]
        [Min(0f)]
        [SerializeField] private float meleeHitRadius = 0.75f;

        [Tooltip("Wind-up before the hit lands. This is the window a player can react in, so it is the single biggest feel dial.")]
        [Min(0f)]
        [SerializeField] private float meleeWindUp = 0.35f;

        [Tooltip("How long the swing itself lasts.")]
        [Min(0f)]
        [SerializeField] private float meleeSwingDuration = 0.3f;

        [Tooltip("Shortest wait between swings.")]
        [Min(0f)]
        [SerializeField] private float meleeMinCooldown = 0.5f;

        [Tooltip("Longest wait between swings.")]
        [Min(0f)]
        [SerializeField] private float meleeMaxCooldown = 1f;

        [Tooltip("How many hits one swing deals.")]
        [Min(1)]
        [SerializeField] private int meleeHits = 1;

        [Tooltip("How hard it shoves what it hits.")]
        [Min(0f)]
        [SerializeField] private float meleePushForce;

        [Tooltip("Its swings also break terrain.")]
        [SerializeField] private bool meleeBreaksTiles;

        [Tooltip("Damage its swings do to terrain. Only matters when it breaks tiles.")]
        [Min(0)]
        [SerializeField] private int meleeTileDamage;

        [Header("Ranged")]
        [Tooltip("What it shoots. An ObjectID name, or one of your own projectiles.")]
        [SerializeField] private string projectileItemId = string.Empty;

        [Tooltip("Damage per projectile. Leave at 0 to let the area level decide it.")]
        [Min(0)]
        [SerializeField] private int rangedDamage;

        [Tooltip("It will not shoot closer than this — it backs off or switches to melee instead.")]
        [Min(0f)]
        [SerializeField] private float rangedMinDistance = 2f;

        [Tooltip("It will not shoot further than this.")]
        [Min(0f)]
        [SerializeField] private float rangedMaxDistance = 10f;

        [Tooltip("Wind-up before it shoots.")]
        [Min(0f)]
        [SerializeField] private float rangedWindUp = 0.4f;

        [Tooltip("Shortest wait between shots.")]
        [Min(0f)]
        [SerializeField] private float rangedMinCooldown = 1f;

        [Tooltip("Longest wait between shots.")]
        [Min(0f)]
        [SerializeField] private float rangedMaxCooldown = 2f;

        [Tooltip("Projectiles loosed per shot. More than one makes a spread.")]
        [Min(1)]
        [SerializeField] private int projectilesPerShot = 1;

        [Tooltip("How wide that spread is, in degrees.")]
        [Min(0f)]
        [SerializeField] private float spreadAngle;

        [Tooltip("Gap between projectiles when a shot fires several.")]
        [Min(0f)]
        [SerializeField] private float timeBetweenShots;

        [Header("What it shrugs off")]
        [Tooltip("Nothing pushes it back.")]
        [SerializeField] private bool immuneToPushBack;

        [Tooltip("Arrows and bolts do not reach it.")]
        [SerializeField] private bool immuneToRangedDamage;

        [Header("What it leaves standing")]
        [Tooltip("A whole object left in the world when it dies. Not loot.")]
        [SerializeField] private DimensionLeavesBehindTemplate leavesBehind = new DimensionLeavesBehindTemplate();

        [Header("Gravity wells")]
        [Tooltip("Gravity wells pull it off course as it wanders. 0 means they never do.")]
        [Range(0f, 1f)]
        [SerializeField] private float gravityWellChance;

        [Tooltip("How hard a well pulls it.")]
        [Min(0f)]
        [SerializeField] private float gravityWellStrength = 1f;

        [Tooltip("How close a well has to be to reach it.")]
        [Min(0f)]
        [SerializeField] private float gravityWellRange = 10f;

        [Tooltip("Which physics layers the gravity well pulls on. 0 leaves the game's own choice.")]
        [Min(0)]
        [SerializeField] private int gravityWellAttractsLayers;

        [Tooltip("How far off its heading a well may bend it, in degrees.")]
        [Min(0f)]
        [SerializeField] private float gravityWellMaxTurn = 45f;

        [Header("More ways it fights")]
        [Tooltip("A sweeping ray, hurting on contact, a shield, placing objects, orbiting its owner.")]
        [SerializeField] private DimensionMoreCombatTemplate moreCombat = new DimensionMoreCombatTemplate();

        [Header("Smashing through the world")]
        [Tooltip("Whether it attacks walls and buildings in its way to reach you.")]
        [SerializeField] private DimensionSmashesObjectsTemplate smashesObjects = new DimensionSmashesObjectsTemplate();

        [Header("If it is a worm or serpent")]
        [Tooltip("A body that follows its head, with a trail, a tail, and a way of weaving.")]
        [SerializeField] private DimensionSegmentedCreatureTemplate segmented = new DimensionSegmentedCreatureTemplate();

        [Header("Where it walks")]
        [Tooltip("A route it walks across the world, rather than milling around where it spawned.")]
        [SerializeField] private DimensionPatrolPathTemplate patrol = new DimensionPatrolPathTemplate();

        [Header("Its habits")]
        [Tooltip("How it loiters, taunts, guards a nest, and whether it can be kept or fed.")]
        [SerializeField] private DimensionCreatureHabitsTemplate habits = new DimensionCreatureHabitsTemplate();

        [Header("How it arrives, idles and leaves")]
        [Tooltip("Its entrance, its idle animations, party scaling, and when it despawns.")]
        [SerializeField] private DimensionCreatureLifecycleTemplate lifecycle = new DimensionCreatureLifecycleTemplate();

        [Header("Where it sits in the world")]
        [Tooltip("Its world tier and which ways its art faces. The tier drives the stat curves.")]
        [SerializeField] private DimensionObjectBasicsTemplate basics = new DimensionObjectBasicsTemplate();

        [Header("Conditions")]
        [Tooltip("What it starts affected by, and what conditions cannot touch it.")]
        [SerializeField] private DimensionInitialConditionsTemplate conditions = new DimensionInitialConditionsTemplate();

        [Header("Company")]
        [Tooltip("Creatures from this mod that appear alongside it.")]
        [SerializeField] private string[] arrivesWith = new string[0];

        [Tooltip("The company follows it around rather than staying where they appeared.")]
        [SerializeField] private bool companyFollows = true;

        [Tooltip("Where its company appears relative to it.")]
        [SerializeField] private Vector3 companyAppearsAt = Vector3.zero;

        [Header("What its attacks sound like")]
        [Tooltip("Its own attack sounds. Leave at 0 to sound like the game would have made it sound.")]
        [SerializeField] private DimensionAttackSoundsTemplate attackSounds = new DimensionAttackSoundsTemplate();

        [Header("How it comes at you")]
        [Tooltip("How it closes on its target, and how far back it hangs.")]
        [SerializeField] private DimensionPursuitTemplate pursuit = new DimensionPursuitTemplate();

        [Tooltip("The shot pattern it fires in.")]
        [SerializeField] private DimensionRangedShapeTemplate rangedShape = new DimensionRangedShapeTemplate();

        [Tooltip("The shape, timing and force of its melee swing.")]
        [SerializeField] private DimensionMeleeShapeTemplate meleeShape = new DimensionMeleeShapeTemplate();

        [Header("Abilities")]
        [Tooltip("Everything else it can do — charging, leaping, exploding, enraging, sleeping, breeding.")]
        [SerializeField] private DimensionCreatureAbility[] abilities = new DimensionCreatureAbility[0];

        // ---- Answers a creature shares with the things a player PLACES ------------------------
        //
        // Every one of the eight below is written by a method the world-object generator also
        // calls. A beam, a healing aura, mana, an owner, hiding
        // in bushes, roaming a circuit, hurting whatever comes near, and a shop are all buildable,
        // and nearly all of them belong on a creature — so these fields are what reaches them from
        // one. Nesting them here rather than on the mob asset is what reaches mobs, animals and
        // bosses in one go, because all three carry this template.
        //
        // ORDER MATTERS FOR WHAT IS WRITTEN FROM THESE. The generator calls them after the habits
        // and abilities and before the temperament, so the temperament still has the last word on
        // the attack tags and the chase distance, and the companion sweep still runs last of all
        // and sees everything they added.

        [Header("Firing a beam, calling out, and dripping")]
        [Tooltip("A sweeping beam, an alert it plays when it notices you, things it drips, and " +
                 "whether it can be set alight.")]
        [SerializeField] private DimensionBeamAndAmbienceTemplate beamAndAmbience =
            new DimensionBeamAndAmbienceTemplate();

        [Header("Hiding and hatching")]
        [Tooltip("Hiding in bushes and peeking out, being an egg that hatches, and claiming a " +
                 "caveling territory.")]
        [SerializeField] private DimensionHidingAndHatchingTemplate hidingAndHatching =
            new DimensionHidingAndHatchingTemplate();

        [Header("Mana, auras and who it belongs to")]
        [Tooltip("A mana pool, siphoning, healing what is near it, and which creature it is a " +
                 "part of or belongs to.")]
        [SerializeField] private DimensionManaAndAuraTemplate manaAndAura =
            new DimensionManaAndAuraTemplate();

        [Header("Reacting and leaving a scent")]
        [Tooltip("What its own wounds do to it, the scent it leaves behind, and the odds and ends.")]
        [SerializeField] private DimensionFinalTouchesTemplate finalTouches =
            new DimensionFinalTouchesTemplate();

        [Header("Roaming and wandering")]
        [Tooltip("Roaming a circuit and chewing the ground as it goes, or wandering near " +
                 "something. Roaming needs a route under 'Where it walks'.")]
        [SerializeField] private DimensionSpawnerAndOrbTemplate spawnerAndOrb =
            new DimensionSpawnerAndOrbTemplate();

        [Header("Its shop")]
        [Tooltip("What it sells, if a player can trade with it.")]
        [SerializeField] private DimensionTraderTemplate trader = new DimensionTraderTemplate();

        [Header("Hurting whatever comes near it")]
        [Tooltip("A creature that damages anything that gets close, without swinging at it.")]
        [SerializeField] private DimensionContinuousAttackTemplate continuousAttack =
            new DimensionContinuousAttackTemplate();

        [Header("Walking up to it")]
        [Tooltip("How close a player has to be, and how high its name floats, for a creature that " +
                 "can be tended or traded with.")]
        [SerializeField] private DimensionCreatureTendingTemplate tending =
            new DimensionCreatureTendingTemplate();

        [Header("If it belongs to somebody")]
        [Tooltip("It is something summoned, and fights for whoever summoned it.")]
        [SerializeField] private DimensionCreatureMinionTemplate minion =
            new DimensionCreatureMinionTemplate();

        [Header("When it would have died")]
        [Tooltip("Whether it drops at zero health instead of dying.")]
        [SerializeField] private DimensionCreatureLastStandTemplate lastStand =
            new DimensionCreatureLastStandTemplate();

        public DimensionPursuitTemplate Pursuit
        {
            get { return pursuit ?? (pursuit = new DimensionPursuitTemplate()); }
        }

        public DimensionRangedShapeTemplate RangedShape
        {
            get { return rangedShape ?? (rangedShape = new DimensionRangedShapeTemplate()); }
        }

        public DimensionMeleeShapeTemplate MeleeShape
        {
            get { return meleeShape ?? (meleeShape = new DimensionMeleeShapeTemplate()); }
        }

        public float OilMortarMultiplier { get { return oilMortarMultiplier < 0f ? 0f : oilMortarMultiplier; } }

        public float OilMortarTerrainMultiplier
        {
            get { return oilMortarTerrainMultiplier < 0f ? 0f : oilMortarTerrainMultiplier; }
        }

        public int OilMortarFlatDamage { get { return oilMortarFlatDamage < 0 ? 0 : oilMortarFlatDamage; } }

        public int OilMortarFlatTerrainDamage
        {
            get { return oilMortarFlatTerrainDamage < 0 ? 0 : oilMortarFlatTerrainDamage; }
        }

        public float FireMortarMultiplier
        {
            get { return fireMortarMultiplier < 0f ? 0f : fireMortarMultiplier; }
        }

        public float FireMortarTerrainMultiplier
        {
            get { return fireMortarTerrainMultiplier < 0f ? 0f : fireMortarTerrainMultiplier; }
        }

        public int FireMortarFlatDamage
        {
            get { return fireMortarFlatDamage < 0 ? 0 : fireMortarFlatDamage; }
        }

        public int FireMortarFlatTerrainDamage
        {
            get { return fireMortarFlatTerrainDamage < 0 ? 0 : fireMortarFlatTerrainDamage; }
        }

        public int NoticesThingsOnLayers
 { get { return noticesThingsOnLayers < 0 ? 0 : noticesThingsOnLayers; } }

        public bool ChecksForThingsEveryFrame { get { return checksForThingsEveryFrame; } }

        public float DetectionRadius { get { return detectionRadius < 0f ? 0f : detectionRadius; } }

        public float CombatRadius { get { return combatRadius < 0f ? 0f : combatRadius; } }

        public string FactionId { get { return factionId ?? string.Empty; } }

        public string[] WantsToAttackTags { get { return wantsToAttackTags ?? new string[0]; } }

        public string[] CantAttackTags { get { return cantAttackTags ?? new string[0]; } }

        /// <summary>What it is willing to eat.</summary>
        public string[] EatsTags { get { return eatsTags ?? new string[0]; } }

        public DimensionCreatureIdleMovement IdleMovement { get { return idleMovement; } }

        public bool WandersWhenIdle
        {
            get { return idleMovement == DimensionCreatureIdleMovement.WanderNearby; }
        }

        public float MinWanderDistance { get { return minWanderDistance < 0f ? 0f : minWanderDistance; } }

        /// <summary>Longest wander step, never shorter than the shortest one.</summary>
        public float MaxWanderDistance
        {
            get { return maxWanderDistance < MinWanderDistance ? MinWanderDistance : maxWanderDistance; }
        }

        public float MinWanderPause { get { return minWanderPause < 0f ? 0f : minWanderPause; } }

        public float MaxWanderPause
        {
            get { return maxWanderPause < MinWanderPause ? MinWanderPause : maxWanderPause; }
        }

        public WalkPatternBehaviourDefinition WalkPattern { get { return walkPattern; } }

        public float MaxWanderDuration { get { return maxWanderDuration < 0f ? 0f : maxWanderDuration; } }

        public bool OwnNumbersBeatTheWalkPattern { get { return ownNumbersBeatTheWalkPattern; } }

        public float WanderSpeedMultiplier
        {
            get { return wanderSpeedMultiplier <= 0f ? 1f : wanderSpeedMultiplier; }
        }

        public DimensionCreatureAttackKind AttackKind { get { return attackKind; } }

        public bool HasMelee
        {
            get
            {
                return attackKind == DimensionCreatureAttackKind.Melee
                    || attackKind == DimensionCreatureAttackKind.MeleeAndRanged;
            }
        }

        public bool HasRanged
        {
            get
            {
                return attackKind == DimensionCreatureAttackKind.Ranged
                    || attackKind == DimensionCreatureAttackKind.MeleeAndRanged;
            }
        }

        public int MeleeDamage { get { return meleeDamage < 0 ? 0 : meleeDamage; } }

        /// <summary>Whether the area level decides the melee damage instead of a fixed number.</summary>
        /// <remarks>
        /// Vanilla leaves this to the level almost everywhere: <c>MeleeAttackStateAuthoring.OnValidate</c>
        /// overwrites <c>meleeDamage</c> from <c>AreaLevelAuthoring</c> whenever one is present, so an
        /// authored number only survives on a creature that has no area level. Saying zero means "do
        /// what vanilla does" rather than "deal no damage".
        /// </remarks>
        public bool MeleeDamageFromLevel { get { return MeleeDamage == 0; } }

        public float MeleeReach { get { return meleeReach < 0f ? 0f : meleeReach; } }

        public float MeleeSwingLandsAhead
        {
            get { return meleeSwingLandsAhead < 0f ? 0f : meleeSwingLandsAhead; }
        }

        public float MeleeHitRadius { get { return meleeHitRadius < 0f ? 0f : meleeHitRadius; } }

        public float MeleeWindUp { get { return meleeWindUp < 0f ? 0f : meleeWindUp; } }

        public float MeleeSwingDuration { get { return meleeSwingDuration < 0f ? 0f : meleeSwingDuration; } }

        public float MeleeMinCooldown { get { return meleeMinCooldown < 0f ? 0f : meleeMinCooldown; } }

        public float MeleeMaxCooldown
        {
            get { return meleeMaxCooldown < MeleeMinCooldown ? MeleeMinCooldown : meleeMaxCooldown; }
        }

        public int MeleeHits { get { return meleeHits < 1 ? 1 : meleeHits; } }

        public float MeleePushForce { get { return meleePushForce < 0f ? 0f : meleePushForce; } }

        public bool MeleeBreaksTiles { get { return meleeBreaksTiles; } }

        public int MeleeTileDamage
        {
            get { return !meleeBreaksTiles || meleeTileDamage < 0 ? 0 : meleeTileDamage; }
        }

        public string ProjectileItemId { get { return projectileItemId ?? string.Empty; } }

        public int RangedDamage { get { return rangedDamage < 0 ? 0 : rangedDamage; } }

        /// <summary>Whether the area level decides the ranged damage instead of a fixed number.</summary>
        public bool RangedDamageFromLevel { get { return RangedDamage == 0; } }

        public float RangedMinDistance { get { return rangedMinDistance < 0f ? 0f : rangedMinDistance; } }

        public float RangedMaxDistance
        {
            get { return rangedMaxDistance < RangedMinDistance ? RangedMinDistance : rangedMaxDistance; }
        }

        public float RangedWindUp { get { return rangedWindUp < 0f ? 0f : rangedWindUp; } }

        public float RangedMinCooldown { get { return rangedMinCooldown < 0f ? 0f : rangedMinCooldown; } }

        public float RangedMaxCooldown
        {
            get { return rangedMaxCooldown < RangedMinCooldown ? RangedMinCooldown : rangedMaxCooldown; }
        }

        public int ProjectilesPerShot { get { return projectilesPerShot < 1 ? 1 : projectilesPerShot; } }

        public float SpreadAngle { get { return spreadAngle < 0f ? 0f : spreadAngle; } }

        public float TimeBetweenShots { get { return timeBetweenShots < 0f ? 0f : timeBetweenShots; } }

        /// <summary>Whether it was told to shoot but given nothing to shoot.</summary>
        /// <remarks>
        /// The range attack state runs perfectly happily with no projectile and simply never produces
        /// one, so this fails as "the creature does nothing at range" rather than as an error.
        /// </remarks>
        public bool RangedIsMissingProjectile
        {
            get { return HasRanged && string.IsNullOrEmpty(ProjectileItemId); }
        }

        /// <summary>Whether it can see far enough to reach anything it is meant to shoot.</summary>
        /// <remarks>
        /// A creature whose detection radius is shorter than its minimum firing distance can never be
        /// in a position to shoot: by the time it notices you, you are already too close.
        /// </remarks>
        public bool CannotSeeFarEnoughToShoot
        {
            get { return HasRanged && DetectionRadius > 0f && DetectionRadius < RangedMinDistance; }
        }

        /// <summary>Whether it will chase but never be able to act.</summary>
        /// <remarks>
        /// An ability that deals damage counts: a creature with no melee or ranged attack but a charge
        /// or an explosion is perfectly capable of hurting somebody, so it is not the dead-end this
        /// flags.
        /// </remarks>
        public bool ChasesButCannotAttack
        {
            get
            {
                if (attackKind != DimensionCreatureAttackKind.None)
                {
                    return false;
                }

                DimensionCreatureAbility[] all = Abilities;
                for (int i = 0; i < all.Length; i++)
                {
                    switch (all[i].Kind)
                    {
                        case DimensionCreatureAbilityKind.ChargeAttack:
                        case DimensionCreatureAbilityKind.JumpAttack:
                        case DimensionCreatureAbilityKind.MortarShot:
                        case DimensionCreatureAbilityKind.Explode:
                            return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Everything else it can do, with the empty entries dropped.</summary>
        /// <summary>Whether nothing pushes it back.</summary>
        public bool ImmuneToPushBack { get { return immuneToPushBack; } }

        /// <summary>Whether ranged damage does not reach it.</summary>
        public bool ImmuneToRangedDamage { get { return immuneToRangedDamage; } }

        /// <summary>How often a gravity well catches it, 0 to 1.</summary>
        public float GravityWellChance
        {
            get
            {
                if (gravityWellChance < 0f)
                {
                    return 0f;
                }

                return gravityWellChance > 1f ? 1f : gravityWellChance;
            }
        }

        /// <summary>Whether gravity wells affect it at all.</summary>
        public bool GravityWellsAffectIt { get { return GravityWellChance > 0f; } }

        public float GravityWellStrength { get { return gravityWellStrength < 0f ? 0f : gravityWellStrength; } }
        public int GravityWellAttractsLayers
        {
            get { return gravityWellAttractsLayers < 0 ? 0 : gravityWellAttractsLayers; }
        }

        public float GravityWellRange { get { return gravityWellRange < 0f ? 0f : gravityWellRange; } }
        public float GravityWellMaxTurn { get { return gravityWellMaxTurn < 0f ? 0f : gravityWellMaxTurn; } }

        /// <summary>The creatures that appear alongside it, with blanks dropped.</summary>
        public string[] ArrivesWith
        {
            get
            {
                string[] all = arrivesWith ?? new string[0];
                System.Collections.Generic.List<string> kept =
                    new System.Collections.Generic.List<string>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (!string.IsNullOrEmpty(all[i]))
                    {
                        kept.Add(all[i]);
                    }
                }

                return kept.ToArray();
            }
        }

        /// <summary>Its world tier and which ways its art faces.</summary>
        public DimensionObjectBasicsTemplate Basics
        {
            get { return basics ?? new DimensionObjectBasicsTemplate(); }
        }

        /// <summary>What it starts affected by, and what cannot touch it.</summary>
        public DimensionInitialConditionsTemplate Conditions
        {
            get { return conditions ?? new DimensionInitialConditionsTemplate(); }
        }

        /// <summary>How it arrives, idles, scales with the party and leaves.</summary>
        public DimensionMoreCombatTemplate MoreCombat
        {
            get { return moreCombat ?? (moreCombat = new DimensionMoreCombatTemplate()); }
        }

        public DimensionSmashesObjectsTemplate SmashesObjects
        {
            get { return smashesObjects ?? (smashesObjects = new DimensionSmashesObjectsTemplate()); }
        }

        public DimensionSegmentedCreatureTemplate Segmented
        {
            get { return segmented ?? (segmented = new DimensionSegmentedCreatureTemplate()); }
        }

        public DimensionPatrolPathTemplate Patrol
        {
            get { return patrol ?? (patrol = new DimensionPatrolPathTemplate()); }
        }

        public DimensionCreatureHabitsTemplate Habits
        {
            get { return habits ?? (habits = new DimensionCreatureHabitsTemplate()); }
        }

        public DimensionCreatureLifecycleTemplate Lifecycle
        {
            get { return lifecycle ?? new DimensionCreatureLifecycleTemplate(); }
        }

        /// <summary>A sweeping beam, an alert, drips, and being set alight.</summary>
        public DimensionBeamAndAmbienceTemplate BeamAndAmbience
        {
            get { return beamAndAmbience ?? (beamAndAmbience = new DimensionBeamAndAmbienceTemplate()); }
        }

        /// <summary>Hiding in bushes, hatching, and claiming a territory.</summary>
        public DimensionHidingAndHatchingTemplate HidingAndHatching
        {
            get
            {
                return hidingAndHatching ??
                    (hidingAndHatching = new DimensionHidingAndHatchingTemplate());
            }
        }

        /// <summary>Mana, siphoning, healing what is near it, and who it belongs to.</summary>
        public DimensionManaAndAuraTemplate ManaAndAura
        {
            get { return manaAndAura ?? (manaAndAura = new DimensionManaAndAuraTemplate()); }
        }

        /// <summary>What its own wounds do to it, and the scent it leaves.</summary>
        public DimensionFinalTouchesTemplate FinalTouches
        {
            get { return finalTouches ?? (finalTouches = new DimensionFinalTouchesTemplate()); }
        }

        /// <summary>Roaming a circuit, and wandering near something.</summary>
        public DimensionSpawnerAndOrbTemplate SpawnerAndOrb
        {
            get { return spawnerAndOrb ?? (spawnerAndOrb = new DimensionSpawnerAndOrbTemplate()); }
        }

        /// <summary>What it sells, if a player can trade with it.</summary>
        public DimensionTraderTemplate Trader
        {
            get { return trader ?? (trader = new DimensionTraderTemplate()); }
        }

        /// <summary>Whether it hurts whatever comes near it, without swinging.</summary>
        public DimensionContinuousAttackTemplate ContinuousAttack
        {
            get
            {
                return continuousAttack ??
                    (continuousAttack = new DimensionContinuousAttackTemplate());
            }
        }

        /// <summary>How close a player has to be, and how high its name floats.</summary>
        public DimensionCreatureTendingTemplate Tending
        {
            get { return tending ?? (tending = new DimensionCreatureTendingTemplate()); }
        }

        /// <summary>Whether it is something summoned, and how hard it hits for its tier.</summary>
        public DimensionCreatureMinionTemplate Minion
        {
            get { return minion ?? (minion = new DimensionCreatureMinionTemplate()); }
        }

        /// <summary>Whether it drops at zero health instead of dying.</summary>
        public DimensionCreatureLastStandTemplate LastStand
        {
            get { return lastStand ?? (lastStand = new DimensionCreatureLastStandTemplate()); }
        }

        /// <summary>
        /// Whether it is a creature a player can walk up to and use.
        /// </summary>
        /// <remarks>
        /// Both windows are the same shape from the framework's side — an interactable child on the
        /// body a player walks up to, wired to a use and a walk-away — so the view builder asks
        /// this one question rather than each generator working it out again.
        /// </remarks>
        public bool CanBeWalkedUpTo
        {
            get { return Habits.IsLivestock || Trader.IsATrader; }
        }

        public bool ArrivesWithCompany { get { return ArrivesWith.Length > 0; } }
        public bool CompanyFollows { get { return companyFollows; } }

        public Vector3 CompanyAppearsAt { get { return companyAppearsAt; } }

        /// <summary>Whether a gravity well was tuned and then set never to catch it.</summary>
        /// <remarks>
        /// Looks configured — a strength, a range and a turn are all filled in — and the chance is
        /// zero, so none of it can ever happen.
        /// </remarks>
        public bool TunedGravityThatNeverApplies
        {
            get { return GravityWellChance <= 0f && gravityWellStrength != 1f; }
        }

        /// <summary>A whole object left in the world when it dies.</summary>
        public DimensionLeavesBehindTemplate LeavesBehind
        {
            get { return leavesBehind ?? new DimensionLeavesBehindTemplate(); }
        }

        /// <summary>What its attacks sound like.</summary>
        public DimensionAttackSoundsTemplate AttackSounds
        {
            get { return attackSounds ?? new DimensionAttackSoundsTemplate(); }
        }

        public DimensionCreatureAbility[] Abilities
        {
            get
            {
                DimensionCreatureAbility[] all = abilities ?? new DimensionCreatureAbility[0];
                int count = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                    {
                        count++;
                    }
                }

                DimensionCreatureAbility[] kept = new DimensionCreatureAbility[count];
                int next = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                    {
                        kept[next++] = all[i];
                    }
                }

                return kept;
            }
        }

        /// <summary>Whether the same ability was added more than once.</summary>
        /// <remarks>
        /// Each of these is a single component on the prefab, so a second entry of the same kind does
        /// not stack — it silently overwrites the first, and the settings the author wrote earlier
        /// disappear with no sign anything went wrong.
        /// </remarks>
        public bool HasDuplicateAbilities
        {
            get
            {
                DimensionCreatureAbility[] all = Abilities;
                for (int i = 0; i < all.Length; i++)
                {
                    for (int j = i + 1; j < all.Length; j++)
                    {
                        if (all[i].Kind == all[j].Kind)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}
