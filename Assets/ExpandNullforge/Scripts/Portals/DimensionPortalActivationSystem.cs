using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Networking;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateAfter(typeof(DimensionTravelServerRpcSystem))]
  public partial class DimensionPortalActivationSystem : SystemBase
  {
    private readonly Dictionary<Entity, double> lastActivationAtByPortal =
        new Dictionary<Entity, double>();
    private readonly List<Entity> stalePortalKeys =
        new List<Entity>();

    private readonly DimensionTravelResultRelay travelResultRelay =
        new DimensionTravelResultRelay();

    private EntityQuery portalQuery;
    private EntityArchetype resultArchetype;
    private double nextCooldownCleanupAt;

    protected override void OnCreate()
    {
      portalQuery = GetEntityQuery(
          ComponentType.ReadWrite<DimensionPortalCD>(),
          ComponentType.ReadWrite<DimensionPortalActivationBuffer>());
      resultArchetype = EntityManager.CreateArchetype(
          typeof(DimensionTravelResultRpc),
          typeof(SendRpcCommandRequest));
      RequireForUpdate(portalQuery);
    }

    protected override void OnDestroy()
    {
      lastActivationAtByPortal.Clear();
      stalePortalKeys.Clear();
      travelResultRelay.Dispose();
    }

    protected override void OnUpdate()
    {
      double now = World.Time.ElapsedTime;
      bool shouldCleanupCooldowns =
          lastActivationAtByPortal.Count > 0 && now >= nextCooldownCleanupAt;
      bool hasPendingActivations = DimensionPortalRuntime.HasPendingActivations;
      bool hasTravelResultRelayWork = travelResultRelay.HasPendingWork(now);

      if (!shouldCleanupCooldowns &&
          !hasPendingActivations &&
          !hasTravelResultRelayWork)
      {
        return;
      }

      if (shouldCleanupCooldowns)
      {
        CleanupStaleCooldowns(now);
      }

      IDimensionService service;
      if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
      {
        travelResultRelay.Bind(null);
        return;
      }

      travelResultRelay.Bind(service);
      if (hasTravelResultRelayWork)
      {
        travelResultRelay.Flush(EntityManager, resultArchetype, now);
      }

      if (!DimensionPortalRuntime.HasPendingActivations)
      {
        return;
      }

      using NativeArray<Entity> entities =
          portalQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<DimensionPortalCD> portals =
          portalQuery.ToComponentDataArray<DimensionPortalCD>(Allocator.Temp);

      for (int i = 0; i < entities.Length; i++)
      {
        Entity portalEntity = entities[i];
        DimensionPortalCD portal = portals[i];
        DynamicBuffer<DimensionPortalActivationBuffer> activations =
            EntityManager.GetBuffer<DimensionPortalActivationBuffer>(portalEntity);

        if (activations.Length == 0)
        {
          continue;
        }

        ProcessPortalActivations(
            service,
            portalEntity,
            portal,
            activations,
            now);
        activations.Clear();
      }

      DimensionPortalRuntime.MarkPendingActivationsDrained();
      if (travelResultRelay.HasPendingWork(now))
      {
        travelResultRelay.Flush(EntityManager, resultArchetype, now);
      }
    }

    private void ProcessPortalActivations(
        IDimensionService service,
        Entity portalEntity,
        DimensionPortalCD portal,
        DynamicBuffer<DimensionPortalActivationBuffer> activations,
        double now)
    {
      string portalId = portal.PortalId.ToString();
      if (portal.Active == 0 || string.IsNullOrEmpty(portalId))
      {
        SendFailureToActivations(
            activations,
            "portal-not-active",
            "This dimension portal is not active.");
        return;
      }

      if (portal.Interactable == 0)
      {
        SendFailureToActivations(
            activations,
            "portal-not-interactable",
            "This dimension portal cannot be used directly.");
        return;
      }

      if (!IsPortalCharged(portal))
      {
        SendFailureToActivations(
            activations,
            "portal-not-ready",
            "This dimension portal is still charging.");
        return;
      }

      double lastActivationAt;
      if (portal.ActivationCooldownSeconds > 0.0f &&
          lastActivationAtByPortal.TryGetValue(portalEntity, out lastActivationAt) &&
          now - lastActivationAt < portal.ActivationCooldownSeconds)
      {
        SendFailureToActivations(
            activations,
            "portal-cooldown-active",
            "This dimension portal is still cooling down.");
        return;
      }

      for (int i = 0; i < activations.Length; i++)
      {
        DimensionPortalActivationBuffer activation = activations[i];
        if (!CanUseActivation(activation.Player))
        {
          SendResult(
              activation.SourceConnection,
              activation.RequestId,
              DimensionTravelResult.Failed(
                  "player-not-ready",
                  "The player entity is not ready for dimension travel."));
          continue;
        }

        string reason = activation.Reason.ToString();
        DimensionTravelResult result =
            service.RequestPortalTravel(
                new DimensionPortalTravelRequest(
                    activation.Player,
                    portalId,
                    portal.RequireGeneratedArea != 0,
                    portal.AllowFallbackPosition != 0,
                    string.IsNullOrEmpty(reason)
                        ? "Dimension portal activation."
                        : reason));

        if (DimensionFrameworkLog.VerboseRuntimeLogging)
        {
          DimensionFrameworkLog.Verbose(
              "[ExpandNullforge] Dimension portal activation result. accepted=" +
              result.Accepted +
              " code=" +
              result.Code +
              " message=" +
              result.Message +
              " portalId=" +
              portalId);
        }

        if (result.Accepted)
        {
          lastActivationAtByPortal[portalEntity] = now;
          TrackInstantPortalTravel(activation.Player, portalEntity, portal, now);
        }

        SendResult(
            activation.SourceConnection,
            activation.RequestId,
            result,
            !result.Accepted);
        travelResultRelay.Track(result, activation.SourceConnection, activation.RequestId, now);
      }
    }

    /// <summary>
    /// Immersion bookkeeping for instant item portals. Travelling INTO a dimension through one
    /// records the portal's exact tile for the travelling player; any accepted travel whose
    /// target is the overworld consumes that record and queues a fresh instant portal on the
    /// same tile, so the player lands back beside "the same" temporary portal they left by
    /// (which then closes on its usual timer).
    /// </summary>
    private void TrackInstantPortalTravel(
        Entity player,
        Entity portalEntity,
        DimensionPortalCD portal,
        double now)
    {
      string targetDimensionId = portal.TargetDimensionId.ToString();
      if (string.Equals(targetDimensionId, DimensionIds.Overworld, StringComparison.Ordinal))
      {
        int2 reopenTile;
        string reopenItemName;
        if (DimensionItemPortalRegistry.TryConsumePendingReopen(
            player, now, out reopenTile, out reopenItemName))
        {
          DimensionItemPortalRegistry.EnqueueReopenSpawn(reopenTile, reopenItemName, now);
        }

        return;
      }

      DimensionItemPortalConfig config;
      bool backward;
      if (!DimensionItemPortalRegistry.TryGetConfigByPortalId(
          portal.PortalId.ToString(), out config, out backward) ||
          backward)
      {
        return;
      }

      if (!EntityManager.HasComponent<LocalTransform>(portalEntity))
      {
        return;
      }

      float3 portalPosition =
          EntityManager.GetComponentData<LocalTransform>(portalEntity).Position;
      int2 portalTile = new int2(
          (int)math.round(portalPosition.x),
          (int)math.round(portalPosition.z));
      DimensionItemPortalRegistry.RecordPendingReopen(
          player, portalTile, config.ItemObjectName, now);
    }

    private static bool IsPortalCharged(DimensionPortalCD portal)
    {
      return portal.Active != 0 &&
          (portal.Charged != 0 || portal.ActivationChargeSeconds <= 0.0f);
    }

    private void SendFailureToActivations(
        DynamicBuffer<DimensionPortalActivationBuffer> activations,
        string code,
        string message)
    {
      DimensionTravelResult result = DimensionTravelResult.Failed(code, message);
      for (int i = 0; i < activations.Length; i++)
      {
        SendResult(
            activations[i].SourceConnection,
            activations[i].RequestId,
            result,
            true);
      }
    }

    private void SendResult(
        Entity targetConnection,
        uint requestId,
        DimensionTravelResult result)
    {
      SendResult(targetConnection, requestId, result, !result.Accepted);
    }

    private void SendResult(
        Entity targetConnection,
        uint requestId,
        DimensionTravelResult result,
        bool isFinal)
    {
      if (targetConnection == Entity.Null)
      {
        return;
      }

      Entity entity = EntityManager.CreateEntity(resultArchetype);
      EntityManager.SetComponentData(entity, new DimensionTravelResultRpc
      {
        RequestId = requestId,
        Accepted = result.Accepted ? (byte)1 : (byte)0,
        Final = isFinal ? (byte)1 : (byte)0,
        Code = ToFixed64(result.Code),
        Message = ToFixed128(result.Message),
        TravelId = ToFixed64(result.TravelId),
        LoadTicketId = ToFixed64(result.LoadTicketId),
        TargetDimensionId = ToFixed64(result.TargetDimensionId),
        TargetLocalX = result.TargetLocalPosition.x,
        TargetLocalY = result.TargetLocalPosition.y,
        TargetAbsoluteX = result.TargetAbsolutePosition.x,
        TargetAbsoluteY = result.TargetAbsolutePosition.y
      });
      EntityManager.SetComponentData(entity, new SendRpcCommandRequest
      {
        TargetConnection = targetConnection
      });
    }

    private bool CanUseActivation(Entity player)
    {
      return player != Entity.Null &&
          EntityManager.Exists(player) &&
          EntityManager.HasComponent<PlayerGhost>(player) &&
          EntityManager.HasComponent<LocalTransform>(player) &&
          EntityManager.HasComponent<UIActionBuffer>(player);
    }

    private void CleanupStaleCooldowns(double now)
    {
      stalePortalKeys.Clear();
      foreach (Entity portalEntity in lastActivationAtByPortal.Keys)
      {
        if (!EntityManager.Exists(portalEntity))
        {
          stalePortalKeys.Add(portalEntity);
        }
      }

      for (int i = 0; i < stalePortalKeys.Count; i++)
      {
        lastActivationAtByPortal.Remove(stalePortalKeys[i]);
      }

      stalePortalKeys.Clear();
      nextCooldownCleanupAt = now + 10.0d;
    }

    private static FixedString64Bytes ToFixed64(string value)
    {
      FixedString64Bytes result = default;
      if (string.IsNullOrEmpty(value))
      {
        return result;
      }

      int count = math.min(value.Length, 63);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }

      return result;
    }

    private static FixedString128Bytes ToFixed128(string value)
    {
      FixedString128Bytes result = default;
      if (string.IsNullOrEmpty(value))
      {
        return result;
      }

      int count = math.min(value.Length, 127);
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
