using System.Collections.Generic;
using Pug.ECS.Components;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ChunkLoaderRuntimeSystem : SystemBase
{
  private readonly struct MergedSimulationRegion
  {
    public MergedSimulationRegion(int x, int y, int width, int height)
    {
      X = x;
      Y = y;
      Width = width;
      Height = height;
    }

    public readonly int X;
    public readonly int Y;
    public readonly int Width;
    public readonly int Height;

    public long Key
    {
      get
      {
        unchecked
        {
          long hash = 1469598103934665603L;
          hash = (hash ^ X) * 1099511628211L;
          hash = (hash ^ Y) * 1099511628211L;
          hash = (hash ^ Width) * 1099511628211L;
          hash = (hash ^ Height) * 1099511628211L;
          return hash;
        }
      }
    }

    public int2 Low =>
        new int2(
            X * ChunkLoaderConstants.ChunkSize,
            Y * ChunkLoaderConstants.ChunkSize);

    public int2 Size =>
        new int2(
            Width * ChunkLoaderConstants.ChunkSize,
            Height * ChunkLoaderConstants.ChunkSize);
  }

  private readonly List<ChunkLoaderRegistrationRecord> _records = new();
  private readonly Dictionary<ulong, Entity> _anchors = new();
  private readonly Dictionary<long, Entity> _simulationRegionAnchors = new();
  private readonly HashSet<ulong> _desiredAnchorIds = new();
  private readonly HashSet<long> _desiredSimulationRegionKeys = new();
  private readonly HashSet<long> _loadedSubMaps = new();
  private readonly HashSet<long> _mergeCells = new();
  private readonly List<ChunkCoordinate> _activeSimulationCoordinates = new();
  private readonly List<MergedSimulationRegion> _mergedSimulationRegions = new();
  private readonly List<ulong> _staleAnchorIds = new();
  private readonly List<long> _staleSimulationRegionKeys = new();

  private EntityQuery _anchorQuery;
  private EntityQuery _mergedRegionQuery;
  private EntityQuery _subMapQuery;
  private double _nextReconcileAt;

  protected override void OnCreate()
  {
    _anchorQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderRuntimeAnchorCD>());
    _mergedRegionQuery = GetEntityQuery(
        ComponentType.ReadOnly<ChunkLoaderMergedSimulationRegionCD>());
    _subMapQuery = GetEntityQuery(
        ComponentType.ReadOnly<SubMapCD>());
    _nextReconcileAt = 0;
    ChunkLoaderRegistry.RecordChanged += OnRegistryChanged;
    ChunkLoaderRegistry.RecordDeleted += OnRegistryDeleted;
  }

  protected override void OnDestroy()
  {
    ChunkLoaderRegistry.RecordChanged -= OnRegistryChanged;
    ChunkLoaderRegistry.RecordDeleted -= OnRegistryDeleted;
    DestroyAllAnchors();
    ChunkLoaderSimulationRegions.Reset();
  }

  protected override void OnUpdate()
  {
    double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
    if (now < _nextReconcileAt)
    {
      return;
    }

    _nextReconcileAt = now + ChunkLoaderConstants.RuntimeReconcileIntervalSeconds;
    ChunkLoaderRegistry.EnsureLoadedForCurrentWorld();
    if (!ChunkLoaderRegistry.IsLoaded)
    {
      return;
    }

    if (!ChunkLoaderCompatibility.ValidateCore(World))
    {
      MarkEnabledRecordsUnavailable();
      DestroyAllAnchors();
      return;
    }

    RebuildAnchorLookup();
    RebuildMergedRegionLookup();
    RebuildLoadedSubMapLookup();
    ReconcileRegistrations(now);
    ReconcileMergedSimulationRegions();
    RemoveStaleAnchors();
    RemoveStaleMergedSimulationRegions();
  }

  private void ReconcileRegistrations(double now)
  {
    ChunkLoaderRegistry.GetRecords(_records);
    _desiredAnchorIds.Clear();
    _activeSimulationCoordinates.Clear();

    for (int i = 0; i < _records.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _records[i];
      if (!record.desiredEnabled)
      {
        ChunkLoaderSimulationRegions.SetInactive(record.registrationId);
        if (_anchors.TryGetValue(record.registrationId, out Entity disabledAnchor))
        {
          DestroyAnchor(record.registrationId, disabledAnchor);
        }

        ChunkLoaderRegistry.SetRuntimeState(
            record.registrationId,
            ChunkLoaderRuntimeState.Disabled);
        continue;
      }

      _desiredAnchorIds.Add(record.registrationId);
      ChunkCoordinate coordinate = record.Coordinate;
      ChunkLoaderSimulationRegions.SetActive(record.registrationId, coordinate);
      _activeSimulationCoordinates.Add(coordinate);

      if (!_anchors.TryGetValue(record.registrationId, out Entity anchor) ||
          !EntityManager.Exists(anchor))
      {
        anchor = CreateAnchor(record, now);
        if (anchor == Entity.Null)
        {
          ChunkLoaderRegistry.SetRuntimeState(
              record.registrationId,
              ChunkLoaderRuntimeState.Error,
              ChunkLoaderErrorCode.RuntimeAnchorFailed,
              "The server could not create the chunk simulation anchor.");
          continue;
        }

        _anchors[record.registrationId] = anchor;
        ChunkLoaderRegistry.SetRuntimeState(
            record.registrationId,
            ChunkLoaderRuntimeState.Loading);
      }

      UpdateAnchorState(record, anchor, now);
    }
  }

  private Entity CreateAnchor(
      ChunkLoaderRegistrationRecord record,
      double now)
  {
    try
    {
      ChunkCoordinate coordinate = record.Coordinate;
      int2 center = coordinate.Center;

      Entity anchor = EntityManager.CreateEntity(
          typeof(ChunkLoaderRuntimeAnchorCD),
          typeof(ChunkLoaderSimulationRegionCD),
          typeof(LocalTransform),
          typeof(KeepAreaLoadedCD),
          typeof(DontDisableCD),
          typeof(DontSerializeCD));

      EntityManager.SetComponentData(anchor, new ChunkLoaderRuntimeAnchorCD
      {
        RegistrationId = record.registrationId,
        ChunkX = record.chunkX,
        ChunkY = record.chunkY,
        CreatedAt = now,
        SubMapObservedAt = 0,
        ImmediateLoadEnabled = 1
      });
      EntityManager.SetComponentData(anchor, new ChunkLoaderSimulationRegionCD
      {
        RegistrationId = record.registrationId,
        ChunkX = record.chunkX,
        ChunkY = record.chunkY
      });
      EntityManager.SetComponentData(
          anchor,
          LocalTransform.FromPosition(new float3(center.x, 0.0f, center.y)));
      EntityManager.SetComponentData(anchor, new KeepAreaLoadedCD
      {
        KeepLoadedRadius = ChunkLoaderConstants.ResidencyRadius,
        StartLoadRadius = ChunkLoaderConstants.ResidencyRadius,
        ImmediateLoadRadius = ChunkLoaderConstants.ResidencyRadius
      });

      return anchor;
    }
    catch (System.Exception ex)
    {
      Debug.LogError(
          $"[ChunkLoaderMod] Failed to create runtime anchor registration={record.registrationId}. {ex}");
      return Entity.Null;
    }
  }

  private void UpdateAnchorState(
      ChunkLoaderRegistrationRecord record,
      Entity anchor,
      double now)
  {
    if (!EntityManager.Exists(anchor) ||
        !EntityManager.HasComponent<ChunkLoaderRuntimeAnchorCD>(anchor))
    {
      return;
    }

    ChunkLoaderRuntimeAnchorCD state =
        EntityManager.GetComponentData<ChunkLoaderRuntimeAnchorCD>(anchor);
    bool subMapPresent =
        _loadedSubMaps.Contains(record.Coordinate.ParentSubMapKey);

    if (subMapPresent)
    {
      if (state.SubMapObservedAt <= 0)
      {
        state.SubMapObservedAt = now;
        EntityManager.SetComponentData(anchor, state);
      }

      if (now - state.SubMapObservedAt >=
          ChunkLoaderConstants.RuntimeLoadedStabilizationSeconds)
      {
        DisableImmediateLoading(anchor, ref state);
        ChunkLoaderRegistry.SetRuntimeState(
            record.registrationId,
            ChunkLoaderRuntimeState.Loaded);
      }
      else
      {
        ChunkLoaderRegistry.SetRuntimeState(
            record.registrationId,
            ChunkLoaderRuntimeState.Loading);
      }

      return;
    }

    if (state.SubMapObservedAt > 0)
    {
      state.SubMapObservedAt = 0;
      EnableImmediateLoading(anchor, ref state);
      EntityManager.SetComponentData(anchor, state);
    }

    double elapsed = now - state.CreatedAt;
    if (elapsed >= ChunkLoaderConstants.RuntimeLoadTimeoutSeconds)
    {
      ChunkLoaderRegistry.SetRuntimeState(
          record.registrationId,
          ChunkLoaderRuntimeState.Error,
          ChunkLoaderErrorCode.ChunkNotGenerated,
          $"No generated parent {ChunkLoaderConstants.ParentSubMapSize}x" +
          $"{ChunkLoaderConstants.ParentSubMapSize} world submap was found " +
          "for this 16x16 chunk.");
    }
    else
    {
      ChunkLoaderRegistry.SetRuntimeState(
          record.registrationId,
          ChunkLoaderRuntimeState.Loading);
    }
  }

  private void DisableImmediateLoading(
      Entity anchor,
      ref ChunkLoaderRuntimeAnchorCD state)
  {
    if (state.ImmediateLoadEnabled == 0)
    {
      return;
    }

    KeepAreaLoadedCD residency =
        EntityManager.GetComponentData<KeepAreaLoadedCD>(anchor);
    residency.ImmediateLoadRadius = 0.0f;
    EntityManager.SetComponentData(anchor, residency);
    state.ImmediateLoadEnabled = 0;
    EntityManager.SetComponentData(anchor, state);
  }

  private void EnableImmediateLoading(
      Entity anchor,
      ref ChunkLoaderRuntimeAnchorCD state)
  {
    if (state.ImmediateLoadEnabled != 0)
    {
      return;
    }

    KeepAreaLoadedCD residency =
        EntityManager.GetComponentData<KeepAreaLoadedCD>(anchor);
    residency.ImmediateLoadRadius = ChunkLoaderConstants.ResidencyRadius;
    EntityManager.SetComponentData(anchor, residency);
    state.ImmediateLoadEnabled = 1;
  }

  private void RebuildAnchorLookup()
  {
    _anchors.Clear();
    using NativeArray<Entity> entities =
        _anchorQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderRuntimeAnchorCD> anchors =
        _anchorQuery.ToComponentDataArray<ChunkLoaderRuntimeAnchorCD>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      // Older builds incorrectly marked the persistent runtime anchor as a
      // save blocker. Core Keeper will not begin serialization while any
      // BlockSaveCD exists, so repair legacy anchors before reconciling them.
      if (EntityManager.HasComponent<BlockSaveCD>(entities[i]))
      {
        EntityManager.RemoveComponent<BlockSaveCD>(entities[i]);
      }
      if (!EntityManager.HasComponent<DontSerializeCD>(entities[i]))
      {
        EntityManager.AddComponent<DontSerializeCD>(entities[i]);
      }

      ulong registrationId = anchors[i].RegistrationId;
      if (registrationId == 0 ||
          _anchors.ContainsKey(registrationId))
      {
        EntityManager.DestroyEntity(entities[i]);
        continue;
      }

      _anchors.Add(registrationId, entities[i]);
    }
  }

  private void RebuildMergedRegionLookup()
  {
    _simulationRegionAnchors.Clear();
    using NativeArray<Entity> entities =
        _mergedRegionQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ChunkLoaderMergedSimulationRegionCD> regions =
        _mergedRegionQuery.ToComponentDataArray<ChunkLoaderMergedSimulationRegionCD>(
            Allocator.Temp);

    for (int i = 0; i < entities.Length; i++)
    {
      ChunkLoaderMergedSimulationRegionCD region = regions[i];
      MergedSimulationRegion keyRegion =
          new MergedSimulationRegion(
              region.LowX / ChunkLoaderConstants.ChunkSize,
              region.LowY / ChunkLoaderConstants.ChunkSize,
              region.SizeX / ChunkLoaderConstants.ChunkSize,
              region.SizeY / ChunkLoaderConstants.ChunkSize);
      long key = keyRegion.Key;
      if (_simulationRegionAnchors.ContainsKey(key))
      {
        EntityManager.DestroyEntity(entities[i]);
        continue;
      }

      _simulationRegionAnchors.Add(key, entities[i]);
    }
  }

  private void RebuildLoadedSubMapLookup()
  {
    _loadedSubMaps.Clear();
    using NativeArray<SubMapCD> subMaps =
        _subMapQuery.ToComponentDataArray<SubMapCD>(Allocator.Temp);

    for (int i = 0; i < subMaps.Length; i++)
    {
      int2 index = subMaps[i].index;
      _loadedSubMaps.Add(((long)index.x << 32) ^ (uint)index.y);
    }
  }

  private void RemoveStaleAnchors()
  {
    if (_anchors.Count == 0)
    {
      return;
    }

    _staleAnchorIds.Clear();
    foreach (KeyValuePair<ulong, Entity> entry in _anchors)
    {
      if (!_desiredAnchorIds.Contains(entry.Key))
      {
        _staleAnchorIds.Add(entry.Key);
      }
    }

    for (int i = 0; i < _staleAnchorIds.Count; i++)
    {
      ulong registrationId = _staleAnchorIds[i];
      DestroyAnchor(registrationId, _anchors[registrationId]);
    }
    _staleAnchorIds.Clear();
  }

  private void ReconcileMergedSimulationRegions()
  {
    BuildMergedSimulationRegions();
    _desiredSimulationRegionKeys.Clear();
    for (int i = 0; i < _mergedSimulationRegions.Count; i++)
    {
      MergedSimulationRegion region = _mergedSimulationRegions[i];
      long key = region.Key;
      _desiredSimulationRegionKeys.Add(key);
      if (_simulationRegionAnchors.TryGetValue(key, out Entity existing) &&
          EntityManager.Exists(existing))
      {
        continue;
      }

      Entity entity = CreateMergedSimulationRegion(region);
      if (entity != Entity.Null)
      {
        _simulationRegionAnchors[key] = entity;
      }
    }
  }

  private void BuildMergedSimulationRegions()
  {
    _mergedSimulationRegions.Clear();
    _mergeCells.Clear();
    if (_activeSimulationCoordinates.Count == 0)
    {
      return;
    }

    for (int i = 0; i < _activeSimulationCoordinates.Count; i++)
    {
      ChunkCoordinate coordinate = _activeSimulationCoordinates[i];
      _mergeCells.Add(coordinate.ToKey());
    }

    while (_mergeCells.Count > 0)
    {
      ChunkCoordinate start = default;
      foreach (long key in _mergeCells)
      {
        start = new ChunkCoordinate(
            (int)(key >> 32),
            unchecked((int)(uint)key));
        break;
      }

      int width = 1;
      while (_mergeCells.Contains(new ChunkCoordinate(start.X + width, start.Y).ToKey()))
      {
        width++;
      }

      int height = 1;
      bool canGrow = true;
      while (canGrow)
      {
        int y = start.Y + height;
        for (int x = 0; x < width; x++)
        {
          if (!_mergeCells.Contains(new ChunkCoordinate(start.X + x, y).ToKey()))
          {
            canGrow = false;
            break;
          }
        }
        if (canGrow)
        {
          height++;
        }
      }

      for (int y = 0; y < height; y++)
      {
        for (int x = 0; x < width; x++)
        {
          _mergeCells.Remove(new ChunkCoordinate(start.X + x, start.Y + y).ToKey());
        }
      }

      _mergedSimulationRegions.Add(
          new MergedSimulationRegion(start.X, start.Y, width, height));
    }
  }

  private Entity CreateMergedSimulationRegion(MergedSimulationRegion region)
  {
    try
    {
      int2 low = region.Low;
      int2 size = region.Size;
      int2 center = low + size / 2;
      Entity entity = EntityManager.CreateEntity(
          typeof(ChunkLoaderMergedSimulationRegionCD),
          typeof(LocalTransform),
          typeof(EnableEntitiesInBoxCD),
          typeof(DontDisableCD),
          typeof(DontSerializeCD));
      EntityManager.SetComponentData(entity, new ChunkLoaderMergedSimulationRegionCD
      {
        LowX = low.x,
        LowY = low.y,
        SizeX = size.x,
        SizeY = size.y
      });
      EntityManager.SetComponentData(
          entity,
          LocalTransform.FromPosition(new float3(center.x, 0.0f, center.y)));
      EntityManager.SetComponentData(entity, new EnableEntitiesInBoxCD
      {
        Area = PugGeometry.AxisAlignedBoundingBox.FromLowerCornerAndSize(
            new float2(low.x, low.y),
            new float2(size.x, size.y))
      });
      return entity;
    }
    catch (System.Exception ex)
    {
      Debug.LogError(
          $"[ChunkLoaderMod] Failed to create merged simulation region. {ex}");
      return Entity.Null;
    }
  }

  private void RemoveStaleMergedSimulationRegions()
  {
    if (_simulationRegionAnchors.Count == 0)
    {
      return;
    }

    _staleSimulationRegionKeys.Clear();
    foreach (KeyValuePair<long, Entity> entry in _simulationRegionAnchors)
    {
      if (!_desiredSimulationRegionKeys.Contains(entry.Key))
      {
        _staleSimulationRegionKeys.Add(entry.Key);
      }
    }

    for (int i = 0; i < _staleSimulationRegionKeys.Count; i++)
    {
      long key = _staleSimulationRegionKeys[i];
      Entity entity = _simulationRegionAnchors[key];
      if (entity != Entity.Null && EntityManager.Exists(entity))
      {
        EntityManager.DestroyEntity(entity);
      }
      _simulationRegionAnchors.Remove(key);
    }
    _staleSimulationRegionKeys.Clear();
  }

  private void DestroyAnchor(ulong registrationId, Entity anchor)
  {
    if (anchor != Entity.Null && EntityManager.Exists(anchor))
    {
      EntityManager.DestroyEntity(anchor);
    }

    _anchors.Remove(registrationId);
    ChunkLoaderSimulationRegions.SetInactive(registrationId);
  }

  private void DestroyAllAnchors()
  {
    if (EntityManager.World == null || !EntityManager.World.IsCreated)
    {
      _anchors.Clear();
      _simulationRegionAnchors.Clear();
      return;
    }

    if (!_anchorQuery.IsEmptyIgnoreFilter)
    {
      EntityManager.DestroyEntity(_anchorQuery);
    }
    if (!_mergedRegionQuery.IsEmptyIgnoreFilter)
    {
      EntityManager.DestroyEntity(_mergedRegionQuery);
    }
    _anchors.Clear();
    _simulationRegionAnchors.Clear();
    _desiredAnchorIds.Clear();
    _desiredSimulationRegionKeys.Clear();
    _activeSimulationCoordinates.Clear();
    _mergedSimulationRegions.Clear();
    ChunkLoaderSimulationRegions.Reset();
  }

  private void MarkEnabledRecordsUnavailable()
  {
    ChunkLoaderRegistry.GetRecords(_records);
    for (int i = 0; i < _records.Count; i++)
    {
      if (_records[i].desiredEnabled)
      {
        ChunkLoaderRegistry.SetRuntimeState(
            _records[i].registrationId,
            ChunkLoaderRuntimeState.Error,
            ChunkLoaderErrorCode.CompatibilityUnavailable,
            ChunkLoaderCompatibility.CoreFailure);
      }
    }
  }

  private void OnRegistryChanged(ChunkLoaderRegistrationRecord _)
  {
    _nextReconcileAt = 0.0d;
  }

  private void OnRegistryDeleted(ulong _)
  {
    _nextReconcileAt = 0.0d;
  }
}
