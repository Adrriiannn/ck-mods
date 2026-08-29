using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The body of a generated sign: the picture a player walks up to, and the writing window that
    /// opens when they read it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS ONE DERIVES FROM <c>WorldLabel</c>, NOT FROM <c>SignText</c>, AND THAT IS THE WHOLE
    /// POINT. <c>SignText.UpdateSprite</c> runs every frame and dereferences two authored
    /// <c>SpriteObject</c> fields — <c>signWritten</c> and <c>signUnWritten</c> — with no null check,
    /// as soon as the entity has a description to show. A <c>SpriteObject</c> is addressed into a
    /// compiled sprite atlas and cannot be pointed at a loose <c>Sprite</c> an author dropped into a
    /// field, so those two fields can never be filled by a generator: the game's own sign component
    /// on a generated object is a <c>NullReferenceException</c> every frame, not a sign. The method
    /// is private and reached through <c>ManagedLateUpdate</c>, so there is no overriding around it
    /// from a subclass either.
    /// </para>
    /// <para>
    /// NOTHING IS LOST BY STEPPING DOWN A LEVEL, because the sign UI never asks for a
    /// <c>SignText</c>. <c>SignTextUI</c> and <c>ChestInventoryUI</c> both read
    /// <c>PlayerController.activeWorldLabel</c>, which is typed <c>WorldLabel</c>, and everything
    /// they use — <c>GetName</c>, <c>GetState</c>, <c>entity</c> — is declared on <c>WorldLabel</c>.
    /// The two methods below are <c>SignText.Interact</c> and <c>SignText.OnPlayerLeft</c> rewritten
    /// with the sprite switch left out.
    /// </para>
    /// <para>
    /// ONE GUARD IS STILL DROPPED FROM <c>Interact</c>, AND THE REASON HAS CHANGED. The game's
    /// version refuses to make a sign the active world label unless <c>worldLabel != null</c> — that
    /// is, unless the prefab carries the <c>PugText</c> for the floating writing above it. The
    /// generator now always builds one (see <c>DimensionFloatingTextUtility</c>; a <c>PugText</c>
    /// needs no font asset from a mod, because <c>PugText.font</c> is <c>[NonSerialized]</c> and
    /// filled from the game's own <c>TextManager</c> on every render). So the guard would normally
    /// pass. It stays dropped for the one case it would fail: a sign prefab written by an older
    /// version of this framework, where the field is null and the guard would silently turn the
    /// writing window into a box that saves nothing. What actually has to be true is that the sign
    /// can HOLD words, and that is the <c>DescriptionBuffer</c> check below.
    /// </para>
    /// <para>
    /// A TYPE OF OUR OWN IS ALSO THE ONLY WAY TO BE DRAWN AT ALL. Exactly one asset in the ripped
    /// corpus carries <c>SignText</c> — <c>Assets/GameObject/SignText.prefab</c>, listed in
    /// <c>Resources/PooledGraphicalObjectBank.asset</c> — and the game's pools are built before any
    /// mod's, so a generated sign carrying that component was handed the game's wooden sign to wear.
    /// </para>
    /// </remarks>
    public class DimensionSignView : WorldLabel, IDimensionAuthoredBody
    {
        /// <summary>
        /// The renderer that draws the sign. Wired at generation; the sprite it shows is replaced on
        /// every occupy, because this instance is shared with every other generated sign.
        /// </summary>
        [Tooltip("The renderer that draws this sign. Wired at generation.")]
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
        /// Ahead of <c>base.OnOccupied()</c>, which reads the entity's visibility state. A sign still
        /// showing the previous sign's picture for one frame is the visible bug the shared-view
        /// contract exists to stop.
        /// </remarks>
        public override void OnOccupied()
        {
            DimensionAuthoredBody.Point(body, objectData.objectID, objectData.variation);
            base.OnOccupied();
        }

        /// <summary>Opens the writing window against this sign.</summary>
        /// <remarks>
        /// The description buffer is checked because that is where the words are kept: without it
        /// there is nothing for the window to write to, and making the sign active would leave the
        /// player typing into an object that cannot hold the answer. The generator puts
        /// <c>DescriptionAuthoring</c> on anything it wires to this method, so the check normally
        /// passes.
        /// </remarks>
        public void Interact()
        {
            PlayerController player = Manager.main.player;
            if (player == null)
            {
                return;
            }

            bool canHoldWords = world.EntityManager.HasBuffer<DescriptionBuffer>(entity);
            player.SetActiveWorldLabel(canHoldWords ? this : null);
            Manager.ui.OnSignWindowOpen();
        }

        /// <summary>Closes the writing window when the player who opened it walks away.</summary>
        /// <remarks>
        /// The identity check matters because the pooled instance is shared: a player standing
        /// between two generated signs must not have the one they are writing on closed by the other
        /// one's exit trigger.
        /// </remarks>
        public void OnPlayerLeft()
        {
            PlayerController player = Manager.main.player;
            if (player == null || player.activeWorldLabel != this)
            {
                return;
            }

            Manager.ui.HideAllInventoryAndCraftingUI(true);
            player.SetActiveWorldLabel(null);
        }

        /// <summary>
        /// Lets go of the player before this instance is handed to the next sign.
        /// </summary>
        /// <remarks>
        /// Without this the pooled instance goes back to the pool while still being someone's active
        /// world label, and their next keystroke writes onto whatever object borrowed it next.
        /// </remarks>
        public override void OnFree()
        {
            OnPlayerLeft();
            base.OnFree();
        }

        /// <inheritdoc cref="OnFree"/>
        protected override void OnDeath()
        {
            base.OnDeath();
            OnPlayerLeft();
        }
    }
}
