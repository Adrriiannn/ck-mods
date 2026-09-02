using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Loading;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool IsServerWorldAvailable()
    {
      return serverWorld != null && serverWorld.IsCreated;
    }

    private void CreateServerQueries()
    {
      if (!IsServerWorldAvailable())
      {
        serverQueriesCreated = false;
        return;
      }

      EntityManager entityManager = serverWorld.EntityManager;
      runtimeLoadAnchorQuery = entityManager.CreateEntityQuery(
          ComponentType.ReadOnly<DimensionLoadAnchorCD>());
      runtimeSimulationRegionQuery = entityManager.CreateEntityQuery(
          ComponentType.ReadOnly<DimensionMergedSimulationRegionCD>());
      subMapQuery = entityManager.CreateEntityQuery(
          ComponentType.ReadOnly<SubMapCD>());
      networkTimeQuery =
          entityManager.CreateEntityQuery(
              new EntityQueryDesc
              {
                All = new ComponentType[] { ComponentType.ReadOnly<NetworkTime>() },
                Options = EntityQueryOptions.IncludeSystems
              });
      serverPlayerQuery = entityManager.CreateEntityQuery(
          ComponentType.ReadOnly<PlayerGhost>(),
          ComponentType.ReadOnly<LocalTransform>());
      serverQueriesCreated = true;
      nextRuntimeLoadReconcileAt = 0;
      nextPlayerContextTrackAt = 0;
      runtimeLoadTopologyDirty = true;
      DestroyAllRuntimeLoadAnchors();
    }

    private bool IsValidLoadBounds(DimensionBounds bounds, out string error)
    {
      int2 size = bounds.Size;
      if (size.x <= 0 || size.y <= 0)
      {
        error = "The requested load area must have a positive size.";
        return false;
      }

      if (size.x > MaximumLoadAreaSideTiles || size.y > MaximumLoadAreaSideTiles)
      {
        error = "A single runtime load request cannot exceed "
            + MaximumLoadAreaSideTiles
            + " tiles on either axis.";
        return false;
      }

      if (size.x * size.y > MaximumLoadAreaTiles)
      {
        error = "A single runtime load request cannot exceed "
            + MaximumLoadAreaTiles
            + " tiles.";
        return false;
      }

      error = string.Empty;
      return true;
    }

    private bool IsRuntimeLoadBudgetAvailable(DimensionBounds requestedBounds, out string error)
    {
      int requestedCells = CountRuntimeLoadBudgetCells(requestedBounds);
      int activeTickets = runtimeLoadRecords.Count;
      int activeCells = CountRuntimeLoadBudgetCells();

      if (activeTickets >= MaximumRuntimeLoadTickets)
      {
        error = "Runtime load ticket budget exceeded ("
            + activeTickets
            + "/"
            + MaximumRuntimeLoadTickets
            + " active tickets). Release an existing ticket before requesting another area.";
        return false;
      }

      if (activeCells + requestedCells > MaximumRuntimeLoadCells)
      {
        error = "Runtime load area budget exceeded (requested "
            + requestedCells
            + " 16x16 cells, active "
            + activeCells
            + "/"
            + MaximumRuntimeLoadCells
            + " cells). Release existing load tickets before requesting another area.";
        return false;
      }

      error = string.Empty;
      return true;
    }

    private int CountRuntimeLoadBudgetCells()
    {
      int total = 0;
      foreach (RuntimeLoadRecord record in runtimeLoadRecords.Values)
      {
        total += CountRuntimeLoadBudgetCells(record.Area.LocalBounds);
      }

      return total;
    }

    private int CountRuntimeLoadBudgetCells(DimensionBounds bounds)
    {
      int2 size = bounds.Size;
      int cellsX = CeilDivPositive(size.x, RuntimeLoadCellSizeTiles);
      int cellsY = CeilDivPositive(size.y, RuntimeLoadCellSizeTiles);
      return cellsX * cellsY;
    }

    private int CeilDivPositive(int value, int divisor)
    {
      if (value <= 0)
      {
        return 0;
      }

      return (value + divisor - 1) / divisor;
    }

    private Entity CreateRuntimeLoadAnchor(RuntimeLoadRecord record)
    {
      if (!IsServerWorldAvailable())
      {
        return Entity.Null;
      }

      try
      {
        DimensionBounds absoluteBounds = record.Area.AbsoluteBounds;
        int2 size = absoluteBounds.Size;
        float2 center2 =
            new float2(
                absoluteBounds.Min.x + size.x * 0.5f,
                absoluteBounds.Min.y + size.y * 0.5f);

        EntityManager entityManager = serverWorld.EntityManager;
        Entity anchor = entityManager.CreateEntity(
            typeof(DimensionLoadAnchorCD),
            typeof(LocalTransform),
            typeof(DontDisableCD),
            typeof(DontSerializeCD));

        entityManager.SetComponentData(anchor, new DimensionLoadAnchorCD
        {
          TicketHash = record.TicketHash,
          AbsoluteMinX = absoluteBounds.Min.x,
          AbsoluteMinY = absoluteBounds.Min.y,
          AbsoluteMaxExclusiveX = absoluteBounds.MaxExclusive.x,
          AbsoluteMaxExclusiveY = absoluteBounds.MaxExclusive.y,
          CreatedAt = record.CreatedAt,
          SubMapsObservedAt = record.SubMapsObservedAt,
          ImmediateLoadEnabled = record.ImmediateLoadEnabled ? (byte)1 : (byte)0,
          KeepTilesResident = record.KeepTilesResident ? (byte)1 : (byte)0,
          EnableSimulation = record.EnableSimulation ? (byte)1 : (byte)0
        });
        entityManager.SetComponentData(
            anchor,
            LocalTransform.FromPosition(new float3(center2.x, 0.0f, center2.y)));

        if (record.KeepTilesResident)
        {
          float radius = CalculateLoadRadius(size);
          entityManager.AddComponentData(anchor, new KeepAreaLoadedCD
          {
            KeepLoadedRadius = radius,
            StartLoadRadius = radius,
            ImmediateLoadRadius = radius
          });
        }

        return anchor;
      }
      catch (Exception ex)
      {
        AddDiagnostic(DimensionDiagnosticSeverity.Error, record.Area.DimensionId, "Runtime load anchor creation failed: " + ex.Message);
        return Entity.Null;
      }
    }

    private float CalculateLoadRadius(int2 size)
    {
      float diagonalRadius = math.length(new float2(size.x, size.y)) * 0.5f + 1.0f;
      return math.max(MinimumLoadRadius, diagonalRadius);
    }

    private void ReconcileRuntimeLoadRecords(double now)
    {
      if (!IsServerWorldAvailable())
      {
        MarkRuntimeLoadRecordsFailed("The server world is no longer available.");
        return;
      }

      if (!serverQueriesCreated)
      {
        CreateServerQueries();
      }

      RebuildObservedSubMaps();
      RebuildRuntimeSimulationRegionLookup();
      foreach (KeyValuePair<string, RuntimeLoadRecord> entry in runtimeLoadRecords)
      {
        RuntimeLoadRecord record = entry.Value;
        if (record.Anchor == Entity.Null || !serverWorld.EntityManager.Exists(record.Anchor))
        {
          record.State = DimensionLoadState.Failed;
          record.Message = "The runtime load anchor no longer exists.";
          UpdateLoadTicket(record);
          continue;
        }

        KeepOutOfTheSaveAndAlwaysLoaded(record.Anchor);

        bool allSubMapsObserved = AreRequiredSubMapsObserved(record);
        if (allSubMapsObserved)
        {
          if (record.SubMapsObservedAt <= 0)
          {
            record.SubMapsObservedAt = now;
            UpdateAnchorObservedAt(record);
          }

          if (now - record.SubMapsObservedAt >= LoadedStabilizationSeconds)
          {
            DisableImmediateLoading(record);
            record.State = record.EnableSimulation
                ? DimensionLoadState.Simulating
                : DimensionLoadState.Resident;
            record.Message = record.EnableSimulation
                ? "Area is resident and simulation is enabled."
                : "Area is resident.";
          }
          else
          {
            record.State = DimensionLoadState.Loading;
            record.Message = "Area parent submaps are stabilizing.";
          }

          UpdateLoadTicket(record);
          continue;
        }

        if (record.SubMapsObservedAt > 0)
        {
          record.SubMapsObservedAt = 0;
          record.ImmediateLoadEnabled = record.KeepTilesResident;
          UpdateAnchorObservedAt(record);
          EnableImmediateLoading(record);
        }

        if (now - record.CreatedAt > record.TimeoutSeconds)
        {
          DisableImmediateLoading(record);
          record.State = DimensionLoadState.Failed;
          record.Message = "Timed out before all parent submaps were observed.";
          UpdateLoadTicket(record);
        }
        else
        {
          record.State = DimensionLoadState.Loading;
          record.Message = "Waiting for parent submaps to load.";
          UpdateLoadTicket(record);
        }
      }

      ReconcileMergedSimulationRegions();
      RemoveStaleMergedSimulationRegions();
      runtimeLoadTopologyDirty = false;
    }

    private bool CanUseSettledRuntimeLoadMaintenance()
    {
      if (runtimeLoadTopologyDirty || !IsServerWorldAvailable())
      {
        return false;
      }

      foreach (RuntimeLoadRecord record in runtimeLoadRecords.Values)
      {
        if (!IsRuntimeLoadRecordSettled(record))
        {
          return false;
        }
      }

      return true;
    }

    private bool IsRuntimeLoadRecordSettled(RuntimeLoadRecord record)
    {
      if (record == null ||
          record.Anchor == Entity.Null ||
          record.SubMapsObservedAt <= 0 ||
          record.ImmediateLoadEnabled)
      {
        return false;
      }

      return record.State == DimensionLoadState.Resident ||
          record.State == DimensionLoadState.Simulating;
    }

    private void MaintainSettledRuntimeLoadRecords()
    {
      if (!IsServerWorldAvailable())
      {
        MarkRuntimeLoadRecordsFailed("The server world is no longer available.");
        runtimeLoadTopologyDirty = true;
        return;
      }

      foreach (RuntimeLoadRecord record in runtimeLoadRecords.Values)
      {
        if (record.Anchor == Entity.Null || !serverWorld.EntityManager.Exists(record.Anchor))
        {
          record.State = DimensionLoadState.Failed;
          record.Message = "The runtime load anchor no longer exists.";
          UpdateLoadTicket(record);
          runtimeLoadTopologyDirty = true;
          continue;
        }

        KeepOutOfTheSaveAndAlwaysLoaded(record.Anchor);
      }
    }

    private void RebuildObservedSubMaps()
    {
      observedSubMaps.Clear();
      if (!IsServerWorldAvailable() || !serverQueriesCreated)
      {
        return;
      }

      using (NativeArray<SubMapCD> subMaps =
          subMapQuery.ToComponentDataArray<SubMapCD>(Allocator.Temp))
      {
        for (int i = 0; i < subMaps.Length; i++)
        {
          observedSubMaps.Add(SubMapKey(subMaps[i].index.x, subMaps[i].index.y));
        }
      }
    }

    private void RebuildRuntimeSimulationRegionLookup()
    {
      runtimeSimulationRegionAnchors.Clear();
      if (!IsServerWorldAvailable() || !serverQueriesCreated)
      {
        return;
      }

      using (NativeArray<Entity> entities =
          runtimeSimulationRegionQuery.ToEntityArray(Allocator.Temp))
      using (NativeArray<DimensionMergedSimulationRegionCD> regions =
          runtimeSimulationRegionQuery.ToComponentDataArray<DimensionMergedSimulationRegionCD>(Allocator.Temp))
      {
        for (int i = 0; i < entities.Length; i++)
        {
          Entity entity = entities[i];
          DimensionMergedSimulationRegionCD region = regions[i];
          MergedSimulationRegion keyRegion =
              new MergedSimulationRegion(
                  region.AbsoluteMinX,
                  region.AbsoluteMinY,
                  region.SizeX,
                  region.SizeY);

          if (region.SizeX <= 0 ||
              region.SizeY <= 0 ||
              runtimeSimulationRegionAnchors.ContainsKey(keyRegion.Key))
          {
            serverWorld.EntityManager.DestroyEntity(entity);
            continue;
          }

          KeepOutOfTheSaveAndAlwaysLoaded(entity);
          runtimeSimulationRegionAnchors[keyRegion.Key] = entity;
        }
      }
    }

    private bool AreRequiredSubMapsObserved(RuntimeLoadRecord record)
    {
      for (int i = 0; i < record.RequiredSubMaps.Count; i++)
      {
        if (!observedSubMaps.Contains(record.RequiredSubMaps[i]))
        {
          return false;
        }
      }

      return true;
    }

    private void RefreshMergedSimulationRegions()
    {
      if (!IsServerWorldAvailable() || !serverQueriesCreated)
      {
        return;
      }

      RebuildRuntimeSimulationRegionLookup();
      ReconcileMergedSimulationRegions();
      RemoveStaleMergedSimulationRegions();
    }

    private void ReconcileMergedSimulationRegions()
    {
      BuildMergedSimulationRegions();
      desiredSimulationRegionKeys.Clear();
      for (int i = 0; i < mergedSimulationRegions.Count; i++)
      {
        MergedSimulationRegion region = mergedSimulationRegions[i];
        long key = region.Key;
        desiredSimulationRegionKeys.Add(key);
        Entity existing;
        if (runtimeSimulationRegionAnchors.TryGetValue(key, out existing) &&
            existing != Entity.Null &&
            serverWorld.EntityManager.Exists(existing))
        {
          continue;
        }

        Entity entity = CreateMergedSimulationRegion(region);
        if (entity != Entity.Null)
        {
          runtimeSimulationRegionAnchors[key] = entity;
        }
      }
    }

    private void BuildMergedSimulationRegions()
    {
      mergedSimulationRegions.Clear();
      simulationEdgesX.Clear();
      simulationEdgesY.Clear();
      simulationCells.Clear();

      foreach (RuntimeLoadRecord record in runtimeLoadRecords.Values)
      {
        if (!record.EnableSimulation ||
            record.State == DimensionLoadState.Failed ||
            record.Anchor == Entity.Null ||
            !serverWorld.EntityManager.Exists(record.Anchor))
        {
          continue;
        }

        DimensionBounds bounds = record.Area.AbsoluteBounds;
        simulationEdgesX.Add(bounds.Min.x);
        simulationEdgesX.Add(bounds.MaxExclusive.x);
        simulationEdgesY.Add(bounds.Min.y);
        simulationEdgesY.Add(bounds.MaxExclusive.y);
      }

      SortUnique(simulationEdgesX);
      SortUnique(simulationEdgesY);
      if (simulationEdgesX.Count < 2 || simulationEdgesY.Count < 2)
      {
        return;
      }

      for (int x = 0; x < simulationEdgesX.Count - 1; x++)
      {
        for (int y = 0; y < simulationEdgesY.Count - 1; y++)
        {
          if (IsSimulationCellCovered(
              simulationEdgesX[x],
              simulationEdgesY[y],
              simulationEdgesX[x + 1],
              simulationEdgesY[y + 1]))
          {
            simulationCells.Add(GridCellKey(x, y));
          }
        }
      }

      while (simulationCells.Count > 0)
      {
        long startKey = 0;
        foreach (long key in simulationCells)
        {
          startKey = key;
          break;
        }

        int startX = (int)(startKey >> 32);
        int startY = unchecked((int)(uint)startKey);
        int width = 1;
        while (simulationCells.Contains(GridCellKey(startX + width, startY)))
        {
          width++;
        }

        int height = 1;
        bool canGrow = true;
        while (canGrow)
        {
          int y = startY + height;
          for (int x = 0; x < width; x++)
          {
            if (!simulationCells.Contains(GridCellKey(startX + x, y)))
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
            simulationCells.Remove(GridCellKey(startX + x, startY + y));
          }
        }

        int minX = simulationEdgesX[startX];
        int minY = simulationEdgesY[startY];
        int maxX = simulationEdgesX[startX + width];
        int maxY = simulationEdgesY[startY + height];
        if (maxX > minX && maxY > minY)
        {
          mergedSimulationRegions.Add(
              new MergedSimulationRegion(minX, minY, maxX - minX, maxY - minY));
        }
      }
    }

    private bool IsSimulationCellCovered(
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
      foreach (RuntimeLoadRecord record in runtimeLoadRecords.Values)
      {
        if (!record.EnableSimulation ||
            record.State == DimensionLoadState.Failed ||
            record.Anchor == Entity.Null ||
            !serverWorld.EntityManager.Exists(record.Anchor))
        {
          continue;
        }

        DimensionBounds bounds = record.Area.AbsoluteBounds;
        if (bounds.Min.x <= minX &&
            bounds.Min.y <= minY &&
            bounds.MaxExclusive.x >= maxX &&
            bounds.MaxExclusive.y >= maxY)
        {
          return true;
        }
      }

      return false;
    }

    private Entity CreateMergedSimulationRegion(MergedSimulationRegion region)
    {
      if (!IsServerWorldAvailable())
      {
        return Entity.Null;
      }

      try
      {
        EntityManager entityManager = serverWorld.EntityManager;
        int2 center =
            new int2(
                region.MinX + region.SizeX / 2,
                region.MinY + region.SizeY / 2);

        Entity entity = entityManager.CreateEntity(
            typeof(DimensionMergedSimulationRegionCD),
            typeof(LocalTransform),
            typeof(EnableEntitiesInBoxCD),
            typeof(DontDisableCD),
            typeof(DontSerializeCD));
        entityManager.SetComponentData(entity, new DimensionMergedSimulationRegionCD
        {
          AbsoluteMinX = region.MinX,
          AbsoluteMinY = region.MinY,
          SizeX = region.SizeX,
          SizeY = region.SizeY
        });
        entityManager.SetComponentData(
            entity,
            LocalTransform.FromPosition(new float3(center.x, 0.0f, center.y)));
        entityManager.SetComponentData(entity, new EnableEntitiesInBoxCD
        {
          Area = PugGeometry.AxisAlignedBoundingBox.FromLowerCornerAndSize(
              new float2(region.MinX, region.MinY),
              new float2(region.SizeX, region.SizeY))
        });
        return entity;
      }
      catch (Exception ex)
      {
        AddDiagnostic(DimensionDiagnosticSeverity.Error, string.Empty, "Merged simulation region creation failed: " + ex.Message);
        return Entity.Null;
      }
    }

    private void RemoveStaleMergedSimulationRegions()
    {
      if (runtimeSimulationRegionAnchors.Count == 0 || !IsServerWorldAvailable())
      {
        return;
      }

      staleSimulationRegionKeys.Clear();
      foreach (KeyValuePair<long, Entity> entry in runtimeSimulationRegionAnchors)
      {
        if (!desiredSimulationRegionKeys.Contains(entry.Key))
        {
          staleSimulationRegionKeys.Add(entry.Key);
        }
      }

      for (int i = 0; i < staleSimulationRegionKeys.Count; i++)
      {
        long key = staleSimulationRegionKeys[i];
        Entity entity = runtimeSimulationRegionAnchors[key];
        if (entity != Entity.Null && serverWorld.EntityManager.Exists(entity))
        {
          serverWorld.EntityManager.DestroyEntity(entity);
        }

        runtimeSimulationRegionAnchors.Remove(key);
      }

      staleSimulationRegionKeys.Clear();
    }

    /// <summary>
    /// Takes an entity out of the save and stops the world unloading it.
    /// </summary>
    /// <remarks>
    /// The load anchor and the simulation region both need exactly this and nothing else, which
    /// is why one method answers for both. It was written twice, a hundred lines apart in this
    /// same file, under two names that read as two different repairs.
    /// </remarks>
    private void KeepOutOfTheSaveAndAlwaysLoaded(Entity entity)
    {
      if (!IsServerWorldAvailable() || entity == Entity.Null || !serverWorld.EntityManager.Exists(entity))
      {
        return;
      }

      EntityManager entityManager = serverWorld.EntityManager;
      if (entityManager.HasComponent<BlockSaveCD>(entity))
      {
        entityManager.RemoveComponent<BlockSaveCD>(entity);
      }

      if (!entityManager.HasComponent<DontSerializeCD>(entity))
      {
        entityManager.AddComponent<DontSerializeCD>(entity);
      }

      if (!entityManager.HasComponent<DontDisableCD>(entity))
      {
        entityManager.AddComponent<DontDisableCD>(entity);
      }
    }

    private void SortUnique(List<int> values)
    {
      values.Sort();
      for (int i = values.Count - 1; i > 0; i--)
      {
        if (values[i] == values[i - 1])
        {
          values.RemoveAt(i);
        }
      }
    }

    private long GridCellKey(int x, int y)
    {
      return ((long)x << 32) ^ (uint)y;
    }

    private void DisableImmediateLoading(RuntimeLoadRecord record)
    {
      if (!record.ImmediateLoadEnabled || !record.KeepTilesResident)
      {
        return;
      }

      if (!IsServerWorldAvailable() ||
          record.Anchor == Entity.Null ||
          !serverWorld.EntityManager.Exists(record.Anchor) ||
          !serverWorld.EntityManager.HasComponent<KeepAreaLoadedCD>(record.Anchor))
      {
        return;
      }

      KeepAreaLoadedCD residency =
          serverWorld.EntityManager.GetComponentData<KeepAreaLoadedCD>(record.Anchor);
      residency.ImmediateLoadRadius = 0.0f;
      serverWorld.EntityManager.SetComponentData(record.Anchor, residency);
      record.ImmediateLoadEnabled = false;
      UpdateAnchorObservedAt(record);
    }

    private void EnableImmediateLoading(RuntimeLoadRecord record)
    {
      if (record.ImmediateLoadEnabled || !record.KeepTilesResident)
      {
        return;
      }

      if (!IsServerWorldAvailable() ||
          record.Anchor == Entity.Null ||
          !serverWorld.EntityManager.Exists(record.Anchor) ||
          !serverWorld.EntityManager.HasComponent<KeepAreaLoadedCD>(record.Anchor))
      {
        return;
      }

      DimensionBounds absoluteBounds = record.Area.AbsoluteBounds;
      float radius = CalculateLoadRadius(absoluteBounds.Size);
      KeepAreaLoadedCD residency =
          serverWorld.EntityManager.GetComponentData<KeepAreaLoadedCD>(record.Anchor);
      residency.ImmediateLoadRadius = radius;
      serverWorld.EntityManager.SetComponentData(record.Anchor, residency);
      record.ImmediateLoadEnabled = true;
      UpdateAnchorObservedAt(record);
    }

    private void UpdateAnchorObservedAt(RuntimeLoadRecord record)
    {
      if (!IsServerWorldAvailable() ||
          record.Anchor == Entity.Null ||
          !serverWorld.EntityManager.Exists(record.Anchor) ||
          !serverWorld.EntityManager.HasComponent<DimensionLoadAnchorCD>(record.Anchor))
      {
        return;
      }

      DimensionLoadAnchorCD component =
          serverWorld.EntityManager.GetComponentData<DimensionLoadAnchorCD>(record.Anchor);
      component.SubMapsObservedAt = record.SubMapsObservedAt;
      component.ImmediateLoadEnabled = record.ImmediateLoadEnabled ? (byte)1 : (byte)0;
      serverWorld.EntityManager.SetComponentData(record.Anchor, component);
    }

    private void UpdateLoadTicket(RuntimeLoadRecord record)
    {
      DimensionLoadTicket previous;
      DimensionLoadTicket updated =
          new DimensionLoadTicket(true, record.TicketId, record.State, record.Message);
      bool changed =
          !loadTickets.TryGetValue(record.TicketId, out previous) ||
          previous.State != updated.State ||
          !string.Equals(previous.Message, updated.Message, StringComparison.Ordinal);
      loadTickets[record.TicketId] = updated;
      if (changed)
      {
        RaiseLoadTicketChanged(record);
      }
    }

    private void MarkRuntimeLoadRecordsFailed(string message)
    {
      foreach (RuntimeLoadRecord record in runtimeLoadRecords.Values)
      {
        record.State = DimensionLoadState.Failed;
        record.Message = message;
        UpdateLoadTicket(record);
      }
    }

    private void DestroyRuntimeLoadAnchor(RuntimeLoadRecord record)
    {
      if (!IsServerWorldAvailable() || record.Anchor == Entity.Null)
      {
        return;
      }

      EntityManager entityManager = serverWorld.EntityManager;
      if (entityManager.Exists(record.Anchor))
      {
        entityManager.DestroyEntity(record.Anchor);
      }
    }

    private void DestroyAllRuntimeLoadAnchors()
    {
      if (!IsServerWorldAvailable() || !serverQueriesCreated)
      {
        runtimeSimulationRegionAnchors.Clear();
        return;
      }

      if (!runtimeLoadAnchorQuery.IsEmptyIgnoreFilter)
      {
        serverWorld.EntityManager.DestroyEntity(runtimeLoadAnchorQuery);
      }
      if (!runtimeSimulationRegionQuery.IsEmptyIgnoreFilter)
      {
        serverWorld.EntityManager.DestroyEntity(runtimeSimulationRegionQuery);
      }

      runtimeSimulationRegionAnchors.Clear();
    }

    private void ResetRuntimeLoadState()
    {
      runtimeLoadRecords.Clear();
      loadTickets.Clear();
      runtimeSimulationRegionAnchors.Clear();
      observedSubMaps.Clear();
      pendingTravelByPlayerId.Clear();
      pendingTravelKeys.Clear();
      trackedPlayerContexts.Clear();
      playerPersistentIdCache.Clear();
      observedPlayerIds.Clear();
      trackedPlayerKeys.Clear();
      desiredSimulationRegionKeys.Clear();
      mergedSimulationRegions.Clear();
      simulationEdgesX.Clear();
      simulationEdgesY.Clear();
      simulationCells.Clear();
      staleSimulationRegionKeys.Clear();
      nextRuntimeLoadReconcileAt = 0;
      nextPlayerContextTrackAt = 0;
      runtimeLoadTopologyDirty = false;
    }

    private void PopulateRequiredSubMaps(DimensionBounds absoluteBounds, List<long> requiredSubMaps)
    {
      requiredSubMaps.Clear();
      int maxInclusiveX = absoluteBounds.MaxExclusive.x - 1;
      int maxInclusiveY = absoluteBounds.MaxExclusive.y - 1;
      int minSubX = FloorDiv(absoluteBounds.Min.x, ParentSubMapSize);
      int minSubY = FloorDiv(absoluteBounds.Min.y, ParentSubMapSize);
      int maxSubX = FloorDiv(maxInclusiveX, ParentSubMapSize);
      int maxSubY = FloorDiv(maxInclusiveY, ParentSubMapSize);

      for (int y = minSubY; y <= maxSubY; y++)
      {
        for (int x = minSubX; x <= maxSubX; x++)
        {
          requiredSubMaps.Add(SubMapKey(x, y));
        }
      }
    }

    private int FloorDiv(int value, int divisor)
    {
      int quotient = value / divisor;
      int remainder = value % divisor;
      if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
      {
        quotient--;
      }

      return quotient;
    }

    private long SubMapKey(int x, int y)
    {
      return ((long)x << 32) ^ (uint)y;
    }

    private string CreateLoadTicketId()
    {
      return "load-" + Guid.NewGuid().ToString("N");
    }

    private ulong HashTicketId(string ticketId)
    {
      unchecked
      {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (int i = 0; i < ticketId.Length; i++)
        {
          hash ^= ticketId[i];
          hash *= prime;
        }

        return hash;
      }
    }
  }
}
