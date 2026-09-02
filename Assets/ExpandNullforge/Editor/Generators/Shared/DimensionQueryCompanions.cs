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
    /// that carries EVERY component named in its query. Writing the one component that obviously
    /// belongs to a feature and none of the others is the easy mistake, because the others come
    /// from unrelated authoring components. The entity then never matches the query, the
    /// system never runs on it, and nothing anywhere reports a problem: the component that was
    /// written is present, its converter ran, the number in it is right. The author generates
    /// cleanly and the feature does nothing.
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
    internal static partial class DimensionQueryCompanions
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
        /// entity that no longer has the component. So a locked chest becomes unhittable the moment
        /// the key goes in, and a prop that changes look after a while is walked through for ever
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
    }
}
