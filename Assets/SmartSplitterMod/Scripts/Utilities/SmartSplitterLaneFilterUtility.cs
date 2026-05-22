using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
    world = Manager.ecs != null ? Manager.ecs.ServerWorld : null;
    splitter = Entity.Null;
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
    Vector3 mousePosition3 = Manager.ui.mouse.GetMouseGameViewPosition();
    Vector2 mousePosition = new Vector2(mousePosition3.x, mousePosition3.z);

    Vector2 aimVector = mousePosition - playerPosition;
    if (aimVector.sqrMagnitude < 0.001f)
    {
      return false;
    }

    float targetRadiusSq = targetRadius * targetRadius;
    float cursorRadius = SmartSplitterDebugSettings.SmartSplitterPanelCursorRadius;
    float cursorRadiusSq = cursorRadius * cursorRadius;
    float aimLineRadiusSq = aimLineRadius * aimLineRadius;
    float bestScore = float.MaxValue;
    float bestPlayerDistanceSq = targetRadiusSq;

    for (int i = 0; i < entities.Length; i++)
    {
      if (!TryGetSplitterCenter(entityManager, originalsData[i], out int2 center))
      {
        continue;
      }

      Vector2 splitterCenter = new Vector2(center.x, center.y);
      float playerDistanceSq = (splitterCenter - playerPosition).sqrMagnitude;
      if (playerDistanceSq > targetRadiusSq)
      {
        continue;
      }

      float cursorDistanceSq = (splitterCenter - mousePosition).sqrMagnitude;
      float aimDistanceSq = DistanceToSegmentSq(splitterCenter, playerPosition, mousePosition);

      bool cursorIsOnSplitter = cursorDistanceSq <= cursorRadiusSq;
      bool aimLineCrossesSplitter = aimDistanceSq <= aimLineRadiusSq;
      if (!cursorIsOnSplitter && !aimLineCrossesSplitter)
      {
        continue;
      }

      // Prefer the splitter directly under the cursor, then fall back to the aim line.
      float score = cursorIsOnSplitter ? cursorDistanceSq : cursorRadiusSq + aimDistanceSq;
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
    }

    if (splitter == Entity.Null)
    {
      return false;
    }

    distance = Mathf.Sqrt(bestPlayerDistanceSq);
    return true;
  }

  public static void SetLaneFilter(
      EntityManager entityManager,
      Entity splitter,
      SmartSplitterLane lane,
      SmartSplitterLaneFilter filter)
  {
    SmartSplitterLaneFiltersCD filters = entityManager.GetComponentData<SmartSplitterLaneFiltersCD>(splitter);

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

    entityManager.SetComponentData(splitter, filters);
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

  private static float DistanceToSegmentSq(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
  {
    Vector2 segment = segmentEnd - segmentStart;
    float lengthSq = segment.sqrMagnitude;
    if (lengthSq <= 0.0001f)
    {
      return (point - segmentStart).sqrMagnitude;
    }

    float t = Vector2.Dot(point - segmentStart, segment) / lengthSq;
    t = Mathf.Clamp01(t);
    Vector2 closest = segmentStart + segment * t;
    return (point - closest).sqrMagnitude;
  }
}
