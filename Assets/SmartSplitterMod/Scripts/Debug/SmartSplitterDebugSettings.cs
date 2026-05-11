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
