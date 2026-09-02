using Pug.Sprite;
using Pug.RP;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// The particles and the ready flash, and the shadow area they dirty.
  /// </summary>
  public sealed partial class DimensionPortalVisual
  {
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
  }
}
