using Pug.Sprite;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimensions API/Portal Visual Profile")]
    public sealed class DimensionPortalVisualProfileAsset : ScriptableObject
    {
        private static readonly Sprite[] EmptySpriteArray = new Sprite[0];

        [SerializeField, HideInInspector]
        private int portalArtworkDefaultsVersion;

        public static readonly Color VanillaLoadPointEmissiveColor =
            new Color(0.0f, 2.568409f, 3.7735853f, 1.0f);
        public static readonly Color VanillaPortalShaderEmissiveColor =
            new Color(0.47641504f, 0.7775954f, 1.0f, 1.0f);
        public static readonly Color VanillaCenterTint =
            new Color(0.6273585f, 0.9010171f, 1.0f, 1.0f);
        public static readonly Color VanillaCenterEmissiveColor =
            new Color(8.884704f, 11.821176f, 14.381177f, 1.0f);
        public const float VanillaCenterGlowIntensity = 0.4f;
        public static readonly Color VanillaGroundLightColor =
            new Color(0.28627455f, 0.8524765f, 1.0f, 1.0f);
        public static readonly Color VanillaPaletteDark =
            new Color(20.0f / 255.0f, 43.0f / 255.0f, 92.0f / 255.0f, 1.0f);
        public static readonly Color VanillaPaletteDeep =
            new Color(20.0f / 255.0f, 62.0f / 255.0f, 171.0f / 255.0f, 1.0f);
        public static readonly Color VanillaPaletteMid =
            new Color(22.0f / 255.0f, 93.0f / 255.0f, 217.0f / 255.0f, 1.0f);
        public static readonly Color VanillaPaletteBright =
            new Color(24.0f / 255.0f, 133.0f / 255.0f, 216.0f / 255.0f, 1.0f);
        public static readonly Color VanillaPaletteCore =
            new Color(25.0f / 255.0f, 189.0f / 255.0f, 198.0f / 255.0f, 1.0f);
        public static readonly Color VanillaCenterPaletteDeep =
            new Color(29.0f / 255.0f, 48.0f / 255.0f, 137.0f / 255.0f, 1.0f);

        [Tooltip("Optional static SpriteAsset data-block reference for the portal frame. Leave empty for the vanilla portal frame.")]
        [SerializeField] private DataBlockRef<SpriteAsset> portalFrameSpriteAsset;
        [SerializeField] private Color frameTint = Color.white;
        [ColorUsage(true, true)]
        [SerializeField] private Color frameEmissiveColor =
            new Color(0.47641504f, 0.7775954f, 1.0f, 1.0f);
        [SerializeField] private bool frameVisible = true;
        [Tooltip("Screen-space position relative to the vanilla portal frame, in source pixels.")]
        [SerializeField] private Vector2 frameOffsetPixels = Vector2.zero;
        [SerializeField] private Vector2 frameScale = Vector2.one;
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float frameRotationDegrees;
        [SerializeField] private bool frameFlipX;
        [SerializeField] private bool frameFlipY;

        [Tooltip("Optional looping SpriteAsset data-block reference for the continuous bottom-to-top charging layer. Animation index 0 is drawn as an independent overlay, so its native frame size, pivot, and Studio layout are preserved.")]
        [SerializeField] private DataBlockRef<SpriteAsset> chargeWaveSpriteAsset;
        [SerializeField] private Color chargeWaveDarkColor =
            new Color(20.0f / 255.0f, 43.0f / 255.0f, 92.0f / 255.0f, 1.0f);
        [SerializeField] private Color chargeWaveDeepColor =
            new Color(20.0f / 255.0f, 62.0f / 255.0f, 171.0f / 255.0f, 1.0f);
        [SerializeField] private Color chargeWaveMidColor =
            new Color(22.0f / 255.0f, 93.0f / 255.0f, 217.0f / 255.0f, 1.0f);
        [SerializeField] private Color chargeWaveBrightColor =
            new Color(24.0f / 255.0f, 133.0f / 255.0f, 216.0f / 255.0f, 1.0f);
        [SerializeField] private Color chargeWaveCoreColor =
            new Color(25.0f / 255.0f, 189.0f / 255.0f, 198.0f / 255.0f, 1.0f);
        [SerializeField] private Color chargeWaveTint = Color.white;
        [ColorUsage(true, true)]
        [SerializeField] private Color chargeWaveEmissiveColor =
            new Color(0.47641504f, 0.7775954f, 1.0f, 1.0f);
        [Min(0.01f)]
        [SerializeField] private float chargeWaveSpeed = 1.0f;
        [SerializeField] private bool chargeWaveVisible = true;
        [Tooltip("Screen-space position relative to the vanilla charging sweep, in source pixels.")]
        [SerializeField] private Vector2 chargeWaveOffsetPixels = Vector2.zero;
        [SerializeField] private Vector2 chargeWaveScale = Vector2.one;
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float chargeWaveRotationDegrees;
        [SerializeField] private bool chargeWaveFlipX;
        [SerializeField] private bool chargeWaveFlipY;

        [Tooltip("Optional SpriteAsset data-block reference containing the persistent milestone states. Animation index 0 is used.")]
        [SerializeField] private DataBlockRef<SpriteAsset> milestoneSpriteAsset;
        [SerializeField] private Color milestoneDarkColor =
            new Color(20.0f / 255.0f, 43.0f / 255.0f, 92.0f / 255.0f, 1.0f);
        [SerializeField] private Color milestoneDeepColor =
            new Color(20.0f / 255.0f, 62.0f / 255.0f, 171.0f / 255.0f, 1.0f);
        [SerializeField] private Color milestoneMidColor =
            new Color(22.0f / 255.0f, 93.0f / 255.0f, 217.0f / 255.0f, 1.0f);
        [SerializeField] private Color milestoneBrightColor =
            new Color(24.0f / 255.0f, 133.0f / 255.0f, 216.0f / 255.0f, 1.0f);
        [SerializeField] private Color milestoneCoreColor =
            new Color(25.0f / 255.0f, 189.0f / 255.0f, 198.0f / 255.0f, 1.0f);
        [SerializeField] private Color milestoneTint = Color.white;
        [ColorUsage(true, true)]
        [SerializeField] private Color milestoneEmissiveColor =
            new Color(0.0f, 2.568409f, 3.7735853f, 1.0f);
        [Range(0.0f, 1.0f)]
        [SerializeField] private float firstMilestone = 0.25f;
        [Range(0.0f, 1.0f)]
        [SerializeField] private float secondMilestone = 0.5f;
        [Range(0.0f, 1.0f)]
        [SerializeField] private float thirdMilestone = 0.75f;
        [Min(0)]
        [SerializeField] private int milestoneEmptyFrame;
        [Min(0)]
        [SerializeField] private int milestoneFirstFrame = 1;
        [Min(0)]
        [SerializeField] private int milestoneSecondFrame = 3;
        [Min(0)]
        [SerializeField] private int milestoneThirdFrame = 4;
        [Min(0)]
        [SerializeField] private int milestoneReadyFrame = 7;
        [SerializeField] private bool milestonesVisible = true;
        [Tooltip("Screen-space position relative to the vanilla milestone artwork, in source pixels.")]
        [SerializeField] private Vector2 milestoneOffsetPixels = Vector2.zero;
        [SerializeField] private Vector2 milestoneScale = Vector2.one;
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float milestoneRotationDegrees;
        [SerializeField] private bool milestoneFlipX;
        [SerializeField] private bool milestoneFlipY;

        [Tooltip("Optional SpriteAsset data-block reference for the activated center. Animation 0 should loop; animation 1 should open once and exit to animation 0.")]
        [SerializeField] private DataBlockRef<SpriteAsset> centerEffectSpriteAsset;
        [SerializeField] private Color centerDarkColor =
            new Color(20.0f / 255.0f, 43.0f / 255.0f, 92.0f / 255.0f, 1.0f);
        [SerializeField] private Color centerDeepColor =
            new Color(29.0f / 255.0f, 48.0f / 255.0f, 137.0f / 255.0f, 1.0f);
        [SerializeField] private Color centerMidColor =
            new Color(20.0f / 255.0f, 62.0f / 255.0f, 171.0f / 255.0f, 1.0f);
        [SerializeField] private Color centerBrightColor =
            new Color(22.0f / 255.0f, 93.0f / 255.0f, 217.0f / 255.0f, 1.0f);
        [SerializeField] private Color centerCoreColor =
            new Color(25.0f / 255.0f, 189.0f / 255.0f, 198.0f / 255.0f, 1.0f);
        [SerializeField] private Color centerHighlightColor = Color.white;
        [SerializeField] private Color centerTint =
            new Color(0.6273585f, 0.9010171f, 1.0f, 1.0f);
        [ColorUsage(true, true)]
        [SerializeField] private Color centerEmissiveColor =
            new Color(8.884704f, 11.821176f, 14.381177f, 1.0f);
        [Tooltip("Optional Core Keeper screen-space alignment offset for the activated center, in source pixels. X follows world X; Y follows the game's projected (world Y + world Z) axis. Leave at zero for the vanilla alignment.")]
        [SerializeField] private Vector2 centerOffsetPixels = Vector2.zero;
        [SerializeField] private bool centerVisible = true;
        [SerializeField] private Vector2 centerScale = Vector2.one;
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float centerRotationDegrees;
        [SerializeField] private bool centerFlipX;
        [SerializeField] private bool centerFlipY;
        [Range(0.0f, 1.0f)]
        [Tooltip("Scales the activated center's emissive bloom. The default compensates for the SpriteObject shader so the white inner highlight stays close to vanilla brightness.")]
        [SerializeField] private float centerGlowIntensity =
            VanillaCenterGlowIntensity;
        [SerializeField] private bool playReadyFlash = true;

        [SerializeField] private bool centerParticlesEnabled = true;
        [Tooltip("Automatically derive the moving GatherEnergy flecks from the activated center palette. The exact vanilla particle gradient is preserved while the center palette is vanilla.")]
        [SerializeField] private bool centerParticlesFollowCenterPalette = true;
        [Tooltip("Color of the persistent animated GatherEnergy flecks inside the portal. White preserves the vanilla blue/cyan gradient.")]
        [SerializeField] private Color centerParticleTint = Color.white;
        [Tooltip("Optional animated SpriteAsset used by the custom inner swirl. Animation 0 must contain at least one frame and loop.")]
        [SerializeField] private DataBlockRef<SpriteAsset> centerSwirlSpriteAsset;
        [Tooltip("Replace the vanilla GatherEnergy particle effect with the animated custom swirl SpriteAsset.")]
        [SerializeField] private bool centerSwirlOverrideVanilla;
        [Tooltip("Show the persistent inner swirl in both vanilla and custom modes.")]
        [SerializeField] private bool centerSwirlVisible = true;
        [Min(0.01f)]
        [Tooltip("Playback speed multiplier for animation 0 of the custom inner swirl.")]
        [SerializeField] private float centerSwirlPlaybackSpeed = 1.0f;
        [ColorUsage(true, true)]
        [Tooltip("HDR emission applied to the custom swirl SpriteObject.")]
        [SerializeField] private Color centerSwirlEmissiveColor = Color.white;
        [SerializeField] private bool centerSwirlFlipX;
        [SerializeField] private bool centerSwirlFlipY;
        [Tooltip("Optional Sprite emitted by the persistent inner flecks. Leave empty to preserve the exact vanilla wcircle Sprite.")]
        [SerializeField] private Sprite centerParticleSprite;
        [Tooltip("Optional texture used by the persistent inner flecks. The generator keeps the vanilla particle shaders and only replaces their main texture.")]
        [SerializeField] private Texture2D centerParticleTexture;
        [Tooltip("Automatically derive the one-shot DeathBlink ready burst from the activated center palette. The exact vanilla particle gradient is preserved while the center palette is vanilla.")]
        [SerializeField] private bool readyFlashFollowsCenterPalette = true;
        [Tooltip("Color of the one-shot DeathBlink effect when the portal becomes ready. White preserves the vanilla blue/cyan gradient.")]
        [SerializeField] private Color readyFlashTint = Color.white;
        [Min(0.0f)]
        [SerializeField] private float centerParticleEmissionMultiplier = 1.0f;
        [Min(0.0f)]
        [SerializeField] private float centerParticleSizeMultiplier = 1.0f;
        [Min(0.0f)]
        [SerializeField] private float centerParticleLifetimeMultiplier = 1.0f;
        [Range(-8.0f, 8.0f)]
        [Tooltip("Scales the vanilla orbital velocity. Negative values reverse the swirl direction.")]
        [SerializeField] private float centerParticleOrbitSpeedMultiplier = 1.0f;
        [Range(-8.0f, 8.0f)]
        [Tooltip("Scales the vanilla inward radial velocity. Negative values turn it into outward drift.")]
        [SerializeField] private float centerParticleRadialSpeedMultiplier = 1.0f;
        [Min(0.0f)]
        [SerializeField] private float centerParticleRadiusMultiplier = 1.0f;
        [SerializeField] private bool centerParticleTrailsEnabled = true;
        [Min(0.0f)]
        [SerializeField] private float centerParticleTrailLifetimeMultiplier = 1.0f;
        [Tooltip("Screen-space position relative to the vanilla inner energy effect, in source pixels.")]
        [SerializeField] private Vector2 centerParticleOffsetPixels = Vector2.zero;
        [SerializeField] private Vector2 centerParticleScale = Vector2.one;
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float centerParticleRotationDegrees;
        [Tooltip("Optional texture used by the one-shot ready burst. The generator keeps the vanilla particle shaders and only replaces their main texture.")]
        [SerializeField] private Texture2D readyFlashTexture;
        [Tooltip("Optional ordered Sprite sequence used by the one-shot ready burst. Leave empty to preserve the exact seven-frame vanilla flash sequence. All custom frames must share one backing texture.")]
        [SerializeField] private Sprite[] readyFlashSprites = new Sprite[0];
        [Min(0.0f)]
        [SerializeField] private float readyFlashEmissionMultiplier = 1.0f;
        [Min(0.0f)]
        [SerializeField] private float readyFlashSizeMultiplier = 1.0f;
        [Tooltip("Screen-space position relative to the vanilla ready burst, in source pixels.")]
        [SerializeField] private Vector2 readyFlashOffsetPixels = Vector2.zero;
        [SerializeField] private Vector2 readyFlashScale = Vector2.one;
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float readyFlashRotationDegrees;

        [SerializeField] private bool groundLightEnabled = true;
        [SerializeField] private Color groundLightColor =
            new Color(0.28627455f, 0.8524765f, 1.0f, 1.0f);
        [Min(0.0f)]
        [Tooltip("Authored Unity Light intensity. The runtime brightness range below is applied by the vanilla LightFlickerEffect.")]
        [SerializeField] private float groundLightIntensity = 0.65f;
        [Min(0.01f)]
        [SerializeField] private float groundLightRange = 5.0f;
        [Min(0.0f)]
        [SerializeField] private float groundLightMinimumIntensity = 0.3f;
        [Min(0.0f)]
        [SerializeField] private float groundLightMaximumIntensity = 0.3f;
        [SerializeField] private bool groundLightMovement = true;
        [SerializeField] private bool groundLightCastsShadows = true;
        [Tooltip("Screen-space position relative to the vanilla projected portal light, in source pixels.")]
        [SerializeField] private Vector2 groundLightOffsetPixels = Vector2.zero;

        [SerializeField] private bool portalShadowEnabled = true;
        [Tooltip("Optional consumer-owned floor/contact shadow sprite. The vanilla floor-shadow material and shader remain in use.")]
        [SerializeField] private Sprite portalShadowSprite;
        [Tooltip("Optional consumer-owned responsive shadow-caster sprite. The vanilla shadow-caster material and shader remain in use.")]
        [SerializeField] private Sprite portalShadowCasterSprite;
        [Tooltip("Screen-space position relative to the vanilla contact and responsive portal shadows, in source pixels.")]
        [SerializeField] private Vector2 portalShadowOffsetPixels = Vector2.zero;
        [SerializeField] private Vector2 portalShadowScale = Vector2.one;
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float portalShadowRotationDegrees;
        [SerializeField] private bool portalShadowFlipX;
        [SerializeField] private bool portalShadowFlipY;

        public DataBlockRef<SpriteAsset> PortalFrameSpriteAsset { get { return portalFrameSpriteAsset; } }
        public Color FrameTint { get { return frameTint; } }
        public Color FrameEmissiveColor { get { return frameEmissiveColor; } }
        public bool FrameVisible { get { return frameVisible; } }
        public Vector2 FrameOffsetPixels { get { return frameOffsetPixels; } }
        public Vector2 FrameScale { get { return ClampLayerScale(frameScale); } }
        public float FrameRotationDegrees { get { return NormalizeRotation(frameRotationDegrees); } }
        public bool FrameFlipX { get { return frameFlipX; } }
        public bool FrameFlipY { get { return frameFlipY; } }
        public DataBlockRef<SpriteAsset> ChargeWaveSpriteAsset { get { return chargeWaveSpriteAsset; } }
        public Color ChargeWaveDarkColor { get { return chargeWaveDarkColor; } }
        public Color ChargeWaveDeepColor { get { return chargeWaveDeepColor; } }
        public Color ChargeWaveMidColor { get { return chargeWaveMidColor; } }
        public Color ChargeWaveBrightColor { get { return chargeWaveBrightColor; } }
        public Color ChargeWaveCoreColor { get { return chargeWaveCoreColor; } }
        public Color ChargeWaveTint { get { return chargeWaveTint; } }
        public Color ChargeWaveEmissiveColor { get { return chargeWaveEmissiveColor; } }
        public float ChargeWaveSpeed { get { return Mathf.Max(0.01f, chargeWaveSpeed); } }
        public bool ChargeWaveVisible { get { return chargeWaveVisible; } }
        public Vector2 ChargeWaveOffsetPixels { get { return chargeWaveOffsetPixels; } }
        public Vector2 ChargeWaveScale { get { return ClampLayerScale(chargeWaveScale); } }
        public float ChargeWaveRotationDegrees
        {
            get { return NormalizeRotation(chargeWaveRotationDegrees); }
        }
        public bool ChargeWaveFlipX { get { return chargeWaveFlipX; } }
        public bool ChargeWaveFlipY { get { return chargeWaveFlipY; } }
        public DataBlockRef<SpriteAsset> MilestoneSpriteAsset { get { return milestoneSpriteAsset; } }
        public Color MilestoneDarkColor { get { return milestoneDarkColor; } }
        public Color MilestoneDeepColor { get { return milestoneDeepColor; } }
        public Color MilestoneMidColor { get { return milestoneMidColor; } }
        public Color MilestoneBrightColor { get { return milestoneBrightColor; } }
        public Color MilestoneCoreColor { get { return milestoneCoreColor; } }
        public Color MilestoneTint { get { return milestoneTint; } }
        public Color MilestoneEmissiveColor { get { return milestoneEmissiveColor; } }
        public float FirstMilestone { get { return Mathf.Clamp01(firstMilestone); } }
        public float SecondMilestone
        {
            get { return Mathf.Clamp(secondMilestone, FirstMilestone, 1.0f); }
        }
        public float ThirdMilestone
        {
            get { return Mathf.Clamp(thirdMilestone, SecondMilestone, 1.0f); }
        }
        public int MilestoneEmptyFrame { get { return Mathf.Max(0, milestoneEmptyFrame); } }
        public int MilestoneFirstFrame { get { return Mathf.Max(0, milestoneFirstFrame); } }
        public int MilestoneSecondFrame { get { return Mathf.Max(0, milestoneSecondFrame); } }
        public int MilestoneThirdFrame { get { return Mathf.Max(0, milestoneThirdFrame); } }
        public int MilestoneReadyFrame { get { return Mathf.Max(0, milestoneReadyFrame); } }
        public bool MilestonesVisible { get { return milestonesVisible; } }
        public Vector2 MilestoneOffsetPixels { get { return milestoneOffsetPixels; } }
        public Vector2 MilestoneScale { get { return ClampLayerScale(milestoneScale); } }
        public float MilestoneRotationDegrees
        {
            get { return NormalizeRotation(milestoneRotationDegrees); }
        }
        public bool MilestoneFlipX { get { return milestoneFlipX; } }
        public bool MilestoneFlipY { get { return milestoneFlipY; } }
        public DataBlockRef<SpriteAsset> CenterEffectSpriteAsset { get { return centerEffectSpriteAsset; } }
        public Color CenterDarkColor { get { return centerDarkColor; } }
        public Color CenterDeepColor { get { return centerDeepColor; } }
        public Color CenterMidColor { get { return centerMidColor; } }
        public Color CenterBrightColor { get { return centerBrightColor; } }
        public Color CenterCoreColor { get { return centerCoreColor; } }
        public Color CenterHighlightColor { get { return centerHighlightColor; } }
        public Color CenterTint { get { return centerTint; } }
        public Color CenterEmissiveColor { get { return centerEmissiveColor; } }
        public Vector2 CenterOffsetPixels { get { return centerOffsetPixels; } }
        public bool CenterVisible { get { return centerVisible; } }
        public Vector2 CenterScale { get { return ClampLayerScale(centerScale); } }
        public float CenterRotationDegrees { get { return NormalizeRotation(centerRotationDegrees); } }
        public bool CenterFlipX { get { return centerFlipX; } }
        public bool CenterFlipY { get { return centerFlipY; } }
        public float CenterGlowIntensity
        {
            get { return Mathf.Clamp01(centerGlowIntensity); }
        }
        public int CenterIdleAnimationIndex { get { return 0; } }
        public int CenterOpeningAnimationIndex { get { return 1; } }
        public bool PlayReadyFlash { get { return playReadyFlash; } }
        public bool CenterParticlesEnabled { get { return centerParticlesEnabled; } }
        public bool CenterParticlesFollowCenterPalette
        {
            get { return centerParticlesFollowCenterPalette; }
        }
        public Color CenterParticleTint { get { return centerParticleTint; } }
        public DataBlockRef<SpriteAsset> CenterSwirlSpriteAsset
        {
            get { return centerSwirlSpriteAsset; }
        }
        public bool CenterSwirlOverrideVanilla { get { return centerSwirlOverrideVanilla; } }
        public bool CenterSwirlVisible { get { return centerSwirlVisible; } }
        public float CenterSwirlPlaybackSpeed
        {
            get { return Mathf.Max(0.01f, centerSwirlPlaybackSpeed); }
        }
        public Color CenterSwirlEmissiveColor { get { return centerSwirlEmissiveColor; } }
        public bool CenterSwirlFlipX { get { return centerSwirlFlipX; } }
        public bool CenterSwirlFlipY { get { return centerSwirlFlipY; } }
        public Sprite CenterParticleSprite { get { return centerParticleSprite; } }
        public Texture2D CenterParticleTexture { get { return centerParticleTexture; } }
        public bool ReadyFlashFollowsCenterPalette
        {
            get { return readyFlashFollowsCenterPalette; }
        }
        public Color ReadyFlashTint { get { return readyFlashTint; } }
        public float CenterParticleEmissionMultiplier
        {
            get { return Mathf.Max(0.0f, centerParticleEmissionMultiplier); }
        }
        public float CenterParticleSizeMultiplier
        {
            get { return Mathf.Max(0.0f, centerParticleSizeMultiplier); }
        }
        public float CenterParticleLifetimeMultiplier
        {
            get { return Mathf.Max(0.0f, centerParticleLifetimeMultiplier); }
        }
        public float CenterParticleOrbitSpeedMultiplier
        {
            get { return Mathf.Clamp(centerParticleOrbitSpeedMultiplier, -8.0f, 8.0f); }
        }
        public float CenterParticleRadialSpeedMultiplier
        {
            get { return Mathf.Clamp(centerParticleRadialSpeedMultiplier, -8.0f, 8.0f); }
        }
        public float CenterParticleRadiusMultiplier
        {
            get { return Mathf.Max(0.0f, centerParticleRadiusMultiplier); }
        }
        public bool CenterParticleTrailsEnabled { get { return centerParticleTrailsEnabled; } }
        public float CenterParticleTrailLifetimeMultiplier
        {
            get { return Mathf.Max(0.0f, centerParticleTrailLifetimeMultiplier); }
        }
        public Vector2 CenterParticleOffsetPixels { get { return centerParticleOffsetPixels; } }
        public Vector2 CenterParticleScale { get { return ClampLayerScale(centerParticleScale); } }
        public float CenterParticleRotationDegrees
        {
            get { return NormalizeRotation(centerParticleRotationDegrees); }
        }
        public Texture2D ReadyFlashTexture { get { return readyFlashTexture; } }
        public Sprite[] ReadyFlashSprites
        {
            get { return readyFlashSprites ?? EmptySpriteArray; }
        }
        public float ReadyFlashEmissionMultiplier
        {
            get { return Mathf.Max(0.0f, readyFlashEmissionMultiplier); }
        }
        public float ReadyFlashSizeMultiplier
        {
            get { return Mathf.Max(0.0f, readyFlashSizeMultiplier); }
        }
        public Vector2 ReadyFlashOffsetPixels { get { return readyFlashOffsetPixels; } }
        public Vector2 ReadyFlashScale { get { return ClampLayerScale(readyFlashScale); } }
        public float ReadyFlashRotationDegrees
        {
            get { return NormalizeRotation(readyFlashRotationDegrees); }
        }
        public bool GroundLightEnabled { get { return groundLightEnabled; } }
        public Color GroundLightColor { get { return groundLightColor; } }
        public float GroundLightIntensity { get { return Mathf.Max(0.0f, groundLightIntensity); } }
        public float GroundLightRange { get { return Mathf.Max(0.01f, groundLightRange); } }
        public float GroundLightMinimumIntensity
        {
            get { return Mathf.Max(0.0f, groundLightMinimumIntensity); }
        }
        public float GroundLightMaximumIntensity
        {
            get { return Mathf.Max(GroundLightMinimumIntensity, groundLightMaximumIntensity); }
        }
        public bool GroundLightMovement { get { return groundLightMovement; } }
        public bool GroundLightCastsShadows { get { return groundLightCastsShadows; } }
        public Vector2 GroundLightOffsetPixels { get { return groundLightOffsetPixels; } }
        public bool PortalShadowEnabled { get { return portalShadowEnabled; } }
        public Sprite PortalShadowSprite { get { return portalShadowSprite; } }
        public Sprite PortalShadowCasterSprite { get { return portalShadowCasterSprite; } }
        public Vector2 PortalShadowOffsetPixels { get { return portalShadowOffsetPixels; } }
        public Vector2 PortalShadowScale { get { return ClampLayerScale(portalShadowScale); } }
        public float PortalShadowRotationDegrees
        {
            get { return NormalizeRotation(portalShadowRotationDegrees); }
        }
        public bool PortalShadowFlipX { get { return portalShadowFlipX; } }
        public bool PortalShadowFlipY { get { return portalShadowFlipY; } }

        private static Vector2 ClampLayerScale(Vector2 value)
        {
            return new Vector2(
                Mathf.Clamp(Mathf.Abs(value.x), 0.05f, 8.0f),
                Mathf.Clamp(Mathf.Abs(value.y), 0.05f, 8.0f));
        }

        private static float NormalizeRotation(float value)
        {
            return Mathf.Repeat(value + 180.0f, 360.0f) - 180.0f;
        }
    }
}
