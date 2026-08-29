using Pug.Sprite;
using UnityEngine;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// The body of a generated animal a player can walk up to, tend and name: an animated creature
    /// that also opens Core Keeper's own tending window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS A SEPARATE CLASS AND NOT A FLAG ON THE CREATURE VIEW. The tending window accepts
    /// nothing but a <c>Cattle</c> — <c>Cattle.Interact</c> calls
    /// <c>Manager.main.player.SetActiveCattle(this)</c> and <c>CattleUI</c> reads
    /// <c>player.activeCattle</c> as one — and <c>Cattle</c> derives from
    /// <c>EntityMonoBehaviour</c> directly, so a class cannot be both this and
    /// <see cref="DimensionCreatureView"/>. The drawing half is not duplicated: it lives in
    /// <see cref="DimensionCreaturePresentationBinder"/> and both views call it.
    /// </para>
    /// <para>
    /// AND NOT THE BARE <c>Cattle</c> EITHER. Core Keeper pools graphical objects by component TYPE
    /// and the first prefab to claim a type keeps it for the session, so two generated animals
    /// sharing the plain <c>Cattle</c> component would both be drawn as whichever loaded first.
    /// It is a different type from <c>DimensionCattleView</c> as well, which is the WORLD OBJECT
    /// that is tended like an animal and draws itself with a still <c>SpriteRenderer</c>: sharing
    /// that type would draw an animated creature as a placed object's picture.
    /// </para>
    /// <para>
    /// IT FACES WHERE IT GOES, ALWAYS, and that is not this class's choice. <c>Cattle</c> overrides
    /// all three of the animation-orientation questions to answer true, so unlike a plain creature
    /// there is no per-animal answer to re-derive. Every animal in the game a player can tend turns,
    /// so nothing is lost; it is written down because the plain view DOES re-derive it and the
    /// difference would otherwise read as an oversight.
    /// </para>
    /// <para>
    /// THE POOLED INSTANCE IS SHARED with every other tended animal, so everything that differs
    /// between them is re-read in <see cref="OnOccupied"/> and nothing is baked into the prefab.
    /// </para>
    /// </remarks>
    public class DimensionTendedAnimalView : Cattle
    {
        /// <summary>The sprite that plays the animal's clips. Wired at generation.</summary>
        [Tooltip("The sprite that plays the animal's clips. Wired at generation.")]
        public SpriteObject bodySprite;

        /// <summary>The blob under its feet. Wired at generation; may be absent.</summary>
        [Tooltip("The blob under its feet. Wired at generation; may be absent.")]
        public SpriteObject shadowSprite;

        /// <summary>The animal currently being drawn.</summary>
        protected DimensionCreaturePresentationDefinition currentPresentation;

        private readonly DimensionCreatureSoundTiming sounds = new DimensionCreatureSoundTiming();

        /// <summary>
        /// Listens for the moments inside a clip once, for the life of the instance.
        /// </summary>
        /// <remarks>
        /// Subscribing here rather than on every occupy is what keeps a pooled instance from
        /// stacking one subscription per animal it has ever drawn.
        /// </remarks>
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

        /// <summary>
        /// Points this instance at the animal it is currently being used for.
        /// </summary>
        /// <remarks>
        /// The order around <c>base.OnOccupied()</c> is the one the plain creature view explains and
        /// holds for the same reasons: the body first, because the base replays the last animation
        /// the server sent and a body with no clips answers that with nothing; the shadow
        /// afterwards, because the base switches the shadow object back on while it resets a pooled
        /// instance.
        /// </remarks>
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
        /// Runs the game's own per-frame pass — the floating name and the leash rope — unless this
        /// prefab was written without a name tag.
        /// </summary>
        /// <remarks>
        /// <c>Cattle.UpdateName</c> dereferences <c>nameTag</c> with no check, from
        /// <c>ManagedLateUpdate</c>, which runs every frame for every drawn entity. The generator
        /// fills the field; the guard is for a prefab written by an older build and still on disk in
        /// somebody's mod, which would otherwise throw every frame rather than merely being
        /// nameless. <c>EntityMonoBehaviour.ManagedLateUpdate</c> is an empty method body, so the
        /// skipped path costs the name and the rope and nothing else.
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
