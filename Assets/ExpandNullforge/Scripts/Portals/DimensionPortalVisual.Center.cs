using Pug.Sprite;
using Pug.RP;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// The centre of the portal: its idle, its opening, its closing, its swirl.
  /// </summary>
  public sealed partial class DimensionPortalVisual
  {
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
  }
}
