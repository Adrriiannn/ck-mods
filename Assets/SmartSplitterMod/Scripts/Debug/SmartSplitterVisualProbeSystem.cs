using Pug.Automation;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterVisualProbeSystem : SystemBase
{
  private EntityQuery _physicalSplitterQuery;
  private EntityQuery _splitterOrchestratorQuery;
  private EntityQuery _electricityQuery;

  private double _nextLogAt;
  private const double LogIntervalSeconds = 1.0d;

  protected override void OnCreate()
  {
    base.OnCreate();

    _physicalSplitterQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadOnly<LocalTransform>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _splitterOrchestratorQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<MoversWithSharedStateBuffer>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _electricityQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ElectricityCD>(),
        ComponentType.ReadOnly<LocalTransform>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });
  }

  protected override void OnUpdate()
  {
    if (!SmartSplitterDebugSettings.EnableVisualProbe)
    {
      return;
    }

    double now = World.Time.ElapsedTime;
    if (now < _nextLogAt)
    {
      return;
    }

    _nextLogAt = now + LogIntervalSeconds;

    using NativeArray<Entity> physicalEntities =
        _physicalSplitterQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ObjectDataCD> objectData =
        _physicalSplitterQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> physicalTransforms =
        _physicalSplitterQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    using NativeArray<Entity> orchestratorEntities =
        _splitterOrchestratorQuery.ToEntityArray(Allocator.Temp);

    using NativeArray<Entity> electricityEntities =
        _electricityQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ElectricityCD> electricityData =
        _electricityQuery.ToComponentDataArray<ElectricityCD>(Allocator.Temp);
    using NativeArray<LocalTransform> electricityTransforms =
        _electricityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    for (int i = 0; i < physicalEntities.Length; i++)
    {
      Entity physicalEntity = physicalEntities[i];

      if (!EntityManager.Exists(physicalEntity))
      {
        continue;
      }

      ObjectDataCD data = objectData[i];

      if (data.objectID != ObjectID.ConveyorBeltSplitter)
      {
        continue;
      }

      LocalTransform transform = physicalTransforms[i];
      int tileX = Mathf.RoundToInt(transform.Position.x);
      int tileY = Mathf.RoundToInt(transform.Position.z);

      Entity nearestOrchestrator = FindNearestSplitterOrchestrator(
          tileX,
          tileY,
          orchestratorEntities,
          out int nearestCenterX,
          out int nearestCenterY,
          out float nearestDistance,
          out int nearestBufferLength);

      bool powered = IsPoweredByAdjacentElectricity(
          tileX,
          tileY,
          electricityEntities,
          electricityData,
          electricityTransforms,
          out Entity powerEntity,
          out int powerX,
          out int powerY,
          out float electricityAmount,
          out float sourceEnergy);

      Debug.Log(
          $"[SmartSplitterVisualProbe] physical splitter entity={physicalEntity} " +
          $"objectID={data.objectID} pos=({transform.Position.x:0.00},{transform.Position.z:0.00}) " +
          $"tile=({tileX},{tileY}) powered={powered} " +
          $"powerEntity={powerEntity} powerTile=({powerX},{powerY}) " +
          $"electricityAmount={electricityAmount:0.00} sourceEnergy={sourceEnergy:0.00} " +
          $"nearestOrchestrator={nearestOrchestrator} nearestCenter=({nearestCenterX},{nearestCenterY}) " +
          $"nearestDistance={nearestDistance:0.00} nearestBufferLength={nearestBufferLength}");

      if (!SmartSplitterDebugSettings.EnableVisualProbeVerboseLogs)
      {
        continue;
      }

      Debug.Log(
          $"[SmartSplitterVisualProbe] physical explicit-components entity={physicalEntity} " +
          BuildExplicitComponentFlags(physicalEntity));

      if (nearestOrchestrator != Entity.Null && EntityManager.Exists(nearestOrchestrator))
      {
        Debug.Log(
            $"[SmartSplitterVisualProbe] orchestrator explicit-components entity={nearestOrchestrator} " +
            BuildExplicitComponentFlags(nearestOrchestrator));

        LogOrchestratorBuffer(nearestOrchestrator);
      }

      LogNearbyElectricity(
          tileX,
          tileY,
          electricityEntities,
          electricityData,
          electricityTransforms);
    }
  }

  private Entity FindNearestSplitterOrchestrator(
      int physicalTileX,
      int physicalTileY,
      NativeArray<Entity> orchestratorEntities,
      out int centerX,
      out int centerY,
      out float nearestDistance,
      out int bufferLength)
  {
    Entity nearest = Entity.Null;
    centerX = 0;
    centerY = 0;
    nearestDistance = float.MaxValue;
    bufferLength = 0;

    for (int i = 0; i < orchestratorEntities.Length; i++)
    {
      Entity orchestrator = orchestratorEntities[i];

      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
      {
        continue;
      }

      DynamicBuffer<MoversWithSharedStateBuffer> buffer =
          EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

      if (buffer.Length < 2)
      {
        continue;
      }

      if (!TryGetBufferCenter(buffer, out int cx, out int cy))
      {
        continue;
      }

      float distance = math.distance(
          new float2(physicalTileX, physicalTileY),
          new float2(cx, cy));

      if (distance >= nearestDistance)
      {
        continue;
      }

      nearest = orchestrator;
      centerX = cx;
      centerY = cy;
      nearestDistance = distance;
      bufferLength = buffer.Length;
    }

    if (nearestDistance > SmartSplitterDebugSettings.VisualProbeRadius)
    {
      return Entity.Null;
    }

    return nearest;
  }

  private bool TryGetBufferCenter(
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      out int centerX,
      out int centerY)
  {
    centerX = 0;
    centerY = 0;

    int found = 0;
    int sumX = 0;
    int sumY = 0;

    for (int i = 0; i < buffer.Length; i++)
    {
      Entity moverEntity = buffer[i].moverEntity;

      if (!EntityManager.Exists(moverEntity) ||
          !EntityManager.HasComponent<MoverCD>(moverEntity))
      {
        continue;
      }

      MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);

      sumX += mover.start.x;
      sumY += mover.start.y;
      found++;
    }

    if (found <= 0)
    {
      return false;
    }

    centerX = Mathf.RoundToInt((float)sumX / found);
    centerY = Mathf.RoundToInt((float)sumY / found);

    return true;
  }

  private bool IsPoweredByAdjacentElectricity(
      int tileX,
      int tileY,
      NativeArray<Entity> electricityEntities,
      NativeArray<ElectricityCD> electricityData,
      NativeArray<LocalTransform> electricityTransforms,
      out Entity powerEntity,
      out int powerX,
      out int powerY,
      out float electricityAmount,
      out float sourceEnergy)
  {
    powerEntity = Entity.Null;
    powerX = 0;
    powerY = 0;
    electricityAmount = 0f;
    sourceEnergy = 0f;

    for (int i = 0; i < electricityEntities.Length; i++)
    {
      Entity entity = electricityEntities[i];

      if (!EntityManager.Exists(entity))
      {
        continue;
      }

      ElectricityCD electricity = electricityData[i];

      if (!electricity.hasEnoughElectricityToPowerStuff)
      {
        continue;
      }

      LocalTransform transform = electricityTransforms[i];

      int ex = Mathf.RoundToInt(transform.Position.x);
      int ey = Mathf.RoundToInt(transform.Position.z);

      int dx = math.abs(ex - tileX);
      int dy = math.abs(ey - tileY);

      bool sameTile = dx == 0 && dy == 0;
      bool orthogonallyAdjacent = (dx == 1 && dy == 0) || (dx == 0 && dy == 1);

      if (!sameTile && !orthogonallyAdjacent)
      {
        continue;
      }

      powerEntity = entity;
      powerX = ex;
      powerY = ey;
      electricityAmount = electricity.electricityAmount;
      sourceEnergy = electricity.sourceEnergy;

      return true;
    }

    return false;
  }

  private string BuildExplicitComponentFlags(Entity entity)
  {
    if (entity == Entity.Null || !EntityManager.Exists(entity))
    {
      return "missing";
    }

    return
        $"exists=True " +
        $"hasObjectData={EntityManager.HasComponent<ObjectDataCD>(entity)} " +
        $"hasLocalTransform={EntityManager.HasComponent<LocalTransform>(entity)} " +
        $"hasLocalToWorld={EntityManager.HasComponent<LocalToWorld>(entity)} " +
        $"hasMover={EntityManager.HasComponent<MoverCD>(entity)} " +
        $"hasMoverTimer={EntityManager.HasComponent<MoverTimerCD>(entity)} " +
        $"hasMoverOrchestrator={EntityManager.HasComponent<MoverOrchestratorCD>(entity)} " +
        $"hasSharedMoverBuffer={EntityManager.HasBuffer<MoversWithSharedStateBuffer>(entity)} " +
        $"hasEnabledMoverFromSharedState={EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(entity)} " +
        $"hasElectricity={EntityManager.HasComponent<ElectricityCD>(entity)} " +
        $"hasElectricityRef={EntityManager.HasComponent<ElectricityEntityRefCD>(entity)}";
  }

  private void LogOrchestratorBuffer(Entity orchestrator)
  {
    if (orchestrator == Entity.Null ||
        !EntityManager.Exists(orchestrator) ||
        !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
    {
      return;
    }

    DynamicBuffer<MoversWithSharedStateBuffer> buffer =
        EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

    for (int i = 0; i < buffer.Length; i++)
    {
      Entity moverEntity = buffer[i].moverEntity;
      bool hasMover = EntityManager.Exists(moverEntity) &&
                      EntityManager.HasComponent<MoverCD>(moverEntity);

      if (!hasMover)
      {
        Debug.Log(
            $"[SmartSplitterVisualProbe] orchestrator buffer[{i}] mover={moverEntity} hasMover=False");
        continue;
      }

      MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);

      Debug.Log(
          $"[SmartSplitterVisualProbe] orchestrator buffer[{i}] " +
          $"mover={moverEntity} cachedStart={buffer[i].cachedStart} cachedDirection={buffer[i].cachedDirection} " +
          $"start={mover.start} stop={mover.stop} moveTime={mover.moveTime} cooldownTime={mover.cooldownTime} " +
          $"splitsIntoOnMove={mover.splitsIntoOnMove} index={mover.indexInOrchestrator} " +
          $"orchestrator={mover.moverOrchestratorEntity} " +
          $"enabledShared={IsEnabledMoverFromSharedStateEnabled(moverEntity)}");
    }
  }

  private void LogNearbyElectricity(
      int tileX,
      int tileY,
      NativeArray<Entity> electricityEntities,
      NativeArray<ElectricityCD> electricityData,
      NativeArray<LocalTransform> electricityTransforms)
  {
    for (int i = 0; i < electricityEntities.Length; i++)
    {
      Entity entity = electricityEntities[i];

      if (!EntityManager.Exists(entity))
      {
        continue;
      }

      LocalTransform transform = electricityTransforms[i];

      int ex = Mathf.RoundToInt(transform.Position.x);
      int ey = Mathf.RoundToInt(transform.Position.z);

      int dx = math.abs(ex - tileX);
      int dy = math.abs(ey - tileY);

      if (dx > 2 || dy > 2)
      {
        continue;
      }

      ElectricityCD electricity = electricityData[i];

      Debug.Log(
          $"[SmartSplitterVisualProbe] nearby electricity entity={entity} " +
          $"tile=({ex},{ey}) dx={dx} dy={dy} " +
          $"electricityAmount={electricity.electricityAmount:0.00} " +
          $"sourceEnergy={electricity.sourceEnergy:0.00} " +
          $"hasEnough={electricity.hasEnoughElectricityToPowerStuff} " +
          BuildExplicitComponentFlags(entity));
    }
  }

  private bool IsEnabledMoverFromSharedStateEnabled(Entity entity)
  {
    if (entity == Entity.Null ||
        !EntityManager.Exists(entity) ||
        !EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(entity))
    {
      return false;
    }

    return EntityManager.IsComponentEnabled<EnabledMoverFromSharedStateCD>(entity);
  }
}
