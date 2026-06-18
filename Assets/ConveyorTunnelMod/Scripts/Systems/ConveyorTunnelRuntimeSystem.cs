using System;
using System.Collections.Generic;
using Pug.Automation;
using Pug.Automation.Components;
using Pug.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PugAutomationSystem))]
[UpdateBefore(typeof(StateSystemGroup))]
public partial class ConveyorTunnelRuntimeSystem : SystemBase
{
  private struct EndpointSnapshot
  {
    public Entity Entity;
    public int2 Tile;
    public int2 Direction;
    public int Variation;
  }

  private struct ActiveTunnelPair
  {
    public int PairId;
    public Entity EntranceEndpoint;
    public Entity ExitEndpoint;
    public int2 EntranceTile;
    public int2 ExitTile;
    public int2 Direction;
    public float2 IntakePoint;
    public float2 ReleasePoint;
    public float2 ReleaseTarget;
    public float TravelDuration;
    public bool AcceptsNewItems;
  }

  private struct IntakeCandidate
  {
    public Entity DroppedEntity;
    public ActiveTunnelPair Pair;
    public float2 ItemPosition;
  }

  private const double DiscoveryRefreshIntervalSeconds = 0.25d;
  private const double FullVisualBroadcastIntervalSeconds = 5.0d;
  private const float IntakeLaneHalfWidth = 0.26f;
  private const float IntakeCommitForwardDistance = -0.06f;
  private const float IntakeCommitMaxForwardDistance = 0.10f;
  private const float IntakeRouteMinMotionSq = 0.0001f;
  private const float IntakeRouteReverseTolerance = 0.01f;
  private const float UndergroundHeight = -50.0f;
  private const float UndergroundHoldBaseCoordinate = -200000.0f;
  private const float UndergroundHoldSpacing = 3.0f;
  private const float MinTravelDuration = 0.35f;
  private const float TravelSecondsPerTile = 1.5f;
  private const int RouteMoveTime = 1;
  private const int MaxTransportDiagnosticLogs = 2048;
  private const string RuntimeBuildTag = "mouth-aligned-side-intake-v24";

  private readonly List<EndpointSnapshot> _endpoints = new List<EndpointSnapshot>();
  private readonly HashSet<Entity> _pairedEndpoints = new HashSet<Entity>();
  private readonly Dictionary<Entity, ConveyorTunnelEndpointStateCD> _statesByEndpoint =
      new Dictionary<Entity, ConveyorTunnelEndpointStateCD>();
  private readonly Dictionary<Entity, EndpointSnapshot> _endpointsByEntity =
      new Dictionary<Entity, EndpointSnapshot>();
  private readonly Dictionary<long, EndpointSnapshot> _endpointsByTile =
      new Dictionary<long, EndpointSnapshot>();
  private readonly Dictionary<int, ActiveTunnelPair> _activePairsById =
      new Dictionary<int, ActiveTunnelPair>();
  private readonly Dictionary<long, List<int>> _pairIdsByInterestTile =
      new Dictionary<long, List<int>>();
  private readonly Dictionary<Entity, int> _payloadCountsByEndpoint =
      new Dictionary<Entity, int>();
  private readonly Dictionary<Entity, Entity> _moveeByDroppedEntity =
      new Dictionary<Entity, Entity>();
  private readonly List<IntakeCandidate> _intakeCandidates = new List<IntakeCandidate>();
  private readonly HashSet<Entity> _seenDroppedItemsThisIntake = new HashSet<Entity>();
  private readonly HashSet<long> _seenEndpointVisualTiles = new HashSet<long>();
  private readonly Dictionary<long, int> _lastVisualStateSignatureByTile =
      new Dictionary<long, int>();
  private readonly List<long> _visualTilesToClear = new List<long>();
  private readonly List<ConveyorTunnelPersistentPair> _persistedPairs =
      new List<ConveyorTunnelPersistentPair>();
  private readonly List<ConveyorTunnelPendingEndpoint> _persistedPendingEndpoints =
      new List<ConveyorTunnelPendingEndpoint>();
  private readonly HashSet<long> _reservedPersistedPairTiles = new HashSet<long>();
  private readonly HashSet<Entity> _knownPendingEndpointEntities = new HashSet<Entity>();
  private readonly HashSet<Entity> _placementCandidateEntities = new HashSet<Entity>();
  private readonly List<EndpointSnapshot> _placementCandidates =
      new List<EndpointSnapshot>();
  private readonly List<int2> _destroyedEndpointTiles = new List<int2>();

  private EntityQuery _objectQuery;
  private EntityQuery _destroyedObjectQuery;
  private EntityQuery _droppedItemQuery;
  private EntityQuery _payloadQuery;
  private EntityQuery _moveeQuery;
  private double _nextDiscoveryRefreshAt = -1d;
  private double _nextFullVisualBroadcastAt = -1d;
  private double _moveeLookupBuiltAt = -1d;
  private double _lastNoActivePairLogAt = -10d;
  private double _lastNoDroppedItemLogAt = -10d;
  private int _lastEndpointCount = -1;
  private int _lastPairCount = -1;
  private int _transportDiagnosticLogs;
  private bool _forceVisualStateBroadcastThisRefresh;

