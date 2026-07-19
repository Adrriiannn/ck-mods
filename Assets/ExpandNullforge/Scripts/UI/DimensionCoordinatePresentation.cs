using System;
using ExpandNullforge.Api;
using ExpandNullforge.Networking;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.UI
{
  /// <summary>
  /// Rewrites the vanilla map coordinate widget to show dimension-local coordinates
  /// while the local player is inside any registered non-overworld dimension.
  ///
  /// This is intentionally presentation-only. The player, map cursor, entities and
  /// vanilla map systems still use the real absolute coordinates, so other mods
  /// that are not dimension-aware keep seeing the actual Core Keeper world space
  /// unless they explicitly integrate with ExpandNullforge's coordinate API.
  /// Dimension-aware mods should use DimensionApiCoordinates or IDimensionService
  /// coordinate-domain helpers to convert between absolute and dimension-local
  /// coordinates.
  /// </summary>
  public static class DimensionCoordinatePresentation
  {
    private const double AttachRetrySeconds = 0.50d;
    private const double SlowAttachRetrySeconds = 5.0d;
    private const int FastAttachRetryMisses = 8;

    private static DimensionCoordinatePresentationHost host;
    private static double nextAttachAttemptAt;
    private static int attachMissCount;

    public static void EnsureAttached()
    {
      if (host != null)
      {
        return;
      }

      double now = Time.realtimeSinceStartupAsDouble;
      if (now < nextAttachAttemptAt)
      {
        return;
      }

      nextAttachAttemptAt = now + AttachRetrySeconds;
      CoordinatesUI coordinates = UnityEngine.Object.FindFirstObjectByType<CoordinatesUI>();
      if (coordinates == null)
      {
        attachMissCount++;
        if (attachMissCount >= FastAttachRetryMisses)
        {
          nextAttachAttemptAt = now + SlowAttachRetrySeconds;
        }

        return;
      }

      attachMissCount = 0;
      host = coordinates.GetComponent<DimensionCoordinatePresentationHost>();
      if (host == null)
      {
        host = coordinates.gameObject.AddComponent<DimensionCoordinatePresentationHost>();
      }

      host.Bind(coordinates);
    }

    public static void Reset()
    {
      if (host != null)
      {
        UnityEngine.Object.Destroy(host);
      }

      host = null;
      nextAttachAttemptAt = 0.0d;
      attachMissCount = 0;
    }
  }

  [DefaultExecutionOrder(32760)]
  public sealed class DimensionCoordinatePresentationHost : MonoBehaviour, IManagedLateUpdate
  {
    private CoordinatesUI coordinates;
    private bool registeredWithUpdateManager;

    public void Bind(CoordinatesUI source)
    {
      coordinates = source;
      TryRegisterManagedLateUpdate();
    }

    private void Awake()
    {
      if (coordinates == null)
      {
        coordinates = GetComponent<CoordinatesUI>();
      }
    }

    private void LateUpdate()
    {
      TryRegisterManagedLateUpdate();
      RenderLocalCoordinates();
    }

    public void ManagedLateUpdate()
    {
      RenderLocalCoordinates();
    }

    private void OnEnable()
    {
      TryRegisterManagedLateUpdate();
    }

    private void OnDisable()
    {
      TryUnregisterManagedLateUpdate();
    }

    private void OnDestroy()
    {
      TryUnregisterManagedLateUpdate();
    }

    private void RenderLocalCoordinates()
    {
      if (coordinates == null ||
          coordinates.mapUI == null)
      {
        return;
      }

      IDimensionService service;
      if (!DimensionApi.TryGetService(out service) || service == null)
      {
        return;
      }

      float2 cursorAbsolutePosition = coordinates.mapUI.GetCursorWorldPosition();
      DimensionContext presentationContext;
      if (!TryGetPresentationContext(service, cursorAbsolutePosition, out presentationContext))
      {
        return;
      }

      int2 localTile = (int2)math.floor(presentationContext.LocalPosition);
      string coordinateText = localTile.x.ToString("F0") + ", " + localTile.y.ToString("F0");
      RenderPugText(coordinates.coordinateText, coordinateText);
      RenderPugText(coordinates.coordinateTextOutline, coordinateText);

      string distanceText = "(" + math.length(new float2(localTile.x, localTile.y)).ToString("F0") + ")";
      RenderPugText(coordinates.distanceText, distanceText);
      RenderPugText(coordinates.distanceTextOutline, distanceText);
    }

    private bool TryGetPresentationContext(
        IDimensionService service,
        float2 cursorAbsolutePosition,
        out DimensionContext context)
    {
      string currentDimensionId;
      if (TryGetPresentationDimensionId(out currentDimensionId))
      {
        DimensionCoordinateDomain currentDomain;
        if (service.TryGetCoordinateDomain(currentDimensionId, out currentDomain))
        {
          context =
              new DimensionContext(
                  true,
                  currentDomain.DimensionId,
                  cursorAbsolutePosition,
                  currentDomain.ToLocal(cursorAbsolutePosition));
          return true;
        }
      }

      context = service.GetCoordinateContextForAbsolute(cursorAbsolutePosition);
      return context.IsKnown && IsPresentationDimension(context.DimensionId);
    }

    private bool TryGetPresentationDimensionId(out string dimensionId)
    {
      DimensionPlayerContextNetworkSnapshot snapshot;
      if (DimensionPlayerContextNetworkState.TryGetCurrentSnapshot(out snapshot) &&
          snapshot.IsKnown)
      {
        string snapshotDimensionId = snapshot.Context.DimensionId;
        if (IsPresentationDimension(snapshotDimensionId))
        {
          dimensionId = snapshotDimensionId;
          return true;
        }
      }

      IDimensionService service;
      if (DimensionApi.TryGetService(out service) &&
          service != null &&
          Manager.main != null &&
          Manager.main.player != null)
      {
        Vector3 playerPosition = Manager.main.player.WorldPosition;
        DimensionContext inferredContext =
            service.GetCoordinateContextForAbsolute(new float2(playerPosition.x, playerPosition.z));
        if (inferredContext.IsKnown && IsPresentationDimension(inferredContext.DimensionId))
        {
          dimensionId = inferredContext.DimensionId;
          return true;
        }
      }

      dimensionId = string.Empty;
      return false;
    }

    private static bool IsPresentationDimension(string dimensionId)
    {
      return !string.IsNullOrEmpty(dimensionId) &&
             !string.Equals(dimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
    }

    private static void RenderPugText(PugText pugText, string text)
    {
      if (pugText != null)
      {
        pugText.Render(text, false, false, true);
      }
    }

    private void TryRegisterManagedLateUpdate()
    {
      if (registeredWithUpdateManager || !isActiveAndEnabled || Manager.update == null)
      {
        return;
      }

      Manager.update.AddToLateUpdate(this);
      registeredWithUpdateManager = true;
    }

    private void TryUnregisterManagedLateUpdate()
    {
      if (!registeredWithUpdateManager)
      {
        return;
      }

      if (Manager.update != null)
      {
        Manager.update.RemoveFromLateUpdate(this);
      }

      registeredWithUpdateManager = false;
    }
  }
}
