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
    public bool HasCenter;
    public int2 Center;
    public string LastLogKey = string.Empty;
    public double NextCoreLogTime;
    public string LastCoreLogKey = string.Empty;
  }

  private sealed class DroppedItemSnapshot
  {
    public Entity Entity;
    public ObjectID ItemObject;
    public int ItemVariation;
    public int ItemAmount;
    public float3 Position;
  }

  private struct DirectRouteGuard
  {
    public Entity Orchestrator;
    public double Until;
  }

  private struct SmartRoutePiece
  {
    public SmartSplitterLane Lane;
    public int Amount;
  }

  private enum SmartRouteStage
  {
    ToCenter,
    ToExit,
    Reject
  }

  private struct SmartRoutePlan
  {
    public Entity Orchestrator;
    public SmartRouteStage Stage;
    public SmartSplitterLane Lane;
    public float2 CenterTarget;
    public float2 ExitTarget;
    public int BaseMoveTime;
    public double ExpiresAt;
  }

  private readonly Dictionary<Entity, CachedSplitter> _splitters = new();
  private readonly Dictionary<Entity, int> _laneRoundRobinIndex = new();
  private readonly Dictionary<Entity, double> _recentlyReleasedRoutedEntities = new();
  private readonly Dictionary<Entity, DirectRouteGuard> _directRouteGuards = new();
  private readonly Dictionary<Entity, SmartRoutePlan> _smartRoutePlans = new();
  private readonly HashSet<Entity> _handledDroppedEntitiesThisTick = new();
  private readonly Dictionary<Entity, Entity> _moveeByDroppedEntity = new();
  private readonly List<DroppedItemSnapshot> _droppedItemSnapshots = new();
  private readonly Dictionary<long, List<int>> _droppedItemIndexesByTile = new();
  private readonly HashSet<int> _candidateDroppedItemIndexes = new();
  private readonly List<SmartSplitterLane> _routeLaneScratch = new(3);
  private readonly List<SmartRoutePiece> _routePieceScratch = new(3);
  private readonly Dictionary<Entity, bool> _splitterPowerState = new();
  private readonly HashSet<Entity> _smartStateDirty = new();
  private readonly HashSet<long> _poweredElectricityTiles = new();
  private readonly Dictionary<long, int> _placedSplitterVariationByTile = new();
  private bool _loggedFirstRegisteredSplitter;
  private double _moveeLookupBuiltAt = -1d;
  private double _placedSplitterVariationIndexBuiltAt = -1d;

  private static bool EnableRouting = true;
  private static bool EnableElectricityGateLogs = false;
  private static bool EnableCoreRoutingDiagnostics = false;
  private static bool EnableRouteHandoffDiagnostics = false;

  private int _coreRoutingDiagnosticLogs;
  private const int MaxCoreRoutingDiagnosticLogs = 300;
  private int _routeHandoffDiagnosticLogs;
  private const int MaxRouteHandoffDiagnosticLogs = 260;

  private const float InputDetectDistance = 4.20f;
  private const float InputLaneHalfWidth = 0.35f;
  private const float MinInputDistanceFromCenter = 0.15f;
  private const float AcceptedItemRedirectDistance = 0.70f;

  private const double RecentlyReleasedRoutedEntityIgnoreSeconds = 0.35d;
  private const double DirectRouteGuardSeconds = 1.20d;
  private const double SmartRoutePlanLifetimeSeconds = 2.50d;
  // Core Keeper's Movee system stops items once they are within roughly
  // sqrt(0.1) tiles of the target, so our center handoff must accept that
  // same practical arrival band or items can park near the splitter center.
  private const float SmartRouteCenterArrivalRadius = 0.34f;
  private const float SmartRouteExitArrivalRadius = 0.28f;
  private const int VanillaSplitterOutputSplitCount = 2;

  private EntityQuery _allOrchestratorsQuery;
  private EntityQuery _taggedSplitterQuery;
  private EntityQuery _droppedItemQuery;
  private EntityQuery _moverQuery;
  private EntityQuery _moveeQuery;
  private EntityQuery _objectDataTransformQuery;
  private EntityQuery _electricityQuery;
  private EntityQuery _databaseQuery;

  protected override void OnCreate()
  {
    Debug.Log($"[SmartSplitterRuntimeSystem] Created in world={World?.Name ?? "unknown"}");

    _allOrchestratorsQuery = GetEntityQuery(ComponentType.ReadOnly<MoversWithSharedStateBuffer>());

    _taggedSplitterQuery = GetEntityQuery(
        ComponentType.ReadOnly<SmartSplitterTag>(),
        ComponentType.ReadOnly<SmartSplitterOriginalOutputsCD>());

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

    _moveeQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<MoveeCD>(),
        ComponentType.ReadOnly<BigEntityRefCD>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _databaseQuery = GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());

    RequireForUpdate(_allOrchestratorsQuery);
    RequireForUpdate(_databaseQuery);
  }

  protected override void OnUpdate()
  {
    double now = World.Time.ElapsedTime;

    ComponentLookup<MoverCD> moverLookup = GetComponentLookup<MoverCD>(true);

    PruneDestroyedCachedSplitters();
    RefreshSmartSplitterConfig(moverLookup);
    RegisterSplitters();
    SmartSplitterPersistence.FlushIfDue();

    if (_splitters.Count == 0)
    {
      return;
    }

    using NativeArray<Entity> allMovers = _moverQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<MoverCD> allMoverData = _moverQuery.ToComponentDataArray<MoverCD>(Allocator.Temp);

    using NativeArray<ElectricityCD> electricityData = _electricityQuery.ToComponentDataArray<ElectricityCD>(Allocator.Temp);
    using NativeArray<LocalTransform> electricityTransforms = _electricityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    BuildPoweredElectricityTileSet(electricityData, electricityTransforms, _poweredElectricityTiles);

    PruneRecentlyReleasedRoutedEntities(now);
    PruneDirectRouteGuards(now);
    PruneSmartRoutePlans(now);
    _handledDroppedEntitiesThisTick.Clear();

    ApplyElectricityGateToSplitters(
        _poweredElectricityTiles,
        allMovers,
        allMoverData);

    using NativeArray<Entity> droppedEntities = _droppedItemQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ObjectDataCD> droppedOuterObjects = _droppedItemQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> droppedTransforms = _droppedItemQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
    PugDatabase.DatabaseBankCD databaseBank = _databaseQuery.GetSingleton<PugDatabase.DatabaseBankCD>();

    RebuildDroppedItemIndex(droppedEntities, droppedOuterObjects, droppedTransforms);

    RouteSmartSplitterItemsDirectly(
        now,
        databaseBank,
        allMovers,
        allMoverData);

    SmartSplitterPersistence.FlushIfDue();
  }

  private void RefreshSmartSplitterConfig(ComponentLookup<MoverCD> moverLookup)
  {
    using NativeArray<Entity> orchestrators = _allOrchestratorsQuery.ToEntityArray(Allocator.Temp);

    foreach (Entity orchestrator in orchestrators)
    {
      if (!EntityManager.Exists(orchestrator) ||
          EntityManager.HasComponent<EntityDestroyedCD>(orchestrator) ||
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
          !moverLookup.HasComponent(output1.moverEntity) ||
          EntityManager.HasComponent<EntityDestroyedCD>(output0.moverEntity) ||
          EntityManager.HasComponent<EntityDestroyedCD>(output1.moverEntity))
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

      if (!EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        CacheOriginalOutputs(orchestrator, output0, mover0, output1, mover1);
      }

      if (!EntityManager.HasComponent<SmartSplitterLaneFiltersCD>(orchestrator))
      {
        SmartSplitterLaneFiltersCD filters = SmartSplitterLaneFilterUtility.CreateDefaultLaneFilters();
        int2 center = GetSplitterCenter(mover0, mover1);
        if (SmartSplitterPersistence.TryGetFilters(center, out SmartSplitterLaneFiltersCD savedFilters))
        {
          filters = savedFilters;
        }

        EntityManager.AddComponentData(orchestrator, filters);
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

  private static int2 GetSplitterCenter(MoverCD mover0, MoverCD mover1)
  {
    return new int2(
        Mathf.RoundToInt((mover0.start.x + mover1.start.x) * 0.5f),
        Mathf.RoundToInt((mover0.start.y + mover1.start.y) * 0.5f));
  }
  private void DisableSharedMoverTriggers(Entity orchestrator)
  {
    DisableSharedMoverTrigger<CycleEnabledMoversTriggerCD>(orchestrator);
    DisableSharedMoverTrigger<EnableSharedMoversTriggerCD>(orchestrator);
    DisableSharedMoverTrigger<DeactivateSharedMoversTriggerCD>(orchestrator);
  }

  private void DisableSharedMoverTrigger<T>(Entity orchestrator)
      where T : unmanaged, IComponentData, IEnableableComponent
  {
    if (EntityManager.Exists(orchestrator) &&
        EntityManager.HasComponent<T>(orchestrator))
    {
      EntityManager.SetComponentEnabled<T>(orchestrator, false);
    }
  }
  private void RegisterSplitters()
  {
    using NativeArray<Entity> entities = _taggedSplitterQuery.ToEntityArray(Allocator.Temp);

    foreach (Entity orchestrator in entities)
    {
      if (IsCachedSplitterDestroyed(orchestrator))
      {
        continue;
      }

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

      if (!SmartSplitterLaneFilterUtility.TryGetSplitterCenter(EntityManager, originals, out int2 center))
      {
        continue;
      }

      _splitters[orchestrator] = new CachedSplitter
      {
        Orchestrator = orchestrator,
        HasCenter = true,
        Center = center
      };

      if (!_loggedFirstRegisteredSplitter)
      {
        _loggedFirstRegisteredSplitter = true;
        Debug.Log($"[SmartSplitterRuntimeSystem] Registered first splitter orchestrator={orchestrator}");
      }
    }
  }

  private void PruneDestroyedCachedSplitters()
  {
    List<Entity> stale = null;

    foreach (CachedSplitter splitter in _splitters.Values)
    {
      if (!IsCachedSplitterDestroyed(splitter.Orchestrator))
      {
        continue;
      }

      stale ??= new List<Entity>();
      stale.Add(splitter.Orchestrator);
    }

    if (stale == null)
    {
      return;
    }

    foreach (Entity entity in stale)
    {
      RemoveCachedSplitter(entity, true);
    }
  }

  private bool IsCachedSplitterDestroyed(Entity entity)
  {
    return entity == Entity.Null ||
           !EntityManager.Exists(entity) ||
           EntityManager.HasComponent<EntityDestroyedCD>(entity);
  }

  private void RemoveCachedSplitter(Entity entity, bool deletePersistedFilters)
  {
    if (_splitters.TryGetValue(entity, out CachedSplitter splitter) &&
        deletePersistedFilters &&
        splitter.HasCenter)
    {
      SmartSplitterPersistence.DeleteFilters(splitter.Center);
    }

    _splitters.Remove(entity);
    _splitterPowerState.Remove(entity);
    _smartStateDirty.Remove(entity);
    _laneRoundRobinIndex.Remove(entity);
    RemoveSmartRoutePlansForOrchestrator(entity);
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
        if ((!hadState || !wasPowered) &&
            !HasSmartRuntimeState(orchestrator))
        {
          BlockSmartSplitterState(orchestrator, allMovers, allMoverData);
        }

        continue;
      }

      if ((hadState && wasPowered) ||
          HasSmartRuntimeState(orchestrator))
      {
        RestoreSmartSplitterToVanillaState(orchestrator);
      }
    }

    if (stale == null)
    {
      return;
    }

    foreach (Entity entity in stale)
    {
      RemoveCachedSplitter(entity, IsCachedSplitterDestroyed(entity));
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

  private void RebuildDroppedItemIndex(
      NativeArray<Entity> droppedEntities,
      NativeArray<ObjectDataCD> droppedOuterObjects,
      NativeArray<LocalTransform> droppedTransforms)
  {
    _droppedItemSnapshots.Clear();
    _droppedItemIndexesByTile.Clear();

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

      ObjectDataCD inner = contained[0].objectData;
      if (inner.amount <= 0)
      {
        continue;
      }

      DroppedItemSnapshot snapshot = new DroppedItemSnapshot
      {
        Entity = droppedEntity,
        ItemObject = inner.objectID,
        ItemVariation = inner.variation,
        ItemAmount = inner.amount,
        Position = droppedTransforms[i].Position
      };

      int snapshotIndex = _droppedItemSnapshots.Count;
      _droppedItemSnapshots.Add(snapshot);

      int tileX = Mathf.RoundToInt(snapshot.Position.x);
      int tileY = Mathf.RoundToInt(snapshot.Position.z);
      long key = GetTileKey(tileX, tileY);
      if (!_droppedItemIndexesByTile.TryGetValue(key, out List<int> indexes))
      {
        indexes = new List<int>(4);
        _droppedItemIndexesByTile.Add(key, indexes);
      }

      indexes.Add(snapshotIndex);
    }
  }

  private static long GetTileKey(int x, int y)
  {
    return ((long)x << 32) ^ (uint)y;
  }
  private void RouteSmartSplitterItemsDirectly(
      double now,
      PugDatabase.DatabaseBankCD databaseBank,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    if (!EnableRouting)
    {
      return;
    }

    List<Entity> stale = null;

    foreach (CachedSplitter splitter in _splitters.Values)
    {
      Entity orchestrator = splitter.Orchestrator;

      if (!EntityManager.Exists(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterConfigCD>(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterLaneFiltersCD>(orchestrator) ||
          !EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(orchestrator))
      {
        stale ??= new List<Entity>();
        stale.Add(orchestrator);
        continue;
      }

      SmartSplitterConfigCD config =
          EntityManager.GetComponentData<SmartSplitterConfigCD>(orchestrator);

      if (!config.Enabled || !IsSplitterSmartPowered(orchestrator))
      {
        continue;
      }

      SmartSplitterOriginalOutputsCD originals =
          EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(orchestrator);

      if (!IsOriginalOutputStateValid(originals))
      {
        stale ??= new List<Entity>();
        stale.Add(orchestrator);
        continue;
      }

      EnsurePoweredSmartSplitterOwnsRouting(orchestrator, originals, allMovers, allMoverData);

      if (!TryGetSmartLaneDirections(
              orchestrator,
              originals,
              out int2 center,
              out int2 forwardDirection,
              out int2 leftDirection,
              out int2 rightDirection))
      {
        continue;
      }

      MoverCD leftMover = EntityManager.GetComponentData<MoverCD>(originals.LeftMoverEntity);
      MoverCD rightMover = EntityManager.GetComponentData<MoverCD>(originals.RightMoverEntity);

      if (!TryGetSmartInputDirection(orchestrator, center, out int2 inputDirection) ||
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
        continue;
      }

      SmartSplitterLaneFiltersCD filters =
          EntityManager.GetComponentData<SmartSplitterLaneFiltersCD>(orchestrator);

      ProgressSmartRoutePlans(orchestrator, now);
      CollectInputCorridorCandidateItems(center, inputDirection);

      foreach (int snapshotIndex in _candidateDroppedItemIndexes)
      {
        if (snapshotIndex < 0 || snapshotIndex >= _droppedItemSnapshots.Count)
        {
          continue;
        }

        DroppedItemSnapshot snapshot = _droppedItemSnapshots[snapshotIndex];
        Entity droppedEntity = snapshot.Entity;

        if (_handledDroppedEntitiesThisTick.Contains(droppedEntity) ||
            _smartRoutePlans.ContainsKey(droppedEntity) ||
            ShouldSkipDirectRouteForOwner(droppedEntity, orchestrator, now))
        {
          continue;
        }

        if (!IsInsideDirectRoutingWindow(
                snapshot.Position.x,
                snapshot.Position.z,
                centerX,
                centerY,
                outputAxisX,
                outputAxisY,
                inputAxisX,
                inputAxisY,
                out float inputDistance) ||
            inputDistance > AcceptedItemRedirectDistance)
        {
          continue;
        }

        SmartSplitterDecision decision =
            DecideRoute(filters, snapshot.ItemObject, snapshot.ItemVariation);

        if (TryRouteIncomingDroppedItem(
                splitter,
                droppedEntity,
                snapshot.ItemObject,
                snapshot.ItemVariation,
                snapshot.ItemAmount,
                snapshot.Position,
                decision,
                center,
                inputDirection,
                forwardDirection,
                leftDirection,
                rightDirection,
                leftMover.moveTime,
                databaseBank,
                now))
        {
          _handledDroppedEntitiesThisTick.Add(droppedEntity);
        }
      }
    }

    if (stale == null)
    {
      return;
    }

    foreach (Entity entity in stale)
    {
      RemoveCachedSplitter(entity, IsCachedSplitterDestroyed(entity));
    }
  }

  private void EnsurePoweredSmartSplitterOwnsRouting(
      Entity orchestrator,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    bool needsBlock = !_smartStateDirty.Contains(orchestrator);

    if (!needsBlock && EntityManager.HasBuffer<MoversWithSharedStateBuffer>(orchestrator))
    {
      needsBlock = EntityManager.GetBuffer<MoversWithSharedStateBuffer>(orchestrator).Length > 0;
    }

    if (!needsBlock)
    {
      needsBlock = IsSplitterMoverStillVanillaActive(originals.LeftMoverEntity) ||
                   IsSplitterMoverStillVanillaActive(originals.RightMoverEntity);
    }

    if (needsBlock)
    {
      BlockSmartSplitterState(orchestrator, allMovers, allMoverData);
    }
  }

  private bool IsSplitterMoverStillVanillaActive(Entity moverEntity)
  {
    if (!EntityManager.Exists(moverEntity) ||
        !EntityManager.HasComponent<MoverCD>(moverEntity))
    {
      return false;
    }

    MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);
    if (mover.splitsIntoOnMove != 0)
    {
      return true;
    }

    return EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(moverEntity) &&
           EntityManager.IsComponentEnabled<EnabledMoverFromSharedStateCD>(moverEntity);
  }

  private bool TryRouteIncomingDroppedItem(
      CachedSplitter splitter,
      Entity droppedEntity,
      ObjectID itemObject,
      int itemVariation,
      int itemAmount,
      float3 itemPosition,
      SmartSplitterDecision decision,
      int2 center,
      int2 inputDirection,
      int2 forwardDirection,
      int2 leftDirection,
      int2 rightDirection,
      int baseMoveTime,
      PugDatabase.DatabaseBankCD databaseBank,
      double now)
  {
    if (itemObject == ObjectID.None ||
        itemAmount <= 0 ||
        !EntityManager.Exists(droppedEntity) ||
        !EntityManager.HasBuffer<ContainedObjectsBuffer>(droppedEntity))
    {
      return false;
    }

    if (decision == SmartSplitterDecision.Blocked)
    {
      if (!TryGetRejectDirection(inputDirection, out int2 rejectDirection))
      {
        return false;
      }

      float2 rejectTarget = new float2(
          center.x + rejectDirection.x,
          center.y + rejectDirection.y);
      int moveTime = CalculateDirectRouteMoveTime(
          baseMoveTime,
          new float2(itemPosition.x, itemPosition.z),
          rejectTarget);

      bool routed = TrySetDroppedEntityRoute(
          splitter.Orchestrator,
          droppedEntity,
          rejectTarget,
          moveTime,
          itemPosition,
          now,
          "blocked");

      if (routed)
      {
        _smartRoutePlans[droppedEntity] = new SmartRoutePlan
        {
          Orchestrator = splitter.Orchestrator,
          Stage = SmartRouteStage.Reject,
          Lane = SmartSplitterLane.Center,
          CenterTarget = rejectTarget,
          ExitTarget = rejectTarget,
          BaseMoveTime = baseMoveTime,
          ExpiresAt = now + SmartRoutePlanLifetimeSeconds
        };
      }

      return routed;
    }

    if (!BuildRoutePieces(
            splitter.Orchestrator,
            decision,
            itemAmount,
            itemObject,
            itemVariation,
            databaseBank,
            out int nextRouteStartIndex))
    {
      return false;
    }

    if (ShouldLogRouteProbe())
    {
      LogRouteProbe(
          $"route-start orchestrator={splitter.Orchestrator} dropped={droppedEntity} " +
          $"item={itemObject}/{itemVariation} amount={itemAmount} decision={decision} " +
          $"pos=({itemPosition.x:0.000},{itemPosition.z:0.000}) center={FormatInt2(center)} " +
          $"pieces={_routePieceScratch.Count} movee={DescribeAssociatedMovee(droppedEntity, now)}");
    }

    DynamicBuffer<ContainedObjectsBuffer> contained =
        EntityManager.GetBuffer<ContainedObjectsBuffer>(droppedEntity);

    if (contained.Length == 0)
    {
      return false;
    }

    ContainedObjectsBuffer originalContained = contained[0];
    int originalAmount = originalContained.objectData.amount;
    if (originalAmount != itemAmount || originalAmount <= 0)
    {
      return false;
    }

    int successfullyAssignedAmount = 0;

    for (int i = 0; i < _routePieceScratch.Count; i++)
    {
      SmartRoutePiece piece = _routePieceScratch[i];
      if (!TryGetLaneTarget(
              piece.Lane,
              center,
              forwardDirection,
              leftDirection,
              rightDirection,
              out float2 target))
      {
        continue;
      }

      float2 centerTarget = new float2(center.x, center.y);
      float2 currentPosition = new float2(itemPosition.x, itemPosition.z);
      bool itemIsAtCenter =
          math.distance(currentPosition, centerTarget) <= SmartRouteCenterArrivalRadius;
      float2 routeTarget = itemIsAtCenter ? target : centerTarget;
      SmartRouteStage routeStage = itemIsAtCenter
          ? SmartRouteStage.ToExit
          : SmartRouteStage.ToCenter;
      int moveTime = CalculateDirectRouteMoveTime(
          baseMoveTime,
          currentPosition,
          routeTarget);

      if (i == 0)
      {
        originalContained.objectData.amount = piece.Amount;
        contained[0] = originalContained;

        bool assigned = TrySetDroppedEntityRoute(
            splitter.Orchestrator,
            droppedEntity,
            routeTarget,
            moveTime,
            itemPosition,
            now,
            piece.Lane.ToString());

        if (ShouldLogRouteProbe())
        {
          LogRouteProbe(
              $"assign-original result={assigned} orchestrator={splitter.Orchestrator} " +
              $"dropped={droppedEntity} lane={piece.Lane} amount={piece.Amount} stage={routeStage} " +
              $"target=({routeTarget.x:0.000},{routeTarget.y:0.000}) exit=({target.x:0.000},{target.y:0.000}) " +
              $"moveTime={moveTime} itemAtCenter={itemIsAtCenter} " +
              $"movee={DescribeAssociatedMovee(droppedEntity, now)}");
        }

        if (!assigned)
        {
          originalContained.objectData.amount = originalAmount;
          contained[0] = originalContained;
          return false;
        }

        _smartRoutePlans[droppedEntity] = new SmartRoutePlan
        {
          Orchestrator = splitter.Orchestrator,
          Stage = routeStage,
          Lane = piece.Lane,
          CenterTarget = centerTarget,
          ExitTarget = target,
          BaseMoveTime = baseMoveTime,
          ExpiresAt = now + SmartRoutePlanLifetimeSeconds
        };

        successfullyAssignedAmount += piece.Amount;
        continue;
      }

      ContainedObjectsBuffer splitContained = originalContained;
      splitContained.objectData.amount = piece.Amount;

      Entity splitEntity = EntityUtility.DropNewEntity(
          World,
          splitContained,
          itemPosition,
          databaseBank.databaseBankBlob,
          Entity.Null);

      if (splitEntity == Entity.Null || !EntityManager.Exists(splitEntity))
      {
        continue;
      }

      SetDroppedBigEntityRoute(splitEntity, routeTarget, moveTime);
      GuardDirectRoute(splitEntity, splitter.Orchestrator, now);
      if (ShouldLogRouteProbe())
      {
        LogRouteProbe(
            $"assign-split orchestrator={splitter.Orchestrator} source={droppedEntity} split={splitEntity} " +
            $"lane={piece.Lane} amount={piece.Amount} stage={routeStage} " +
            $"target=({routeTarget.x:0.000},{routeTarget.y:0.000}) exit=({target.x:0.000},{target.y:0.000}) " +
            $"moveTime={moveTime} movee={DescribeAssociatedMovee(splitEntity, now)}");
      }
      _smartRoutePlans[splitEntity] = new SmartRoutePlan
      {
        Orchestrator = splitter.Orchestrator,
        Stage = routeStage,
        Lane = piece.Lane,
        CenterTarget = centerTarget,
        ExitTarget = target,
        BaseMoveTime = baseMoveTime,
        ExpiresAt = now + SmartRoutePlanLifetimeSeconds
      };

      successfullyAssignedAmount += piece.Amount;
    }

    if (successfullyAssignedAmount <= 0)
    {
      originalContained.objectData.amount = originalAmount;
      contained[0] = originalContained;
      return false;
    }

    if (successfullyAssignedAmount < originalAmount)
    {
      originalContained.objectData.amount =
          contained[0].objectData.amount + (originalAmount - successfullyAssignedAmount);
      contained[0] = originalContained;
    }

    _laneRoundRobinIndex[splitter.Orchestrator] = nextRouteStartIndex;

    LogCoreRoutingThrottled(
        splitter,
        $"direct-smart|{droppedEntity}|{itemObject}|{itemVariation}|{itemAmount}|{decision}",
        $"direct-smart orchestrator={splitter.Orchestrator} dropped={droppedEntity} " +
        $"item={itemObject}/{itemVariation} amount={itemAmount} decision={decision} " +
        $"pieces={_routePieceScratch.Count} nextStart={nextRouteStartIndex}",
        now,
        0.25d);

    return true;
  }

  private void ProgressSmartRoutePlans(Entity orchestrator, double now)
  {
    if (_smartRoutePlans.Count == 0)
    {
      return;
    }

    List<Entity> stale = null;
    List<KeyValuePair<Entity, SmartRoutePlan>> updates = null;

    foreach (KeyValuePair<Entity, SmartRoutePlan> entry in _smartRoutePlans)
    {
      Entity droppedEntity = entry.Key;
      SmartRoutePlan plan = entry.Value;

      if (plan.Orchestrator != orchestrator)
      {
        continue;
      }

      if (now > plan.ExpiresAt ||
          !EntityManager.Exists(droppedEntity) ||
          !EntityManager.HasComponent<LocalTransform>(droppedEntity))
      {
        stale ??= new List<Entity>();
        stale.Add(droppedEntity);
        continue;
      }

      float3 position = EntityManager.GetComponentData<LocalTransform>(droppedEntity).Position;
      float2 current = new float2(position.x, position.z);

      if (plan.Stage == SmartRouteStage.Reject)
      {
        if (math.distance(current, plan.ExitTarget) <= SmartRouteExitArrivalRadius)
        {
          stale ??= new List<Entity>();
          stale.Add(droppedEntity);
        }

        continue;
      }

      if (plan.Stage == SmartRouteStage.ToCenter)
      {
        float centerDistance = math.distance(current, plan.CenterTarget);
        if (centerDistance > SmartRouteCenterArrivalRadius)
        {
          continue;
        }

        int moveTime = CalculateDirectRouteMoveTime(plan.BaseMoveTime, current, plan.ExitTarget);
        bool released = TrySetDroppedEntityRoute(
            orchestrator,
            droppedEntity,
            plan.ExitTarget,
            moveTime,
            position,
            now,
            plan.Lane.ToString());

        if (ShouldLogRouteProbe())
        {
          LogRouteProbe(
              $"release-center result={released} orchestrator={orchestrator} dropped={droppedEntity} " +
              $"lane={plan.Lane} pos=({current.x:0.000},{current.y:0.000}) " +
              $"center=({plan.CenterTarget.x:0.000},{plan.CenterTarget.y:0.000}) centerDist={centerDistance:0.000} " +
              $"exit=({plan.ExitTarget.x:0.000},{plan.ExitTarget.y:0.000}) moveTime={moveTime} " +
              $"movee={DescribeAssociatedMovee(droppedEntity, now)}");
        }

        if (!released)
        {
          // If the small movee entity is not visible yet, keep the plan alive
          // and retry. Advancing without updating the active movee leaves the
          // dropped item parked on the splitter center.
          continue;
        }

        plan.Stage = SmartRouteStage.ToExit;
        plan.ExpiresAt = now + SmartRoutePlanLifetimeSeconds;
        updates ??= new List<KeyValuePair<Entity, SmartRoutePlan>>();
        updates.Add(new KeyValuePair<Entity, SmartRoutePlan>(droppedEntity, plan));
        continue;
      }

      float exitDistance = math.distance(current, plan.ExitTarget);
      float distanceFromCenter = math.distance(current, plan.CenterTarget);
      if (distanceFromCenter <= SmartRouteCenterArrivalRadius &&
          exitDistance > SmartRouteExitArrivalRadius)
      {
        if (ShouldLogRouteProbe())
        {
          LogRouteProbe(
              $"waiting-exit orchestrator={orchestrator} dropped={droppedEntity} lane={plan.Lane} " +
              $"pos=({current.x:0.000},{current.y:0.000}) exit=({plan.ExitTarget.x:0.000},{plan.ExitTarget.y:0.000}) " +
              $"exitDist={exitDistance:0.000} movee={DescribeAssociatedMovee(droppedEntity, now)}");
        }
      }

      if (exitDistance <= SmartRouteExitArrivalRadius ||
          distanceFromCenter > 0.85f)
      {
        stale ??= new List<Entity>();
        stale.Add(droppedEntity);
      }
    }

    if (updates != null)
    {
      for (int i = 0; i < updates.Count; i++)
      {
        _smartRoutePlans[updates[i].Key] = updates[i].Value;
      }
    }

    if (stale == null)
    {
      return;
    }

    for (int i = 0; i < stale.Count; i++)
    {
      _smartRoutePlans.Remove(stale[i]);
    }
  }

  private bool BuildRoutePieces(
      Entity orchestrator,
      SmartSplitterDecision decision,
      int itemAmount,
      ObjectID itemObject,
      int itemVariation,
      PugDatabase.DatabaseBankCD databaseBank,
      out int nextRouteStartIndex)
  {
    _routeLaneScratch.Clear();
    _routePieceScratch.Clear();

    int startIndex = _laneRoundRobinIndex.TryGetValue(orchestrator, out int storedIndex)
        ? storedIndex
        : 0;

    nextRouteStartIndex = startIndex;

    if (decision == SmartSplitterDecision.None ||
        decision == SmartSplitterDecision.Blocked ||
        itemAmount <= 0)
    {
      return false;
    }

    for (int offset = 0; offset < 3; offset++)
    {
      int laneIndex = (startIndex + offset) % 3;
      SmartSplitterLane lane = (SmartSplitterLane)laneIndex;
      if (DecisionIncludesLane(decision, lane))
      {
        _routeLaneScratch.Add(lane);
      }
    }

    if (_routeLaneScratch.Count == 0)
    {
      return false;
    }

    bool canSplitStack =
        itemAmount > 1 &&
        _routeLaneScratch.Count > 1 &&
        PugDatabase.GetEntityObjectInfo(itemObject, databaseBank.databaseBankBlob, itemVariation).isStackable;

    int pieceCount = canSplitStack
        ? math.min(itemAmount, _routeLaneScratch.Count)
        : 1;

    int baseAmount = itemAmount / pieceCount;
    int remainder = itemAmount % pieceCount;

    for (int i = 0; i < pieceCount; i++)
    {
      _routePieceScratch.Add(new SmartRoutePiece
      {
        Lane = _routeLaneScratch[i],
        Amount = baseAmount + (i < remainder ? 1 : 0)
      });
    }

    SmartSplitterLane lastLane = _routePieceScratch[_routePieceScratch.Count - 1].Lane;
    nextRouteStartIndex = ((int)lastLane + 1) % 3;
    return true;
  }

  private bool TrySetDroppedEntityRoute(
      Entity orchestrator,
      Entity droppedEntity,
      float2 target,
      int moveTime,
      float3 itemPosition,
      double now,
      string routeName)
  {
    Entity associatedMovee = FindMoveeForDroppedItem(droppedEntity, now);

    if (associatedMovee != Entity.Null &&
        EntityManager.Exists(associatedMovee) &&
        EntityManager.HasComponent<MoveeCD>(associatedMovee))
    {
      RedirectMoveeToTarget(associatedMovee, target, moveTime);
      SetDroppedBigEntityRoute(droppedEntity, target, moveTime);
      GuardDirectRoute(droppedEntity, orchestrator, now);
      return true;
    }

    if (EntityManager.Exists(droppedEntity) &&
        EntityManager.HasBuffer<SmallEntityRefBuffer>(droppedEntity))
    {
      if (ShouldLogRouteProbe())
      {
        LogRouteProbe(
            $"set-route-failed-no-movee orchestrator={orchestrator} dropped={droppedEntity} route={routeName} " +
            $"target=({target.x:0.000},{target.y:0.000}) moveTime={moveTime} " +
            $"smallRefs={DescribeSmallRefs(droppedEntity)}");
      }
      return false;
    }

    if (EntityManager.Exists(droppedEntity))
    {
      SetDroppedBigEntityRoute(droppedEntity, target, moveTime);
      GuardDirectRoute(droppedEntity, orchestrator, now);
      if (ShouldLogRouteProbe())
      {
        LogRouteProbe(
            $"set-route-big-only orchestrator={orchestrator} dropped={droppedEntity} route={routeName} " +
            $"target=({target.x:0.000},{target.y:0.000}) moveTime={moveTime}");
      }
      return true;
    }

    LogCoreRouting(
        $"direct-route failed orchestrator={orchestrator} dropped={droppedEntity} route={routeName} " +
        $"pos=({itemPosition.x:0.00},{itemPosition.z:0.00}) target=({target.x:0.00},{target.y:0.00})");

    return false;
  }

  private void SetDroppedBigEntityRoute(Entity droppedEntity, float2 target, int moveTime)
  {
    MoveeBigEntityCD route = new MoveeBigEntityCD
    {
      target = target,
      moveTimer = math.max(1, moveTime)
    };

    if (EntityManager.HasComponent<MoveeBigEntityCD>(droppedEntity))
    {
      EntityManager.SetComponentData(droppedEntity, route);
    }
    else
    {
      EntityManager.AddComponentData(droppedEntity, route);
    }
  }

  private void GuardDirectRoute(Entity droppedEntity, Entity orchestrator, double now)
  {
    _directRouteGuards[droppedEntity] = new DirectRouteGuard
    {
      Orchestrator = orchestrator,
      Until = now + DirectRouteGuardSeconds
    };
  }

  private bool ShouldSkipDirectRouteForOwner(Entity droppedEntity, Entity orchestrator, double now)
  {
    if (!_directRouteGuards.TryGetValue(droppedEntity, out DirectRouteGuard guard))
    {
      return false;
    }

    if (now > guard.Until)
    {
      _directRouteGuards.Remove(droppedEntity);
      return false;
    }

    return guard.Orchestrator == orchestrator;
  }

  private void PruneDirectRouteGuards(double now)
  {
    if (_directRouteGuards.Count == 0)
    {
      return;
    }

    List<Entity> stale = null;

    foreach (KeyValuePair<Entity, DirectRouteGuard> entry in _directRouteGuards)
    {
      if (now <= entry.Value.Until && EntityManager.Exists(entry.Key))
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
      _directRouteGuards.Remove(entity);
    }
  }

  private void PruneSmartRoutePlans(double now)
  {
    if (_smartRoutePlans.Count == 0)
    {
      return;
    }

    List<Entity> stale = null;

    foreach (KeyValuePair<Entity, SmartRoutePlan> entry in _smartRoutePlans)
    {
      if (now <= entry.Value.ExpiresAt &&
          EntityManager.Exists(entry.Key) &&
          EntityManager.HasComponent<LocalTransform>(entry.Key))
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

    for (int i = 0; i < stale.Count; i++)
    {
      _smartRoutePlans.Remove(stale[i]);
    }
  }

  private void RemoveSmartRoutePlansForOrchestrator(Entity orchestrator)
  {
    if (_smartRoutePlans.Count == 0)
    {
      return;
    }

    List<Entity> stale = null;

    foreach (KeyValuePair<Entity, SmartRoutePlan> entry in _smartRoutePlans)
    {
      if (entry.Value.Orchestrator != orchestrator)
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

    for (int i = 0; i < stale.Count; i++)
    {
      _smartRoutePlans.Remove(stale[i]);
    }
  }

  private static int CalculateDirectRouteMoveTime(
      int baseMoveTime,
      float2 currentPosition,
      float2 target)
  {
    float distance = math.distance(currentPosition, target);
    return math.max(1, Mathf.CeilToInt(baseMoveTime * math.max(0.05f, distance)));
  }

  private static bool TryGetLaneTarget(
      SmartSplitterLane lane,
      int2 center,
      int2 forwardDirection,
      int2 leftDirection,
      int2 rightDirection,
      out float2 target)
  {
    int2 direction = lane switch
    {
      SmartSplitterLane.Left => leftDirection,
      SmartSplitterLane.Center => forwardDirection,
      SmartSplitterLane.Right => rightDirection,
      _ => default
    };

    if (direction.Equals(default(int2)))
    {
      target = default;
      return false;
    }

    target = new float2(center.x + direction.x, center.y + direction.y);
    return true;
  }

  private static bool IsInsideDirectRoutingWindow(
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
    inputDistance = Dot(offsetX, offsetY, inputAxisX, inputAxisY);

    if (outputAxisDistance > InputLaneHalfWidth)
    {
      return false;
    }

    // We route just before the item enters the splitter, but allow a small
    // center overshoot so dense stacks cannot slip past the decision window
    // and park on a blocked smart splitter.
    return inputDistance >= -0.10f && inputDistance <= AcceptedItemRedirectDistance;
  }
  private void RedirectMoveeToTarget(Entity moveeEntity, float2 target, int moveTime)
  {
    MoveeCD movee = EntityManager.GetComponentData<MoveeCD>(moveeEntity);
    if (math.distancesq(movee.target, target) <= 0.0001f &&
        movee.moveTimer > 0)
    {
      return;
    }

    movee.target = target;
    movee.moveTimer = math.max(1, moveTime);
    EntityManager.SetComponentData(moveeEntity, movee);
  }

  private void RedirectMoveeToTarget(
      Entity moveeEntity,
      float2 target,
      int moveTime,
      float2 currentPosition)
  {
    float distance = math.distance(currentPosition, target);
    int scaledMoveTime = math.max(
        1,
        Mathf.CeilToInt(moveTime * math.max(0.05f, distance)));

    RedirectMoveeToTarget(moveeEntity, target, scaledMoveTime);
  }
  private Entity FindMoveeForDroppedItem(Entity droppedEntity, double now)
  {
    if (EntityManager.Exists(droppedEntity) &&
        EntityManager.HasBuffer<SmallEntityRefBuffer>(droppedEntity))
    {
      DynamicBuffer<SmallEntityRefBuffer> smallRefs =
          EntityManager.GetBuffer<SmallEntityRefBuffer>(droppedEntity);

      for (int i = 0; i < smallRefs.Length; i++)
      {
        Entity candidate = smallRefs[i].Value;
        if (!EntityManager.Exists(candidate) ||
            !EntityManager.HasComponent<MoveeCD>(candidate))
        {
          continue;
        }

        if (!EntityManager.HasComponent<BigEntityRefCD>(candidate) ||
            EntityManager.GetComponentData<BigEntityRefCD>(candidate).Value == droppedEntity)
        {
          return candidate;
        }
      }
    }

    if (_moveeLookupBuiltAt != now)
    {
      RebuildMoveeLookup(now);
    }

    return _moveeByDroppedEntity.TryGetValue(droppedEntity, out Entity movee)
        ? movee
        : Entity.Null;
  }

  private void RebuildMoveeLookup(double now)
  {
    _moveeByDroppedEntity.Clear();

    using NativeArray<Entity> moveeEntities = _moveeQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<BigEntityRefCD> moveeRefs = _moveeQuery.ToComponentDataArray<BigEntityRefCD>(Allocator.Temp);

    for (int i = 0; i < moveeEntities.Length; i++)
    {
      Entity droppedEntity = moveeRefs[i].Value;
      if (droppedEntity != Entity.Null && !_moveeByDroppedEntity.ContainsKey(droppedEntity))
      {
        _moveeByDroppedEntity.Add(droppedEntity, moveeEntities[i]);
      }
    }

    _moveeLookupBuiltAt = now;
  }

  private static bool TryGetRejectDirection(int2 inputDirection, out int2 rejectDirection)
  {
    rejectDirection = default;

    if (inputDirection.x == 0 && inputDirection.y > 0)
    {
      rejectDirection = new int2(1, 1);
      return true;
    }

    if (inputDirection.x > 0 && inputDirection.y == 0)
    {
      rejectDirection = new int2(1, -1);
      return true;
    }

    if (inputDirection.x == 0 && inputDirection.y < 0)
    {
      rejectDirection = new int2(-1, -1);
      return true;
    }

    if (inputDirection.x < 0 && inputDirection.y == 0)
    {
      rejectDirection = new int2(-1, 1);
      return true;
    }

    return false;
  }
  private void BlockSmartSplitterState(
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

    ApplyBlocked(orchestrator, buffer, originals, allMovers, allMoverData);
  }

  private void RestoreSmartSplitterToVanillaState(Entity orchestrator)
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
    RestoreOriginalOutputSplitCounts(originals);
    RemoveSmartRoutePlansForOrchestrator(orchestrator);
    _smartStateDirty.Remove(orchestrator);
  }

  private bool HasSmartRuntimeState(Entity orchestrator)
  {
    return _smartStateDirty.Contains(orchestrator);
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
  private void CollectInputCorridorCandidateItems(int2 centerTile, int2 inputDirection)
  {
    _candidateDroppedItemIndexes.Clear();

    int2 perpendicular = new int2(-inputDirection.y, inputDirection.x);
    int maxSteps = Mathf.CeilToInt(InputDetectDistance) + 1;

    for (int step = 0; step <= maxSteps; step++)
    {
      int2 tile = new int2(
          centerTile.x - inputDirection.x * step,
          centerTile.y - inputDirection.y * step);

      for (int side = -1; side <= 1; side++)
      {
        int candidateX = tile.x + perpendicular.x * side;
        int candidateY = tile.y + perpendicular.y * side;
        long key = GetTileKey(candidateX, candidateY);

        if (!_droppedItemIndexesByTile.TryGetValue(key, out List<int> indexes))
        {
          continue;
        }

        for (int i = 0; i < indexes.Count; i++)
        {
          _candidateDroppedItemIndexes.Add(indexes[i]);
        }
      }
    }
  }
  private void ApplyBlocked(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals,
      NativeArray<Entity> allMovers,
      NativeArray<MoverCD> allMoverData)
  {
    buffer.Clear();
    DisableSharedMoverTriggers(orchestrator);
    SetAllSplitterMoverSplitCounts(orchestrator, 0, allMovers, allMoverData);
    ForceEnabledMoverFromSharedState(originals, Entity.Null);
    _smartStateDirty.Add(orchestrator);
  }
  private void RestoreBothOutputs(
      Entity orchestrator,
      DynamicBuffer<MoversWithSharedStateBuffer> buffer,
      SmartSplitterOriginalOutputsCD originals)
  {
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

  private void RestoreOriginalOutputSplitCounts(SmartSplitterOriginalOutputsCD originals)
  {
    RestoreOriginalOutputSplitCount(originals.LeftMoverEntity);
    RestoreOriginalOutputSplitCount(originals.RightMoverEntity);
  }

  private void RestoreOriginalOutputSplitCount(Entity moverEntity)
  {
    if (!EntityManager.Exists(moverEntity) ||
        !EntityManager.HasComponent<MoverCD>(moverEntity))
    {
      return;
    }

    MoverCD mover = EntityManager.GetComponentData<MoverCD>(moverEntity);
    if (mover.splitsIntoOnMove == VanillaSplitterOutputSplitCount)
    {
      return;
    }

    mover.splitsIntoOnMove = VanillaSplitterOutputSplitCount;
    EntityManager.SetComponentData(moverEntity, mover);
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

    EnsurePlacedSplitterVariationIndex();
    if (_placedSplitterVariationByTile.TryGetValue(GetTileKey(center.x, center.y), out placedVariation))
    {
      return true;
    }

    int bestDistance = int.MaxValue;
    int bestVariation = 0;
    bool found = false;

    for (int dx = -1; dx <= 1; dx++)
    {
      for (int dy = -1; dy <= 1; dy++)
      {
        int distance = Mathf.Abs(dx) + Mathf.Abs(dy);
        if (distance > 1 || distance >= bestDistance)
        {
          continue;
        }

        if (!_placedSplitterVariationByTile.TryGetValue(
                GetTileKey(center.x + dx, center.y + dy),
                out int variation))
        {
          continue;
        }

        bestDistance = distance;
        bestVariation = variation;
        found = true;
      }
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

  private void EnsurePlacedSplitterVariationIndex()
  {
    double now = World.Time.ElapsedTime;
    if (_placedSplitterVariationIndexBuiltAt == now)
    {
      return;
    }

    _placedSplitterVariationIndexBuiltAt = now;
    _placedSplitterVariationByTile.Clear();

    using NativeArray<ObjectDataCD> objectData = _objectDataTransformQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> transforms = _objectDataTransformQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    for (int i = 0; i < objectData.Length; i++)
    {
      if (objectData[i].objectID != ObjectID.ConveyorBeltSplitter)
      {
        continue;
      }

      int x = Mathf.RoundToInt(transforms[i].Position.x);
      int y = Mathf.RoundToInt(transforms[i].Position.z);
      _placedSplitterVariationByTile[GetTileKey(x, y)] = objectData[i].variation;
    }
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

  private void LogRouteProbe(string message)
  {
    if (!ShouldLogRouteProbe())
    {
      return;
    }

    _routeHandoffDiagnosticLogs++;
    Debug.Log("[SmartSplitterRouteProbe] " + message);
  }

  private bool ShouldLogRouteProbe()
  {
    return EnableRouteHandoffDiagnostics &&
           _routeHandoffDiagnosticLogs < MaxRouteHandoffDiagnosticLogs;
  }

  private string DescribeAssociatedMovee(Entity droppedEntity, double now)
  {
    Entity movee = FindMoveeForDroppedItem(droppedEntity, now);
    return DescribeMovee(movee);
  }

  private string DescribeMovee(Entity movee)
  {
    if (movee == Entity.Null)
    {
      return "null";
    }

    if (!EntityManager.Exists(movee))
    {
      return $"{movee}:missing";
    }

    string enabledState = "no-enabled-tag";
    if (EntityManager.HasComponent<BigEntityIsEnabledCD>(movee))
    {
      enabledState = EntityManager.IsComponentEnabled<BigEntityIsEnabledCD>(movee)
          ? "enabled"
          : "disabled";
    }

    string bigRef = "no-big-ref";
    if (EntityManager.HasComponent<BigEntityRefCD>(movee))
    {
      bigRef = EntityManager.GetComponentData<BigEntityRefCD>(movee).Value.ToString();
    }

    if (!EntityManager.HasComponent<MoveeCD>(movee))
    {
      return $"{movee}:{enabledState}:big={bigRef}:no-movee";
    }

    MoveeCD data = EntityManager.GetComponentData<MoveeCD>(movee);
    return $"{movee}:{enabledState}:big={bigRef}:" +
           $"pos=({data.position.x:0.000},{data.position.y:0.000}):" +
           $"target=({data.target.x:0.000},{data.target.y:0.000}):timer={data.moveTimer}";
  }

  private string DescribeSmallRefs(Entity droppedEntity)
  {
    if (!EntityManager.Exists(droppedEntity))
    {
      return "dropped-missing";
    }

    if (!EntityManager.HasBuffer<SmallEntityRefBuffer>(droppedEntity))
    {
      return "no-buffer";
    }

    DynamicBuffer<SmallEntityRefBuffer> smallRefs =
        EntityManager.GetBuffer<SmallEntityRefBuffer>(droppedEntity);

    if (smallRefs.Length == 0)
    {
      return "empty-buffer";
    }

    string result = $"count={smallRefs.Length}";
    int max = math.min(smallRefs.Length, 3);
    for (int i = 0; i < max; i++)
    {
      result += $" [{i}]={DescribeMovee(smallRefs[i].Value)}";
    }

    return result;
  }
  private static string FormatInt2(int2 value)
  {
    return $"({value.x},{value.y})";
  }
  private static float Dot(float ax, float ay, float bx, float by)
  {
    return ax * bx + ay * by;
  }
}
