using System;
using PugMod;
using UnityEngine;

public static class ConveyorTunnelIds
{
  public const string ObjectName = "ConveyorTunnel";

  private static bool _resolved;
  private static bool _loggedWaiting;

  public static ObjectID ConveyorTunnelObjectID { get; private set; } = ObjectID.None;

  public static bool TryRefresh()
  {
    if (_resolved && ConveyorTunnelObjectID != ObjectID.None)
    {
      return true;
    }

    if (API.Authoring == null)
    {
      return false;
    }

    try
    {
      ObjectID objectID = API.Authoring.GetObjectID(ObjectName);

      if (objectID == ObjectID.None)
      {
        LogWaitingOnce();
        return false;
      }

      ConveyorTunnelObjectID = objectID;
      _resolved = true;
      Debug.Log($"[ConveyorTunnelMod] Resolved {ObjectName} as ObjectID={(int)objectID}");
      return true;
    }
    catch (Exception exception)
    {
      LogWaitingOnce(exception);
      return false;
    }
  }

  public static void Reset()
  {
    _resolved = false;
    _loggedWaiting = false;
    ConveyorTunnelObjectID = ObjectID.None;
  }

  private static void LogWaitingOnce(Exception exception = null)
  {
    if (_loggedWaiting)
    {
      return;
    }

    _loggedWaiting = true;
    string suffix = exception == null ? string.Empty : $": {exception.Message}";
    Debug.Log($"[ConveyorTunnelMod] Waiting for custom object '{ObjectName}' to be registered{suffix}");
  }
}
