using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The body Core Keeper gives a thing of each kind, and the shape that goes on it.
    /// </summary>
    internal static partial class DimensionQueryCompanions
    {
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
        /// component, so for anything this framework builds the two agree. On a hand-built vanilla
        /// prefab they do NOT agree, and the census cites the wrong one of the two.
        /// Nothing else in the tree writes a non-zero offset, so without this pass the term does
        /// nothing; the world object asset asks for it in words.
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
                // Category04 and NOT Category15. A critter walks through the game's own robot arm,
                // so it must not be stopped by a modded one.
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
    }
}
