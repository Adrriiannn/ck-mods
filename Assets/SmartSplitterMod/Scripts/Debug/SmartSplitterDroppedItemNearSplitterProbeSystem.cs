using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterDroppedItemNearSplitterProbeSystem : SystemBase
{
    private double _nextLogTime;

    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>()));
    }

    protected override void OnUpdate()
    {
        if (!SmartSplitterDebugSettings.EnableDroppedItemNearSplitterProbe)
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

        EntityQuery droppedItemQuery = GetEntityQuery(
            ComponentType.ReadOnly<ObjectDataCD>(),
            ComponentType.ReadOnly<ContainedObjectsBuffer>(),
            ComponentType.ReadOnly<LocalTransform>()
        );

        NativeArray<Entity> itemEntities = droppedItemQuery.ToEntityArray(Allocator.Temp);
        NativeArray<ObjectDataCD> outerObjects = droppedItemQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
        NativeArray<LocalTransform> transforms = droppedItemQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

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

            for (int i = 0; i < itemEntities.Length; i++)
            {
                if (outerObjects[i].objectID != ObjectID.DroppedItem)
                {
                    continue;
                }

                Entity dropped = itemEntities[i];
                DynamicBuffer<ContainedObjectsBuffer> contained =
                    EntityManager.GetBuffer<ContainedObjectsBuffer>(dropped);

                if (contained.Length == 0)
                {
                    continue;
                }

                var inner = contained[0].objectData;

                if (SmartSplitterDebugSettings.OnlyLogDirtOrTurfItems &&
                    !SmartSplitterDebugUtility.IsDirtOrTurf(inner.objectID))
                {
                    continue;
                }

                float x = transforms[i].Position.x;
                float y = transforms[i].Position.z;
                float distance = SmartSplitterDebugUtility.Distance2D(x, y, centerX, centerY);

                if (distance > SmartSplitterDebugSettings.NearbyItemRadius)
                {
                    continue;
                }

                Debug.Log(
                    $"[SmartSplitterDebug][DroppedItemNearSplitterProbe] orchestrator={orchestrator} droppedEntity={dropped} " +
                    $"item={inner.objectID} variation={inner.variation} amount={inner.amount} " +
                    $"pos=({x:0.00},{y:0.00}) splitterCenter=({centerX:0.00},{centerY:0.00}) distance={distance:0.00}"
                );

                logged++;

                if (logged >= SmartSplitterDebugSettings.MaxRowsPerProbe)
                {
                    Debug.Log("[SmartSplitterDebug][DroppedItemNearSplitterProbe] stopped at row limit.");
                    DisposeArrays(splitters, itemEntities, outerObjects, transforms);
                    return;
                }
            }
        }

        DisposeArrays(splitters, itemEntities, outerObjects, transforms);
    }

    private static void DisposeArrays(
        NativeArray<Entity> splitters,
        NativeArray<Entity> itemEntities,
        NativeArray<ObjectDataCD> outerObjects,
        NativeArray<LocalTransform> transforms)
    {
        splitters.Dispose();
        itemEntities.Dispose();
        outerObjects.Dispose();
        transforms.Dispose();
    }
}
