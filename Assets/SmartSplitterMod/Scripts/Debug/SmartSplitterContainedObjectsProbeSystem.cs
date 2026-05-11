using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterContainedObjectsProbeSystem : SystemBase
{
    private double _nextLogTime;

    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<ContainedObjectsBuffer>()));
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableContainedObjectsProbe)
        {
            return;
        }

        if (!SmartSplitterDebugSettings.ShouldRunProbe(World.Time.ElapsedTime, ref _nextLogTime))
        {
            return;
        }

        EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<ContainedObjectsBuffer>());
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

        int logged = 0;

        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            DynamicBuffer<ContainedObjectsBuffer> contained = EntityManager.GetBuffer<ContainedObjectsBuffer>(entity);

            if (contained.Length == 0)
            {
                continue;
            }

            for (int c = 0; c < contained.Length; c++)
            {
                var objectData = contained[c].objectData;

                if (SmartSplitterDebugSettings.OnlyLogDirtOrTurfItems &&
                    !SmartSplitterDebugUtility.IsDirtOrTurf(objectData.objectID))
                {
                    continue;
                }

                Debug.Log(
                    $"[SmartSplitterDebug][ContainedObjectsProbe] entity={entity} slot={c} " +
                    $"item={objectData.objectID} variation={objectData.variation} amount={objectData.amount}"
                );

                logged++;

                if (logged >= SmartSplitterDebugSettings.MaxRowsPerProbe)
                {
                    Debug.Log("[SmartSplitterDebug][ContainedObjectsProbe] stopped at row limit.");
                    entities.Dispose();
                    return;
                }
            }
        }

        entities.Dispose();
    }
}
