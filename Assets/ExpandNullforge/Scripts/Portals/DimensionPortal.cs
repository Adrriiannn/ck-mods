using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Networking;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  // Not sealed: the instant item portal's visual carries the DimensionInstantPortal subclass so
  // the pooled graphical-object system can tell the two portal visuals apart.
  [AddComponentMenu("Dimension Framework/Dimension Portal")]
  public class DimensionPortal : EntityMonoBehaviour
  {
    [Header("Portal")]
    [SerializeField]
    private string fallbackPortalId = string.Empty;

    [SerializeField]
    private bool requireGeneratedArea = true;

    [SerializeField]
    private bool allowFallbackPosition = true;

    [SerializeField]
    private string interactionReason = "Dimension portal activation.";

    private const int ReadyObjectDataAmount = 600;
    private const int MaxChargingVisualAmount = ReadyObjectDataAmount - 1;
    private const int OccupiedReadyTrigger = -601574123;
    private const int BecameReadyTrigger = 2039883312;
    private const int TeleportTrigger = -1518581387;
    private static RuntimeAnimatorController cachedVanillaPortalAnimatorController;

    [Header("Visuals")]
    [SerializeField]
    private DimensionPortalVisual visual;

    private float lastUseTime = -1000.0f;
    private bool wasActivePreviousFrame;
    private bool visualStateInitialized;
    private bool lastVisualActive;
    private bool lastVisualActivated;
    private int lastVisualAmount = int.MinValue;
    private float localVisualChargeStartedAt = -1.0f;

    // Looping ambience bed while an instant portal stands open. The clip loads async via
    // Addressables, so the view polls until it lands; the source lives on the pooled view
    // GameObject and is stopped whenever the view is freed or rebound as a placed portal.
    private AudioSource instantAmbienceSource;
    private bool instantAmbienceWanted;

    public override void OnOccupied()
    {
      base.OnOccupied();
      EnsureVanillaPortalAnimatorController();

      ResetVisualTracking();

      bool activated = IsPortalActivatedForVisuals();
      wasActivePreviousFrame = activated;
      UpdateVisuals(true);
      if (activated && HasAnimatorController())
      {
        animator.SetTrigger(OccupiedReadyTrigger);
      }

      // Pooled portal visuals are shared across portal prefabs, so the instant item-portal
      // (V2) look is resolved per entity from its own ObjectDataCD — present at view creation,
      // no replication wait — instead of trusting whichever prefab instance the pool handed
      // over.
      bool instantPortal = entityExist &&
          DimensionItemPortalRegistry.IsInstantPortalObjectId(base.objectData.objectID);

      DimensionPortalVisual resolvedVisual = ResolveVisual();
      if (resolvedVisual != null)
      {
        resolvedVisual.SetItemPortalMode(instantPortal);
        resolvedVisual.RefreshProjectedShadow();

        // An instant portal spawns already active, so the "just became active" opening trigger
        // never fires — play the opening explicitly the moment its view appears. Gated on the
        // portal actually being active: when the view appears later (portal not yet replicated
        // or already closing), the regular became-activated path plays the opening instead.
        if (activated && instantPortal)
        {
          resolvedVisual.PlayItemPortalOpening();

          // Configured sound feedback, synced with the opening animation. Client-side, so
          // every player near the spawn hears it. Re-binding a still-open portal replays the
          // opening animation, and the sound follows it — same behavior.
          PlayActivationSound(true);
        }
      }

      if (!instantPortal || !activated)
      {
        // A pooled view can be rebound as a placed portal (or as a not-yet-open instant one);
        // never let a previous binding's ambience keep humming.
        StopInstantAmbience();
      }
    }

    public override void ManagedLateUpdate()
    {
      base.ManagedLateUpdate();
      UpdateVisuals(false);
      UpdateInstantAmbience();
    }

    public override void OnFree()
    {
      StopInstantAmbience();
      base.OnFree();
    }

    private bool IsInstantPortalObject()
    {
      return entityExist &&
          DimensionItemPortalRegistry.IsInstantPortalObjectId(base.objectData.objectID);
    }

    private bool TryGetSoundProfile(out DimensionPortalSoundProfile profile)
    {
      profile = default(DimensionPortalSoundProfile);
      return entityExist &&
          DimensionPortalSoundRegistry.TryGetForObjectId(base.objectData.objectID, out profile);
    }

    /// <summary>
    /// Activation feedback per the configured sound profile: Peak mode plays the activation
    /// one-shot; Loop mode (instant portals only) arms the looping bed instead. The two modes
    /// are exclusive by design. Placed portals only ever use their activation sound.
    /// </summary>
    private void PlayActivationSound(bool instantPortal)
    {
      if (!Application.isPlaying || !TryGetSoundProfile(out DimensionPortalSoundProfile profile))
      {
        return;
      }

      if (instantPortal && profile.Mode == DimensionPortalSoundMode.Loop)
      {
        instantAmbienceWanted = true;
        return;
      }

      DimensionPortalSoundRegistry.PlayOneShot(profile.ActivationSound, transform.position);
    }

    private void PlayDeactivationSound()
    {
      if (!Application.isPlaying || !TryGetSoundProfile(out DimensionPortalSoundProfile profile))
      {
        StopInstantAmbience();
        return;
      }

      if (profile.Mode == DimensionPortalSoundMode.Loop)
      {
        StopInstantAmbience();
        return;
      }

      DimensionPortalSoundRegistry.PlayOneShot(profile.DeactivationSound, transform.position);
    }

    /// <summary>
    /// Keeps the instant portal's looping bed in step with its life: starts it once the
    /// async-loaded clip is available (polled — the first portal after game start may open
    /// before the clip has landed) and stops it the moment the portal deactivates to close.
    /// </summary>
    private void UpdateInstantAmbience()
    {
      if (!instantAmbienceWanted)
      {
        return;
      }

      if (!IsPortalActivatedForVisuals())
      {
        StopInstantAmbience();
        return;
      }

      if (instantAmbienceSource != null && instantAmbienceSource.isPlaying)
      {
        return;
      }

      if (!TryGetSoundProfile(out DimensionPortalSoundProfile profile) ||
          profile.Mode != DimensionPortalSoundMode.Loop)
      {
        return;
      }

      AudioClip clip = DimensionPortalSoundRegistry.TryGetLoopClip(profile.LoopSound);
      if (clip == null)
      {
        return;
      }

      StartInstantAmbience(clip);
    }

    private void StartInstantAmbience(AudioClip clip)
    {
      if (instantAmbienceSource == null)
      {
        instantAmbienceSource = GetComponent<AudioSource>();
        if (instantAmbienceSource == null)
        {
          instantAmbienceSource = gameObject.AddComponent<AudioSource>();
        }
      }

      DimensionPortalSoundRegistry.ConfigureSpatialSource(instantAmbienceSource, clip, true);
      instantAmbienceSource.Play();
    }

    private void StopInstantAmbience()
    {
      instantAmbienceWanted = false;
      if (instantAmbienceSource != null && instantAmbienceSource.isPlaying)
      {
        instantAmbienceSource.Stop();
      }
    }

    public void Use()
    {
      if (Time.unscaledTime - lastUseTime < 0.25f)
      {
        return;
      }

      lastUseTime = Time.unscaledTime;

      string portalId;
      bool requireGenerated;
      bool allowFallback;
      if (!TryResolvePortalRequest(out portalId, out requireGenerated, out allowFallback))
      {
        Debug.LogWarning("[ExpandNullforge] Dimension portal interaction ignored because no portal id is configured.");
        return;
      }

      if (!IsPortalReadyForUse())
      {
        SpawnNotFullyChargedText();
        return;
      }

      uint requestId = DimensionPortalTravelActions.RequestPortalTravel(
          portalId,
          requireGenerated,
          allowFallback,
          ResolveInteractionReason(portalId));

      if (requestId == 0)
      {
        Debug.LogWarning("[ExpandNullforge] Dimension portal interaction could not queue a travel request.");
        return;
      }

      DimensionFrameworkLog.Verbose(
          "[ExpandNullforge] Dimension portal travel request queued. requestId=" +
          requestId +
          " portalId=" +
          portalId);

      // Vanilla teleport feedback on the interacting client: the portal's squash (Animator
      // TeleportTrigger) and the PortalTeleport effect (sound + particles). Vanilla fires these
      // through the effect's animationEventEffects wiring, which the generated prefab does not set
      // up, so they must be invoked directly the moment travel is queued — this is what makes the
      // portal squeeze vertically when a player steps through it.
      PlayLocalTeleportEffects();
      TeleportEffects();
    }

    public void OnLeavePortal()
    {
      // Kept as a stable prefab callback. The portal currently has no local exit cleanup.
    }

    public void PlayLocalTeleportEffects()
    {
      EnsureVanillaPortalAnimatorController();
      if (HasAnimatorController())
      {
        animator.SetTrigger(TeleportTrigger);
      }
    }

    public void TeleportEffects()
    {
      if (!entityExist)
      {
        return;
      }

      EntityUtility.PlayEffectEventClient(new EffectEventCD
      {
        entity = entity,
        effectID = EffectID.PortalTeleport
      });
    }

    private void UpdateVisuals(bool force)
    {
      if (!entityExist)
      {
        return;
      }

      bool portalActive;
      int amount = GetVisualChargeAmount(out portalActive);
      bool activated = amount >= ReadyObjectDataAmount;
      bool becameActivated = visualStateInitialized && !lastVisualActivated && activated;
      bool becameDeactivated = visualStateInitialized && lastVisualActivated && !activated;
      if (!force &&
          visualStateInitialized &&
          lastVisualAmount == amount &&
          lastVisualActive == portalActive &&
          lastVisualActivated == activated)
      {
        return;
      }

      visualStateInitialized = true;
      lastVisualAmount = amount;
      lastVisualActive = portalActive;
      lastVisualActivated = activated;

      if (becameActivated)
      {
        EnsureVanillaPortalAnimatorController();

        // A placed portal "lights up" here (charge completed); an instant portal only reaches
        // this transition when its view appeared before its portal data replicated — the
        // OnOccupied path and this one are mutually exclusive, so the sound plays exactly once.
        PlayActivationSound(IsInstantPortalObject());
      }

      if (becameDeactivated && IsInstantPortalObject())
      {
        PlayDeactivationSound();
      }

      DimensionPortalVisual resolvedVisual = ResolveVisual();
      if (resolvedVisual != null)
      {
        resolvedVisual.ApplyPortalState(
            amount,
            portalActive,
            activated,
            becameActivated,
            force);
      }

      if (!wasActivePreviousFrame && activated)
      {
        wasActivePreviousFrame = true;
        if (HasAnimatorController())
        {
          animator.SetTrigger(BecameReadyTrigger);
        }
      }
      else if (!activated)
      {
        wasActivePreviousFrame = false;
      }
    }

    private void EnsureVanillaPortalAnimatorController()
    {
      if (animator == null)
      {
        animator = GetComponent<Animator>();
      }

      if (animator == null || animator.runtimeAnimatorController != null)
      {
        return;
      }

      RuntimeAnimatorController controller = ResolveVanillaPortalAnimatorController();
      if (controller == null)
      {
        return;
      }

      animator.runtimeAnimatorController = controller;
      animator.Rebind();
    }

    private static RuntimeAnimatorController ResolveVanillaPortalAnimatorController()
    {
      if (cachedVanillaPortalAnimatorController != null)
      {
        return cachedVanillaPortalAnimatorController;
      }

      ObjectInfo objectInfo = PugDatabase.GetObjectInfo(ObjectID.Portal);
      if (objectInfo == null || objectInfo.prefabInfos == null)
      {
        return null;
      }

      for (int i = 0; i < objectInfo.prefabInfos.Count; i++)
      {
        PrefabInfo prefabInfo = objectInfo.prefabInfos[i];
        Portal vanillaPortal = prefabInfo == null ? null : prefabInfo.prefab as Portal;
        if (vanillaPortal == null)
        {
          continue;
        }

        Animator sourceAnimator = vanillaPortal.animator;
        if (sourceAnimator == null)
        {
          sourceAnimator = vanillaPortal.GetComponent<Animator>();
        }

        if (sourceAnimator == null || sourceAnimator.runtimeAnimatorController == null)
        {
          continue;
        }

        cachedVanillaPortalAnimatorController = sourceAnimator.runtimeAnimatorController;
        return cachedVanillaPortalAnimatorController;
      }

      return null;
    }

    private bool HasAnimatorController()
    {
      return animator != null && animator.runtimeAnimatorController != null;
    }

    private DimensionPortalVisual ResolveVisual()
    {
      if (visual != null)
      {
        return visual;
      }

      visual = GetComponent<DimensionPortalVisual>();
      return visual;
    }

    private void ResetVisualTracking()
    {
      wasActivePreviousFrame = false;
      visualStateInitialized = false;
      lastVisualActive = false;
      lastVisualActivated = false;
      lastVisualAmount = int.MinValue;
      localVisualChargeStartedAt = -1.0f;
    }

    private bool IsPortalActivatedForVisuals()
    {
      bool portalActive;
      return entityExist &&
          GetVisualChargeAmount(out portalActive) >= ReadyObjectDataAmount;
    }

    private int GetVisualChargeAmount(out bool portalActive)
    {
      portalActive = false;
      if (!entityExist)
      {
        localVisualChargeStartedAt = -1.0f;
        return 0;
      }

      int objectDataAmount = Mathf.Clamp(base.objectData.amount, 0, ReadyObjectDataAmount);
      DimensionPortalCD portal;
      if (EntityUtility.TryGetComponentData<DimensionPortalCD>(entity, world, out portal))
      {
        if (portal.Active == 0)
        {
          localVisualChargeStartedAt = -1.0f;
          return 0;
        }

        portalActive = true;

        if (portal.Charged != 0 ||
            portal.ActivationChargeSeconds <= 0.0f ||
            objectDataAmount >= ReadyObjectDataAmount)
        {
          localVisualChargeStartedAt = -1.0f;
          return ReadyObjectDataAmount;
        }

        return ResolveVisualChargeAmount(portal, objectDataAmount);
      }

      portalActive = true;
      localVisualChargeStartedAt = -1.0f;
      return objectDataAmount;
    }

    private int ResolveVisualChargeAmount(
        DimensionPortalCD portal,
        int objectDataAmount)
    {
      float chargeSeconds = Mathf.Max(0.01f, portal.ActivationChargeSeconds);
      float authoritativeProgress = Mathf.Clamp01(Mathf.Max(
          portal.ActivationProgress,
          (float)objectDataAmount / ReadyObjectDataAmount));

      if (localVisualChargeStartedAt < 0.0f)
      {
        localVisualChargeStartedAt = Time.time - authoritativeProgress * chargeSeconds;
      }
      else if (authoritativeProgress > 0.0f)
      {
        float authoritativeStartedAt = Time.time - authoritativeProgress * chargeSeconds;
        if (authoritativeStartedAt < localVisualChargeStartedAt)
        {
          localVisualChargeStartedAt = authoritativeStartedAt;
        }
      }

      float localProgress = Mathf.Clamp01((Time.time - localVisualChargeStartedAt) / chargeSeconds);
      float progress = Mathf.Max(authoritativeProgress, localProgress);
      int amount = Mathf.Clamp(
          Mathf.RoundToInt(progress * ReadyObjectDataAmount),
          0,
          ReadyObjectDataAmount);
      if (portal.Charged == 0 && objectDataAmount < ReadyObjectDataAmount)
      {
        amount = Mathf.Min(amount, MaxChargingVisualAmount);
      }

      return amount;
    }

    private bool TryResolvePortalRequest(
        out string portalId,
        out bool requireGenerated,
        out bool allowFallback)
    {
      portalId = string.Empty;
      requireGenerated = requireGeneratedArea;
      allowFallback = allowFallbackPosition;

      if (entityExist)
      {
        DimensionPortalCD portal;
        if (EntityUtility.TryGetComponentData<DimensionPortalCD>(entity, world, out portal))
        {
          portalId = portal.PortalId.ToString();
          requireGenerated = portal.RequireGeneratedArea != 0;
          allowFallback = portal.AllowFallbackPosition != 0;
          if (!string.IsNullOrEmpty(portalId))
          {
            return true;
          }
        }
      }

      portalId = fallbackPortalId ?? string.Empty;
      return !string.IsNullOrEmpty(portalId);
    }

    private string ResolveInteractionReason(string portalId)
    {
      DimensionPortalPresentationDefinition presentation;
      if (TryGetPresentation(portalId, out presentation) &&
          !string.IsNullOrEmpty(presentation.PromptText))
      {
        return presentation.PromptText;
      }

      return string.IsNullOrEmpty(interactionReason)
          ? "Dimension portal activation."
          : interactionReason;
    }

    private static bool TryGetPresentation(
        string portalId,
        out DimensionPortalPresentationDefinition presentation)
    {
      presentation = default(DimensionPortalPresentationDefinition);
      IDimensionService service;
      return DimensionApi.TryGetService(out service) &&
             service != null &&
             service.TryFindPortalPresentationForPortal(portalId, out presentation);
    }

    private bool IsPortalReadyForUse()
    {
      if (!entityExist)
      {
        return true;
      }

      DimensionPortalCD portal;
      if (!EntityUtility.TryGetComponentData<DimensionPortalCD>(entity, world, out portal))
      {
        return base.objectData.amount >= ReadyObjectDataAmount;
      }

      if (portal.Active == 0)
      {
        return false;
      }

      if (portal.Charged != 0 || portal.ActivationChargeSeconds <= 0.0f)
      {
        return true;
      }

      return base.objectData.amount >= ReadyObjectDataAmount;
    }

    private static void SpawnNotFullyChargedText()
    {
      Manager manager = Manager.main;
      PlayerController player = manager != null ? manager.player : null;
      if (player != null)
      {
        Emote.SpawnEmoteText(
            player.center,
            Emote.EmoteType.NotFullyCharged,
            true,
            false,
            true);
      }
    }

  }
}
