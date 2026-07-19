using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Networking;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  [AddComponentMenu("Dimension Framework/Dimension Portal")]
  public sealed class DimensionPortal : EntityMonoBehaviour
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

      DimensionPortalVisual resolvedVisual = ResolveVisual();
      if (resolvedVisual != null)
      {
        resolvedVisual.RefreshProjectedShadow();
      }
    }

    public override void ManagedLateUpdate()
    {
      base.ManagedLateUpdate();
      UpdateVisuals(false);
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
