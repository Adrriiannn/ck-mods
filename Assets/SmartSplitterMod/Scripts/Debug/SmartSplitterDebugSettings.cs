using UnityEngine;

public static class SmartSplitterDebugSettings
{
  // Master switch. Keep false unless actively debugging.
  public static bool EnableDebugProbes = true;

  // Main focused probes.
  public static bool EnableBrainHeartbeat = false;
  public static bool EnableOrchestratorProbe = false;
  public static bool EnableSplitterCoreProbe = false;
  public static bool EnableDroppedItemNearSplitterProbe = false;
  public static bool EnableOrientationTopologyProbe = false;
  public static bool EnablePassthroughCandidateProbe = false;
  public static bool EnableMoveeHandoffProbe = true;

  // Runtime prototype toggles. Keep disabled unless actively testing passthrough behavior.
  public static bool EnablePassthroughPrototype = true;
  public static bool EnablePassthroughPrototypeLogs = true;

  // Extra probes. Keep these off unless needed.
  public static bool EnableContainedObjectsProbe = false;
  public static bool EnableMoveeNearSplitterProbe = false;

  // Filters / noise controls.
  public static bool OnlyLogLikelySplitters = true;
  public static bool OnlyLogConfiguredSmartSplitters = true;
  public static bool OnlyLogDirtOrTurfItems = true;

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
  public static bool MoveeHandoffOnlyUnmatchedItems = true;

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
