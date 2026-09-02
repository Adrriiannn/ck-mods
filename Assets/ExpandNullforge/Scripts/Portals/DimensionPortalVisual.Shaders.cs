using Pug.Sprite;
using Pug.RP;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// The shaders and the state each layer is put into before anything animates.
  /// </summary>
  public sealed partial class DimensionPortalVisual
  {
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
  }
}
