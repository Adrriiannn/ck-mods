using Pug.Automation;
using Pug.Automation.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterSplitterCoreProbeSystem : SystemBase
{
    private double _nextLogTime;

    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>()));
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableSplitterCoreProbe)
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

            DynamicBuffer<MoversWithSharedStateBuffer> buffer =
                EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

            Debug.Log(
                $"[SmartSplitterDebug][SplitterCoreProbe] ORCHESTRATOR orchestrator={orchestrator} " +
                $"bufferLength={buffer.Length} " +
                SmartSplitterDebugUtility.BuildMoverOrchestratorText(EntityManager, "orchestrator", orchestrator) + " " +
                SmartSplitterDebugUtility.BuildSmartSplitterStateText(EntityManager, orchestrator)
            );

            for (int b = 0; b < buffer.Length; b++)
            {
                MoversWithSharedStateBuffer entry = buffer[b];

                Debug.Log(
                    $"[SmartSplitterDebug][SplitterCoreProbe] BUFFER_ENTRY orchestrator={orchestrator} entry[{b}] " +
                    $"moverEntity={entry.moverEntity} cachedDirection={entry.cachedDirection} cachedStart={entry.cachedStart} " +
                    SmartSplitterDebugUtility.BuildMoverText(EntityManager, entry.moverEntity) + " " +
                    SmartSplitterDebugUtility.BuildMoverOrchestratorText(EntityManager, "bufferEntryMover", entry.moverEntity)
                );
            }

            LogSameOrchestratorMovers(orchestrator);

            logged++;

            if (logged >= SmartSplitterDebugSettings.MaxRowsPerProbe)
            {
                Debug.Log("[SmartSplitterDebug][SplitterCoreProbe] stopped at row limit.");
                break;
            }
        }

        entities.Dispose();
    }

    private void LogSameOrchestratorMovers(Entity orchestrator)
    {
        EntityQuery moverQuery = GetEntityQuery(ComponentType.ReadOnly<MoverCD>());
        NativeArray<Entity> moverEntities = moverQuery.ToEntityArray(Allocator.Temp);
        NativeArray<MoverCD> movers = moverQuery.ToComponentDataArray<MoverCD>(Allocator.Temp);

        int logged = 0;

        for (int i = 0; i < moverEntities.Length; i++)
        {
            MoverCD mover = movers[i];

            if (mover.moverOrchestratorEntity != orchestrator)
            {
                continue;
            }

            Debug.Log(
                $"[SmartSplitterDebug][SplitterCoreProbe] SAME_ORCHESTRATOR_MOVER orchestrator={orchestrator} " +
                $"mover={moverEntities[i]} " +
                SmartSplitterDebugUtility.BuildMoverText(EntityManager, moverEntities[i])
            );

            logged++;

            if (logged >= SmartSplitterDebugSettings.MaxRowsPerProbe)
            {
                Debug.Log("[SmartSplitterDebug][SplitterCoreProbe] same-orchestrator mover log stopped at row limit.");
                break;
            }
        }

        moverEntities.Dispose();
        movers.Dispose();
    }
}
