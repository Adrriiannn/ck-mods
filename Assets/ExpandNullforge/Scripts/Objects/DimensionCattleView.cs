using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The body of a generated animal: the picture a player walks up to, and the tending window that
    /// opens when they use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IT HAS TO BE A <c>Cattle</c>, AND IT HAS TO BE OURS. The tending window will accept nothing
    /// else — <c>PlayerController.SetActiveCattle</c> takes a <c>Cattle</c> and <c>CattleUI</c> reads
    /// <c>player.activeCattle</c> as one — so this cannot be written against the base
    /// <c>EntityMonoBehaviour</c>. But Core Keeper pools graphical objects by component TYPE and the
    /// first prefab to claim a type keeps it, so a bare <c>Cattle</c> on two generated animals draws
    /// the second one with the first one's body forever. A type of our own is the only way both
    /// halves can be true.
    /// </para>
    /// <para>
    /// NOTHING IS OVERRIDDEN AWAY ANY MORE, AND THAT IS THE POINT OF THIS CLASS'S CURRENT SHAPE.
    /// <c>Cattle.UpdateName</c> dereferences <c>nameTag</c> and <c>Cattle.UpdateLeash</c>
    /// dereferences <c>XScaler</c> with no null check, both from <c>ManagedLateUpdate</c>, and
    /// <c>OnShow</c> and <c>OnHide</c> dereference <c>nameTag</c> again. This class used to override
    /// all three empty, because the generator could fill neither field. It now fills both — the
    /// scaler since the bodies work, and the name tag since the floating-text work — so the game's
    /// own per-frame pass runs, and a generated animal gets back its floating name AND the rope
    /// drawn when a player leads it.
    /// </para>
    /// <para>
    /// THE NAME IS ALLOWED TO BE ABSENT. <c>Cattle.GetName</c> returns null when the entity has no
    /// <c>NameCD</c>, and <c>UpdateName</c> answers that by hiding the tag, so an animal nobody has
    /// named yet simply has no words over it — the same as vanilla. The generator still puts
    /// <c>NameAuthoring</c> on every tended animal, because the tending window offers to name it
    /// whether or not it can hold one and the server throws on a name it cannot store.
    /// </para>
    /// <para>
    /// THE POOLED INSTANCE IS SHARED, so the picture is re-read per entity in
    /// <see cref="OnOccupied"/> and nothing that differs between animals may be baked into the
    /// prefab.
    /// </para>
    /// </remarks>
    public class DimensionCattleView : Cattle,
        IDimensionAuthoredBody,
        IDimensionAuthoredLight
    {
        /// <summary>
        /// The renderer that draws the animal. Wired at generation; the sprite it shows is replaced
        /// on every occupy, because this instance is shared with every other generated animal.
        /// </summary>
        [Tooltip("The renderer that draws this animal. Wired at generation.")]
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
        /// Ahead of <c>base.OnOccupied()</c> deliberately: an instance still showing the previous
        /// animal's picture for one frame is the visible bug the whole shared-view contract exists to
        /// stop.
        /// </remarks>
        public override void OnOccupied()
        {
            DimensionAuthoredBody.Point(body, objectData.objectID, objectData.variation);
            PointTheLight();
            base.OnOccupied();
        }

        /// <inheritdoc/>
        public void PointTheLight()
        {
            DimensionAuthoredLight.Point(optionalLightOptimizer, objectData.objectID);
        }

        /// <summary>
        /// Runs the game's own per-frame pass — the name tag and the leash rope — unless this
        /// prefab predates the tag.
        /// </summary>
        /// <remarks>
        /// The guard is for one case only: a prefab written by an older version of this framework,
        /// still on disk in somebody's mod, with <c>nameTag</c> left null. <c>Cattle.UpdateName</c>
        /// dereferences it with no check, so without the guard that prefab throws every frame
        /// instead of merely being nameless. Regenerating the animal fills the field and this
        /// branch stops being taken. <c>EntityMonoBehaviour.ManagedLateUpdate</c> is an empty method
        /// body, so the skipped path costs the name and the rope and nothing else.
        /// </remarks>
        public override void ManagedLateUpdate()
        {
            if (nameTag == null)
            {
                return;
            }

            base.ManagedLateUpdate();
        }

        /// <inheritdoc cref="ManagedLateUpdate"/>
        protected override void OnShow()
        {
            if (nameTag == null)
            {
                return;
            }

            base.OnShow();
        }

        /// <inheritdoc cref="ManagedLateUpdate"/>
        protected override void OnHide()
        {
            if (nameTag == null)
            {
                return;
            }

            base.OnHide();
        }
    }
}
