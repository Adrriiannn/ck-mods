using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The layer numbers and radii vanilla uses, and the serialized writes that set them.
    /// </summary>
    internal static partial class DimensionQueryCompanions
    {
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
    }
}
