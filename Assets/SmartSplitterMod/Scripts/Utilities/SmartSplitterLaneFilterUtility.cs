using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class SmartSplitterLaneFilterUtility
{
  public const float DefaultTargetRadius = 4.0f;

  public static bool TryFindNearestSmartSplitter(
      out World world,
      out Entity splitter,
      out float distance,
      float targetRadius = DefaultTargetRadius)
  {
    world = Manager.ecs != null ? Manager.ecs.ServerWorld : null;
    splitter = Entity.Null;
    distance = float.MaxValue;

    if (world == null || !world.IsCreated || Manager.main == null || Manager.main.player == null)
    {
      return false;
    }

    EntityManager entityManager = world.EntityManager;
    EntityQuery query = entityManager.CreateEntityQuery(
        ComponentType.ReadOnly<SmartSplitterTag>(),
        ComponentType.ReadOnly<SmartSplitterLaneFiltersCD>(),
        ComponentType.ReadOnly<SmartSplitterOriginalOutputsCD>());

    using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
    using NativeArray<SmartSplitterOriginalOutputsCD> originalsData =
        query.ToComponentDataArray<SmartSplitterOriginalOutputsCD>(Allocator.Temp);
    query.Dispose();

    float3 playerPosition = Manager.main.player.WorldPosition;
    float bestDistanceSq = targetRadius * targetRadius;

    for (int i = 0; i < entities.Length; i++)
    {
      if (!TryGetSplitterCenter(entityManager, originalsData[i], out int2 center))
      {
        continue;
      }

      float dx = center.x - playerPosition.x;
      float dz = center.y - playerPosition.z;
      float distanceSq = dx * dx + dz * dz;

      if (distanceSq >= bestDistanceSq)
      {
        continue;
      }

      bestDistanceSq = distanceSq;
      splitter = entities[i];
    }

    if (splitter == Entity.Null)
    {
      return false;
    }

    distance = Mathf.Sqrt(bestDistanceSq);
    return true;
  }

  public static bool TryFindLookedAtSmartSplitter(
      out World world,
      out Entity splitter,
      out float distance,
      float targetRadius,
      float aimLineRadius)
  {
    return TryFindLookedAtSmartSplitter(
        out world,
        out splitter,
        out _,
        out distance,
        targetRadius,
        aimLineRadius);
  }

  public static bool TryFindLookedAtSmartSplitter(
      out World world,
      out Entity splitter,
      out int2 center,
      out float distance,
      float targetRadius,
      float aimLineRadius)
  {
    world = Manager.ecs != null ? Manager.ecs.ServerWorld : null;
    splitter = Entity.Null;
    center = default;
    distance = float.MaxValue;

    if (world == null ||
        !world.IsCreated ||
        Manager.main == null ||
        Manager.main.player == null ||
        Manager.ui == null ||
        Manager.ui.mouse == null)
    {
      return false;
    }

    EntityManager entityManager = world.EntityManager;
    EntityQuery query = entityManager.CreateEntityQuery(
        ComponentType.ReadOnly<SmartSplitterTag>(),
        ComponentType.ReadOnly<SmartSplitterLaneFiltersCD>(),
        ComponentType.ReadOnly<SmartSplitterOriginalOutputsCD>());

    using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
    using NativeArray<SmartSplitterOriginalOutputsCD> originalsData =
        query.ToComponentDataArray<SmartSplitterOriginalOutputsCD>(Allocator.Temp);
    query.Dispose();

    float3 playerPosition3 = Manager.main.player.WorldPosition;
    Vector2 playerPosition = new Vector2(playerPosition3.x, playerPosition3.z);
    Vector3 mousePosition3 = EntityMonoBehaviour.ToWorldFromRender(
        Manager.ui.mouse.GetMouseGameViewPosition());
    Vector2 mousePosition = new Vector2(mousePosition3.x, mousePosition3.z);

    float targetRadiusSq = targetRadius * targetRadius;
    float bestScore = float.MaxValue;
    float bestPlayerDistanceSq = targetRadiusSq;
    int2 bestCenter = default;
    float tileHalfExtent = Mathf.Max(0.55f, aimLineRadius * 0.5f);
    const float sameAxisTolerance = 0.72f;

    for (int i = 0; i < entities.Length; i++)
    {
      if (!TryGetSplitterCenter(entityManager, originalsData[i], out int2 candidateCenter))
      {
        continue;
      }

      Vector2 splitterCenter = new Vector2(candidateCenter.x, candidateCenter.y);
      Vector2 playerOffset = splitterCenter - playerPosition;
      float playerDistanceSq = playerOffset.sqrMagnitude;
      if (playerDistanceSq > targetRadiusSq)
      {
        continue;
      }

      bool isCardinalNeighbor =
          (Mathf.Abs(playerOffset.x) <= sameAxisTolerance && Mathf.Abs(playerOffset.y) <= targetRadius) ||
          (Mathf.Abs(playerOffset.y) <= sameAxisTolerance && Mathf.Abs(playerOffset.x) <= targetRadius);
      if (!isCardinalNeighbor)
      {
        continue;
      }

      Bounds2D hitbox = new Bounds2D(
          splitterCenter.x - tileHalfExtent,
          splitterCenter.y - tileHalfExtent,
          splitterCenter.x + tileHalfExtent,
          splitterCenter.y + tileHalfExtent);

      bool aimLineCrossesSplitter = SegmentIntersectsBounds(playerPosition, mousePosition, hitbox, out float hitT);
      if (!aimLineCrossesSplitter)
      {
        continue;
      }

      // Match vanilla-feeling targeting: the first valid tile along the
      // player-to-cursor line wins, with player distance as a stable tiebreaker.
      float score = hitT + playerDistanceSq * 0.01f;
      if (score > bestScore)
      {
        continue;
      }

      if (Mathf.Approximately(score, bestScore) &&
          playerDistanceSq >= bestPlayerDistanceSq)
      {
        continue;
      }

      bestScore = score;
      bestPlayerDistanceSq = playerDistanceSq;
      splitter = entities[i];
      bestCenter = candidateCenter;
    }

    if (splitter == Entity.Null)
    {
      return false;
    }

    distance = Mathf.Sqrt(bestPlayerDistanceSq);
    center = bestCenter;
    return true;
  }

  public static bool TryFindLookedAtPhysicalSplitter(
      out World world,
      out Entity splitter,
      out int2 center,
      out float distance,
      float targetRadius,
      float aimLineRadius)
  {
    world = null;
    splitter = Entity.Null;
    center = default;
    distance = float.MaxValue;

    if (!TryGetClientObjectWorld(out world) ||
        Manager.main == null ||
        Manager.main.player == null ||
        Manager.ui == null ||
        Manager.ui.mouse == null)
    {
      return false;
    }

    EntityManager entityManager = world.EntityManager;
    EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadOnly<LocalTransform>()
      },
      None = new[]
      {
        ComponentType.ReadOnly<EntityDestroyedCD>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
    using NativeArray<ObjectDataCD> objectData =
        query.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> transforms =
        query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
    query.Dispose();

    float3 playerPosition3 = Manager.main.player.WorldPosition;
    Vector2 playerPosition = new Vector2(playerPosition3.x, playerPosition3.z);
    Vector3 mousePosition3 = EntityMonoBehaviour.ToWorldFromRender(
        Manager.ui.mouse.GetMouseGameViewPosition());
    Vector2 mousePosition = new Vector2(mousePosition3.x, mousePosition3.z);

    float targetRadiusSq = targetRadius * targetRadius;
    float bestScore = float.MaxValue;
    float bestPlayerDistanceSq = targetRadiusSq;
    int2 bestCenter = default;
    float tileHalfExtent = Mathf.Max(0.55f, aimLineRadius * 0.5f);
    const float sameAxisTolerance = 0.72f;

    for (int i = 0; i < entities.Length; i++)
    {
      if (objectData[i].objectID != ObjectID.ConveyorBeltSplitter)
      {
        continue;
      }

      int2 candidateCenter = new int2(
          Mathf.RoundToInt(transforms[i].Position.x),
          Mathf.RoundToInt(transforms[i].Position.z));
      Vector2 splitterCenter = new Vector2(candidateCenter.x, candidateCenter.y);
      Vector2 playerOffset = splitterCenter - playerPosition;
      float playerDistanceSq = playerOffset.sqrMagnitude;
      if (playerDistanceSq > targetRadiusSq)
      {
        continue;
      }

      bool isCardinalNeighbor =
          (Mathf.Abs(playerOffset.x) <= sameAxisTolerance && Mathf.Abs(playerOffset.y) <= targetRadius) ||
          (Mathf.Abs(playerOffset.y) <= sameAxisTolerance && Mathf.Abs(playerOffset.x) <= targetRadius);
      if (!isCardinalNeighbor)
      {
        continue;
      }

      Bounds2D hitbox = new Bounds2D(
          splitterCenter.x - tileHalfExtent,
          splitterCenter.y - tileHalfExtent,
          splitterCenter.x + tileHalfExtent,
          splitterCenter.y + tileHalfExtent);

      if (!SegmentIntersectsBounds(playerPosition, mousePosition, hitbox, out float hitT))
      {
        continue;
      }

      float score = hitT + playerDistanceSq * 0.01f;
      if (score > bestScore)
      {
        continue;
      }

      if (Mathf.Approximately(score, bestScore) &&
          playerDistanceSq >= bestPlayerDistanceSq)
      {
        continue;
      }

      bestScore = score;
      bestPlayerDistanceSq = playerDistanceSq;
      splitter = entities[i];
      bestCenter = candidateCenter;
    }

    if (splitter == Entity.Null)
    {
      return false;
    }

    distance = Mathf.Sqrt(bestPlayerDistanceSq);
    center = bestCenter;
    return true;
  }

  private readonly struct Bounds2D
  {
    public Bounds2D(float minX, float minY, float maxX, float maxY)
    {
      MinX = minX;
      MinY = minY;
      MaxX = maxX;
      MaxY = maxY;
    }

    public readonly float MinX;
    public readonly float MinY;
    public readonly float MaxX;
    public readonly float MaxY;
  }

  public static void SetLaneFilter(
      EntityManager entityManager,
      Entity splitter,
      SmartSplitterLane lane,
      SmartSplitterLaneFilter filter)
  {
    SmartSplitterLaneFiltersCD filters = entityManager.GetComponentData<SmartSplitterLaneFiltersCD>(splitter);
    SetLaneFilterValue(ref filters, lane, filter);
    entityManager.SetComponentData(splitter, filters);
    SmartSplitterPersistence.TrySaveFilters(entityManager, splitter, filters);
  }

  public static void SetLaneFilterValue(
      ref SmartSplitterLaneFiltersCD filters,
      SmartSplitterLane lane,
      SmartSplitterLaneFilter filter)
  {
    switch (lane)
    {
      case SmartSplitterLane.Left:
        filters.Left = filter;
        break;

      case SmartSplitterLane.Center:
        filters.Center = filter;
        break;

      case SmartSplitterLane.Right:
        filters.Right = filter;
        break;
    }
  }

  public static void SetLaneToAny(EntityManager entityManager, Entity splitter, SmartSplitterLane lane)
  {
    SetLaneFilter(entityManager, splitter, lane, CreateAnyFilter());
  }

  public static void SetLaneToNone(EntityManager entityManager, Entity splitter, SmartSplitterLane lane)
  {
    SetLaneFilter(entityManager, splitter, lane, CreateNoneFilter());
  }

  public static void SetLaneToItem(
      EntityManager entityManager,
      Entity splitter,
      SmartSplitterLane lane,
      ObjectID objectID,
      int variation)
  {
    SetLaneFilter(entityManager, splitter, lane, CreateItemFilter(objectID, variation));
  }

  public static SmartSplitterLaneFilter GetLaneFilter(SmartSplitterLaneFiltersCD filters, SmartSplitterLane lane)
  {
    switch (lane)
    {
      case SmartSplitterLane.Left:
        return filters.Left;

      case SmartSplitterLane.Center:
        return filters.Center;

      case SmartSplitterLane.Right:
        return filters.Right;

      default:
        return CreateAnyFilter();
    }
  }

  public static SmartSplitterLaneFilter GetNextProofFilter(SmartSplitterLaneFilter current)
  {
    if (current.Mode == SmartSplitterLaneFilterMode.Any)
    {
      return CreateNoneFilter();
    }

    if (current.Mode == SmartSplitterLaneFilterMode.None)
    {
      return CreateItemFilter(ObjectID.WallDirtBlock, 0);
    }

    return CreateAnyFilter();
  }

  public static SmartSplitterLaneFilter CreateAnyFilter()
  {
    return new SmartSplitterLaneFilter
    {
      Mode = SmartSplitterLaneFilterMode.Any,
      FilterObject = ObjectID.None,
      FilterVariation = 0
    };
  }

  public static SmartSplitterLaneFilter CreateNoneFilter()
  {
    return new SmartSplitterLaneFilter
    {
      Mode = SmartSplitterLaneFilterMode.None,
      FilterObject = ObjectID.None,
      FilterVariation = 0
    };
  }

  public static SmartSplitterLaneFilter CreateItemFilter(ObjectID objectID, int variation)
  {
    if (objectID == ObjectID.None)
    {
      return CreateNoneFilter();
    }

    return new SmartSplitterLaneFilter
    {
      Mode = SmartSplitterLaneFilterMode.Item,
      FilterObject = objectID,
      FilterVariation = variation
    };
  }

  public static string FormatFilter(SmartSplitterLaneFilter filter)
  {
    if (filter.Mode == SmartSplitterLaneFilterMode.Item)
    {
      return $"{filter.Mode}({filter.FilterObject}, variation={filter.FilterVariation})";
    }

    return filter.Mode.ToString();
  }

  public static bool TryGetSplitterCenter(
      EntityManager entityManager,
      SmartSplitterOriginalOutputsCD originals,
      out int2 center)
  {
    if (TryGetSplitterCenterFromCachedOutputs(originals, out center))
    {
      return true;
    }

    return TryGetSplitterCenterFromCurrentMovers(entityManager, originals, out center);
  }

  public static bool TryGetSplitterCenterFromCachedOutputs(
      SmartSplitterOriginalOutputsCD originals,
      out int2 center)
  {
    center = default;

    if (!originals.HasOriginalOutputs)
    {
      return false;
    }

    center = new int2(
        Mathf.RoundToInt((originals.LeftCachedStart.x + originals.RightCachedStart.x) * 0.5f),
        Mathf.RoundToInt((originals.LeftCachedStart.y + originals.RightCachedStart.y) * 0.5f));

    return true;
  }

  public static bool TryGetSplitterCenterFromCurrentMovers(
      EntityManager entityManager,
      SmartSplitterOriginalOutputsCD originals,
      out int2 center)
  {
    center = default;

    if (!originals.HasOriginalOutputs ||
        originals.LeftMoverEntity == Entity.Null ||
        originals.RightMoverEntity == Entity.Null ||
        !entityManager.Exists(originals.LeftMoverEntity) ||
        !entityManager.Exists(originals.RightMoverEntity) ||
        !entityManager.HasComponent<MoverCD>(originals.LeftMoverEntity) ||
        !entityManager.HasComponent<MoverCD>(originals.RightMoverEntity))
    {
      return false;
    }

    MoverCD leftMover = entityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
    MoverCD rightMover = entityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

    center = new int2(
        Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
        Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

    return true;
  }

  public static SmartSplitterLaneFiltersCD CreateDefaultLaneFilters()
  {
    SmartSplitterLaneFilter any = CreateAnyFilter();
    return new SmartSplitterLaneFiltersCD
    {
      Left = any,
      Center = any,
      Right = any
    };
  }

  public static bool TryFindSmartSplitterAtCenter(
      World world,
      int2 center,
      out Entity splitter)
  {
    splitter = Entity.Null;

    if (world == null || !world.IsCreated)
    {
      return false;
    }

    EntityManager entityManager = world.EntityManager;
    EntityQuery query = entityManager.CreateEntityQuery(
        ComponentType.ReadOnly<SmartSplitterTag>(),
        ComponentType.ReadOnly<SmartSplitterLaneFiltersCD>(),
        ComponentType.ReadOnly<SmartSplitterOriginalOutputsCD>());

    using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
    using NativeArray<SmartSplitterOriginalOutputsCD> originalsData =
        query.ToComponentDataArray<SmartSplitterOriginalOutputsCD>(Allocator.Temp);
    query.Dispose();

    for (int i = 0; i < entities.Length; i++)
    {
      if (TryGetSplitterCenter(entityManager, originalsData[i], out int2 candidateCenter) &&
          candidateCenter.x == center.x &&
          candidateCenter.y == center.y)
      {
        splitter = entities[i];
        return true;
      }
    }

    return false;
  }

  private static bool TryGetClientObjectWorld(out World world)
  {
    world = null;

    if (Manager.ecs != null && Manager.ecs.ClientWorld != null && Manager.ecs.ClientWorld.IsCreated)
    {
      world = Manager.ecs.ClientWorld;
      return true;
    }

    World defaultWorld = World.DefaultGameObjectInjectionWorld;
    if (defaultWorld != null && defaultWorld.IsCreated)
    {
      world = defaultWorld;
      return true;
    }

    foreach (World candidate in World.All)
    {
      if (candidate != null && candidate.IsCreated)
      {
        world = candidate;
        return true;
      }
    }

    return false;
  }

  private static bool SegmentIntersectsBounds(
      Vector2 segmentStart,
      Vector2 segmentEnd,
      Bounds2D bounds,
      out float hitT)
  {
    hitT = 0.0f;
    Vector2 direction = segmentEnd - segmentStart;
    float tMin = 0.0f;
    float tMax = 1.0f;

    if (!ClipSegmentAxis(segmentStart.x, direction.x, bounds.MinX, bounds.MaxX, ref tMin, ref tMax) ||
        !ClipSegmentAxis(segmentStart.y, direction.y, bounds.MinY, bounds.MaxY, ref tMin, ref tMax))
    {
      return false;
    }

    hitT = tMin;
    return true;
  }

  private static bool ClipSegmentAxis(
      float start,
      float direction,
      float min,
      float max,
      ref float tMin,
      ref float tMax)
  {
    if (Mathf.Abs(direction) < 0.0001f)
    {
      return start >= min && start <= max;
    }

    float inv = 1.0f / direction;
    float t1 = (min - start) * inv;
    float t2 = (max - start) * inv;
    if (t1 > t2)
    {
      float temp = t1;
      t1 = t2;
      t2 = temp;
    }

    tMin = Mathf.Max(tMin, t1);
    tMax = Mathf.Min(tMax, t2);
    return tMin <= tMax;
  }
}
