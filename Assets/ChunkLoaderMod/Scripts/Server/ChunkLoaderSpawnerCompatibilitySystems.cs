using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SpawnAroundEntitySystem))]
public partial class ChunkLoaderPrepareSpawnersSystem : SystemBase
{
  private EntityQuery _spawnerQuery;
  private EntityQuery _playerQuery;

  protected override void OnCreate()
  {
    _spawnerQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadWrite<SpawnEntitiesAroundEntityCD>()
      },
      None = new[]
      {
        ComponentType.ReadOnly<ChunkLoaderTemporarilyRelaxedSpawnerCD>()
      }
    });
    _playerQuery = GetEntityQuery(
        ComponentType.ReadOnly<PlayerGhost>(),
        ComponentType.ReadOnly<LocalTransform>());
  }

  protected override void OnUpdate()
  {
    if (ChunkLoaderSimulationRegions.Count == 0)
    {
      return;
    }

    using NativeArray<Entity> entities =
        _spawnerQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<SpawnEntitiesAroundEntityCD> spawners =
        _spawnerQuery.ToComponentDataArray<SpawnEntitiesAroundEntityCD>(
            Allocator.Temp);
    using NativeArray<LocalTransform> playerTransforms =
        _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      SpawnEntitiesAroundEntityCD spawner = spawners[i];
      if (!spawner.spawnCloseToPlayers ||
          !TryGetSpawnerPosition(entities[i], spawner, out float2 position) ||
          !ChunkLoaderSimulationRegions.Contains(position) ||
          HasActualPlayerNear(
              position,
              spawner.maxSpawnDistance + 21.0f,
              playerTransforms))
      {
        continue;
      }

      spawner.spawnCloseToPlayers = false;
      EntityManager.SetComponentData(entities[i], spawner);
      EntityManager.AddComponent<ChunkLoaderTemporarilyRelaxedSpawnerCD>(entities[i]);
    }
  }

  private bool TryGetSpawnerPosition(
      Entity spawnerEntity,
      SpawnEntitiesAroundEntityCD spawner,
      out float2 position)
  {
    Entity transformEntity = spawner.mainEntity != Entity.Null
        ? spawner.mainEntity
        : spawnerEntity;
    if (!EntityManager.Exists(transformEntity) ||
        !EntityManager.HasComponent<LocalTransform>(transformEntity))
    {
      position = default;
      return false;
    }

    LocalTransform transform =
        EntityManager.GetComponentData<LocalTransform>(transformEntity);
    position = new float2(transform.Position.x, transform.Position.z);
    return true;
  }

  private static bool HasActualPlayerNear(
      float2 position,
      float radius,
      NativeArray<LocalTransform> transforms)
  {
    float radiusSq = radius * radius;
    for (int i = 0; i < transforms.Length; i++)
    {
      float2 playerPosition = new float2(
          transforms[i].Position.x,
          transforms[i].Position.z);
      if (math.distancesq(position, playerPosition) <= radiusSq)
      {
        return true;
      }
    }

    return false;
  }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SpawnAroundEntitySystem))]
public partial class ChunkLoaderRestoreSpawnersSystem : SystemBase
{
  private EntityQuery _relaxedSpawnerQuery;

  protected override void OnCreate()
  {
    _relaxedSpawnerQuery = GetEntityQuery(
        ComponentType.ReadWrite<SpawnEntitiesAroundEntityCD>(),
        ComponentType.ReadOnly<ChunkLoaderTemporarilyRelaxedSpawnerCD>());
  }

  protected override void OnDestroy()
  {
    if (World != null && World.IsCreated)
    {
      RestoreAllTemporarilyRelaxedSpawners();
    }
  }

  protected override void OnUpdate()
  {
    RestoreAllTemporarilyRelaxedSpawners();
  }

  private void RestoreAllTemporarilyRelaxedSpawners()
  {
    using NativeArray<Entity> entities =
        _relaxedSpawnerQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<SpawnEntitiesAroundEntityCD> spawners =
        _relaxedSpawnerQuery
            .ToComponentDataArray<SpawnEntitiesAroundEntityCD>(
                Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      SpawnEntitiesAroundEntityCD spawner = spawners[i];
      spawner.spawnCloseToPlayers = true;
      EntityManager.SetComponentData(entities[i], spawner);
      EntityManager.RemoveComponent<ChunkLoaderTemporarilyRelaxedSpawnerCD>(entities[i]);
    }
  }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(RunSimulationSystemGroup))]
[UpdateBefore(typeof(DestroyEntitiesWhenNoNearbyPlayerSystem))]
public partial class ChunkLoaderTemporaryEntityRetentionSystem : SystemBase
{
  private EntityQuery _retentionQuery;

  protected override void OnCreate()
  {
    _retentionQuery = GetEntityQuery(
        ComponentType.ReadWrite<DestroyEntityWhenNoNearbyPlayerCD>(),
        ComponentType.ReadOnly<LocalTransform>());
  }

  protected override void OnUpdate()
  {
    if (ChunkLoaderSimulationRegions.Count == 0)
    {
      return;
    }

    using NativeArray<Entity> entities =
        _retentionQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<DestroyEntityWhenNoNearbyPlayerCD> retention =
        _retentionQuery
            .ToComponentDataArray<DestroyEntityWhenNoNearbyPlayerCD>(
                Allocator.Temp);
    using NativeArray<LocalTransform> transforms =
        _retentionQuery.ToComponentDataArray<LocalTransform>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      float2 position = new float2(
          transforms[i].Position.x,
          transforms[i].Position.z);
      if (!ChunkLoaderSimulationRegions.Contains(position))
      {
        continue;
      }

      DestroyEntityWhenNoNearbyPlayerCD data = retention[i];
      if (data.timer.isRunning)
      {
        data.timer.Stop();
        EntityManager.SetComponentData(entities[i], data);
      }
    }
  }
}
