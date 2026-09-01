using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The body of a generated character: the picture a player walks up to, and the trading window
    /// that opens when they talk to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TYPE OF OUR OWN, FOR THE POOLING REASON. Core Keeper pools graphical objects by component
    /// TYPE and the first prefab to claim a type keeps it for the session
    /// (<c>MemoryManager.CreateModdedPrefabPool</c> ends in a <c>TryAdd</c>), so two generated
    /// characters sharing the bare <c>NPC</c> component would both be drawn with whichever one loaded
    /// first. Deriving keeps everything <c>NPC</c> does — the buy-side inventory handler built in
    /// <c>OnOccupied</c>, the vendor window, the walk-away tidy-up — and only changes which pool the
    /// body comes out of.
    /// </para>
    /// <para>
    /// NOTHING HAS TO BE SUPPRESSED HERE, UNLIKE THE ANIMAL. <c>NPC</c> declares no fields at all and
    /// overrides no per-frame pass, so there is no hand-authored hierarchy to dereference. The one
    /// thing it does ask for is orientation-driven animation, and the generator answers that by
    /// giving the body an <c>XScaler</c> transform — without it
    /// <c>EntityMonoBehaviour.SetOrientation</c> throws the first time a facing direction is
    /// non-zero.
    /// </para>
    /// <para>
    /// THE POOLED INSTANCE IS SHARED with every other generated character, so the picture is re-read
    /// per entity in <see cref="OnOccupied"/>.
    /// </para>
    /// </remarks>
    public class DimensionNpcView : NPC,
        IDimensionAuthoredBody,
        IDimensionAuthoredLight
    {
        /// <summary>
        /// The renderer that draws the character. Wired at generation; the sprite it shows is
        /// replaced on every occupy, because this instance is shared with every other generated
        /// character.
        /// </summary>
        [Tooltip("The renderer that draws this character. Wired at generation.")]
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
    }
}
