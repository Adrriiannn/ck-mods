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
[UpdateBefore(typeof(PugAutomationSystem))]
public partial class SmartSplitterRuntimeSystem : SystemBase
{
  private sealed class CachedSplitter
  {
    public Entity Orchestrator;
    public double NextLogTime;
    public string LastLogKey = string.Empty;
    public double NextCoreLogTime;
    public string LastCoreLogKey = string.Empty;
  }

  private sealed class TrackedRouteState
  {
    public Entity TrackedEntity;
    public float LastInputDistance;
    public bool WasCloseToSplitter;
  }

  private readonly Dictionary<Entity, CachedSplitter> _splitters = new();
  private readonly Dictionary<Entity, int> _laneRoundRobinIndex = new();
  private readonly Dictionary<Entity, TrackedRouteState> _trackedRoutes = new();
  private readonly Dictionary<Entity, double> _recentlyReleasedRoutedEntities = new();
  private readonly Dictionary<Entity, bool> _splitterPowerState = new();
  private readonly Dictionary<Entity, MoverCD> _patchedCenterOutputOriginalMovers = new();
  private readonly Dictionary<Entity, bool> _patchedCenterOutputAddedSharedState = new();
  private readonly HashSet<Entity> _smartStateDirty = new();
  private readonly HashSet<long> _poweredElectricityTiles = new();
  private bool _loggedFirstRegisteredSplitter;

  private static bool EnableRouting = true;
  private static bool EnableDecisionLogs = false;
  private static bool EnableRoutingLogs = false;
  private static bool EnableElectricityGateLogs = false;
  private static bool EnableCoreRoutingDiagnostics = true;

  private static bool EnableElectricityDeepScan = false;
  private int _coreRoutingDiagnosticLogs;
  private const int MaxCoreRoutingDiagnosticLogs = 300;

  private double _electricityDeepScanAt = -1d;
  private const double ElectricityDeepScanDelaySeconds = 3.0d;

  private const float InputDetectDistance = 4.20f;
  private const float InputLaneHalfWidth = 0.35f;
  private const float MinInputDistanceFromCenter = 0.15f;
  private const float WrongSideBlockDistanceFromCenter = 0.75f;

