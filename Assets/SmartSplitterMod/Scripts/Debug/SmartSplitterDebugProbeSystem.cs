using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterDebugProbeSystem : SystemBase
{
    private double _nextLogTime;

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableBrainHeartbeat)
        {
            return;
        }

        if (!SmartSplitterDebugSettings.ShouldRunProbe(World.Time.ElapsedTime, ref _nextLogTime, 2.0d))
        {
            return;
        }

        Debug.Log(
            "[SmartSplitterDebug][Brain] " +
            $"orchestratorProbe={SmartSplitterDebugSettings.EnableOrchestratorProbe} " +
            $"coreProbe={SmartSplitterDebugSettings.EnableSplitterCoreProbe} " +
            $"droppedItemNearSplitterProbe={SmartSplitterDebugSettings.EnableDroppedItemNearSplitterProbe} " +
            $"containedObjectsProbe={SmartSplitterDebugSettings.EnableContainedObjectsProbe} " +
            $"moveeNearSplitterProbe={SmartSplitterDebugSettings.EnableMoveeNearSplitterProbe}"
        );
    }
}
