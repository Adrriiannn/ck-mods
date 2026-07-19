using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Portals
{
  public struct DimensionPortalCD : IComponentData
  {
    public FixedString64Bytes PortalId;
    public FixedString64Bytes TargetDimensionId;
    public float TargetLocalX;
    public float TargetLocalY;
    public float ActivationCooldownSeconds;
    public float ActivationChargeSeconds;
    public float ActivationProgress;
    public double ActivationStartedAt;
    public byte RequireGeneratedArea;
    public byte AllowFallbackPosition;
    public byte Active;
    public byte Charged;
    public byte Interactable;
  }

  public struct DimensionPortalActivationBuffer : IBufferElementData
  {
    public Entity Player;
    public Entity SourceConnection;
    public uint RequestId;
    public double RequestedAt;
    public FixedString128Bytes Reason;
  }

  public struct DimensionPortalHydratedCD : IComponentData
  {
  }

  public struct DimensionPortalChargeUpdateCD : IComponentData
  {
  }

  [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(DimensionPortalHydrationSystem))]
  [UpdateBefore(typeof(DimensionPortalActivationSystem))]
  public partial class DimensionPortalChargeSystem : SystemBase
  {
    public const int ReadyObjectDataAmount = 600;
    private const double ChargeUpdateIntervalSeconds = 0.2d;

    private EntityQuery portalQuery;
    private double nextChargeUpdateAt;

    protected override void OnCreate()
    {
      portalQuery = GetEntityQuery(
          ComponentType.ReadWrite<DimensionPortalCD>(),
          ComponentType.ReadWrite<ObjectDataCD>(),
          ComponentType.ReadOnly<DimensionPortalChargeUpdateCD>());
      RequireForUpdate(portalQuery);
    }

    protected override void OnUpdate()
    {
      double now = World.Time.ElapsedTime;
      if (now < nextChargeUpdateAt)
      {
        return;
      }

      nextChargeUpdateAt = now + ChargeUpdateIntervalSeconds;
      using NativeArray<Entity> entities = portalQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<DimensionPortalCD> portals =
          portalQuery.ToComponentDataArray<DimensionPortalCD>(Allocator.Temp);
      using NativeArray<ObjectDataCD> objectDatas =
          portalQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);

      for (int i = 0; i < entities.Length; i++)
      {
        Entity entity = entities[i];
        DimensionPortalCD portal = portals[i];
        ObjectDataCD objectData = objectDatas[i];

        bool active = portal.Active != 0;
        float chargeSeconds = math.max(0.0f, portal.ActivationChargeSeconds);
        float progress = 0.0f;
        bool charged = false;

        if (!active)
        {
          portal.ActivationStartedAt = -1.0d;
        }
        else if (chargeSeconds <= 0.0f || portal.Charged != 0)
        {
          progress = 1.0f;
          charged = true;
        }
        else
        {
          if (portal.ActivationStartedAt <= 0.0d || portal.ActivationStartedAt > now)
          {
            float savedProgress = math.clamp(
                math.max(
                    portal.ActivationProgress,
                    (float)objectData.amount / ReadyObjectDataAmount),
                0.0f,
                1.0f);
            portal.ActivationStartedAt = now - savedProgress * chargeSeconds;
          }

          double elapsed = now - portal.ActivationStartedAt;
          progress = math.clamp((float)(elapsed / chargeSeconds), 0.0f, 1.0f);
          charged = progress >= 1.0f;
          if (charged)
          {
            progress = 1.0f;
          }
        }

        byte chargedValue = charged ? (byte)1 : (byte)0;
        if (portal.Charged != chargedValue ||
            !Approximately(portal.ActivationProgress, progress) ||
            portal.ActivationStartedAt != portals[i].ActivationStartedAt)
        {
          portal.Charged = chargedValue;
          portal.ActivationProgress = progress;
          EntityManager.SetComponentData(entity, portal);
        }

        int amount = charged
            ? ReadyObjectDataAmount
            : math.clamp((int)math.round(progress * ReadyObjectDataAmount), 0, ReadyObjectDataAmount - 1);
        if (objectData.amount != amount)
        {
          objectData.amount = amount;
          EntityManager.SetComponentData(entity, objectData);
        }

        if (EntityManager.HasComponent<MapMarkerActivatedCD>(entity))
        {
          MapMarkerActivatedCD marker =
              EntityManager.GetComponentData<MapMarkerActivatedCD>(entity);
          if (marker.Value != charged)
          {
            marker.Value = charged;
            EntityManager.SetComponentData(entity, marker);
          }
        }

        if (!active || charged)
        {
          EntityManager.RemoveComponent<DimensionPortalChargeUpdateCD>(entity);
        }
      }
    }

    private static bool Approximately(float left, float right)
    {
      return math.abs(left - right) <= 0.0001f;
    }
  }
}
