using System;
using Pug.ECS.Components;
using Pug.UnityExtensions;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class ChunkLoaderCompatibility
{
  private static bool _coreValidated;
  private static bool _coreAvailable;
  private static string _coreFailure = string.Empty;

  public static bool CoreAvailable => _coreAvailable;
  public static string CoreFailure => _coreFailure;

  public static bool ValidateCore(World serverWorld)
  {
    if (_coreValidated)
    {
      return _coreAvailable;
    }

    _coreValidated = true;
    _coreAvailable = false;
    _coreFailure = string.Empty;

    try
    {
      if (serverWorld == null || !serverWorld.IsCreated)
      {
        return Fail("The server ECS world is unavailable.");
      }

      KeepAreaLoadedCD residency = new KeepAreaLoadedCD
      {
        KeepLoadedRadius = ChunkLoaderConstants.ResidencyRadius,
        StartLoadRadius = ChunkLoaderConstants.ResidencyRadius,
        ImmediateLoadRadius = ChunkLoaderConstants.ResidencyRadius
      };

      EnableEntitiesInBoxCD simulation = new EnableEntitiesInBoxCD
      {
        Area = PugGeometry.AxisAlignedBoundingBox.FromLowerCornerAndSize(
            float2.zero,
            new float2(ChunkLoaderConstants.OperationalSize))
      };

      if (residency.KeepLoadedRadius <= 0.0f ||
          simulation.Area.High.x - simulation.Area.Low.x != ChunkLoaderConstants.OperationalSize)
      {
        return Fail("The current residency or enable-box component layout is incompatible.");
      }

      _coreAvailable = true;
      Debug.Log("[ChunkLoaderMod] Core compatibility checks passed.");
      return true;
    }
    catch (Exception ex)
    {
      return Fail(ex.Message);
    }
  }

  public static bool IsMapUiAvailable()
  {
    return Manager.ui != null &&
           Manager.ui.mapUI != null &&
           Manager.ui.mapUI.mapPartsContainer != null;
  }

  public static void Reset()
  {
    _coreValidated = false;
    _coreAvailable = false;
    _coreFailure = string.Empty;
  }

  private static bool Fail(string message)
  {
    _coreFailure = message ?? "Unknown compatibility failure.";
    Debug.LogError($"[ChunkLoaderMod] Compatibility failure: {_coreFailure}");
    return false;
  }
}
