using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[WorldSystemFilter(
    WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation,
    WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(DestroyEntityIfPlacementNotValidSystem))]
public partial class SmartSplitterPlacementDirectionSyncSystem : SystemBase
{
  private EntityQuery _pendingPlacementQuery;

  protected override void OnCreate()
  {
    _pendingPlacementQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadWrite<DirectionCD>(),
        ComponentType.ReadOnly<DestroyEntityIfPlacementNotValidCD>()
      }
    });

    RequireForUpdate(_pendingPlacementQuery);
  }

  protected override void OnUpdate()
  {
    if (!SmartSplitterPlacementCompatibility.ShouldRemapEntityCreationVariation)
    {
      return;
    }

    using NativeArray<Entity> entities =
        _pendingPlacementQuery.ToEntityArray(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      Entity entity = entities[i];
      ObjectDataCD objectData = EntityManager.GetComponentData<ObjectDataCD>(entity);

      if (objectData.objectID != ObjectID.ConveyorBeltSplitter ||
          objectData.variation < 0 ||
          objectData.variation > 3)
      {
        continue;
      }

      int2 direction =
          DirectionBasedOnVariationCD.GetDirectionFromVariation(
              objectData.variation,
              false);

      DirectionCD directionCD = EntityManager.GetComponentData<DirectionCD>(entity);
      float3 correctedDirection = new float3(direction.x, 0f, direction.y);

      if (math.any(directionCD.direction != correctedDirection))
      {
        directionCD.direction = correctedDirection;
        EntityManager.SetComponentData(entity, directionCD);
      }
    }
  }
}
