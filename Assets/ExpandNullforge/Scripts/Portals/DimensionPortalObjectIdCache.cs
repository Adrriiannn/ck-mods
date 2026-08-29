using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Portals
{
  public static class DimensionPortalObjectIdCache
  {
    private static readonly Dictionary<string, ObjectID> ObjectIds =
        new Dictionary<string, ObjectID>(StringComparer.Ordinal);

    private static readonly HashSet<string> LoggedWaiting =
        new HashSet<string>(StringComparer.Ordinal);

    public static void Clear()
    {
      ObjectIds.Clear();
      LoggedWaiting.Clear();
    }

    public static bool TryResolve(string objectName, out ObjectID objectID)
    {
      objectID = ObjectID.None;
      if (string.IsNullOrEmpty(objectName))
      {
        return false;
      }

      if (ObjectIds.TryGetValue(objectName, out objectID) &&
          objectID != ObjectID.None)
      {
        return true;
      }

      // The framework's one resolver: the game's own names off the enum first, then the mod's own
      // out of the runtime lookup. Asking API.Authoring alone answered None for every vanilla name
      // a portal rule could legitimately point at.
      objectID = ExpandNullforge.Foundation.DimensionObjectNames.Resolve(objectName);
      if (objectID == ObjectID.None)
      {
        LogWaitingOnce(objectName);
        return false;
      }

      ObjectIds[objectName] = objectID;
      LoggedWaiting.Remove(objectName);
      return true;
    }

    public static bool Refresh(string objectName)
    {
      ObjectID objectID;
      return TryResolve(objectName, out objectID);
    }

    private static void LogWaitingOnce(string objectName)
    {
      if (LoggedWaiting.Contains(objectName))
      {
        return;
      }

      LoggedWaiting.Add(objectName);
      Foundation.DimensionFrameworkLog.Verbose(
          "Waiting for custom portal object '" +
          objectName +
          "' to be registered.");
    }
  }
}
