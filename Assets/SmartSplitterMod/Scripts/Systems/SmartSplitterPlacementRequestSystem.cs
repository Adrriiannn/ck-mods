using Inventory;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[WorldSystemFilter(
    WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation,
    WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(EndPredictedSimulationSystemGroup))]
[UpdateBefore(typeof(InventorySystemGroup))]
public partial class SmartSplitterPlacementRequestSystem : SystemBase
{
  private EntityQuery _inventoryChangeQuery;

  protected override void OnCreate()
  {
    _inventoryChangeQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[] { ComponentType.ReadWrite<InventoryChangeBuffer>() },
      Options = EntityQueryOptions.IncludeSystems
    });

    RequireForUpdate(_inventoryChangeQuery);
  }

  protected override void OnUpdate()
  {
    if (!SmartSplitterPlacementCompatibility.ShouldRemapInventoryPlacementRequest)
    {
      return;
    }

    BufferLookup<ContainedObjectsBuffer> containedObjectsLookup =
        GetBufferLookup<ContainedObjectsBuffer>(true);

    using NativeArray<Entity> entities =
        _inventoryChangeQuery.ToEntityArray(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      DynamicBuffer<InventoryChangeBuffer> inventoryChanges =
          EntityManager.GetBuffer<InventoryChangeBuffer>(entities[i]);

      for (int j = 0; j < inventoryChanges.Length; j++)
      {
        InventoryChangeBuffer entry = inventoryChanges[j];
        InventoryChangeData change = entry.inventoryChangeData;

        if (!ShouldRemapPlacement(entry, change, containedObjectsLookup))
        {
          continue;
        }

        int requestedForward =
            SmartSplitterOrientationUtility.NormalizeVariation(change.index2);
        int correctedPlacement =
            SmartSplitterOrientationUtility.GetPlacementVariationForSmartForwardVariation(
                requestedForward);

        if (correctedPlacement == change.index2)
        {
          continue;
        }

        int2 correctedDirection =
            DirectionBasedOnVariationCD.GetDirectionFromVariation(
                correctedPlacement,
                false);

        change.index2 = correctedPlacement;
        change.position2 =
            new float3(correctedDirection.x, 0f, correctedDirection.y);

        entry.inventoryChangeData = change;
        inventoryChanges[j] = entry;
      }
    }
  }

  private static bool ShouldRemapPlacement(
      InventoryChangeBuffer entry,
      InventoryChangeData change,
      BufferLookup<ContainedObjectsBuffer> containedObjectsLookup)
  {
    if (change.inventoryAction != InventoryAction.ConsumeEntityAt ||
        change.index2 < 0 ||
        change.index2 > 3 ||
        change.bool1 ||
        entry.playerEntity == Entity.Null ||
        entry.playerEntity != change.inventory1 ||
        !containedObjectsLookup.HasBuffer(change.inventory1))
    {
      return false;
    }

    DynamicBuffer<ContainedObjectsBuffer> containedObjects =
        containedObjectsLookup[change.inventory1];

    if (change.index1 < 0 || change.index1 >= containedObjects.Length)
    {
      return false;
    }

    return containedObjects[change.index1].objectID == ObjectID.ConveyorBeltSplitter;
  }
}
