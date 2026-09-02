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
}
