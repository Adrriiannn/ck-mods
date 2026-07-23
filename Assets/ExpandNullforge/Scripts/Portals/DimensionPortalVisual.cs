using Pug.Sprite;
using Pug.RP;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  [AddComponentMenu("Dimension Framework/Dimension Portal Visual")]
  public sealed class DimensionPortalVisual : MonoBehaviour
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

    private void ApplyStaticState()
    {
      ApplyVanillaShadowShaders();
      ApplyVanillaPortalShader();
      ApplyPersistentVisibility();
      if (!staticStateApplied)
      {
        bool shaderBodyActive = portalBodyRenderer != null;
        ConfigureStaticLayer(
            portalBody,
            portalBodyVisible && !shaderBodyActive,
            portalBodyColor,
            ScaleColor(
                MultiplyColors(portalBodyEmissiveColor, portalBodyColor),
                5.0f));
        ConfigureStaticLayer(portalOutlineMask, !itemPortalMode, Color.clear, Color.clear);
        ConfigureStaticLayer(portalOutlineSupportMask, !itemPortalMode, Color.clear, Color.clear);
        ConfigureStaticLayer(portalOutlineCap, false, Color.clear, Color.clear);
        ConfigureStaticLayer(
            portalChargeProgress,
            false,
            milestoneColor,
            milestoneEmissiveColor);
        ConfigureStaticLayer(
            portalEmissiveWave,
            false,
            chargeWaveColor,
            ScaleColor(
                MultiplyColors(chargeWaveEmissiveColor, chargeWaveColor),
                3.5f));
        ConfigureStaticLayer(
            portalCenterEffect,
            false,
            centerColor,
            ScaleColor(
                MultiplyColors(centerEmissiveColor, centerColor),
                Mathf.Clamp01(centerGlowIntensity)));
        staticStateApplied = true;
      }
    }

    private void ApplyPersistentVisibility()
    {
      if (portalShadow != null)
      {
        portalShadow.enabled = projectedShadowVisible;
      }

      if (portalShadowCaster != null)
      {
        portalShadowCaster.enabled = projectedShadowVisible;
      }

      if (portalBodyRenderer != null && !portalBodyVisible)
      {
        portalBodyRenderer.enabled = false;
      }
    }

    private void ApplyVanillaPortalShader()
    {
      if (portalBodyRenderer == null)
      {
        return;
      }

      if (vanillaPortalShader == null)
      {
        vanillaPortalShader = Shader.Find("Amplify/Portal");
      }

      Material material = portalBodyRenderer.sharedMaterial;
      if (vanillaPortalShader != null &&
          material != null &&
          material.shader != vanillaPortalShader)
      {
        material.shader = vanillaPortalShader;
      }
    }

    private void ApplyVanillaShadowShaders()
    {
      if (vanillaShadowShadersApplied)
      {
        return;
      }

      bool floorShadowApplied = TryApplyVanillaShadowShader(
          portalShadow,
          VanillaFloorShadowShaderName,
          ref vanillaFloorShadowShader);
      bool shadowCasterApplied = TryApplyVanillaShadowShader(
          portalShadowCaster,
          VanillaShadowCasterShaderName,
          ref vanillaShadowCasterShader);
      vanillaShadowShadersApplied = floorShadowApplied && shadowCasterApplied;
    }

    private static bool TryApplyVanillaShadowShader(
        Renderer renderer,
        string shaderName,
        ref Shader cachedShader)
    {
      if (renderer == null)
      {
        return true;
      }

      if (cachedShader == null)
      {
        cachedShader = Shader.Find(shaderName);
      }

      Material material = renderer.sharedMaterial;
      if (cachedShader == null || material == null)
      {
        return false;
      }

      if (material.shader != cachedShader)
      {
        material.shader = cachedShader;
      }

      return true;
    }

    private void ApplyOutlineCapState()
    {
      if (portalOutlineCap == null)
      {
        return;
      }

      if (itemPortalMode)
      {
        SetActive(portalOutlineCap, false);
        return;
      }

      Color outlineColor = GetCurrentOutlineColor();
      bool visible = outlineColor.a > 0.001f;
      if (!visible)
      {
        outlineColor = Color.clear;
      }

      if (outlineCapVisible == visible && Approximately(lastOutlineCapColor, outlineColor))
      {
        return;
      }

      SetActive(portalOutlineCap, visible);
      portalOutlineCap.color = outlineColor;
      portalOutlineCap.emissiveColor = outlineColor;
      portalOutlineCap.flashColor = Color.clear;
      portalOutlineCap.outlineColor = Color.clear;
      portalOutlineCap.ApplyVisualChange();
      outlineCapVisible = visible;
      lastOutlineCapColor = outlineColor;
    }

    private Color GetCurrentOutlineColor()
    {
      if (portalOutlineMask != null && portalOutlineMask.outlineColor.a > 0.001f)
      {
        return portalOutlineMask.outlineColor;
      }

      if (portalOutlineSupportMask != null &&
          portalOutlineSupportMask.outlineColor.a > 0.001f)
      {
        return portalOutlineSupportMask.outlineColor;
      }

      return Color.clear;
    }

    private static bool Approximately(Color a, Color b)
    {
      return Mathf.Abs(a.r - b.r) < 0.001f &&
          Mathf.Abs(a.g - b.g) < 0.001f &&
          Mathf.Abs(a.b - b.b) < 0.001f &&
          Mathf.Abs(a.a - b.a) < 0.001f;
    }

    private void ResetDynamicState()
    {
      wasActivated = false;
      lastProgressFrame = -1;
      chargeWaveStarted = false;
      centerIdleStarted = false;
      ResetCenterOpeningFallback();
      ResetCenterClosing();
      outlineCapVisible = false;
      lastOutlineCapColor = Color.clear;
      portalBodyRendererStateApplied = false;
      portalBodyRendererActivated = false;
      portalBodySpriteStateApplied = false;
      portalBodySpriteActivated = false;

      ResetAnimatedLayer(
          portalChargeProgress,
          false,
          0,
          false,
          0.0f,
          0.01f,
          milestoneColor,
          milestoneEmissiveColor);
      ResetAnimatedLayer(
          portalEmissiveWave,
          false,
          0,
          true,
          Mathf.Max(0.01f, chargeWaveSpeed),
          0.0f,
          chargeWaveColor,
          ScaleColor(
              MultiplyColors(chargeWaveEmissiveColor, chargeWaveColor),
              3.5f));
      ResetAnimatedLayer(
          portalCenterEffect,
          false,
          0,
          true,
          1.0f,
          0.0f,
          centerColor,
          ScaleColor(
              MultiplyColors(centerEmissiveColor, centerColor),
              Mathf.Clamp01(centerGlowIntensity)));
      ResetCustomSwirl();
      ConfigureStaticLayer(portalOutlineCap, false, Color.clear, Color.clear);
      StopCenterParticles();
      if (centerParticlesRoot != null)
      {
        centerParticlesRoot.SetActive(false);
      }
      if (readyFlashRoot != null)
      {
        readyFlashRoot.SetActive(false);
      }
    }

    private static void ResetAnimatedLayer(
        SpriteObject spriteObject,
        bool active,
        int animationIndex,
        bool forceResetTime,
        float animationTimescale,
        float animationTime,
        Color color,
        Color emissiveColor)
    {
      if (spriteObject == null)
      {
        return;
      }

      TryPlayAnimation(spriteObject, animationIndex, forceResetTime);
      spriteObject.animationTimescale = animationTimescale;
      spriteObject.animationTime = animationTime;
      spriteObject.color = color;
      spriteObject.emissiveColor = emissiveColor;
      spriteObject.flashColor = Color.clear;
      spriteObject.outlineColor = Color.clear;
      SetActive(spriteObject, active);
      spriteObject.ApplyVisualChange();
    }

    private void ApplyChargeProgress(
        float progress,
        bool portalActive,
        bool activated,
        bool force)
    {
      if (portalBodyRenderer != null)
      {
        SetActive(portalBody, false);
        SetActive(portalEmissiveWave, false);
        ApplyPortalBodyRendererState(activated, force);
        chargeWaveStarted = false;
      }

      // A SpriteObject component can exist even when its Scriptable Data address failed
      // to resolve. Never hide the persistent portal body for an unusable charge wave:
      // doing so made the whole portal invisible until activation and then threw every
      // frame from ApplyVisualChange().
      bool waveAvailable = chargeWaveVisible &&
          portalEmissiveWave != null && portalEmissiveWave.asset != null;
      bool waveActive = portalActive && !activated && waveAvailable;
      if (portalBodyRenderer == null)
      {
        SetActive(portalBody, portalBodyVisible);
        ApplyPortalBodySpriteState(activated, force);
        SetActive(portalEmissiveWave, waveActive);
      }
      else
      {
        waveActive = false;
      }

      if (waveActive)
      {
        if (force || !chargeWaveStarted)
        {
          TryPlayAnimation(portalEmissiveWave, 0, true);
          portalEmissiveWave.animationTime =
              Time.time * Mathf.Max(0.01f, chargeWaveSpeed);
          chargeWaveStarted = true;
        }

        portalEmissiveWave.animationTimescale = Mathf.Max(0.01f, chargeWaveSpeed);
        portalEmissiveWave.ApplyVisualChange();
      }
      else
      {
        chargeWaveStarted = false;
      }

      int milestoneStage = ResolveMilestoneStage(progress, activated);
      // Keep the milestone layer available for stage zero as well. The vanilla
      // frame for that stage is transparent, so the default appearance is
      // unchanged, while custom sheets can author a visible pre-threshold state
      // by drawing into frame 0.
      bool active = portalActive && milestoneVisible;
      SetActive(portalChargeProgress, active);
      if (!active || portalChargeProgress == null)
      {
        lastProgressFrame = -1;
        return;
      }

      int progressFrame = ResolveMilestoneFrame(milestoneStage);
      if (force || lastProgressFrame != progressFrame)
      {
        TryPlayAnimation(portalChargeProgress, 0, false);
        portalChargeProgress.animationTimescale = 0.0f;
        portalChargeProgress.animationTime = ResolveMilestoneAnimationTime(progressFrame);
        portalChargeProgress.ApplyVisualChange();
        lastProgressFrame = progressFrame;
      }
    }

    private float ResolveMilestoneAnimationTime(int sourceFrame)
    {
      if (portalChargeProgress == null || portalChargeProgress.asset == null)
      {
        return Mathf.Max(0, sourceFrame) + 0.01f;
      }

      SpriteAsset asset = portalChargeProgress.asset;
      FrameAnimation animation = asset.animationCount > 0
          ? asset.GetAnimationAt(0)
          : null;
      if (animation == null)
      {
        return Mathf.Max(0, sourceFrame) + 0.01f;
      }

      int clampedSource = Mathf.Clamp(
          sourceFrame,
          0,
          Mathf.Max(0, animation.srcFrameCount - 1));
      int runtimeFrame = clampedSource;
      if (animation.runtimeFrameRemap != null &&
          animation.runtimeFrameRemap.Length > 0)
      {
        runtimeFrame = 0;
        for (int i = 0; i < animation.runtimeFrameRemap.Length; i++)
        {
          if (animation.runtimeFrameRemap[i] == clampedSource)
          {
            runtimeFrame = i;
            break;
          }
        }
      }

      float fps = animation.fps > 0.0f ? animation.fps : 1.0f;
      return (runtimeFrame + 0.01f) / fps;
    }

    private void ApplyPortalBodyRendererState(bool activated, bool force)
    {
      if (portalBodyRenderer == null)
      {
        return;
      }

      portalBodyRenderer.gameObject.SetActive(portalBodyVisible);
      portalBodyRenderer.enabled = portalBodyVisible;
      if (!portalBodyVisible)
      {
        return;
      }
      portalBodyRenderer.color = Color.white;
      if (!force &&
          portalBodyRendererStateApplied &&
          portalBodyRendererActivated == activated)
      {
        return;
      }

      if (portalBodyPropertyBlock == null)
      {
        portalBodyPropertyBlock = new MaterialPropertyBlock();
      }

      portalBodyRenderer.GetPropertyBlock(portalBodyPropertyBlock);
      portalBodyPropertyBlock.SetFloat(LoadingMulProperty, activated ? 0.0f : 1.0f);
      portalBodyPropertyBlock.SetFloat(
          LoadingSpeedProperty,
          Mathf.Max(0.01f, chargeWaveSpeed));
      portalBodyPropertyBlock.SetFloat(
          EmissiveStrengthMulProperty,
          activated ? 1.0f : 0.0f);
      portalBodyPropertyBlock.SetColor(MainColorProperty, portalBodyColor);
      Color emissiveColor = activated
          ? MultiplyColors(portalBodyEmissiveColor, portalBodyColor)
          : MultiplyColors(chargeWaveEmissiveColor, chargeWaveColor);
      portalBodyPropertyBlock.SetColor(EmissiveColorProperty, emissiveColor);
      portalBodyRenderer.SetPropertyBlock(portalBodyPropertyBlock);
      portalBodyRendererStateApplied = true;
      portalBodyRendererActivated = activated;
    }

    private void ApplyPortalBodySpriteState(bool activated, bool force)
    {
      if (portalBody == null || portalBodyRenderer != null || !portalBodyVisible)
      {
        return;
      }

      if (!force &&
          portalBodySpriteStateApplied &&
          portalBodySpriteActivated == activated)
      {
        return;
      }

      portalBody.color = portalBodyColor;
      portalBody.emissiveColor = activated
          ? ScaleColor(MultiplyColors(portalBodyEmissiveColor, portalBodyColor), 5.0f)
          : Color.clear;
      portalBody.ApplyVisualChange();
      portalBodySpriteStateApplied = true;
      portalBodySpriteActivated = activated;
    }

    private int ResolveMilestoneStage(float progress, bool activated)
    {
      if (activated)
      {
        return 4;
      }

      float first = Mathf.Clamp01(firstMilestone);
      float second = Mathf.Clamp(secondMilestone, first, 1.0f);
      float third = Mathf.Clamp(thirdMilestone, second, 1.0f);
      if (progress >= third)
      {
        return 3;
      }

      if (progress >= second)
      {
        return 2;
      }

      return progress >= first ? 1 : 0;
    }

    private static int ResolveMilestoneFrame(int stage)
    {
      if (stage >= 4)
      {
        return MilestoneReadyFrame;
      }

      if (stage == 3)
      {
        return MilestoneThirdFrame;
      }

      if (stage == 2)
      {
        return MilestoneSecondFrame;
      }

      return stage == 1 ? MilestoneFirstFrame : MilestoneEmptyFrame;
    }

    private void ApplyCenterEffect(bool activated, bool becameActivated, bool force)
    {
      bool centerActive = activated && centerVisible;
      if (activated)
      {
        ResetCenterClosing();
      }

      if (!centerClosingPending)
      {
        SetActive(portalCenterEffect, centerActive);
      }

      if (portalCenterEffect != null)
      {
        portalCenterEffect.enabled = true;
      }

      if (centerParticlesRoot != null)
      {
        centerParticlesRoot.SetActive(activated && centerParticlesVisible);
      }
      SetActive(
          portalCustomSwirl,
          activated &&
          centerParticlesVisible &&
          portalCustomSwirl != null &&
          portalCustomSwirl.asset != null);
      if (!activated)
      {
        centerIdleStarted = false;
        StopCustomSwirl();
        ResetCenterOpeningFallback();
        if (wasActivated)
        {
          StopCenterParticles();
        }
        if (centerParticlesRoot != null)
        {
          centerParticlesRoot.SetActive(false);
        }
        if (readyFlashRoot != null)
        {
          readyFlashRoot.SetActive(false);
        }

        // An instant item portal (V2) switches off by playing the opening in reverse: the
        // swirls and particles are already hidden above, while the center stays visible just
        // long enough for the one-shot closing animation before LateUpdate hides it too.
        if (itemPortalMode && wasActivated && !centerClosingPending)
        {
          TryStartCenterClosing();
        }

        return;
      }

      if (becameActivated || (!wasActivated && !force))
      {
        bool openingStarted = centerVisible && TryStartCenterOpening();
        RestartCenterParticles();
        RestartCustomSwirl();
        PlayReadyFlash();
        if (centerVisible && !openingStarted)
        {
          StartCenterIdleAnimation();
        }
        return;
      }

      if (force || !centerIdleStarted)
      {
        // Never restart the idle loop while an opening is still playing. An item portal triggers its
        // opening explicitly a frame or two after spawn (once its id replicates), and this branch would
        // otherwise clobber that opening with the idle animation in the same frame.
        if (centerVisible && !centerOpeningFallbackPending)
        {
          StartCenterIdleAnimation();
        }
        RestartCenterParticles();
        RestartCustomSwirl();
        StopReadyFlash();
      }
    }

    private void ResetCustomSwirl()
    {
      if (portalCustomSwirl == null)
      {
        return;
      }

      TryPlayAnimation(portalCustomSwirl, 0, true);
      portalCustomSwirl.animationTimescale = Mathf.Max(0.01f, customSwirlPlaybackSpeed);
      portalCustomSwirl.animationTime = 0.0f;
      SetActive(portalCustomSwirl, false);
      portalCustomSwirl.ApplyVisualChange();
    }

    private void RestartCustomSwirl()
    {
      if (!centerParticlesVisible ||
          portalCustomSwirl == null ||
          portalCustomSwirl.asset == null)
      {
        StopCustomSwirl();
        return;
      }

      SetActive(portalCustomSwirl, true);
      TryPlayAnimation(portalCustomSwirl, 0, true);
      portalCustomSwirl.animationTimescale = Mathf.Max(0.01f, customSwirlPlaybackSpeed);
      portalCustomSwirl.ApplyVisualChange();
    }

    private void StopCustomSwirl()
    {
      if (portalCustomSwirl != null)
      {
        portalCustomSwirl.animationTimescale = 0.0f;
        SetActive(portalCustomSwirl, false);
      }
    }

    private bool TryStartCenterOpening()
    {
      ResetCenterOpeningFallback();
      if (portalCenterEffect == null || portalCenterEffect.asset == null)
      {
        return false;
      }

      SpriteAsset asset = portalCenterEffect.asset;
      if (centerOpeningAnimationIndex < 0 ||
          centerOpeningAnimationIndex >= asset.animationCount ||
          !TryPlayAnimation(portalCenterEffect, centerOpeningAnimationIndex, true))
      {
        return false;
      }

      centerOpeningAnimationHash = asset.GetAnimationHash(centerOpeningAnimationIndex);
      centerOpeningObserved =
          portalCenterEffect.currentAnimationHash == centerOpeningAnimationHash;
      centerIdleStarted = centerOpeningAnimationIndex == centerIdleAnimationIndex;
      if (centerIdleStarted)
      {
        return true;
      }

      FrameAnimation opening = asset.GetAnimationAt(centerOpeningAnimationIndex);
      float animationSpeed = Mathf.Abs(portalCenterEffect.animationTimescale);
      if (animationSpeed <= 0.0001f)
      {
        animationSpeed = 1.0f;
      }

      float duration = opening == null ? 0.0f : opening.duration / animationSpeed;
      if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0.0f)
      {
        duration = 0.01f;
      }

      bool hasNativeExit =
          opening != null && !opening.loop && opening.runtimeExitAnimationHash != 0;
      centerOpeningFallbackAt =
          Time.time + duration + (hasNativeExit ? 0.1f : 0.0f);
      centerOpeningFallbackPending = true;
      return true;
    }

    private void EnsureCenterIdleAfterOpening()
    {
      if (!centerOpeningFallbackPending ||
          portalCenterEffect == null ||
          portalCenterEffect.gameObject == null ||
          !portalCenterEffect.gameObject.activeSelf)
      {
        return;
      }

      int currentAnimationHash = portalCenterEffect.currentAnimationHash;
      if (currentAnimationHash == centerOpeningAnimationHash)
      {
        centerOpeningObserved = true;
        if (Time.time < centerOpeningFallbackAt)
        {
          return;
        }
      }
      else if (!centerOpeningObserved && Time.time < centerOpeningFallbackAt)
      {
        return;
      }

      StartCenterIdleAnimation();
    }

    private void StartCenterIdleAnimation()
    {
      ResetCenterOpeningFallback();
      TryPlayAnimation(portalCenterEffect, centerIdleAnimationIndex, false);
      centerIdleStarted = true;
    }

    private void ResetCenterOpeningFallback()
    {
      centerOpeningFallbackPending = false;
      centerOpeningObserved = false;
      centerOpeningAnimationHash = 0;
      centerOpeningFallbackAt = -1.0f;
    }

    /// <summary>
    /// Plays the one-shot closing animation (the opening in reverse) on an instant item portal
    /// that just switched off. The center is reactivated for the duration of the animation and
    /// hidden again from LateUpdate once it completes.
    /// </summary>
    private bool TryStartCenterClosing()
    {
      if (!centerVisible ||
          centerClosingAnimationIndex < 0 ||
          portalCenterEffect == null ||
          portalCenterEffect.asset == null)
      {
        return false;
      }

      SpriteAsset asset = portalCenterEffect.asset;
      if (centerClosingAnimationIndex >= asset.animationCount)
      {
        return false;
      }

      SetActive(portalCenterEffect, true);
      portalCenterEffect.enabled = true;
      if (!TryPlayAnimation(portalCenterEffect, centerClosingAnimationIndex, true))
      {
        SetActive(portalCenterEffect, false);
        return false;
      }

      FrameAnimation closing = asset.GetAnimationAt(centerClosingAnimationIndex);
      float animationSpeed = Mathf.Abs(portalCenterEffect.animationTimescale);
      if (animationSpeed <= 0.0001f)
      {
        animationSpeed = 1.0f;
      }

      float duration = closing == null ? 0.0f : closing.duration / animationSpeed;
      if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0.0f)
      {
        duration = 0.01f;
      }

      centerClosingPending = true;
      centerClosingEndsAt = Time.time + duration;
      return true;
    }

    private void FinishCenterClosingIfDue()
    {
      if (!centerClosingPending || Time.time < centerClosingEndsAt)
      {
        return;
      }

      ResetCenterClosing();
      SetActive(portalCenterEffect, false);
    }

    private void ResetCenterClosing()
    {
      centerClosingPending = false;
      centerClosingEndsAt = -1.0f;
    }

    private void RestartCenterParticles()
    {
      if (!centerParticlesVisible || centerParticlesRoot == null)
      {
        StopPersistentCenterParticles();
        return;
      }

      centerParticlesRoot.SetActive(true);
      ParticleSystem[] particleSystems = GetCenterParticleSystems();
      for (int i = 0; i < particleSystems.Length; i++)
      {
        ParticleSystem particleSystem = particleSystems[i];
        if (particleSystem == null || IsReadyFlashParticle(particleSystem))
        {
          continue;
        }

        particleSystem.Clear(true);
        particleSystem.Play(true);
      }
    }

    private void StopCenterParticles()
    {
      StopPersistentCenterParticles();
      StopReadyFlash();
    }

    private void StopPersistentCenterParticles()
    {
      ParticleSystem[] particleSystems = GetCenterParticleSystems();
      for (int i = 0; i < particleSystems.Length; i++)
      {
        ParticleSystem particleSystem = particleSystems[i];
        if (particleSystem != null && !IsReadyFlashParticle(particleSystem))
        {
          particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
      }
    }

    private void PlayReadyFlash()
    {
      if (!playReadyFlash)
      {
        StopReadyFlash();
        return;
      }

      ParticleSystem[] particleSystems = GetReadyFlashParticleSystems();
      if (particleSystems.Length == 0)
      {
        return;
      }

      if (readyFlashRoot != null)
      {
        readyFlashRoot.SetActive(true);
      }

      for (int i = 0; i < particleSystems.Length; i++)
      {
        ParticleSystem particleSystem = particleSystems[i];
        if (particleSystem == null)
        {
          continue;
        }

        particleSystem.gameObject.SetActive(true);
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        particleSystem.Clear(true);
        particleSystem.Play(true);
      }
    }

    private void StopReadyFlash()
    {
      ParticleSystem[] particleSystems = GetReadyFlashParticleSystems();
      for (int i = 0; i < particleSystems.Length; i++)
      {
        ParticleSystem particleSystem = particleSystems[i];
        if (particleSystem != null)
        {
          particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
          particleSystem.gameObject.SetActive(false);
        }
      }

      if (readyFlashRoot != null)
      {
        readyFlashRoot.SetActive(false);
      }
    }

    private void UpdateReadyFlashState()
    {
      if (readyFlashRoot != null && !readyFlashRoot.activeSelf)
      {
        return;
      }

      ParticleSystem[] particleSystems = GetReadyFlashParticleSystems();
      bool alive = false;
      for (int i = 0; i < particleSystems.Length; i++)
      {
        ParticleSystem particleSystem = particleSystems[i];
        if (particleSystem != null &&
            particleSystem.gameObject.activeSelf &&
            particleSystem.IsAlive(true))
        {
          alive = true;
          break;
        }
      }

      if (!alive)
      {
        StopReadyFlash();
      }
    }

    private static bool IsReadyFlashParticle(ParticleSystem particleSystem)
    {
      return particleSystem != null && particleSystem.gameObject.name == "DeathBlink";
    }

    private void MarkShadowAreaDirty()
    {
      Bounds bounds = new Bounds(transform.position, new Vector3(4.0f, 4.0f, 4.0f));
      if (portalShadowCaster != null)
      {
        bounds.Encapsulate(portalShadowCaster.bounds);
      }

      if (portalShadow != null)
      {
        bounds.Encapsulate(portalShadow.bounds);
      }

      Shadows.MarkAreaDirty(bounds, false);
    }

    private ParticleSystem[] GetCenterParticleSystems()
    {
      if (centerParticleSystems != null)
      {
        return centerParticleSystems;
      }

      if (centerParticlesRoot != null)
      {
        centerParticleSystems =
            centerParticlesRoot.GetComponentsInChildren<ParticleSystem>(true);
      }
      else
      {
        // Backward-compatible fallback for prefabs generated before particles gained
        // an independent sibling root.
        centerParticleSystems = portalCenterEffect == null
            ? new ParticleSystem[0]
            : portalCenterEffect.GetComponentsInChildren<ParticleSystem>(true);
      }
      return centerParticleSystems;
    }

    private ParticleSystem[] GetReadyFlashParticleSystems()
    {
      if (readyFlashParticleSystems != null)
      {
        return readyFlashParticleSystems;
      }

      GameObject lookupRoot = readyFlashRoot != null
          ? readyFlashRoot
          : centerParticlesRoot;
      ParticleSystem[] all = lookupRoot == null
          ? new ParticleSystem[0]
          : lookupRoot.GetComponentsInChildren<ParticleSystem>(true);
      int count = 0;
      for (int i = 0; i < all.Length; i++)
      {
        if (IsReadyFlashParticle(all[i]))
        {
          count++;
        }
      }

      readyFlashParticleSystems = new ParticleSystem[count];
      int targetIndex = 0;
      for (int i = 0; i < all.Length; i++)
      {
        if (IsReadyFlashParticle(all[i]))
        {
          readyFlashParticleSystems[targetIndex++] = all[i];
        }
      }

      return readyFlashParticleSystems;
    }

    private static void ConfigureStaticLayer(
        SpriteObject spriteObject,
        bool active,
        Color color,
        Color emissiveColor)
    {
      if (spriteObject == null)
      {
        return;
      }

      SetActive(spriteObject, active);
      spriteObject.color = color;
      spriteObject.emissiveColor = emissiveColor;
      spriteObject.flashColor = Color.clear;
      spriteObject.outlineColor = Color.clear;
      spriteObject.animationTimescale = 1.0f;
      spriteObject.ApplyVisualChange();
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
