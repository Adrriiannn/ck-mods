using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(RunSimulationSystemGroup))]
[UpdateAfter(typeof(SpawnEnvironmentObjectsPeriodicallySystem))]
public partial class ChunkLoaderPeriodicRespawnSystem : SystemBase
{
  private readonly List<ChunkCoordinate> _activeCoordinates = new();
  private int _cursor;
  private double _nextSpawnAt = 30.0d;

  protected override void OnUpdate()
  {
    double now = SystemAPI.Time.ElapsedTime;
    if (now < _nextSpawnAt)
    {
      return;
    }

    if (IsCreativeWorld())
    {
      _nextSpawnAt = now + 30.0d;
      return;
    }

    EntityQuery inProgressQuery =
        EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SpawnEnvironmentObjectsCD>());
    bool spawnAlreadyInProgress = !inProgressQuery.IsEmptyIgnoreFilter;
    inProgressQuery.Dispose();
    if (spawnAlreadyInProgress)
    {
      _nextSpawnAt = now + 1.0d;
      return;
    }

    ChunkLoaderSimulationRegions.GetActiveCoordinates(_activeCoordinates);
    if (_activeCoordinates.Count == 0)
    {
      _cursor = 0;
      _nextSpawnAt = now + 5.0d;
      return;
    }

    int totalCells = _activeCoordinates.Count;
    _cursor = (_cursor + 1) % totalCells;
    ChunkCoordinate coordinate = _activeCoordinates[_cursor];
    int2 position = coordinate.Origin;

    if (IsSubMapLoaded(coordinate) && !HasActualPlayerWithin(position, 200.0f))
    {
      Entity entity = EntityManager.CreateEntity(typeof(SpawnEnvironmentObjectsCD));
      EntityManager.SetComponentData(entity, new SpawnEnvironmentObjectsCD
      {
        respawn = true,
        position = position
      });
    }

    _nextSpawnAt = now + 900.0d / totalCells;
  }

  private bool IsCreativeWorld()
  {
    EntityQuery query =
        EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldGenerationTypeCD>());
    bool creative = false;
    if (query.CalculateEntityCount() == 1)
    {
      creative =
          query.GetSingleton<WorldGenerationTypeCD>().Value ==
          WorldGenerationType.Creative;
    }
    query.Dispose();
    return creative;
  }

  private bool IsSubMapLoaded(ChunkCoordinate coordinate)
  {
    EntityQuery query = EntityManager.CreateEntityQuery(
        ComponentType.ReadOnly<SubMapCD>());
    using NativeArray<SubMapCD> subMaps =
        query.ToComponentDataArray<SubMapCD>(Allocator.Temp);
    query.Dispose();

    for (int i = 0; i < subMaps.Length; i++)
    {
      int2 parentSubMap = coordinate.ParentSubMapIndex;
      if (subMaps[i].index.x == parentSubMap.x &&
          subMaps[i].index.y == parentSubMap.y)
      {
        return true;
      }
    }

    return false;
  }

  private bool HasActualPlayerWithin(int2 position, float radius)
  {
    EntityQuery query = EntityManager.CreateEntityQuery(
        ComponentType.ReadOnly<PlayerGhost>(),
        ComponentType.ReadOnly<LocalTransform>());
    using NativeArray<LocalTransform> transforms =
        query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
    query.Dispose();

    float radiusSq = radius * radius;
    float2 point = new float2(position.x, position.y);
    for (int i = 0; i < transforms.Length; i++)
    {
      float2 player = new float2(
          transforms[i].Position.x,
          transforms[i].Position.z);
      if (math.distancesq(point, player) < radiusSq)
      {
        return true;
      }
    }

    return false;
  }
}
