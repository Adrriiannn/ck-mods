using Pug.Sprite;
using UnityEngine;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// The body of a generated creature a player can talk to and trade with: an animated creature
    /// that also opens Core Keeper's own vendor window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE SAME REASONING AS THE TENDED ANIMAL. <c>NPC.Interact</c> hands the player's sell slots
    /// and its own inventory handler to the vendor window, and that handler is built in
    /// <c>NPC.OnOccupied</c>, so the component really has to be an <c>NPC</c>. <c>NPC</c> derives
    /// from <c>EntityMonoBehaviour</c> directly, so this cannot also be
    /// <see cref="DimensionCreatureView"/>; the drawing half lives in
    /// <see cref="DimensionCreaturePresentationBinder"/> and is shared rather than copied.
    /// </para>
    /// <para>
    /// A TYPE OF OUR OWN, for the pooling reason: Core Keeper pools graphical objects by component
    /// type, so two generated merchants sharing the plain <c>NPC</c> component would both be drawn
    /// as whichever loaded first. It is a different type from <c>DimensionNpcView</c> as well,
    /// which is the placed object that is talked to and draws itself with a still
    /// <c>SpriteRenderer</c>.
    /// </para>
    /// <para>
    /// A MERCHANT THAT WALKS IS BUILDABLE; A MERCHANT ROOM IS NOT. Core Keeper decides which object
    /// belongs in a merchant room from static data read inside a Burst job
    /// (<c>SpawnMerchantSystem.GetRequiredObjectForMerchant</c>), so a custom trader can spawn,
    /// wander and trade, and cannot be placed by the game's own merchant-room rule.
    /// </para>
    /// <para>
    /// <c>NPC</c> answers true to all three animation-orientation questions and declares no fields
    /// of its own, so there is nothing here to guard against and nothing to re-derive but the art.
    /// </para>
    /// </remarks>
    public class DimensionTalkingCreatureView : NPC
    {
        /// <summary>The sprite that plays the creature's clips. Wired at generation.</summary>
        [Tooltip("The sprite that plays the creature's clips. Wired at generation.")]
        public SpriteObject bodySprite;

        /// <summary>The blob under its feet. Wired at generation; may be absent.</summary>
        [Tooltip("The blob under its feet. Wired at generation; may be absent.")]
        public SpriteObject shadowSprite;

        /// <summary>The creature currently being drawn.</summary>
        protected DimensionCreaturePresentationDefinition currentPresentation;

        private readonly DimensionCreatureSoundTiming sounds = new DimensionCreatureSoundTiming();

        /// <inheritdoc cref="DimensionTendedAnimalView.Awake"/>
        protected override void Awake()
        {
            base.Awake();
            if (bodySprite != null)
            {
                bodySprite.onAnimationEvent += OnMoment;
            }
        }

        private void OnMoment(int momentHash)
        {
            if (currentPresentation == null)
            {
                return;
            }

            int sound = currentPresentation.SoundForMoment(momentHash);
            if (sound != 0)
            {
                AudioManager.SfxFollowTransform(sound, transform);
            }
        }

        /// <inheritdoc cref="DimensionTendedAnimalView.OnOccupied"/>
        public override void OnOccupied()
        {
            DimensionCreaturePresentationDefinition presentation =
                DimensionCreaturePresentationRegistry.For(objectData.objectID);
            currentPresentation = presentation;
            if (presentation != null)
            {
                sounds.StartTheGapAgain();
                DimensionCreaturePresentationBinder.ApplyHitAndDeathSounds(this, presentation);
                DimensionCreaturePresentationBinder.ApplyBody(bodySprite, presentation);
            }

            base.OnOccupied();

            if (presentation != null)
            {
                DimensionCreaturePresentationBinder.ApplyShadow(shadowSprite, presentation);
            }
        }

        protected override void HandleAnimationTrigger(int animID)
        {
            base.HandleAnimationTrigger(animID);
            sounds.PlayForTrigger(animID, currentPresentation, transform);
        }

        /// <summary>
        /// The noise it makes when a player talks to it: its own idle sound.
        /// </summary>
        /// <remarks>
        /// <c>NPC.PlayInteractSound</c> is an empty virtual on purpose — every one of the game's own
        /// characters is a hand-built prefab that overrides it. A generated one has no separate
        /// answer for this, and its idle noise is the one sound it is known by, so that is what it
        /// says when it is spoken to.
        /// </remarks>
        public override void PlayInteractSound()
        {
            if (currentPresentation == null || currentPresentation.IdleSound == 0)
            {
                return;
            }

            AudioManager.SfxFollowTransform(currentPresentation.IdleSound, transform);
        }
    }
}
