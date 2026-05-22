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
  public static bool EnableMoveeHandoffProbe = false;

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
  public const float MoveeHandoffProbeRadius = 2.25f;
  public const double MoveeHandoffProbeIntervalSeconds = 0.10d;
  public static bool MoveeHandoffOnlyUnmatchedItems = false;

  // Visual swap settings.
  public static bool EnableVisualSwap = true;
  public static bool EnableVisualSwapLogs = false;
  public static float VisualSwapIntervalSeconds = 0.0f;

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

  // Temporary UI/state proof. Keep disabled now that the prefab-backed panel exists.
  public static bool EnableLaneFilterVerticalSlice = false;
  public static KeyCode LaneFilterVerticalSliceCycleLeftKey = KeyCode.F9;

  // Prefab-backed Smart Splitter panel.
  public static bool EnableSmartSplitterPanel = true;
  public static KeyCode SmartSplitterPanelToggleKey = KeyCode.F9;
  public static float SmartSplitterPanelScale = 1.0f;
  public static float SmartSplitterPanelInteractionRadius = 2.0f;
  public static float SmartSplitterPanelAimLineRadius = 0.85f;
  public static float SmartSplitterPanelCursorRadius = 1.05f;

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
