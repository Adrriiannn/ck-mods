using System;
using ExpandNullforge.Api;
using ExpandNullforge.Networking;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.UI
{
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
