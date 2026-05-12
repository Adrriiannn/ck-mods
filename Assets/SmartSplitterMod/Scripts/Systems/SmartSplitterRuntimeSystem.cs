using System.Collections.Generic;
using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SmartSplitterRuntimeSystem : SystemBase
{
  private sealed class CachedSplitter
  {
    public Entity Orchestrator;
    public double NextLogTime;
    public string LastLogKey = string.Empty;
  }

  private sealed class TrackedRouteState
  {
    public Entity TrackedEntity;
    public float LastInputDistance;
    public bool WasCloseToSplitter;
  }

  private readonly Dictionary<Entity, CachedSplitter> _splitters = new();
  private readonly Dictionary<Entity, double> _recentlyArmedEntities = new();
  private readonly Dictionary<Entity, bool> _bothSingleNextRight = new();
  private readonly Dictionary<Entity, TrackedRouteState> _trackedRoutes = new();
  private readonly Dictionary<Entity, bool> _splitterPowerState = new();
  private readonly HashSet<Entity> _smartStateDirty = new();

  private static bool EnableRouting = true;
  private static bool EnableDecisionLogs = false;
  private static bool EnableRoutingLogs = false;
  private static bool EnableElectricityGateLogs = true;

  private static bool EnableElectricityDeepScan = false;

  private double _electricityDeepScanAt = -1d;
  private const double ElectricityDeepScanDelaySeconds = 3.0d;

  private static bool ForceRightOnlyTestMode = false;

  private const float InputDetectDistance = 4.20f;
  private const float InputLaneHalfWidth = 0.35f;
  private const float MinInputDistanceFromCenter = 0.15f;

  private const double ArmedRouteDurationSeconds = 8.00d;
  private const double RecentlyArmedEntityIgnoreSeconds = 8.00d;
  private const double DecisionLogCooldownSeconds = 0.75d;
  private const double RouteHoldSeconds = 8.00d;

  private const float RouteTrackingDistanceEpsilon = 0.05f;
  private const float RouteTrackingCloseDistance = 0.35f;

  private EntityQuery _allOrchestratorsQuery;
  private EntityQuery _taggedSplitterQuery;
  private EntityQuery _routeQuery;
  private EntityQuery _droppedItemQuery;
  private EntityQuery _moverQuery;
  private EntityQuery _objectDataTransformQuery;
  private EntityQuery _electricityQuery;

  protected override void OnCreate()
  {
    _allOrchestratorsQuery = GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>());

    _taggedSplitterQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterTag>(),
        ComponentType.ReadOnly<SmartSplitterOriginalOutputsCD>());

    _routeQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterTag>(),
        ComponentType.ReadOnly<SmartSplitterConfigCD>(),
        ComponentType.ReadWrite<SmartSplitterArmedRouteCD>(),
        ComponentType.ReadOnly<SmartSplitterOriginalOutputsCD>(),
        ComponentType.ReadWrite<MoversWithSharedStateBuffer>());

    _droppedItemQuery = GetEntityQuery(
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadOnly<ContainedObjectsBuffer>(),
        ComponentType.ReadOnly<LocalTransform>());

    _objectDataTransformQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
       {
          ComponentType.ReadOnly<ObjectDataCD>()
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

    _moverQuery = GetEntityQuery(ComponentType.ReadOnly<MoverCD>());

    RequireForUpdate(_allOrchestratorsQuery);
  }

  protected override void OnUpdate()
  {
    double now = World.Time.ElapsedTime;

    ComponentLookup<MoverCD> moverLookup = GetComponentLookup<MoverCD>(true);

    using NativeArray<Entity> allMovers = _moverQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<MoverCD> allMoverData = _moverQuery.ToComponentDataArray<MoverCD>(Allocator.Temp);

    using NativeArray<Entity> droppedEntities = _droppedItemQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ObjectDataCD> droppedOuterObjects = _droppedItemQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> droppedTransforms = _droppedItemQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    using NativeArray<Entity> electricityEntities = _electricityQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ElectricityCD> electricityData = _electricityQuery.ToComponentDataArray<ElectricityCD>(Allocator.Temp);
    using NativeArray<LocalTransform> electricityTransforms = _electricityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    RefreshSmartSplitterConfig(moverLookup);
    RegisterSplitters();

    if (EnableElectricityDeepScan)
    {
      if (_electricityDeepScanAt < 0d)
      {
        _electricityDeepScanAt = now + ElectricityDeepScanDelaySeconds;

        Debug.Log(
            $"[SmartSplitterElectricityProbe] Deep scan scheduled at t={_electricityDeepScanAt:0.00} current={now:0.00}");
      }

      if (now >= _electricityDeepScanAt)
      {
        RunElectricityDeepScan(
            allMovers,
            allMoverData);
      }
    }

    UpdateTrackedRoutes(droppedEntities, droppedTransforms, allMovers, allMoverData);

    ApplyElectricityGateToSplitters(
        electricityEntities,
        electricityData,
        electricityTransforms,
        allMovers,
        allMoverData);

    if (ForceRightOnlyTestMode)
    {
      ForceAllSplittersRightOnly(allMovers, allMoverData);
    }
    else
    {
      // Important priority:
      // 1. Already-on-belt/frontmost DroppedItem wins.
      // 2. Feeder/robot-arm carried item is only used when the input belt is empty.
      // This prevents the robot arm's NEXT carried item from overriding the CURRENT belt item.
      ObserveSplitters(now, droppedEntities, droppedOuterObjects, droppedTransforms, allMovers, allMoverData);
      ObserveFeederCarriedItems(now, allMovers, allMoverData, droppedEntities, droppedOuterObjects, droppedTransforms);
      ApplyArmedRoutes(now, allMovers, allMoverData);
    }
  }

  private void RefreshSmartSplitterConfig(ComponentLookup<MoverCD> moverLookup)
  {
    using NativeArray<Entity> orchestrators = _allOrchestratorsQuery.ToEntityArray(Allocator.Temp);

    foreach (Entity orchestrator in orchestrators)
    {
      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
      {
        continue;
      }

      DynamicBuffer<MoversWithSharedStateBuffer> buffer =
          EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

      if (buffer.Length != 2)
      {
        continue;
      }

      MoversWithSharedStateBuffer output0 = buffer[0];
      MoversWithSharedStateBuffer output1 = buffer[1];

      if (!moverLookup.HasComponent(output0.moverEntity) ||
          !moverLookup.HasComponent(output1.moverEntity))
      {
        continue;
      }

      MoverCD mover0 = moverLookup[output0.moverEntity];
      MoverCD mover1 = moverLookup[output1.moverEntity];

      if (mover0.splitsIntoOnMove != 2 || mover1.splitsIntoOnMove != 2)
      {
        continue;
      }

      if (!EntityManager.HasComponent<SmartSplitterTag>(orchestrator))
      {
        EntityManager.AddComponent<SmartSplitterTag>(orchestrator);
      }

      if (!EntityManager.HasComponent<SmartSplitterConfigCD>(orchestrator))
      {
        EntityManager.AddComponentData(orchestrator, new SmartSplitterConfigCD
        {
          Enabled = true,
          LeftFilterObject = ObjectID.WallDirtBlock,
          LeftFilterVariation = 0,
          RightFilterObject = ObjectID.WallTurfBlock,
          RightFilterVariation = 0
        });
      }
      else
      {
        // Prototype hardcoded filters for the current test pass.
        // This also migrates already-tagged splitters from the previous both-dirt test default.
        SmartSplitterConfigCD config = EntityManager.GetComponentData<SmartSplitterConfigCD>(orchestrator);
        config.Enabled = true;
        config.LeftFilterObject = ObjectID.WallDirtBlock;
        config.LeftFilterVariation = 0;
        config.RightFilterObject = ObjectID.WallTurfBlock;
        config.RightFilterVariation = 0;
        EntityManager.SetComponentData(orchestrator, config);
      }

      if (!EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        CacheOriginalOutputs(orchestrator, output0, mover0, output1, mover1);
      }

      if (!EntityManager.HasComponent<SmartSplitterArmedRouteCD>(orchestrator))
      {
        EntityManager.AddComponentData(orchestrator, EmptyArmedRoute());
      }
    }
  }

  private void CacheOriginalOutputs(
      Entity orchestrator,
      MoversWithSharedStateBuffer output0,
      MoverCD mover0,
      MoversWithSharedStateBuffer output1,
      MoverCD mover1)
  {
    bool output0IsLeft;

    if (mover0.stop.x != mover1.stop.x)
    {
      output0IsLeft = mover0.stop.x < mover1.stop.x;
    }
    else
    {
      output0IsLeft = mover0.stop.y < mover1.stop.y;
    }

    MoversWithSharedStateBuffer leftOutput = output0IsLeft ? output0 : output1;
    MoversWithSharedStateBuffer rightOutput = output0IsLeft ? output1 : output0;
    MoverCD leftMover = output0IsLeft ? mover0 : mover1;
    MoverCD rightMover = output0IsLeft ? mover1 : mover0;

    EntityManager.AddComponentData(orchestrator, new SmartSplitterOriginalOutputsCD
    {
      HasOriginalOutputs = true,

      LeftMoverEntity = leftOutput.moverEntity,
      LeftMoverIndex = leftMover.indexInOrchestrator,
      LeftCachedDirection = leftOutput.cachedDirection,
      LeftCachedStart = leftOutput.cachedStart,

      RightMoverEntity = rightOutput.moverEntity,
      RightMoverIndex = rightMover.indexInOrchestrator,
      RightCachedDirection = rightOutput.cachedDirection,
      RightCachedStart = rightOutput.cachedStart
    });
  }

  private void RegisterSplitters()
  {
    using NativeArray<Entity> entities = _taggedSplitterQuery.ToEntityArray(Allocator.Temp);

    foreach (Entity orchestrator in entities)
    {
      if (_splitters.ContainsKey(orchestrator))
      {
        continue;
      }

      SmartSplitterOriginalOutputsCD originals =
          EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

      if (!IsOriginalOutputStateValid(originals))
      {
        continue;
      }

      _splitters[orchestrator] = new CachedSplitter
      {
        Orchestrator = orchestrator
      };
    }
  }

  private void ApplyElectricityGateToSplitters(
      NativeArray<Entity> electricityEntities,
      NativeArray<ElectricityCD> electricityData,
      NativeArray<LocalTransform> electricityTransforms,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    List<Entity> stale = null;

    foreach (CachedSplitter splitter in _splitters.Values)
    {
      Entity orchestrator = splitter.Orchestrator;

      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        stale ??= new List<Entity>();
        stale.Add(orchestrator);
        continue;
      }

      bool powered = IsSplitterPoweredByAdjacentElectricity(
          orchestrator,
          electricityEntities,
          electricityData,
          electricityTransforms);

      bool hadState = _splitterPowerState.TryGetValue(orchestrator, out bool wasPowered);
      _splitterPowerState[orchestrator] = powered;

      if (EnableElectricityGateLogs && (!hadState || wasPowered != powered))
      {
        Debug.Log(
            $"[SmartSplitterElectricityGate] orchestrator={orchestrator} powered={powered} wasPowered={(hadState ? wasPowered.ToString() : "unknown")} dirty={_smartStateDirty.Contains(orchestrator)}");
      }

      if (powered)
      {
        continue;
      }

      ClearArmedRoute(orchestrator);
      _trackedRoutes.Remove(orchestrator);

      // Important: do not touch a fresh vanilla splitter every tick.
      // Only restore when this mod previously forced one-sided smart state.
      if (_smartStateDirty.Contains(orchestrator))
      {
        RestoreVanillaSplitterState(orchestrator, allMovers, allMoverData);
        _smartStateDirty.Remove(orchestrator);
      }
    }

    if (stale == null)
    {
      return;
    }

    foreach (Entity entity in stale)
    {
      _splitters.Remove(entity);
      _splitterPowerState.Remove(entity);
      _smartStateDirty.Remove(entity);
      _trackedRoutes.Remove(entity);
    }
  }

  private bool IsSplitterSmartPowered(Entity orchestrator)
  {
    return _splitterPowerState.TryGetValue(orchestrator, out bool powered) && powered;
  }

  private bool TryGetSplitterCenterTile(Entity orchestrator, out int centerX, out int centerY)
  {
    centerX = 0;
    centerY = 0;

    if (!EntityManager.Exists(orchestrator) ||
        !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
    {
      return false;
    }

    SmartSplitterOriginalOutputsCD originals =
        EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

    if (!IsOriginalOutputStateValid(originals))
    {
      return false;
    }

    MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
    MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

    centerX = Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f);
    centerY = Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f);

    return true;
  }

  private bool IsSplitterPoweredByAdjacentElectricity(
      Entity orchestrator,
      NativeArray<Entity> electricityEntities,
      NativeArray<ElectricityCD> electricityData,
      NativeArray<LocalTransform> electricityTransforms)
  {
    if (!TryGetSplitterCenterTile(orchestrator, out int splitterX, out int splitterY))
    {
      return false;
    }

    for (int i = 0; i < electricityEntities.Length; i++)
    {
      ElectricityCD electricity = electricityData[i];

      bool canPowerSmartSplitter =
          electricity.hasEnoughElectricityToPowerStuff ||
          electricity.sourceEnergy > 0;

      if (!canPowerSmartSplitter)
      {
        continue;
      }

      LocalTransform transform = electricityTransforms[i];

      int powerX = Mathf.RoundToInt(transform.Position.x);
      int powerY = Mathf.RoundToInt(transform.Position.z);

      int dx = Mathf.Abs(powerX - splitterX);
      int dy = Mathf.Abs(powerY - splitterY);

      bool sameTile = dx == 0 && dy == 0;
      bool orthogonallyAdjacent = (dx == 1 && dy == 0) || (dx == 0 && dy == 1);

      if (sameTile || orthogonallyAdjacent)
      {
        return true;
      }
    }

    return false;
  }

  private void RunElectricityDeepScan(
    NativeArray<Entity> allMovers,
    NativeArray<MoverCD> allMoverData)
  {
    Debug.Log("[SmartSplitterElectricityProbe] ===== START DEEP SCAN =====");

    using NativeArray<Entity> objectEntities =
        _objectDataTransformQuery.ToEntityArray(Allocator.Temp);

    using NativeArray<ObjectDataCD> objectData =
        _objectDataTransformQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);

    using NativeArray<Entity> electricityEntities =
        _electricityQuery.ToEntityArray(Allocator.Temp);

    using NativeArray<ElectricityCD> electricityData =
        _electricityQuery.ToComponentDataArray<ElectricityCD>(Allocator.Temp);

    foreach (CachedSplitter splitter in _splitters.Values)
    {
      Entity orchestrator = splitter.Orchestrator;

      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        continue;
      }

      SmartSplitterOriginalOutputsCD originals =
          EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

      if (!IsOriginalOutputStateValid(originals))
      {
        continue;
      }

      MoverCD leftMover =
          EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);

      MoverCD rightMover =
          EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

      float centerX = (leftMover.start.x + rightMover.start.x) * 0.5f;
      float centerY = (leftMover.start.y + rightMover.start.y) * 0.5f;

      Debug.Log(
          $"[SmartSplitterElectricityProbe] SPLITTER orchestrator={orchestrator} " +
          $"center=({centerX:0.00},{centerY:0.00})");

      ScanEntity(orchestrator, "SPLITTER_ORCHESTRATOR");

      ScanEntity(originals.LeftMoverEntity, "LEFT_OUTPUT");
      ScanEntity(originals.RightMoverEntity, "RIGHT_OUTPUT");

      for (int i = 0; i < objectEntities.Length; i++)
      {
        Entity entity = objectEntities[i];

        if (!EntityManager.HasComponent<LocalTransform>(entity))
        {
          continue;
        }

        LocalTransform transform =
            EntityManager.GetComponentData<LocalTransform>(entity);

        float dx = transform.Position.x - centerX;
        float dz = transform.Position.z - centerY;

        float distanceSq = dx * dx + dz * dz;

        if (distanceSq > 25.0f)
        {
          continue;
        }

        Debug.Log(
            $"[SmartSplitterElectricityProbe] NEARBY entity={entity} " +
            $"distanceSq={distanceSq:0.00}");

        ScanEntity(entity, "NEARBY_OBJECT");
      }

      Debug.Log(
      $"[SmartSplitterElectricityProbe] ELECTRICITY_ENTITY_COUNT count={electricityEntities.Length}");

      for (int i = 0; i < electricityEntities.Length; i++)
      {
        Entity entity = electricityEntities[i];
        ElectricityCD electricity = electricityData[i];

        Debug.Log(
            $"[SmartSplitterElectricityProbe] ELECTRICITY_ENTITY entity={entity} " +
            $"amount={electricity.electricityAmount} " +
            $"sourceEnergy={electricity.sourceEnergy} " +
            $"hasEnough={electricity.hasEnoughElectricityToPowerStuff} " +
            $"hasLocalTransform={EntityManager.HasComponent<LocalTransform>(entity)} " +
            $"hasObjectData={EntityManager.HasComponent<ObjectDataCD>(entity)} " +
            $"hasElectricityConnection={EntityManager.HasComponent<ElectricityConnectionCD>(entity)} " +
            $"hasElectricityEntityRef={EntityManager.HasComponent<ElectricityEntityRefCD>(entity)} " +
            $"hasActivatedByElectricity={EntityManager.HasComponent<ActivatedByElectricityStateCD>(entity)}");

        if (EntityManager.HasComponent<LocalTransform>(entity))
        {
          LocalTransform transform =
              EntityManager.GetComponentData<LocalTransform>(entity);

          float dx = transform.Position.x - centerX;
          float dz = transform.Position.z - centerY;
          float distanceSq = dx * dx + dz * dz;

          Debug.Log(
              $"[SmartSplitterElectricityProbe] ELECTRICITY_ENTITY_TRANSFORM entity={entity} " +
              $"pos=({transform.Position.x:0.00},{transform.Position.y:0.00},{transform.Position.z:0.00}) " +
              $"distanceSq={distanceSq:0.00}");
        }

        ScanEntity(entity, "ELECTRICITY_ENTITY_FULL");
      }

      for (int i = 0; i < allMovers.Length; i++)
      {
        Entity moverEntity = allMovers[i];
        MoverCD mover = allMoverData[i];

        float dx = mover.start.x - centerX;
        float dz = mover.start.y - centerY;

        float distanceSq = dx * dx + dz * dz;

        if (distanceSq > 25.0f)
        {
          continue;
        }

        Debug.Log(
            $"[SmartSplitterElectricityProbe] NEARBY_MOVER entity={moverEntity} " +
            $"distanceSq={distanceSq:0.00}");

        ScanEntity(moverEntity, "NEARBY_MOVER");
      }
    }

    Debug.Log("[SmartSplitterElectricityProbe] ===== END DEEP SCAN =====");

    EnableElectricityDeepScan = false;
  }

  private void ScanEntity(Entity entity, string label)
  {
    if (!EntityManager.Exists(entity))
    {
      Debug.Log($"[SmartSplitterElectricityProbe] {label} entity={entity} DOES_NOT_EXIST");
      return;
    }

    Debug.Log(
        $"[SmartSplitterElectricityProbe] {label} entity={entity} " +
        $"hasObjectData={EntityManager.HasComponent<ObjectDataCD>(entity)} " +
        $"hasLocalTransform={EntityManager.HasComponent<LocalTransform>(entity)} " +
        $"hasElectricity={EntityManager.HasComponent<ElectricityCD>(entity)} " +
        $"hasElectricityConnection={EntityManager.HasComponent<ElectricityConnectionCD>(entity)} " +
        $"hasElectricityEntityRef={EntityManager.HasComponent<ElectricityEntityRefCD>(entity)} " +
        $"hasActivatedByElectricity={EntityManager.HasComponent<ActivatedByElectricityStateCD>(entity)} " +
        $"hasMover={EntityManager.HasComponent<MoverCD>(entity)} " +
        $"hasMoverOrchestrator={EntityManager.HasComponent<MoverOrchestratorCD>(entity)}");

    if (EntityManager.HasComponent<ObjectDataCD>(entity))
    {
      ObjectDataCD objectData =
          EntityManager.GetComponentData<ObjectDataCD>(entity);

      Debug.Log(
          $"[SmartSplitterElectricityProbe] {label} objectData " +
          $"objectID={objectData.objectID} " +
          $"variation={objectData.variation}");
    }

    if (EntityManager.HasComponent<LocalTransform>(entity))
    {
      LocalTransform transform =
          EntityManager.GetComponentData<LocalTransform>(entity);

      Debug.Log(
          $"[SmartSplitterElectricityProbe] {label} transform " +
          $"position=({transform.Position.x:0.00},{transform.Position.y:0.00},{transform.Position.z:0.00})");
    }

    if (EntityManager.HasComponent<ElectricityCD>(entity))
    {
      ElectricityCD electricity =
          EntityManager.GetComponentData<ElectricityCD>(entity);

      Debug.Log(
          $"[SmartSplitterElectricityProbe] {label} electricity " +
          $"amount={electricity.electricityAmount} " +
          $"sourceEnergy={electricity.sourceEnergy} " +
          $"hasEnough={electricity.hasEnoughElectricityToPowerStuff} " +
          $"circuitType={electricity.circuitType} " +
          $"connectionMode={electricity.circuitConnectionMode}");
    }

    if (EntityManager.HasComponent<ElectricityConnectionCD>(entity))
    {
      ElectricityConnectionCD connection =
          EntityManager.GetComponentData<ElectricityConnectionCD>(entity);

      Debug.Log(
          $"[SmartSplitterElectricityProbe] {label} electricityConnection " +
          $"connectedEntity={connection.connectedEntity} " +
          $"position={connection.position} " +
          $"mode={connection.mode} " +
          $"direction={connection.direction} " +
          $"prioritize={connection.prioritize}");

      for (int dir = 0; dir < 4; dir++)
      {
        Entity sourceEntity =
          connection.GetSourceEntity((ElectricityDirection)dir);

        Debug.Log(
            $"[SmartSplitterElectricityProbe] {label} source[{dir}]={sourceEntity}");
      }
    }

    if (EntityManager.HasComponent<ElectricityEntityRefCD>(entity))
    {
      ElectricityEntityRefCD refData =
          EntityManager.GetComponentData<ElectricityEntityRefCD>(entity);

      Debug.Log(
          $"[SmartSplitterElectricityProbe] {label} electricityRef " +
          $"value={refData.Value}");

      ScanEntity(refData.Value, label + "_REF_TARGET");
    }

    if (EntityManager.HasComponent<MoverCD>(entity))
    {
      MoverCD mover =
          EntityManager.GetComponentData<MoverCD>(entity);

      Debug.Log(
          $"[SmartSplitterElectricityProbe] {label} mover " +
          $"start={mover.start} " +
          $"stop={mover.stop} " +
          $"splits={mover.splitsIntoOnMove} " +
          $"index={mover.indexInOrchestrator}");
    }
  }

  private void ObserveFeederCarriedItems(
      double now,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData,
      NativeArray<Entity> droppedEntities,
      NativeArray<ObjectDataCD> droppedOuterObjects,
      NativeArray<LocalTransform> droppedTransforms)
  {
    foreach (CachedSplitter splitter in _splitters.Values)
    {
      if (!EntityManager.Exists(splitter.Orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterConfigCD>(splitter.Orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(splitter.Orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterArmedRouteCD>(splitter.Orchestrator))
      {
        continue;
      }

      if (!IsSplitterSmartPowered(splitter.Orchestrator))
      {
        continue;
      }

      if (_trackedRoutes.ContainsKey(splitter.Orchestrator))
      {
        continue;
      }

      SmartSplitterConfigCD config =
          EntityManager.GetComponentData<SmartSplitterConfigCD>(splitter.Orchestrator);

      if (!config.Enabled)
      {
        continue;
      }

      SmartSplitterOriginalOutputsCD originals =
          EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(splitter.Orchestrator);

      if (!IsOriginalOutputStateValid(originals))
      {
        continue;
      }

      MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
      MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

      // If an item is already on the input belt, do NOT let the robot arm's next carried item override it.
      if (TryFindBestIncomingItem(
              leftMover,
              rightMover,
              now,
              droppedEntities,
              droppedOuterObjects,
              droppedTransforms,
              out _,
              out _,
              out _,
              out _,
              out _))
      {
        continue;
      }

      if (!TryFindBestFeederCarriedItem(
              leftMover,
              rightMover,
              allMovers,
              allMoverData,
              out Entity feederEntity,
              out ObjectID itemObject,
              out int itemVariation,
              out int itemAmount,
              out float distance))
      {
        continue;
      }

      SmartSplitterDecision decision = DecideRoute(config, itemObject, itemVariation);

      if (decision == SmartSplitterDecision.Both && itemAmount <= 1)
      {
        continue;
      }

      decision = ApplyBothSingleAlternation(splitter.Orchestrator, decision, itemAmount);

      if (decision == SmartSplitterDecision.Both)
      {
        RestoreVanillaSplitterState(splitter.Orchestrator, allMovers, allMoverData);
        ClearArmedRoute(splitter.Orchestrator);
        return;
      }

      SmartSplitterArmedRouteCD currentArmed =
          EntityManager.GetComponentData<SmartSplitterArmedRouteCD>(splitter.Orchestrator);

      bool sameArmedRoute =
          currentArmed.HasArmedRoute &&
          currentArmed.ArmedEntity == feederEntity &&
          currentArmed.Decision == decision &&
          currentArmed.ItemObject == itemObject &&
          currentArmed.ItemVariation == itemVariation &&
          currentArmed.ItemAmount == itemAmount &&
          now <= currentArmed.ExpiresAt;

      if (sameArmedRoute)
      {
        continue;
      }

      ArmRoute(splitter, feederEntity, itemObject, itemVariation, itemAmount, decision, distance, now);
    }
  }

  private bool TryFindBestFeederCarriedItem(
      MoverCD leftMover,
      MoverCD rightMover,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData,
      out Entity feederEntity,
      out ObjectID itemObject,
      out int itemVariation,
      out int itemAmount,
      out float bestDistance)
  {
    feederEntity = Entity.Null;
    itemObject = ObjectID.None;
    itemVariation = 0;
    itemAmount = 0;
    bestDistance = float.MaxValue;

    float centerX = (leftMover.start.x + rightMover.start.x) * 0.5f;
    float centerY = (leftMover.start.y + rightMover.start.y) * 0.5f;

    float outputX = rightMover.stop.x - leftMover.stop.x;
    float outputY = rightMover.stop.y - leftMover.stop.y;

    if (!TryNormalize(outputX, outputY, out float outputAxisX, out float outputAxisY))
    {
      return false;
    }

    float inputAxisX = -outputAxisY;
    float inputAxisY = outputAxisX;

    bool found = false;

    for (int i = 0; i < allMovers.Length; i++)
    {
      Entity moverEntity = allMovers[i];
      MoverCD mover = allMoverData[i];

      if (mover.inventoryEntity == Entity.Null)
      {
        continue;
      }

      if (!EntityManager.Exists(mover.inventoryEntity) ||
          !EntityManager.HasBuffer<ContainedObjectsBuffer>(mover.inventoryEntity))
      {
        continue;
      }

      DynamicBuffer<ContainedObjectsBuffer> contained =
          EntityManager.GetBuffer<ContainedObjectsBuffer>(mover.inventoryEntity);

      if (contained.Length == 0)
      {
        continue;
      }

      var inner = contained[0].objectData;

      if (inner.objectID == ObjectID.None || inner.amount <= 0)
      {
        continue;
      }

      float itemX = mover.stop.x;
      float itemY = mover.stop.y;

      if (!IsInsideInputCorridor(
              itemX,
              itemY,
              centerX,
              centerY,
              outputAxisX,
              outputAxisY,
              inputAxisX,
              inputAxisY,
              out float inputDistance))
      {
        continue;
      }

      if (inputDistance >= bestDistance)
      {
        continue;
      }

      found = true;
      feederEntity = moverEntity;
      bestDistance = inputDistance;
      itemObject = inner.objectID;
      itemVariation = inner.variation;
      itemAmount = inner.amount;
    }

    return found;
  }

  private void ObserveSplitters(
    double now,
    NativeArray<Entity> droppedEntities,
    NativeArray<ObjectDataCD> droppedOuterObjects,
    NativeArray<LocalTransform> droppedTransforms,
    NativeArray<Entity> allMovers,
    NativeArray<MoverCD> allMoverData)
  {
    List<Entity> stale = null;

    foreach (CachedSplitter splitter in _splitters.Values)
    {
      if (!EntityManager.Exists(splitter.Orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterConfigCD>(splitter.Orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(splitter.Orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterArmedRouteCD>(splitter.Orchestrator))
      {
        stale ??= new List<Entity>();
        stale.Add(splitter.Orchestrator);
        continue;
      }

      ObserveSplitter(splitter, now, droppedEntities, droppedOuterObjects, droppedTransforms, allMovers, allMoverData);
    }

    if (stale == null)
    {
      return;
    }

    foreach (Entity entity in stale)
    {
      _splitters.Remove(entity);
    }
  }

  private void ObserveSplitter(
    CachedSplitter splitter,
    double now,
    NativeArray<Entity> droppedEntities,
    NativeArray<ObjectDataCD> droppedOuterObjects,
    NativeArray<LocalTransform> droppedTransforms,
    NativeArray<Entity> allMovers,
    NativeArray<MoverCD> allMoverData)
  {
    SmartSplitterConfigCD config =
        EntityManager.GetComponentData<SmartSplitterConfigCD>(splitter.Orchestrator);

    if (!config.Enabled)
    {
      return;
    }

    if (!IsSplitterSmartPowered(splitter.Orchestrator))
    {
      return;
    }

    if (_trackedRoutes.ContainsKey(splitter.Orchestrator))
    {
      return;
    }

    SmartSplitterOriginalOutputsCD originals =
        EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(splitter.Orchestrator);

    if (!IsOriginalOutputStateValid(originals))
    {
      return;
    }

    MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
    MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

    if (!TryFindBestIncomingItem(
            leftMover,
            rightMover,
            now,
            droppedEntities,
            droppedOuterObjects,
            droppedTransforms,
            out Entity bestDroppedEntity,
            out ObjectID itemObject,
            out int itemVariation,
            out int itemAmount,
            out float distance))
    {
      return;
    }

    SmartSplitterDecision decision = DecideRoute(config, itemObject, itemVariation);
    decision = ApplyBothSingleAlternation(splitter.Orchestrator, decision, itemAmount);

    if (decision == SmartSplitterDecision.Both)
    {
      RestoreVanillaSplitterState(splitter.Orchestrator, allMovers, allMoverData);
      ClearArmedRoute(splitter.Orchestrator);
      return;
    }

    SmartSplitterArmedRouteCD currentArmed =
        EntityManager.GetComponentData<SmartSplitterArmedRouteCD>(splitter.Orchestrator);

    bool sameArmedRoute =
        currentArmed.HasArmedRoute &&
        currentArmed.ArmedEntity == bestDroppedEntity &&
        currentArmed.Decision == decision &&
        currentArmed.ItemObject == itemObject &&
        currentArmed.ItemVariation == itemVariation &&
        currentArmed.ItemAmount == itemAmount &&
        now <= currentArmed.ExpiresAt;

    if (sameArmedRoute)
    {
      return;
    }

    ArmRoute(splitter, bestDroppedEntity, itemObject, itemVariation, itemAmount, decision, distance, now);

    _trackedRoutes[splitter.Orchestrator] = new TrackedRouteState
    {
      TrackedEntity = bestDroppedEntity,
      LastInputDistance = distance,
      WasCloseToSplitter = distance <= RouteTrackingCloseDistance
    };
  }

  private void ArmRoute(
      CachedSplitter splitter,
      Entity armedEntity,
      ObjectID itemObject,
      int itemVariation,
      int itemAmount,
      SmartSplitterDecision decision,
      float distance,
      double now)
  {
    EntityManager.SetComponentData(splitter.Orchestrator, new SmartSplitterArmedRouteCD
    {
      HasArmedRoute = true,
      AppliedOnce = false,
      VerifiedHoldState = false,
      ArmedEntity = armedEntity,
      Decision = decision,
      ItemObject = itemObject,
      ItemVariation = itemVariation,
      ItemAmount = itemAmount,
      ArmedAt = now,
      ExpiresAt = now + ArmedRouteDurationSeconds,
      RouteAppliedAt = 0d,
      HoldUntil = 0d
    });

    LogDecisionIfEnabled(splitter, armedEntity, itemObject, itemVariation, itemAmount, decision, distance, now);
  }

  private void ForceAllSplittersRightOnly(
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    using NativeArray<Entity> orchestrators = _taggedSplitterQuery.ToEntityArray(Allocator.Temp);

    foreach (Entity orchestrator in orchestrators)
    {
      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        continue;
      }

      SmartSplitterOriginalOutputsCD originals =
          EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

      if (!IsOriginalOutputStateValid(originals))
      {
        continue;
      }

      DynamicBuffer<MoversWithSharedStateBuffer> buffer =
          EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

      ApplyRightOnly(orchestrator, buffer, originals, allMovers, allMoverData);
    }
  }

  private void RestoreVanillaSplitterState(
    Entity orchestrator,
    NativeArray<Entity> allMovers,
    NativeArray<MoverCD> allMoverData)
  {
    if (!EntityManager.Exists(orchestrator) ||
        !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator) ||
        !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
    {
      return;
    }

    SmartSplitterOriginalOutputsCD originals =
        EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

    if (!IsOriginalOutputStateValid(originals))
    {
      return;
    }

    DynamicBuffer<MoversWithSharedStateBuffer> buffer =
        EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

    RestoreBothOutputs(orchestrator, buffer, originals);
    SetAllSplitterMoverSplitCounts(orchestrator, 2, allMovers, allMoverData);
    RestoreEnabledMoverFromSharedState(originals);
    _smartStateDirty.Remove(orchestrator);
  }

  private void UpdateTrackedRoutes(
    NativeArray<Entity> droppedEntities,
    NativeArray<LocalTransform> droppedTransforms,
    NativeArray<Entity> allMovers,
    NativeArray<MoverCD> allMoverData)
  {
    if (_trackedRoutes.Count == 0)
    {
      return;
    }

    List<Entity> finished = null;

    foreach (KeyValuePair<Entity, TrackedRouteState> entry in _trackedRoutes)
    {
      Entity orchestrator = entry.Key;
      TrackedRouteState tracked = entry.Value;

      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        finished ??= new List<Entity>();
        finished.Add(orchestrator);
        continue;
      }

      SmartSplitterOriginalOutputsCD originals =
          EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

      if (!IsOriginalOutputStateValid(originals))
      {
        finished ??= new List<Entity>();
        finished.Add(orchestrator);
        continue;
      }

      MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
      MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

      if (!TryGetTrackedDroppedItemInputDistance(
              tracked.TrackedEntity,
              leftMover,
              rightMover,
              droppedEntities,
              droppedTransforms,
              out float currentDistance))
      {
        finished ??= new List<Entity>();
        finished.Add(orchestrator);
        continue;
      }

      if (currentDistance <= RouteTrackingCloseDistance)
      {
        tracked.WasCloseToSplitter = true;
      }

      bool movedAwayAfterClose =
          tracked.WasCloseToSplitter &&
          currentDistance > tracked.LastInputDistance + RouteTrackingDistanceEpsilon;

      tracked.LastInputDistance = currentDistance;

      if (movedAwayAfterClose)
      {
        finished ??= new List<Entity>();
        finished.Add(orchestrator);
      }
    }

    if (finished == null)
    {
      return;
    }

    foreach (Entity orchestrator in finished)
    {
      RestoreVanillaSplitterState(orchestrator, allMovers, allMoverData);
      ClearArmedRoute(orchestrator);
      _trackedRoutes.Remove(orchestrator);
    }
  }

  private bool TryGetTrackedDroppedItemInputDistance(
      Entity trackedEntity,
      MoverCD leftMover,
      MoverCD rightMover,
      NativeArray<Entity> droppedEntities,
      NativeArray<LocalTransform> droppedTransforms,
      out float inputDistance)
  {
    inputDistance = float.MaxValue;

    float centerX = (leftMover.start.x + rightMover.start.x) * 0.5f;
    float centerY = (leftMover.start.y + rightMover.start.y) * 0.5f;

    float outputX = rightMover.stop.x - leftMover.stop.x;
    float outputY = rightMover.stop.y - leftMover.stop.y;

    if (!TryNormalize(outputX, outputY, out float outputAxisX, out float outputAxisY))
    {
      return false;
    }

    float inputAxisX = -outputAxisY;
    float inputAxisY = outputAxisX;

    for (int i = 0; i < droppedEntities.Length; i++)
    {
      if (droppedEntities[i] != trackedEntity)
      {
        continue;
      }

      float itemX = droppedTransforms[i].Position.x;
      float itemY = droppedTransforms[i].Position.z;

      return IsInsideInputCorridor(
          itemX,
          itemY,
          centerX,
          centerY,
          outputAxisX,
          outputAxisY,
          inputAxisX,
          inputAxisY,
          out inputDistance);
    }

    return false;
  }

  private bool TryFindBestIncomingItem(
      MoverCD leftMover,
      MoverCD rightMover,
      double now,
      NativeArray<Entity> droppedEntities,
      NativeArray<ObjectDataCD> droppedOuterObjects,
      NativeArray<LocalTransform> droppedTransforms,
      out Entity bestDroppedEntity,
      out ObjectID itemObject,
      out int itemVariation,
      out int itemAmount,
      out float bestDistance)
  {
    bestDroppedEntity = Entity.Null;
    itemObject = ObjectID.None;
    itemVariation = 0;
    itemAmount = 0;
    bestDistance = float.MaxValue;

    float centerX = (leftMover.start.x + rightMover.start.x) * 0.5f;
    float centerY = (leftMover.start.y + rightMover.start.y) * 0.5f;

    float outputX = rightMover.stop.x - leftMover.stop.x;
    float outputY = rightMover.stop.y - leftMover.stop.y;

    if (!TryNormalize(outputX, outputY, out float outputAxisX, out float outputAxisY))
    {
      return false;
    }

    float inputAxisX = -outputAxisY;
    float inputAxisY = outputAxisX;

    bool found = false;

    for (int i = 0; i < droppedEntities.Length; i++)
    {
      Entity droppedEntity = droppedEntities[i];

      if (droppedOuterObjects[i].objectID != ObjectID.DroppedItem)
      {
        continue;
      }

      if (!EntityManager.Exists(droppedEntity) ||
          !EntityManager.HasBuffer<ContainedObjectsBuffer>(droppedEntity))
      {
        continue;
      }

      DynamicBuffer<ContainedObjectsBuffer> contained =
          EntityManager.GetBuffer<ContainedObjectsBuffer>(droppedEntity);

      if (contained.Length == 0)
      {
        continue;
      }

      var inner = contained[0].objectData;

      if (inner.amount <= 0)
      {
        continue;
      }

      float itemX = droppedTransforms[i].Position.x;
      float itemY = droppedTransforms[i].Position.z;

      if (!IsInsideInputCorridor(
              itemX,
              itemY,
              centerX,
              centerY,
              outputAxisX,
              outputAxisY,
              inputAxisX,
              inputAxisY,
              out float inputDistance))
      {
        continue;
      }

      if (inputDistance >= bestDistance)
      {
        continue;
      }

      found = true;
      bestDroppedEntity = droppedEntity;
      bestDistance = inputDistance;
      itemObject = inner.objectID;
      itemVariation = inner.variation;
      itemAmount = inner.amount;
    }

    return found;
  }

  private void ApplyArmedRoutes(
      double now,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    using NativeArray<Entity> orchestrators = _routeQuery.ToEntityArray(Allocator.Temp);

    foreach (Entity orchestrator in orchestrators)
    {
      if (!EnableRouting)
      {
        continue;
      }

      if (!IsSplitterSmartPowered(orchestrator))
      {
        if (_smartStateDirty.Contains(orchestrator))
        {
          RestoreVanillaSplitterState(orchestrator, allMovers, allMoverData);
          _smartStateDirty.Remove(orchestrator);
        }

        ClearArmedRoute(orchestrator);
        _trackedRoutes.Remove(orchestrator);
        continue;
      }

      SmartSplitterConfigCD config =
          EntityManager.GetComponentData<SmartSplitterConfigCD>(orchestrator);

      if (!config.Enabled)
      {
        continue;
      }

      SmartSplitterArmedRouteCD armed =
          EntityManager.GetComponentData<SmartSplitterArmedRouteCD>(orchestrator);

      if (!armed.HasArmedRoute || armed.Decision == SmartSplitterDecision.None)
      {
        continue;
      }

      if (armed.AppliedOnce)
      {
        if (!_trackedRoutes.TryGetValue(orchestrator, out TrackedRouteState tracked))
        {
          ClearArmedRoute(orchestrator);
          continue;
        }

        bool trackedStillExists = EntityManager.Exists(tracked.TrackedEntity);

        if (trackedStillExists)
        {
          // Keep reasserting the selected one-sided route while the owned item still exists.
          // Vanilla automation can rebuild/toggle shared mover state between ticks; if we
          // only apply once, the output can remain forced while splitsIntoOnMove returns
          // to 2, causing a stack to split into multiple stacks on the same forced side.
          SmartSplitterOriginalOutputsCD heldOriginals =
              EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

          if (!IsOriginalOutputStateValid(heldOriginals))
          {
            ClearArmedRoute(orchestrator);
            _trackedRoutes.Remove(orchestrator);
            continue;
          }

          DynamicBuffer<MoversWithSharedStateBuffer> heldBuffer =
              EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

          ApplyDecision(orchestrator, heldBuffer, heldOriginals, armed, allMovers, allMoverData);
          continue;
        }

        SmartSplitterOriginalOutputsCD appliedOriginals =
            EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

        DynamicBuffer<MoversWithSharedStateBuffer> appliedBuffer =
            EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

        RestoreBothOutputs(orchestrator, appliedBuffer, appliedOriginals);

        SetAllSplitterMoverSplitCounts(
            orchestrator,
            2,
            allMovers,
            allMoverData);

        RestoreEnabledMoverFromSharedState(appliedOriginals);

        _trackedRoutes.Remove(orchestrator);

        ClearArmedRoute(orchestrator);

        continue;
      }

      if (now > armed.ExpiresAt ||
          !EntityManager.Exists(armed.ArmedEntity))
      {
        ClearArmedRoute(orchestrator);
        continue;
      }

      SmartSplitterOriginalOutputsCD originals =
          EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

      if (!IsOriginalOutputStateValid(originals))
      {
        continue;
      }

      DynamicBuffer<MoversWithSharedStateBuffer> buffer =
          EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

      ApplyDecision(orchestrator, buffer, originals, armed, allMovers, allMoverData);

      VerifyRouteState("after-apply", orchestrator, allMovers, allMoverData);

      Debug.Log(
          $"[SmartSplitterRuntime] post-apply decision={armed.Decision} " +
          $"orchestrator={orchestrator} " +
          $"bufferLength={buffer.Length} " +
          $"leftSplits={EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity).splitsIntoOnMove} " +
          $"rightSplits={EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity).splitsIntoOnMove}");

      if (armed.Decision == SmartSplitterDecision.Both)
      {
        ClearArmedRoute(orchestrator);
        continue;
      }

      armed.AppliedOnce = true;
      armed.RouteAppliedAt = now;

      EntityManager.SetComponentData(orchestrator, armed);

      _trackedRoutes[orchestrator] = new TrackedRouteState
      {
        TrackedEntity = armed.ArmedEntity,
        LastInputDistance = float.MaxValue,
        WasCloseToSplitter = false
      };
    }
  }

  private void ApplyDecision(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals,
      SmartSplitterArmedRouteCD armed,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    switch (armed.Decision)
    {
      case SmartSplitterDecision.LeftOnly:
        ApplyLeftOnly(orchestrator, buffer, originals, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "LEFT_ONLY");
        break;

      case SmartSplitterDecision.RightOnly:
        ApplyRightOnly(orchestrator, buffer, originals, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "RIGHT_ONLY");
        break;

      case SmartSplitterDecision.Both:
        RestoreBothOutputs(orchestrator, buffer, originals);
        SetAllSplitterMoverSplitCounts(orchestrator, 2, allMovers, allMoverData);
        RestoreMoverOrchestratorCycling(orchestrator);
        LogRoute(orchestrator, armed, "BOTH");
        break;

      case SmartSplitterDecision.Blocked:
        RestoreBothOutputs(orchestrator, buffer, originals);
        SetAllSplitterMoverSplitCounts(orchestrator, 2, allMovers, allMoverData);
        RestoreMoverOrchestratorCycling(orchestrator);
        LogRoute(orchestrator, armed, "BLOCKED_FALLBACK_BOTH");
        break;
    }
  }

  private void ApplyLeftOnly(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    buffer.Clear();

    buffer.Add(new MoversWithSharedStateBuffer
    {
      moverEntity = originals.LeftMoverEntity,
      cachedDirection = originals.LeftCachedDirection,
      cachedStart = originals.LeftCachedStart
    });

    SetAllSplitterMoverSplitCounts(orchestrator, 1, allMovers, allMoverData);
    ForceMoverOrchestrator(orchestrator, originals.LeftMoverEntity);
    ForceEnabledMoverFromSharedState(originals, originals.LeftMoverEntity);
    _smartStateDirty.Add(orchestrator);
  }

  private void ApplyRightOnly(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    buffer.Clear();

    buffer.Add(new MoversWithSharedStateBuffer
    {
      moverEntity = originals.RightMoverEntity,
      cachedDirection = originals.RightCachedDirection,
      cachedStart = originals.RightCachedStart
    });

    SetAllSplitterMoverSplitCounts(orchestrator, 1, allMovers, allMoverData);
    ForceMoverOrchestrator(orchestrator, originals.RightMoverEntity);
    ForceEnabledMoverFromSharedState(originals, originals.RightMoverEntity);
    _smartStateDirty.Add(orchestrator);
  }

  private void RestoreBothOutputs(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals)
  {
    buffer.Clear();

    buffer.Add(new MoversWithSharedStateBuffer
    {
      moverEntity = originals.LeftMoverEntity,
      cachedDirection = originals.LeftCachedDirection,
      cachedStart = originals.LeftCachedStart
    });

    buffer.Add(new MoversWithSharedStateBuffer
    {
      moverEntity = originals.RightMoverEntity,
      cachedDirection = originals.RightCachedDirection,
      cachedStart = originals.RightCachedStart
    });

    RestoreMoverOrchestratorCycling(orchestrator);
    RestoreEnabledMoverFromSharedState(originals);
  }

  private void ForceMoverOrchestrator(Entity orchestrator, Entity selectedMoverEntity)
  {
    if (!EntityManager.HasComponent<MoverOrchestratorCD>(orchestrator) ||
        !EntityManager.HasComponent<MoverCD>(selectedMoverEntity))
    {
      return;
    }

    MoverCD selectedMover = EntityManager.GetComponentData<MoverCD>(selectedMoverEntity);
    MoverOrchestratorCD orchestratorData = EntityManager.GetComponentData<MoverOrchestratorCD>(orchestrator);

    orchestratorData.enabledMoverIndex = selectedMover.indexInOrchestrator;
    orchestratorData.nextMoverCycleIncrement = 0;

    EntityManager.SetComponentData(orchestrator, orchestratorData);
  }

  private void RestoreMoverOrchestratorCycling(Entity orchestrator)
  {
    if (!EntityManager.HasComponent<MoverOrchestratorCD>(orchestrator))
    {
      return;
    }

    MoverOrchestratorCD orchestratorData = EntityManager.GetComponentData<MoverOrchestratorCD>(orchestrator);

    orchestratorData.nextMoverCycleIncrement = 1;

    if (orchestratorData.enabledMoverIndex < 0)
    {
      orchestratorData.enabledMoverIndex = 0;
    }

    EntityManager.SetComponentData(orchestrator, orchestratorData);
  }

  private void SetAllSplitterMoverSplitCounts(
      Entity orchestrator,
      int splitCount,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    for (int i = 0; i < allMovers.Length; i++)
    {
      MoverCD mover = allMoverData[i];

      if (mover.moverOrchestratorEntity != orchestrator)
      {
        continue;
      }

      if (mover.splitsIntoOnMove == splitCount)
      {
        continue;
      }

      mover.splitsIntoOnMove = splitCount;
      EntityManager.SetComponentData(allMovers[i], mover);
    }
  }

  private bool IsOriginalOutputStateValid(SmartSplitterOriginalOutputsCD originals)
  {
    return originals.HasOriginalOutputs &&
           EntityManager.Exists(originals.LeftMoverEntity) &&
           EntityManager.Exists(originals.RightMoverEntity) &&
           EntityManager.HasComponent<MoverCD>(originals.LeftMoverEntity) &&
           EntityManager.HasComponent<MoverCD>(originals.RightMoverEntity);
  }

  private static SmartSplitterArmedRouteCD EmptyArmedRoute()
  {
    return new SmartSplitterArmedRouteCD
    {
      HasArmedRoute = false,
      AppliedOnce = false,
      VerifiedHoldState = false,
      ArmedEntity = Entity.Null,
      Decision = SmartSplitterDecision.None,
      ItemObject = ObjectID.None,
      ItemVariation = 0,
      ItemAmount = 0,
      ArmedAt = 0d,
      ExpiresAt = 0d,
      RouteAppliedAt = 0d,
      HoldUntil = 0d
    };
  }

  private void ClearArmedRoute(Entity orchestrator)
  {
    EntityManager.SetComponentData(orchestrator, EmptyArmedRoute());
  }

  private static bool IsInsideInputCorridor(
      float itemX,
      float itemY,
      float centerX,
      float centerY,
      float outputAxisX,
      float outputAxisY,
      float inputAxisX,
      float inputAxisY,
      out float inputDistance)
  {
    float offsetX = itemX - centerX;
    float offsetY = itemY - centerY;

    float outputAxisDistance = Mathf.Abs(Dot(offsetX, offsetY, outputAxisX, outputAxisY));
    inputDistance = Mathf.Abs(Dot(offsetX, offsetY, inputAxisX, inputAxisY));

    if (outputAxisDistance > InputLaneHalfWidth)
    {
      return false;
    }

    if (inputDistance < MinInputDistanceFromCenter || inputDistance > InputDetectDistance)
    {
      return false;
    }

    return true;
  }

  private static SmartSplitterDecision DecideRoute(
      SmartSplitterConfigCD config,
      ObjectID itemObject,
      int itemVariation)
  {
    bool matchesLeft =
        itemObject == config.LeftFilterObject &&
        itemVariation == config.LeftFilterVariation;

    bool matchesRight =
        itemObject == config.RightFilterObject &&
        itemVariation == config.RightFilterVariation;

    if (matchesLeft && matchesRight)
    {
      return SmartSplitterDecision.Both;
    }

    if (matchesLeft)
    {
      return SmartSplitterDecision.LeftOnly;
    }

    if (matchesRight)
    {
      return SmartSplitterDecision.RightOnly;
    }

    return SmartSplitterDecision.Blocked;
  }

  private SmartSplitterDecision ApplyBothSingleAlternation(
    Entity orchestrator,
    SmartSplitterDecision decision,
    int itemAmount)
  {
    if (decision != SmartSplitterDecision.Both || itemAmount > 1)
    {
      return decision;
    }

    bool nextRight =
        !_bothSingleNextRight.TryGetValue(orchestrator, out bool current) || current;

    _bothSingleNextRight[orchestrator] = !nextRight;

    return nextRight
        ? SmartSplitterDecision.RightOnly
        : SmartSplitterDecision.LeftOnly;
  }

  private void LogDecisionIfEnabled(
      CachedSplitter splitter,
      Entity armedEntity,
      ObjectID itemObject,
      int itemVariation,
      int itemAmount,
      SmartSplitterDecision decision,
      float distance,
      double now)
  {
    if (!EnableDecisionLogs)
    {
      return;
    }

    string logKey = $"{splitter.Orchestrator}|{armedEntity}|{itemObject}|{itemVariation}|{itemAmount}|{decision}|{distance:0.00}";

    if (now < splitter.NextLogTime && splitter.LastLogKey == logKey)
    {
      return;
    }

    splitter.LastLogKey = logKey;
    splitter.NextLogTime = now + DecisionLogCooldownSeconds;

    Debug.Log(
        $"[SmartSplitterRuntime] armed orchestrator={splitter.Orchestrator} " +
        $"entity={armedEntity} item={itemObject} variation={itemVariation} amount={itemAmount} " +
        $"decision={decision} inputDistance={distance:0.00}");
  }

  private void LogRoute(
      Entity orchestrator,
      SmartSplitterArmedRouteCD armed,
      string route)
  {
    if (!EnableRoutingLogs)
    {
      return;
    }

    Debug.Log(
        $"[SmartSplitterRuntime] route={route} orchestrator={orchestrator} " +
        $"armedEntity={armed.ArmedEntity} item={armed.ItemObject} " +
        $"variation={armed.ItemVariation} amount={armed.ItemAmount}");
  }

  private void PruneRecentlyArmedEntities(double now)
  {
    if (_recentlyArmedEntities.Count == 0)
    {
      return;
    }

    List<Entity> expired = null;

    foreach (KeyValuePair<Entity, double> entry in _recentlyArmedEntities)
    {
      if (now <= entry.Value)
      {
        continue;
      }

      expired ??= new List<Entity>();
      expired.Add(entry.Key);
    }

    if (expired == null)
    {
      return;
    }

    foreach (Entity entity in expired)
    {
      _recentlyArmedEntities.Remove(entity);
    }
  }

  private void VerifyRouteState(
      string phase,
      Entity orchestrator,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    if (!EnableRoutingLogs)
    {
      return;
    }

    if (!EntityManager.Exists(orchestrator) ||
        !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
    {
      Debug.Log($"[SmartSplitterRuntime] verify {phase} orchestrator={orchestrator} missing");
      return;
    }

    DynamicBuffer<MoversWithSharedStateBuffer> buffer =
        EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator);

    string bufferMover = buffer.Length > 0 ? buffer[0].moverEntity.ToString() : "none";

    Debug.Log(
        $"[SmartSplitterRuntime] verify {phase} orchestrator={orchestrator} " +
        $"bufferLength={buffer.Length} firstBufferMover={bufferMover}");

    for (int i = 0; i < allMovers.Length; i++)
    {
      MoverCD mover = EntityManager.GetComponentData<MoverCD>(allMovers[i]);

      if (mover.moverOrchestratorEntity != orchestrator)
      {
        continue;
      }

      Debug.Log(
          $"[SmartSplitterRuntime] verify {phase} mover={allMovers[i]} " +
          $"index={mover.indexInOrchestrator} start={mover.start} stop={mover.stop} " +
          $"splits={mover.splitsIntoOnMove}");
    }

    if (EntityManager.HasComponent<DeactivateSharedMoversTriggerCD>(orchestrator))
    {
      bool triggerEnabled =
          EntityManager.IsComponentEnabled<DeactivateSharedMoversTriggerCD>(orchestrator);

      Debug.Log($"[SmartSplitterRuntime] verify {phase} deactivateTriggerEnabled={triggerEnabled}");
    }

    if (EntityManager.HasComponent<DeactivateSharedMoversTriggerEntityCD>(orchestrator))
    {
      Debug.Log($"[SmartSplitterRuntime] verify {phase} has DeactivateSharedMoversTriggerEntityCD");
    }

    if (EntityManager.HasComponent<MoverOrchestratorCD>(orchestrator))
    {
      var orchestratorData =
          EntityManager.GetComponentData<MoverOrchestratorCD>(orchestrator);

      Debug.Log(
          $"[SmartSplitterRuntime] verify {phase} " +
          $"enabledMoverIndex={orchestratorData.enabledMoverIndex} " +
          $"nextMoverCycleIncrement={orchestratorData.nextMoverCycleIncrement}");
    }
  }

  private void ForceEnabledMoverFromSharedState(
      SmartSplitterOriginalOutputsCD originals,
      Entity selectedMoverEntity)
  {
    SetEnabledMoverFromSharedState(originals.LeftMoverEntity, originals.LeftMoverEntity == selectedMoverEntity);
    SetEnabledMoverFromSharedState(originals.RightMoverEntity, originals.RightMoverEntity == selectedMoverEntity);
  }

  private void RestoreEnabledMoverFromSharedState(SmartSplitterOriginalOutputsCD originals)
  {
    SetEnabledMoverFromSharedState(originals.LeftMoverEntity, true);
    SetEnabledMoverFromSharedState(originals.RightMoverEntity, true);
  }

  private void SetEnabledMoverFromSharedState(Entity moverEntity, bool enabled)
  {
    if (!EntityManager.Exists(moverEntity) ||
        !EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(moverEntity))
    {
      return;
    }

    EntityManager.SetComponentEnabled<EnabledMoverFromSharedStateCD>(moverEntity, enabled);
  }

  private static bool TryNormalize(float x, float y, out float normalizedX, out float normalizedY)
  {
    float length = Mathf.Sqrt(x * x + y * y);

    if (length <= 0.0001f)
    {
      normalizedX = 0f;
      normalizedY = 0f;
      return false;
    }

    normalizedX = x / length;
    normalizedY = y / length;
    return true;
  }

  private static float Dot(float ax, float ay, float bx, float by)
  {
    return ax * bx + ay * by;
  }
}
