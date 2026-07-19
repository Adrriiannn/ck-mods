using System;
using ExpandNullforge.Api;
using ExpandNullforge.Networking;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public partial class DimensionPortalMapMarkerScopeSystem : SystemBase
  {
    private const double CurrentDimensionPollSeconds = 0.20d;
    private const double FullMarkerRescopeSeconds = 1.0d;

    private EntityQuery markerQuery;
    private double nextCurrentDimensionPollAt;
    private double nextFullMarkerRescopeAt;
    private string scopedDimensionId = string.Empty;
    private int scopedMarkerCount = -1;

    protected override void OnCreate()
    {
      markerQuery = GetEntityQuery(
          ComponentType.ReadOnly<MapMarkerCD>(),
          ComponentType.ReadWrite<MapMarkerActivatedCD>(),
          ComponentType.ReadOnly<LocalTransform>());
      RequireForUpdate(markerQuery);
    }

    protected override void OnUpdate()
    {
      double now = World.Time.ElapsedTime;
      if (now < nextCurrentDimensionPollAt)
      {
        return;
      }

      nextCurrentDimensionPollAt = now + CurrentDimensionPollSeconds;

      IDimensionService service;
      if (!DimensionApi.TryGetService(out service) || service == null)
      {
        return;
      }

      string currentDimensionId;
      if (!TryGetCurrentDimensionId(service, out currentDimensionId))
      {
        return;
      }

      int markerCount = markerQuery.CalculateEntityCount();
      bool dimensionChanged =
          !string.Equals(scopedDimensionId, currentDimensionId, StringComparison.Ordinal);
      bool markerCountChanged = scopedMarkerCount != markerCount;
      if (!dimensionChanged &&
          !markerCountChanged &&
          now < nextFullMarkerRescopeAt)
      {
        return;
      }

      using NativeArray<Entity> entities = markerQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<MapMarkerCD> markers =
          markerQuery.ToComponentDataArray<MapMarkerCD>(Allocator.Temp);
      using NativeArray<LocalTransform> transforms =
          markerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

      for (int i = 0; i < entities.Length; i++)
      {
        MapMarkerCD marker = markers[i];
        if (marker.mapMarkerType != MapMarkerType.Portal &&
            marker.mapMarkerType != MapMarkerType.Waypoint)
        {
          continue;
        }

        DimensionContext markerContext =
            service.GetCoordinateContextForAbsolute(transforms[i].Position.xz);
        if (!markerContext.IsKnown)
        {
          continue;
        }

        bool hidden =
            !string.Equals(
                markerContext.DimensionId,
                currentDimensionId,
                StringComparison.Ordinal);
        MapMarkerActivatedCD activated =
            EntityManager.GetComponentData<MapMarkerActivatedCD>(entities[i]);
        if (activated.Hidden == hidden)
        {
          continue;
        }

        activated.Hidden = hidden;
        EntityManager.SetComponentData(entities[i], activated);
      }

      scopedDimensionId = currentDimensionId;
      scopedMarkerCount = markerCount;
      nextFullMarkerRescopeAt = now + FullMarkerRescopeSeconds;
    }

    private static bool TryGetCurrentDimensionId(
        IDimensionService service,
        out string dimensionId)
    {
      DimensionPlayerContextNetworkSnapshot snapshot;
      if (DimensionPlayerContextNetworkState.TryGetCurrentSnapshot(out snapshot) &&
          snapshot.IsKnown &&
          !string.IsNullOrEmpty(snapshot.Context.DimensionId))
      {
        dimensionId = snapshot.Context.DimensionId;
        return true;
      }

      if (Manager.main == null || Manager.main.player == null)
      {
        dimensionId = string.Empty;
        return false;
      }

      Vector3 playerPosition = Manager.main.player.WorldPosition;
      DimensionContext playerContext =
          service.GetCoordinateContextForAbsolute(new float2(playerPosition.x, playerPosition.z));
      dimensionId = playerContext.DimensionId;
      return playerContext.IsKnown && !string.IsNullOrEmpty(dimensionId);
    }
  }
}
