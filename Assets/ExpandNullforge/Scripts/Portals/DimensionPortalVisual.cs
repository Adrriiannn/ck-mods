using Pug.Sprite;
using Pug.RP;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  [AddComponentMenu("Dimension Framework/Dimension Portal Visual")]
  public sealed partial class DimensionPortalVisual : MonoBehaviour
  {
    private const int ReadyObjectDataAmount = 600;
    private const string VanillaFloorShadowShaderName = "Amplify/FloorShadow";
    private const string VanillaShadowCasterShaderName = "Amplify/ShadowCastingSprite";
    private static Shader vanillaFloorShadowShader;
    private static Shader vanillaShadowCasterShader;
    private static Shader vanillaPortalShader;
    private static readonly int LoadingMulProperty = Shader.PropertyToID("_loadingMul");
    private static readonly int LoadingSpeedProperty = Shader.PropertyToID("_loadingSpeed");
    private static readonly int EmissiveStrengthMulProperty =
        Shader.PropertyToID("_emissiveStrengthMul");
    private static readonly int EmissiveColorProperty = Shader.PropertyToID("_emissiveColor");
    private static readonly int MainColorProperty = Shader.PropertyToID("_MainColor");

    [Header("SpriteObject Layers")]
    [SerializeField]
    private SpriteObject portalBody;

    [SerializeField]
    private SpriteRenderer portalBodyRenderer;

    [SerializeField]
    private SpriteObject portalChargeProgress;

    [SerializeField]
    private SpriteObject portalEmissiveWave;

    [SerializeField]
    private SpriteObject portalCenterEffect;

    [SerializeField]
    private SpriteObject portalCustomSwirl;

    [SerializeField]
    private GameObject centerParticlesRoot;

    [SerializeField]
    private GameObject readyFlashRoot;

    [SerializeField]
    private SpriteObject portalOutlineMask;

    [SerializeField]
    private SpriteObject portalOutlineSupportMask;

    [SerializeField]
    private SpriteObject portalOutlineCap;

    [SerializeField]
    private Renderer portalShadow;

    [SerializeField]
    private Renderer portalShadowCaster;

    [Header("Layer Visibility")]
    [SerializeField]
    private bool portalBodyVisible = true;

    [SerializeField]
    private bool chargeWaveVisible = true;

    [SerializeField]
    private bool milestoneVisible = true;

    [SerializeField]
    private bool centerVisible = true;

    [SerializeField]
    private bool centerParticlesVisible = true;

    [SerializeField]
    private bool projectedShadowVisible = true;

    // Baked true on the dedicated instant item-portal (V2) visual prefab: the frame, charge wave,
    // milestones, outlines and ground shadow stay hidden, leaving the inner circle, swirls and
    // light. Baked (not switched at runtime) so the frameless look never waits on replicated state,
    // and safe under pooling because visual pools are per prefab.
    [SerializeField]
    private bool itemPortalMode;

    [Header("Resolved Portal Style")]
    [SerializeField]
    private Color portalBodyColor = Color.white;

    [ColorUsage(true, true)]
    [SerializeField]
    private Color portalBodyEmissiveColor =
        new Color(0.47641504f, 0.7775954f, 1.0f, 1.0f);

    [SerializeField]
    private Color chargeWaveColor = Color.white;

    [ColorUsage(true, true)]
    [SerializeField]
    private Color chargeWaveEmissiveColor =
        new Color(0.47641504f, 0.7775954f, 1.0f, 1.0f);

    [Min(0.01f)]
    [SerializeField]
    private float chargeWaveSpeed = 1.0f;

    [SerializeField]
    private Color milestoneColor = Color.white;

    [ColorUsage(true, true)]
    [SerializeField]
    private Color milestoneEmissiveColor =
        new Color(0.0f, 2.568409f, 3.7735853f, 1.0f);

    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float firstMilestone = 0.25f;

    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float secondMilestone = 0.5f;

    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float thirdMilestone = 0.75f;

    // The milestone stage -> sheet frame mapping is fixed to the vanilla sheet order
    // (empty 0, bottom 1, middle 3, upper 4, ready 7). Custom milestone sheets follow the
    // same frame order; only the activation THRESHOLDS are authorable.
    private const int MilestoneEmptyFrame = 0;
    private const int MilestoneFirstFrame = 1;
    private const int MilestoneSecondFrame = 3;
    private const int MilestoneThirdFrame = 4;
    private const int MilestoneReadyFrame = 7;

    [SerializeField]
    private Color centerColor =
        new Color(0.6273585f, 0.9010171f, 1.0f, 1.0f);

    [ColorUsage(true, true)]
    [SerializeField]
    private Color centerEmissiveColor =
        new Color(8.884704f, 11.821176f, 14.381177f, 1.0f);

    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float centerGlowIntensity = 0.4f;

    [SerializeField]
    private int centerIdleAnimationIndex;

    [SerializeField]
    private int centerOpeningAnimationIndex = 1;

    // Animation index of the one-shot closing sequence (the opening in reverse). -1 disables
    // closing entirely; the generator bakes 2 on the instant item-portal prefab, whose center
    // SpriteAsset carries idle/opening/closing animations.
    [SerializeField]
    private int centerClosingAnimationIndex = -1;

    [SerializeField]
    private bool playReadyFlash = true;

    [Min(0.01f)]
    [SerializeField]
    private float customSwirlPlaybackSpeed = 1.0f;

    private bool staticStateApplied;
    private bool vanillaShadowShadersApplied;
    private bool wasActivated;
    private int lastProgressFrame = -1;
    private bool chargeWaveStarted;
    private bool centerIdleStarted;
    private bool centerOpeningFallbackPending;
    private bool centerOpeningObserved;
    private int centerOpeningAnimationHash;
    private float centerOpeningFallbackAt = -1.0f;
    private bool centerClosingPending;
    private float centerClosingEndsAt = -1.0f;
    private bool outlineCapVisible;
    private Color lastOutlineCapColor = Color.clear;
    private ParticleSystem[] centerParticleSystems;
    private ParticleSystem[] readyFlashParticleSystems;
    private MaterialPropertyBlock portalBodyPropertyBlock;
    private bool portalBodySpriteStateApplied;
    private bool portalBodySpriteActivated;
    private bool portalBodyRendererStateApplied;
    private bool portalBodyRendererActivated;

    private void Awake()
    {
      ApplyStaticState();
    }

    private void OnEnable()
    {
      ApplyStaticState();
      ResetDynamicState();
      RefreshProjectedShadow();
    }

    private void OnDisable()
    {
      ResetDynamicState();
    }

    private void LateUpdate()
    {
      EnsureCenterIdleAfterOpening();
      FinishCenterClosingIfDue();
      ApplyOutlineCapState();
      UpdateReadyFlashState();
    }

    public void ApplyPortalState(
        int amount,
        bool portalActive,
        bool activated,
        bool becameActivated,
        bool force)
    {
      ApplyStaticState();

      amount = Mathf.Clamp(amount, 0, ReadyObjectDataAmount);
      float progress = Mathf.Clamp01((float)amount / ReadyObjectDataAmount);

      ApplyChargeProgress(progress, portalActive, activated, force);
      ApplyCenterEffect(activated, becameActivated, force);

      wasActivated = activated;
    }

    public void RefreshProjectedShadow()
    {
      MarkShadowAreaDirty();
    }

    /// <summary>
    /// True while this instance renders an instant item portal (V2) — baked on the dedicated
    /// frameless prefab and switched per entity at view-bind time, because pooled portal
    /// visuals can serve any portal entity.
    /// </summary>
    public bool IsItemPortalMode
    {
      get { return itemPortalMode; }
    }

    // The prefab-baked layer configuration, captured before the first runtime mode switch so a
    // pooled instance can serve an instant portal and later a framed portal (or vice versa)
    // without permanently losing its authored visibility.
    private bool bakedDefaultsCaptured;
    private bool bakedBodyVisible;
    private bool bakedWaveVisible;
    private bool bakedMilestoneVisible;
    private bool bakedShadowVisible;
    private bool bakedReadyFlash;
    private int bakedClosingIndex;

    private void CaptureBakedDefaults()
    {
      if (bakedDefaultsCaptured)
      {
        return;
      }

      bakedDefaultsCaptured = true;
      bakedBodyVisible = portalBodyVisible;
      bakedWaveVisible = chargeWaveVisible;
      bakedMilestoneVisible = milestoneVisible;
      bakedShadowVisible = projectedShadowVisible;
      bakedReadyFlash = playReadyFlash;
      bakedClosingIndex = centerClosingAnimationIndex;
    }

    /// <summary>
    /// Switches this instance between the frameless instant item-portal look and its
    /// prefab-baked configuration. Pooled portal visuals are shared across portal prefabs, so
    /// the view resolves this per entity at bind time; turning the mode off restores the exact
    /// baked layer visibility.
    /// </summary>
    public void SetItemPortalMode(bool on)
    {
      CaptureBakedDefaults();
      if (itemPortalMode == on)
      {
        return;
      }

      itemPortalMode = on;
      if (on)
      {
        portalBodyVisible = false;
        chargeWaveVisible = false;
        milestoneVisible = false;
        projectedShadowVisible = false;
        playReadyFlash = false;
        if (centerClosingAnimationIndex < 0)
        {
          // Animation 2 is the closing one-shot of the instant center contract; the runtime
          // guards gracefully when the bound center asset has no third animation.
          centerClosingAnimationIndex = 2;
        }
      }
      else
      {
        portalBodyVisible = bakedBodyVisible;
        chargeWaveVisible = bakedWaveVisible;
        milestoneVisible = bakedMilestoneVisible;
        projectedShadowVisible = bakedShadowVisible;
        playReadyFlash = bakedReadyFlash;
        centerClosingAnimationIndex = bakedClosingIndex;
      }

      // Re-run the one-time static layer setup so the new visibility and outline gating apply.
      staticStateApplied = false;
      ApplyStaticState();
    }

    /// <summary>
    /// Plays the portal opening animation and swirl intro once. An item portal spawns already active,
    /// so the normal "just became active" trigger never fires — this drives it explicitly on spawn.
    /// </summary>
    public void PlayItemPortalOpening()
    {
      if (!centerVisible)
      {
        return;
      }

      // A pooled instance may be reused while its previous portal was still mid-close.
      ResetCenterClosing();

      // The one-time static layer setup leaves the center effect inactive until the first charge
      // update; reactivate it here so the opening actually renders instead of playing on a hidden
      // object.
      SetActive(portalCenterEffect, true);
      if (portalCenterEffect != null)
      {
        portalCenterEffect.enabled = true;
      }

      bool openingStarted = TryStartCenterOpening();
      RestartCenterParticles();
      RestartCustomSwirl();
      PlayReadyFlash();
      if (!openingStarted)
      {
        StartCenterIdleAnimation();
      }
    }

    private static bool Approximately(Color a, Color b)
    {
      return Mathf.Abs(a.r - b.r) < 0.001f &&
          Mathf.Abs(a.g - b.g) < 0.001f &&
          Mathf.Abs(a.b - b.b) < 0.001f &&
          Mathf.Abs(a.a - b.a) < 0.001f;
    }

    private static Color MultiplyColors(Color left, Color right)
    {
      return new Color(
          left.r * right.r,
          left.g * right.g,
          left.b * right.b,
          left.a * right.a);
    }

    private static Color ScaleColor(Color color, float scale)
    {
      return new Color(
          color.r * scale,
          color.g * scale,
          color.b * scale,
          color.a);
    }

    private static void SetActive(SpriteObject spriteObject, bool active)
    {
      if (spriteObject != null &&
          spriteObject.gameObject != null &&
          spriteObject.gameObject.activeSelf != active)
      {
        spriteObject.gameObject.SetActive(active);
      }
    }

    private static bool TryPlayAnimation(
        SpriteObject spriteObject,
        int animationIndex,
        bool forceResetTime)
    {
      if (spriteObject == null || spriteObject.asset == null)
      {
        return false;
      }

      SpriteAsset asset = spriteObject.asset;
      if (!asset.hasAnimations ||
          animationIndex < 0 ||
          animationIndex >= asset.animationCount)
      {
        return false;
      }

      int animationHash = asset.GetAnimationHash(animationIndex);
      if (!spriteObject.HasAnimation(animationHash))
      {
        return false;
      }

      spriteObject.PlayAnimation(
          animationHash,
          0,
          forceResetTime: forceResetTime,
          skipTransition: true);
      spriteObject.ApplyVisualChange();
      return true;
    }
  }
}
