using Pug.Sprite;
using UnityEngine;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// The body of a generated creature: the sprite that plays its animations, its shadow, and
    /// the sounds it makes while it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE SHARED CLASS, SELF-CONFIGURING, AND THAT IS A CONTRACT. Core Keeper pools graphical
    /// objects by component TYPE, so the instance that was drawing one creature a moment ago is
    /// handed straight to the next. Everything that differs between creatures is therefore
    /// re-derived in <see cref="OnOccupied"/> from the entity's own object id, and nothing at all
    /// is baked into the prefab. Bosses are safe from being handed a mob's instance for the same
    /// reason it is a risk here: <see cref="DimensionBossView"/> is a different type, so it pools
    /// separately.
    /// </para>
    /// <para>
    /// THE ONE TRAP IN SWAPPING THE ASSET AT RUNTIME. A SpriteObject decides once, in
    /// <c>OnEnable</c>, whether its asset has any animation events, and assigning a new asset does
    /// not revisit that. A creature whose art has events, handed an instance that last drew one
    /// without, would play every frame and fire nothing. Switching the component off and on again
    /// is what re-asks the question — and it is safe to do, because the readiness check that runs
    /// alongside it returns immediately once an instance is already ready.
    /// </para>
    /// <para>
    /// Hit and death sounds are not played here on purpose. Core Keeper already plays whatever is
    /// in <c>soundOptions</c> when an entity is damaged and when it dies, so all that is needed is
    /// to put the authored numbers there — playing them again from here would double every hit.
    /// The rest are played here because nothing in the game would: spawning, noticing a player and
    /// idling have no sound field anywhere, only an animation that starts.
    /// </para>
    /// </remarks>
    public class DimensionCreatureView : EntityMonoBehaviour
    {
        /// <summary>The sprite that plays the creature's clips. Wired at generation.</summary>
        [Tooltip("The sprite that plays the creature's clips. Wired at generation.")]
        public SpriteObject bodySprite;

        /// <summary>The blob under its feet. Wired at generation; may be absent.</summary>
        [Tooltip("The blob under its feet. Wired at generation; may be absent.")]
        public SpriteObject shadowSprite;

        /// <summary>
        /// Re-derived every time this view is handed an entity, never trusted from the prefab.
        /// </summary>
        private bool facesWhereItGoes;

        /// <summary>
        /// The three noises nothing in the game would play, and the gap between idle ones.
        /// </summary>
        /// <remarks>
        /// Shared with the tended-animal and talking-creature views, which cannot derive from this
        /// class: both have to BE the game's own <c>Cattle</c> and <c>NPC</c> for their windows to
        /// open, and both of those derive from <c>EntityMonoBehaviour</c> directly.
        /// </remarks>
        private readonly DimensionCreatureSoundTiming sounds = new DimensionCreatureSoundTiming();

        /// <summary>
        /// The creature currently being drawn, kept so the per-frame moment handler can look up
        /// what a fired moment sounds like without going back through the registry every frame.
        /// </summary>
        protected DimensionCreaturePresentationDefinition currentPresentation;

        protected override bool updateAnimOrientation
        {
            get { return facesWhereItGoes; }
        }

        protected override bool updateAnimMovement
        {
            get { return facesWhereItGoes; }
        }

        protected override bool updateAnimMovementSpeed
        {
            get { return facesWhereItGoes; }
        }

        /// <summary>
        /// Listens for the moments inside a clip once, for the life of the instance.
        /// </summary>
        /// <remarks>
        /// Subscribing here rather than each time the view is handed an entity is what keeps a
        /// pooled instance from stacking one subscription per creature it has ever drawn — after
        /// a few hundred spawns that would be a footstep playing a few hundred times.
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
            if (currentPresentation != null)
            {
                PlaySound(currentPresentation.SoundForMoment(momentHash));
            }
        }

        /// <summary>
        /// Points this instance at the creature it is currently being used for.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE ORDER AROUND base.OnOccupied() IS LOAD-BEARING IN BOTH DIRECTIONS. The body has to
        /// be pointed at its own sprite asset first, because the base replays whatever animation
        /// the server last sent for this entity and a body that has not been given its clips yet
        /// would answer that with nothing. The shadow has to be sized afterwards, because the base
        /// switches the shadow object back on as part of resetting a pooled instance — sizing it
        /// first would leave a creature that wants no shadow wearing the last one's.
        /// </para>
        /// <para>
        /// An unregistered creature leaves the instance exactly as it was rather than clearing it.
        /// Clearing would be tidier and worse: the visible result of a missing registration would
        /// become an invisible creature rather than one wearing the previous creature's art, and
        /// the second is far easier to notice and report.
        /// </para>
        /// </remarks>
        public override void OnOccupied()
        {
            DimensionCreaturePresentationDefinition presentation =
                DimensionCreaturePresentationRegistry.For(objectData.objectID);
            currentPresentation = presentation;
            if (presentation != null)
            {
                facesWhereItGoes = presentation.TurnsToFaceWhereItGoes;
                sounds.StartTheGapAgain();
                DimensionCreaturePresentationBinder.ApplyHitAndDeathSounds(this, presentation);
                ApplyBody(presentation);
            }

            base.OnOccupied();

            if (presentation != null)
            {
                ApplyShadow(presentation);
            }
        }

        /// <summary>
        /// Both bodies were lifted into <see cref="DimensionCreaturePresentationBinder"/> word for
        /// word, and these hand straight over to them.
        /// </summary>
        /// <remarks>
        /// Not tidying. Core Keeper's tending and trading windows only accept a <c>Cattle</c> and an
        /// <c>NPC</c>, both of which derive from <c>EntityMonoBehaviour</c> directly, so the views
        /// for those two cannot derive from this class and would otherwise need their own copy of
        /// the same rules — including the enable-and-disable trap in the middle of the body one.
        /// One copy, three callers.
        /// </remarks>
        private void ApplyBody(DimensionCreaturePresentationDefinition presentation)
        {
            DimensionCreaturePresentationBinder.ApplyBody(bodySprite, presentation);
        }

        /// <inheritdoc cref="ApplyBody"/>
        private void ApplyShadow(DimensionCreaturePresentationDefinition presentation)
        {
            DimensionCreaturePresentationBinder.ApplyShadow(shadowSprite, presentation);
        }

        protected override void HandleAnimationTrigger(int animID)
        {
            base.HandleAnimationTrigger(animID);
            sounds.PlayForTrigger(animID, currentPresentation, transform);
        }

        private void PlaySound(int sound)
        {
            if (sound == 0)
            {
                return;
            }

            AudioManager.SfxFollowTransform(sound, transform);
        }
    }
}
