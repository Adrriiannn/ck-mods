using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterMoveeNearSplitterProbeSystem : SystemBase
{
    private double _nextLogTime;

    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>()));
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableMoveeNearSplitterProbe)
        {
            return;
        }

        if (!SmartSplitterDebugSettings.ShouldRunProbe(World.Time.ElapsedTime, ref _nextLogTime))
        {
            return;
        }

        ComponentLookup<MoverCD> moverLookup = GetComponentLookup<MoverCD>(true);

        EntityQuery splitterQuery = GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>());
        NativeArray<Entity> splitters = splitterQuery.ToEntityArray(Allocator.Temp);

        EntityQuery moveeQuery = GetEntityQuery(ComponentType.ReadOnly<MoveeCD>());
        NativeArray<Entity> moveeEntities = moveeQuery.ToEntityArray(Allocator.Temp);
        NativeArray<MoveeCD> movees = moveeQuery.ToComponentDataArray<MoveeCD>(Allocator.Temp);

        int logged = 0;

        for (int s = 0; s < splitters.Length; s++)
        {
            Entity orchestrator = splitters[s];

            if (SmartSplitterDebugSettings.OnlyLogLikelySplitters &&
                !SmartSplitterDebugUtility.LooksLikeSplitter(EntityManager, orchestrator, moverLookup))
            {
                continue;
            }

            DynamicBuffer<MoversWithSharedStateBuffer> buffer =
                EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

            if (buffer.Length == 0 || !moverLookup.HasComponent(buffer[0].moverEntity))
            {
                continue;
            }

            MoverCD firstMover = moverLookup[buffer[0].moverEntity];
            float centerX = firstMover.start.x;
            float centerY = firstMover.start.y;

            for (int i = 0; i < moveeEntities.Length; i++)
            {
                MoveeCD movee = movees[i];

                float x = movee.position.x;
                float y = movee.position.y;
                float distance = SmartSplitterDebugUtility.Distance2D(x, y, centerX, centerY);

                if (distance > SmartSplitterDebugSettings.MoveeNearSplitterRadius)
                {
                    continue;
                }

                Debug.Log(
                    $"[SmartSplitterDebug][MoveeNearSplitterProbe] orchestrator={orchestrator} movee={moveeEntities[i]} " +
                    $"position={movee.position} center=({centerX:0.00},{centerY:0.00}) distance={distance:0.00}"
                );

                logged++;

                if (logged >= SmartSplitterDebugSettings.MaxRowsPerProbe)
                {
                    Debug.Log("[SmartSplitterDebug][MoveeNearSplitterProbe] stopped at row limit.");
                    DisposeArrays(splitters, moveeEntities, movees);
                    return;
                }
            }
        }

        DisposeArrays(splitters, moveeEntities, movees);
    }

    private static void DisposeArrays(
        NativeArray<Entity> splitters,
        NativeArray<Entity> moveeEntities,
        NativeArray<MoveeCD> movees)
    {
        splitters.Dispose();
        moveeEntities.Dispose();
        movees.Dispose();
    }
}
