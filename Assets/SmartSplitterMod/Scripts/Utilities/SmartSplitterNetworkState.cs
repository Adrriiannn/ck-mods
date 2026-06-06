using System.Collections.Generic;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

public static class SmartSplitterNetworkState
{
  private static readonly Dictionary<long, SmartSplitterLaneFiltersCD> FiltersByCenter = new();
  private static readonly Dictionary<long, double> LastFilterRequestAt = new();
  private const double FilterRequestCooldownSeconds = 0.50d;

  public static void Reset()
  {
    FiltersByCenter.Clear();
    LastFilterRequestAt.Clear();
  }

  public static bool TryGetFilters(int2 center, out SmartSplitterLaneFiltersCD filters)
  {
    return FiltersByCenter.TryGetValue(GetKey(center), out filters);
  }

  public static void RememberFilters(int2 center, SmartSplitterLaneFiltersCD filters)
  {
    FiltersByCenter[GetKey(center)] = filters;
  }

  public static void RememberLaneFilter(int2 center, SmartSplitterLane lane, SmartSplitterLaneFilter filter)
  {
    if (!FiltersByCenter.TryGetValue(GetKey(center), out SmartSplitterLaneFiltersCD filters))
    {
      filters = SmartSplitterLaneFilterUtility.CreateDefaultLaneFilters();
    }

    SmartSplitterLaneFilterUtility.SetLaneFilterValue(ref filters, lane, filter);
    RememberFilters(center, filters);
  }

  public static void RequestFilters(int2 center)
  {
    if (!TryGetClientEntityManager(out EntityManager entityManager))
    {
      return;
    }

    double now = Time.realtimeSinceStartup;
    long key = GetKey(center);
    if (LastFilterRequestAt.TryGetValue(key, out double lastRequestAt) &&
        now - lastRequestAt < FilterRequestCooldownSeconds)
    {
      return;
    }

    LastFilterRequestAt[key] = now;

    EntityArchetype archetype = entityManager.CreateArchetype(
        typeof(SmartSplitterFilterRequestRpc),
        typeof(SendRpcCommandRequest));
    Entity entity = entityManager.CreateEntity(archetype);
    entityManager.SetComponentData(entity, new SmartSplitterFilterRequestRpc
    {
      CenterX = center.x,
      CenterY = center.y
    });
  }

  public static void SendLaneFilter(int2 center, SmartSplitterLane lane, SmartSplitterLaneFilter filter)
  {
    RememberLaneFilter(center, lane, filter);

    if (!TryGetClientEntityManager(out EntityManager entityManager))
    {
      return;
    }

    EntityArchetype archetype = entityManager.CreateArchetype(
        typeof(SmartSplitterSetLaneFilterRpc),
        typeof(SendRpcCommandRequest));
    Entity entity = entityManager.CreateEntity(archetype);
    entityManager.SetComponentData(entity, new SmartSplitterSetLaneFilterRpc
    {
      CenterX = center.x,
      CenterY = center.y,
      Lane = (byte)lane,
      Mode = (byte)filter.Mode,
      ObjectID = (int)filter.FilterObject,
      Variation = filter.FilterVariation
    });
  }

  private static bool TryGetClientEntityManager(out EntityManager entityManager)
  {
    entityManager = default;

    World world = API.Client != null ? API.Client.World : null;
    if ((world == null || !world.IsCreated) && Manager.ecs != null)
    {
      world = Manager.ecs.ClientWorld;
    }

    if (world == null || !world.IsCreated)
    {
      return false;
    }

    entityManager = world.EntityManager;
    return true;
  }

  private static long GetKey(int2 center)
  {
    return ((long)center.x << 32) ^ (uint)center.y;
  }
}
