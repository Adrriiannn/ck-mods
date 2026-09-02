using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The table itself, and the small predicates each row answers with.
    /// </summary>
    internal static partial class DimensionQueryCompanions
    {
        private static bool HasNamed(GameObject root, string typeName)
        {
            Component[] present = root.GetComponents<Component>();
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i] != null && present[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static T Ensure<T>(GameObject root)
            where T : Component
        {
            if (root == null)
            {
                return null;
            }

            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        /// <summary>
        /// Whether the blast this answer hangs off actually reaches anywhere.
        /// </summary>
        /// <remarks>
        /// The tile-laying answer requires a blast, so Unity attaches one — with a reach of zero,
        /// no damage and no terrain damage. The job that lays the tiles reads that reach and walks
        /// a ring between zero and zero, so it lays nothing. Nothing can fill the number in from
        /// here: on a world object there is no blast for the author to have sized.
        /// </remarks>
        private static bool ItIsABlastThatActuallyReaches(GameObject root)
        {
            ExplosionAuthoring blast = root.GetComponent<ExplosionAuthoring>();
            return blast != null && blast.radius > 0f;
        }

        /// <summary>
        /// Whether the creature is something somebody summoned, which is the only thing the game
        /// runs a touch attack or an orbit on.
        /// </summary>
        private static bool ItBelongsToSomebody(GameObject root)
        {
            return HasNamed(root, "PetAuthoring") ||
                   HasNamed(root, "MinionAuthoring") ||
                   HasNamed(root, "MinionDataAuthoring");
        }

        private static bool NoticesThingsNearby(GameObject root)
        {
            SeesNearbyThings(root, VanillaNoticeRadius, VanillaNoticeLayers, false);
            return true;
        }

        /// <summary>
        /// What Core Keeper's own pressure plate watches: the player, one tile, every frame.
        /// </summary>
        /// <remarks>
        /// THE LAYER IS THE WHOLE POINT and it was wrong. The overlap that fills the buffer only
        /// returns a thing whose own layer is one the watcher is watching, and the player's trigger
        /// is Category01 — so a plate watching Category00, which is what the shared default gave
        /// it, never sees a player at all and never presses. <c>PressurePlateEntity</c> watches
        /// Category01 at one tile and looks every frame rather than on a cooldown, because a plate
        /// that reacts a beat late is a plate that reacts to nothing.
        /// </remarks>
        private static bool NoticesAPlayerStandingOnIt(GameObject root)
        {
            SeesNearbyThings(root, 1f, PlayerTriggerLayer, true);
            return true;
        }

        /// <summary>
        /// What the game's own excavation statue watches: a player holding something, twelve tiles.
        /// </summary>
        private static bool NoticesWhatAPlayerIsHoldingFromAcrossTheRoom(GameObject root)
        {
            SeesNearbyThings(root, 12f, PlayerColliderLayer | EnemyTriggerLayer, false);
            return true;
        }

        /// <summary>
        /// What the game's own spike trap watches: things it can hurt, five tiles.
        /// </summary>
        private static bool NoticesWhatItCanHurt(GameObject root)
        {
            SeesNearbyThings(root, 5f, EnemyTriggerLayer, false);
            return true;
        }

        /// <summary>
        /// The list of beams Core Keeper's beam system works on, and the cooldown beside it.
        /// </summary>
        /// <remarks>
        /// <c>BeamAttackStateSystem</c>'s query names <c>BeamBuffer</c> and
        /// <c>AttackCooldownTimerCD</c>, and <c>BeamAttackStateConverter</c> produces neither. The
        /// framework's own marker's converter ensures both, and the system then adds the beams
        /// itself. Nothing here has to decide what a beam IS.
        /// </remarks>
        private static bool AListOfBeamsForTheSystemToFill(GameObject root)
        {
            return Ensure<ExpandNullforge.Creatures.DimensionBeamBufferAuthoring>(root) != null;
        }

        /// <summary>
        /// A route for something that roams to roam along.
        /// </summary>
        /// <remarks>
        /// <c>RoamingStateSystem.cs:152</c> names <c>RoamingPathBuffer</c> in its query, and the
        /// only thing in the game that produces one is <c>RoamingPathAuthoring</c>. Its own
        /// defaults are a valid circuit, so the numbers are only written when the author left them
        /// at nothing: a radius of zero is a circle with no room in it and a path with no points
        /// has nothing to walk between. The numbers are <c>WormSegmentEntity</c>'s own, which is
        /// Core Keeper's roamer: a wandering circle 25 tiles across with ten turning points.
        /// </remarks>
        private static bool ARouteToRoamAlong(GameObject root)
        {
            RoamingPathAuthoring path = Ensure<RoamingPathAuthoring>(root);
            if (path == null)
            {
                return false;
            }

            if (path.regionRadius <= 0)
            {
                path.regionRadius = 25;
            }

            if (path.pointCount <= 0)
            {
                path.pointCount = 10;
            }

            if (path.segmentation <= 0)
            {
                path.segmentation = 10;
            }

            return true;
        }

        /// <summary>
        /// A chase, so a creature that breeds has something to walk to a mate with.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>BreedStateRequest.ShouldUpdate</c> asks for the breed state, the meals record and
        /// <c>ChaseStateCD</c>, and <c>OnUpdate</c> reads the mate straight off the chase's
        /// <c>targetEntity</c> — the chase is how the animal walks over to it. Only
        /// <c>ChaseStateConverter</c> makes that component, and the creature generator writes a
        /// chase for Hostile and Defensive creatures only, so a PASSIVE animal told to breed had
        /// none and the request never ran. Core Keeper's own cow carries one.
        /// </para>
        /// <para>
        /// A chase on a passive animal chases nothing on its own: what it will go after comes from
        /// its behaviour tags, and a passive creature's wants list is empty. This adds the ability
        /// to walk to a mate and nothing else.
        /// </para>
        /// <para>
        /// The numbers are <c>CowEntity</c>'s: eight tiles, at one and a half times its walking
        /// speed. A chase left at a speed of zero enters the state and stands still.
        /// </para>
        /// </remarks>
        private static bool AChaseToWalkToAMateWith(GameObject root)
        {
            ChaseStateAuthoring chase = Ensure<ChaseStateAuthoring>(root);
            if (chase == null)
            {
                return false;
            }

            if (chase.chaseAtDistance <= 0f)
            {
                chase.chaseAtDistance = 8f;
            }

            if (chase.moveSpeedMultiplier <= 0f)
            {
                chase.moveSpeedMultiplier = 1.5f;
            }

            return true;
        }

        /// <summary>
        /// Eating, which is what breeding counts.
        /// </summary>
        /// <remarks>
        /// <c>ChaseStateRequest.cs:116</c> only lets an animal go looking for a mate when it has a
        /// breed state, a meals record AND an eat state, and it compares what the animal is
        /// carrying against <c>EatStateCD.maxFoodUntilFull</c> — so with no eat state the breeding
        /// branch is never reached at all. The numbers are <c>CowEntity</c>'s: eats from one tile
        /// away, over 0.55 seconds, and is full after four.
        /// </remarks>
        private static bool AMealToCountTowardsBreeding(GameObject root)
        {
            EatStateAuthoring eat = Ensure<EatStateAuthoring>(root);
            if (eat == null)
            {
                return false;
            }

            if (eat.distanceToEat <= 0f)
            {
                eat.distanceToEat = 1f;
            }

            if (eat.duration <= 0f)
            {
                eat.duration = 0.55f;
            }

            if (eat.maxFoodUntilFull <= 0)
            {
                eat.maxFoodUntilFull = 4;
            }

            return true;
        }

        /// <summary>Category01 PlayerTrigger, which is what a player's own trigger belongs to.</summary>
        private const uint PlayerTriggerLayer = 2u;

        /// <summary>Category02 PlayerCollider.</summary>
        private const uint PlayerColliderLayer = 4u;

        /// <summary>Category03 EnemyTrigger, which every creature's trigger belongs to.</summary>
        private const uint EnemyTriggerLayer = 8u;

        /// <summary>Category04 EnemyCollider, which is what a creature's body belongs to.</summary>
        private const uint EnemyColliderLayer = 16u;

        /// <summary>Adds the sibling at its own defaults, which is all its row needs.</summary>
        private static bool Fill<T>(GameObject root)
            where T : Component
        {
            return Ensure<T>(root) != null;
        }

        private static bool GivesItSomethingToCallAnEnemy(GameObject root)
        {
            return DecidesWhoIsAnEnemy(root) != null;
        }

        /// <summary>
        /// Lets a door swing, if the door has a body a player can walk up to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>TriggerSetVariationSystem</c> is the whole of opening, and its query is the trigger
        /// component, the use buffer and the object's own data. The trigger component is the only
        /// source of the first two, so a door without one is a wall a player cannot even aim at —
        /// no use buffer means <c>InteractablePostConverter</c> stops before it writes the
        /// interactable. The game's own <c>WoodDoorEntity</c> carries it beside the door.
        /// </para>
        /// <para>
        /// THE GUARD IS NOT OPTIONAL. That same post-converter, once the buffer IS there, reaches
        /// straight into the object's body for its first <c>InteractableObject</c> and takes it by
        /// index. On a door with no body, or a body nothing can use, that is an exception during
        /// conversion rather than a door that does not open — so a door that cannot take the
        /// trigger is told about it instead of being given one.
        /// </para>
        /// </remarks>
        private static bool DoorCanBeOpened(GameObject root)
        {
            return ThereIsSomethingToUseOnIt(root) &&
                   Ensure<ChangeVariationTriggerAuthoring>(root) != null;
        }

        /// <summary>
        /// Whether the object's picture carries something a player can walk up to and use.
        /// </summary>
        /// <remarks>
        /// EVERY "when it is used" ANSWER HAS TO ASK THIS FIRST. Core Keeper turns an object into
        /// something usable in a step that runs after conversion, and that step reaches straight
        /// into the object's picture for its first usable part and takes it by index. On an object
        /// whose picture has none, that is an exception during generation rather than a control
        /// that quietly does nothing — so the answers that produce a use are refused, with a
        /// sentence, on anything that cannot take one.
        /// </remarks>
        public static bool ThereIsSomethingToUseOnIt(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            ObjectAuthoring identity = root.GetComponent<ObjectAuthoring>();
            if (identity == null || identity.graphicalPrefab == null)
            {
                return false;
            }

            return identity.graphicalPrefab.GetComponentInChildren<InteractableObject>(true) != null;
        }

        private static Companion[] BuildRows()
        {
            return new[]
            {
                // ---- the buffer nothing but the tracker fills -------------------------------
                //
                // THESE NAME SOMETHING NO OBJECT CAN CARRY, ON PURPOSE. Half of the authoring
                // components below declare the tracker as required, so Unity attaches one for them
                // — with a reach of zero and a mask of zero. An overlap of no size against no
                // layers returns nothing, forever, so the buffer those systems read is empty for
                // the object's whole life. Checking whether the tracker is PRESENT would find it
                // and move on; naming something nothing can have means the row is always looked at,
                // and the fill is a merge, so it gives the object real numbers without taking away
                // whatever a pass with its own numbers already asked for.

                new Companion(
                    typeof(SummonAreaAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "LarvaBossSummonAreaEntity",
                    "BossSummoningSystem",
                    NoticesThingsNearby,
                    null),
                new Companion(
                    typeof(SummonAreaAuthoring),
                    "AnimationAuthoring",
                    "LarvaBossSummonAreaEntity",
                    "BossSummoningSystem",
                    Fill<AnimationAuthoring>,
                    null),
                new Companion(
                    typeof(ChangeVariationWhenObjectNearbyAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "PressurePlateEntity",
                    "ChangeVariationWhenObjectNearbySystem",
                    NoticesAPlayerStandingOnIt,
                    null),
                new Companion(
                    typeof(ChangeVariationWhenObjectNearbyAuthoring),
                    "BehaviourTagsAuthoring",
                    "PressurePlateEntity",
                    "ChangeVariationWhenObjectNearbySystem",
                    GivesItSomethingToCallAnEnemy,
                    null),
                new Companion(
                    typeof(ChangeVariationWhenPlayerHoldObjectNearbyAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "ExcavationStatueEntity",
                    "ChangeVariationWhenPlayerHoldObjectNearbySystem",
                    NoticesWhatAPlayerIsHoldingFromAcrossTheRoom,
                    null),
                new Companion(
                    typeof(AddForceToNearbyEntitiesAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "RobotBossAttackPushbackEntity",
                    "AddForceToNearbyEntitiesSystem",
                    NoticesThingsNearby,
                    null),
                new Companion(
                    typeof(AttackContinuouslyAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "SpikeTrapEntity",
                    "AttackContinuouslyStateSystem",
                    NoticesWhatItCanHurt,
                    null),
                new Companion(
                    typeof(AttackContinuouslyAuthoring),
                    "BehaviourTagsAuthoring",
                    "SpikeTrapEntity",
                    "AttackContinuouslyStateSystem",
                    GivesItSomethingToCallAnEnemy,
                    null),
                new Companion(
                    typeof(BushStateAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "BushEntity",
                    "BushStateSystem",
                    NoticesThingsNearby,
                    null),
                new Companion(
                    typeof(SiphonMana.Authoring.SiphonManaAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "not checked against a named prefab",
                    "SiphonManaSystem",
                    NoticesThingsNearby,
                    null),
                new Companion(
                    typeof(HealNearbyEntitiesAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "not checked against a named prefab",
                    "HealOtherEntitiesSystem",
                    NoticesThingsNearby,
                    null),

                // ---- the event terminal ------------------------------------------------------
                new Companion(
                    typeof(EventTerminalAuthoring),
                    "ATrackerThatCanActuallySeeSomething",
                    "EventTerminalEntity",
                    "EventTerminalSystem",
                    TerminalWatchesItsOwnReach,
                    null),
                new Companion(
                    typeof(EventTerminalAuthoring),
                    "ImmunityZoneAuthoring",
                    "EventTerminalEntity",
                    "EventTerminalSystem",
                    TerminalKeepsPlayersSafeInItsOwnReach,
                    null),

                // ---- things with a slot in them ---------------------------------------------
                new Companion(
                    typeof(EnemySpawnerPlatformAuthoring),
                    "InventoryAuthoring",
                    "EnemySpawnerPlatformEntity",
                    "EnemySpawnerPlatformSystem",
                    OneSlotToPutSomethingIn,
                    null),
                new Companion(
                    typeof(EnemySpawnerPlatformAuthoring),
                    "ElectricityAuthoring",
                    "EnemySpawnerPlatformEntity",
                    "EnemySpawnerPlatformSystem",
                    Fill<Pug.Automation.ElectricityAuthoring>,
                    null),
                new Companion(
                    typeof(BossStatueAuthoring),
                    "InventoryAuthoring",
                    "LarvaBossStatueEntity",
                    "BossStatueSystem",
                    OneSlotToPutSomethingIn,
                    null),
                new Companion(
                    typeof(BossStatueAuthoring),
                    "AncientElectricityConnectionAuthoring",
                    "LarvaBossStatueEntity",
                    "BossStatueSystem",
                    Fill<AncientElectricityConnectionAuthoring>,
                    null),
                new Companion(
                    typeof(MerchantAuthoring),
                    "InventoryAuthoring",
                    "CavelingMerchantEntity",
                    "MerchantBuyInventorySystem",
                    ShelvesForWhatTheTraderSells,
                    null),
                new Companion(
                    typeof(MerchantAuthoring),
                    "StateAuthoring",
                    "CavelingMerchantEntity",
                    "MerchantBuyInventorySystem",
                    Fill<StateAuthoring>,
                    null),

                // ---- powered things ----------------------------------------------------------
                new Companion(
                    typeof(ActivatedByElectricityStateAuthoring),
                    "ElectricityAuthoring",
                    "ElectricalDoorHorizontalEntity",
                    "ActivatedByElectricityStateSystem",
                    Fill<Pug.Automation.ElectricityAuthoring>,
                    null),

                // ---- explosive things --------------------------------------------------------
                new Companion(
                    typeof(SequenceExplosiveAuthoring),
                    "ExplosiveAuthoring",
                    "BlunderBombEntity",
                    "ExplosiveSystem.SequencedExplosionsJob",
                    Fill<ExplosiveAuthoring>,
                    null),
                new Companion(
                    typeof(ProximityTriggerAuthoring),
                    "ExplosiveAuthoring",
                    "ProximityBombEntity",
                    "ExplosiveSystem.ProximityExplosionCheck",
                    Fill<ExplosiveAuthoring>,
                    null),

                // ---- things that push, and things that face a way ---------------------------
                new Companion(
                    typeof(VelocityAffectorAuthoring),
                    "DirectionBasedOnVariationAuthoring",
                    "ConveyorBeltForwardEntity",
                    "PlayerControllerBurstedUtility",
                    Fill<DirectionBasedOnVariationAuthoring>,
                    null),
                new Companion(
                    typeof(ElectricOrbAuthoring),
                    "RotationAuthoring",
                    "AffixElectricOrbEntity",
                    "ElectricOrbSystem",
                    Fill<RotationAuthoring>,
                    null),

                // ---- doors and beds -----------------------------------------------------------
                new Companion(
                    typeof(DoorAuthoring),
                    "ChangeVariationTriggerAuthoring",
                    "WoodDoorEntity",
                    "TriggerSetVariationSystem",
                    DoorCanBeOpened,
                    ThereIsSomethingToUseOnIt,
                    "is a door with nothing a player can walk up to and use, so it can never be " +
                    "opened. Set what using it does, and give it a picture, and it will swing."),
                new Companion(
                    typeof(BedAuthoring),
                    "OccupiableAuthoring",
                    "BedEntity",
                    "Sleep.GetOffsetAndFacingDirectionFromOccupiable",
                    null,
                    "is a bed nobody can lie down on. Tick 'Can be occupied' and set where the " +
                    "sleeper's head and feet go — without it the game puts them on the bed's exact " +
                    "centre and they stand straight back up."),

                // ---- what only the author can answer ----------------------------------------
                new Companion(
                    typeof(SittableAuthoring),
                    "CanBeControlledByOtherEntityAuthoring",
                    "CavelingChairEntity",
                    "TriggerUseControllableSystem",
                    Fill<CanBeControlledByOtherEntityAuthoring>,
                    null),
                new Companion(
                    typeof(WayPointAuthoring),
                    "PortalAuthoring",
                    "WayPointEntity",
                    "WaypointSystem",
                    Fill<PortalAuthoring>,
                    null),
                new Companion(
                    typeof(WayPointAuthoring),
                    "SpawnCompanionsAuthoring",
                    "WayPointEntity",
                    "WaypointSystem",
                    null,
                    "is set as somewhere players can travel to, but the game only charges up a " +
                    "travel point that carries its own map pin as a separate little object beside " +
                    "it, and this framework does not build that pin yet. It will sit there and " +
                    "never light up. Use a portal for now."),
                // The sibling IS present here — Unity attaches it because the authoring class
                // requires it — and it arrives with a radius of nothing. The job reads that radius
                // and its ring test can never be true, so the answer is inert. A name nothing can
                // ever carry is used so the row is always looked at rather than skipped on sight
                // of a companion that is there and empty.
                new Companion(
                    typeof(SpawnTileOnExplosionAuthoring),
                    "ABlastWithARealRadius",
                    "OilExplosionEntity",
                    "ExplosionDamageSystem.SpawnTileOnExplosionJob",
                    // NO FILL. Handing ItIsABlastThatActuallyReaches in as this row's fill would
                    // claim to supply something it never supplies: that method adds nothing — it is
                    // the same read the gap check already makes. A row that only
                    // looks answers with a sentence; the look itself is TheGapIsReallyClosed's job,
                    // and CloseTheGaps asks it before saying anything, so a blast that really does
                    // reach is still silent.
                    null,
                    "is set to lay ground where it explodes, but only a blast can do that and this " +
                    "is an object rather than a blast. Put the answer on the blast the object sets " +
                    "off instead."),
                // ---- the beam ---------------------------------------------------------------
                // THIS ROW DOES NOT REFUSE THE BEAM, and a refusal would not be true. Core
                // Keeper's beam system does not need a list of beams nothing in the game creates.
                // The system creates them itself: `ck-db\Pug.Other\BeamAttackStateSystem.cs:133`
                // adds the first beam the moment the wind-up timer elapses and `:141` adds the rest
                // of the fan, and the query at `:218` only asks that the buffer EXISTS. What is
                // really missing is one line in Core Keeper's own converter —
                // `BeamAttackStateConverter` ensures StateInfoCD, adds BeamAttackStateCD and never
                // calls EnsureHasBuffer<BeamBuffer>() — and no vanilla prefab carries a beam, so
                // nothing in the shipped game ever ran into it. A wrong refusal is worse than a
                // missing feature: it teaches the next reader that a working system is broken.
                new Companion(
                    typeof(BeamAttackStateAuthoring),
                    "DimensionBeamBufferAuthoring",
                    "no vanilla prefab uses this",
                    "BeamAttackStateSystem",
                    AListOfBeamsForTheSystemToFill,
                    "is given a beam attack, and the list of beams Core Keeper's beam system works " +
                    "on could not be put on it, so the beam will never fire. Use a ranged attack " +
                    "instead."),
                new Companion(
                    typeof(BeamAttackStateAuthoring),
                    ATrackerThatCanActuallySeeSomething,
                    "no vanilla prefab uses this",
                    "BeamAttackStateRequest",
                    NoticesWhatItCanHurt,
                    null),
                new Companion(
                    typeof(BeamAttackStateAuthoring),
                    "BehaviourTagsAuthoring",
                    "no vanilla prefab uses this",
                    "BeamAttackStateRequest",
                    GivesItSomethingToCallAnEnemy,
                    null),

                // ---- roaming a circuit ------------------------------------------------------
                new Companion(
                    typeof(RoamingStateAuthoring),
                    "RoamingPathAuthoring",
                    "WormSegmentEntity",
                    "RoamingStateSystem",
                    ARouteToRoamAlong,
                    null),

                // ---- breeding ---------------------------------------------------------------
                new Companion(
                    typeof(BreedStateAuthoring),
                    "ChaseStateAuthoring",
                    "CowEntity",
                    "BreedStateRequest",
                    AChaseToWalkToAMateWith,
                    null),
                new Companion(
                    typeof(BreedStateAuthoring),
                    "EatStateAuthoring",
                    "CowEntity",
                    "ChaseStateRequest",
                    AMealToCountTowardsBreeding,
                    null),
                // ---- playing dead --------------------------------------------------------------
                // AnimateDontDestroyOnZeroHealthSystem's job reads HealthCD, the animate component,
                // the animation buffer and its pointer, and it decides on `healthCD.health <= 0` —
                // so on something with no health at all it never fires, and SetEntitiesDestroyedSystem
                // has nothing to stop either. No fill, because how much health a thing has is the
                // author's answer and a number invented here would decide how hard it is to knock
                // down. CoreBossOrbEntity is Core Keeper's own thing that drops and gets back up.
                new Companion(
                    typeof(DontDestroyOnZeroHealthAuthoring),
                    "HealthAuthoring",
                    "CoreBossOrbEntity",
                    "AnimateDontDestroyOnZeroHealthSystem",
                    null,
                    "plays dead instead of dying, and has no health to lose, so it can never reach " +
                    "zero and will never drop. Give it health."),
                new Companion(
                    typeof(SpawnerAuthoring),
                    "NothingInTheGameReadsThis",
                    "no vanilla prefab uses this",
                    "no system",
                    null,
                    "is marked as a spawner. Nothing in Core Keeper reads that mark, so it will " +
                    "spawn nothing. Use a nest, a territory spawner or a spawner platform."),
                new Companion(
                    typeof(TouchAttackAuthoring),
                    "PetAuthoring",
                    "not checked against a named prefab",
                    "TouchAttackStateSystem",
                    // NO FILL, for the same reason as the tile-laying row above: ItBelongsToSomebody
                    // only looks, and nothing here can decide that a creature is somebody's pet.
                    null,
                    "hurts what it touches, but the game only runs that on something summoned by " +
                    "someone — a pet or a minion. On a creature that stands on its own it will " +
                    "never hurt anything it touches. Give it a melee attack instead."),
                new Companion(
                    typeof(MinionOrbitAuthoring),
                    "PetAuthoring",
                    "OrbitalTurretEntity",
                    "MinionOrbitStateSystem",
                    // NO FILL. Same again: a look, not a supply.
                    null,
                    "is set to orbit, but the game only orbits something around whoever summoned " +
                    "it. Unless it is a pet or a minion it will stay where it is."),
            };
        }

        private static bool TerminalWatchesItsOwnReach(GameObject root)
        {
            EventTerminalAuthoring terminal = root.GetComponent<EventTerminalAuthoring>();
            float radius = terminal != null && terminal.radius > VanillaNoticeRadius
                ? terminal.radius
                : VanillaNoticeRadius;
            SeesNearbyThings(root, radius, VanillaNoticeLayers, false);
            return true;
        }

        private static bool TerminalKeepsPlayersSafeInItsOwnReach(GameObject root)
        {
            EventTerminalAuthoring terminal = root.GetComponent<EventTerminalAuthoring>();
            ImmunityZoneAuthoring zone = Ensure<ImmunityZoneAuthoring>(root);
            if (zone != null && terminal != null && zone.radius <= 0f)
            {
                zone.radius = terminal.radius;
            }

            return zone != null;
        }

        /// <summary>
        /// One slot, for the thing that has to be put in before the object does its job.
        /// </summary>
        /// <remarks>
        /// A spawner platform takes one trophy and a boss statue takes one crystal, and both of the
        /// game's systems walk <c>ContainedObjectsBuffer</c> looking for it. That buffer comes only
        /// from an inventory, so an object with no inventory has nowhere to put the thing and the
        /// system never matches it. The game's own prefabs for both jobs carry exactly one slot.
        /// </remarks>
        private static bool OneSlotToPutSomethingIn(GameObject root)
        {
            InventoryAuthoring inventory = Ensure<InventoryAuthoring>(root);
            if (inventory == null)
            {
                return false;
            }

            if (inventory.sizeX <= 0)
            {
                inventory.sizeX = 1;
            }

            if (inventory.sizeY <= 0)
            {
                inventory.sizeY = 1;
            }

            if (inventory.slotRequirements == null)
            {
                inventory.slotRequirements = new List<SlotRequirement>();
            }
        
            return true;
        }

        /// <summary>
        /// Enough shelves to hold what the trader was given to sell.
        /// </summary>
        /// <remarks>
        /// <c>MerchantBuyInventorySystem</c> copies the trader's stock straight into
        /// <c>ContainedObjectsBuffer</c> slot by slot, so a trader with no inventory has a
        /// permanently empty shop and the system never matches it at all.
        /// </remarks>
        private static bool ShelvesForWhatTheTraderSells(GameObject root)
        {
            MerchantAuthoring merchant = root.GetComponent<MerchantAuthoring>();
            int stock = merchant != null && merchant.items != null ? merchant.items.Count : 0;

            InventoryAuthoring inventory = Ensure<InventoryAuthoring>(root);
            if (inventory == null)
            {
                return false;
            }

            if (inventory.sizeY <= 0)
            {
                inventory.sizeY = 1;
            }

            int wanted = stock > 0 ? stock : 1;
            if (inventory.sizeX * inventory.sizeY < wanted)
            {
                inventory.sizeX = wanted;
                inventory.sizeY = 1;
            }

            if (inventory.slotRequirements == null)
            {
                inventory.slotRequirements = new List<SlotRequirement>();
            }
        
            return true;
        }
    }
}
