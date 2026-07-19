using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Persistence;
using Interaction;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  [UpdateBefore(typeof(DimensionPortalHydrationSystem))]
  public partial class DimensionReturnPortalSpawnSystem : SystemBase
  {
    private const double RetrySeconds = 2.0d;

    private EntityQuery databaseQuery;
    private EntityQuery portalQuery;
    private double nextAttemptAt;
    private bool loggedWaitingForObject;
    private bool loggedWaitingForService;
    private bool loggedWaitingForEntryArea;
    private string observedWorldKey = string.Empty;
    private readonly HashSet<string> observedReturnPortalIds =
        new HashSet<string>(StringComparer.Ordinal);

    protected override void OnCreate()
    {
      databaseQuery = GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
      portalQuery = GetEntityQuery(ComponentType.ReadOnly<DimensionPortalCD>());
      RequireForUpdate(databaseQuery);
    }

    protected override void OnUpdate()
    {
      double now = World.Time.ElapsedTime;
      if (now < nextAttemptAt)
      {
        return;
      }

      nextAttemptAt = now + RetrySeconds;

      if (DimensionReturnPortalSpawnRegistry.Count == 0)
      {
        return;
      }

      IDimensionService service;
      if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
      {
        LogWaitingForServiceOnce();
        return;
      }

      loggedWaitingForService = false;
      if (!ShouldWakeForReturnPortalSpawn(service))
      {
        return;
      }

      if (!DimensionWorldRegistry.IsLoaded)
      {
        DimensionWorldRegistry.EnsureLoadedForCurrentWorld();
        if (!DimensionWorldRegistry.IsLoaded)
        {
          return;
        }
      }

      string worldKey = DimensionWorldRegistry.WorldKey;
      if (!string.Equals(observedWorldKey, worldKey, StringComparison.Ordinal))
      {
        ResetObservedWorld(worldKey);
      }

      for (int i = 0; i < DimensionReturnPortalSpawnRegistry.Count; i++)
      {
        DimensionReturnPortalSpawnDefinition definition;
        if (!DimensionReturnPortalSpawnRegistry.TryGet(i, out definition) ||
            !definition.IsValid)
        {
          continue;
        }

        if (!ShouldSpawnReturnPortalNow(service, definition))
        {
          continue;
        }

        if (HasReturnPortal(definition))
        {
          continue;
        }

        if (!IsEntryAreaReady(service, definition))
        {
          continue;
        }

        loggedWaitingForEntryArea = false;

        ObjectID portalObjectID;
        if (!TryResolvePortalObjectID(definition, out portalObjectID))
        {
          LogWaitingForObjectOnce(definition);
          continue;
        }

        loggedWaitingForObject = false;
        TrySpawnReturnPortal(service, definition, portalObjectID);
      }
    }

    private static bool ShouldWakeForReturnPortalSpawn(IDimensionService service)
    {
      IDimensionRuntimeStateService runtimeState = service as IDimensionRuntimeStateService;
      if (runtimeState == null)
      {
        return true;
      }

      return runtimeState.HasActiveRuntimeTravelWork ||
             runtimeState.HasActiveRuntimeGenerationWork ||
             HasTrackedPlayerInReturnPortalSource(runtimeState);
    }

    private static bool HasTrackedPlayerInReturnPortalSource(
        IDimensionRuntimeStateService runtimeState)
    {
      for (int i = 0; i < DimensionReturnPortalSpawnRegistry.Count; i++)
      {
        DimensionReturnPortalSpawnDefinition definition;
        if (!DimensionReturnPortalSpawnRegistry.TryGet(i, out definition) ||
            !definition.IsValid)
        {
          continue;
        }

        if (runtimeState.HasTrackedPlayerInDimension(definition.SourceDimensionId))
        {
          return true;
        }
      }

      return false;
    }

    private static bool ShouldSpawnReturnPortalNow(
        IDimensionService service,
        DimensionReturnPortalSpawnDefinition definition)
    {
      IDimensionRuntimeStateService runtimeState = service as IDimensionRuntimeStateService;
      if (runtimeState == null)
      {
        return true;
      }

      return runtimeState.HasActiveRuntimeTravelWork ||
             runtimeState.HasTrackedPlayerInDimension(definition.SourceDimensionId);
    }

    private bool HasReturnPortal(DimensionReturnPortalSpawnDefinition definition)
    {
      bool hasObservedReservation = observedReturnPortalIds.Contains(definition.PortalId);
      bool hasMatchingPortalEntity = false;
      using NativeArray<Entity> entities = portalQuery.ToEntityArray(Allocator.Temp);
      using NativeArray<DimensionPortalCD> portals =
          portalQuery.ToComponentDataArray<DimensionPortalCD>(Allocator.Temp);
      using NativeList<Entity> duplicates = new NativeList<Entity>(Allocator.Temp);

      Entity firstReturnPortal = Entity.Null;
      for (int i = 0; i < portals.Length; i++)
      {
        if (string.Equals(
            portals[i].PortalId.ToString(),
            definition.PortalId,
            StringComparison.Ordinal))
        {
          hasMatchingPortalEntity = true;
          observedReturnPortalIds.Add(definition.PortalId);
          if (!IsLivePortalEntity(entities[i]))
          {
            continue;
          }

          EnsurePortalComponents(entities[i], definition);
          if (firstReturnPortal == Entity.Null)
          {
            firstReturnPortal = entities[i];
          }
          else
          {
            duplicates.Add(entities[i]);
          }
        }
      }

      for (int i = 0; i < duplicates.Length; i++)
      {
        Entity duplicate = duplicates[i];
        if (duplicate != Entity.Null && EntityManager.Exists(duplicate))
        {
          EntityManager.DestroyEntity(duplicate);
        }
      }

      if (duplicates.Length > 0)
      {
        Debug.LogWarning(
            "[ExpandNullforge] Removed " +
            duplicates.Length +
            " duplicate return portal entities for portalId=" +
            definition.PortalId +
            ".");
      }

      return hasObservedReservation || hasMatchingPortalEntity || firstReturnPortal != Entity.Null;
    }

    private bool IsLivePortalEntity(Entity entity)
    {
      if (entity == Entity.Null || !EntityManager.Exists(entity))
      {
        return false;
      }

      return !EntityManager.HasComponent<EntityDestroyedCD>(entity) ||
             !EntityManager.IsComponentEnabled<EntityDestroyedCD>(entity);
    }

    private bool IsEntryAreaReady(
        IDimensionService service,
        DimensionReturnPortalSpawnDefinition definition)
    {
      DimensionGenerationStatus status;
      if (!service.TryGetGenerationStatus(
          definition.SourceDimensionId,
          definition.RequiredGeneratedBounds,
          out status) ||
          status.State != DimensionGenerationState.Ready)
      {
        LogWaitingForEntryAreaOnce(definition, status);
        return false;
      }

      return true;
    }

    private bool TryResolvePortalObjectID(
        DimensionReturnPortalSpawnDefinition definition,
        out ObjectID portalObjectID)
    {
      portalObjectID = ObjectID.None;
      if (API.Authoring == null)
      {
        return false;
      }

      return DimensionPortalObjectIdCache.TryResolve(
          definition.PortalObjectName,
          out portalObjectID);
    }

    private bool TrySpawnReturnPortal(
        IDimensionService service,
        DimensionReturnPortalSpawnDefinition definition,
        ObjectID portalObjectID)
    {
      float2 absolutePosition;
      if (!service.TryToAbsolute(
          definition.SourceDimensionId,
          definition.SpawnLocalPosition,
          out absolutePosition))
      {
        Debug.LogWarning(
            "[ExpandNullforge] Could not resolve return portal spawn position for portalId=" +
            definition.PortalId +
            ".");
        return false;
      }

      PugDatabase.DatabaseBankCD databaseBank = databaseQuery.GetSingleton<PugDatabase.DatabaseBankCD>();
      Entity portalEntity =
          EntityUtility.CreateEntity(
              World,
              new Vector3(absolutePosition.x, 0.0f, absolutePosition.y),
              portalObjectID,
              1,
              databaseBank.databaseBankBlob,
              0);

      if (portalEntity == Entity.Null || !EntityManager.Exists(portalEntity))
      {
        Debug.LogWarning(
            "[ExpandNullforge] Could not create return portal entity for portalId=" +
            definition.PortalId +
            ".");
        return false;
      }

      EnsurePortalComponents(portalEntity, definition);
      observedReturnPortalIds.Add(definition.PortalId);
      DimensionFrameworkLog.Verbose(
          "[ExpandNullforge] Spawned " +
          definition.DisplayName +
          " at local=(" +
          definition.SpawnLocalPosition.x +
          ", " +
          definition.SpawnLocalPosition.y +
          ") absolute=(" +
          absolutePosition.x +
          ", " +
          absolutePosition.y +
          ").");
      return true;
    }

    private void EnsurePortalComponents(
        Entity portalEntity,
        DimensionReturnPortalSpawnDefinition definition)
    {
      DimensionPortalCD portal = new DimensionPortalCD
      {
        PortalId = ToFixed64(definition.PortalId),
        TargetDimensionId = ToFixed64(definition.TargetDimensionId),
        TargetLocalX = definition.TargetLocalPosition.x,
        TargetLocalY = definition.TargetLocalPosition.y,
        ActivationCooldownSeconds = definition.ActivationCooldownSeconds,
        ActivationChargeSeconds = 0.0f,
        ActivationProgress = 1.0f,
        ActivationStartedAt = -1.0d,
        RequireGeneratedArea = definition.RequireGeneratedAreaOnUse ? (byte)1 : (byte)0,
        AllowFallbackPosition = definition.AllowFallbackPositionOnUse ? (byte)1 : (byte)0,
        Active = 1,
        Charged = 1,
        Interactable = definition.Interactable ? (byte)1 : (byte)0
      };

      if (EntityManager.HasComponent<DimensionPortalCD>(portalEntity))
      {
        EntityManager.SetComponentData(portalEntity, portal);
      }
      else
      {
        EntityManager.AddComponentData(portalEntity, portal);
      }

      if (!EntityManager.HasBuffer<DimensionPortalActivationBuffer>(portalEntity))
      {
        EntityManager.AddBuffer<DimensionPortalActivationBuffer>(portalEntity);
      }

      if (!EntityManager.HasComponent<DimensionPortalHydratedCD>(portalEntity))
      {
        EntityManager.AddComponent<DimensionPortalHydratedCD>(portalEntity);
      }

      if (EntityManager.HasComponent<DimensionPortalChargeUpdateCD>(portalEntity))
      {
        EntityManager.RemoveComponent<DimensionPortalChargeUpdateCD>(portalEntity);
      }

      EnsureReadyObjectData(portalEntity);

      if (!EntityManager.HasBuffer<TriggerUseInteractionBuffer>(portalEntity))
      {
        EntityManager.AddBuffer<TriggerUseInteractionBuffer>(portalEntity);
      }

      if (!EntityManager.HasBuffer<TriggerExitInteractionBuffer>(portalEntity))
      {
        EntityManager.AddBuffer<TriggerExitInteractionBuffer>(portalEntity);
      }

      EnsureMapMarkerComponents(portalEntity);

      if (EntityManager.HasComponent<LocalTransform>(portalEntity))
      {
        LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(portalEntity);
        transform.Rotation = quaternion.identity;
        EntityManager.SetComponentData(portalEntity, transform);
      }
    }

    private void EnsureReadyObjectData(Entity portalEntity)
    {
      if (!EntityManager.HasComponent<ObjectDataCD>(portalEntity))
      {
        return;
      }

      ObjectDataCD objectData = EntityManager.GetComponentData<ObjectDataCD>(portalEntity);
      if (objectData.amount == DimensionPortalChargeSystem.ReadyObjectDataAmount)
      {
        return;
      }

      objectData.amount = DimensionPortalChargeSystem.ReadyObjectDataAmount;
      EntityManager.SetComponentData(portalEntity, objectData);
    }

    private void EnsureMapMarkerComponents(Entity portalEntity)
    {
      if (!EntityManager.HasComponent<MapMarkerCD>(portalEntity))
      {
        EntityManager.AddComponentData(portalEntity, new MapMarkerCD
        {
          mapMarkerType = MapMarkerType.Portal,
          userMapMarkerType = UserMapMarkerType.None,
          uniqueMarkerId = ObjectID.None
        });
      }

      if (!EntityManager.HasComponent<MapMarkerActivatedCD>(portalEntity))
      {
        EntityManager.AddComponentData(portalEntity, new MapMarkerActivatedCD
        {
          Value = true,
          Hidden = false
        });
      }
      else
      {
        MapMarkerActivatedCD marker =
            EntityManager.GetComponentData<MapMarkerActivatedCD>(portalEntity);
        if (!marker.Value)
        {
          marker.Value = true;
          EntityManager.SetComponentData(portalEntity, marker);
        }
      }
    }

    private void ResetObservedWorld(string worldKey)
    {
      observedWorldKey = worldKey ?? string.Empty;
      loggedWaitingForObject = false;
      loggedWaitingForService = false;
      loggedWaitingForEntryArea = false;
      observedReturnPortalIds.Clear();
    }

    private void LogWaitingForObjectOnce(DimensionReturnPortalSpawnDefinition definition)
    {
      if (loggedWaitingForObject)
      {
        return;
      }

      loggedWaitingForObject = true;
      DimensionFrameworkLog.Verbose(
          "[ExpandNullforge] Waiting for return portal ObjectID before spawning " +
          definition.DisplayName +
          ".");
    }

    private void LogWaitingForServiceOnce()
    {
      if (loggedWaitingForService)
      {
        return;
      }

      loggedWaitingForService = true;
      DimensionFrameworkLog.Verbose("[ExpandNullforge] Waiting for the dimension service before spawning return portals.");
    }

    private void LogWaitingForEntryAreaOnce(
        DimensionReturnPortalSpawnDefinition definition,
        DimensionGenerationStatus status)
    {
      if (loggedWaitingForEntryArea)
      {
        return;
      }

      loggedWaitingForEntryArea = true;
      string state = status.State == DimensionGenerationState.Unknown
          ? "unknown"
          : status.State.ToString();
      DimensionFrameworkLog.Verbose(
          "[ExpandNullforge] Waiting for " +
          definition.SourceDimensionId +
          " entry area to finish generation before spawning " +
          definition.DisplayName +
          ". Current state=" +
          state +
          ".");
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
  }
}
