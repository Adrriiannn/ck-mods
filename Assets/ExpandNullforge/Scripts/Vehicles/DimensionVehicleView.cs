using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Vehicles
{
    /// <summary>
    /// The body of a generated vehicle: the picture a player walks up to, and the thing that stops
    /// fighting the rider once somebody is sitting on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS TYPE EXISTS RATHER THAN THE GAME'S OWN <c>Boat</c>, <c>GoKart</c> OR <c>Minecart</c>.
    /// Two separate reasons, either of which alone would be enough.
    /// </para>
    /// <para>
    /// First, POOLING IS BY COMPONENT TYPE AND FIRST COME WINS. <c>MemoryManager</c> maps a component
    /// type to one pooled prefab and the game's own pools are built before any mod's, so a mod prefab
    /// carrying the vanilla <c>GoKart</c> component is handed one of the GAME's karts to draw itself
    /// with — the same trap <see cref="ExpandNullforge.Containers.DimensionContainerView"/> was
    /// written to escape. A type of our own wins its own pool.
    /// </para>
    /// <para>
    /// Second, THE GAME'S VEHICLE PRESENTERS DEREFERENCE HAND-AUTHORED FIELDS WITH NO NULL CHECK.
    /// <c>GoKart.ManagedLateUpdate</c> reads <c>effectsDirection</c>, four <c>ParticleSystem</c>s and
    /// <c>interactable.optionalOutlineController</c> every frame, and <c>Boat.UpdateBoatParticles</c>
    /// reads <c>waterParticles</c> the same way. A generator cannot synthesise that hierarchy, so
    /// putting the game's component on a generated prefab is a NullReferenceException every frame
    /// rather than a vehicle. That is the honest cost of a generated vehicle: it is a picture that
    /// moves, with no dust, no smoke and no engine loop.
    /// </para>
    /// <para>
    /// ONE SHARED CLASS, SELF-CONFIGURING, AND THAT IS A CONTRACT — the same rule
    /// <see cref="ExpandNullforge.Containers.DimensionContainerView"/> lives under. Our pool is shared
    /// by every vehicle this framework generates, so the instance that drew a boat a moment ago is
    /// handed straight to a cart. Nothing that differs between vehicles may be baked into the prefab;
    /// the picture is re-read in <see cref="OnOccupied"/> from the entity's own object record.
    /// </para>
    /// </remarks>
    public class DimensionVehicleView : EntityMonoBehaviour
    {
        /// <summary>
        /// The renderer that draws the vehicle. Wired at generation; the sprite it shows is replaced
        /// on every occupy, because this instance is shared with every other generated vehicle.
        /// </summary>
        [Tooltip("The renderer that draws this vehicle. Wired at generation.")]
        public SpriteRenderer body;

        /// <summary>
        /// Never asks the animator to turn the art.
        /// </summary>
        /// <remarks>
        /// A generated vehicle has a plain <see cref="SpriteRenderer"/> and no Animator, and the base
        /// class's orientation and movement passes exist only to push floats at one. Saying no here
        /// keeps those passes off a view that has nothing for them to drive.
        /// </remarks>
        protected override bool updateAnimOrientation
        {
            get { return false; }
        }

        /// <inheritdoc cref="updateAnimOrientation"/>
        protected override bool updateAnimMovement
        {
            get { return false; }
        }

        /// <summary>
        /// Points the body at whatever the entity being drawn is, before the base class runs.
        /// </summary>
        /// <remarks>
        /// Ahead of <c>base.OnOccupied()</c> deliberately, and for the same reason a container does
        /// it: an instance still showing the previous occupant's picture for one frame is the visible
        /// bug this class exists to stop. A vehicle with no picture of its own leaves the renderer
        /// switched off rather than keeping the last one's.
        /// </remarks>
        public override void OnOccupied()
        {
            if (body != null)
            {
                Sprite sprite = ResolveSprite();
                body.sprite = sprite;
                body.enabled = sprite != null;
            }

            base.OnOccupied();
        }

        /// <summary>
        /// Stops the body chasing its own entity while somebody is riding it.
        /// </summary>
        /// <remarks>
        /// Copied deliberately from <c>Boat.UpdatePosition</c> and <c>GoKart.UpdatePosition</c>, which
        /// are identical to each other and to this. While a vehicle is ridden its transform is driven
        /// by <c>SetControllableLocalToWorldJob</c> from the RIDER, and the entity's own
        /// <c>LocalToWorld</c> is one frame behind that; letting the base class write it back is what
        /// makes a ridden vehicle stutter between the two positions. The guard checks both sides of
        /// the claim, because a controller that has moved on to something else must let this one go.
        /// </remarks>
        public override void UpdatePosition(bool hasLocalToWorld, in LocalToWorld localToWorld)
        {
            ControlledByOtherEntityCD controlledBy =
                EntityUtility.GetComponentData<ControlledByOtherEntityCD>(entity, world);

            ControllingOtherEntityCD controlling;
            bool somebodyElseIsDrivingThis =
                controlledBy.controlledByEntity != Entity.Null &&
                (!EntityUtility.TryGetComponentData(controlledBy.controlledByEntity, world, out controlling) ||
                 controlling.controlledEntity == entity);

            if (!somebodyElseIsDrivingThis)
            {
                base.UpdatePosition(hasLocalToWorld, in localToWorld);
            }
        }

        /// <summary>
        /// The vehicle's own picture, or its inventory icon when it has none.
        /// </summary>
        /// <remarks>
        /// <c>additionalSprites[0]</c> is not a spare field being borrowed: it is already where Core
        /// Keeper keeps an object's picture for the case where something other than the world draws
        /// it. The icon fallback means a vehicle authored with only an icon still shows something.
        /// </remarks>
        private Sprite ResolveSprite()
        {
            ObjectInfo info = PugDatabase.GetObjectInfo(objectData.objectID, objectData.variation);
            if (info == null)
            {
                return null;
            }

            if (info.additionalSprites != null && info.additionalSprites.Count > 0 &&
                info.additionalSprites[0] != null)
            {
                return info.additionalSprites[0];
            }

            return info.icon;
        }
    }
}
