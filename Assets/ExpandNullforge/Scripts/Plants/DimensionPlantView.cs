using Pug.Sprite;
using UnityEngine;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// The body of a generated crop: the picture in the ground, its shadow and its glow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE PIECE THAT WAS MISSING. A generated crop carried everything needed to grow —
    /// stages, timers, a harvest, better versions — and nothing at all to draw, so it grew and
    /// ripened while being completely invisible in the soil. The authoring surface for its art was
    /// deleted rather than left promising, and this is what makes it real again.
    /// </para>
    /// <para>
    /// ONE SHARED CLASS, SELF-CONFIGURING, AND THAT IS A CONTRACT. Core Keeper pools graphical
    /// objects by the TYPE of the component on them, so the instance that was just a carrot is
    /// handed straight to a pumpkin, holding the carrot's sprite asset, the carrot's colour wash
    /// and the carrot's glow. Nothing per-crop may be baked into this prefab; everything is looked
    /// up again in <see cref="OnOccupied"/> from the object id and variation the entity carries.
    /// That is the same rule <c>DimensionCreatureView</c> lives by, for the same reason.
    /// </para>
    /// <para>
    /// AN UNREGISTERED PLANT IS LEFT ALONE RATHER THAN CLEARED. Clearing would be tidier and worse:
    /// the visible result of a missing registration would become an invisible crop, which is
    /// exactly the failure this class exists to end, instead of one wearing the previous crop's
    /// art — which anybody would notice and report.
    /// </para>
    /// <para>
    /// NO FIXED NUMBER OF STAGES. Core Keeper's own plant renderer holds four animations and plays
    /// number <c>stage + 1</c>, so a crop with more than three looks reads off the end of that
    /// array on the client. Nothing here is fixed: the stage list comes from the registry and is
    /// exactly as long as the crop has stages, so a crop may have as many as its author draws.
    /// </para>
    /// </remarks>
    public class DimensionPlantView : EntityMonoBehaviour
    {
        /// <summary>The sprite that shows the plant itself. Wired at generation.</summary>
        [Tooltip("The sprite that shows the plant itself. Wired at generation.")]
        public SpriteObject plantSprite;

        /// <summary>The plant's own shape laid on the ground. Wired at generation.</summary>
        [Tooltip("The plant's own shape laid on the ground. Wired at generation.")]
        public SpriteObject shadowSprite;

        /// <summary>The soft blob of light a glowing plant throws. Wired at generation.</summary>
        [Tooltip("The soft blob of light a glowing plant throws. Wired at generation.")]
        public SpriteObject glowSprite;

        /// <summary>The material for a plant lit by the cave around it. Wired at generation.</summary>
        [Tooltip("The material for a plant lit by the cave around it. Wired at generation.")]
        public Material litMaterial;

        /// <summary>The material for a plant that is its own light. Wired at generation.</summary>
        [Tooltip("The material for a plant that is its own light. Wired at generation.")]
        public Material unlitMaterial;

        /// <summary>The look currently being drawn, re-derived every time an entity arrives.</summary>
        protected DimensionPlantPresentationDefinition currentLook;

        /// <summary>
        /// The stage the pictures are currently showing, which is not the entity's stage.
        /// </summary>
        /// <remarks>
        /// Kept apart so the frame a plant grows on can be noticed: everything that happens when a
        /// crop moves on a stage — the little jump, the ripening puff, the sound — hangs off the
        /// two disagreeing, and comparing against the entity every frame would fire none of it.
        /// </remarks>
        private int shownStage = -1;

        private bool reachedFinalStage;

        private float nextShineTime;

        /// <summary>The shortest and longest gap between two twinkles on a ripe plant.</summary>
        /// <remarks>Vanilla's own numbers, read off <c>Plant.ResetShineCooldown</c>.</remarks>
        private const float ShortestShineGap = 4f;

        private const float LongestShineGap = 8f;

        /// <summary>
        /// The little jump a plant makes when it moves on a stage.
        /// </summary>
        /// <remarks>
        /// A globally registered transform animation, not one this mod ships — the game looks it up
        /// by number and quietly does nothing when it has no entry, so calling it can never throw.
        /// </remarks>
        private static readonly int GrowAnimation = AnimID.plantGrow;

        public override void OnOccupied()
        {
            // A look with no pictures behind it is treated exactly like no look at all. The sprite
            // objects on this prefab ship pointing at nothing, so every call below would be made
            // against a null asset — and the game's own sprite code reads that reference without
            // checking it in three of the places this view would reach.
            currentLook = DimensionPlantPresentationRegistry.For(objectData.objectID, variation);
            if (currentLook != null && !currentLook.HasArt)
            {
                currentLook = null;
            }

            shownStage = -1;
            reachedFinalStage = false;
            nextShineTime = float.MaxValue;

            // The body has to be pointed at its own pictures BEFORE the base runs, because the base
            // replays whatever animation the server last sent for this entity, and a body that has
            // not been given its pictures yet would answer that with nothing.
            if (currentLook != null)
            {
                ApplyArt(currentLook);
            }

            base.OnOccupied();

            // The shadow is sized afterwards, because the base switches child objects back on as
            // part of resetting a pooled instance: doing it first would leave a plant that wants no
            // shadow wearing the last one's.
            if (currentLook != null)
            {
                ApplyShadow(currentLook);
                UpdateLook(false);
            }
        }

        public override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            if (currentLook == null)
            {
                return;
            }

            UpdateLook(true);
        }

        /// <summary>
        /// Points both sprites at this crop's own pictures and washes them its own colour.
        /// </summary>
        /// <remarks>
        /// The switch off and on again is not superstition: a SpriteObject decides once, in
        /// <c>OnEnable</c>, whether its asset has any animation events, and assigning a new asset
        /// does not revisit that. It is safe to do because the readiness check that runs alongside
        /// returns immediately once an instance is already ready.
        /// </remarks>
        protected virtual void ApplyArt(DimensionPlantPresentationDefinition look)
        {
            SpriteAsset wanted = look.HasArt
                ? new DataBlockRef<SpriteAsset>(
                    new DataBlockAddress(
                        look.SpriteAssetAddressLow,
                        look.SpriteAssetAddressHigh)).Get()
                : null;

            ApplyAssetTo(plantSprite, wanted, look.ColourWash);

            // The shadow draws the same pictures in flat black, exactly as every vanilla plant's
            // does — which is why it takes the asset and not the wash.
            ApplyAssetTo(shadowSprite, wanted, ShadowColour);
        }

        private void ApplyAssetTo(SpriteObject sprite, SpriteAsset wanted, Color colour)
        {
            if (sprite == null)
            {
                return;
            }

            sprite.color = colour;
            if (wanted == null || sprite.asset == wanted)
            {
                return;
            }

            sprite.asset = wanted;
            bool wasEnabled = sprite.enabled;
            sprite.enabled = false;
            sprite.enabled = wasEnabled;
        }

        /// <summary>The flat black every vanilla plant lays its own shape on the ground in.</summary>
        /// <remarks>
        /// Measured off <c>Plant.prefab</c>'s shadow sprite: black at 0.6117647 alpha. Public
        /// because the generator paints the shared body with it and this view repaints it on every
        /// pooled instance — two places that must not drift, since a shadow that is not this exact
        /// black sits visibly differently on the ground from the crop planted next to it.
        /// </remarks>
        public static readonly Color ShadowColour = new Color(0f, 0f, 0f, 0.6117647f);

        protected virtual void ApplyShadow(DimensionPlantPresentationDefinition look)
        {
            if (shadowSprite != null)
            {
                shadowSprite.gameObject.SetActive(look.CastsAShadow);
            }
        }

        /// <summary>
        /// Catches the pictures up with the plant, and plays what belongs to changing.
        /// </summary>
        /// <param name="playChangeEffects">
        /// False on the frame a pooled instance is handed this entity. A plant already halfway
        /// grown when it comes on screen has not just grown, and puffing leaves over every plant a
        /// player walks past would read as the whole farm ripening at once.
        /// </param>
        protected virtual void UpdateLook(bool playChangeEffects)
        {
            int stage = ReadStage();
            if (stage != shownStage)
            {
                shownStage = stage;
                PlayStage(stage, playChangeEffects);
            }

            if (!reachedFinalStage || !currentLook.HasShine || Time.time < nextShineTime)
            {
                return;
            }

            PlayOnBothSprites(currentLook.ShineAnimation, false);
            ResetShineTimer();
        }

        /// <summary>
        /// Which stage the entity is on, and whether it has finished growing.
        /// </summary>
        /// <remarks>
        /// A plant that has reached its final stage keeps the answer rather than asking again:
        /// vanilla does the same, and the reason is that the growing component is removed from
        /// nothing — it is the property blob that says where the top is, and reading a blob per
        /// plant per frame is the one part of this that is not free.
        /// </remarks>
        private int ReadStage()
        {
            if (reachedFinalStage)
            {
                return shownStage < 0 ? 0 : shownStage;
            }

            if (!EntityUtility.HasComponentData<GrowingCD>(entity, world))
            {
                reachedFinalStage = true;
                return 0;
            }

            GrowingCD growing = EntityUtility.GetComponentData<GrowingCD>(entity, world);
            if (growing.currentStage >= currentLook.StageAnimations.Length - 1)
            {
                reachedFinalStage = true;
            }

            return growing.currentStage;
        }

        private void PlayStage(int stage, bool playChangeEffects)
        {
            int animation = currentLook.AnimationForStage(stage);
            if (animation != 0)
            {
                PlayOnBothSprites(animation, true);
                if (plantSprite != null)
                {
                    plantSprite.RandomizeCurrentAnimationTime();
                    if (shadowSprite != null)
                    {
                        shadowSprite.animationTime = plantSprite.animationTime;
                    }
                }
            }

            ApplyGlow(stage);

            if (reachedFinalStage)
            {
                ResetShineTimer();
            }

            if (!playChangeEffects)
            {
                return;
            }

            if (plantSprite != null)
            {
                plantSprite.PlayTransformAnimation(GrowAnimation);
            }

            if (shadowSprite != null)
            {
                shadowSprite.PlayTransformAnimation(GrowAnimation);
            }

            if (reachedFinalStage)
            {
                PlayRipeEffects();
            }
        }

        /// <summary>
        /// Plays one animation on the plant and on its shadow together.
        /// </summary>
        /// <remarks>
        /// Guarded by <c>HasAnimation</c> rather than by anything of ours, because that one call
        /// answers both questions that matter: whether the sprite has an asset at all, and whether
        /// that asset has this animation. A stage whose picture failed to copy is exactly the
        /// second case, and playing it anyway reads a transition off a null asset.
        /// </remarks>
        private void PlayOnBothSprites(int animation, bool skipTransition)
        {
            if (plantSprite != null && plantSprite.HasAnimation(animation))
            {
                plantSprite.PlayAnimation(animation, false, skipTransition);
            }

            if (shadowSprite != null && shadowSprite.HasAnimation(animation))
            {
                shadowSprite.PlayAnimation(animation, false, skipTransition);
            }
        }

        /// <summary>
        /// Switches the plant's own light and the light it throws on the ground.
        /// </summary>
        /// <remarks>
        /// The material swap is what makes a glowing plant readable in a dark cave: the lit
        /// material multiplies the picture by whatever light reaches the tile, so a plant that is
        /// meant to be its own light source goes black in exactly the dark it should be brightest
        /// in. Vanilla's glow tulip does the same swap for the same reason.
        /// </remarks>
        private void ApplyGlow(int stage)
        {
            bool on = currentLook.Glows && stage >= currentLook.FirstGlowingStage;

            if (plantSprite != null)
            {
                plantSprite.emissiveColor = on ? currentLook.GlowColour : Color.black;

                // The material is chosen by the setting alone, never by whether the plant happens
                // to be glowing at this stage. A crop drawn at full brightness with no light of its
                // own is a perfectly ordinary thing to want — a pale fungus, a crystal — and tying
                // the two together would make that tickbox do nothing at all.
                Material wanted = currentLook.IgnoresTorchlight ? unlitMaterial : litMaterial;
                if (wanted != null && plantSprite.material != wanted)
                {
                    plantSprite.material = wanted;
                }

                plantSprite.ApplyVisualChange();
            }

            if (glowSprite == null)
            {
                return;
            }

            bool lightsGround = on && currentLook.LightsTheGround;
            glowSprite.emissiveColor = lightsGround ? currentLook.GroundGlowColour : Color.black;
            glowSprite.gameObject.SetActive(lightsGround);
        }

        private void PlayRipeEffects()
        {
            if (currentLook.RipePuff >= 0)
            {
                Manager.effects.PlayPuff((PuffID)currentLook.RipePuff, transform.position, 4, false, 0);
            }

            if (currentLook.RipeSound != 0)
            {
                AudioManager.SfxFollowTransform(currentLook.RipeSound, transform);
            }
        }

        private void ResetShineTimer()
        {
            nextShineTime = Time.time + Random.Range(ShortestShineGap, LongestShineGap);
        }
    }
}
