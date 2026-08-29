using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What has to sit beside what, so that the game actually looks at the object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Core Keeper does its work in systems, and a system only touches an entity
    /// that carries EVERY component named in its query. The framework used to write the one
    /// component that obviously belongs to a feature and none of the others, because the others
    /// come from unrelated authoring components. The entity then never matched the query, the
    /// system never ran on it, and nothing anywhere reported a problem: the component we wrote was
    /// present, its converter ran, the number in it was right. The author generated cleanly and the
    /// feature did nothing.
    /// </para>
    /// <para>
    /// The instance that exposed it was the summoning circle. <c>BossSummoningSystem</c>'s only
    /// query is <c>NearbyEntitiesBufferCD</c> + <c>AnimationBuffer</c> +
    /// <c>AnimationBufferPointer</c> + <c>SummonAreaCD</c>; <c>SummonAreaAuthoring</c> supplies the
    /// last one and declares no <c>RequireComponent</c>, so both routes that built a circle built
    /// one the game never looked at. The game's own <c>LarvaBossSummonAreaEntity</c> carries a
    /// tracker and an animation beside its summon area.
    /// </para>
    /// <para>
    /// So the knowledge lives here, once: for a component the framework writes, what else the
    /// system that reads it demands, whether we can fill that in ourselves, and — when we cannot —
    /// the sentence the author gets instead. The creature, critter, world-object, container,
    /// workbench, vehicle, item and plant generators all call <see cref="CloseTheGaps"/> on the
    /// finished object; the projectile and blast generators do not, because two of the rows are
    /// written for something placed in the world and would say the wrong thing to somebody
    /// building a shot. Adding a new authoring surface without adding its row here is caught by
    /// <c>DimensionQueryCompanionTests</c>.
    /// </para>
    /// <para>
    /// The shapes are not invented. Each row names the Core Keeper prefab doing that same job, and
    /// carries what that prefab carries.
    /// </para>
    /// </remarks>
    internal static class DimensionQueryCompanions
    {
        /// <summary>
        /// How far Core Keeper's own reacting objects notice things, measured off theirs.
        /// </summary>
        /// <remarks>
        /// The summoning circles, the pressure plate and the firework all sit near 1.7, which is a
        /// little over the two tiles the systems check by default. It is the floor rather than the
        /// answer: a surface that knows its own reach passes that instead.
        /// </remarks>
        public const float VanillaNoticeRadius = 1.7f;

        /// <summary>
        /// The physics layer Core Keeper's own reacting objects watch.
        /// </summary>
        public const uint VanillaNoticeLayers = 1u;

        /// <summary>
        /// The layer mask Core Keeper's own creatures watch (<c>LarvaEntity</c> sets Category01).
        /// </summary>
        public const uint VanillaCreatureNoticeLayers = 2u;

        /// <summary>How far a creature notices by default, off <c>LarvaEntity</c>.</summary>
        public const float VanillaCreatureNoticeRadius = 8f;

        /// <summary>
        /// Lets an object see what is near it, without taking away what an earlier pass gave it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>NearbyEntitiesTrackerAuthoring</c> is the only thing in the game that fills
        /// <c>NearbyEntitiesBufferCD</c> (<c>NearbyEntitiesTrackerSystem</c> runs one
        /// <c>OverlapSphere</c> at its radius against its layer mask), and it is
        /// <c>[DisallowMultipleComponent]</c> — one radius and one mask for every feature on the
        /// object that reads that buffer.
        /// </para>
        /// <para>
        /// THIS MERGES RATHER THAN SETS, and that is the whole point. Several separate answers on
        /// one object want to see nearby things — a door that opens when you hold a key, a plate
        /// that reacts to what is put on it, a trap that attacks, a circle that waits for its
        /// offering — and each of those passes runs at a different point. When the last pass to run
        /// simply wrote its own numbers, a shove authored at six tiles quietly became 1.7, on the
        /// one object where any of it worked at all.
        /// </para>
        /// <para>
        /// Seeing further than one feature needs costs that feature nothing: every consumer filters
        /// the buffer by its own rule afterwards — the held-item gate applies its own radius, the
        /// shove applies its own — so the widest ask and the union of the masks is the answer that
        /// keeps every feature working. Seeing too little is the bug.
        /// </para>
        /// </remarks>
        public static void SeesNearbyThings(
            GameObject root,
            float radius,
            uint layers,
            bool everyFrame)
        {
            SeesNearbyThings(root, radius, layers, everyFrame, null);
        }

        /// <summary>
        /// The same, saying so when the ask was narrower than what the object already had.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE MERGE IS WITHIN ONE GENERATE, NOT ACROSS THEM, and that distinction is the whole
        /// difference between a merge and a ratchet. Several separate answers on one object want to
        /// see nearby things and each runs at a different point in the same generate, so the widest
        /// ask has to win between them. But a generator also reloads the prefab it made last time
        /// and changes it in place, so what the LAST generate wrote is sitting on the object when
        /// this one starts — and merging with that meant narrowing a creature's notice range from
        /// twelve tiles to four left it at twelve, every time, until the prefab was deleted by
        /// hand. A control that cannot be turned down is a control that does not do what its label
        /// says.
        /// </para>
        /// <para>
        /// So the FIRST write this generate makes to a given object replaces what is there, and
        /// every write after it merges. The object is remembered by its instance, not by its id
        /// alone, because Unity reuses instance ids: an id whose remembered object is not this same
        /// object counts as the first write. Nothing has to be inserted into any generator for this
        /// to hold, which is why it is here rather than in six call sites.
        /// </para>
        /// </remarks>
        public static void SeesNearbyThings(
            GameObject root,
            float radius,
            uint layers,
            bool everyFrame,
            Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            bool startingFresh = ThisGenerateHasNotWrittenTheTrackerYet(root);

            NearbyEntitiesTrackerAuthoring tracker = Ensure<NearbyEntitiesTrackerAuthoring>(root);
            if (tracker == null)
            {
                return;
            }

            if (startingFresh)
            {
                tracker.radius = radius;
                tracker.detectsLayer = new Unity.Physics.Authoring.PhysicsCategoryTags
                {
                    Value = layers
                };
                tracker.ignoreCooldown = everyFrame;
                return;
            }

            if (say != null && radius > 0f && tracker.radius > radius)
            {
                say("was set to notice things " + radius + " tiles away, and something else on it " +
                    "already reaches " + tracker.radius + " — everything on an object that watches " +
                    "its surroundings shares one reach, so the wider one wins and it will reach " +
                    tracker.radius + ".");
            }

            if (radius > tracker.radius)
            {
                tracker.radius = radius;
            }

            tracker.detectsLayer = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = tracker.detectsLayer.Value | layers
            };

            tracker.ignoreCooldown = tracker.ignoreCooldown || everyFrame;
        }

        /// <summary>
        /// Whether this is the first thing written to that object's tracker in this generate.
        /// </summary>
        /// <remarks>
        /// Keyed on the instance AND the reference. A generator loads prefab contents fresh every
        /// generate, so the object this pass is holding is a different instance from the one the
        /// last generate held; but Unity hands out instance ids again after an object is destroyed,
        /// so an id on its own would eventually collide and make a first write look like a later
        /// one. Checking the reference too turns that collision into "first write", which is the
        /// safe way round. Entries for objects that have been destroyed are dropped once the table
        /// grows past a thousand, so a long editor session does not keep them all.
        /// </remarks>
        private static bool ThisGenerateHasNotWrittenTheTrackerYet(GameObject root)
        {
            int id = root.GetInstanceID();

            GameObject remembered;
            if (TrackersWrittenThisGenerate.TryGetValue(id, out remembered) &&
                ReferenceEquals(remembered, root))
            {
                return false;
            }

            if (TrackersWrittenThisGenerate.Count > 1000)
            {
                List<int> gone = new List<int>();
                foreach (KeyValuePair<int, GameObject> entry in TrackersWrittenThisGenerate)
                {
                    if (entry.Value == null)
                    {
                        gone.Add(entry.Key);
                    }
                }

                for (int i = 0; i < gone.Count; i++)
                {
                    TrackersWrittenThisGenerate.Remove(gone[i]);
                }
            }

            TrackersWrittenThisGenerate[id] = root;
            return true;
        }

        /// <summary>The objects whose tracker has already been written, by instance id.</summary>
        private static readonly Dictionary<int, GameObject> TrackersWrittenThisGenerate =
            new Dictionary<int, GameObject>();

        /// <summary>
        /// Forgets that an object's tracker was written, so the next write starts fresh again.
        /// </summary>
        /// <remarks>
        /// Only the guard test needs this: it runs several probes in one editor session and each
        /// one has to behave like a generate of its own.
        /// </remarks>
        public static void ForgetWhatWasWrittenToTheTracker(GameObject root)
        {
            if (root != null)
            {
                TrackersWrittenThisGenerate.Remove(root.GetInstanceID());
            }
        }

        /// <summary>How far the object can see, or zero when it cannot see at all.</summary>
        /// <remarks>
        /// The three readers below exist so the guard test can check what the merge did without
        /// the test assembly having to reference the physics package for one field.
        /// </remarks>
        public static float HowFarItSees(GameObject root)
        {
            NearbyEntitiesTrackerAuthoring tracker =
                root == null ? null : root.GetComponent<NearbyEntitiesTrackerAuthoring>();
            return tracker == null ? 0f : tracker.radius;
        }

        /// <summary>Which physics layers the object watches, as one mask.</summary>
        public static uint WhatItWatches(GameObject root)
        {
            NearbyEntitiesTrackerAuthoring tracker =
                root == null ? null : root.GetComponent<NearbyEntitiesTrackerAuthoring>();
            return tracker == null ? 0u : tracker.detectsLayer.Value;
        }

        /// <summary>Whether it re-checks every frame rather than on the usual cooldown.</summary>
        public static bool ItLooksEveryFrame(GameObject root)
        {
            NearbyEntitiesTrackerAuthoring tracker =
                root == null ? null : root.GetComponent<NearbyEntitiesTrackerAuthoring>();
            return tracker != null && tracker.ignoreCooldown;
        }

        /// <summary>
        /// Gives an object the component that decides who counts as an enemy, keeping any answer
        /// already on it.
        /// </summary>
        /// <remarks>
        /// <c>BehaviourTagsCD</c> is a hard gate on the chase, on every attack, on eating, on
        /// exploding and on the two "reacts to what is nearby" systems. Its lists may legitimately
        /// be empty — that reads as "nothing here is my enemy" — but the component itself has to be
        /// present or the system does not look at the object at all.
        /// </remarks>
        public static BehaviourTagsAuthoring DecidesWhoIsAnEnemy(GameObject root)
        {
            BehaviourTagsAuthoring tags = Ensure<BehaviourTagsAuthoring>(root);
            if (tags == null)
            {
                return null;
            }

            if (tags.wantsToAttackTags == null)
            {
                tags.wantsToAttackTags = new List<ObjectCategoryTag>();
            }

            if (tags.cantAttackTags == null)
            {
                tags.cantAttackTags = new List<ObjectCategoryTag>();
            }

            if (tags.eatsTags == null)
            {
                tags.eatsTags = new List<ObjectCategoryTag>();
            }

            return tags;
        }

        /// <summary>
        /// Says in the object table that this object's LOOK is not part of which object it is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE ONE RULE FOR THE WHOLE VARIATION TRAP. Six places in Core Keeper look an object up
        /// by <c>PugDatabase.GetPrimaryPrefabEntity(objectID, database, someVariation)</c> and then
        /// either set or remove the object's <c>PhysicsCollider</c> from what came back. That
        /// lookup has no fallback to look 0 — unlike <c>GetEntityObjectInfo</c> sitting beside it,
        /// which is the asymmetry that makes this a trap — so it returns <c>Entity.Null</c> unless
        /// an entry matches that exact look OR the entry says its look is dynamic. This framework
        /// registers ONE prefab per object, so every look but 0 missed, and all six took their
        /// other branch:
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// <c>ColliderVariationSystem</c> and <c>ColliderBasedOnDirectionVariationSystem</c> index
        /// a <c>ComponentLookup</c> with the result inside a Burst job, with no null test at all.
        /// </description></item>
        /// <item><description>
        /// <c>ResetColliderAfterVariationChangeSystem</c>,
        /// <c>UpdateColliderMelodyAffectedSystem</c>,
        /// <c>UpdateColliderWhenChangeVariationAfterTimeObjectSystem</c> and
        /// <c>UpdateColliderWhenChangeVariationWhenContainingObjectSystem</c> REMOVE the collider,
        /// and the removal is permanent: three of them stamp the entity so they never look at it
        /// again, and the fourth restores through <c>ECB.SetComponent</c>, which no-ops on an
        /// entity that no longer has the component. So a locked chest became unhittable the moment
        /// the key went in, and a prop that changes look after a while was walked through forever
        /// after.
        /// </description></item>
        /// </list>
        /// <para>
        /// ONE WRITE FIXES ALL SIX, because they all fail for the same reason. An object with one
        /// registered prefab has one prefab for every look it can be in, and saying so turns every
        /// one of those lookups from "no such thing" into "this object's own prefab" — so the four
        /// removals become a set of the collider it already had, and the two Burst reads stop
        /// indexing with a null entity. It is the same flag the item generator has always set; the
        /// pass before this one wrote it on items only and never on anything placed.
        /// </para>
        /// <para>
        /// WHAT ELSE THE FLAG CHANGES, checked rather than assumed. It is read in exactly four
        /// places in the game. <c>GetEntityObjectInfo</c> already fell back to look 0, so nothing
        /// there moves. <c>HasObject</c> starts answering yes for a look nobody registered, which
        /// two callers ask — the critter catcher and the spawn-around table — and in both the yes
        /// is what lets a modded object work at all. <c>GetPrimaryPrefabEntity</c> is the fix
        /// above, and the same change rescues three more branches that treated the null as "give
        /// up": the loot drop's first-look rule, the fishing net's contents, and — the worst of
        /// them — <c>DeserializeComponentsSystem</c>, which destroys a saved object and then
        /// returns without rebuilding it. The fourth reader is a developer console command.
        /// </para>
        /// <para>
        /// AND WHERE IT MUST NOT BE WRITTEN, which is the whole reason it is called from the three
        /// Finish helpers and not from the sweep every generator reaches. The flag means "any look
        /// of this object is this prefab", so it is only true of an object the framework registered
        /// ONCE. Two paths are not that, and both reach <c>CloseTheGaps</c> without a Finish
        /// helper: the plant generator registers a crop and its seed at several looks each — a
        /// version on seed look 1 and plant look 2, and so on — and marking the base one dynamic
        /// would hand every one of those lookups the base prefab and quietly delete rare crops;
        /// and the item generator writes this same field from the author's own "its look is chosen
        /// as it is made" answer, which nothing here is allowed to overwrite.
        /// </para>
        /// <para>
        /// THE RULE IS A CONTRACT ON THE CALLER AND NOTHING HERE CAN CHECK IT. "Registered exactly
        /// once" is a fact about what the generator did with the object, and this method holds a
        /// GameObject; it cannot count registrations, and the only thing it does check is that the
        /// flag is not already set. The two exclusions above hold because of which generators call
        /// a Finish helper today, not because anything stops the third. Two call sites already
        /// bypass the helpers and reach <c>CloseTheGaps</c> directly — the summoning circle and the
        /// boss map pin in <c>DimensionCreatureGenerator</c> — and neither carries anything that
        /// changes its look, so they cost nothing today and would cost the trap back if one ever
        /// did. Written down because the summary of this rule reads as if the code enforced it.
        /// </para>
        /// <para>
        /// A SEVENTH SYSTEM IN THE SAME FAMILY, and it is the worst of them.
        /// <c>AdaptiveEntitySystem</c> looks the object's primary prefab up the same way and then
        /// indexes a <c>BufferLookup</c> with the result, with no null check, inside a Bursted job
        /// — so a miss there is not the missing collider the six collider systems produce but a
        /// read off the null entity, which in a release build is memory rather than an exception.
        /// It is reachable: the world-object generator writes
        /// <c>AdaptiveEntityBufferAuthoring</c>, and the system writes a new look onto the object
        /// as it adapts, so the miss would land a tick after the object first changes. It is
        /// covered today only because every world object goes through
        /// <c>FinishAWorldObject</c>. Widening the plant-style carve-out — a world object
        /// registered at more than one look — turns this one from a missing collider into a
        /// corrupting write, which is why it is named here rather than left in a report.
        /// </para>
        /// </remarks>
        public static void ItsLookIsNotPartOfItsIdentity(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            ObjectAuthoring identity = root.GetComponent<ObjectAuthoring>();
            if (identity == null || identity.variationIsDynamic)
            {
                return;
            }

            identity.variationIsDynamic = true;
            UnityEditor.EditorUtility.SetDirty(identity);
        }

        /// <summary>
        /// Makes the object one the game sends to players.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>GhostPostConverter</c> returns immediately when the object has no
        /// <c>GhostAuthoringComponent</c>, so without one the entity never gets <c>GhostInstance</c>.
        /// That is not only a networking nicety: <c>ChaseStateSystem</c>, <c>ProjectileSystem</c>,
        /// <c>ProjectileMovementSystem</c> and <c>ExplosionDamageSystem</c> all name
        /// <c>GhostInstance</c> in their queries, so a ghost-less arrow never moves and a ghost-less
        /// blast never hurts anything. 1,727 of the game's own entity prefabs carry one.
        /// </para>
        /// <para>
        /// TWO OF THE FIELDS ARE NOT LEFT ALONE, and the rest deliberately are. Importance and the
        /// optimization mode are OVERWRITTEN at conversion:
        /// <c>GhostConfigFromIMonoBehaviourData.OverrideGhostConfig</c> sets both from the
        /// object's type — a placed prefab becomes Static at importance 1 or 2, a critter Dynamic
        /// at 2, a creature Static or Dynamic at 3 or 5, a boss 10 — and <c>ObjectAuthoring</c>
        /// implements <c>IEntityMonoBehaviourData</c>, so a generated object reaches it exactly as
        /// the game's own prefabs do. Writing either here would be writing a number the game
        /// throws away. The ghost's identity is likewise not the guid: Core Keeper never runs
        /// Unity's baker, and <c>GhostPostConverter</c> derives the ghost type from a hash of the
        /// GameObject's name.
        /// </para>
        /// <para>
        /// <c>SupportAutoCommandTarget</c> IS written, because nothing overwrites it and its own
        /// default is <c>true</c> while 2,070 of the 2,074 vanilla prefabs that carry a ghost
        /// serialize it <c>0</c>. The four that do not are the two hydra workbenches, the biome
        /// sampler and the player's ghost. WHAT IT DOES IS NARROWER THAN AN EARLIER REMARK HERE
        /// SAID: <c>GhostPostConverter</c> adds <c>AutoCommandTarget</c> only when
        /// <c>SupportAutoCommandTarget</c> AND <c>HasOwner</c> are both true, so on a chest, which
        /// has no owner, leaving it true never put an <c>AutoCommandTarget</c> on anything. It is
        /// load-bearing on exactly the objects that DO get an owner below, and it is written on
        /// everything so that the two answers cannot drift apart.
        /// </para>
        /// <para>
        /// <c>HasOwner</c> IS WRITTEN, AND IT IS THE ONE THAT WAS COSTING A CRASH. The component it
        /// produces, <c>GhostOwner</c>, is added at conversion only when this is true. The game
        /// then SETS that component without asking whether it is there:
        /// <c>RangeWeaponSlot</c> ends every spawn with <c>ecb.SetComponent&lt;GhostOwner&gt;</c>
        /// on the projectile it just made, and so does <c>EntityUtility</c> on the creature-fired
        /// path. <c>ExplosiveSystem</c> is the one that asks first, which is what makes the other
        /// two a deliberate assumption rather than an oversight: every one of the 80 vanilla
        /// prefabs that serializes <c>HasOwner: 1</c> is a projectile, a bomb, a trail or a
        /// predicted explosion. Nothing this framework generated had it, so the first shot from a
        /// modded bow reached a <c>SetComponent</c> on an archetype without the component and took
        /// the rest of that command buffer down with it.
        /// </para>
        /// <para>
        /// It is passed in rather than guessed, and it is false for everything placed: an owner
        /// puts the entity into <c>SetGhostRelevancySetSystem</c>'s owner query, which a chest does
        /// not belong in, and no vanilla chest, creature or workbench carries one.
        /// </para>
        /// </remarks>
        public static void IsSentToPlayers(GameObject root)
        {
            IsSentToPlayers(root, false);
        }

        /// <summary>
        /// The same, saying whether the thing is fired or thrown by somebody.
        /// </summary>
        /// <remarks>
        /// Pass true for a projectile, a bomb or a predicted blast — anything the game spawns from
        /// a weapon, an attack or an explosive, because those spawn paths write the owner onto it
        /// without checking. Pass false for anything that is placed, walks or grows.
        /// </remarks>
        public static void IsSentToPlayers(GameObject root, bool somebodyFiresIt)
        {
            if (root == null)
            {
                return;
            }

            Unity.NetCode.GhostAuthoringComponent ghost =
                root.GetComponent<Unity.NetCode.GhostAuthoringComponent>();
            if (ghost == null)
            {
                ghost = root.AddComponent<Unity.NetCode.GhostAuthoringComponent>();
            }

            // Both answers restated on every generate. The generators reload the previous prefab
            // and mutate it, so a ghost written before this pass keeps whatever it was given.
            ghost.HasOwner = somebodyFiresIt;
            ghost.SupportAutoCommandTarget = false;
        }

        /// <summary>
        /// Says every client works this object out for itself instead of watching the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ONE ANSWER IN THE GAME REQUIRES IT, AND SAYS SO OUT LOUD.
        /// <c>MortarProjectileConverter</c> ends with a check: an arcing shot that is set to land
        /// only where it can see, on an object whose supported modes are anything but predicted,
        /// prints "MortarProjectileAuthoring is set up with checkVisibility, but is not predicted.
        /// Currently we only support predicted behaviour as tileLookup has no history on server".
        /// A fresh ghost supports both modes, so an author who ticked that control got the error
        /// and a visibility test run against tiles the server does not keep.
        /// </para>
        /// <para>
        /// Both vanilla prefabs that use it — <c>CicadaBossBuzzMortarProjectileEntity</c> and
        /// <c>HydraBossShockwaveMortarProjectileEntity</c> — serialize
        /// <c>SupportedGhostModes: 2</c>, which is <c>Predicted</c>, and neither touches the
        /// default mode. Written on both branches so that unticking the control takes the
        /// narrowing back: everything else in the game supports both.
        /// </para>
        /// </remarks>
        public static void ItIsWorkedOutOnEveryClient(GameObject root, bool onlyThatWay)
        {
            if (root == null)
            {
                return;
            }

            Unity.NetCode.GhostAuthoringComponent ghost =
                root.GetComponent<Unity.NetCode.GhostAuthoringComponent>();
            if (ghost == null)
            {
                return;
            }

            ghost.SupportedGhostModes = onlyThatWay
                ? Unity.NetCode.GhostModeMask.Predicted
                : Unity.NetCode.GhostModeMask.All;
        }

        /// <summary>
        /// Gives the object a body other things can bump into, and that it can be pushed around by.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>PhysicsCollider</c> comes only from <c>PhysicsShapeAuthoring</c> and
        /// <c>PhysicsVelocity</c> only from a non-static <c>PhysicsBodyAuthoring</c>. Every attack
        /// and every explosion finds what it hits through the collision world, and every movement
        /// state in the game writes a velocity — <c>ChaseStateSystem</c>, <c>RandomWalkStateSystem</c>,
        /// <c>RandomFollowStateSystem</c>, <c>PetWalkStateSystem</c>,
        /// <c>FollowPheromoneStateSystem</c>, <c>MoveToPositionFromCommandStateSystem</c>,
        /// <c>ChargeAttackStateSystem</c>, <c>JumpAttackStateSystem</c>,
        /// <c>SnakeMovementStateSystem</c> and <c>SlimeBossSystem</c> all name
        /// <c>PhysicsVelocity</c>, and <c>ChaseStateRequest</c> and <c>ChargeAttackStateRequest</c>
        /// both demand <c>PhysicsCollider</c> before they will even consider the creature.
        /// </para>
        /// <para>
        /// The shape and the size are written through <c>SerializedObject</c> rather than through
        /// the component's own setters, because those setters take geometry types that live in an
        /// assembly the editor tools do not reference and reaching for them would mean widening the
        /// assembly graph for three numbers. This is the same road
        /// <c>DimensionRuntimeConsumerBootstrapUtility.ConfigureGhostAuthoringComponent</c> already
        /// takes. The LAYERS are written through the component's own properties, because that one
        /// type does live in an assembly these tools already reference and its serialized form is
        /// thirty-two separate booleans.
        /// </para>
        /// <para>
        /// A CREATURE CARRIES TWO SHAPES, not one, and the second is what makes it findable. The
        /// first is its body on Category04, colliding with 00, 04, 08 and 17. The second is a
        /// trigger on Category03 that collides with Category03 and raises trigger events instead of
        /// blocking. 63 of the 115 vanilla creature prefabs carry both. An earlier remark here said
        /// 70 of 137, which came from a classifier nobody wrote down; the 115 are the prefabs
        /// carrying EnemyAuthoring.
        /// The trigger is not decoration: <c>NearbyEntitiesTrackerSystem</c> overlaps against the
        /// watcher's own layer mask, so a creature that is only on Category04 is invisible to every
        /// watcher of Category03 — which is what a spike trap, a summoning circle and this file's
        /// own "notices what it can hurt" row all watch.
        /// </para>
        /// <para>
        /// THE NUMBERS ARE MEASURED, and each one says what it was compared against. Of the 115
        /// vanilla prefabs carrying <c>EnemyAuthoring</c>, 64 carry a Category04 body ball: 55 put
        /// it at <c>(0, 0.4, 0)</c> and 7 at <c>(0, 0.4, -0.4)</c>; 31 use radius 0.375 and 13 use
        /// 0.25; 59 of them collide with <c>00,04,08,17</c> and answer with
        /// <c>CollideRaiseCollisionEvents</c>. The size is twice the radius on every one.
        /// <c>LarvaEntity</c>'s own <c>z = -0.4</c> is a minority of seven, and an earlier pass
        /// copied it as if it were the rule. 67 carry the Category03 trigger as well, and 63 of the 67
        /// collide with Category03 alone and answer with trigger events. "All but one" was said
        /// here and it is all but four: RobotBossLegEntity, CicadaEnemyEntity and
        /// CicadaEnemyNatureEntity collide with 131419, and MoldTentacleEntity with 14 answering
        /// no collision. The 63/63 the census printed was 63 of 67, made unanimous by counting
        /// only the ones that agreed. The per-kind
        /// table this and <see cref="TheBodyVanillaGivesIt"/> come from is in
        /// E:\ck mods\ck-research\query-match-census.md, under the measured collider table, which
        /// now states for every row whether its agreement count covers the layers, the size and
        /// centre, or both.
        /// </para>
        /// </remarks>
        public static void HasABodyThingsCanTouch(
            GameObject root,
            float width,
            float height,
            Action<string> say)
        {
            HasABodyThingsCanTouch(root, width, height, width, say);
        }

        /// <summary>
        /// The same, with the trigger sized separately from the body.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE TWO SHAPES ARE NOT THE SAME SIZE IN THE GAME. 63 vanilla creature prefabs carry
        /// both a Category04 body ball and a Category03 trigger ball; 39 give them the same radius
        /// and 24 do not. <c>LarvaEntity</c>, the prefab this method's own remark names, is one of
        /// the 24 — body 0.25, trigger 0.375 — and so are the crab, the robots and the nymph
        /// (<c>GoldenBombScarabEntity</c> 0.25 against 0.6, <c>CicadaNymphEntity</c> 0.5 against
        /// 1.0). Welding them to one number makes a small creature that other things notice from
        /// further away unbuildable, which is the exact arrangement most of the game's own small
        /// creatures use.
        /// </para>
        /// <para>
        /// Both numbers are tiles across, off the creature's own controls. The radius is
        /// 0.375 × that: 31 of the 64 vanilla body balls are radius 0.375 and 40 of the 64 trigger
        /// balls are, so one tile across reproduces the commonest creature in the game.
        /// </para>
        /// </remarks>
        public static void HasABodyThingsCanTouch(
            GameObject root,
            float width,
            float height,
            float widthOthersNoticeItAt,
            Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            float radius = CreatureBodyRadius * (width > 0f ? width : 1f);
            float noticedRadius = CreatureBodyRadius *
                (widthOthersNoticeItAt > 0f ? widthOthersNoticeItAt : (width > 0f ? width : 1f));
            Vector3 middle =
                new Vector3(0f, CreatureBodyCentreHeight * (height > 0f ? height : 1f), 0f);

            // Written on every generate rather than only when the component is new. The generators
            // reload the previous prefab and mutate it, so a shape added by an older pass keeps
            // whatever it was given — which for every creature made before this was a box on all
            // thirty-two layers. Generation is authoritative, so the values are re-stated.
            WriteTheShape(
                ShapeOn(root, 0),
                Ball(radius, middle, CreatureBelongsTo, CreatureCollidesWith, ItCollidesAndSaysSo),
                say);

            WriteTheShape(
                ShapeOn(root, 1),
                Ball(
                    noticedRadius,
                    middle,
                    EnemyTriggerLayer,
                    EnemyTriggerLayer,
                    ItOnlyTellsSystemsItTouched),
                say);

            Unity.Physics.Authoring.PhysicsBodyAuthoring body =
                root.GetComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
            if (body == null)
            {
                body = root.AddComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
            }

            // Dynamic, which is what LarvaEntity and CowEntity serialize. It is set through the
            // serialized field rather than the MotionType property because the property's type
            // lives in Unity.Physics.Hybrid, an assembly these editor tools do not reference —
            // and a body that came out Static would carry no PhysicsVelocity, which is the one
            // component ten movement systems all name.
            //
            // The damping is LarvaEntity's too. Unity's defaults are 0.01 and 0.05, which leaves a
            // creature sliding after knockback and free to tumble; the game's own creatures damp
            // linear motion hard and lock rotation outright.
            UnityEditor.SerializedObject bodyObject = new UnityEditor.SerializedObject(body);
            bodyObject.Update();
            SetEnum(bodyObject, "m_MotionType", 0, say);
            SetFloat(bodyObject, "m_LinearDamping", 10f, say);
            SetFloat(bodyObject, "m_AngularDamping", float.PositiveInfinity, say);
            bodyObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Gives a critter the body Core Keeper's own critters have.
        /// </summary>
        /// <remarks>
        /// 40 of the 50 vanilla critter prefabs are one sphere of radius 0.15 at
        /// <c>(0, 0.4, 0)</c> on Category15 — the critter layer — colliding with 00, 08 and 17 and
        /// blocking normally, on a dynamic body. None of the 50 carries the Category03 trigger a
        /// creature carries, and the layer is the whole difference between a critter and a
        /// creature: a critter on the creature layers is chased, attacked and counted as an enemy
        /// by everything that watches for one.
        /// </remarks>
        public static void HasTheBodyACritterHas(GameObject root, Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            WriteTheShape(
                ShapeOn(root, 0),
                Ball(
                    CritterBodyRadius,
                    new Vector3(0f, CreatureBodyCentreHeight, 0f),
                    CritterBelongsTo,
                    CritterCollidesWith,
                    ItBlocks),
                say);

            Unity.Physics.Authoring.PhysicsBodyAuthoring body =
                root.GetComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
            if (body == null)
            {
                body = root.AddComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
            }

            UnityEditor.SerializedObject bodyObject = new UnityEditor.SerializedObject(body);
            bodyObject.Update();
            SetEnum(bodyObject, "m_MotionType", 0, say);
            SetFloat(bodyObject, "m_LinearDamping", 10f, say);
            SetFloat(bodyObject, "m_AngularDamping", float.PositiveInfinity, say);
            bodyObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>What sort of placed thing this is, which is what decides its body.</summary>
        /// <remarks>
        /// It is not a new question put to the author. Every entry is read off something the author
        /// already answered somewhere else — the ground-cover tick, the world-object kind, whether
        /// the thing crafts, whether it holds items, whether it is ridden — so nothing here can
        /// contradict a control, and no control was added for it.
        /// </remarks>
        public enum PlacedThingKind
        {
            /// <summary>
            /// An ordinary placed prop a player walks into. 254 of the 977 vanilla prop shapes.
            /// </summary>
            Decoration = 0,

            /// <summary>
            /// Grass, a rug, a floor tile. 41 vanilla prefabs, of which 29 agree on the layers and
            /// 24 of the 29 boxes on the geometry. "All one answer" was said here and it is a
            /// plurality: the other 12 watch the enemy trigger as well.
            /// </summary>
            GroundCover = 1,

            /// <summary>
            /// Something you put items into. 192 vanilla containers carrying 218 shapes, of
            /// which 110 agree on the layers and 142 of the 197 boxes on the geometry.
            /// </summary>
            Chest = 2,

            /// <summary>
            /// A crafting station. 73 vanilla ones carrying 95 shapes, of which 38 agree on the
            /// layers and 56 of the 88 boxes on the geometry.
            /// </summary>
            Workbench = 3,

            /// <summary>
            /// A bed. <c>BedEntity</c> is the only one in the game, and it is TWO slabs.
            /// </summary>
            Bed = 4,

            /// <summary>
            /// A door across the corridor. 9 of the 34 vanilla door prefabs, and the shut half of
            /// the pair the game ships for every door.
            /// </summary>
            Door = 5,

            /// <summary>A trophy that summons what it commemorates. 83 vanilla, all identical.</summary>
            Trophy = 6,

            /// <summary>
            /// A kart. 14 of the 18 vanilla vehicles, and the plurality shape.
            /// </summary>
            /// <remarks>
            /// The name is kept from the pass that had one vehicle answer, so nothing that already
            /// hands this in changes meaning: it is still what a vehicle gets when nothing says it
            /// is a boat or a minecart, and a kart is what 14 of the 18 are.
            /// </remarks>
            Vehicle = 7,

            /// <summary>
            /// A drill, a sprinkler, a planter. 16 vanilla machines carrying 28 shapes, of which
            /// 12 agree on the layers and 16 of the 28 boxes on the geometry.
            /// </summary>
            Machine = 8,

            /// <summary>A crop. 35 of the 38 vanilla plants that have a body share one answer.</summary>
            Plant = 9,

            /// <summary>A planted seed. 26 vanilla, all one answer.</summary>
            Seed = 10,

            /// <summary>
            /// A boat. <c>BoatEntity</c> and <c>SpeederBoatEntity</c>, and its shape is very
            /// nearly nothing.
            /// </summary>
            Boat = 11,

            /// <summary>A minecart. <c>MinecartEntity</c> and <c>WonkyMinecartEntity</c>.</summary>
            Minecart = 12,

            /// <summary>
            /// A door along the corridor, in a wall that runs north to south. 8 of the 34 vanilla
            /// door prefabs, shipped as separate objects from the ones across.
            /// </summary>
            DoorAlongTheWall = 13,

            /// <summary>
            /// A prop a player walks through that still blocks placement and can still be hit — a
            /// torch, a candle, a lamp, a painting. 277 of the 977 vanilla prop shapes.
            /// </summary>
            WalkThroughProp = 14,

            /// <summary>
            /// Ground cover that is also wired: a conveyor belt.
            /// </summary>
            /// <remarks>
            /// Ground cover wins over machine when an object is both, which is right — a belt is
            /// walked over — but the machine answer is the only one that carries the electrical
            /// layer, so a modded belt lost the layer its own wiring reads.
            /// <c>ConveyorBeltForwardEntity</c> carries Category09, Category12 and Category14 and
            /// answers no collision, so Category12 belongs on it whichever of the two rows it is
            /// read as.
            /// </remarks>
            PoweredGroundCover = 15,

            /// <summary>
            /// A machine that moves items instead of chewing ground: a robot arm, an item
            /// collector, a pulse circuit. 14 of the 36 vanilla machine shapes.
            /// </summary>
            /// <remarks>
            /// It is not a new question either. The answers that make something a machine split
            /// cleanly in two on the prefabs — the movers watch Category02 and Category04 and the
            /// drills, sprinklers and lamps also watch Category15 — so the side is read off which
            /// automation answer the object already carries.
            /// </remarks>
            MachineThatMovesItems = 16
        }

        /// <summary>
        /// Gives a placed object the body Core Keeper's own object of the same sort has.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY EVERY PLACED THING NEEDS ONE. Being hit in Core Keeper is a shape cast against the
        /// collision world — <c>AttackSystem.CheckForHit</c> is a <c>BoxCastAll</c>, and the
        /// player's own target picker builds its whole candidate list from a <c>SphereCastAll</c>.
        /// Every "can this be hit" test — mineable, destructible, damageable, attackable with
        /// melee, not hittable, ground decoration — is a lookup INSIDE that loop. An entity with no
        /// <c>PhysicsCollider</c> is not in the collision world at all, so it is never a candidate:
        /// a chest that cannot be broken, a statue you walk through, a plant no tool can harvest,
        /// and not one line printed anywhere. Core Keeper puts shapes on all of them, and the counts an earlier remark
        /// gave here were from a classifier nobody wrote down. Re-measured off named marker
        /// components, the population is 892 prop prefabs carrying 977 shapes, 192 containers
        /// carrying 218, 73 crafting stations carrying 95, 34 doors carrying 65, 18 vehicles, 26
        /// seeds and one bed carrying two. Having a shape is not the same as being solid, which
        /// is what "949 of its 950 props" conflated: 277 of those 977 prop shapes are the
        /// walk-through class and 41 more are ground cover. It puts a BODY on almost none of
        /// them: a placed thing is a static collider, and only creatures, critters and thrown
        /// projectiles get a body.
        /// </para>
        /// <para>
        /// ONE ANSWER FOR EVERYTHING IS WRONG, and it is the mistake the pass before this one made.
        /// A boat and a minecart are on Category06, the layer nothing blocks against, which is what
        /// lets a player walk onto one; put on Category00 they become a hole in the map that
        /// creatures path around. Ground cover is on Category10 and blocks nothing at all; put on
        /// Category06 the placement check refuses to put anything on that tile and a sword hits the
        /// rug. A crop and a seed answer no collision at all; left at the fresh default they
        /// collide. The table below is measured per sort of thing, off the prefabs named against
        /// each entry, and the whole of it is in
        /// E:\ck mods\ck-research\query-match-census.md under the measured collider table.
        /// </para>
        /// <para>
        /// THE SIZE COMES FROM THE OBJECT'S OWN FOOTPRINT, not a constant, for every sort whose
        /// shape is a box round the tiles it stands on. The centre is
        /// <c>((wide - 1) / 2 + cornerOffset.x, 0.5, (tall - 1) / 2 + cornerOffset.y)</c>, and
        /// here is what that was measured against: of the 523 vanilla prefabs whose single box is
        /// exactly its footprint, the x and the z terms hold on 523 of 523 with no exceptions, and
        /// the <c>y = 0.5</c> term holds on 404. All 119 that fail y answer no collision at all —
        /// 103 floor coverings sunk to <c>y = -0.5</c> and 16 at <c>y = 0</c> — so among the
        /// shapes this method writes as solid the exceptions are six posts. An earlier remark here
        /// said "547 of 547, zero exceptions", which was true of x and z and was never checked in
        /// y. <c>PlanterBoxEntity</c> does satisfy it: its box sits at <c>x = 0</c> because it
        /// carries <c>prefabCornerOffset.x = -1</c>.
        /// </para>
        /// <para>
        /// THE CORNER OFFSET IS THE AUTHOR'S NOW. It is read from
        /// <c>PlaceableObjectAuthoring.prefabCornerOffset</c>, which is where a mod object's
        /// footprint really lives — <c>ObjectAuthoring.ObjectInfo</c> copies both fields off that
        /// component, so for anything this framework builds the two agree. On a vanilla prefab
        /// built the old way they do NOT agree, and the census cited the wrong one of the two.
        /// Until this pass nothing in the tree ever wrote a non-zero offset, so the term could not
        /// do anything; the world object asset now asks for it in words.
        /// </para>
        /// <para>
        /// A door and a trophy do not use the footprint at all; both carry a shape of their own
        /// that every vanilla prefab of that sort repeats.
        /// </para>
        /// </remarks>
        public static void HasTheBodyAPlacedThingHas(
            GameObject root,
            int tilesWide,
            int tilesTall,
            PlacedThingKind kind,
            Vector2 cornerOffset,
            Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            float wide = tilesWide > 0 ? tilesWide : 1;
            float tall = tilesTall > 0 ? tilesTall : 1;

            // A LIST, NOT ONE SHAPE. A bed is two slabs with a gap a player stands in, and the
            // pass that wrote one box over the whole footprint made a modded bed a two-tile wall
            // where the game's own bed is two thin ends. Every other sort still returns one.
            MeasuredShape[] measured = TheBodyVanillaGivesIt(kind, wide, tall, cornerOffset);

            // ANYTHING THAT PUSHES HAS TO BE ON THE PUSH LAYER, whatever sort of thing it is.
            // PlayerControllerBurstableStatics builds one filter for this and one only —
            // CollidesWith Category14, BelongsTo everything — and PlayerVelocityCalculationSystem
            // uses it to find what is shoving the player about. Nothing in this framework wrote
            // Category14 anywhere, so the "pushes what stands on it" control produced a belt that
            // converted, drew and moved nobody: the query that reads it never found the object.
            // ConveyorBeltForwardEntity carries the layer, and it is added here rather than in a
            // branch because a belt is not the only thing that can push — the answer the author
            // gave is what decides it.
            if (measured.Length > 0 && HasNamed(root, "VelocityAffectorAuthoring"))
            {
                measured[0].BelongsTo |= VelocityAffectorLayer;
            }

            for (int i = 0; i < measured.Length; i++)
            {
                WriteTheShape(ShapeOn(root, i), measured[i], say);
            }

            // NOTHING ELSE MAY BE LEFT ON IT. A shape written by an earlier generate stays on the
            // prefab, because every generator reloads the previous prefab and mutates it and
            // ShapeOn only ever adds. Until a bed was two slabs every placed sort wrote exactly
            // one shape, so there was never a surplus; author a bed, change what it is, regenerate
            // and the footboard stayed behind as a solid box a tile away, blocking a tile, with
            // nothing short of deleting the prefab to clear it.
            TakeOffAnyShapeBeyond(root, measured.Length, say);

            // NO BODY, deliberately. A PhysicsBodyAuthoring that is not Static produces a
            // PhysicsVelocity and a mass, and a placed object with those is shoved around by
            // anything that walks into it. Core Keeper gives a body to creatures, critters and
            // thrown projectiles and to almost nothing else that is placed.
        }

        /// <summary>The shapes Core Keeper's own object of that sort carries.</summary>
        /// <remarks>
        /// <para>
        /// Every branch names the prefabs it was measured from and how many of them agree. Where a
        /// sort splits, the plurality is taken and the split is written into the census rather than
        /// averaged into something no vanilla object has.
        /// </para>
        /// <para>
        /// AN AGREEMENT COUNT HERE COVERS THE SIZE AND THE CENTRE AS WELL AS THE LAYERS. The pass
        /// before this one printed counts that had only ever compared the layers and the collision
        /// response, and two of them were badly wrong when the geometry was looked at:
        /// "bed 2/2" was two slabs against one box, and "vehicle 18/18" was three different shapes
        /// against one — 2 of 18 on the geometry. Where the two counts differ, both are given.
        /// </para>
        /// </remarks>
        private static MeasuredShape[] TheBodyVanillaGivesIt(
            PlacedThingKind kind,
            float wide,
            float tall,
            Vector2 cornerOffset)
        {
            Vector3 footprint = new Vector3(wide, 1f, tall);
            Vector3 overItsTiles = new Vector3(
                ((wide - 1f) * 0.5f) + cornerOffset.x,
                0.5f,
                ((tall - 1f) * 0.5f) + cornerOffset.y);

            switch (kind)
            {
                // GROUND COVER. Layers and response: 29 of 41. The other 12 are the rugs and the
                // big floor tiles, which watch Category03 as well. Size and centre: 24 of the 29
                // boxes are the footprint at the formula centre. Named off HiveBone1Entity,
                // MoldVeinEntity0 and BirdPoopEntity, which are three of the 29 — the pass before
                // named EerieRugEntity and BigFloorTileCavelingEntity here, and both of those are
                // in the 12 that disagree with the number written below. Category10 is the layer
                // nothing blocks against and nothing casts at, which is why a vanilla rug can be
                // built on and is not hit by a sword.
                case PlacedThingKind.GroundCover:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        GroundCoverBelongsTo,
                        PlayerTriggerLayer,
                        NothingBlocksAgainstIt));

                // GROUND COVER THAT IS ALSO WIRED. The ground-cover answer wins for a conveyor
                // belt, because a belt is walked over, and until this row existed that also threw
                // away Category12 — the layer the wiring reads and the only place the machine
                // answer put it.
                //
                // WHAT CONVEYORBELTFORWARDENTITY REALLY CARRIES, since this row cited it and got
                // it wrong twice. It belongs to Category09, Category12 and Category14 — 20992, not
                // the 5120 written here — its box is (1.00001, 2, 1.00001) at (0, 0, 0) rather
                // than the footprint at the formula centre, it watches Category01, and it carries
                // neither a GroundDecorationAuthoring nor any automation answer: its components
                // are the mineable, health, placeable, state, damage-reduction, direction and
                // VelocityAffectorAuthoring set. So it is not an example of this branch being
                // reached, and it is not what this branch writes. What it is good for is the one
                // bit that matters: Category14 is VelocityAffectorTrigger, and that is written
                // here by the push rule in HasTheBodyAPlacedThingHas, off the "pushes what stands
                // on it" answer, rather than by this row.
                //
                // Category10 is kept over the belt's Category09 because that is the layer the
                // ground-cover tick asked for, and Category09 blocks where Category10 does not.
                case PlacedThingKind.PoweredGroundCover:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        GroundCoverBelongsTo | ElectricalTriggerLayer,
                        PlayerTriggerLayer,
                        NothingBlocksAgainstIt));

                // A PROP A PLAYER WALKS THROUGH. 277 of the 977 vanilla prop shapes are on
                // Category06 answering nothing at all: TorchEntity and PaintingBackWallEntity are
                // two of them, both watching Category01 and Category03 together.
                // CandleEntity was named here as a third and it is not one: it watches 20,
                // Category02 and Category04. That is the same false-exemplar mistake this pass
                // corrected one row up, made in the row the pass added.
                //
                // WHICH OF THE TWO WATCH MASKS IS RIGHT, and what the counts really compare. Over
                // all 366 vanilla shapes that are Category06 alone answering nothing, 143 watch
                // Category01 alone and 130 watch Category01 and Category03 together, so a plain
                // plurality over that whole population says Category01 alone. It is the wrong
                // population: the 143 are the things a player walks OVER — every rug
                // (RugEntity and its fifteen colours), WovenMat, PetBedEntity, RailEntity, the
                // ground slimes, the immunity emitters, the glass and wood floor pieces, and the
                // planted seeds — while the 130 are the things a player walks PAST at eye level,
                // which is what this row is for: TorchEntity, ChineseWallLanternBackWallEntity,
                // CalendarBackWallEntity, PaintingBackWallEntity and the wall decorations. The
                // framework already sends a rug to GroundCover and a seed to Seed, so the pair is
                // taken off the class this row actually serves.
                //
                // AN EARLIER NOTE HERE SAID THE PAIR "can only add watchers, never remove one".
                // That is false and it should not be relied on again: Unity Physics filtering is
                // symmetric — a pair matches only when each side's BelongsTo is in the other's
                // CollidesWith — so widening a watch mask does change what the prop touches.
                //
                // Size: the framework writes the footprint box at the formula centre, and among
                // the 275 Category06 boxes exactly 21 are that on type, size and centre together.
                // 148 agree in x and z and 114 of those also in y; the widest single agreement in
                // the class is 93 boxes at (1,1,1) centred (0,-0.5,0), which is a shape sunk below
                // the floor and belongs to the rugs rather than to a torch. The real props here
                // are SMALLER than their tile — TorchEntity is (0.5,1,0.5) and
                // PaintingBackWallEntity (0.5,1,0.8) at (0,0.5,-0.4) — and there is no control on
                // this framework that would size a prop smaller than the tile it stands on, so the
                // footprint is what is written and 21 of 275 is what that is worth.
                //
                // THIS IS THE THIRD ANSWER THE PASS BEFORE HAD NO ROOM FOR. Its only two were a
                // solid Category00 box and the ground-cover tick, so a modded torch was either a
                // wall or a rug no sword could hit and no placement noticed. Category06 is inside
                // PlaceObjectStateRequest's refusal mask and inside the melee cast, so this one
                // blocks building on its tile and takes a hit, and a player still walks past it.
                case PlacedThingKind.WalkThroughProp:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        WalkThroughBelongsTo,
                        PlayerTriggerLayer | EnemyTriggerLayer,
                        NothingBlocksAgainstIt));

                // 35 of the 38 vanilla plants that carry a shape, and the 35 agree on the layers,
                // the response, the radius and the centre alike: CarrockPlantEntity,
                // BloatOatPlantEntity. A ball 0.6 across, no collision, seen by the player and
                // enemy trigger layers so it can still be walked over and harvested.
                case PlacedThingKind.Plant:
                    return One(Ball(
                        CropRadius,
                        new Vector3(0f, 0.5f, 0f),
                        WalkThroughBelongsTo,
                        PlayerTriggerLayer | EnemyTriggerLayer,
                        NothingBlocksAgainstIt));

                // 26 of 26 on the layers, the response, the radius and the centre:
                // CarrockSeedEntity, BloatOatSeedEntity. The plant's ball, watching the player's
                // trigger only.
                case PlacedThingKind.Seed:
                    return One(Ball(
                        CropRadius,
                        new Vector3(0f, 0.5f, 0f),
                        WalkThroughBelongsTo,
                        PlayerTriggerLayer,
                        NothingBlocksAgainstIt));

                // A KART. All 18 vanilla vehicles agree on the layers and the response — that is
                // what the 18 of 18 the pass before printed had compared — and they are THREE
                // different shapes, so one answer for all of them matched 2 of 18 on size and
                // centre. This is the plurality of the three: 14 karts, measured off
                // PrimitiveGoKartEntity, RenegadeGoKartEntity, SpeederGoKartEntity and the ten
                // RuinKart prefabs, every one of them box (1, 1, 0.75) at (0, 0.5, 0.125).
                // Category06 is the whole point — a player boards a vehicle by walking into the
                // tile it is on, and a vehicle on Category00 is a hole in the map that enemies
                // path around instead.
                case PlacedThingKind.Vehicle:
                    return One(Box(
                        new Vector3(1f, 1f, KartDepth),
                        new Vector3(cornerOffset.x, 0.5f, KartCentreDepth + cornerOffset.y),
                        WalkThroughBelongsTo,
                        SolidCollidesWith,
                        ItBlocks));

                // A BOAT, AND ITS SHAPE IS VERY NEARLY NOTHING. 2 of 2: BoatEntity and
                // SpeederBoatEntity are box (0.01, 1, 0.01) at (0, 0.5, 0.125). Giving a boat the
                // whole tile it stands on makes it refuse every placement on that tile and
                // swallow melee swings across all of it, because Category06 is inside
                // PlaceObjectStateRequest's refusal mask; the game's own boat does neither.
                case PlacedThingKind.Boat:
                    return One(Box(
                        new Vector3(BoatSide, 1f, BoatSide),
                        new Vector3(cornerOffset.x, 0.5f, KartCentreDepth + cornerOffset.y),
                        WalkThroughBelongsTo,
                        SolidCollidesWith,
                        ItBlocks));

                // A MINECART. 2 of 2: MinecartEntity and WonkyMinecartEntity are box (1, 1, 1) at
                // (0, 0.5, 0) — the footprint at the footprint centre, which is the one of the
                // three vehicle shapes the pass before wrote for all eighteen.
                case PlacedThingKind.Minecart:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        WalkThroughBelongsTo,
                        SolidCollidesWith,
                        ItBlocks));

                // 110 of the 218 chest shapes on layers and response, and 142 of the 197 chest
                // boxes are the footprint at the formula centre: ChestEntity. The only sort of
                // placed thing that watches the critter layer as well, so a critter cannot walk
                // through a chest.
                case PlacedThingKind.Chest:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        SolidBelongsTo,
                        SolidCollidesWith | CritterColliderLayer,
                        ItBlocks));

                // A DOOR ACROSS THE CORRIDOR. Layers and response: 48 of the 65 door shapes.
                // Size and centre: 9 of the 34 vanilla door prefabs — WoodDoorEntity,
                // CoralDoorEntity, GalaxiteDoorEntity — are box (2, 5, 0.3) at (0, 2.5, 0). A door
                // is a slab two tiles across and five high, NOT the one-tile cube its footprint
                // would give, and it deliberately does not watch Category00.
                case PlacedThingKind.Door:
                    return One(Box(
                        new Vector3(2f, DoorHeight, DoorThickness),
                        new Vector3(0f, DoorHeight * 0.5f, 0f),
                        SolidBelongsTo,
                        DoorCollidesWith,
                        ItBlocks));

                // A DOOR ALONG THE WALL, which the game ships as its own object:
                // WoodDoorVertEntity, CoralDoorVertEntity, GalaxiteDoorVertEntity and five more
                // are box (0.3, 5, 2) at (0, 2.5, 0), 8 prefabs against the 9 across.
                //
                // EVERY VANILLA DOOR IS ONE TILE BY ONE TILE. The pass before this one chose
                // between the two slabs by asking whether the footprint was wider than it was
                // tall, which on a one-by-one door is always true, so every modded door was an
                // across door: set in a north-south wall it blocked two tiles sideways and 0.3 of
                // a tile along the corridor a player was walking down. The choice is now the
                // author's, because nothing on the object can tell you which way its doorway runs.
                case PlacedThingKind.DoorAlongTheWall:
                    return One(Box(
                        new Vector3(DoorThickness, DoorHeight, 2f),
                        new Vector3(0f, DoorHeight * 0.5f, 0f),
                        SolidBelongsTo,
                        DoorCollidesWith,
                        ItBlocks));

                // 83 of 83 on the layers, the response, the size and the centre alike:
                // AcidLarvaTrophyEntity, AzeosTrophyEntity. This is the one row in the table whose
                // agreement count survived being re-measured against the geometry.
                case PlacedThingKind.Trophy:
                    return One(Box(
                        new Vector3(0.8f, 0.8f, 0.7f),
                        new Vector3(0f, 0.3f, -0.1f),
                        SolidBelongsTo,
                        SolidCollidesWith,
                        ItBlocks));

                // A MACHINE THAT CHEWS GROUND OR STANDS AND WORKS. Category12 beside Category00 is
                // the electrical trigger the wiring reads, and 36 vanilla shapes carry that pair.
                // They split by what the machine does, not at random: 17 also watch Category15 —
                // the thirteen drills, plus SprinklerEntity, LampEntity, SirenLampEntity and
                // ChineseLanternEntity — and that is the branch here. 14 do not, and they are all
                // the item movers, which now have a branch of their own; the last 5 are the four
                // crafting machines, which add Category00 as well, and TableSawEntity.
                //
                // The pass before this one named CrudeDrillForwardEntity, RobotArmForwardEntity
                // and ItemCollectorEntity as proof of this value. Only the drill is: the arm and
                // the collector watch 20 and have no Category15, so every framework machine
                // watched the critter layer that the game's own arms and collectors do not, and
                // critters got stuck on a modded robot arm.
                //
                // Of the 28 boxes measured for the census row, 16 are the footprint at the formula
                // centre. The drills also carry a second shape on Category14 reaching into the
                // tile they chew — CrudeDrillForwardEntity's is (1,1,2) at (0,0,0.5) — and this
                // framework has no control that sizes that, so it writes the first only.
                case PlacedThingKind.Machine:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        SolidBelongsTo | ElectricalTriggerLayer,
                        DoorCollidesWith,
                        ItBlocks));

                // A MACHINE THAT MOVES ITEMS. 14 of the 36: ItemCollectorEntity, the four
                // RobotArm prefabs, the four RobotFarmArm prefabs and the four PulseCircuit
                // prefabs are all Category00 plus Category12 watching 20 — Category02 and
                // Category04 and NOT Category15. A critter walks through the game's own robot arm
                // and used to be stopped by a modded one.
                case PlacedThingKind.MachineThatMovesItems:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        SolidBelongsTo | ElectricalTriggerLayer,
                        PlayerColliderLayer | EnemyColliderLayer,
                        ItBlocks));

                // A BED IS TWO SLABS WITH A GAP, which is why a player can stand in the middle of
                // one. BedEntity, the only bed in the game, carries box (1, 1, 0.2) at
                // (0, 0.5, -0.3) and box (1, 1, 0.7) at (0, 0.5, 1.15) over a footprint of 1 by 2
                // — a headboard and a footboard, open from z -0.2 to z 0.8. Both slabs are
                // Category00, both watch 00, 02 and 04 and both block, so the "2 of 2" the pass
                // before printed was true of the layers and of nothing else: it wrote ONE box over
                // the whole footprint and a modded bed was a two-tile wall.
                //
                // The headboard sits at the near edge and the footboard at the far one, so the gap
                // grows with the footprint. On a bed one tile deep the two meet, which is the
                // honest reading of a shape only ever measured on a bed two tiles deep.
                case PlacedThingKind.Bed:
                    return new MeasuredShape[]
                    {
                        Box(
                            new Vector3(wide, 1f, BedHeadboardDepth),
                            new Vector3(
                                ((wide - 1f) * 0.5f) + cornerOffset.x,
                                0.5f,
                                BedHeadboardCentre + cornerOffset.y),
                            SolidBelongsTo,
                            SolidCollidesWith,
                            ItBlocks),
                        Box(
                            new Vector3(wide, 1f, BedFootboardDepth),
                            new Vector3(
                                ((wide - 1f) * 0.5f) + cornerOffset.x,
                                0.5f,
                                (tall - 1f) + BedFootboardCentre + cornerOffset.y),
                            SolidBelongsTo,
                            SolidCollidesWith,
                            ItBlocks),
                    };

                // Decoration and Workbench land here. Layers and response: 254 of the 977 prop
                // shapes and 38 of the 95 crafting-station shapes are Category00 watching 00, 02
                // and 04 and blocking. Size and centre: 141 of the 839 prop boxes and 56 of the 88
                // crafting-station boxes are the footprint at the formula centre. Both are
                // pluralities of a wide spread rather than near-unanimities: the prop class splits
                // three ways, and the two readings that are not this one are reachable through the
                // ground-cover tick and through WalkThroughProp.
                default:
                    return One(Box(
                        footprint,
                        overItsTiles,
                        SolidBelongsTo,
                        SolidCollidesWith,
                        ItBlocks));
            }
        }

        /// <summary>
        /// Takes off any body shape past the ones this generate wrote, and says it did.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The generators reload the previous prefab and mutate it, and <see cref="ShapeOn"/> only
        /// ever adds, so a shape an earlier generate wrote survives one that writes fewer. That
        /// could not happen while every placed sort wrote exactly one shape; a bed writes two, so
        /// changing a bed into anything else left its footboard behind — a solid box a tile away
        /// from the object, blocking a tile, with no control that clears it.
        /// </para>
        /// <para>
        /// It is deliberately NOT used on a creature or a critter. Those write a fixed pair every
        /// time, so there is never a surplus to take off, and reaching into them from here would
        /// be a second pass with an opinion about a shape it did not write.
        /// </para>
        /// </remarks>
        private static void TakeOffAnyShapeBeyond(GameObject root, int howMany, Action<string> say)
        {
            if (root == null || howMany < 0)
            {
                return;
            }

            Unity.Physics.Authoring.PhysicsShapeAuthoring[] present =
                root.GetComponents<Unity.Physics.Authoring.PhysicsShapeAuthoring>();
            for (int i = present.Length - 1; i >= howMany; i--)
            {
                if (present[i] == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(present[i], true);
                if (say != null)
                {
                    say("was carrying a body shape left over from when it was a different sort of " +
                        "thing, and it has been taken off. A leftover shape blocks the tile it " +
                        "sits on even though nothing on the object draws anything there.");
                }
            }
        }

        /// <summary>One shape, for the sorts of thing that carry exactly one.</summary>
        private static MeasuredShape[] One(MeasuredShape only)
        {
            return new MeasuredShape[] { only };
        }

        /// <summary>One measured shape, ready to be written onto a component.</summary>
        private struct MeasuredShape
        {
            public bool IsABall;
            public float Radius;
            public Vector3 Size;
            public Vector3 Centre;
            public uint BelongsTo;
            public uint CollidesWith;
            public int HowItAnswersATouch;
        }

        private static MeasuredShape Box(
            Vector3 size,
            Vector3 centre,
            uint belongsTo,
            uint collidesWith,
            int howItAnswersATouch)
        {
            MeasuredShape shape = new MeasuredShape();
            shape.IsABall = false;
            shape.Radius = 0f;
            shape.Size = size;
            shape.Centre = centre;
            shape.BelongsTo = belongsTo;
            shape.CollidesWith = collidesWith;
            shape.HowItAnswersATouch = howItAnswersATouch;
            return shape;
        }

        /// <summary>
        /// A ball. Its size is twice its radius on every vanilla prefab that has one, and
        /// <c>PhysicsShapeAuthoring.OnValidate</c> reconciles the two, so both are written.
        /// </summary>
        private static MeasuredShape Ball(
            float radius,
            Vector3 centre,
            uint belongsTo,
            uint collidesWith,
            int howItAnswersATouch)
        {
            MeasuredShape shape = new MeasuredShape();
            shape.IsABall = true;
            shape.Radius = radius;
            shape.Size = new Vector3(radius * 2f, radius * 2f, radius * 2f);
            shape.Centre = centre;
            shape.BelongsTo = belongsTo;
            shape.CollidesWith = collidesWith;
            shape.HowItAnswersATouch = howItAnswersATouch;
            return shape;
        }

        private static void WriteTheShape(
            Unity.Physics.Authoring.PhysicsShapeAuthoring shape,
            MeasuredShape measured,
            Action<string> say)
        {
            if (shape == null)
            {
                return;
            }

            UnityEditor.SerializedObject shapeObject = new UnityEditor.SerializedObject(shape);
            shapeObject.Update();
            SetEnum(
                shapeObject,
                "m_ShapeType",
                measured.IsABall ? ShapeTypeSphere : ShapeTypeBox,
                say);
            if (measured.IsABall)
            {
                SetFloat(shapeObject, "m_SphereRadius", measured.Radius, say);
            }

            SetFloat3(shapeObject, "m_PrimitiveSize", measured.Size, say);
            SetFloat3(shapeObject, "m_PrimitiveCenter", measured.Centre, say);
            SetHowItAnswersATouch(shapeObject, measured.HowItAnswersATouch, say);
            shapeObject.ApplyModifiedPropertiesWithoutUndo();

            WatchesTheseLayers(shape, measured.BelongsTo, measured.CollidesWith);
        }

        /// <summary>Category00 DefaultCollider, what a solid placed object belongs to.</summary>
        private const uint SolidBelongsTo = 1u;

        /// <summary>
        /// Category00, 02 and 04 — the default, player and enemy collider layers.
        /// </summary>
        /// <remarks>
        /// The critter layer is NOT in here. <c>ChestEntity</c> and the doors do watch it;
        /// <c>CopperWorkBenchEntity</c>, <c>CartographyTableEntity</c>, <c>PlanterBoxEntity</c>,
        /// <c>CritterCatcherEntity</c>, <c>MinecartEntity</c> and <c>BoatEntity</c> do not, and a
        /// pass that gave every solid object the critter layer left critters stuck on modded
        /// workbenches and decorations they walk straight through in the game.
        /// </remarks>
        private const uint SolidCollidesWith = 1u | 4u | 16u;

        /// <summary>Category15 CritterCollider, which only a chest and a door add.</summary>
        private const uint CritterColliderLayer = 32768u;

        /// <summary>Category06 DefaultTrigger, what a vehicle, a crop and a torch belong to.</summary>
        private const uint WalkThroughBelongsTo = 64u;

        /// <summary>
        /// Category10 DefaultLowTriggerNonBlocking, where all 41 vanilla ground covers are.
        /// </summary>
        /// <remarks>
        /// NOT Category06. <c>PlaceObjectStateRequest</c> refuses a placement whose overlap hits
        /// anything on 131935, and Category06 is in that number while Category10 is not — so a rug
        /// on Category06 is a tile the player can then put nothing on. The player's melee cast
        /// includes Category06 and not Category10 as well, so such a rug is also hit by a sword.
        /// </remarks>
        private const uint GroundCoverBelongsTo = 1024u;

        /// <summary>Category12 ElectricalTrigger, which every vanilla machine carries.</summary>
        private const uint ElectricalTriggerLayer = 4096u;

        /// <summary>
        /// Category14 VelocityAffectorTrigger, the one layer that makes a push reach the player.
        /// </summary>
        /// <remarks>
        /// <c>PlayerControllerBurstableStatics</c> declares a single filter for this — collides
        /// with Category14, belongs to everything — and <c>PlayerVelocityCalculationSystem</c>
        /// overlaps with it to find what is shoving the player about. An object that pushes and is
        /// not on this layer is never found by that overlap, however good its push is.
        /// </remarks>
        private const uint VelocityAffectorLayer = 16384u;

        /// <summary>
        /// Category02, 04 and 15, which is what a door watches — Category00 deliberately left out.
        /// </summary>
        private const uint DoorCollidesWith = 4u | 16u | 32768u;

        /// <summary>Five tiles tall, off <c>WoodDoorEntity</c>.</summary>
        private const float DoorHeight = 5f;

        /// <summary>The thin side of a door slab.</summary>
        private const float DoorThickness = 0.3f;

        /// <summary>A crop and a seed are a ball 0.6 across, off <c>CarrockPlantEntity</c>.</summary>
        private const float CropRadius = 0.3f;

        /// <summary>
        /// 0.75 deep, off the 14 vanilla karts — a kart does not fill the tile it sits on.
        /// </summary>
        private const float KartDepth = 0.75f;

        /// <summary>
        /// 0.125 back from the tile centre, which every kart and both boats carry.
        /// </summary>
        private const float KartCentreDepth = 0.125f;

        /// <summary>
        /// A boat is one centimetre square. <c>BoatEntity</c> and <c>SpeederBoatEntity</c> both
        /// serialize <c>0.01</c>, and it is not a rounding artefact: a boat that filled its tile
        /// would refuse every placement on that tile and eat every melee swing across it.
        /// </summary>
        private const float BoatSide = 0.01f;

        /// <summary>The headboard slab, 0.2 deep off <c>BedEntity</c>.</summary>
        private const float BedHeadboardDepth = 0.2f;

        /// <summary>The headboard sits at <c>z = -0.3</c>, in front of the bed's own tile.</summary>
        private const float BedHeadboardCentre = -0.3f;

        /// <summary>The footboard slab, 0.7 deep off <c>BedEntity</c>.</summary>
        private const float BedFootboardDepth = 0.7f;

        /// <summary>
        /// The footboard sits 0.15 past the far tile: <c>BedEntity</c> is two tiles deep and puts
        /// it at <c>z = 1.15</c>.
        /// </summary>
        private const float BedFootboardCentre = 0.15f;

        /// <summary>Category04 EnemyCollider, what a creature's solid shape belongs to.</summary>
        private const uint CreatureBelongsTo = 16u;

        /// <summary>Category00, 04, 08 and 17, which is what a vanilla creature collides with.</summary>
        private const uint CreatureCollidesWith = 1u | 16u | 256u | 131072u;

        /// <summary>0.375, the commonest vanilla creature body radius.</summary>
        private const float CreatureBodyRadius = 0.375f;

        /// <summary>0.4 above the entity's own position, which is 55 of the 64 vanilla bodies.</summary>
        private const float CreatureBodyCentreHeight = 0.4f;

        /// <summary>0.15, off <c>CritterLarvaEntity</c> and 39 other critters.</summary>
        private const float CritterBodyRadius = 0.15f;

        /// <summary>Category15 CritterCollider.</summary>
        private const uint CritterBelongsTo = 32768u;

        /// <summary>Category00, 08 and 17, off <c>CritterLarvaEntity</c>.</summary>
        private const uint CritterCollidesWith = 1u | 256u | 131072u;

        /// <summary><c>ShapeType.Box</c>, and the enum runs from zero without gaps.</summary>
        private const int ShapeTypeBox = 0;

        /// <summary><c>ShapeType.Sphere</c>.</summary>
        private const int ShapeTypeSphere = 2;

        /// <summary><c>CollisionResponsePolicy.Collide</c>.</summary>
        private const int ItBlocks = 0;

        /// <summary><c>CollisionResponsePolicy.CollideRaiseCollisionEvents</c>.</summary>
        private const int ItCollidesAndSaysSo = 1;

        /// <summary><c>CollisionResponsePolicy.RaiseTriggerEvents</c>.</summary>
        private const int ItOnlyTellsSystemsItTouched = 3;

        /// <summary>
        /// <c>CollisionResponsePolicy.None</c>, which is 254 and not 4.
        /// </summary>
        /// <remarks>
        /// It is <c>byte.MaxValue - 1</c> in <c>Unity.Physics.Material</c>. Every vanilla crop,
        /// seed, rug, floor tile and torch serializes it; a freshly added shape does not, and the
        /// fresh default is <c>Collide</c>, so leaving the field alone is the difference between a
        /// crop a player walks over and a crop a player walks into.
        /// </remarks>
        private const int NothingBlocksAgainstIt = 254;

        /// <summary>
        /// The object's shape at that position, adding shapes until there is one there.
        /// </summary>
        /// <remarks>
        /// A creature needs two, both on the same object — <c>LarvaEntity</c> carries its body and
        /// its enemy trigger as two <c>PhysicsShapeAuthoring</c> components on one GameObject, and
        /// the baker compounds them. <c>GetComponent</c> only ever returns the first, so an earlier
        /// pass could only ever write one and the trigger half was never there.
        /// </remarks>
        private static Unity.Physics.Authoring.PhysicsShapeAuthoring ShapeOn(GameObject root, int index)
        {
            Unity.Physics.Authoring.PhysicsShapeAuthoring[] present =
                root.GetComponents<Unity.Physics.Authoring.PhysicsShapeAuthoring>();
            if (index < present.Length)
            {
                return present[index];
            }

            Unity.Physics.Authoring.PhysicsShapeAuthoring added = null;
            for (int i = present.Length; i <= index; i++)
            {
                added = root.AddComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();
            }

            return added;
        }

        /// <summary>
        /// Writes whether the shape blocks, reports, or does neither.
        /// </summary>
        /// <remarks>
        /// Through the serialized field rather than the component's own property, because the
        /// property's type is <c>CollisionResponsePolicy</c> from <c>Unity.Physics</c>, which these
        /// editor tools do not reference. An earlier pass recorded that as a reason the field could
        /// not be written at all; the serialized form is an ordinary enum two levels down and needs
        /// no reference. The override flag is raised beside it, the way assigning the property
        /// would.
        /// </remarks>
        private static void SetHowItAnswersATouch(
            UnityEditor.SerializedObject target,
            int value,
            Action<string> say)
        {
            UnityEditor.SerializedProperty property =
                target.FindProperty("m_Material.m_CollisionResponse.m_Value");
            if (property == null ||
                (property.propertyType != UnityEditor.SerializedPropertyType.Enum &&
                 property.propertyType != UnityEditor.SerializedPropertyType.Integer))
            {
                CouldNotWrite(target, "m_Material.m_CollisionResponse.m_Value", say);
                return;
            }

            property.intValue = value;

            UnityEditor.SerializedProperty overridden =
                target.FindProperty("m_Material.m_CollisionResponse.m_Override");
            if (overridden != null &&
                overridden.propertyType == UnityEditor.SerializedPropertyType.Boolean)
            {
                overridden.boolValue = true;
            }
        }

        /// <summary>
        /// Puts the shape on the layers it belongs to and the layers it answers.
        /// </summary>
        /// <remarks>
        /// Set through the component's own properties rather than <c>SerializedObject</c> because
        /// the two of them are the one part of the shape whose type — <c>PhysicsCategoryTags</c> —
        /// lives in an assembly these tools already reference, and the serialized form is
        /// thirty-two separate booleans. Assigning the property also raises the override flag,
        /// which is what makes the value win over a material template.
        /// </remarks>
        private static void WatchesTheseLayers(
            Unity.Physics.Authoring.PhysicsShapeAuthoring shape,
            uint belongsTo,
            uint collidesWith)
        {
            if (shape == null)
            {
                return;
            }

            shape.BelongsTo = new Unity.Physics.Authoring.PhysicsCategoryTags { Value = belongsTo };
            shape.CollidesWith =
                new Unity.Physics.Authoring.PhysicsCategoryTags { Value = collidesWith };
        }

        private static void SetEnum(
            UnityEditor.SerializedObject target,
            string path,
            int value,
            Action<string> say)
        {
            UnityEditor.SerializedProperty property = target.FindProperty(path);
            if (property == null)
            {
                CouldNotWrite(target, path, say);
                return;
            }

            property.enumValueIndex = value;
        }

        private static void SetFloat(
            UnityEditor.SerializedObject target,
            string path,
            float value,
            Action<string> say)
        {
            UnityEditor.SerializedProperty property = target.FindProperty(path);
            if (property == null || property.propertyType != UnityEditor.SerializedPropertyType.Float)
            {
                CouldNotWrite(target, path, say);
                return;
            }

            property.floatValue = value;
        }

        /// <summary>
        /// Writes a three-number field that may be a <c>Vector3</c> or a <c>float3</c>.
        /// </summary>
        /// <remarks>
        /// THIS IS WHY THE OLD SIZES NEVER LANDED. <c>PhysicsShapeAuthoring</c>'s size and centre
        /// are <c>Unity.Mathematics.float3</c>, which Unity serializes as an ordinary struct with
        /// three float children rather than as a <c>Vector3</c>. A write guarded on
        /// <c>SerializedPropertyType.Vector3</c> therefore did nothing at all, and the shape kept
        /// the field initialiser — one by one by one, centred on the object's own position. Both
        /// forms are handled here, and a field that is neither is reported instead of skipped.
        /// </remarks>
        private static void SetFloat3(
            UnityEditor.SerializedObject target,
            string path,
            Vector3 value,
            Action<string> say)
        {
            UnityEditor.SerializedProperty property = target.FindProperty(path);
            if (property == null)
            {
                CouldNotWrite(target, path, say);
                return;
            }

            if (property.propertyType == UnityEditor.SerializedPropertyType.Vector3)
            {
                property.vector3Value = value;
                return;
            }

            UnityEditor.SerializedProperty x = target.FindProperty(path + ".x");
            UnityEditor.SerializedProperty y = target.FindProperty(path + ".y");
            UnityEditor.SerializedProperty z = target.FindProperty(path + ".z");
            if (x == null || y == null || z == null)
            {
                CouldNotWrite(target, path, say);
                return;
            }

            x.floatValue = value.x;
            y.floatValue = value.y;
            z.floatValue = value.z;
        }

        /// <summary>
        /// Says that a number could not be written, rather than skipping it in silence.
        /// </summary>
        /// <remarks>
        /// A shape whose size never landed looks exactly like a shape that was never asked for, and
        /// that is how the wrong hitbox survived a whole pass. Reaching a field by its serialized
        /// name is a guess about a package the framework does not own, so when the guess is wrong
        /// the author hears about it.
        /// </remarks>
        private static void CouldNotWrite(
            UnityEditor.SerializedObject target,
            string path,
            Action<string> say)
        {
            if (say == null)
            {
                return;
            }

            string owner = target != null && target.targetObject != null
                ? target.targetObject.GetType().Name
                : "a physics component";

            say("could not set '" + path + "' on " + owner + ", so its body is not the shape or " +
                "size the game's own objects use. The physics package this reaches into has " +
                "changed its field names. Nothing else about the object is affected.");
        }

        /// <summary>
        /// The authoring answers whose systems all name <c>AnimationOrientationCD</c>.
        /// </summary>
        private static readonly string[] AnswersThatOnlyWorkOnSomethingThatCanTurn =
        {
            "ChaseStateAuthoring",
            "MeleeAttackStateAuthoring",
            "RangeAttackStateAuthoring",
            "ChargeAttackStateAuthoring",
            "JumpAttackStateAuthoring",
            "RandomWalkStateAuthoring",
            "RoamingStateAuthoring",
            "RandomFollowStateAuthoring",
            "FollowPheromoneStateAuthoring",
            "PetWalkStateAuthoring",
            "MoveToPositionFromCommandStateAuthoring",
            "EatStateAuthoring",
            "CombatEmoteStateAuthoring",
            "IdleWhenNearbyPlayerStateAuthoring",
            "DamageObjectStateAuthoring",

            // Added by the 2026-08-29 combat census, which read both systems and found the facing
            // component named for writing in each. A healer set not to turn never heals, and a
            // beam set not to turn never fires — silently, both of them.
            "HealOtherEntityStateAuthoring",
            "BeamAttackStateAuthoring"
        };

        /// <summary>
        /// Lets something turn when it is carrying an answer that only works on something that
        /// turns.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>AnimationConverter</c> only produces <c>AnimationOrientationCD</c> when the object's
        /// facing is something other than "does not turn", and the answers listed above all name
        /// that component in their work query — the chase, every attack, wandering, roaming,
        /// following, eating, emoting, smashing, healing and the beam. A creature left at the
        /// scenery default therefore does not chase, does not attack and does not wander, and
        /// nothing says why. The list is the record: it is checked against the censuses rather
        /// than against a remembered count, because a count in a sentence goes stale on the first
        /// system that is added to it.
        /// </para>
        /// <para>
        /// "Does not turn" is a real answer for a turret or a plant, so it is not overruled on
        /// something that only stands there. It is overruled only when the object is also carrying
        /// an answer that cannot work without turning — and the author is told, by name, that it
        /// happened and how to choose differently.
        /// </para>
        /// </remarks>
        public static void TurnsIfSomethingOnItNeedsTo(
            GameObject root,
            string displayName,
            Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            AnimationAuthoring animation = root.GetComponent<AnimationAuthoring>();
            if (animation == null ||
                animation.orientationSupport != AnimationAuthoring.OrientationSupport.None)
            {
                return;
            }

            string needsIt = null;
            for (int i = 0; i < AnswersThatOnlyWorkOnSomethingThatCanTurn.Length; i++)
            {
                if (HasNamed(root, AnswersThatOnlyWorkOnSomethingThatCanTurn[i]))
                {
                    needsIt = AnswersThatOnlyWorkOnSomethingThatCanTurn[i];
                    break;
                }
            }

            if (needsIt == null)
            {
                return;
            }

            // 3 is Horizontal and Vertical together, which is what LarvaEntity, CavelingEntity and
            // CowEntity all serialize, and what the framework calls "all four ways".
            animation.orientationSupport = (AnimationAuthoring.OrientationSupport)3;

            if (say != null)
            {
                say("'" + displayName + "' was set not to turn, but it also moves or fights, and " +
                    "the game skips anything that cannot turn when it works out chasing, " +
                    "attacking and wandering. It was generated turning all four ways, which is " +
                    "what the game's own creatures do. Set 'Which ways its art can face' " +
                    "deliberately if you want something else.");
            }
        }

        /// <summary>
        /// What one authoring answer needs beside it, and what the author is told when we cannot
        /// supply it.
        /// </summary>
        private sealed class Companion
        {
            public Companion(
                Type present,
                string alsoNeeds,
                string vanillaPrefab,
                string readBy,
                Func<GameObject, bool> fillIn,
                string sayInstead)
                : this(present, alsoNeeds, vanillaPrefab, readBy, fillIn, null, sayInstead)
            {
            }

            /// <summary>
            /// The same, for a row whose fill only applies to some objects.
            /// </summary>
            public Companion(
                Type present,
                string alsoNeeds,
                string vanillaPrefab,
                string readBy,
                Func<GameObject, bool> fillIn,
                Func<GameObject, bool> onlyWhen,
                string sayInstead)
            {
                Present = present;
                AlsoNeeds = alsoNeeds;
                VanillaPrefab = vanillaPrefab;
                ReadBy = readBy;
                FillIn = fillIn;
                OnlyWhen = onlyWhen;
                SayInstead = sayInstead;
            }

            /// <summary>The authoring component the framework writes.</summary>
            public Type Present { get; private set; }

            /// <summary>The sibling authoring component the same system's query also demands.</summary>
            public string AlsoNeeds { get; private set; }

            /// <summary>Core Keeper's own object doing that job, which carries both.</summary>
            public string VanillaPrefab { get; private set; }

            /// <summary>The system whose query is the reason.</summary>
            public string ReadBy { get; private set; }

            /// <summary>
            /// Supplies the sibling. Null when only the author can answer it; otherwise returns
            /// false when it looked and decided it could not, in which case
            /// <see cref="SayInstead"/> is said.
            /// </summary>
            public Func<GameObject, bool> FillIn { get; private set; }

            /// <summary>
            /// Whether <see cref="FillIn"/> applies to this object at all. Null means always.
            /// </summary>
            /// <remarks>
            /// <para>
            /// IT IS WHAT MAKES "the row said it fills this in" A CLAIM THAT CAN BE CHECKED. The
            /// guard reads the object after the sweep and treats a gap left by a row that fills as
            /// broken. Written from <c>FillIn != null</c> alone that was wrong by construction: a
            /// door with nothing to use on it cannot be given a use trigger — the game's own
            /// converter reaches into the picture by index and throws — so its row declines and
            /// speaks, exactly as designed, and the guard called that a break.
            /// </para>
            /// <para>
            /// So the precondition is declared here, beside the fill, and it must be a pure read:
            /// the guard asks it while listing, and the listing is the half of the guard that is
            /// not allowed to change the object. A row whose fill can decline for any other reason
            /// does not belong in the fill column at all — its answer is a sentence.
            /// </para>
            /// </remarks>
            public Func<GameObject, bool> OnlyWhen { get; private set; }

            /// <summary>
            /// What the author is told when <see cref="FillIn"/> is null, or when
            /// <see cref="OnlyWhen"/> says this object is not one it can fill. Plain words, no
            /// component names: it has to be actionable by someone who has never opened Unity.
            /// </summary>
            public string SayInstead { get; private set; }
        }

        private static Companion[] rows;

        /// <summary>
        /// Every companion this framework knows about. Read by <see cref="CloseTheGaps"/> and by the
        /// guard test.
        /// </summary>
        private static Companion[] Rows
        {
            get
            {
                if (rows == null)
                {
                    rows = BuildRows();
                }

                return rows;
            }
        }

        /// <summary>
        /// Adds every component the game's own systems need beside what this generate already wrote,
        /// and says plainly where a gap can only be closed by the author.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call this LAST, after every pass has written what it owns. The rows only ever add, so
        /// they cannot take back an answer an earlier pass made; and they only act on objects that
        /// already carry the feature, so an object without the feature is untouched.
        /// </para>
        /// <para>
        /// IT DOES NOT SAY THE OBJECT'S LOOK IS DYNAMIC. That is
        /// <see cref="ItsLookIsNotPartOfItsIdentity"/>, and it belongs to the three Finish helpers
        /// rather than here, because the two generators that reach this method without going
        /// through one of them are exactly the two it would be wrong for: the item generator, where
        /// the author answers the question themselves, and the plant generator, which registers
        /// several looks of one crop and needs the game to tell them apart.
        /// </para>
        /// </remarks>
        public static void CloseTheGaps(GameObject root, string displayName, Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            Companion[] all = Rows;
            bool[] alreadySaid = new bool[all.Length];

            // RUN UNTIL NOTHING NEW APPEARS, not once through the rows. A component this sweep
            // supplies can itself have a row — "the thing we added needs another thing" is the same
            // bug class one level down — and a single pass in array order would only ever reach
            // that when the second row happened to sit later in the array. The stop condition is
            // the object itself: when a pass adds no component, there is nothing left to cascade
            // from. The bound is there so a pair of rows that each ask for the other cannot spin.
            for (int pass = 0; pass <= all.Length; pass++)
            {
                int before = root.GetComponents<Component>().Length;

                for (int i = 0; i < all.Length; i++)
                {
                    Companion row = all[i];
                    if (root.GetComponent(row.Present) == null)
                    {
                        continue;
                    }

                    if (HasNamed(root, row.AlsoNeeds))
                    {
                        continue;
                    }

                    // A ROW WITH NOTHING BUT WORDS STILL HAS TO LOOK BEFORE IT SPEAKS. Three rows
                    // used to hand a pure read in as their "fill" — is this blast big enough, does
                    // this creature belong to somebody — which made them claim to supply something
                    // they never supplied. The read belongs here, where it always was for the rows
                    // that do fill, so a blast that really does reach and a minion that really does
                    // have an owner stay silent exactly as before.
                    if (row.FillIn == null && TheGapIsReallyClosed(root, row))
                    {
                        continue;
                    }

                    if (row.FillIn != null && row.FillIn(root) && TheGapIsReallyClosed(root, row))
                    {
                        continue;
                    }

                    // Once per row, however many passes it takes. Saying the same sentence four
                    // times over would read as four separate problems.
                    if (!alreadySaid[i] && say != null && !string.IsNullOrEmpty(row.SayInstead))
                    {
                        alreadySaid[i] = true;
                        say("'" + displayName + "' " + row.SayInstead);
                    }
                }

                if (root.GetComponents<Component>().Length == before)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// The last thing done to a generated creature, critter, boss or animal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three things every one of Core Keeper's own creature prefabs carries and none of ours
        /// did: it is sent to players, it has a body, and it can turn. Without the first the client
        /// never even builds it and the chase skips it; without the second nothing can hit it, it
        /// cannot be noticed by anything else's overlap, and every movement state in the game skips
        /// it; without the third the chase, the attacks and the wander all skip it.
        /// </para>
        /// <para>
        /// It runs LAST because the facing question can only be answered once every other pass has
        /// finished writing what the creature does.
        /// </para>
        /// </remarks>
        public static void FinishACreature(GameObject root, string displayName, Action<string> say)
        {
            FinishACreature(root, displayName, 1f, 1f, say);
        }

        /// <summary>
        /// The same, sized from the creature rather than from a constant.
        /// </summary>
        /// <remarks>
        /// The generator wrote the body earlier in its run, before the chase and the wander read
        /// it, and this restates it at the end the way every other value here is restated. Both
        /// calls have to agree on the size or the second would undo the first, which is why the
        /// numbers come in here as well.
        /// </remarks>
        public static void FinishACreature(
            GameObject root,
            string displayName,
            float bodyWidthInTiles,
            float widthOthersNoticeItAt,
            Action<string> say)
        {
            IsSentToPlayers(root);
            ItsLookIsNotPartOfItsIdentity(root);
            HasABodyThingsCanTouch(
                root,
                bodyWidthInTiles,
                1f,
                widthOthersNoticeItAt,
                Prefixed(displayName, say));
            TurnsIfSomethingOnItNeedsTo(root, displayName, say);
            CloseTheGaps(root, displayName, say);
        }

        /// <summary>
        /// The last thing done to a generated critter.
        /// </summary>
        /// <remarks>
        /// The same three things a creature gets, with the critter's own body: Core Keeper's
        /// critters are one small sphere on the critter layer rather than a creature-sized sphere
        /// on the enemy layer, and the layer is what keeps a moth from being treated as a monster
        /// by everything that watches for one.
        /// </remarks>
        /// <summary>
        /// Carries what the object IS onto the running object, not only into the object table.
        /// </summary>
        /// <remarks>
        /// The one system this matters most for is the one that applies burning ground, acid,
        /// mould, oil and slippery slime, and it does not read a wrong default — it names the
        /// type component in its query and never sees an object without one. Every object Core
        /// Keeper builds has it; nothing this framework built outside the creature and item paths
        /// did, so a mod chest, plant, workbench, vehicle or critter could stand in lava and be
        /// touched by none of it. The component reads the type off the object's own identity, so
        /// there is nothing to set.
        /// </remarks>
        public static void CarriesWhatItIsOntoTheRunningObject(GameObject root)
        {
            if (root == null || root.GetComponent<ObjectAuthoring>() == null)
            {
                return;
            }

            Ensure<ExpandNullforge.Authoring.DimensionObjectTypeAuthoring>(root);
        }

        public static void FinishACritter(GameObject root, string displayName, Action<string> say)
        {
            CarriesWhatItIsOntoTheRunningObject(root);
            IsSentToPlayers(root);
            ItsLookIsNotPartOfItsIdentity(root);
            HasTheBodyACritterHas(root, Prefixed(displayName, say));
            TurnsIfSomethingOnItNeedsTo(root, displayName, say);
            CloseTheGaps(root, displayName, say);
        }

        /// <summary>
        /// The last thing done to a generated object that is placed in the world.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It gives the object a body, and that is new. Nothing the world-object, container,
        /// workbench or vehicle generators built had one, which meant nothing in the game could
        /// hit it, mine it, dig it, walk into it or find it with any of the casts every attack and
        /// every explosion runs — while the answers that describe being hit were all written and
        /// all silent. Core Keeper's own objects carry one on essentially everything that is
        /// placed. See <see cref="HasTheBodyAPlacedThingHas"/> for the shape and the layers.
        /// </para>
        /// <para>
        /// Everything it needs is already answered elsewhere on the object rather than asked
        /// again: the footprint and its corner offset are the ones the author gave for placement,
        /// and what SORT of thing it is — see <see cref="WhatSortOfPlacedThingItIs"/> — is read off
        /// the ground-cover tick, the world object's kind, the riding answer, the crafting answer
        /// and the automation answers. So there is no new control, and none of those controls can
        /// be contradicted by this pass.
        /// </para>
        /// </remarks>
        public static void FinishAWorldObject(GameObject root, string displayName, Action<string> say)
        {
            FinishAWorldObject(
                root,
                displayName,
                default(WhatTheAuthorSaidAboutItsBody),
                say);
        }

        /// <summary>
        /// The same, for a generator whose author answered which way the doorway runs or whether a
        /// player walks through it.
        /// </summary>
        public static void FinishAWorldObject(
            GameObject root,
            string displayName,
            WhatTheAuthorSaidAboutItsBody said,
            Action<string> say)
        {
            CarriesWhatItIsOntoTheRunningObject(root);

            // The same thing a creature and a critter already got here, and the largest remaining
            // difference between a generated placed object and Core Keeper's own: 2,074 of the
            // game's 4,180 entity prefabs carry it, ChestEntity, CopperWorkBenchEntity,
            // WoodDoorEntity, TorchEntity, EerieRugEntity and MinecartEntity among them, and
            // GhostPostConverter returns immediately without it so the entity never gets
            // GhostInstance at all.
            IsSentToPlayers(root);
            ItsLookIsNotPartOfItsIdentity(root);

            GiveItTheBodyItsFootprintAsksFor(
                root,
                displayName,
                PlacedThingKind.Decoration,
                said,
                say);
            TurnsIfSomethingOnItNeedsTo(root, displayName, say);
            CloseTheGaps(root, displayName, say);
        }

        /// <summary>
        /// Sizes a placed object's body from the footprint and the sort of thing it already is.
        /// </summary>
        public static void GiveItTheBodyItsFootprintAsksFor(
            GameObject root,
            string displayName,
            Action<string> say)
        {
            GiveItTheBodyItsFootprintAsksFor(root, displayName, PlacedThingKind.Decoration, say);
        }

        /// <summary>
        /// The same, for a generator that already knows what it is building.
        /// </summary>
        /// <remarks>
        /// The plant generator builds one prefab for a crop and one for its seed through the same
        /// method, so it hands in which of the two this one is rather than leaving the answer to be
        /// guessed off components a later pass might rename.
        /// </remarks>
        public static void GiveItTheBodyItsFootprintAsksFor(
            GameObject root,
            string displayName,
            PlacedThingKind whenNothingElseSaysSo,
            Action<string> say)
        {
            GiveItTheBodyItsFootprintAsksFor(
                root,
                displayName,
                whenNothingElseSaysSo,
                default(WhatTheAuthorSaidAboutItsBody),
                say);
        }

        /// <summary>
        /// The same, carrying the two answers only the author can give.
        /// </summary>
        public static void GiveItTheBodyItsFootprintAsksFor(
            GameObject root,
            string displayName,
            PlacedThingKind whenNothingElseSaysSo,
            WhatTheAuthorSaidAboutItsBody said,
            Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            int wide = 1;
            int tall = 1;
            Vector2 cornerOffset = Vector2.zero;

            PlaceableObjectAuthoring placeable = root.GetComponent<PlaceableObjectAuthoring>();
            if (placeable != null)
            {
                if (placeable.prefabTileSize.x > 0)
                {
                    wide = placeable.prefabTileSize.x;
                }

                if (placeable.prefabTileSize.y > 0)
                {
                    tall = placeable.prefabTileSize.y;
                }

                cornerOffset = new Vector2(
                    placeable.prefabCornerOffset.x,
                    placeable.prefabCornerOffset.y);
            }

            HasTheBodyAPlacedThingHas(
                root,
                wide,
                tall,
                WhatSortOfPlacedThingItIs(root, whenNothingElseSaysSo, said),
                cornerOffset,
                Prefixed(displayName, say));
        }

        /// <summary>
        /// Reads what sort of placed thing this is off what the author already answered.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY BRANCH IS AN EXISTING CONTROL. Ground cover is the "it is ground cover" tick; a
        /// door, a bed and a trophy are the world object's own kind; a vehicle is the riding
        /// answer; a workbench is "it crafts"; a chest is "it holds items"; a machine is one of the
        /// automation answers; a crop and a seed are which of the two the plant generator is
        /// building. Nothing here asks the author anything new, and nothing here can overrule
        /// something they set.
        /// </para>
        /// <para>
        /// The order matters where an object is two things at once. A boss statue holds an item and
        /// is not a chest; a workbench that holds items is a workbench. Ground cover wins outright,
        /// because "a player walks over it" is the strongest thing anybody said about it.
        /// </para>
        /// </remarks>
        public static PlacedThingKind WhatSortOfPlacedThingItIs(
            GameObject root,
            PlacedThingKind whenNothingElseSaysSo)
        {
            return WhatSortOfPlacedThingItIs(
                root,
                whenNothingElseSaysSo,
                default(WhatTheAuthorSaidAboutItsBody));
        }

        /// <summary>
        /// The two answers about a body that nothing already on the object can supply.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERYTHING ELSE IN <see cref="WhatSortOfPlacedThingItIs"/> IS READ OFF THE OBJECT, and
        /// these two cannot be. Every vanilla door prefab is one tile by one tile whichever way it
        /// faces, so the footprint says nothing about which way the doorway runs; and a torch and a
        /// statue carry exactly the same components, so nothing on the object says whether a player
        /// walks through it. Both are questions only the author can answer, both were being
        /// answered wrongly by a guess, and both now come from a control with those words on it.
        /// </para>
        /// <para>
        /// A generator that has neither question — a container, a crafting station, a vehicle, a
        /// crop — hands in nothing and gets what it got before.
        /// </para>
        /// </remarks>
        public struct WhatTheAuthorSaidAboutItsBody
        {
            /// <summary>
            /// The doorway runs along the wall — north to south — rather than across it.
            /// </summary>
            public bool ADoorwayThatRunsAlongTheWall;

            /// <summary>
            /// A player and a creature walk straight through it, the way they walk through a torch
            /// or a candle, while it still blocks building on its tile and still takes a hit.
            /// </summary>
            public bool PlayersWalkThroughIt;
        }

        /// <summary>
        /// The same, with the two answers only the author can give.
        /// </summary>
        public static PlacedThingKind WhatSortOfPlacedThingItIs(
            GameObject root,
            PlacedThingKind whenNothingElseSaysSo,
            WhatTheAuthorSaidAboutItsBody said)
        {
            if (root == null)
            {
                return whenNothingElseSaysSo;
            }

            if (HasNamed(root, "GroundDecorationAuthoring"))
            {
                return ItIsWired(root)
                    ? PlacedThingKind.PoweredGroundCover
                    : PlacedThingKind.GroundCover;
            }

            if (HasNamed(root, "SeedAuthoring") ||
                HasNamed(root, "DimensionSeedAuthoring") ||
                HasNamed(root, "AutomatedPlantableSeedAuthoring"))
            {
                return PlacedThingKind.Seed;
            }

            if (HasNamed(root, "PlantAuthoring") ||
                HasNamed(root, "RootPlantAuthoring") ||
                HasNamed(root, "GrowingPlantAuthoring") ||
                HasNamed(root, "AutomatedHarvestablePlantAuthoring") ||
                HasNamed(root, "DimensionCropTierPlantAuthoring"))
            {
                return PlacedThingKind.Plant;
            }

            // THE THREE VEHICLES ARE THREE SHAPES, and the framework already writes exactly one of
            // these three components per vehicle from the kind the author picked, so the answer is
            // read off that rather than asked again. BoatAuthoring is on both vanilla boats,
            // MinecartAuthoring on both minecarts and VehicleAuthoring on all 14 karts.
            if (HasNamed(root, "BoatAuthoring"))
            {
                return PlacedThingKind.Boat;
            }

            if (HasNamed(root, "MinecartAuthoring"))
            {
                return PlacedThingKind.Minecart;
            }

            if (HasNamed(root, "VehicleAuthoring"))
            {
                return PlacedThingKind.Vehicle;
            }

            if (HasNamed(root, "DoorAuthoring") || HasNamed(root, "FenceGateAuthoring"))
            {
                return said.ADoorwayThatRunsAlongTheWall
                    ? PlacedThingKind.DoorAlongTheWall
                    : PlacedThingKind.Door;
            }

            if (HasNamed(root, "BedAuthoring"))
            {
                return PlacedThingKind.Bed;
            }

            if (HasNamed(root, "TrophyAuthoring"))
            {
                return PlacedThingKind.Trophy;
            }

            if (ItIsWired(root))
            {
                // The two machine shapes are read off which automation answer is already on the
                // object: an arm, a collector or a circuit watches 20 and a drill, a sprinkler or
                // a lamp also watches the critter layer. No new question.
                return ItMovesItemsAround(root)
                    ? PlacedThingKind.MachineThatMovesItems
                    : PlacedThingKind.Machine;
            }

            if (HasNamed(root, "CraftingAuthoring"))
            {
                return PlacedThingKind.Workbench;
            }

            if (HasNamed(root, "InventoryAuthoring"))
            {
                return PlacedThingKind.Chest;
            }

            // LAST, so that nothing that is a door, a chest, a crafting station or a machine can
            // be turned into a prop by ticking it. It only ever decides between the two answers an
            // ordinary prop can have.
            if (said.PlayersWalkThroughIt &&
                (whenNothingElseSaysSo == PlacedThingKind.Decoration ||
                 whenNothingElseSaysSo == PlacedThingKind.WalkThroughProp))
            {
                return PlacedThingKind.WalkThroughProp;
            }

            return whenNothingElseSaysSo;
        }

        /// <summary>
        /// The automation answers every vanilla machine carries one of.
        /// </summary>
        /// <remarks>
        /// Read off the seventeen prefabs the census counted as machines —
        /// <c>CrudeDrillForwardEntity</c>, <c>RobotArmForwardEntity</c>, <c>ItemCollectorEntity</c>,
        /// <c>SprinklerEntity</c> — rather than guessed from the names.
        /// </remarks>
        /// <summary>Whether the object carries one of the automation answers.</summary>
        private static bool ItIsWired(GameObject root)
        {
            for (int i = 0; i < TheAnswersThatMakeSomethingAMachine.Length; i++)
            {
                if (HasNamed(root, TheAnswersThatMakeSomethingAMachine[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly string[] TheAnswersThatMakeSomethingAMachine =
        {
            "AutomatedMoverAuthoring",
            "AutomatedMoverSharedAuthoring",
            "AutomatedCrafterAuthoring",
            "AutomatedMinerAuthoring",
            "AutomatedStorageAuthoring",
            "AutomatedHarvestAndMoverAuthoring",
            "AutomatedMoveAndPlanterAuthoring",
            "DrillAuthoring",
            "SprinklerAuthoring",
        };

        /// <summary>Whether the machine's job is carrying items from one place to another.</summary>
        /// <remarks>
        /// The 36 vanilla shapes on Category00 plus Category12 split in two by exactly this, and
        /// the split is not a guess about the names: ItemCollectorEntity, the four RobotArm
        /// prefabs, the four RobotFarmArm prefabs and the four PulseCircuit prefabs watch 20, and
        /// every drill, SprinklerEntity, LampEntity, SirenLampEntity and ChineseLanternEntity also
        /// watch Category15.
        /// </remarks>
        private static bool ItMovesItemsAround(GameObject root)
        {
            for (int i = 0; i < TheAnswersThatCarryItemsAbout.Length; i++)
            {
                if (HasNamed(root, TheAnswersThatCarryItemsAbout[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly string[] TheAnswersThatCarryItemsAbout =
        {
            "AutomatedMoverAuthoring",
            "AutomatedMoverSharedAuthoring",
            "AutomatedStorageAuthoring",
            "AutomatedHarvestAndMoverAuthoring",
            "AutomatedMoveAndPlanterAuthoring",
        };

        /// <summary>Names the object in a sentence a deeper pass wrote about it.</summary>
        private static Action<string> Prefixed(string displayName, Action<string> say)
        {
            if (say == null)
            {
                return null;
            }

            return delegate(string message) { say("'" + displayName + "' " + message); };
        }

        /// <summary>
        /// What <see cref="WhatIsStillMissingWithoutTouchingIt"/> appends to a gap the author is
        /// never told about. The mutating lister this used to name is gone: it had no callers left
        /// once the guard stopped auditing itself, and a dead method with a doc saying the guard
        /// uses it is worse than no method.
        /// </summary>
        public const string SaysNothingAboutIt = " | says nothing";

        /// <summary>
        /// What <see cref="WhatIsStillMissingWithoutTouchingIt"/> puts before a gap's own words.
        /// </summary>
        /// <remarks>
        /// Written down so the guard can take the words back out and look for them in what the
        /// sweep really said. The words in a listing come off the row; the words an author reads
        /// come out of <see cref="CloseTheGaps"/>, and until the guard compared the two, deleting
        /// the sweep's whole reporting block left every test in the file green.
        /// </remarks>
        public const string SaysThis = " | says: ";

        /// <summary>
        /// What <see cref="WhatIsStillMissingWithoutTouchingIt"/> appends to a gap whose row says
        /// it supplies the missing thing itself ON THIS OBJECT.
        /// </summary>
        /// <remarks>
        /// The last three words are the whole of it. Four rows fill when they can and speak when
        /// they cannot — a door with nothing to use on it, a touch attack on a creature nobody
        /// summoned — and stamping this from "the row has a fill" alone put the marker on a gap
        /// those rows never claimed. <c>Companion.OnlyWhen</c> is where a row writes down which
        /// objects its fill applies to, and this is stamped only when that says yes.
        /// </remarks>
        public const string TheRowSaysItFillsThisIn = " | the row says it fills this in";

        /// <summary>
        /// Lists what is still missing, WITHOUT changing the object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS EXISTS BECAUSE THE GUARD WAS AUDITING ITSELF. The lister it replaced ran
        /// the same row logic as <see cref="CloseTheGaps"/> and performed every fill while it
        /// looked,
        /// so the one test named for proving the companions work could not fail when they stopped
        /// working: empty the body of <c>CloseTheGaps</c> and the list came back identical, because
        /// the listing had done the filling. This one only reads. It is what the test observes
        /// after the production sweep has run, so what it reports is what the sweep left behind.
        /// </para>
        /// <para>
        /// It also marks the rows that CLAIM to fill their own gap. Four rows carry both a fill and
        /// a sentence, and a caller that excused any spoken-about gap excused those four as well —
        /// gut one of their fills and the entry simply changed from "says nothing" to "says", which
        /// the guard skipped. A row that says it fills something has to have filled it; only a row
        /// with nothing but words is allowed to leave the gap open.
        /// </para>
        /// </remarks>
        public static List<string> WhatIsStillMissingWithoutTouchingIt(GameObject root)
        {
            List<string> missing = new List<string>();
            if (root == null)
            {
                return missing;
            }

            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                Companion row = all[i];
                if (root.GetComponent(row.Present) == null)
                {
                    continue;
                }

                if (TheGapIsReallyClosed(root, row))
                {
                    continue;
                }

                // THE MARKER IS THE ROW'S CLAIM ABOUT THIS OBJECT, not about itself. Stamped from
                // FillIn != null alone it was a claim four rows never made: a door with nothing to
                // use on it, a tile-laying answer on something that is not a blast, a touch attack
                // on a creature nobody summoned. Those rows decline BY DESIGN and say so in words,
                // and a caller that reads the marker as "this had to be filled" reports a break
                // that is the row working. OnlyWhen is where a row writes down which objects its
                // fill applies to, and it is a pure read, so this stays a listing.
                bool theRowMeantToFillThisOne =
                    row.FillIn != null && (row.OnlyWhen == null || row.OnlyWhen(root));

                missing.Add(
                    row.Present.Name + " needs " + row.AlsoNeeds + " (" + row.ReadBy + ")" +
                    (theRowMeantToFillThisOne ? TheRowSaysItFillsThisIn : string.Empty) +
                    (string.IsNullOrEmpty(row.SayInstead)
                        ? SaysNothingAboutIt
                        : SaysThis + row.SayInstead));
            }

            return missing;
        }

        /// <summary>
        /// A gap a row said nothing can carry: a tracker that is present and sees nothing.
        /// </summary>
        public const string ATrackerThatCanActuallySeeSomething =
            "ATrackerThatCanActuallySeeSomething";

        /// <summary>A blast that is present and reaches nowhere.</summary>
        public const string ABlastWithARealRadius = "ABlastWithARealRadius";

        /// <summary>Every name a row may put in its "also needs" that is not a component.</summary>
        /// <remarks>
        /// Written down so the guard can insist every other "also needs" is a real type this build
        /// has. A typo in a component name would otherwise read as a gap nothing can ever close and
        /// would be reported forever.
        /// </remarks>
        public static readonly string[] GapsThatAreAValueRatherThanAComponent =
        {
            ATrackerThatCanActuallySeeSomething,
            ABlastWithARealRadius,

            // "BeamBufferHasNoProducerInTheGame" used to be here, and it was not true. The beam
            // system fills its own buffer; the gap was one missing line in Core Keeper's converter,
            // and the framework now supplies it with a converter of its own. See the beam rows.
            "NothingInTheGameReadsThis",
        };

        /// <summary>
        /// Whether the fill actually left the object with what the row said it needed.
        /// </summary>
        /// <remarks>
        /// <c>Fill&lt;T&gt;</c>, <c>NoticesThingsNearby</c>, <c>NoticesAPlayerStandingOnIt</c>,
        /// <c>NoticesWhatAPlayerIsHoldingFromAcrossTheRoom</c> and <c>NoticesWhatItCanHurt</c> all
        /// return true unconditionally, so believing the return value meant the "does close it"
        /// half of the guard could not fail for any row that fills — which is every row that claims
        /// to close anything. This looks at the object instead.
        /// </remarks>
        private static bool TheGapIsReallyClosed(GameObject root, Companion row)
        {
            if (root == null || row == null)
            {
                return false;
            }

            if (row.AlsoNeeds == ATrackerThatCanActuallySeeSomething)
            {
                return HowFarItSees(root) > 0f && WhatItWatches(root) != 0u;
            }

            if (row.AlsoNeeds == ABlastWithARealRadius)
            {
                return ItIsABlastThatActuallyReaches(root);
            }

            // The two rows that ask for a pet accept a minion or a minion's data as well, because
            // the system behind them runs on anything somebody summoned.
            if (row.AlsoNeeds == "PetAuthoring")
            {
                return ItBelongsToSomebody(root);
            }

            return HasNamed(root, row.AlsoNeeds);
        }

        /// <summary>What every row says it also needs, so the guard can check the names.</summary>
        public static List<string> EveryThingARowSaysIsAlsoNeeded()
        {
            List<string> needed = new List<string>();
            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                if (!needed.Contains(all[i].AlsoNeeds))
                {
                    needed.Add(all[i].AlsoNeeds);
                }
            }

            return needed;
        }

        /// <summary>The authoring components this framework writes that have a row here.</summary>
        public static List<string> CoveredAuthoringComponents()
        {
            List<string> names = new List<string>();
            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                if (!names.Contains(all[i].Present.Name))
                {
                    names.Add(all[i].Present.Name);
                }
            }

            return names;
        }

        /// <summary>Rows whose gap only the author can close, with the sentence they get.</summary>
        public static List<string> RowsThatOnlyTheAuthorCanClose()
        {
            List<string> said = new List<string>();
            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].FillIn == null)
                {
                    said.Add(all[i].Present.Name + ": " + all[i].SayInstead);
                }
            }

            return said;
        }

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
                    // NO FILL. This row used to hand ItIsABlastThatActuallyReaches in as its fill,
                    // and that method adds nothing — it is the same read the gap check makes, so
                    // the row was claiming to supply something it never supplied. A row that only
                    // looks answers with a sentence; the look itself is TheGapIsReallyClosed's job,
                    // and CloseTheGaps asks it before saying anything, so a blast that really does
                    // reach is still silent.
                    null,
                    "is set to lay ground where it explodes, but only a blast can do that and this " +
                    "is an object rather than a blast. Put the answer on the blast the object sets " +
                    "off instead."),
                // ---- the beam ---------------------------------------------------------------
                // THIS ROW USED TO REFUSE THE BEAM, and the refusal was not true. It said Core
                // Keeper's beam system needed a list of beams nothing in the game ever creates.
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
