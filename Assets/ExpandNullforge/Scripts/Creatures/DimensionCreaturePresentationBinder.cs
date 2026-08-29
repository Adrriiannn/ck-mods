using Pug.Sprite;
using UnityEngine;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Points a shared body and shadow at whichever creature is being drawn right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE COPY OF THE RULES, BECAUSE C# LEAVES NO CHOICE ABOUT THE CLASSES. A creature a player can
    /// walk up to has to BE what the game's window expects — <c>Cattle</c> for the tending window,
    /// <c>NPC</c> for the trading one — and both of those derive from
    /// <c>EntityMonoBehaviour</c> directly, so neither can also derive from
    /// <see cref="DimensionCreatureView"/>. Without this, the three views would each carry their own
    /// copy of how a body is pointed at an asset, and the trap in the middle of it would have to be
    /// remembered three times.
    /// </para>
    /// <para>
    /// THE TRAP. A <c>SpriteObject</c> decides once, in <c>OnEnable</c>, whether its asset has any
    /// animation events, and assigning a new asset does not revisit that. A creature whose art has
    /// moments, handed an instance that last drew one without, would play every frame and fire
    /// nothing. Switching the component off and on again is what re-asks the question, and it is
    /// safe because the readiness check that runs alongside it returns immediately once an instance
    /// is already ready.
    /// </para>
    /// <para>
    /// Nothing here reads or writes an entity. It is the drawing half only, so it can be called from
    /// a view whose base class already owns the rest.
    /// </para>
    /// </remarks>
    public static class DimensionCreaturePresentationBinder
    {
        private static readonly int IdleAnimation = Animator.StringToHash("idle");

        /// <summary>
        /// Points the body at the creature's own art.
        /// </summary>
        /// <remarks>
        /// CALL THIS BEFORE the base class's OnOccupied. The base replays whatever animation the
        /// server last sent for this entity, and a body that has not been given its clips yet
        /// answers that with nothing.
        /// </remarks>
        public static void ApplyBody(
            SpriteObject bodySprite,
            DimensionCreaturePresentationDefinition presentation)
        {
            if (bodySprite == null || presentation == null || !presentation.HasBody)
            {
                return;
            }

            // Re-derived like everything else: an instance last used for a creature whose clips
            // had no moments arrives with event processing switched off, and would then play a
            // clip full of footsteps in silence.
            bodySprite.processAnimationEvents = presentation.HasMoments;

            SpriteAsset wanted = new DataBlockRef<SpriteAsset>(
                new DataBlockAddress(
                    presentation.SpriteAssetAddressLow,
                    presentation.SpriteAssetAddressHigh)).Get();
            if (wanted == null || bodySprite.asset == wanted)
            {
                return;
            }

            bodySprite.asset = wanted;

            // See the class remarks: this is what re-asks whether the new asset has events.
            bool wasEnabled = bodySprite.enabled;
            bodySprite.enabled = false;
            bodySprite.enabled = wasEnabled;

            bodySprite.PlayAnimation(IdleAnimation, 0, true, true);
        }

        /// <summary>
        /// Sizes the blob under the creature's feet, or takes it away.
        /// </summary>
        /// <remarks>
        /// CALL THIS AFTER the base class's OnOccupied. The base switches the shadow object back on
        /// as part of resetting a pooled instance, so sizing it first would leave a creature that
        /// wants no shadow wearing the last one's.
        /// </remarks>
        public static void ApplyShadow(
            SpriteObject shadowSprite,
            DimensionCreaturePresentationDefinition presentation)
        {
            if (shadowSprite == null || presentation == null)
            {
                return;
            }

            if (!presentation.HasShadow)
            {
                shadowSprite.gameObject.SetActive(false);
                return;
            }

            shadowSprite.gameObject.SetActive(true);
            shadowSprite.SetVariant(presentation.ShadowVariantHash);
        }

        /// <summary>
        /// Puts the authored hit and death sounds where Core Keeper already looks for them.
        /// </summary>
        /// <remarks>
        /// The game plays whatever is in <c>soundOptions</c> when an entity is damaged and when it
        /// dies, so these two are not played by any view — playing them again would double every
        /// hit. The rest of a creature's sounds have no field anywhere and are played off an
        /// animation trigger instead.
        /// </remarks>
        public static void ApplyHitAndDeathSounds(
            EntityMonoBehaviour view,
            DimensionCreaturePresentationDefinition presentation)
        {
            if (view == null || presentation == null)
            {
                return;
            }

            if (view.soundOptions == null)
            {
                view.soundOptions = new EntityMonoBehaviour.SoundOptions();
            }

            view.soundOptions.takeDamageSfx = new SFXTableIDField { value = presentation.HitSound };
            view.soundOptions.deathSfx = new SFXTableIDField { value = presentation.DeathSound };
        }
    }

    /// <summary>
    /// The three noises a creature makes that Core Keeper would never make for it, and the gap
    /// that keeps one creature from sounding like a crowd.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SPAWNING, NOTICING YOU AND IDLING HAVE NO SOUND FIELD ANYWHERE in Core Keeper — only an
    /// animation that starts — so a view has to play them itself off the trigger. Hitting and dying
    /// are the opposite: the game already plays whatever is in <c>soundOptions</c>, so playing them
    /// here as well would double every hit.
    /// </para>
    /// <para>
    /// A creature returns to standing constantly — after every swing, every pause in a wander — so
    /// playing the idle noise on each return would make one caveling sound like a crowd. The gap is
    /// randomised so a group of them does not chirp in unison.
    /// </para>
    /// </remarks>
    public sealed class DimensionCreatureSoundTiming
    {
        /// <summary>The shortest gap between two idle noises, in seconds.</summary>
        public const float ShortestIdleSoundGap = 6f;

        /// <summary>The longest gap between two idle noises, in seconds.</summary>
        public const float LongestIdleSoundGap = 14f;

        private static readonly int IdleAnimation = Animator.StringToHash("idle");

        /// <summary>
        /// The trigger a creature's aggro noise rides on.
        /// </summary>
        /// <remarks>
        /// <c>ChaseStateSystem.cs:361</c> fires this the moment a creature decides to come after
        /// somebody. It is also a clip an author can draw for, under "Noticing you" — for a long
        /// while it was not, and this listener waited for a trigger nobody could give art to.
        /// </remarks>
        private static readonly int NoticedSomethingAnimation = Animator.StringToHash("preChase");

        private static readonly int SpawnAnimation = Animator.StringToHash("spawn");

        private float nextIdleSoundTime;

        /// <summary>Starts the idle gap over, for an instance just handed a new creature.</summary>
        public void StartTheGapAgain()
        {
            nextIdleSoundTime = Time.time + LongestIdleSoundGap;
        }

        /// <summary>Plays whatever this trigger is worth, at the given place.</summary>
        public void PlayForTrigger(
            int animID,
            DimensionCreaturePresentationDefinition presentation,
            Transform where)
        {
            if (presentation == null || where == null)
            {
                return;
            }

            if (animID == SpawnAnimation)
            {
                Play(presentation.SpawnSound, where);
                return;
            }

            if (animID == NoticedSomethingAnimation)
            {
                Play(presentation.AggroSound, where);
                return;
            }

            if (animID != IdleAnimation ||
                presentation.IdleSound == 0 ||
                Time.time < nextIdleSoundTime)
            {
                return;
            }

            Play(presentation.IdleSound, where);
            nextIdleSoundTime = Time.time +
                Random.Range(ShortestIdleSoundGap, LongestIdleSoundGap);
        }

        private static void Play(int sound, Transform where)
        {
            if (sound == 0)
            {
                return;
            }

            AudioManager.SfxFollowTransform(sound, where);
        }
    }
}
