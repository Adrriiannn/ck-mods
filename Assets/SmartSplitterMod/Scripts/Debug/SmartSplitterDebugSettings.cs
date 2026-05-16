using UnityEngine;

public static class SmartSplitterDebugSettings
{
  // Master switch. Keep false unless actively debugging.
  public static bool EnableDebugProbes = false;

  // Main focused probes.
  public static bool EnableBrainHeartbeat = false;
  public static bool EnableOrchestratorProbe = false;
  public static bool EnableSplitterCoreProbe = false;
  public static bool EnableDroppedItemNearSplitterProbe = false;
  public static bool EnableOrientationTopologyProbe = false;
  public static bool EnablePassthroughCandidateProbe = false;
  public static bool EnableMoveeHandoffProbe = false;

  // Runtime prototype toggles. Keep disabled unless actively testing passthrough behavior.
  public static bool EnablePassthroughPrototype = false;
  public static bool EnablePassthroughPrototypeLogs = false;

  // Extra probes. Keep these off unless needed.
  public static bool EnableContainedObjectsProbe = false;
  public static bool EnableMoveeNearSplitterProbe = false;

  // Filters / noise controls.
  public static bool OnlyLogLikelySplitters = false;
  public static bool OnlyLogConfiguredSmartSplitters = false;
  public static bool OnlyLogDirtOrTurfItems = false;

  // Shared log throttling.
  public const double DefaultLogIntervalSeconds = 0.75d;
  public const int MaxRowsPerProbe = 20;

  // Spatial probe settings.
  public const float NearbyItemRadius = 6.0f;
  public const float MoveeNearSplitterRadius = 5.0f;
  public const float OrientationTopologyProbeRadius = 4.0f;
  public const float PassthroughCandidateProbeRadius = 2.25f;
  public const float PassthroughCenterCaptureRadius = 0.85f;
  public const float MoveeHandoffProbeRadius = 2.25f;
  public const double MoveeHandoffProbeIntervalSeconds = 0.10d;
  public static bool MoveeHandoffOnlyUnmatchedItems = false;

  // Visual swap settings.
  public static bool EnableVisualSwap = true;
  public static bool EnableVisualSwapLogs = true;
  public static float VisualSwapIntervalSeconds = 0.25f;

  // GameObject probes.
  public static bool EnableGameObjectVisualProbe = false;
  public static bool EnableGameObjectVisualProbeVerboseLogs = false;
  public static float GameObjectVisualProbeIntervalSeconds = 2.0f;
  public static int GameObjectVisualProbeMaxChildDepth = 3;
  public static bool EnableGameObjectHierarchyProbe = false;
  public static bool EnableGameObjectHierarchyProbeVerboseLogs = false;
  public static float GameObjectHierarchyProbeIntervalSeconds = 2.0f;
  public static int GameObjectHierarchyProbeMaxLogs = 80;

  // Visual probes.
  public static bool EnableVisualProbe = false;
  public static float VisualProbeRadius = 1.5f;
  public static bool EnableVisualProbeVerboseLogs = false;


  public static bool ShouldRunProbe(double now, ref double nextLogTime, double intervalSeconds = DefaultLogIntervalSeconds)
  {
    if (!EnableDebugProbes)
    {
      return false;
    }

    if (now < nextLogTime)
    {
      return false;
    }

    nextLogTime = now + intervalSeconds;
    return true;
  }
}
