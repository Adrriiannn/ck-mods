using ExpandNullforge.Objects;
using UnityEngine;

namespace ExpandNullforge.Containers
{
    /// <summary>
    /// The body of a generated container: the picture a player walks up to, and the chest window
    /// that opens when they use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS TYPE EXISTS AT ALL, RATHER THAN THE GAME'S OWN <c>Chest</c>. Core Keeper pools
    /// graphical objects by component TYPE, and the mapping from type to pool is first-come:
    /// <c>MemoryManager.CreateModdedPrefabPool</c> ends in
    /// <c>_poolFromComponentType.TryAdd(type, prefab)</c>, and the game's own pools are built
    /// before any mod's. A mod prefab carrying the vanilla <c>Chest</c> component therefore never
    /// draws itself: <c>CreateGraphicalObjectSystem</c> asks for a free component of that type and
    /// is handed one of the GAME's chests instead. Every custom container looked like whichever
    /// vanilla chest owned the pool, and no authored picture could ever appear. A type of our own
    /// wins its own pool.
    /// </para>
    /// <para>
    /// ONE SHARED CLASS, SELF-CONFIGURING, AND THAT IS A CONTRACT — the same rule
    /// <c>DimensionCreatureView</c> lives under. Our pool is shared by every container this
    /// framework generates, so the instance that drew a locked chest a moment ago is handed
    /// straight to a pedestal. Nothing that differs between containers may be baked into the
    /// prefab; the picture is re-read in <see cref="OnOccupied"/> from the entity's own object
    /// record.
    /// </para>
    /// <para>
    /// THE PICTURE TRAVELS IN <c>ObjectInfo.additionalSprites</c>, and
    /// <see cref="DimensionAuthoredBody"/> is the one place that knows it. That is not a spare field
    /// being borrowed: it is where Core Keeper already keeps an object's own picture for the case
    /// where something other than the world has to draw it — <c>PlayerController</c> reads
    /// <c>additionalSprites[0]</c> (falling back to <c>icon</c>) for the sprite of a carried
    /// object. Putting the container's world sprite there means one answer feeds both the thing
    /// standing on the ground and the thing held over a player's head, and the reference reaches
    /// the running game on the object's own record rather than through a side table that would
    /// have to be kept in step.
    /// </para>
    /// </remarks>
    public class DimensionContainerView : Chest, IDimensionAuthoredBody
    {
        /// <summary>
        /// The renderer that draws the container. Wired at generation; the sprite it shows is
        /// replaced on every occupy, because this instance is shared with every other container.
        /// </summary>
        [Tooltip("The renderer that draws this container. Wired at generation.")]
        public SpriteRenderer body;

        /// <inheritdoc/>
        public SpriteRenderer Body
        {
            get { return body; }
            set { body = value; }
        }

        /// <summary>
        /// Points the body at whatever the entity being drawn is, before the base class runs.
        /// </summary>
        /// <remarks>
        /// Ahead of <c>base.OnOccupied()</c> deliberately: the base builds the inventory handler
        /// and reads the object's data, and a container that is still showing the previous
        /// occupant's picture for one frame is the visible bug this whole class exists to stop.
        /// An object with no picture of its own leaves the renderer switched off rather than
        /// keeping the last one's — for a container the wrong art is worse than none, because a
        /// player would open it expecting the thing they can see.
        /// </remarks>
        public override void OnOccupied()
        {
            DimensionAuthoredBody.Point(body, objectData.objectID, objectData.variation);
            base.OnOccupied();
        }
    }
}
