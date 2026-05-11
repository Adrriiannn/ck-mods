using Pug.Automation;
using Pug.Automation.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterOrchestratorProbeSystem : SystemBase
{
    private double _nextLogTime;

    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>()));
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableOrchestratorProbe)
        {
            return;
        }

        if (!SmartSplitterDebugSettings.ShouldRunProbe(World.Time.ElapsedTime, ref _nextLogTime))
        {
            return;
        }

        ComponentLookup<MoverCD> moverLookup = GetComponentLookup<MoverCD>(true);

        EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>());
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

        int logged = 0;

        for (int i = 0; i < entities.Length; i++)
        {
            Entity orchestrator = entities[i];

            if (SmartSplitterDebugSettings.OnlyLogLikelySplitters &&
                !SmartSplitterDebugUtility.LooksLikeSplitter(EntityManager, orchestrator, moverLookup))
            {
                continue;
            }

            if (SmartSplitterDebugSettings.OnlyLogConfiguredSmartSplitters &&
                !EntityManager.HasComponent<SmartSplitterTag>(orchestrator))
            {
                continue;
            }

            DynamicBuffer<MoversWithSharedStateBuffer> buffer =
                EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

            Debug.Log(
                $"[SmartSplitterDebug][OrchestratorProbe] orchestrator={orchestrator} " +
                $"bufferLength={buffer.Length} " +
                SmartSplitterDebugUtility.BuildMoverOrchestratorText(EntityManager, "orchestrator", orchestrator)
            );

            for (int b = 0; b < buffer.Length; b++)
            {
                MoversWithSharedStateBuffer entry = buffer[b];

                Debug.Log(
                    $"[SmartSplitterDebug][OrchestratorProbe] entry[{b}] orchestrator={orchestrator} " +
                    $"moverEntity={entry.moverEntity} cachedDirection={entry.cachedDirection} cachedStart={entry.cachedStart} " +
                    SmartSplitterDebugUtility.BuildMoverText(EntityManager, entry.moverEntity)
                );
            }

            logged++;

            if (logged >= SmartSplitterDebugSettings.MaxRowsPerProbe)
            {
                Debug.Log("[SmartSplitterDebug][OrchestratorProbe] stopped at row limit.");
                break;
            }
        }

        entities.Dispose();
    }
}
