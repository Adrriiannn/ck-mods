using Pug.Sprite;
using Pug.RP;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// The charging sweep and the milestone frames it steps through.
  /// </summary>
  public sealed partial class DimensionPortalVisual
  {
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
  }
}