  private const double ArmedRouteDurationSeconds = 8.00d;
  private const double DecisionLogCooldownSeconds = 0.75d;
  private const double RecentlyReleasedRoutedEntityIgnoreSeconds = 1.50d;

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
    Debug.Log($"[SmartSplitterRuntimeSystem] Created in world={World?.Name ?? "unknown"}");

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
          ComponentType.ReadOnly<ObjectDataCD>(),
          ComponentType.ReadOnly<LocalTransform>()
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
      None = new[]
      {
        ComponentType.ReadOnly<EntityDestroyedCD>()
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

    BuildPoweredElectricityTileSet(electricityData, electricityTransforms, _poweredElectricityTiles);

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

    PruneRecentlyReleasedRoutedEntities(now);
    UpdateTrackedRoutes(now, droppedEntities, droppedTransforms, allMovers, allMoverData);

    ApplyElectricityGateToSplitters(
        _poweredElectricityTiles,
        allMovers,
        allMoverData);

    // Route only real incoming item entities. Feeder/robot-arm carried-item
    // prediction can consume round-robin decisions before the item exists.
    ObserveSplitters(now, droppedEntities, droppedOuterObjects, droppedTransforms, allMovers, allMoverData);
    ApplyArmedRoutes(
        now,
        allMovers,
        allMoverData,
        droppedEntities,
        droppedTransforms);
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
          Enabled = true
        });
      }
      else
      {
        SmartSplitterConfigCD config = EntityManager.GetComponentData<SmartSplitterConfigCD>(orchestrator);
        config.Enabled = true;
        EntityManager.SetComponentData(orchestrator, config);
      }

      if (!EntityManager.HasComponent<SmartSplitterLaneFiltersCD>(orchestrator))
      {
        EntityManager.AddComponentData(orchestrator, CreateDefaultLaneFilters());
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

      if (!_loggedFirstRegisteredSplitter)
      {
        _loggedFirstRegisteredSplitter = true;
        Debug.Log($"[SmartSplitterRuntimeSystem] Registered first splitter orchestrator={orchestrator}");
      }
    }
  }

  private void ApplyElectricityGateToSplitters(
      HashSet<long> poweredElectricityTiles,
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
          poweredElectricityTiles);

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
      HashSet<long> poweredElectricityTiles)
  {
    if (!TryGetSplitterCenterTile(orchestrator, out int splitterX, out int splitterY))
    {
      return false;
    }

    return poweredElectricityTiles.Contains(GetTileKey(splitterX, splitterY)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX + 1, splitterY)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX - 1, splitterY)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX, splitterY + 1)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX, splitterY - 1));
  }

  private static void BuildPoweredElectricityTileSet(
      NativeArray<ElectricityCD> electricityData,
      NativeArray<LocalTransform> electricityTransforms,
      HashSet<long> poweredElectricityTiles)
  {
    poweredElectricityTiles.Clear();

    for (int i = 0; i < electricityData.Length; i++)
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

      poweredElectricityTiles.Add(GetTileKey(powerX, powerY));
    }
  }

  private static long GetTileKey(int x, int y)
  {
    return ((long)x << 32) ^ (uint)y;
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
          !EntityManager.HasComponent<SmartSplitterLaneFiltersCD>(splitter.Orchestrator) ||
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
            splitter.Orchestrator,
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
      if (TryBlockWrongSideItemAtSplitterCenter(
              splitter,
              now,
              leftMover,
              rightMover,
              originals,
              droppedEntities,
              droppedOuterObjects,
              droppedTransforms,
              allMovers,
              allMoverData))
      {
        return;
      }

      LogNoIncomingProbe(
          splitter,
          now,
          leftMover,
          rightMover,
          originals,
          droppedEntities,
          droppedOuterObjects,
          droppedTransforms,
          allMovers,
          allMoverData);
      return;
    }

    SmartSplitterLaneFiltersCD filters =
        EntityManager.GetComponentData<SmartSplitterLaneFiltersCD>(splitter.Orchestrator);

    SmartSplitterDecision decision = DecideRoute(filters, itemObject, itemVariation);

    SmartSplitterArmedRouteCD currentArmed =
        EntityManager.GetComponentData<SmartSplitterArmedRouteCD>(splitter.Orchestrator);

    bool sameArmedRoute =
        currentArmed.HasArmedRoute &&
        currentArmed.ArmedEntity == bestDroppedEntity &&
        currentArmed.ItemObject == itemObject &&
        currentArmed.ItemVariation == itemVariation &&
        currentArmed.ItemAmount == itemAmount &&
        now <= currentArmed.ExpiresAt;

    if (sameArmedRoute)
    {
      return;
    }

    decision = SelectLanesForAmount(
        splitter.Orchestrator,
        decision,
        itemAmount,
        out int routeStartLaneIndex,
        out int nextRouteStartLaneIndex);

    LogCoreRoutingThrottled(
        splitter,
        $"arm|{bestDroppedEntity}|{itemObject}|{itemVariation}|{itemAmount}|{decision}",
        $"arm orchestrator={splitter.Orchestrator} entity={bestDroppedEntity} " +
        $"item={itemObject}/{itemVariation} amount={itemAmount} distance={distance:0.00} " +
        $"selectedDecision={decision} routeStart={routeStartLaneIndex} nextStart={nextRouteStartLaneIndex}",
        now,
        0.15d);

    ArmRoute(
        splitter,
        bestDroppedEntity,
        itemObject,
        itemVariation,
        itemAmount,
        decision,
        routeStartLaneIndex,
        nextRouteStartLaneIndex,
        distance,
        now);

    _trackedRoutes[splitter.Orchestrator] = new TrackedRouteState
    {
      TrackedEntity = bestDroppedEntity,
      LastInputDistance = distance,
      WasCloseToSplitter = distance <= RouteTrackingCloseDistance
    };
  }

  private bool TryBlockWrongSideItemAtSplitterCenter(
      CachedSplitter splitter,
      double now,
      MoverCD leftMover,
      MoverCD rightMover,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> droppedEntities,
      NativeArray<ObjectDataCD> droppedOuterObjects,
      NativeArray<LocalTransform> droppedTransforms,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    if (!EntityManager.Exists(splitter.Orchestrator) ||
        !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(splitter.Orchestrator))
    {
      return false;
    }

    int2 centerTile = new int2(
        Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
        Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

    if (!TryGetSmartInputDirection(splitter.Orchestrator, centerTile, out int2 configuredInputDirection) ||
        !TryGetInputCorridorGeometry(
            leftMover,
            rightMover,
            configuredInputDirection,
            out float centerX,
            out float centerY,
            out float outputAxisX,
            out float outputAxisY,
            out float inputAxisX,
            out float inputAxisY))
    {
      return false;
    }

    Entity blockedEntity = Entity.Null;
    ObjectID blockedItem = ObjectID.None;
    int blockedVariation = 0;
    int blockedAmount = 0;
    float bestDistanceSq = WrongSideBlockDistanceFromCenter * WrongSideBlockDistanceFromCenter;
    float blockedSignedInputDistance = 0f;
    float blockedLateralDistance = 0f;

    for (int i = 0; i < droppedEntities.Length; i++)
    {
      Entity droppedEntity = droppedEntities[i];

      if (droppedOuterObjects[i].objectID != ObjectID.DroppedItem ||
          !EntityManager.Exists(droppedEntity) ||
          !EntityManager.HasBuffer<ContainedObjectsBuffer>(droppedEntity))
      {
        continue;
      }

      if (_recentlyReleasedRoutedEntities.TryGetValue(droppedEntity, out double ignoreUntil) &&
          now <= ignoreUntil)
      {
        continue;
      }

      DynamicBuffer<ContainedObjectsBuffer> contained =
          EntityManager.GetBuffer<ContainedObjectsBuffer>(droppedEntity);

      if (contained.Length == 0)
      {
        continue;
      }

      float itemX = droppedTransforms[i].Position.x;
      float itemY = droppedTransforms[i].Position.z;
      float offsetX = itemX - centerX;
      float offsetY = itemY - centerY;
      float distanceSq = offsetX * offsetX + offsetY * offsetY;

      if (distanceSq >= bestDistanceSq)
      {
        continue;
      }

      bool validInputSide = IsInsideInputCorridor(
          itemX,
          itemY,
          centerX,
          centerY,
          outputAxisX,
          outputAxisY,
          inputAxisX,
          inputAxisY,
          out _);

      if (validInputSide)
      {
        continue;
      }

      var inner = contained[0].objectData;
      bestDistanceSq = distanceSq;
      blockedEntity = droppedEntity;
      blockedItem = inner.objectID;
      blockedVariation = inner.variation;
      blockedAmount = inner.amount;
      blockedSignedInputDistance = Dot(offsetX, offsetY, inputAxisX, inputAxisY);
      blockedLateralDistance = Mathf.Abs(Dot(offsetX, offsetY, outputAxisX, outputAxisY));
    }

    if (blockedEntity == Entity.Null)
    {
      return false;
    }

    DynamicBuffer<MoversWithSharedStateBuffer> buffer =
        EntityManager.GetBuffer<MoversWithSharedStateBuffer>(splitter.Orchestrator);

    ApplyBlocked(splitter.Orchestrator, buffer, originals, allMovers, allMoverData);
    ClearArmedRoute(splitter.Orchestrator);

    LogCoreRoutingThrottled(
        splitter,
        $"wrong-side-block|{blockedEntity}|{blockedItem}|{blockedVariation}|{blockedAmount}",
        $"wrong-side-block orchestrator={splitter.Orchestrator} entity={blockedEntity} " +
        $"item={blockedItem}/{blockedVariation} amount={blockedAmount} center=({centerX:0.00},{centerY:0.00}) " +
        $"configuredInput={FormatInt2(configuredInputDirection)} signedInput={blockedSignedInputDistance:0.00} " +
        $"lateral={blockedLateralDistance:0.00}",
        now,
        0.50d);

    return true;
  }

  private void LogNoIncomingProbe(
      CachedSplitter splitter,
      double now,
      MoverCD leftMover,
      MoverCD rightMover,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> droppedEntities,
      NativeArray<ObjectDataCD> droppedOuterObjects,
      NativeArray<LocalTransform> droppedTransforms,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    if (!EnableCoreRoutingDiagnostics)
    {
      return;
    }

    int2 centerTile = new int2(
        Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
        Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

    if (!TryGetSmartInputDirection(splitter.Orchestrator, centerTile, out int2 configuredInputDirection) ||
        !TryGetInputCorridorGeometry(
            leftMover,
            rightMover,
            configuredInputDirection,
            out float centerX,
            out float centerY,
            out float outputAxisX,
            out float outputAxisY,
            out float inputAxisX,
            out float inputAxisY))
    {
      LogCoreRoutingThrottled(
          splitter,
          "no-incoming-no-geometry",
          $"no-incoming reason=no_configured_geometry orchestrator={splitter.Orchestrator} center={FormatInt2(centerTile)}",
          now,
          1.0d);
      return;
    }

    Entity closestEntity = Entity.Null;
    ObjectID closestItem = ObjectID.None;
    int closestVariation = 0;
    int closestAmount = 0;
    float closestDistanceSq = 25f;
    float closestSignedInputDistance = 0f;
    float closestOutputAxisDistance = 0f;
    bool closestInside = false;

    for (int i = 0; i < droppedEntities.Length; i++)
    {
      Entity droppedEntity = droppedEntities[i];

      if (droppedOuterObjects[i].objectID != ObjectID.DroppedItem ||
          !EntityManager.Exists(droppedEntity) ||
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

      float itemX = droppedTransforms[i].Position.x;
      float itemY = droppedTransforms[i].Position.z;
      float offsetX = itemX - centerX;
      float offsetY = itemY - centerY;
      float distanceSq = offsetX * offsetX + offsetY * offsetY;

      if (distanceSq >= closestDistanceSq)
      {
        continue;
      }

      var inner = contained[0].objectData;
      closestDistanceSq = distanceSq;
      closestEntity = droppedEntity;
      closestItem = inner.objectID;
      closestVariation = inner.variation;
      closestAmount = inner.amount;
      closestSignedInputDistance = Dot(offsetX, offsetY, inputAxisX, inputAxisY);
      closestOutputAxisDistance = Mathf.Abs(Dot(offsetX, offsetY, outputAxisX, outputAxisY));
      closestInside = IsInsideInputCorridor(
          itemX,
          itemY,
          centerX,
          centerY,
          outputAxisX,
          outputAxisY,
          inputAxisX,
          inputAxisY,
          out _);
    }

    int placedVariation = TryGetPlacedSplitterVariationAtCenter(splitter.Orchestrator, centerTile, out int variation)
        ? variation
        : -1;

    bool hasPhysicalTopology = TryGetPhysicalForwardTopology(
        originals,
        allMovers,
        allMoverData,
        out int2 physicalCenter,
        out int2 physicalBack,
        out int2 physicalForward,
        out int2 physicalForwardDirection,
        out Entity physicalInputMover,
        out Entity physicalForwardMover);

    LogCoreRoutingThrottled(
        splitter,
        $"no-incoming|{closestEntity}|{closestSignedInputDistance:0.00}|{closestOutputAxisDistance:0.00}|{closestInside}",
        $"no-incoming orchestrator={splitter.Orchestrator} placedVariation={placedVariation} " +
        $"spriteVariation={SmartSplitterOrientationUtility.GetSmartSpriteVariationForPlacedVariation(placedVariation)} " +
        $"configuredInput={FormatInt2(configuredInputDirection)} center=({centerX:0.00},{centerY:0.00}) " +
        $"closest={closestEntity} item={closestItem}/{closestVariation} amount={closestAmount} " +
        $"signedInput={closestSignedInputDistance:0.00} lateral={closestOutputAxisDistance:0.00} inside={closestInside} " +
        $"physicalTopology={hasPhysicalTopology} physicalCenter={FormatInt2(physicalCenter)} " +
        $"physicalBack={FormatInt2(physicalBack)} physicalForward={FormatInt2(physicalForward)} " +
        $"physicalForwardDir={FormatInt2(physicalForwardDirection)} inputMover={physicalInputMover} forwardMover={physicalForwardMover}",
        now,
        0.75d);
  }

  private void ArmRoute(
      CachedSplitter splitter,
      Entity armedEntity,
      ObjectID itemObject,
      int itemVariation,
      int itemAmount,
      SmartSplitterDecision decision,
      int routeStartLaneIndex,
      int nextRouteStartLaneIndex,
      float distance,
      double now)
  {
    if (!EntityManager.Exists(splitter.Orchestrator) ||
        !EntityManager.HasComponent<SmartSplitterArmedRouteCD>(splitter.Orchestrator))
    {
      return;
    }

    EntityManager.SetComponentData(splitter.Orchestrator, new SmartSplitterArmedRouteCD
    {
      HasArmedRoute = true,
      AppliedOnce = false,
      ArmedEntity = armedEntity,
      Decision = decision,
      ItemObject = itemObject,
      ItemVariation = itemVariation,
      ItemAmount = itemAmount,
      RouteStartLaneIndex = routeStartLaneIndex,
      NextRouteStartLaneIndex = nextRouteStartLaneIndex,
      ArmedAt = now,
      ExpiresAt = now + ArmedRouteDurationSeconds,
      RouteAppliedAt = 0d
    });

    _laneRoundRobinIndex[splitter.Orchestrator] = nextRouteStartLaneIndex;

    LogDecisionIfEnabled(splitter, armedEntity, itemObject, itemVariation, itemAmount, decision, distance, now);
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
    double now,
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

      if (!TryGetTrackedDroppedItemInputDistance(
              orchestrator,
              tracked.TrackedEntity,
              originals,
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
      if (_trackedRoutes.TryGetValue(orchestrator, out TrackedRouteState tracked) &&
          tracked.TrackedEntity != Entity.Null)
      {
        _recentlyReleasedRoutedEntities[tracked.TrackedEntity] =
            now + RecentlyReleasedRoutedEntityIgnoreSeconds;
      }

      RestoreVanillaSplitterState(orchestrator, allMovers, allMoverData);
      ClearArmedRoute(orchestrator);
      _trackedRoutes.Remove(orchestrator);
    }
  }

  private void PruneRecentlyReleasedRoutedEntities(double now)
  {
    if (_recentlyReleasedRoutedEntities.Count == 0)
    {
      return;
    }

    List<Entity> stale = null;

    foreach (KeyValuePair<Entity, double> entry in _recentlyReleasedRoutedEntities)
    {
      if (now <= entry.Value)
      {
        continue;
      }

      stale ??= new List<Entity>();
      stale.Add(entry.Key);
    }

    if (stale == null)
    {
      return;
    }

    foreach (Entity entity in stale)
    {
      _recentlyReleasedRoutedEntities.Remove(entity);
    }
  }

  private bool TryGetTrackedDroppedItemInputDistance(
      Entity orchestrator,
      Entity trackedEntity,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> droppedEntities,
      NativeArray<LocalTransform> droppedTransforms,
      out float inputDistance)
  {
    inputDistance = float.MaxValue;

    if (!IsOriginalOutputStateValid(originals))
    {
      return false;
    }

    MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
    MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

    int2 centerTile = new int2(
        Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
        Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

    if (!TryGetSmartInputDirection(orchestrator, centerTile, out int2 inputDirection) ||
        !TryGetInputCorridorGeometry(
            leftMover,
            rightMover,
            inputDirection,
            out float centerX,
            out float centerY,
            out float outputAxisX,
            out float outputAxisY,
            out float inputAxisX,
            out float inputAxisY))
    {
      return false;
    }

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
      Entity orchestrator,
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

    int2 centerTile = new int2(
        Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
        Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

    if (!TryGetSmartInputDirection(orchestrator, centerTile, out int2 inputDirection) ||
        !TryGetInputCorridorGeometry(
            leftMover,
            rightMover,
            inputDirection,
            out float centerX,
            out float centerY,
            out float outputAxisX,
            out float outputAxisY,
            out float inputAxisX,
            out float inputAxisY))
    {
      return false;
    }

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
      NativeArray<MoverCD> allMoverData,
      NativeArray<Entity> droppedEntities,
      NativeArray<LocalTransform> droppedTransforms)
  {
    using NativeArray<Entity> orchestrators = _routeQuery.ToEntityArray(Allocator.Temp);

    foreach (Entity orchestrator in orchestrators)
    {
      if (!EnableRouting)
      {
        continue;
      }

      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterConfigCD>(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterArmedRouteCD>(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator) ||
          !EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
      {
        _trackedRoutes.Remove(orchestrator);
        _smartStateDirty.Remove(orchestrator);
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

      if (EnableRoutingLogs)
      {
        Debug.Log(
            $"[SmartSplitterRuntime] post-apply decision={armed.Decision} " +
            $"orchestrator={orchestrator} " +
            $"bufferLength={buffer.Length} " +
            $"leftSplits={EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity).splitsIntoOnMove} " +
            $"rightSplits={EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity).splitsIntoOnMove}");
      }

      armed.AppliedOnce = true;
      armed.RouteAppliedAt = now;

      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterArmedRouteCD>(orchestrator))
      {
        _trackedRoutes.Remove(orchestrator);
        continue;
      }

      EntityManager.SetComponentData(orchestrator, armed);

      _trackedRoutes[orchestrator] = new TrackedRouteState
      {
        TrackedEntity = armed.ArmedEntity,
        LastInputDistance = float.MaxValue,
        WasCloseToSplitter = false
      };
    }
  }


  private bool TryFindCenterOutputMover(
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData,
      int2 center,
      int2 forwardDirection,
      out int2 forward,
      out Entity forwardMover)
  {
    forward = default;
    forwardMover = Entity.Null;

    if (!IsOriginalOutputStateValid(originals) ||
        (forwardDirection.x == 0 && forwardDirection.y == 0))
    {
      return false;
    }

    forward = new int2(center.x + forwardDirection.x, center.y + forwardDirection.y);

    for (int i = 0; i < allMovers.Length; i++)
    {
      Entity moverEntity = allMovers[i];
      MoverCD mover = allMoverData[i];

      if (mover.start.x != forward.x || mover.start.y != forward.y)
      {
        continue;
      }

      int2 direction = new int2(mover.stop.x - mover.start.x, mover.stop.y - mover.start.y);

      if (direction.x == forwardDirection.x && direction.y == forwardDirection.y)
      {
        forwardMover = moverEntity;
        return true;
      }
    }

    return false;
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
        ApplySmartLaneOutputs(orchestrator, buffer, originals, armed, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "LEFT_ONLY");
        break;

      case SmartSplitterDecision.RightOnly:
        ApplySmartLaneOutputs(orchestrator, buffer, originals, armed, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "RIGHT_ONLY");
        break;

      case SmartSplitterDecision.Both:
        ApplySmartLaneOutputs(orchestrator, buffer, originals, armed, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "BOTH");
        break;

      case SmartSplitterDecision.CenterOnly:
        ApplySmartLaneOutputs(orchestrator, buffer, originals, armed, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "CENTER_ONLY");
        break;

      case SmartSplitterDecision.LeftCenter:
        ApplySmartLaneOutputs(orchestrator, buffer, originals, armed, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "LEFT_CENTER");
        break;

      case SmartSplitterDecision.CenterRight:
        ApplySmartLaneOutputs(orchestrator, buffer, originals, armed, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "CENTER_RIGHT");
        break;

      case SmartSplitterDecision.All:
        ApplySmartLaneOutputs(orchestrator, buffer, originals, armed, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "ALL");
        break;

      case SmartSplitterDecision.Blocked:
        ApplyBlocked(orchestrator, buffer, originals, allMovers, allMoverData);
        LogRoute(orchestrator, armed, "BLOCKED");
        break;
    }
  }

  private void ApplyBlocked(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    RestorePatchedCenterOutputMoversForOrchestrator(orchestrator);
    buffer.Clear();
    SetAllSplitterMoverSplitCounts(orchestrator, 0, allMovers, allMoverData);
    ForceEnabledMoverFromSharedState(originals, Entity.Null);
    _smartStateDirty.Add(orchestrator);
  }

  private void ApplySmartLaneOutputs(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals,
      SmartSplitterArmedRouteCD armed,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    buffer.Clear();

    if (!TryGetSmartLaneDirections(
            orchestrator,
            originals,
            out int2 center,
            out int2 forwardDirection,
            out int2 leftDirection,
            out int2 rightDirection))
    {
      ApplyBlocked(orchestrator, buffer, originals, allMovers, allMoverData);
      return;
    }

    bool includeLeft = DecisionIncludesLane(armed.Decision, SmartSplitterLane.Left);
    bool includeCenter = DecisionIncludesLane(armed.Decision, SmartSplitterLane.Center);
    bool includeRight = DecisionIncludesLane(armed.Decision, SmartSplitterLane.Right);

    LogCoreRouting(
        $"apply-lanes orchestrator={orchestrator} entity={armed.ArmedEntity} decision={armed.Decision} " +
        $"includeLeft={includeLeft} includeCenter={includeCenter} includeRight={includeRight} " +
        $"center={FormatInt2(center)} forwardDir={FormatInt2(forwardDirection)} " +
        $"leftDir={FormatInt2(leftDirection)} rightDir={FormatInt2(rightDirection)} amount={armed.ItemAmount}");

    RetargetMoverPath(originals.LeftMoverEntity, center, leftDirection);
    RetargetMoverPath(originals.RightMoverEntity, center, rightDirection);

    Entity centerMoverEntity = Entity.Null;

    if (includeCenter)
    {
      if (!TryPrepareCenterOutputMover(
              orchestrator,
              originals,
              allMovers,
              allMoverData,
              center,
              forwardDirection,
              out centerMoverEntity))
      {
        LogCoreRouting(
            $"apply-lanes blocked reason=center_mover_not_found orchestrator={orchestrator} " +
            $"center={FormatInt2(center)} forwardDir={FormatInt2(forwardDirection)}");
        ApplyBlocked(orchestrator, buffer, originals, allMovers, allMoverData);
        return;
      }
    }

    int laneCount = CountDecisionLanes(armed.Decision);
    int requestedSplitCount = armed.ItemAmount <= 0
        ? 1
        : math.min(armed.ItemAmount, laneCount);
    int splitCount = math.max(1, requestedSplitCount);
    int routeStartIndex = ((armed.RouteStartLaneIndex % 3) + 3) % 3;

    for (int offset = 0; offset < 3; offset++)
    {
      SmartSplitterLane lane = (SmartSplitterLane)((routeStartIndex + offset) % 3);

      if (!DecisionIncludesLane(armed.Decision, lane))
      {
        continue;
      }

      switch (lane)
      {
        case SmartSplitterLane.Left:
          ConfigureRouteMover(
              originals.LeftMoverEntity,
              orchestrator,
              center,
              leftDirection,
              buffer.Length,
              splitCount);
          buffer.Add(new MoversWithSharedStateBuffer
          {
            moverEntity = originals.LeftMoverEntity,
            cachedDirection = leftDirection,
            cachedStart = center
          });
          break;

        case SmartSplitterLane.Center:
          ConfigureRouteMover(
              centerMoverEntity,
              orchestrator,
              center,
              forwardDirection,
              buffer.Length,
              splitCount);
          buffer.Add(new MoversWithSharedStateBuffer
          {
            moverEntity = centerMoverEntity,
            cachedDirection = forwardDirection,
            cachedStart = center
          });
          break;

        case SmartSplitterLane.Right:
          ConfigureRouteMover(
              originals.RightMoverEntity,
              orchestrator,
              center,
              rightDirection,
              buffer.Length,
              splitCount);
          buffer.Add(new MoversWithSharedStateBuffer
          {
            moverEntity = originals.RightMoverEntity,
            cachedDirection = rightDirection,
            cachedStart = center
          });
          break;
      }
    }

    SetAllSplitterMoverSplitCounts(orchestrator, splitCount, allMovers, allMoverData);

    if (EntityManager.HasComponent<MoverOrchestratorCD>(orchestrator) && buffer.Length > 0)
    {
      MoverOrchestratorCD orchestratorData = EntityManager.GetComponentData<MoverOrchestratorCD>(orchestrator);
      orchestratorData.enabledMoverIndex = 0;
      orchestratorData.nextMoverCycleIncrement = laneCount > 1 ? 1 : 0;
      EntityManager.SetComponentData(orchestrator, orchestratorData);
    }

    SetEnabledMoverFromSharedState(originals.LeftMoverEntity, includeLeft);
    SetEnabledMoverFromSharedState(originals.RightMoverEntity, includeRight);

    if (centerMoverEntity != Entity.Null)
    {
      SetEnabledMoverFromSharedState(centerMoverEntity, includeCenter);
    }

    _smartStateDirty.Add(orchestrator);

    LogSmartRouteBuffer(orchestrator, buffer);
  }

  private bool TryPrepareCenterOutputMover(
      Entity orchestrator,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData,
      int2 center,
      int2 forwardDirection,
      out Entity centerMoverEntity)
  {
    centerMoverEntity = Entity.Null;

    if (!TryFindCenterOutputMover(
            originals,
            allMovers,
            allMoverData,
            center,
            forwardDirection,
            out int2 forward,
            out centerMoverEntity))
    {
      LogCenterOutputSearchFailure(orchestrator, originals, allMovers, allMoverData, center, forwardDirection);
      return false;
    }

    if (!EntityManager.Exists(centerMoverEntity) ||
        !EntityManager.HasComponent<MoverCD>(centerMoverEntity))
    {
      return false;
    }

    if (!_patchedCenterOutputOriginalMovers.ContainsKey(centerMoverEntity))
    {
      _patchedCenterOutputOriginalMovers[centerMoverEntity] =
          EntityManager.GetComponentData<MoverCD>(centerMoverEntity);
      _patchedCenterOutputAddedSharedState[centerMoverEntity] =
          !EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(centerMoverEntity);
    }

    if (_patchedCenterOutputAddedSharedState[centerMoverEntity] &&
        !EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(centerMoverEntity))
    {
      EntityManager.AddComponent<EnabledMoverFromSharedStateCD>(centerMoverEntity);
    }

    MoverCD centerMover = EntityManager.GetComponentData<MoverCD>(centerMoverEntity);
    centerMover.start = center;
    centerMover.stop = forward;
    centerMover.moverOrchestratorEntity = orchestrator;
    centerMover.splitsIntoOnMove = 1;
    centerMover.cycleEnabledMoverAfterActivation = true;
    centerMover.enableAllMoversAfterActivation = false;
    centerMover.allowPickupFromInventories = false;
    centerMover.inventoryEntity = Entity.Null;
    EntityManager.SetComponentData(centerMoverEntity, centerMover);

    LogCoreRouting(
        $"center-mover prepared orchestrator={orchestrator} mover={centerMoverEntity} " +
        $"path={FormatInt2(centerMover.start)}->{FormatInt2(centerMover.stop)} " +
        $"forward={FormatInt2(forward)} splits={centerMover.splitsIntoOnMove}");

    return true;
  }

  private void RestoreBothOutputs(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals)
  {
    RestorePatchedCenterOutputMoversForOrchestrator(orchestrator);
    RestoreOriginalOutputMoverPaths(originals);

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

  private void RestorePatchedCenterOutputMoversForOrchestrator(Entity orchestrator)
  {
    if (_patchedCenterOutputOriginalMovers.Count == 0)
    {
      return;
    }

    List<Entity> restored = null;

    foreach (KeyValuePair<Entity, MoverCD> entry in _patchedCenterOutputOriginalMovers)
    {
      Entity moverEntity = entry.Key;

      if (!EntityManager.Exists(moverEntity) ||
          !EntityManager.HasComponent<MoverCD>(moverEntity))
      {
        restored ??= new List<Entity>();
        restored.Add(moverEntity);
        continue;
      }

      MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);

      if (mover.moverOrchestratorEntity != orchestrator)
      {
        continue;
      }

      EntityManager.SetComponentData(moverEntity, entry.Value);

      if (_patchedCenterOutputAddedSharedState.TryGetValue(moverEntity, out bool addedSharedState) &&
          addedSharedState &&
          EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(moverEntity))
      {
        EntityManager.RemoveComponent<EnabledMoverFromSharedStateCD>(moverEntity);
      }

      restored ??= new List<Entity>();
      restored.Add(moverEntity);
    }

    if (restored == null)
    {
      return;
    }

    foreach (Entity moverEntity in restored)
    {
      _patchedCenterOutputOriginalMovers.Remove(moverEntity);
      _patchedCenterOutputAddedSharedState.Remove(moverEntity);
    }
  }

  private void RestoreMoverOrchestratorCycling(Entity orchestrator)
  {
    if (!EntityManager.Exists(orchestrator) ||
        !EntityManager.HasComponent<MoverOrchestratorCD>(orchestrator))
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
      Entity moverEntity = allMovers[i];

      if (!EntityManager.Exists(moverEntity) ||
          !EntityManager.HasComponent<MoverCD>(moverEntity))
      {
        continue;
      }

      // Important:
      // allMoverData is a snapshot captured at the start of OnUpdate. Forward passthrough
      // temporarily mutates a real forward conveyor mover into a splitter-owned mover.
      // During restore, using the stale snapshot here can overwrite the freshly-restored
      // forward conveyor with its old patched state again.
      //
      // Always read the live MoverCD before writing split counts.
      MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);

      if (mover.moverOrchestratorEntity != orchestrator)
      {
        continue;
      }

      if (mover.splitsIntoOnMove == splitCount)
      {
        continue;
      }

      mover.splitsIntoOnMove = splitCount;
      EntityManager.SetComponentData(moverEntity, mover);
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

  private bool TryGetSmartLaneDirections(
      Entity orchestrator,
      SmartSplitterOriginalOutputsCD originals,
      out int2 center,
      out int2 forwardDirection,
      out int2 leftDirection,
      out int2 rightDirection)
  {
    center = default;
    forwardDirection = default;
    leftDirection = default;
    rightDirection = default;

    if (!IsOriginalOutputStateValid(originals))
    {
      return false;
    }

    MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
    MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

    center = new int2(
        Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
        Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

    if (!TryGetSmartInputDirection(orchestrator, center, out int2 inputSideDirection))
    {
      return false;
    }

    forwardDirection = new int2(-inputSideDirection.x, -inputSideDirection.y);
    leftDirection = new int2(-forwardDirection.y, forwardDirection.x);
    rightDirection = new int2(forwardDirection.y, -forwardDirection.x);
    return true;
  }

  private void RetargetMoverPath(Entity moverEntity, int2 start, int2 direction)
  {
    if (!EntityManager.Exists(moverEntity) ||
        !EntityManager.HasComponent<MoverCD>(moverEntity))
    {
      return;
    }

    MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);
    mover.start = start;
    mover.stop = start + direction;
    EntityManager.SetComponentData(moverEntity, mover);
  }

  private void ConfigureRouteMover(
      Entity moverEntity,
      Entity orchestrator,
      int2 start,
      int2 direction,
      int indexInBuffer,
      int splitCount)
  {
    if (!EntityManager.Exists(moverEntity) ||
        !EntityManager.HasComponent<MoverCD>(moverEntity))
    {
      return;
    }

    MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);
    mover.start = start;
    mover.stop = start + direction;
    mover.moverOrchestratorEntity = orchestrator;
    mover.indexInOrchestrator = indexInBuffer;
    mover.splitsIntoOnMove = splitCount;
    EntityManager.SetComponentData(moverEntity, mover);
  }

  private void RestoreOriginalOutputMoverPaths(SmartSplitterOriginalOutputsCD originals)
  {
    RestoreOriginalOutputMoverPath(
        originals.LeftMoverEntity,
        originals.LeftCachedStart,
        originals.LeftCachedDirection,
        originals.LeftMoverIndex);

    RestoreOriginalOutputMoverPath(
        originals.RightMoverEntity,
        originals.RightCachedStart,
        originals.RightCachedDirection,
        originals.RightMoverIndex);
  }

  private void RestoreOriginalOutputMoverPath(Entity moverEntity, int2 start, int2 direction, int index)
  {
    if (!EntityManager.Exists(moverEntity) ||
        !EntityManager.HasComponent<MoverCD>(moverEntity))
    {
      return;
    }

    MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);
    mover.start = start;
    mover.stop = start + direction;
    mover.indexInOrchestrator = index;
    EntityManager.SetComponentData(moverEntity, mover);
  }

  private bool TryGetSmartInputDirection(Entity orchestrator, int2 center, out int2 inputDirection)
  {
    inputDirection = default;

    if (EntityManager.Exists(orchestrator) &&
        EntityManager.HasComponent<ObjectDataCD>(orchestrator))
    {
      ObjectDataCD orchestratorObjectData = EntityManager.GetComponentData<ObjectDataCD>(orchestrator);
      return SmartSplitterOrientationUtility.TryGetInputDirectionForPlacedVariation(
          orchestratorObjectData.variation,
          out inputDirection);
    }

    if (!TryGetPlacedSplitterVariationAtCenter(orchestrator, center, out int placedVariation))
    {
      return false;
    }

    return SmartSplitterOrientationUtility.TryGetInputDirectionForPlacedVariation(
        placedVariation,
        out inputDirection);
  }

  private bool TryGetPlacedSplitterVariationAtCenter(Entity orchestrator, int2 center, out int placedVariation)
  {
    placedVariation = 0;

    using NativeArray<Entity> entities = _objectDataTransformQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ObjectDataCD> objectData = _objectDataTransformQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> transforms = _objectDataTransformQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    int bestDistance = int.MaxValue;
    int bestVariation = 0;
    bool found = false;

    for (int i = 0; i < entities.Length; i++)
    {
      if (objectData[i].objectID != ObjectID.ConveyorBeltSplitter)
      {
        continue;
      }

      int x = Mathf.RoundToInt(transforms[i].Position.x);
      int y = Mathf.RoundToInt(transforms[i].Position.z);
      int distance = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);

      if (distance > 1 || distance >= bestDistance)
      {
        continue;
      }

      bestDistance = distance;
      bestVariation = objectData[i].variation;
      found = true;
    }

    if (!found)
    {
      LogCoreRouting(
          $"placed-variation lookup-failed orchestrator={orchestrator} center={FormatInt2(center)}");
      return false;
    }

    placedVariation = bestVariation;
    return true;
  }

  private static bool TryGetInputCorridorGeometry(
      MoverCD leftMover,
      MoverCD rightMover,
      int2 inputSideDirection,
      out float centerX,
      out float centerY,
      out float outputAxisX,
      out float outputAxisY,
      out float inputAxisX,
      out float inputAxisY)
  {
    centerX = (leftMover.start.x + rightMover.start.x) * 0.5f;
    centerY = (leftMover.start.y + rightMover.start.y) * 0.5f;
    outputAxisX = 0f;
    outputAxisY = 0f;
    inputAxisX = 0f;
    inputAxisY = 0f;

    if (inputSideDirection.x == 0 && inputSideDirection.y == 0)
    {
      return false;
    }

    inputAxisX = inputSideDirection.x;
    inputAxisY = inputSideDirection.y;
    outputAxisX = -inputAxisY;
    outputAxisY = inputAxisX;
    return true;
  }

  private static SmartSplitterArmedRouteCD EmptyArmedRoute()
  {
    return new SmartSplitterArmedRouteCD
    {
      HasArmedRoute = false,
      AppliedOnce = false,
      ArmedEntity = Entity.Null,
      Decision = SmartSplitterDecision.None,
      ItemObject = ObjectID.None,
      ItemVariation = 0,
      ItemAmount = 0,
      RouteStartLaneIndex = 0,
      NextRouteStartLaneIndex = 0,
      ArmedAt = 0d,
      ExpiresAt = 0d,
      RouteAppliedAt = 0d
    };
  }

  private void ClearArmedRoute(Entity orchestrator)
  {
    if (!EntityManager.Exists(orchestrator) ||
        !EntityManager.HasComponent<SmartSplitterArmedRouteCD>(orchestrator))
    {
      return;
    }

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
    float signedInputDistance = Dot(offsetX, offsetY, inputAxisX, inputAxisY);

    // Only accept items on the configured input side of the splitter. Output
    // lanes, including the straight-through center lane, must not re-arm a route.
    inputDistance = signedInputDistance;

    if (outputAxisDistance > InputLaneHalfWidth)
    {
      return false;
    }

    if (signedInputDistance <= 0f)
    {
      return false;
    }

    if (inputDistance < MinInputDistanceFromCenter || inputDistance > InputDetectDistance)
    {
      return false;
    }

    return true;
  }

  private static SmartSplitterLaneFiltersCD CreateDefaultLaneFilters()
  {
    return new SmartSplitterLaneFiltersCD
    {
      Left = CreateAnyLaneFilter(),
      Center = CreateAnyLaneFilter(),
      Right = CreateAnyLaneFilter()
    };
  }

  private static SmartSplitterLaneFilter CreateAnyLaneFilter()
  {
    return new SmartSplitterLaneFilter
    {
      Mode = SmartSplitterLaneFilterMode.Any,
      FilterObject = ObjectID.None,
      FilterVariation = 0
    };
  }

  private static SmartSplitterDecision DecideRoute(
      SmartSplitterLaneFiltersCD filters,
      ObjectID itemObject,
      int itemVariation)
  {
    SmartSplitterDecision exactMatches = SmartSplitterDecision.None;

    if (LaneAllowsExactItem(filters.Left, itemObject, itemVariation))
    {
      exactMatches |= SmartSplitterDecision.LeftOnly;
    }

    if (LaneAllowsExactItem(filters.Center, itemObject, itemVariation))
    {
      exactMatches |= SmartSplitterDecision.CenterOnly;
    }

    if (LaneAllowsExactItem(filters.Right, itemObject, itemVariation))
    {
      exactMatches |= SmartSplitterDecision.RightOnly;
    }

    if (exactMatches != SmartSplitterDecision.None)
    {
      return exactMatches;
    }

    SmartSplitterDecision anyMatches = SmartSplitterDecision.None;

    if (filters.Left.Mode == SmartSplitterLaneFilterMode.Any)
    {
      anyMatches |= SmartSplitterDecision.LeftOnly;
    }

    if (filters.Center.Mode == SmartSplitterLaneFilterMode.Any)
    {
      anyMatches |= SmartSplitterDecision.CenterOnly;
    }

    if (filters.Right.Mode == SmartSplitterLaneFilterMode.Any)
    {
      anyMatches |= SmartSplitterDecision.RightOnly;
    }

    return anyMatches == SmartSplitterDecision.None
        ? SmartSplitterDecision.Blocked
        : anyMatches;
  }

  private static bool LaneAllowsExactItem(
      SmartSplitterLaneFilter filter,
      ObjectID itemObject,
      int itemVariation)
  {
    // UI-picked filters are item filters, not hidden item-variation filters. Matching
    // by ObjectID keeps icons, placed items, and carried stacks aligned.
    return filter.Mode == SmartSplitterLaneFilterMode.Item &&
           itemObject == filter.FilterObject;
  }

  private SmartSplitterDecision SelectLanesForAmount(
    Entity orchestrator,
    SmartSplitterDecision decision,
    int itemAmount,
    out int routeStartLaneIndex,
    out int nextRouteStartLaneIndex)
  {
    int laneCount = CountDecisionLanes(decision);
    int startIndex = _laneRoundRobinIndex.TryGetValue(orchestrator, out int storedIndex)
        ? storedIndex
        : 0;

    routeStartLaneIndex = startIndex;
    nextRouteStartLaneIndex = startIndex;

    if (laneCount <= 1 || decision == SmartSplitterDecision.Blocked)
    {
      nextRouteStartLaneIndex = FindNextRoundRobinIndex(decision, startIndex);
      return decision;
    }

    int requestedLanes = itemAmount <= 0
        ? 1
        : math.min(itemAmount, laneCount);

    SmartSplitterDecision selected = SmartSplitterDecision.None;
    int selectedCount = 0;
    int nextStartIndex = startIndex;

    for (int offset = 0; offset < 6 && selectedCount < requestedLanes; offset++)
    {
      int laneIndex = (startIndex + offset) % 3;
      SmartSplitterLane lane = (SmartSplitterLane)laneIndex;

      if (!DecisionIncludesLane(decision, lane))
      {
        continue;
      }

      selected |= DecisionForLane(lane);
      selectedCount++;
      nextStartIndex = (laneIndex + 1) % 3;
    }

    nextRouteStartLaneIndex = nextStartIndex;

    return selected == SmartSplitterDecision.None
        ? SmartSplitterDecision.Blocked
        : selected;
  }

  private static int FindNextRoundRobinIndex(SmartSplitterDecision decision, int startIndex)
  {
    if (decision == SmartSplitterDecision.Blocked || decision == SmartSplitterDecision.None)
    {
      return startIndex;
    }

    for (int offset = 0; offset < 3; offset++)
    {
      int laneIndex = (startIndex + offset) % 3;

      if (DecisionIncludesLane(decision, (SmartSplitterLane)laneIndex))
      {
        return (laneIndex + 1) % 3;
      }
    }

    return startIndex;
  }

  private static int CountDecisionLanes(SmartSplitterDecision decision)
  {
    int count = 0;

    if (DecisionIncludesLane(decision, SmartSplitterLane.Left))
    {
      count++;
    }

    if (DecisionIncludesLane(decision, SmartSplitterLane.Center))
    {
      count++;
    }

    if (DecisionIncludesLane(decision, SmartSplitterLane.Right))
    {
      count++;
    }

    return count;
  }

  private static bool DecisionIncludesLane(SmartSplitterDecision decision, SmartSplitterLane lane)
  {
    return (decision & DecisionForLane(lane)) != 0;
  }

  private static SmartSplitterDecision DecisionForLane(SmartSplitterLane lane)
  {
    switch (lane)
    {
      case SmartSplitterLane.Left:
        return SmartSplitterDecision.LeftOnly;

      case SmartSplitterLane.Center:
        return SmartSplitterDecision.CenterOnly;

      case SmartSplitterLane.Right:
        return SmartSplitterDecision.RightOnly;

      default:
        return SmartSplitterDecision.None;
    }
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

  private void LogCoreRouting(string message)
  {
    if (!EnableCoreRoutingDiagnostics ||
        _coreRoutingDiagnosticLogs >= MaxCoreRoutingDiagnosticLogs)
    {
      return;
    }

    _coreRoutingDiagnosticLogs++;
    Debug.Log("[SmartSplitterCore] " + message);
  }

  private void LogCoreRoutingThrottled(
      CachedSplitter splitter,
      string key,
      string message,
      double now,
      double intervalSeconds)
  {
    if (!EnableCoreRoutingDiagnostics ||
        _coreRoutingDiagnosticLogs >= MaxCoreRoutingDiagnosticLogs)
    {
      return;
    }

    if (now < splitter.NextCoreLogTime && splitter.LastCoreLogKey == key)
    {
      return;
    }

    splitter.LastCoreLogKey = key;
    splitter.NextCoreLogTime = now + intervalSeconds;
    LogCoreRouting(message);
  }

  private void LogSmartRouteBuffer(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer)
  {
    if (!EnableCoreRoutingDiagnostics)
    {
      return;
    }

    LogCoreRouting($"buffer orchestrator={orchestrator} length={buffer.Length}");

    for (int i = 0; i < buffer.Length; i++)
    {
      Entity moverEntity = buffer[i].moverEntity;

      if (!EntityManager.Exists(moverEntity) ||
          !EntityManager.HasComponent<MoverCD>(moverEntity))
      {
        LogCoreRouting($"buffer-entry index={i} mover={moverEntity} missing");
        continue;
      }

      MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);
      LogCoreRouting(
          $"buffer-entry index={i} mover={moverEntity} cachedStart={FormatInt2(buffer[i].cachedStart)} " +
          $"cachedDir={FormatInt2(buffer[i].cachedDirection)} path={FormatInt2(mover.start)}->{FormatInt2(mover.stop)} " +
          $"orchestrator={mover.moverOrchestratorEntity} moverIndex={mover.indexInOrchestrator} splits={mover.splitsIntoOnMove}");
    }
  }

  private void LogCenterOutputSearchFailure(
      Entity orchestrator,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData,
      int2 center,
      int2 forwardDirection)
  {
    if (!EnableCoreRoutingDiagnostics)
    {
      return;
    }

    LogCoreRouting(
        $"center-mover search-failed orchestrator={orchestrator} center={FormatInt2(center)} " +
        $"forwardDir={FormatInt2(forwardDirection)} forwardTile={FormatInt2(center + forwardDirection)}");

    for (int i = 0; i < allMovers.Length; i++)
    {
      Entity moverEntity = allMovers[i];

      if (moverEntity == originals.LeftMoverEntity ||
          moverEntity == originals.RightMoverEntity)
      {
        continue;
      }

      MoverCD mover = allMoverData[i];
      int manhattanFromCenter =
          Mathf.Abs(mover.start.x - center.x) +
          Mathf.Abs(mover.start.y - center.y);
      int manhattanFromForward =
          Mathf.Abs(mover.start.x - (center.x + forwardDirection.x)) +
          Mathf.Abs(mover.start.y - (center.y + forwardDirection.y));

      if (manhattanFromCenter > 3 && manhattanFromForward > 3)
      {
        continue;
      }

      LogCoreRouting(
          $"center-mover candidate mover={moverEntity} path={FormatInt2(mover.start)}->{FormatInt2(mover.stop)} " +
          $"dir={FormatInt2(new int2(mover.stop.x - mover.start.x, mover.stop.y - mover.start.y))} " +
          $"orchestrator={mover.moverOrchestratorEntity} index={mover.indexInOrchestrator} splits={mover.splitsIntoOnMove}");
    }
  }

  private bool TryGetPhysicalForwardTopology(
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData,
      out int2 center,
      out int2 back,
      out int2 forward,
      out int2 forwardDirection,
      out Entity inputMover,
      out Entity forwardMover)
  {
    center = default;
    back = default;
    forward = default;
    forwardDirection = default;
    inputMover = Entity.Null;
    forwardMover = Entity.Null;

    if (!IsOriginalOutputStateValid(originals))
    {
      return false;
    }

    MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
    MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

    center = new int2(
        Mathf.RoundToInt((leftMover.start.x + rightMover.start.x) * 0.5f),
        Mathf.RoundToInt((leftMover.start.y + rightMover.start.y) * 0.5f));

    int inputCount = 0;
    int2 foundInputDirection = default;

    for (int i = 0; i < allMovers.Length; i++)
    {
      Entity moverEntity = allMovers[i];
      MoverCD mover = allMoverData[i];

      if (moverEntity == originals.LeftMoverEntity ||
          moverEntity == originals.RightMoverEntity)
      {
        continue;
      }

      if (mover.stop.x == center.x && mover.stop.y == center.y)
      {
        inputCount++;
        inputMover = moverEntity;
        back = mover.start;
        foundInputDirection = new int2(center.x - mover.start.x, center.y - mover.start.y);
      }
    }

    if (inputCount != 1 ||
        (foundInputDirection.x == 0 && foundInputDirection.y == 0))
    {
      return false;
    }

    forwardDirection = foundInputDirection;
    forward = center + forwardDirection;

    for (int i = 0; i < allMovers.Length; i++)
    {
      Entity moverEntity = allMovers[i];
      MoverCD mover = allMoverData[i];

      if (mover.start.x != forward.x || mover.start.y != forward.y)
      {
        continue;
      }

      int2 direction = new int2(mover.stop.x - mover.start.x, mover.stop.y - mover.start.y);

      if (direction.x == forwardDirection.x && direction.y == forwardDirection.y)
      {
        forwardMover = moverEntity;
        return true;
      }
    }

    return false;
  }

  private static string FormatInt2(int2 value)
  {
    return $"({value.x},{value.y})";
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
