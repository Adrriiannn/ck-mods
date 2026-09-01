using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The body of a generated shop: the picture a player walks up to, and the buy window that opens
    /// when they use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE VANILLA TYPE IS ALREADY SPOKEN FOR. Exactly one asset in the whole ripped corpus carries
    /// the bare <c>VendingMachine</c> component — <c>Assets/GameObject/VendingMachine.prefab</c>,
    /// listed in <c>Resources/PooledGraphicalObjectBank.asset</c>, which the game pools before it
    /// pools any mod. Because <c>MemoryManager.CreateModdedPrefabPool</c> registers a type with
    /// <c>TryAdd</c>, a generated shop carrying that component never drew itself at all: it was
    /// handed one of the Forlorn Metropolis machines and looked like one. A type of our own wins its
    /// own pool.
    /// </para>
    /// <para>
    /// EVERYTHING ELSE IS INHERITED UNCHANGED — the stock inventory handler, the buy-only window,
    /// and the walk-away tidy-up. <c>VendingMachine</c> declares no serialized fields and overrides
    /// no per-frame pass, so there is nothing to suppress and nothing to fill in.
    /// </para>
    /// <para>
    /// ONE POOL IS SHARED BY EVERY GENERATED SHOP, so the picture is re-read per entity in
    /// <see cref="OnOccupied"/> rather than baked into the prefab.
    /// </para>
    /// </remarks>
    public class DimensionShopView : VendingMachine,
        IDimensionAuthoredBody,
        IDimensionAuthoredLight
    {
        /// <summary>
        /// The renderer that draws the shop. Wired at generation; the sprite it shows is replaced on
        /// every occupy, because this instance is shared with every other generated shop.
        /// </summary>
        [Tooltip("The renderer that draws this shop. Wired at generation.")]
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
