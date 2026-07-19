using UnityEngine;

namespace ExpandNullforge.Foundation
{
  internal static class DimensionFrameworkLog
  {
    // Keep false for normal builds. Flip locally only when diagnosing runtime flow.
    public static bool VerboseRuntimeLogging = false;

    public static void Verbose(string message)
    {
      if (VerboseRuntimeLogging)
      {
        Debug.Log(message);
      }
    }

    public static void Info(string message)
    {
      Debug.Log(message);
    }

    public static void Warning(string message)
    {
      Debug.LogWarning(message);
    }

    public static void Error(string message)
    {
      Debug.LogError(message);
    }
  }
}
