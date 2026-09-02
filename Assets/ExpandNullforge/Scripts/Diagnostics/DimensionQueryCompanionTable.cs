using System;
using Pug.Properties;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace ExpandNullforge.Diagnostics
{
    /// <summary>
    /// What has to sit beside what on a finished object, so that the game actually looks at it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE BUG CLASS. A Core Keeper system does its work over an <c>EntityQuery</c> naming several
    /// components, and it touches only entities that carry every one of them. The framework writes
    /// the component the feature is named after, the others come from unrelated authoring
    /// components, the entity never matches, the system never runs on it, and nothing anywhere
    /// reports a problem: the component we wrote is present, its converter ran, and the number in
    /// it is right. The exemplar is <c>BossSummoningSystem</c>, whose only work query is
    /// <c>NearbyEntitiesBufferCD</c> + <c>AnimationBuffer</c> + <c>AnimationBufferPointer</c> +
    /// <c>SummonAreaCD</c>; <c>SummonAreaAuthoring</c> supplies the last one, so both routes that
    /// built a summoning circle built one the game never looked at.
    /// </para>
    /// <para>
    /// WHY THIS IS NOT THE EDITOR'S TABLE MOVED DOWN. <c>Editor/Generators/Shared/DimensionQueryCompanions.cs</c>
    /// answers a different question in a different vocabulary: it is keyed on AUTHORING components
    /// (<c>SummonAreaAuthoring</c> needs <c>AnimationAuthoring</c>), it carries
    /// <c>Func&lt;GameObject, bool&gt;</c> repair delegates, and nine of its forty rows name a
    /// VALUE rather than a component ("a tracker that can actually see something"). None of that
    /// survives the trip to runtime, where there are no GameObjects and no authoring components —
    /// only the converted entity. So this table is written from the same source the editor's was
    /// written from, <c>ck-research/query-match-census.md</c>, in the vocabulary the runtime has.
    /// Every row here names the vanilla system whose query it encodes, so the two can be checked
    /// against each other by reading, and neither has to be derived from the other.
    /// </para>
    /// <para>
    /// EVERY PROBE IS A TYPED <c>HasComponent&lt;T&gt;</c>. Looking a component up by name would
    /// need <c>System.Reflection</c>, which Core Keeper's mod sandbox denies. The compiler resolves
    /// each one, which also means a component Core Keeper renames or removes fails the build here
    /// instead of quietly matching nothing in game.
    /// </para>
    /// <para>
    /// THE TABLE IS NOT COMPLETE AND DOES NOT CLAIM TO BE. The census checked 252 feature rows; this
    /// carries the recurring roots — the four components almost nothing the framework generates has
    /// — and the named cases whose failure is invisible. A rule that is not here is not a rule that
    /// passed.
    /// </para>
    /// <para>
    /// THREE KINDS OF RECORDED DEFECT CANNOT BE WRITTEN AS A ROW HERE AT ALL, and the reason is the
    /// same each time: this asks one question, "the object carries A, does it also carry B". Adding
    /// a row that cannot answer the question it is named after would be worse than the gap.
    /// </para>
    /// <list type="bullet">
    /// <item><b>A component a later pass DELETED</b> — the recorded case is
    /// <c>CooldownAuthoring</c> removed by <c>ApplyItemEffects</c> after the generator wrote it.
    /// With the component gone there is no trigger, and no other component says it was ever meant
    /// to be there: an item whose creator left the cooldown blank and an item whose cooldown was
    /// destroyed produce the same entity. <c>CooldownCD</c> is also never in an
    /// <c>EntityQuery</c> — <c>EquipmentSlot</c> reads it through a
    /// <c>ComponentLookup&lt;CooldownCD&gt;</c> off the item's prefab — so there is no companion
    /// set to be missing from. Catching this needs the generator to record what it MEANT to write,
    /// which is a change to every generator pass rather than a row.</item>
    /// <item><b>A value that is wrong rather than absent</b> — a <c>lootTableID</c> left pointing at
    /// a renamed table, a <c>NearbyEntitiesTrackerCD</c> with radius 0 and mask 0, a collider on the
    /// wrong filter. The census wrote this down about the tracker in as many words: a presence check
    /// finds it and moves on. Every loot rule in <c>DropLootSystem</c> is in the same position: the
    /// companions its jobs demand beside <c>DropsLootFromLootTableCD</c> — <c>ObjectDataCD</c>,
    /// <c>LocalTransform</c>, <c>RandomCD</c>, <c>EntityDestroyedCD</c>,
    /// <c>StartDroppingLootCD</c> — are all added by converters that fire on any
    /// <c>ObjectAuthoring</c>, so a rule for it could never fail while the defect it was asked to
    /// catch is a string.</item>
    /// <item><b>A body nothing queries for</b> — a placed object with no <c>PhysicsCollider</c> is
    /// unhittable, unmineable and invisible to every cast, and casts are not queries.
    /// <c>ColliderVariationCD</c> below is one place that gap does show up as a query gap. It was
    /// written here as "the only place", and that is more than was measured: the sweep behind it
    /// covered the world-object queries, and <c>LarvaHiveEggColliderSystem</c>'s
    /// <c>PhysicsCollider</c> + <c>LarvaHiveEggHatchStateCD</c> pair is a second one it did not
    /// reach. No rule for it is written here, because a row is only worth adding when somebody has
    /// read the query it encodes rather than a grep of it.</item>
    /// </list>
    /// <para>
    /// A ROW'S <c>ReadingSystem</c> IS WHATEVER ACTUALLY DEMANDS THE SET, which is not always one
    /// system's <c>EntityQuery</c>. Core Keeper's creature states are entered through
    /// <c>IStateRequester</c> gates, and the gate asks for components the state's own system does
    /// not: <c>ChaseStateRequest.ShouldUpdate</c> demands <c>PhysicsCollider</c>,
    /// <c>BehaviourTagsCD</c> and <c>ObjectDataCD</c>, none of which is in
    /// <c>ChaseStateSystem</c>'s query, and <c>MeleeAttackStateRequest.ShouldUpdate</c> demands
    /// <c>NearbyEntitiesTrackerCD</c> as well as the buffer. So those rows name the gate beside the
    /// system, because a message that says the system requires them would be false.
    /// </para>
    /// </remarks>
    internal static class DimensionQueryCompanionTable
    {
        /// <summary>One component, by name and by a compiled presence check.</summary>
        internal sealed class Need
        {
            public Need(string name, Func<EntityManager, Entity, bool> present)
            {
                Name = name;
                Present = present;
            }

            /// <summary>The component's name, as the census and the decompile write it.</summary>
            public string Name { get; private set; }

            /// <summary>Whether the entity carries it.</summary>
            public Func<EntityManager, Entity, bool> Present { get; private set; }
        }

        /// <summary>One vanilla system's query, as a rule about a finished object.</summary>
        internal sealed class Rule
        {
            public Rule(
                Need trigger,
                Need[] alsoNeeds,
                string readingSystem,
                string vanillaExample,
                string whatBreaks)
            {
                Trigger = trigger;
                AlsoNeeds = alsoNeeds;
                ReadingSystem = readingSystem;
                VanillaExample = vanillaExample;
                WhatBreaks = whatBreaks;
            }

            /// <summary>The component the framework writes, which puts the object in scope.</summary>
            public Need Trigger { get; private set; }

            /// <summary>The rest of the reading system's required set.</summary>
            public Need[] AlsoNeeds { get; private set; }

            /// <summary>The system whose query this is.</summary>
            public string ReadingSystem { get; private set; }

            /// <summary>
            /// Core Keeper's own object doing the same job, which carries all of it. Empty when
            /// there is none, and then the sentence naming one is left out altogether.
            /// </summary>
            /// <remarks>
            /// It was not optional before, and the beam rule — which says in its own text that no
            /// vanilla prefab carries a beam attack — produced "Core Keeper's own no vanilla prefab
            /// carries a beam attack carries the whole set."
            /// </remarks>
            public string VanillaExample { get; private set; }

            /// <summary>What a player would notice, in a player's words.</summary>
            public string WhatBreaks { get; private set; }
        }

        private static Need N<T>(string name)
        {
            return new Need(name, (em, e) => em.HasComponent<T>(e));
        }

        // The four that recur. The census calls these the roots: each one is demanded by many
        // systems and produced by exactly one authoring component the framework often does not add.
        private static readonly Need NearbyEntities = N<NearbyEntitiesBufferCD>("NearbyEntitiesBufferCD");
        private static readonly Need Animation = N<AnimationBuffer>("AnimationBuffer");
        private static readonly Need AnimationPointer = N<AnimationBufferPointer>("AnimationBufferPointer");
        private static readonly Need Orientation = N<AnimationOrientationCD>("AnimationOrientationCD");
        private static readonly Need Velocity = N<PhysicsVelocity>("PhysicsVelocity");
        private static readonly Need Collider = N<PhysicsCollider>("PhysicsCollider");
        private static readonly Need Ghost = N<GhostInstance>("GhostInstance");
        private static readonly Need Transform = N<LocalTransform>("LocalTransform");

        private static readonly Need StateInfo = N<StateInfoCD>("StateInfoCD");
        private static readonly Need ObjectData = N<ObjectDataCD>("ObjectDataCD");
        private static readonly Need Properties = N<ObjectPropertiesCD>("ObjectPropertiesCD");
        private static readonly Need Tags = N<BehaviourTagsCD>("BehaviourTagsCD");
        private static readonly Need Speed = N<MovementSpeedCD>("MovementSpeedCD");
        private static readonly Need Health = N<HealthCD>("HealthCD");
        private static readonly Need InCombat = N<IsInCombatCD>("IsInCombatCD");
        private static readonly Need Cooldown = N<AttackCooldownTimerCD>("AttackCooldownTimerCD");
        private static readonly Need Owner = N<OwnerReferenceCD>("OwnerReferenceCD");
        private static readonly Need Contained = N<ContainedObjectsBuffer>("ContainedObjectsBuffer");
        private static readonly Need DistanceToPlayer = N<DistanceToPlayerCD>("DistanceToPlayerCD");
        private static readonly Need Meals = N<MealsEatenCD>("MealsEatenCD");
        private static readonly Need Randomness = N<RandomCD>("RandomCD");
        private static readonly Need Electricity = N<Pug.Automation.ElectricityCD>("ElectricityCD");
        private static readonly Need NearbyTracker =
            N<NearbyEntitiesTrackerCD>("NearbyEntitiesTrackerCD");
        private static readonly Need ObjectType = N<ObjectTypeCD>("ObjectTypeCD");

        // The four markers below say which generator finished an object, and they exist so that the
        // ObjectTypeCD pairing can be guarded on the four kinds the environmental rule cannot see.
        // Each is written by one generator and by nothing else in this framework.

        /// <summary>A crop, either half of it.</summary>
        /// <remarks>
        /// <c>GrowingCD</c> rather than <c>PlantCD</c> because a crop is two objects and only one
        /// of them is the plant: <c>DimensionSeedConverter</c> and
        /// <c>DimensionPlantProduceConverter</c> both emit the growing timer, so this one probe
        /// covers the seed and the plant, and both go through the same
        /// <c>DimensionPlantGenerator.BuildPrefab</c> pass that adds the type.
        /// </remarks>
        private static readonly Need ItIsACrop = N<GrowingCD>("GrowingCD");

        /// <summary>A critter.</summary>
        private static readonly Need ItIsACritter = N<CritterCD>("CritterCD");

        /// <summary>A station, or anything else built to craft.</summary>
        /// <remarks>
        /// It reaches further than the workbench generator — a container or a world object that a
        /// drill can craft at gets <c>CraftingAuthoring</c> too
        /// (<c>DimensionObjectSpine.Machines.cs</c>) — and that costs nothing: both of those finish
        /// through <c>FinishAWorldObject</c>, which adds the type, so the rule cannot fail on one.
        /// </remarks>
        private static readonly Need ItIsAStation = N<CraftingCD>("CraftingCD");

        /// <summary>A vehicle, whichever of the three kinds it was built as.</summary>
        /// <remarks>
        /// Three probes because <c>DimensionVehicleGenerator.ApplyMovement</c> writes exactly one
        /// of the three and takes the other two off, so no single component says "vehicle".
        /// </remarks>
        private static readonly Need ItIsAVehicle = new Need(
            "BoatCD, MinecartCD or VehicleCD",
            (em, e) => em.HasComponent<BoatCD>(e)
                || em.HasComponent<MinecartCD>(e)
                || em.HasComponent<VehicleCD>(e));

        /// <summary>
        /// An object the environment is meant to reach, and that the game has not excluded.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THREE TYPED PROBES RATHER THAN ONE, because the query this is the trigger for has two
        /// <c>WithNone</c> terms and a rule that ignores them fires on objects the system was never
        /// going to look at. <c>BurningConditionCD</c> is the presence half:
        /// <c>SupportConditionsConverter</c> adds it, disabled, only when the author left
        /// <c>cantBeAffectedByEnvironment</c> off, so it is the one component that says "the
        /// environment is supposed to reach this thing" in the runtime's own vocabulary.
        /// <c>HasComponent</c> answers true for a disabled enableable component, which is what
        /// makes it readable off a prefab at all.
        /// </para>
        /// <para>
        /// Two of the three exclusions are copied from the query and not guessed:
        /// <c>ck-db/Pug.Other/EnvironmentalConditionsSystem.cs:887</c> opens
        /// <c>WithNone&lt;ProjectileCD&gt;().WithNone&lt;DestructibleObjectCD&gt;()</c>. Every
        /// generated projectile carries <c>SupportsConditionsAuthoring</c> with the environment
        /// switch left alone, so without the first exclusion a correctly generated shot reported a
        /// gap in a system that skips shots by name.
        /// </para>
        /// <para>
        /// THE THIRD ONE IS THIS TABLE'S OWN JUDGEMENT AND IS NOT IN THE QUERY. An arcing shell
        /// carries <c>MortarProjectileCD</c> INSTEAD of <c>ProjectileCD</c>
        /// (<c>DimensionProjectileGenerator.ConfigureArtillery</c> takes the straight-shot
        /// component off), so the game's own exclusion misses it and a correctly generated mortar
        /// would be reported here. It is left out because the sentence this rule prints — that
        /// burning ground and acid do nothing to it — is a true thing to say about a shell that
        /// exists for a second and a half and is not a thing anybody can act on. If a shot that
        /// cannot catch fire ever matters, this is the line to delete.
        /// </para>
        /// </remarks>
        private static readonly Need EnvironmentIsMeantToReachIt = new Need(
            "BurningConditionCD",
            (em, e) => em.HasComponent<BurningConditionCD>(e)
                && !em.HasComponent<ProjectileCD>(e)
                && !em.HasComponent<MortarProjectileCD>(e)
                && !em.HasComponent<DestructibleObjectCD>(e));

        private static readonly Rule[] RulesValue =
        {
            // ---- anything the weather, the floor and the puddles are meant to reach ---------
            //
            // ObjectTypeCD has exactly two producers — Core Keeper's
            // EntityMonoBehaviourDataConverter, which no generated object goes through
            // (ObjectConverter writes ObjectDataCD and not ObjectTypeCD,
            // ck-db/Pug.ECS.Conversion/ObjectConverter.cs:18-30), and this framework's own
            // DimensionObjectTypeAuthoring. So an object that misses the one line that adds it
            // drops out of EnvironmentalConditionsSystem's query, and nothing else would say so.
            //
            // THE TRIGGER IS ObjectTypeCD, NOT ObjectDataCD, WHICH IS ON EVERY CONVERTED PREFAB.
            // Triggered on ObjectDataCD the row asks the
            // question of everything the audit is handed, and answers with a consequence that is
            // only true of a fraction of it. The consequence — burning ground, acid, mould, oil and
            // slime — belongs to one query, and that query wants six more components than
            // ObjectTypeCD (ck-db/Pug.Other/EnvironmentalConditionsSystem.cs:887-894):
            // LocalTransform, ObjectTypeCD, SummarizedConditionsBuffer,
            // SummarizedConditionEffectsBuffer, Simulate, ConditionsBuffer and
            // ConditionTickTimerBuffer, with ProjectileCD and DestructibleObjectCD excluded. All
            // four condition buffers come from one converter, SupportConditionsConverter, off one
            // authoring component. An object with none of them was never in that query, so telling
            // its author that fire does not touch it is a sentence that is true and worthless.
            //
            // WHAT THAT COST, MEASURED. A boss with a summoning item and a map pin is generated as
            // three prefabs: the creature, <bossId>-summon-circle and <bossId>-map-marker. The two
            // extras carry no SupportsConditionsAuthoring — the creature generator gives them
            // ObjectAuthoring, their own ability and CloseTheGaps and nothing else — so they were
            // never candidates for the environmental pass, and the broad trigger reported both of
            // them, every session, on content that is exactly right. The subject list is not the
            // problem and the generators are not either: the pin and the circle are real generated
            // objects and belong in the audit's list, and adding a component to them to satisfy a
            // rule would be writing content to please a check.
            //
            // WHICH GENERATORS ADD ObjectTypeCD, MEASURED RATHER THAN REMEMBERED.
            // DimensionQueryCompanions.CarriesWhatItIsOntoTheRunningObject is called by
            // FinishACritter, by both overloads of FinishAWorldObject (containers, workbenches,
            // vehicles and world objects all go through it) and directly by the plant generator;
            // the creature and item generators add DimensionObjectTypeAuthoring themselves. Every
            // kind that also gets conditions support gets the type, so on correct content this rule
            // finds nothing — it guards the pairing, so that a new generator, or a kind that stops
            // going through the finisher, fails here rather than in a player's world. IT GUARDS
            // FOUR OF THE EIGHT KINDS THAT CARRY THE TYPE, not all eight.
            // The other four — crop, critter, station, vehicle — carry no
            // condition buffer and so can never reach this trigger; the four rows under this one
            // are theirs, and the comment on them says what each one's absence actually costs.
            //
            // The other two readers of ObjectTypeCD are not in this rule and cannot be: AttackSystem
            // and EntityUtility read it through a ComponentLookup with a default when it is absent,
            // so a missing type there is a wrong value rather than a missed query, which is the
            // second of the three shapes above.
            new Rule(
                EnvironmentIsMeantToReachIt,
                new[] { ObjectType },
                "EnvironmentalConditionsSystem",
                "LarvaEntity, and every other object it ships",
                "standing on burning ground, acid, mould, oil or slime does nothing to it: no "
                    + "burning, no poison, no slipping, no soaking. This object is built to be "
                    + "affected by the environment and carries the condition buffers for it, and "
                    + "the one component that puts it in front of that system is the one it has "
                    + "not got. Every object the game ships is built from an EntityMonoBehaviourData "
                    + "and gets it from the converter; a generated object only gets it where the "
                    + "generator adds DimensionObjectTypeAuthoring"),

            // ---- the four kinds the rule above cannot reach --------------------------------
            //
            // THE RULE ABOVE GUARDS FOUR OF THE EIGHT KINDS THAT CARRY ObjectTypeCD, AND THAT WAS
            // NOT SAID WHEN IT WAS NARROWED. Its trigger is BurningConditionCD, which arrives with
            // SupportConditionsConverter off SupportsConditionsAuthoring, and exactly four
            // generators call DimensionObjectSpine.ApplyInitialConditions, which is the only thing
            // in this framework that adds that authoring component: containers
            // (DimensionContainerGenerator), creatures (DimensionCreatureGenerator), items
            // (DimensionItemGenerator) and world objects (DimensionWorldObjectGenerator); the
            // method itself is in DimensionObjectSpine.Items.cs.
            // CloseTheGaps does not add it either — it is
            // in the companion pass's "needs nothing beside it" list. So a crop, a critter, a
            // station and a vehicle carry the type and never carry a condition buffer, the trigger
            // above cannot fire on one of them, and nothing said anything when the pairing came
            // apart. The type reaches all four through
            // DimensionQueryCompanions.CarriesWhatItIsOntoTheRunningObject, which is declared in
            // DimensionQueryCompanions.Sweeps.cs, called directly
            // by the plant generator and by FinishACritter and
            // both FinishAWorldObject overloads for the other three.
            //
            // WHAT A MISSING TYPE COSTS EACH OF THEM, MEASURED, AND IT IS NOT ONE SENTENCE FOUR
            // TIMES. One query in the whole game names ObjectTypeCD
            // (ck-db/Pug.Other/EnvironmentalConditionsSystem.cs:888) and it wants the four
            // condition buffers as well, so none of these four is ever in it. The other three
            // readers take a ComponentLookup and fall back to default(ObjectTypeCD), whose Value is
            // ObjectType.NonUsable — the enum's zero — so what the absence costs comes down to
            // whether the authored answer differs from that zero in a comparison somebody makes,
            // and there are only two of those:
            //   ck-db/Pug.Other/AttackSystem.cs:943 withholds the hit effect on the thing it hit
            //   when its type is PlaceablePrefab. A station and a vehicle are authored
            //   PlaceablePrefab — DimensionWorkbenchGenerator and DimensionVehicleGenerator both
            //   stamp objectType — so without the component they show a player the
            //   sparks and the flying number the game holds back for a building.
            //   ck-db/Pug.Other/EntityUtility.cs:1675 reads the same value into flag2, and both
            //   branches that use it (:1696, :1720) sit behind isCreated2 — the RECEIVER's own
            //   condition buffers. None of these four has one, so that reader cannot tell the
            //   difference either way.
            // A crop is authored NonObtainable and a critter Critter
            // (DimensionPlantGenerator and DimensionCritterGenerator). Neither equals
            // PlaceablePrefab and neither does the zero, so nothing in the game reads the
            // difference on those two today, and their rows say so instead of borrowing the
            // station's sentence. Overstating what was found is what the wide rule was removed for.
            //
            // NONE OF THE FOUR CAN FIRE ON CORRECT CONTENT. Each trigger is a component only the
            // generator for that kind writes, and each of those generators adds the type on a pass
            // it runs unconditionally over every object it builds.
            new Rule(
                ItIsAStation,
                new[] { ObjectType },
                "AttackSystem",
                "CopperWorkBenchEntity",
                "hitting it throws up the sparks and the flying damage number the game keeps for "
                    + "creatures. The game withholds those on a building, and it asks the running "
                    + "object what it is rather than the object table, so a station that does not "
                    + "carry its own type answers with the enum's zero instead of the placeable it "
                    + "was authored as"),
            new Rule(
                ItIsAVehicle,
                new[] { ObjectType },
                "AttackSystem",
                "BoatEntity",
                "hitting it throws up the sparks and the flying damage number the game keeps for "
                    + "creatures, for the same reason a station does: the game asks the running "
                    + "object whether it is a placeable, and a boat, cart or kart without its own "
                    + "type answers with the enum's zero"),
            new Rule(
                ItIsACrop,
                new[] { ObjectType },
                "AttackSystem",
                "CarrockPlantEntity",
                "nothing a player can see is different today, and that is the whole of what this "
                    + "row says. A crop is authored NonObtainable, which is not the one value "
                    + "anything compares against, so the missing answer costs it nothing yet. What "
                    + "it does mean is that the crop generator's last pass over this object did "
                    + "not run — it writes this component over the seed and the plant alike — so "
                    + "look at what else that pass writes before looking at this"),
            new Rule(
                ItIsACritter,
                new[] { ObjectType },
                "AttackSystem",
                "CritterLarvaEntity",
                "nothing a player can see is different today, the same as a crop: a critter is "
                    + "authored Critter, which nothing compares against either. It means the "
                    + "critter generator's finishing pass did not run over this object, and that "
                    + "pass also gives it its body, its network presence and its turning"),

            // ---- creatures: the four roots ------------------------------------------------
            new Rule(
                N<ChaseStateCD>("ChaseStateCD"),
                new[] { Ghost, Properties, Transform, Speed, StateInfo, Velocity, Collider,
                        Animation, AnimationPointer, Orientation, Randomness, Tags, ObjectData },
                "ChaseStateSystem, together with the ChaseStateRequest gate that admits an object "
                    + "to it (the gate is where PhysicsCollider, BehaviourTagsCD and ObjectDataCD "
                    + "are demanded)",
                "LarvaEntity",
                "it will stand still and face one direction no matter what a player does"),
            new Rule(
                N<MeleeAttackStateCD>("MeleeAttackStateCD"),
                new[] { Tags, Properties, Orientation, Animation, AnimationPointer, StateInfo,
                        Cooldown, NearbyTracker, NearbyEntities, Transform },
                "MeleeAttackStateSystem, together with the MeleeAttackStateRequest gate that "
                    + "admits an object to it (the gate is where LocalTransform, "
                    + "NearbyEntitiesTrackerCD and NearbyEntitiesBufferCD are demanded)",
                "LarvaEntity",
                "it will never swing at anything"),
            new Rule(
                N<RangeAttackStateCD>("RangeAttackStateCD"),
                new[] { Tags, Transform, Orientation, Animation, AnimationPointer, StateInfo,
                        Cooldown },
                "RangeAttackStateSystem",
                "CavelingEntity",
                "it will never fire at anything"),
            new Rule(
                N<ChargeAttackStateCD>("ChargeAttackStateCD"),
                new[] { Tags, Properties, Velocity, Transform, Orientation, Animation, StateInfo,
                        Collider, NearbyEntities },
                "ChargeAttackStateSystem",
                "LarvaEntity",
                "it will never charge"),
            new Rule(
                N<JumpAttackStateCD>("JumpAttackStateCD"),
                new[] { Transform, Velocity, Orientation, Animation, AnimationPointer, Properties,
                        StateInfo, Cooldown },
                "JumpAttackStateSystem",
                "SlimeBlobEntity",
                "it will never jump at anything"),
            new Rule(
                N<RandomWalkStateCD>("RandomWalkStateCD"),
                new[] { StateInfo, N<RandomWalkStatePatternCD>("RandomWalkStatePatternCD"), Transform,
                        Speed, Properties, N<DetectCollisionCD>("DetectCollisionCD"), Velocity,
                        Animation, Orientation, Randomness, AnimationPointer },
                "RandomWalkStateSystem",
                "CritterLarvaEntity",
                "it will never wander; it stays where it was put"),
            new Rule(
                N<RoamingStateCD>("RoamingStateCD"),
                new[] { Speed, N<RoamingPathBuffer>("RoamingPathBuffer"), StateInfo, Transform,
                        Orientation, Animation },
                "RoamingStateSystem",
                "CowEntity",
                "it will never roam"),
            new Rule(
                N<RandomFollowStateCD>("RandomFollowStateCD"),
                new[] { Transform, Speed, StateInfo, Velocity, Animation, Orientation },
                "RandomFollowStateSystem",
                "CowEntity",
                "it will never follow anything"),
            new Rule(
                N<FollowPheromoneStateCD>("FollowPheromoneStateCD"),
                new[] { Speed, Transform, Animation, Velocity, Orientation, StateInfo },
                "FollowPheromoneStateSystem",
                "LarvaEntity",
                "it will ignore pheromone trails"),
            new Rule(
                N<PetWalkStateCD>("PetWalkStateCD"),
                new[] { Velocity, StateInfo, Animation, AnimationPointer, Orientation, Owner },
                "PetWalkStateSystem",
                "PetSlimeEntity",
                "the pet will not follow its owner"),
            new Rule(
                N<MoveToPositionFromCommandStateCD>("MoveToPositionFromCommandStateCD"),
                new[] { Speed, Transform, Velocity, Orientation, AnimationPointer, Animation,
                        Properties, StateInfo },
                "MoveToPositionFromCommandStateSystem",
                "MinionEntity",
                "it will not go where it is told"),
            new Rule(
                N<SnakeMovementStateCD>("SnakeMovementStateCD"),
                new[] { Speed, N<TargetPointsBuffer>("TargetPointsBuffer"), DistanceToPlayer,
                        Transform, Velocity, StateInfo, N<RoamingPathBuffer>("RoamingPathBuffer") },
                "SnakeMovementStateSystem",
                "GhorimTheDestroyerEntity",
                "the segmented creature will not move at all"),
            new Rule(
                N<DamageObjectStateCD>("DamageObjectStateCD"),
                new[] { Transform, Properties, Animation, AnimationPointer, Orientation, StateInfo,
                        N<DetectCollisionCD>("DetectCollisionCD"), ObjectData, Tags },
                "DamageObjectStateSystem",
                "CavelingEntity",
                "it will walk past the walls and furniture it is meant to smash"),

            // ---- creatures: components one ability needs and its own converter never writes ---
            new Rule(
                N<EvolveStateCD>("EvolveStateCD"),
                new[] { Meals, Transform, ObjectData, StateInfo },
                "EvolveStateSystem",
                "CavelingEntity",
                "it will never grow into the thing it is meant to become, because nothing is "
                    + "counting what it has eaten"),
            new Rule(
                N<BreedStateCD>("BreedStateCD"),
                new[] { Meals, N<ChaseStateCD>("ChaseStateCD"), N<EatStateCD>("EatStateCD"),
                        ObjectData, Transform, Properties, StateInfo, Randomness },
                "BreedStateSystem",
                "CowEntity",
                "it will never breed: the game asks a breeding animal to walk to its mate and to "
                    + "have eaten enough, and both of those are separate abilities"),
            new Rule(
                N<SleepStateCD>("SleepStateCD"),
                new[] { InCombat, StateInfo, Animation, AnimationPointer },
                "SleepStateSystem",
                "CavelingEntity",
                "it will never sleep; the game only lets something sleep if it also knows how to "
                    + "be in a fight"),
            new Rule(
                N<IdleEmoteStateCD>("IdleEmoteStateCD"),
                new[] { InCombat, Transform },
                "IdleEmoteStateRequest",
                "CavelingEntity",
                "it will never play its idle emote"),
            new Rule(
                N<VulnerableStateCD>("VulnerableStateCD"),
                new[] { Transform, Health, StateInfo, Animation, AnimationPointer,
                        N<SummarizedConditionEffectsBuffer>("SummarizedConditionEffectsBuffer"),
                        InCombat },
                "VulnerableStateSystem",
                "GhorimTheDestroyerEntity",
                "it will never become vulnerable, so a fight built around that moment cannot be won"),
            new Rule(
                N<TouchAttackCD>("TouchAttackCD"),
                new[] { Tags, Owner, Transform, Animation },
                "TouchAttackStateSystem",
                "LarvaBossMinionEntity",
                "walking into it does nothing; the game only reads a touch attack on something "
                    + "that belongs to somebody"),
            new Rule(
                N<MinionOrbitCD>("MinionOrbitCD"),
                new[] { Owner },
                "MinionOrbitStateSystem",
                "OrbitalMinionEntity",
                "it will not orbit anything"),
            new Rule(
                N<ShieldCD>("ShieldCD"),
                new[] { StateInfo, Animation, AnimationPointer, InCombat },
                "StateBehaviourModifierSystem",
                "GhorimTheDestroyerEntity",
                "the shield will never come up"),
            new Rule(
                N<BushStateCD>("BushStateCD"),
                new[] { NearbyEntities, Transform, Properties, StateInfo, Animation,
                        AnimationPointer, Health, InCombat },
                "BushStateSystem",
                "BushEntity",
                "hiding in it does nothing and it never rustles"),
            new Rule(
                N<HatchWhenPlayerNearbyStateCD>("HatchWhenPlayerNearbyStateCD"),
                new[] { Transform, N<FactionCD>("FactionCD"), StateInfo, ObjectData, Health,
                        Animation },
                "HatchWhenPlayerNearbyStateSystem",
                "LarvaEggEntity",
                "the egg never hatches"),
            new Rule(
                N<SummonAreaCD>("SummonAreaCD"),
                new[] { NearbyEntities, Animation, AnimationPointer },
                "BossSummoningSystem",
                "LarvaBossSummonAreaEntity",
                "the summoning circle will never fire"),

            // ---- world objects -------------------------------------------------------------
            //
            // THE ONE PLACE A PLACED OBJECT'S MISSING BODY IS A QUERY GAP RATHER THAN A SILENCE.
            // A generated world object, container, workbench or vehicle that comes out with
            // no PhysicsCollider loses nearly everything — nothing can hit it, mine it,
            // dig it or find it with a cast — and almost all of that is invisible to a table like
            // this one, because casts
            // are not EntityQueries. ColliderVariationSystem's job IS a query, and it is exactly
            // ColliderVariationCD + PhysicsCollider + ObjectDataCD
            // (ck-db/Pug.Other/ColliderVariationSystem.cs:365-368), so a door whose collider never
            // swaps is expressible and is written here.
            //
            // NOTHING THIS FRAMEWORK GENERATES CARRIES THE TRIGGER TODAY, and saying so is part of
            // the row. DoorConverter adds ColliderVariationCD only when DoorAuthoring
            // .changesColliderByVariation is true, and DimensionWorldObjectGenerator sets it false
            // on purpose — one prefab per object means there is no second shape to swap to. So this
            // rule guards that decision rather than reporting on content: flip that flag back on,
            // or write a generator that produces a two-shape door, and a missing body stops being
            // silent.
            new Rule(
                N<ColliderVariationCD>("ColliderVariationCD"),
                new[] { Collider, ObjectData },
                "ColliderVariationSystem.UpdateColliderByVariationJob",
                "WoodDoorEntity",
                "the door's shape never changes when it opens or shuts, so a player is blocked by a "
                    + "door that looks open or walks through one that looks shut"),
            new Rule(
                N<ChangeVariationTriggerCD>("ChangeVariationTriggerCD"),
                new[] { N<Interaction.TriggerUseInteractionBuffer>("TriggerUseInteractionBuffer"),
                        ObjectData },
                "TriggerSetVariationSystem",
                "WoodDoorEntity",
                "the door cannot be opened"),
            new Rule(
                N<ChangeVariationWhenObjectNearbyCD>("ChangeVariationWhenObjectNearbyCD"),
                new[] { ObjectData, NearbyEntities, Transform, Tags, Animation, AnimationPointer },
                "ChangeVariationWhenObjectNearbySystem",
                "PressurePlateEntity",
                "it never notices anything standing on or near it"),
            new Rule(
                N<ChangeVariationWhenPlayerHoldObjectNearbyCD>(
                    "ChangeVariationWhenPlayerHoldObjectNearbyCD"),
                new[] { ObjectData, NearbyEntities, Transform, Animation, AnimationPointer },
                "ChangeVariationWhenPlayerHoldObjectNearbySystem",
                "ExcavationStatueEntity",
                "it never reacts to what a player is holding"),
            new Rule(
                N<AddForceToNearbyEntitiesCD>("AddForceToNearbyEntitiesCD"),
                new[] { NearbyEntities },
                "AddForceToNearbyEntitiesSystem",
                "RobotBossAttackPushbackEntity",
                "it shoves nothing"),
            new Rule(
                N<AttackContinuouslyCD>("AttackContinuouslyCD"),
                new[] { Tags, Properties, NearbyEntities, Transform, Animation, StateInfo },
                "AttackContinuouslyStateSystem",
                "SpikeTrapEntity",
                "the trap never hurts anything"),
            new Rule(
                N<ActivatedByElectricityStateCD>("ActivatedByElectricityStateCD"),
                new[] { Electricity, ObjectData, StateInfo, Animation, AnimationPointer },
                "ActivatedByElectricityStateSystem",
                "AncientLightEntity",
                "powering it does nothing"),
            new Rule(
                N<EnemySpawnerPlatformCD>("EnemySpawnerPlatformCD"),
                new[] { Transform, Electricity, Contained, DistanceToPlayer },
                "EnemySpawnerPlatformSystem",
                "EnemySpawnerPlatformEntity",
                "the spawner platform produces nothing"),
            new Rule(
                N<WayPointCD>("WayPointCD"),
                new[] { N<PugEntitiesUtil.CompanionInstantiatedEntityBuffer>(
                            "CompanionInstantiatedEntityBuffer"),
                        DistanceToPlayer, ObjectData, N<PortalCD>("PortalCD") },
                "WaypointSystem",
                "TeleporterEntity",
                "players cannot travel to it"),
            new Rule(
                N<MerchantCD>("MerchantCD"),
                new[] { StateInfo, N<MerchantItemInfoBuffer>("MerchantItemInfoBuffer"), Contained,
                        ObjectData },
                "MerchantBuyInventorySystem",
                "CavelingMerchantEntity",
                "the trader will not buy anything"),
            new Rule(
                N<SequenceExplosiveCD>("SequenceExplosiveCD"),
                new[] { N<IsExplosiveCD>("IsExplosiveCD"), ObjectData, Properties, Transform,
                        Randomness },
                "ExplosiveSystem.SequencedExplosionsJob",
                "SequencedExplosiveEntity",
                "the chain reaction never starts, because the game does not consider this object "
                    + "an explosive at all"),

            // ---- shots and blasts ----------------------------------------------------------
            new Rule(
                N<ProjectileCD>("ProjectileCD"),
                new[] { Tags, Ghost, Properties, Speed,
                        N<MovementSpeedModifierCD>("MovementSpeedModifierCD"), Health },
                "ProjectileSystem",
                "ArrowEntity",
                "the shot never flies and never hits anything"),
            new Rule(
                N<MortarProjectileCD>("MortarProjectileCD"),
                new[] { Health, Animation, AnimationPointer, Transform },
                "MortarProjectileSystem",
                "MortarProjectileEntity",
                "the arcing shot never lands"),
            new Rule(
                N<ExplosionCD>("ExplosionCD"),
                new[] { Tags, Ghost, Transform, ObjectData, Randomness },
                "ExplosionDamageSystem",
                "ExplosionEntity",
                "the blast does no damage"),
            new Rule(
                N<BeamAttackStateCD>("BeamAttackStateCD"),
                new[] { Transform, StateInfo, Cooldown, N<BeamBuffer>("BeamBuffer"), Animation,
                        AnimationPointer, Orientation },
                "BeamAttackStateSystem",
                string.Empty,
                "the beam never fires. Core Keeper ships no object with this ability and nothing "
                    + "in the game fills BeamBuffer, so this one may not be reachable at all"),

            // ---- plants --------------------------------------------------------------------
            new Rule(
                N<GrowingCD>("GrowingCD"),
                new[] { Properties, Transform },
                "PlantsGrowingSystem",
                "CarrockEntity",
                "the crop never advances a stage"),
        };

        /// <summary>Every rule, one per vanilla query worth checking.</summary>
        public static Rule[] All
        {
            get { return RulesValue; }
        }
    }
}
