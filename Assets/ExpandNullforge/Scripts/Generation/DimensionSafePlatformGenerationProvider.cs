using System.Collections.Generic;
using ExpandNullforge.Api;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Generation
{
  public sealed class DimensionSafePlatformGenerationProvider :
      IDimensionGenerationProvider,
      IDimensionGenerationPassProvider
  {
    private const int Tileset = 0;
    private const int MaxTilesPerTick = 384;
    private const int ReadyDelayFrames = 2;

    private readonly Dictionary<string, GenerationJob> jobs =
        new Dictionary<string, GenerationJob>();

    private World cachedTileUpdateWorld;
    private Entity cachedTileUpdateEntity;

    public string ProviderId
    {
      get { return DimensionGenerationProviderIds.SafePlatform; }
    }

    public void ClearJobs()
    {
      jobs.Clear();
      cachedTileUpdateWorld = null;
      cachedTileUpdateEntity = Entity.Null;
    }

    public bool CanGenerate(DimensionDefinition dimension, DimensionBounds localBounds)
    {
      if (string.IsNullOrEmpty(dimension.Id) ||
          dimension.Id == DimensionIds.Overworld ||
          !dimension.HasCapability(DimensionCapabilityFlags.Generation))
      {
        return false;
      }

      // Yield to a painted tile map: a dimension the creator has painted should generate from
      // that map (the tile-map provider), not the flat safe platform. Order-independent, so it
      // does not matter which provider the selection loop checks first.
      if (Foundation.DimensionTileMapRegistry.Has(dimension.Id))
      {
        return false;
      }

      int2 size = localBounds.Size;
      return size.x > 0 && size.y > 0;
    }

    public DimensionGenerationProviderResult TickGeneration(
        DimensionGenerationContext context)
    {
      return TickSafePlatform(context, context.Area.LocalBounds, true, true);
    }

    public DimensionGenerationProviderResult TickGenerationPass(
        DimensionGenerationPassContext context)
    {
      DimensionBounds paintBounds = context.Pass.HasLocalBounds
          ? Intersection(context.Pass.LocalBounds, context.GenerationContext.Area.LocalBounds)
          : context.GenerationContext.Area.LocalBounds;
      if (!IsValidBounds(paintBounds))
      {
        return DimensionGenerationProviderResult.Ready(
            "Generation pass does not overlap the requested area.");
      }

      bool waitForTileUpdate = context.PassIndex + 1 >= context.PassCount;
      return TickSafePlatform(context.GenerationContext, paintBounds, false, waitForTileUpdate);
    }

    private DimensionGenerationProviderResult TickSafePlatform(
        DimensionGenerationContext context,
        DimensionBounds paintBounds,
        bool addBorderWalls,
        bool waitForTileUpdate)
    {
      if (context.ServerWorld == null || !context.ServerWorld.IsCreated)
      {
        return DimensionGenerationProviderResult.Failed(
            "The server world is not available for terrain generation.");
      }

      if (!CanGenerate(context.Dimension, context.Area.LocalBounds))
      {
        return DimensionGenerationProviderResult.Failed(
            "The safe-platform provider cannot generate this dimension area.");
      }

      Foundation.DimensionFrameworkLog.Warning(
          "[ExpandNullforge][tilemap] SAFE PLATFORM generating '" + context.Dimension.Id +
          "' (tile map registered=" + Foundation.DimensionTileMapRegistry.Has(context.Dimension.Id) + ").");

      string key = CreateKey(
          context.Dimension.Id,
          context.Area.LocalBounds,
          paintBounds,
          addBorderWalls,
          waitForTileUpdate);
      GenerationJob job;
      if (!jobs.TryGetValue(key, out job))
      {
        job = new GenerationJob(paintBounds, addBorderWalls, waitForTileUpdate);
      }

      if (job.WaitingForTileUpdate)
      {
        if (Time.frameCount < job.ReadyAfterFrame)
        {
          jobs[key] = job;
          return DimensionGenerationProviderResult.Progress(
              DimensionGenerationState.GeneratingTerrain,
              0.99f,
              "Safe-platform tiles queued.");
        }

        jobs.Remove(key);
        return DimensionGenerationProviderResult.Ready(
            "Safe-platform terrain generated.");
      }

      DynamicBuffer<TileUpdateBuffer> tileUpdates;
      if (!TryGetTileUpdateBuffer(context.ServerWorld, out tileUpdates))
      {
        return DimensionGenerationProviderResult.Progress(
            DimensionGenerationState.GeneratingTerrain,
            job.Progress01,
            "Waiting for the tile update buffer.");
      }

      int painted = 0;
      while (job.NextIndex < job.TotalTileCount && painted < MaxTilesPerTick)
      {
        PaintTile(context.Area, job, tileUpdates);
        job.NextIndex++;
        painted++;
      }

      if (job.NextIndex >= job.TotalTileCount)
      {
        if (!job.WaitForTileUpdate)
        {
          jobs.Remove(key);
          return DimensionGenerationProviderResult.Ready(
              "Safe-platform terrain generated.");
        }

        job.WaitingForTileUpdate = true;
        job.ReadyAfterFrame = Time.frameCount + ReadyDelayFrames;
        jobs[key] = job;
        return DimensionGenerationProviderResult.Progress(
            DimensionGenerationState.GeneratingTerrain,
            0.99f,
            "Safe-platform tiles queued.");
      }

      jobs[key] = job;
      return DimensionGenerationProviderResult.Progress(
          DimensionGenerationState.GeneratingTerrain,
          job.Progress01,
          "Generating safe-platform terrain.");
    }

    private static void PaintTile(
        DimensionArea area,
        GenerationJob job,
        DynamicBuffer<TileUpdateBuffer> tileUpdates)
    {
      int width = job.Width;
      int localX = job.LocalBounds.Min.x + (job.NextIndex % width);
      int localY = job.LocalBounds.Min.y + (job.NextIndex / width);
      int2 local = new int2(localX, localY);
      int2 absolute = area.AbsoluteBounds.Min + (local - area.LocalBounds.Min);

      EntityUtility.AddTile(
          Tileset,
          TileType.ground,
          absolute,
          true,
          tileUpdates);

      if (job.AddBorderWalls && IsBorderTile(local, job.LocalBounds))
      {
        EntityUtility.AddTile(
            Tileset,
            TileType.wall,
            absolute,
            true,
            tileUpdates);
      }
    }

    private static bool IsBorderTile(int2 local, DimensionBounds bounds)
    {
      return local.x == bounds.Min.x ||
             local.y == bounds.Min.y ||
             local.x == bounds.MaxExclusive.x - 1 ||
             local.y == bounds.MaxExclusive.y - 1;
    }

    private static bool IsValidBounds(DimensionBounds bounds)
    {
      return bounds.MaxExclusive.x > bounds.Min.x &&
             bounds.MaxExclusive.y > bounds.Min.y;
    }

    private static DimensionBounds Intersection(DimensionBounds a, DimensionBounds b)
    {
      return new DimensionBounds(
          new int2(math.max(a.Min.x, b.Min.x), math.max(a.Min.y, b.Min.y)),
          new int2(math.min(a.MaxExclusive.x, b.MaxExclusive.x), math.min(a.MaxExclusive.y, b.MaxExclusive.y)));
    }

    private bool TryGetTileUpdateBuffer(
        World serverWorld,
        out DynamicBuffer<TileUpdateBuffer> tileUpdates)
    {
      if (serverWorld == null || !serverWorld.IsCreated)
      {
        tileUpdates = default(DynamicBuffer<TileUpdateBuffer>);
        return false;
      }

      EntityManager entityManager = serverWorld.EntityManager;
      if (cachedTileUpdateWorld == serverWorld &&
          cachedTileUpdateEntity != Entity.Null &&
          entityManager.Exists(cachedTileUpdateEntity) &&
          entityManager.HasBuffer<TileUpdateBuffer>(cachedTileUpdateEntity))
      {
        tileUpdates = entityManager.GetBuffer<TileUpdateBuffer>(cachedTileUpdateEntity);
        return true;
      }

      cachedTileUpdateWorld = serverWorld;
      cachedTileUpdateEntity = Entity.Null;
      using (EntityQuery query =
          entityManager.CreateEntityQuery(ComponentType.ReadWrite<TileUpdateBuffer>()))
      {
        if (query.IsEmpty)
        {
          Entity entity = entityManager.CreateEntity();
          cachedTileUpdateEntity = entity;
          tileUpdates = entityManager.AddBuffer<TileUpdateBuffer>(entity);
          return true;
        }

        using (NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp))
        {
          if (entities.Length == 0)
          {
            tileUpdates = default(DynamicBuffer<TileUpdateBuffer>);
            return false;
          }

          cachedTileUpdateEntity = entities[0];
          tileUpdates = entityManager.GetBuffer<TileUpdateBuffer>(cachedTileUpdateEntity);
          return true;
        }
      }
    }

    private static string CreateKey(
        string dimensionId,
        DimensionBounds requestBounds,
        DimensionBounds paintBounds,
        bool addBorderWalls,
        bool waitForTileUpdate)
    {
      return (dimensionId ?? string.Empty) +
             "|" +
             requestBounds.Min.x +
             "," +
             requestBounds.Min.y +
             "|" +
             requestBounds.MaxExclusive.x +
             "," +
             requestBounds.MaxExclusive.y +
             "|" +
             paintBounds.Min.x +
             "," +
             paintBounds.Min.y +
             "|" +
             paintBounds.MaxExclusive.x +
             "," +
             paintBounds.MaxExclusive.y +
             "|" +
             (addBorderWalls ? "walls" : "floor") +
             "|" +
             (waitForTileUpdate ? "wait" : "nowait");
    }

    private struct GenerationJob
    {
      public readonly DimensionBounds LocalBounds;
      public readonly int Width;
      public readonly int TotalTileCount;
      public readonly bool AddBorderWalls;
      public readonly bool WaitForTileUpdate;
      public int NextIndex;
      public bool WaitingForTileUpdate;
      public int ReadyAfterFrame;

      public GenerationJob(
          DimensionBounds localBounds,
          bool addBorderWalls,
          bool waitForTileUpdate)
      {
        LocalBounds = localBounds;
        int2 size = localBounds.Size;
        Width = math.max(1, size.x);
        TotalTileCount = math.max(1, size.x * size.y);
        AddBorderWalls = addBorderWalls;
        WaitForTileUpdate = waitForTileUpdate;
        NextIndex = 0;
        WaitingForTileUpdate = false;
        ReadyAfterFrame = 0;
      }

      public float Progress01
      {
        get
        {
          return TotalTileCount <= 0
              ? 0.0f
              : math.clamp((float)NextIndex / TotalTileCount, 0.0f, 0.98f);
        }
      }
    }
  }
}
