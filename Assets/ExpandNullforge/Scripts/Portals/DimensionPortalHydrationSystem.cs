using System;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Portals
{
  [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateBefore(typeof(DimensionPortalActivationSystem))]
  public partial class DimensionPortalHydrationSystem : SystemBase
  {
    private const double HydrationIntervalSeconds = 0.50d;

    private EntityQuery portalQuery;
    private double nextHydrationAt;

    protected override void OnCreate()
    {
      portalQuery = GetEntityQuery(new EntityQueryDesc
      {
        All = new[]
        {
          ComponentType.ReadWrite<DimensionPortalCD>()
        },
        None = new[]
        {
          ComponentType.ReadOnly<DimensionPortalHydratedCD>()
        }
      });
      RequireForUpdate(portalQuery);
    }

    protected override void OnUpdate()
    {
      double now = World.Time.ElapsedTime;
      if (now < nextHydrationAt)
      {
        return;
      }

      nextHydrationAt = now + HydrationIntervalSeconds;

      IDimensionService service;
      if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
      {
        return;
      }

      using NativeArray<Entity> entities =
          portalQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<DimensionPortalCD> portals =
          portalQuery.ToComponentDataArray<DimensionPortalCD>(Allocator.Temp);

      for (int i = 0; i < entities.Length; i++)
      {
        DimensionPortalCD portal = portals[i];
        string portalId = portal.PortalId.ToString();
        if (string.IsNullOrEmpty(portalId))
        {
          continue;
        }

        DimensionPortalDefinition definition;
        if (!service.TryGetPortal(portalId, out definition))
        {
          continue;
        }

        DimensionPortalPresentationDefinition presentation;
        bool hasPresentation = service.TryFindPortalPresentationForPortal(portalId, out presentation);
        DimensionPortalCD hydrated = HydratePortal(portal, definition, presentation, hasPresentation);
        if (!PortalEquals(portal, hydrated))
        {
          EntityManager.SetComponentData(entities[i], hydrated);
        }

        if (!EntityManager.HasComponent<DimensionPortalHydratedCD>(entities[i]))
        {
          EntityManager.AddComponent<DimensionPortalHydratedCD>(entities[i]);
        }

        EnsureChargeUpdateState(entities[i], hydrated);
      }
    }

    private void EnsureChargeUpdateState(Entity entity, DimensionPortalCD portal)
    {
      bool needsChargeUpdate =
          portal.Active != 0 &&
          portal.Charged == 0 &&
          portal.ActivationChargeSeconds > 0.0f;
      bool hasChargeUpdate = EntityManager.HasComponent<DimensionPortalChargeUpdateCD>(entity);

      if (needsChargeUpdate && !hasChargeUpdate)
      {
        EntityManager.AddComponent<DimensionPortalChargeUpdateCD>(entity);
      }
      else if (!needsChargeUpdate && hasChargeUpdate)
      {
        EntityManager.RemoveComponent<DimensionPortalChargeUpdateCD>(entity);
      }
    }

    private static DimensionPortalCD HydratePortal(
        DimensionPortalCD portal,
        DimensionPortalDefinition definition,
        DimensionPortalPresentationDefinition presentation,
        bool hasPresentation)
    {
      portal.TargetDimensionId = ToFixed64(definition.ToDimensionId);
      portal.TargetLocalX = definition.ToLocalPosition.x;
      portal.TargetLocalY = definition.ToLocalPosition.y;
      portal.ActivationCooldownSeconds = hasPresentation ? presentation.CooldownSeconds : 0.0f;
      portal.RequireGeneratedArea =
          !hasPresentation || presentation.RequireGeneratedAreaOnUse ? (byte)1 : (byte)0;
      portal.AllowFallbackPosition =
          !hasPresentation || presentation.AllowFallbackPositionOnUse ? (byte)1 : (byte)0;
      portal.Active =
          definition.State == DimensionPortalState.Available &&
          (!hasPresentation || presentation.Enabled)
              ? (byte)1
              : (byte)0;
      portal.Interactable =
          !hasPresentation || presentation.Interactable
              ? (byte)1
              : (byte)0;
      return portal;
    }

    private static bool PortalEquals(DimensionPortalCD a, DimensionPortalCD b)
    {
      return a.PortalId.Equals(b.PortalId) &&
          a.TargetDimensionId.Equals(b.TargetDimensionId) &&
          a.TargetLocalX == b.TargetLocalX &&
          a.TargetLocalY == b.TargetLocalY &&
          a.ActivationCooldownSeconds == b.ActivationCooldownSeconds &&
          a.ActivationChargeSeconds == b.ActivationChargeSeconds &&
          a.ActivationProgress == b.ActivationProgress &&
          a.ActivationStartedAt == b.ActivationStartedAt &&
          a.RequireGeneratedArea == b.RequireGeneratedArea &&
          a.AllowFallbackPosition == b.AllowFallbackPosition &&
          a.Active == b.Active &&
          a.Charged == b.Charged &&
          a.Interactable == b.Interactable;
    }

    private static FixedString64Bytes ToFixed64(string value)
    {
      FixedString64Bytes result = default;
      if (string.IsNullOrEmpty(value))
      {
        return result;
      }

      int count = Math.Min(value.Length, 63);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }

      return result;
    }
  }
}