  protected override void OnCreate()
  {
    _objectQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadOnly<LocalTransform>()
      },
      None = new[]
      {
        ComponentType.ReadOnly<EntityDestroyedCD>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _destroyedObjectQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadOnly<LocalTransform>(),
        ComponentType.ReadOnly<EntityDestroyedCD>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _droppedItemQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ObjectDataCD>(),
        ComponentType.ReadOnly<ContainedObjectsBuffer>(),
        ComponentType.ReadOnly<LocalTransform>()
      },
      None = new[]
      {
        ComponentType.ReadOnly<EntityDestroyedCD>(),
        ComponentType.ReadOnly<ConveyorTunnelPayloadCD>()
      }
    });

    _payloadQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<ConveyorTunnelPayloadCD>(),
        ComponentType.ReadOnly<LocalTransform>()
      },
      None = new[]
      {
        ComponentType.ReadOnly<EntityDestroyedCD>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _moveeQuery = GetEntityQuery(new EntityQueryDesc
    {
      All = new[]
      {
        ComponentType.ReadOnly<MoveeCD>(),
        ComponentType.ReadOnly<BigEntityRefCD>()
      },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    Debug.Log(
        $"[ConveyorTunnelRuntimeSystem] Created build={RuntimeBuildTag} world={World?.Name ?? "unknown"} " +
        $"commitForward={IntakeCommitForwardDistance}-{IntakeCommitMaxForwardDistance} " +
        $"routeReverseTolerance={IntakeRouteReverseTolerance} " +
        $"travelSecondsPerTile={TravelSecondsPerTile} laneHalf={IntakeLaneHalfWidth}");
  }

  protected override void OnUpdate()
  {
    if (!ConveyorTunnelIds.TryRefresh())
    {
      return;
    }

    double now = World.Time.ElapsedTime;
    ProgressPayloads(World.Time.DeltaTime, now);

    if (now >= _nextDiscoveryRefreshAt)
    {
      RefreshEndpointPairs(ConveyorTunnelIds.ConveyorTunnelObjectID);
      _nextDiscoveryRefreshAt = now + DiscoveryRefreshIntervalSeconds;
    }

    TryIntakeDroppedItems(now);
  }

  private void RefreshEndpointPairs(ObjectID tunnelObjectID)
  {
    _endpoints.Clear();
    _pairedEndpoints.Clear();
    _statesByEndpoint.Clear();
    _endpointsByEntity.Clear();
    _endpointsByTile.Clear();
    _activePairsById.Clear();
    _pairIdsByInterestTile.Clear();
    _seenEndpointVisualTiles.Clear();
    _reservedPersistedPairTiles.Clear();
    ConveyorTunnelPersistence.EnsureLoadedForCurrentWorld();
    BuildPayloadCountsByEndpoint();
    double now = World.Time.ElapsedTime;
    _forceVisualStateBroadcastThisRefresh = now >= _nextFullVisualBroadcastAt;

    NativeArray<Entity> entities = _objectQuery.ToEntityArray(Allocator.Temp);
    try
    {
      for (int i = 0; i < entities.Length; i++)
      {
        Entity entity = entities[i];
        ObjectDataCD objectData = EntityManager.GetComponentData<ObjectDataCD>(entity);

        if (objectData.objectID != tunnelObjectID)
        {
          continue;
        }

        int2 direction = ConveyorTunnelDirectionUtility.GetDirectionFromVariation(objectData.variation);
        if (direction.Equals(int2.zero))
        {
          continue;
        }

        LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
        EndpointSnapshot snapshot = new EndpointSnapshot
        {
          Entity = entity,
          Tile = WorldToTile(transform.Position),
          Direction = direction,
          Variation = objectData.variation
        };

        _endpoints.Add(snapshot);
        _endpointsByEntity[entity] = snapshot;
        long tileKey = GetTileKey(snapshot.Tile.x, snapshot.Tile.y);
        _endpointsByTile[tileKey] = snapshot;

        if (!EntityManager.HasComponent<ConveyorTunnelEndpointTag>(entity))
        {
          EntityManager.AddComponent<ConveyorTunnelEndpointTag>(entity);
        }
      }
    }
    finally
    {
      entities.Dispose();
    }

    _endpoints.Sort(CompareEndpoints);
    PruneDestroyedEndpointLinks(tunnelObjectID);

    int nextPairId = 1;
    LockPayloadPairs(ref nextPairId);
    ApplyPersistedPairs(ref nextPairId);
    ApplyPlacementOrderPairing(ref nextPairId);

    ClearMissingEndpointVisualStates();
    if (_forceVisualStateBroadcastThisRefresh)
    {
      _nextFullVisualBroadcastAt = now + FullVisualBroadcastIntervalSeconds;
      _forceVisualStateBroadcastThisRefresh = false;
    }

    int pairCount = nextPairId - 1;
    if (ConveyorTunnelDebugSettings.EnableDiscoveryLogs &&
        (_lastEndpointCount != _endpoints.Count || _lastPairCount != pairCount))
    {
      Debug.Log(
          $"[ConveyorTunnelRuntimeSystem] endpoints={_endpoints.Count} pairs={pairCount} " +
          $"persisted={_persistedPairs.Count} pending={ConveyorTunnelPersistence.HasPending()}");
    }

    _lastEndpointCount = _endpoints.Count;
    _lastPairCount = pairCount;
  }

  private void LockPayloadPairs(ref int nextPairId)
  {
    for (int i = 0; i < _endpoints.Count; i++)
    {
      EndpointSnapshot current = _endpoints[i];
      if (_pairedEndpoints.Contains(current.Entity) ||
          !HasPayloads(current.Entity) ||
          !EntityManager.HasComponent<ConveyorTunnelEndpointStateCD>(current.Entity))
      {
        continue;
      }

      ConveyorTunnelEndpointStateCD previousState =
          EntityManager.GetComponentData<ConveyorTunnelEndpointStateCD>(current.Entity);
      if (previousState.Linked == 0 || previousState.PairedEndpoint == Entity.Null)
      {
        continue;
      }

      Entity entranceEntity = previousState.Role == ConveyorTunnelEndpointRole.Entrance
          ? current.Entity
          : previousState.PairedEndpoint;
      Entity exitEntity = previousState.Role == ConveyorTunnelEndpointRole.Entrance
          ? previousState.PairedEndpoint
          : current.Entity;

      if (_pairedEndpoints.Contains(entranceEntity) ||
          _pairedEndpoints.Contains(exitEntity) ||
          !_endpointsByEntity.TryGetValue(entranceEntity, out EndpointSnapshot entrance) ||
          !_endpointsByEntity.TryGetValue(exitEntity, out EndpointSnapshot exit))
      {
        continue;
      }

      int2 lockedDirection = previousState.Direction.Equals(int2.zero)
          ? entrance.Direction
          : previousState.Direction;
      if (lockedDirection.Equals(int2.zero))
      {
        continue;
      }

      bool acceptsNewItems =
          EndpointDirectionMatchesCurrent(entranceEntity, lockedDirection) &&
          EndpointDirectionMatchesCurrent(exitEntity, lockedDirection);

      entrance.Direction = lockedDirection;
      exit.Direction = lockedDirection;

      ActivatePair(ref nextPairId, entrance, exit, acceptsNewItems);
    }
  }

  private void PruneDestroyedEndpointLinks(ObjectID tunnelObjectID)
  {
    _destroyedEndpointTiles.Clear();

    NativeArray<Entity> destroyedEntities = _destroyedObjectQuery.ToEntityArray(Allocator.Temp);
    try
    {
      for (int i = 0; i < destroyedEntities.Length; i++)
      {
        Entity entity = destroyedEntities[i];
        if (!EntityManager.Exists(entity) ||
            !EntityManager.HasComponent<ObjectDataCD>(entity) ||
            !EntityManager.HasComponent<LocalTransform>(entity))
        {
          continue;
        }

        ObjectDataCD objectData = EntityManager.GetComponentData<ObjectDataCD>(entity);
        if (objectData.objectID != tunnelObjectID)
        {
          continue;
        }

        int2 direction = ConveyorTunnelDirectionUtility.GetDirectionFromVariation(objectData.variation);
        if (direction.Equals(int2.zero))
        {
          continue;
        }

        LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
        AddUniqueDestroyedEndpointTile(WorldToTile(transform.Position));
      }
    }
    finally
    {
      destroyedEntities.Dispose();
    }

    for (int i = 0; i < _destroyedEndpointTiles.Count; i++)
    {
      int2 tile = _destroyedEndpointTiles[i];
      if (_endpointsByTile.ContainsKey(GetTileKey(tile.x, tile.y)))
      {
        continue;
      }

      ConveyorTunnelPersistence.DeletePairByEndpoint(tile);
      ConveyorTunnelPersistence.ClearPendingIfTile(tile);
    }
  }

  private void AddUniqueDestroyedEndpointTile(int2 tile)
  {
    for (int i = 0; i < _destroyedEndpointTiles.Count; i++)
    {
      if (_destroyedEndpointTiles[i].Equals(tile))
      {
        return;
      }
    }

    _destroyedEndpointTiles.Add(tile);
  }

  private void ApplyPersistedPairs(ref int nextPairId)
  {
    _persistedPairs.Clear();
    ConveyorTunnelPersistence.GetPairs(_persistedPairs);

    for (int i = 0; i < _persistedPairs.Count; i++)
    {
      ConveyorTunnelPersistentPair persistedPair = _persistedPairs[i];
      _reservedPersistedPairTiles.Add(
          GetTileKey(persistedPair.EntranceTile.x, persistedPair.EntranceTile.y));
      _reservedPersistedPairTiles.Add(
          GetTileKey(persistedPair.ExitTile.x, persistedPair.ExitTile.y));
      bool hasEntrance = TryGetEndpointAtTile(persistedPair.EntranceTile, out EndpointSnapshot entrance);
      bool hasExit = TryGetEndpointAtTile(persistedPair.ExitTile, out EndpointSnapshot exit);

      if (!hasEntrance || !hasExit)
      {
        if (ConveyorTunnelDebugSettings.EnablePairLogs && (hasEntrance || hasExit))
        {
          Debug.Log(
              $"[ConveyorTunnelRuntimeSystem] Saved pair waiting for missing endpoint " +
              $"entrance={persistedPair.EntranceTile} hasEntrance={hasEntrance} " +
              $"exit={persistedPair.ExitTile} hasExit={hasExit}");
        }

        continue;
      }

      if (_pairedEndpoints.Contains(entrance.Entity) || _pairedEndpoints.Contains(exit.Entity))
      {
        continue;
      }

      if (CanUsePair(entrance, exit, persistedPair.Direction))
      {
        ActivatePair(ref nextPairId, entrance, exit, true);
        continue;
      }

      if (HasPayloads(entrance.Entity) || HasPayloads(exit.Entity))
      {
        continue;
      }

      int2 reverseDirection = new int2(-persistedPair.Direction.x, -persistedPair.Direction.y);
      if (CanUsePair(exit, entrance, reverseDirection))
      {
        ConveyorTunnelPersistence.SavePair(exit.Tile, entrance.Tile, reverseDirection);
        ActivatePair(ref nextPairId, exit, entrance, true);
        continue;
      }

      ConveyorTunnelPersistence.DeletePairByEndpoint(persistedPair.EntranceTile);
    }
  }

  private void ApplyPlacementOrderPairing(ref int nextPairId)
  {
    _persistedPendingEndpoints.Clear();
    _knownPendingEndpointEntities.Clear();
    _placementCandidateEntities.Clear();
    _placementCandidates.Clear();
    ConveyorTunnelPersistence.GetPendingEndpoints(_persistedPendingEndpoints);

    bool isInitialMigration =
        !ConveyorTunnelPersistence.HasPlacementModeInitialized();
    if (isInitialMigration && _endpoints.Count > 0)
    {
      AutoPairExistingEndpoints(ref nextPairId);
      ConveyorTunnelPersistence.MarkPlacementModeInitialized();
    }
    else if (!isInitialMigration)
    {
      for (int i = 0; i < _persistedPendingEndpoints.Count; i++)
      {
        ConveyorTunnelPendingEndpoint pending = _persistedPendingEndpoints[i];
        if (!TryGetEndpointAtTile(pending.Tile, out EndpointSnapshot endpoint) ||
            _pairedEndpoints.Contains(endpoint.Entity) ||
            IsReservedPersistedPairTile(endpoint.Tile))
        {
          continue;
        }

        if (endpoint.Direction.Equals(pending.Direction))
        {
          _knownPendingEndpointEntities.Add(endpoint.Entity);
        }
        else
        {
          ConveyorTunnelPersistence.SetPending(endpoint.Tile, endpoint.Direction);
          AddPlacementCandidate(endpoint);
        }
      }

      for (int i = 0; i < _endpoints.Count; i++)
      {
        EndpointSnapshot endpoint = _endpoints[i];
        if (_pairedEndpoints.Contains(endpoint.Entity) ||
            IsReservedPersistedPairTile(endpoint.Tile) ||
            _knownPendingEndpointEntities.Contains(endpoint.Entity))
        {
          continue;
        }

        AddPlacementCandidate(endpoint);
      }

      for (int i = 0; i < _placementCandidates.Count; i++)
      {
        EndpointSnapshot endpoint = _placementCandidates[i];
        if (_pairedEndpoints.Contains(endpoint.Entity) ||
            IsReservedPersistedPairTile(endpoint.Tile))
        {
          continue;
        }

        if (TryFindBestPendingPairForEndpoint(
                endpoint,
                out EndpointSnapshot entrance,
                out EndpointSnapshot exit))
        {
          ConveyorTunnelPersistence.SavePair(
              entrance.Tile,
              exit.Tile,
              entrance.Direction);
          ActivatePair(ref nextPairId, entrance, exit, true);
          _knownPendingEndpointEntities.Remove(entrance.Entity);
          _knownPendingEndpointEntities.Remove(exit.Entity);
        }
        else
        {
          ConveyorTunnelPersistence.SetPending(endpoint.Tile, endpoint.Direction);
          _knownPendingEndpointEntities.Add(endpoint.Entity);
        }
      }
    }

    for (int i = 0; i < _endpoints.Count; i++)
    {
      EndpointSnapshot endpoint = _endpoints[i];
      if (_pairedEndpoints.Contains(endpoint.Entity))
      {
        continue;
      }

      bool pending = !IsReservedPersistedPairTile(endpoint.Tile);
      if (pending)
      {
        ConveyorTunnelPersistence.SetPending(endpoint.Tile, endpoint.Direction);
      }

      SetState(endpoint, CreateUnlinkedEndpointState(
          endpoint,
          HasPayloads(endpoint.Entity),
          pending));
    }
  }

  private void AutoPairExistingEndpoints(ref int nextPairId)
  {
    for (int i = 0; i < _endpoints.Count; i++)
    {
      EndpointSnapshot endpoint = _endpoints[i];
      if (_pairedEndpoints.Contains(endpoint.Entity) ||
          IsReservedPersistedPairTile(endpoint.Tile))
      {
        continue;
      }

      if (!TryFindBestPairForEndpoint(
              endpoint,
              Entity.Null,
              out EndpointSnapshot entrance,
              out EndpointSnapshot exit))
      {
        continue;
      }

      ConveyorTunnelPersistence.SavePair(entrance.Tile, exit.Tile, entrance.Direction);
      ActivatePair(ref nextPairId, entrance, exit, true);
    }
  }

  private void AddPlacementCandidate(EndpointSnapshot endpoint)
  {
    if (_placementCandidateEntities.Add(endpoint.Entity))
    {
      _placementCandidates.Add(endpoint);
    }
  }

  private bool TryFindBestPendingPairForEndpoint(
      EndpointSnapshot endpoint,
      out EndpointSnapshot entrance,
      out EndpointSnapshot exit)
  {
    entrance = default;
    exit = default;
    int bestDistance = int.MaxValue;
    bool found = false;

    for (int i = 0; i < _endpoints.Count; i++)
    {
      EndpointSnapshot candidate = _endpoints[i];
      Entity candidateEntity = candidate.Entity;
      if (candidateEntity == endpoint.Entity ||
          !_knownPendingEndpointEntities.Contains(candidateEntity) ||
          _pairedEndpoints.Contains(candidateEntity) ||
          IsReservedPersistedPairTile(candidate.Tile))
      {
        continue;
      }

      ConveyorTunnelPairingEvaluation evaluation =
          ConveyorTunnelDirectionUtility.EvaluatePairing(
              candidate.Tile,
              candidate.Direction,
              endpoint.Tile,
              endpoint.Direction);
      if (!evaluation.IsValid)
      {
        continue;
      }

      int distance = math.abs(
          ConveyorTunnelDirectionUtility.Dot(
              endpoint.Tile - candidate.Tile,
              candidate.Direction));
      if (distance >= bestDistance)
      {
        continue;
      }

      bestDistance = distance;
      found = true;
      if (evaluation.EntranceTile.Equals(candidate.Tile))
      {
        entrance = candidate;
        exit = endpoint;
      }
      else
      {
        entrance = endpoint;
        exit = candidate;
      }
    }

    return found;
  }

  private void ActivatePair(
      ref int nextPairId,
      EndpointSnapshot entrance,
      EndpointSnapshot exit,
      bool acceptsNewItems)
  {
    int pairId = nextPairId++;
    ActiveTunnelPair pair = CreateActivePair(pairId, entrance, exit, acceptsNewItems);

    SetState(entrance, CreateLinkedState(
        pairId,
        ConveyorTunnelEndpointRole.Entrance,
        entrance,
        exit,
        HasPayloads(entrance.Entity)));

    SetState(exit, CreateLinkedState(
        pairId,
        ConveyorTunnelEndpointRole.Exit,
        exit,
        entrance,
        HasPayloads(exit.Entity)));

    _activePairsById[pairId] = pair;
    RegisterPairInterestTiles(pair);
    _pairedEndpoints.Add(entrance.Entity);
    _pairedEndpoints.Add(exit.Entity);

    if (ConveyorTunnelDebugSettings.EnablePairLogs)
    {
      Debug.Log(
          $"[ConveyorTunnelRuntimeSystem] Pair {pairId}: entrance={entrance.Tile} exit={exit.Tile} dir={entrance.Direction} accepts={acceptsNewItems}");
    }
  }

  private bool TryGetEndpointAtTile(int2 tile, out EndpointSnapshot endpoint)
  {
    return _endpointsByTile.TryGetValue(GetTileKey(tile.x, tile.y), out endpoint);
  }

  private bool IsReservedPersistedPairTile(int2 tile)
  {
    return _reservedPersistedPairTiles.Contains(GetTileKey(tile.x, tile.y));
  }

  private bool CanUsePair(EndpointSnapshot entrance, EndpointSnapshot exit, int2 direction)
  {
    return ConveyorTunnelDirectionUtility.IsOrderedPair(
        entrance.Tile,
        entrance.Direction,
        exit.Tile,
        exit.Direction,
        direction);
  }

  private bool EndpointDirectionMatchesCurrent(Entity endpoint, int2 direction)
  {
    return _endpointsByEntity.TryGetValue(endpoint, out EndpointSnapshot current) &&
           current.Direction.Equals(direction);
  }

  private void BuildPayloadCountsByEndpoint()
  {
    _payloadCountsByEndpoint.Clear();

    NativeArray<Entity> payloadEntities = _payloadQuery.ToEntityArray(Allocator.Temp);
    try
    {
      for (int i = 0; i < payloadEntities.Length; i++)
      {
        Entity entity = payloadEntities[i];
        if (!EntityManager.Exists(entity) ||
            !EntityManager.HasComponent<ConveyorTunnelPayloadCD>(entity))
        {
          continue;
        }

        ConveyorTunnelPayloadCD payload =
            EntityManager.GetComponentData<ConveyorTunnelPayloadCD>(entity);
        IncrementPayloadCount(payload.EntranceEndpoint);
        IncrementPayloadCount(payload.ExitEndpoint);
      }
    }
    finally
    {
      payloadEntities.Dispose();
    }
  }

  private void IncrementPayloadCount(Entity endpoint)
  {
    if (endpoint == Entity.Null)
    {
      return;
    }

    _payloadCountsByEndpoint.TryGetValue(endpoint, out int count);
    _payloadCountsByEndpoint[endpoint] = count + 1;
  }

  private bool HasPayloads(Entity endpoint)
  {
    return _payloadCountsByEndpoint.TryGetValue(endpoint, out int count) && count > 0;
  }

  private bool TryFindBestPairForEndpoint(
      EndpointSnapshot endpoint,
      Entity reservedPendingEndpoint,
      out EndpointSnapshot entrance,
      out EndpointSnapshot exit)
  {
    entrance = default;
    exit = default;
    int bestDistance = int.MaxValue;
    bool found = false;

    for (int i = 0; i < _endpoints.Count; i++)
    {
      EndpointSnapshot candidate = _endpoints[i];
      if (candidate.Entity == endpoint.Entity)
      {
        continue;
      }

      if (candidate.Entity == reservedPendingEndpoint)
      {
        continue;
      }

      if (_pairedEndpoints.Contains(candidate.Entity))
      {
        continue;
      }

      if (IsReservedPersistedPairTile(candidate.Tile))
      {
        continue;
      }

      ConveyorTunnelPairingEvaluation evaluation =
          ConveyorTunnelDirectionUtility.EvaluatePairing(
              endpoint.Tile,
              endpoint.Direction,
              candidate.Tile,
              candidate.Direction);
      if (!evaluation.IsValid)
      {
        continue;
      }

      int distance = math.abs(
          ConveyorTunnelDirectionUtility.Dot(
              candidate.Tile - endpoint.Tile,
              endpoint.Direction));
      if (distance >= bestDistance)
      {
        continue;
      }

      bestDistance = distance;
      found = true;
      if (evaluation.EntranceTile.Equals(endpoint.Tile))
      {
        entrance = endpoint;
        exit = candidate;
      }
      else
      {
        entrance = candidate;
        exit = endpoint;
      }
    }

    return found;
  }

  private void RegisterPairInterestTiles(ActiveTunnelPair pair)
  {
    RegisterPairInterestTile(pair.EntranceTile, pair.PairId);
    RegisterPairInterestTile(pair.EntranceTile - pair.Direction, pair.PairId);
  }

  private void RegisterPairInterestTile(int2 tile, int pairId)
  {
    long key = GetTileKey(tile.x, tile.y);
    if (!_pairIdsByInterestTile.TryGetValue(key, out List<int> pairIds))
    {
      pairIds = new List<int>(1);
      _pairIdsByInterestTile.Add(key, pairIds);
    }

    pairIds.Add(pairId);
  }

  private void TryIntakeDroppedItems(double now)
  {
    if (_activePairsById.Count == 0)
    {
      if (ConveyorTunnelDebugSettings.EnableTransportLogs &&
          _endpoints.Count > 0 &&
          now >= _lastNoActivePairLogAt + 2.0d)
      {
        _lastNoActivePairLogAt = now;
        LogTransportDiagnostic(
            $"no-active-pairs endpoints={_endpoints.Count} lastPairCount={_lastPairCount}",
            now);
      }

      return;
    }

    _intakeCandidates.Clear();
    _seenDroppedItemsThisIntake.Clear();
    int droppedItemCount = 0;
    int nearbyCandidateCount = 0;
    float nearestDistanceSq = float.MaxValue;
    float2 nearestPosition = default;
    string nearestSource = "none";
    int nearestPairId = 0;
    float nearestForwardDistance = 0.0f;
    float nearestLateralDistance = 0.0f;
    int2 nearestPairDirection = int2.zero;

    EntityTypeHandle entityType = GetEntityTypeHandle();
    ComponentTypeHandle<ObjectDataCD> objectDataType = GetComponentTypeHandle<ObjectDataCD>(true);
    ComponentTypeHandle<LocalTransform> transformType = GetComponentTypeHandle<LocalTransform>(true);
    BufferTypeHandle<ContainedObjectsBuffer> containedType =
        GetBufferTypeHandle<ContainedObjectsBuffer>(true);

    using (NativeArray<ArchetypeChunk> chunks = _droppedItemQuery.ToArchetypeChunkArray(Allocator.Temp))
    {
      for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
      {
        ArchetypeChunk chunk = chunks[chunkIndex];
        NativeArray<Entity> droppedEntities = chunk.GetNativeArray(entityType);
        NativeArray<ObjectDataCD> droppedOuterObjects = chunk.GetNativeArray(ref objectDataType);
        NativeArray<LocalTransform> droppedTransforms = chunk.GetNativeArray(ref transformType);
        BufferAccessor<ContainedObjectsBuffer> containedBuffers =
            chunk.GetBufferAccessor(ref containedType);

        for (int i = 0; i < chunk.Count; i++)
        {
          Entity droppedEntity = droppedEntities[i];
          if (droppedOuterObjects[i].objectID != ObjectID.DroppedItem)
          {
            continue;
          }

          if (EntityManager.HasComponent<ConveyorTunnelPayloadCD>(droppedEntity))
          {
            continue;
          }

          DynamicBuffer<ContainedObjectsBuffer> contained = containedBuffers[i];
          if (contained.Length == 0 || contained[0].objectData.amount <= 0)
          {
            continue;
          }

          if (!_seenDroppedItemsThisIntake.Add(droppedEntity))
          {
            continue;
          }

          droppedItemCount++;
          LocalTransform transform = droppedTransforms[i];
          float2 itemPosition = GetDroppedItemLivePosition(
              droppedEntity,
              transform,
              now,
              out string itemPositionSource);
          TrackNearestDroppedItemToEntrance(
              itemPosition,
              itemPositionSource,
              ref nearestDistanceSq,
              ref nearestPosition,
              ref nearestSource,
              ref nearestPairId,
              ref nearestForwardDistance,
              ref nearestLateralDistance,
              ref nearestPairDirection);
          int tileX = Mathf.RoundToInt(itemPosition.x);
          int tileY = Mathf.RoundToInt(itemPosition.y);
          if (_pairIdsByInterestTile.TryGetValue(GetTileKey(tileX, tileY), out List<int> pairIds))
          {
            nearbyCandidateCount++;
            for (int pairIndex = 0; pairIndex < pairIds.Count; pairIndex++)
            {
              int pairId = pairIds[pairIndex];
              if (!_activePairsById.TryGetValue(pairId, out ActiveTunnelPair pair))
              {
                continue;
              }

              if (TryHandleIntakeCandidate(
                      droppedEntity,
                      itemPosition,
                      tileX,
                      tileY,
                      pair,
                      now))
              {
                break;
              }
            }
          }
        }
      }
    }

    if (droppedItemCount == 0 &&
        ConveyorTunnelDebugSettings.EnableTransportLogs &&
        now >= _lastNoDroppedItemLogAt + 2.0d)
    {
      _lastNoDroppedItemLogAt = now;
      LogTransportDiagnostic(
          $"active-pairs-no-dropped-items pairs={_activePairsById.Count} endpoints={_endpoints.Count}",
          now);
    }
    else if (droppedItemCount > 0 &&
             nearbyCandidateCount == 0 &&
             ConveyorTunnelDebugSettings.EnableTransportLogs &&
             now >= _lastNoDroppedItemLogAt + 2.0d)
    {
      _lastNoDroppedItemLogAt = now;
      LogTransportDiagnostic(
          $"active-pairs-no-nearby-dropped-items pairs={_activePairsById.Count} " +
          $"scannedDroppedItems={droppedItemCount} interestTiles={_pairIdsByInterestTile.Count} " +
          $"nearest={nearestPosition} nearestDist={math.sqrt(nearestDistanceSq):0.00} " +
          $"nearestSource={nearestSource} nearestPair={nearestPairId} " +
          $"nearestForward={nearestForwardDistance:0.00} " +
          $"nearestLateral={nearestLateralDistance:0.00} nearestDir={nearestPairDirection}",
          now);
    }

    for (int i = 0; i < _intakeCandidates.Count; i++)
    {
      TryStartPayload(
          _intakeCandidates[i].DroppedEntity,
          _intakeCandidates[i].Pair,
          _intakeCandidates[i].ItemPosition,
          now);
    }
  }

  private float2 GetDroppedItemLivePosition(
      Entity droppedEntity,
      LocalTransform transform,
      double now,
      out string source)
  {
    Entity movee = FindMoveeForDroppedItem(droppedEntity, now);
    if (movee != Entity.Null &&
        EntityManager.Exists(movee) &&
        EntityManager.HasComponent<MoveeCD>(movee))
    {
      source = "movee";
      return EntityManager.GetComponentData<MoveeCD>(movee).position;
    }

    source = "transform";
    return new float2(transform.Position.x, transform.Position.z);
  }

  private void TrackNearestDroppedItemToEntrance(
      float2 itemPosition,
      string source,
      ref float nearestDistanceSq,
      ref float2 nearestPosition,
      ref string nearestSource,
      ref int nearestPairId,
      ref float nearestForwardDistance,
      ref float nearestLateralDistance,
      ref int2 nearestPairDirection)
  {
    foreach (KeyValuePair<int, ActiveTunnelPair> entry in _activePairsById)
    {
      ActiveTunnelPair pair = entry.Value;
      float distanceSq = math.distancesq(itemPosition, pair.IntakePoint);
      if (distanceSq >= nearestDistanceSq)
      {
        continue;
      }

      GetIntakeLaneDistances(
          itemPosition,
          pair,
          out float forwardDistance,
          out float lateralDistance);

      nearestDistanceSq = distanceSq;
      nearestPosition = itemPosition;
      nearestSource = source;
      nearestPairId = pair.PairId;
      nearestForwardDistance = forwardDistance;
      nearestLateralDistance = lateralDistance;
      nearestPairDirection = pair.Direction;
    }
  }

  private bool TryHandleIntakeCandidate(
      Entity droppedEntity,
      float2 itemPosition,
      int tileX,
      int tileY,
      ActiveTunnelPair pair,
      double now)
  {
    if (!CanPairAcceptNewItems(pair) ||
        !AreEndpointsUsable(pair))
    {
      return false;
    }

    if (!TryGetMoveeDataForDroppedItem(droppedEntity, now, out MoveeCD moveeData))
    {
      LogTransportDiagnostic(
          $"candidate-no-movee dropped={droppedEntity} pos={itemPosition} " +
          $"tile=({tileX},{tileY}) entrance={pair.EntranceTile} dir={pair.Direction}",
          now);
      return false;
    }

    if (!IsMoveeTravellingIntoTunnel(moveeData, itemPosition, pair))
    {
      GetIntakeLaneDistances(
          itemPosition,
          pair,
          out float forwardDistance,
          out float lateralDistance);
      LogTransportDiagnostic(
          $"candidate-route-rejected dropped={droppedEntity} pos={itemPosition} " +
          $"target={moveeData.target} tile=({tileX},{tileY}) pair={pair.PairId} " +
          $"forward={forwardDistance:0.00} lateral={lateralDistance:0.00} dir={pair.Direction}",
          now);
      return false;
    }

    if (!ShouldStartPayloadNow(itemPosition, pair))
    {
      return false;
    }

    _intakeCandidates.Add(new IntakeCandidate
    {
      DroppedEntity = droppedEntity,
      Pair = pair,
      ItemPosition = itemPosition
    });
    return true;
  }

  private bool CanPairAcceptNewItems(ActiveTunnelPair pair)
  {
    return pair.AcceptsNewItems ||
           (!HasPayloads(pair.EntranceEndpoint) && !HasPayloads(pair.ExitEndpoint));
  }

  private bool TryStartPayload(
      Entity droppedEntity,
      ActiveTunnelPair pair,
      float2 itemPosition,
      double now)
  {
    if (!EntityManager.Exists(droppedEntity) ||
        EntityManager.HasComponent<ConveyorTunnelPayloadCD>(droppedEntity) ||
        !EntityManager.HasComponent<LocalTransform>(droppedEntity) ||
        !EntityManager.HasBuffer<ContainedObjectsBuffer>(droppedEntity) ||
        !AreEndpointsUsable(pair))
    {
      return false;
    }

    DynamicBuffer<ContainedObjectsBuffer> contained =
        EntityManager.GetBuffer<ContainedObjectsBuffer>(droppedEntity);
    if (contained.Length == 0 || contained[0].objectData.amount <= 0)
    {
      return false;
    }

    LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(droppedEntity);
    Entity movee = FindMoveeForDroppedItem(droppedEntity, now);
    byte moveeHadEnabledComponent = 0;
    byte moveeWasEnabled = 0;

    if (movee != Entity.Null &&
        EntityManager.Exists(movee))
    {
      CaptureMoveeEnabledState(
          movee,
          out moveeHadEnabledComponent,
          out moveeWasEnabled);
    }

    ConveyorTunnelPayloadCD payload = new ConveyorTunnelPayloadCD
    {
      PairId = pair.PairId,
      EntranceEndpoint = pair.EntranceEndpoint,
      ExitEndpoint = pair.ExitEndpoint,
      MoveeEntity = movee,
      ObjectData = contained[0].objectData,
      AuxDataIndex = 0,
      VisualPoint = itemPosition,
      HoldPoint = GetUndergroundHoldPoint(droppedEntity),
      ReleasePoint = pair.ReleasePoint,
      ReleaseTarget = pair.ReleaseTarget,
      OriginalHeight = transform.Position.y,
      VisualTimer = 0.0f,
      TravelTimer = 0.0f,
      TravelDuration = pair.TravelDuration,
      Phase = ConveyorTunnelPayloadPhase.IntakeAnimation,
      MoveeHadEnabledComponent = moveeHadEnabledComponent,
      MoveeWasEnabled = moveeWasEnabled
    };

    EntityManager.AddComponentData(droppedEntity, payload);
    ConveyorTunnelNetworkState.PublishItemVisualEffect(
        ConveyorTunnelItemVisualEffectKind.Intake,
        payload.VisualPoint);
    HoldPayloadAtSurfacePoint(droppedEntity, payload, payload.VisualPoint, false, now);

    MarkEndpointHasPayload(pair.EntranceEndpoint);
    MarkEndpointHasPayload(pair.ExitEndpoint);
    LogTransportDiagnostic(
        $"payload-start dropped={droppedEntity} pair={pair.PairId} entrance={pair.EntranceTile} " +
        $"exit={pair.ExitTile} intake={pair.IntakePoint} visual={payload.VisualPoint} hold={payload.HoldPoint} " +
        $"release={pair.ReleasePoint}",
        now);
    return true;
  }

  private void ProgressPayloads(float deltaTime, double now)
  {
    NativeArray<Entity> payloadEntities = _payloadQuery.ToEntityArray(Allocator.Temp);
    try
    {
      for (int i = 0; i < payloadEntities.Length; i++)
      {
        Entity entity = payloadEntities[i];
        if (!EntityManager.Exists(entity) ||
            !EntityManager.HasComponent<ConveyorTunnelPayloadCD>(entity))
        {
          continue;
        }

        ConveyorTunnelPayloadCD payload =
            EntityManager.GetComponentData<ConveyorTunnelPayloadCD>(entity);

        bool endpointMissing =
            !IsEndpointUsable(payload.EntranceEndpoint) ||
            !IsEndpointUsable(payload.ExitEndpoint);

        if (endpointMissing)
        {
          ReleasePayload(entity, payload, now);
          continue;
        }

        switch (payload.Phase)
        {
          case ConveyorTunnelPayloadPhase.IntakeAnimation:
            payload.VisualTimer += deltaTime;
            if (payload.VisualTimer >= ConveyorTunnelItemVisualEffects.IntakeTakeDuration)
            {
              BeginUndergroundTravel(entity, ref payload, now);
            }
            else
            {
              HoldPayloadAtSurfacePoint(entity, payload, payload.VisualPoint, false, now);
            }

            break;
          case ConveyorTunnelPayloadPhase.ExitAnimation:
            payload.VisualTimer += deltaTime;
            if (payload.VisualTimer >= ConveyorTunnelItemVisualEffects.ExitDuration)
            {
              ReleasePayload(entity, payload, now);
              continue;
            }

            HoldPayloadAtReleasePoint(entity, payload, now);
            break;
          default:
            payload.TravelTimer += deltaTime;
            if (payload.TravelTimer >= payload.TravelDuration)
            {
              BeginExitAnimation(entity, ref payload, now);
            }
            else
            {
              HoldPayloadUnderground(entity, payload, now);
            }
            break;
        }

        EntityManager.SetComponentData(entity, payload);
      }
    }
    finally
    {
      payloadEntities.Dispose();
    }
  }

  private void BeginUndergroundTravel(
      Entity droppedEntity,
      ref ConveyorTunnelPayloadCD payload,
      double now)
  {
    payload.Phase = ConveyorTunnelPayloadPhase.UndergroundTravel;
    payload.VisualTimer = 0.0f;
    payload.TravelTimer = 0.0f;
    HoldPayloadUnderground(droppedEntity, payload, now);
  }

  private void BeginExitAnimation(
      Entity droppedEntity,
      ref ConveyorTunnelPayloadCD payload,
      double now)
  {
    payload.Phase = ConveyorTunnelPayloadPhase.ExitAnimation;
    payload.VisualTimer = 0.0f;
    ConveyorTunnelNetworkState.PublishItemVisualEffect(
        ConveyorTunnelItemVisualEffectKind.Exit,
        payload.ReleasePoint);
    HoldPayloadAtReleasePoint(droppedEntity, payload, now);
  }

  private void HoldPayloadAtReleasePoint(
      Entity droppedEntity,
      ConveyorTunnelPayloadCD payload,
      double now)
  {
    HoldPayloadAtSurfacePoint(droppedEntity, payload, payload.ReleasePoint, false, now);
  }

  private void HoldPayloadAtSurfacePoint(
      Entity droppedEntity,
      ConveyorTunnelPayloadCD payload,
      float2 point,
      bool keepMoveeDisabled,
      double now)
  {
    if (!EntityManager.Exists(droppedEntity))
    {
      return;
    }

    if (EntityManager.HasComponent<LocalTransform>(droppedEntity))
    {
      LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(droppedEntity);
      float height = payload.OriginalHeight < -10.0f ? 0.0f : payload.OriginalHeight;
      transform.Position = new float3(point.x, height, point.y);
      EntityManager.SetComponentData(droppedEntity, transform);
    }

    SetDroppedBigEntityAtRest(droppedEntity, point);
    Entity movee = ResolvePayloadMovee(droppedEntity, payload, now);
    if (movee != Entity.Null &&
      EntityManager.Exists(movee) &&
      EntityManager.HasComponent<MoveeCD>(movee))
    {
      PlaceMoveeAtRest(movee, point);
      if (keepMoveeDisabled)
      {
        DisableMoveeWhileUnderground(movee);
      }
      else
      {
        RestoreMoveeEnabledState(movee, payload);
      }
    }
  }

  private void HoldPayloadUnderground(
      Entity droppedEntity,
      ConveyorTunnelPayloadCD payload,
      double now)
  {
    if (!EntityManager.Exists(droppedEntity))
    {
      return;
    }

    if (EntityManager.HasComponent<LocalTransform>(droppedEntity))
    {
      LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(droppedEntity);
      transform.Position = new float3(payload.HoldPoint.x, UndergroundHeight, payload.HoldPoint.y);
      EntityManager.SetComponentData(droppedEntity, transform);
    }

    SetDroppedBigEntityRoute(droppedEntity, payload.HoldPoint, RouteMoveTime);
    Entity movee = ResolvePayloadMovee(droppedEntity, payload, now);
    if (movee != Entity.Null &&
        EntityManager.Exists(movee) &&
        EntityManager.HasComponent<MoveeCD>(movee))
    {
      PlaceMoveeAtTarget(movee, payload.HoldPoint, payload.HoldPoint, RouteMoveTime);
      DisableMoveeWhileUnderground(movee);
    }
  }

  private void ReleasePayload(Entity droppedEntity, ConveyorTunnelPayloadCD payload, double now)
  {
    if (!EntityManager.Exists(droppedEntity))
    {
      return;
    }

    if (EntityManager.HasComponent<LocalTransform>(droppedEntity))
    {
      LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(droppedEntity);
      float height = payload.OriginalHeight < -10.0f ? 0.0f : payload.OriginalHeight;
      transform.Position = new float3(payload.ReleasePoint.x, height, payload.ReleasePoint.y);
      EntityManager.SetComponentData(droppedEntity, transform);
    }

    if (EntityManager.HasComponent<ConveyorTunnelPayloadCD>(droppedEntity))
    {
      EntityManager.RemoveComponent<ConveyorTunnelPayloadCD>(droppedEntity);
    }

    SetDroppedBigEntityAtRest(droppedEntity, payload.ReleasePoint);
    Entity movee = ResolvePayloadMovee(droppedEntity, payload, now);
    if (movee != Entity.Null &&
        EntityManager.Exists(movee) &&
        EntityManager.HasComponent<MoveeCD>(movee))
    {
      PlaceMoveeAtRest(movee, payload.ReleasePoint);
      RestoreMoveeEnabledState(movee, payload);
    }

    LogTransportDiagnostic(
        $"payload-release dropped={droppedEntity} pair={payload.PairId} " +
        $"release={payload.ReleasePoint}",
        now);
  }

  private static float2 GetUndergroundHoldPoint(Entity droppedEntity)
  {
    // Vanilla dropped-item merge buckets use rounded MoveeCD X/Z positions, ignoring height.
    // Keep underground payloads on unique off-board tiles so they cannot merge or block the mouth.
    uint slot = unchecked((uint)droppedEntity.Index);
    float x = UndergroundHoldBaseCoordinate - (slot & 0xFFFFu) * UndergroundHoldSpacing;
    float y = UndergroundHoldBaseCoordinate - ((slot >> 16) & 0xFFFFu) * UndergroundHoldSpacing;
    return new float2(x, y);
  }

  private void MarkEndpointHasPayload(Entity endpoint)
  {
    if (endpoint == Entity.Null ||
        !EntityManager.Exists(endpoint) ||
        !EntityManager.HasComponent<ConveyorTunnelEndpointStateCD>(endpoint))
    {
      return;
    }

    ConveyorTunnelEndpointStateCD state =
        EntityManager.GetComponentData<ConveyorTunnelEndpointStateCD>(endpoint);
    state.HasPayloads = 1;
    EntityManager.SetComponentData(endpoint, state);
  }

  private bool AreEndpointsUsable(ActiveTunnelPair pair)
  {
    return IsEndpointUsable(pair.EntranceEndpoint) && IsEndpointUsable(pair.ExitEndpoint);
  }

  private bool IsEndpointUsable(Entity endpoint)
  {
    return endpoint != Entity.Null &&
           EntityManager.Exists(endpoint) &&
           !IsEntityDestroyed(endpoint);
  }

  private bool IsEntityDestroyed(Entity entity)
  {
    return EntityManager.HasComponent<EntityDestroyedCD>(entity) &&
           EntityManager.IsComponentEnabled<EntityDestroyedCD>(entity);
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

  private void SetDroppedBigEntityAtRest(Entity droppedEntity, float2 position)
  {
    MoveeBigEntityCD route = new MoveeBigEntityCD
    {
      target = position,
      moveTimer = -1
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

  private void PlaceMoveeAtTarget(
      Entity moveeEntity,
      float2 position,
      float2 target,
      int moveTime)
  {
    MoveeCD movee = EntityManager.GetComponentData<MoveeCD>(moveeEntity);
    movee.position = position;
    movee.target = target;
    movee.moveTimer = math.max(1, moveTime);
    EntityManager.SetComponentData(moveeEntity, movee);
  }

  private void PlaceMoveeAtRest(Entity moveeEntity, float2 position)
  {
    MoveeCD movee = EntityManager.GetComponentData<MoveeCD>(moveeEntity);
    movee.position = position;
    movee.target = position;
    movee.moveTimer = -1;
    EntityManager.SetComponentData(moveeEntity, movee);
  }

  private bool TryGetMoveeDataForDroppedItem(
      Entity droppedEntity,
      double now,
      out MoveeCD moveeData)
  {
    moveeData = default;
    Entity movee = FindMoveeForDroppedItem(droppedEntity, now);
    if (movee == Entity.Null ||
        !EntityManager.Exists(movee) ||
        !EntityManager.HasComponent<MoveeCD>(movee))
    {
      return false;
    }

    moveeData = EntityManager.GetComponentData<MoveeCD>(movee);
    return true;
  }

  private static bool IsMoveeTravellingIntoTunnel(
      MoveeCD moveeData,
      float2 itemPosition,
      ActiveTunnelPair pair)
  {
    if (moveeData.moveTimer <= 0)
    {
      return false;
    }

    float2 direction = new float2(pair.Direction.x, pair.Direction.y);
    float2 route = moveeData.target - itemPosition;
    if (math.lengthsq(route) < IntakeRouteMinMotionSq)
    {
      return false;
    }

    float forwardMotion = math.dot(route, direction);

    // The entrance accepts items from behind and either side. An item moving
    // against the tunnel direction is approaching from the tunnel-facing side
    // and must continue across the tile instead of being captured.
    return forwardMotion >= -IntakeRouteReverseTolerance;
  }

  private static void GetIntakeLaneDistances(
      float2 itemPosition,
      ActiveTunnelPair pair,
      out float forwardDistance,
      out float lateralDistance)
  {
    float2 center = pair.IntakePoint;
    float2 delta = itemPosition - center;
    float2 direction = new float2(pair.Direction.x, pair.Direction.y);
    forwardDistance = math.dot(delta, direction);
    lateralDistance = math.abs(delta.x * direction.y - delta.y * direction.x);
  }

  private static bool ShouldStartPayloadNow(float2 itemPosition, ActiveTunnelPair pair)
  {
    GetIntakeLaneDistances(
        itemPosition,
        pair,
        out float forwardDistance,
        out float lateralDistance);

    return lateralDistance <= IntakeLaneHalfWidth &&
           forwardDistance >= IntakeCommitForwardDistance &&
           forwardDistance <= IntakeCommitMaxForwardDistance;
  }

  private void LogTransportDiagnostic(string message, double now)
  {
    if (!ConveyorTunnelDebugSettings.EnableTransportLogs ||
        _transportDiagnosticLogs >= MaxTransportDiagnosticLogs)
    {
      return;
    }

    _transportDiagnosticLogs++;
    Debug.Log($"[ConveyorTunnelTransport] t={now:0.00} {message}");
  }

  private void CaptureMoveeEnabledState(
      Entity moveeEntity,
      out byte hadEnabledComponent,
      out byte wasEnabled)
  {
    hadEnabledComponent = 0;
    wasEnabled = 0;

    if (!EntityManager.HasComponent<BigEntityIsEnabledCD>(moveeEntity))
    {
      return;
    }

    hadEnabledComponent = 1;
    wasEnabled = EntityManager.IsComponentEnabled<BigEntityIsEnabledCD>(moveeEntity)
        ? (byte)1
        : (byte)0;
  }

  private void DisableMoveeWhileUnderground(Entity moveeEntity)
  {
    if (EntityManager.HasComponent<BigEntityIsEnabledCD>(moveeEntity))
    {
      EntityManager.SetComponentEnabled<BigEntityIsEnabledCD>(moveeEntity, false);
    }
  }

  private void RestoreMoveeEnabledState(Entity moveeEntity, ConveyorTunnelPayloadCD payload)
  {
    if (payload.MoveeHadEnabledComponent == 0 ||
        !EntityManager.HasComponent<BigEntityIsEnabledCD>(moveeEntity))
    {
      return;
    }

    EntityManager.SetComponentEnabled<BigEntityIsEnabledCD>(
        moveeEntity,
        payload.MoveeWasEnabled != 0);
  }

  private Entity ResolvePayloadMovee(
      Entity droppedEntity,
      ConveyorTunnelPayloadCD payload,
      double now)
  {
    if (payload.MoveeEntity != Entity.Null &&
        EntityManager.Exists(payload.MoveeEntity))
    {
      return payload.MoveeEntity;
    }

    return FindMoveeForDroppedItem(droppedEntity, now);
  }

  private Entity FindMoveeForDroppedItem(Entity droppedEntity, double now)
  {
    if (TryGetCachedMoveeForDroppedItem(droppedEntity, out Entity cachedMovee))
    {
      return cachedMovee;
    }

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
          _moveeByDroppedEntity[droppedEntity] = candidate;
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

  private bool TryGetCachedMoveeForDroppedItem(Entity droppedEntity, out Entity movee)
  {
    movee = Entity.Null;

    if (!_moveeByDroppedEntity.TryGetValue(droppedEntity, out Entity cachedMovee))
    {
      return false;
    }

    if (EntityManager.Exists(cachedMovee) &&
        EntityManager.HasComponent<MoveeCD>(cachedMovee) &&
        (!EntityManager.HasComponent<BigEntityRefCD>(cachedMovee) ||
         EntityManager.GetComponentData<BigEntityRefCD>(cachedMovee).Value == droppedEntity))
    {
      movee = cachedMovee;
      return true;
    }

    _moveeByDroppedEntity.Remove(droppedEntity);
    return false;
  }

  private void RebuildMoveeLookup(double now)
  {
    _moveeByDroppedEntity.Clear();

    EntityTypeHandle entityType = GetEntityTypeHandle();
    ComponentTypeHandle<BigEntityRefCD> bigEntityRefType = GetComponentTypeHandle<BigEntityRefCD>(true);

    using NativeArray<ArchetypeChunk> chunks = _moveeQuery.ToArchetypeChunkArray(Allocator.Temp);
    for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
    {
      ArchetypeChunk chunk = chunks[chunkIndex];
      NativeArray<Entity> moveeEntities = chunk.GetNativeArray(entityType);
      NativeArray<BigEntityRefCD> moveeRefs = chunk.GetNativeArray(ref bigEntityRefType);

      for (int i = 0; i < chunk.Count; i++)
      {
        Entity droppedEntity = moveeRefs[i].Value;
        if (droppedEntity != Entity.Null && !_moveeByDroppedEntity.ContainsKey(droppedEntity))
        {
          _moveeByDroppedEntity.Add(droppedEntity, moveeEntities[i]);
        }
      }
    }

    _moveeLookupBuiltAt = now;
  }

  private void SetState(EndpointSnapshot endpoint, ConveyorTunnelEndpointStateCD state)
  {
    _statesByEndpoint[endpoint.Entity] = state;

    if (EntityManager.HasComponent<ConveyorTunnelEndpointStateCD>(endpoint.Entity))
    {
      EntityManager.SetComponentData(endpoint.Entity, state);
    }
    else
    {
      EntityManager.AddComponentData(endpoint.Entity, state);
    }

    SetTunnelMoverEnabled(endpoint.Entity, state.Linked != 0);
    BroadcastVisualStateIfChanged(state);
  }

  private void SetTunnelMoverEnabled(Entity endpointEntity, bool enabled)
  {
    if (!EntityManager.Exists(endpointEntity))
    {
      return;
    }

    SetMoverEntityEnabled(endpointEntity, enabled);
    SetMoverBufferEnabled(endpointEntity, enabled);

    if (!EntityManager.HasBuffer<SmallEntityRefBuffer>(endpointEntity))
    {
      return;
    }

    DynamicBuffer<SmallEntityRefBuffer> smallRefs =
        EntityManager.GetBuffer<SmallEntityRefBuffer>(endpointEntity);
    for (int i = 0; i < smallRefs.Length; i++)
    {
      Entity smallEntity = smallRefs[i].Value;
      if (!EntityManager.Exists(smallEntity))
      {
        continue;
      }

      SetMoverEntityEnabled(smallEntity, enabled);
      SetMoverBufferEnabled(smallEntity, enabled);
    }
  }

  private void SetMoverEntityEnabled(Entity moverEntity, bool enabled)
  {
    if (!EntityManager.HasComponent<EnabledMoverFromSharedStateCD>(moverEntity))
    {
      return;
    }

    EntityManager.SetComponentEnabled<EnabledMoverFromSharedStateCD>(moverEntity, enabled);
  }

  private void SetMoverBufferEnabled(Entity entity, bool enabled)
  {
    if (!EntityManager.HasBuffer<MoversWithSharedStateBuffer>(entity))
    {
      return;
    }

    DynamicBuffer<MoversWithSharedStateBuffer> movers =
        EntityManager.GetBuffer<MoversWithSharedStateBuffer>(entity);
    for (int i = 0; i < movers.Length; i++)
    {
      Entity moverEntity = movers[i].moverEntity;
      if (!EntityManager.Exists(moverEntity))
      {
        continue;
      }

      SetMoverEntityEnabled(moverEntity, enabled);
    }
  }

  private static ConveyorTunnelEndpointStateCD CreateUnlinkedEndpointState(
      EndpointSnapshot endpoint,
      bool hasPayloads,
      bool pending)
  {
    return new ConveyorTunnelEndpointStateCD
    {
      PairId = 0,
      Role = ConveyorTunnelEndpointRole.Entrance,
      Linked = 0,
      Pending = pending ? (byte)1 : (byte)0,
      HasPayloads = hasPayloads ? (byte)1 : (byte)0,
      Tile = endpoint.Tile,
      Direction = endpoint.Direction,
      PairedTile = endpoint.Tile,
      EntranceHandoffPoint = ConveyorTunnelDirectionUtility.GetEntranceHandoffPoint(endpoint.Tile, endpoint.Direction),
      ExitHandoffPoint = ConveyorTunnelDirectionUtility.GetExitHandoffPoint(endpoint.Tile, endpoint.Direction),
      PairedEndpoint = Entity.Null
    };
  }

  private static ConveyorTunnelEndpointStateCD CreateLinkedState(
      int pairId,
      ConveyorTunnelEndpointRole role,
      EndpointSnapshot endpoint,
      EndpointSnapshot pairedEndpoint,
      bool hasPayloads)
  {
    return new ConveyorTunnelEndpointStateCD
    {
      PairId = pairId,
      Role = role,
      Linked = 1,
      Pending = 0,
      HasPayloads = hasPayloads ? (byte)1 : (byte)0,
      Tile = endpoint.Tile,
      Direction = endpoint.Direction,
      PairedTile = pairedEndpoint.Tile,
      EntranceHandoffPoint = ConveyorTunnelDirectionUtility.GetEntranceHandoffPoint(endpoint.Tile, endpoint.Direction),
      ExitHandoffPoint = ConveyorTunnelDirectionUtility.GetExitHandoffPoint(endpoint.Tile, endpoint.Direction),
      PairedEndpoint = pairedEndpoint.Entity
    };
  }

  private static ActiveTunnelPair CreateActivePair(
      int pairId,
      EndpointSnapshot entrance,
      EndpointSnapshot exit,
      bool acceptsNewItems = true)
  {
    float2 intakePoint =
        ConveyorTunnelDirectionUtility.GetEntranceHandoffPoint(
            entrance.Tile,
            entrance.Direction);
    float2 releasePoint =
        ConveyorTunnelDirectionUtility.GetExitHandoffPoint(exit.Tile, entrance.Direction);
    float undergroundDistance = math.max(
        0.1f,
        math.distance(intakePoint, releasePoint));
    float travelDuration = math.max(
        MinTravelDuration,
        undergroundDistance * TravelSecondsPerTile);

    return new ActiveTunnelPair
    {
      PairId = pairId,
      EntranceEndpoint = entrance.Entity,
      ExitEndpoint = exit.Entity,
      EntranceTile = entrance.Tile,
      ExitTile = exit.Tile,
      Direction = entrance.Direction,
      IntakePoint = intakePoint,
      ReleasePoint = releasePoint,
      ReleaseTarget = new float2(
          exit.Tile.x + entrance.Direction.x * 0.9f,
          exit.Tile.y + entrance.Direction.y * 0.9f),
      TravelDuration = travelDuration,
      AcceptsNewItems = acceptsNewItems
    };
  }

  private void BroadcastVisualStateIfChanged(ConveyorTunnelEndpointStateCD state)
  {
    long key = GetTileKey(state.Tile.x, state.Tile.y);
    _seenEndpointVisualTiles.Add(key);

    int signature = GetVisualStateSignature(state);
    if (_lastVisualStateSignatureByTile.TryGetValue(key, out int previousSignature) &&
        previousSignature == signature &&
        !_forceVisualStateBroadcastThisRefresh)
    {
      return;
    }

    _lastVisualStateSignatureByTile[key] = signature;
    BroadcastVisualState(
        state.Tile,
        new ConveyorTunnelEndpointVisualState
        {
          PairId = state.PairId,
          Role = state.Role,
          Linked = state.Linked,
          Pending = state.Pending,
          HasPayloads = state.HasPayloads,
          Direction = state.Direction,
          PairedTile = state.PairedTile
        });
  }

  private void ClearMissingEndpointVisualStates()
  {
    if (_lastVisualStateSignatureByTile.Count == 0)
    {
      return;
    }

    _visualTilesToClear.Clear();
    foreach (long key in _lastVisualStateSignatureByTile.Keys)
    {
      if (!_seenEndpointVisualTiles.Contains(key))
      {
        _visualTilesToClear.Add(key);
      }
    }

    for (int i = 0; i < _visualTilesToClear.Count; i++)
    {
      long key = _visualTilesToClear[i];
      _lastVisualStateSignatureByTile.Remove(key);
      int2 tile = TileFromKey(key);
      BroadcastVisualState(
          tile,
          new ConveyorTunnelEndpointVisualState
          {
            PairId = 0,
            Role = ConveyorTunnelEndpointRole.None,
            Linked = 0,
            Pending = 0,
            HasPayloads = 0,
            Direction = int2.zero,
            PairedTile = tile
          });
    }
  }

  private static void BroadcastVisualState(int2 tile, ConveyorTunnelEndpointVisualState state)
  {
    ConveyorTunnelNetworkState.RememberAuthoritativeEndpointState(tile, state);
  }

  private static int GetVisualStateSignature(ConveyorTunnelEndpointStateCD state)
  {
    unchecked
    {
      int hash = 17;
      hash = hash * 31 + state.PairId;
      hash = hash * 31 + (int)state.Role;
      hash = hash * 31 + state.Linked;
      hash = hash * 31 + state.Pending;
      hash = hash * 31 + state.HasPayloads;
      hash = hash * 31 + state.Direction.x;
      hash = hash * 31 + state.Direction.y;
      hash = hash * 31 + state.PairedTile.x;
      hash = hash * 31 + state.PairedTile.y;
      return hash;
    }
  }

  private static int CompareEndpoints(EndpointSnapshot a, EndpointSnapshot b)
  {
    int y = a.Tile.y.CompareTo(b.Tile.y);
    if (y != 0)
    {
      return y;
    }

    int x = a.Tile.x.CompareTo(b.Tile.x);
    if (x != 0)
    {
      return x;
    }

    int variation = a.Variation.CompareTo(b.Variation);
    if (variation != 0)
    {
      return variation;
    }

    return a.Entity.Index.CompareTo(b.Entity.Index);
  }

  private static int2 WorldToTile(float3 position)
  {
    return new int2((int)Math.Round(position.x), (int)Math.Round(position.z));
  }

  private static long GetTileKey(int x, int y)
  {
    return ((long)x << 32) ^ (uint)y;
  }

  private static int2 TileFromKey(long key)
  {
    return new int2((int)(key >> 32), unchecked((int)(uint)key));
  }
}
