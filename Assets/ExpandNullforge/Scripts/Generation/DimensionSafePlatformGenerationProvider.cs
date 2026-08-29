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
    // Dirt, and only when nothing says otherwise. This used to be the ONLY tileset the whole file
    // named, which is why every generated dimension came out a dirt platform however carefully its
    // biomes were authored. DimensionTerrainMaterialRegistry answers first now; this is what a cell
    // no biome covers still gets, so an unauthored dimension generates exactly what it always did.
    private const int FallbackTileset = DimensionTerrainMaterialRegistry.DefaultTileset;
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
      // A full-area pass is the single-provider behavior wearing a pass id, so it keeps the
      // border walls; only an authored, scoped pass paints an open-edged patch.
      return TickSafePlatform(
          context.GenerationContext,
          paintBounds,
          !context.Pass.HasLocalBounds,
          waitForTileUpdate);
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

      string key = CreateKey(
          context.Dimension.Id,
          context.Area.LocalBounds,
          paintBounds,
          addBorderWalls,
          waitForTileUpdate);
      GenerationJob job;
      if (!jobs.TryGetValue(key, out job))
      {
        job = new GenerationJob(
            context.Dimension.Id,
            paintBounds,
            addBorderWalls,
            waitForTileUpdate);
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

      // Asked once per tick rather than per cell: it is a dictionary-count read, and nothing can
      // register a tileset between two cells of one loop.
      bool scatterCover = DimensionOverlayRuleRegistry.Any;

      // The budget counts TILE WRITES, not cells, and it is only tested between cells. A cell's
      // ground and the cover growing on it have to reach the game in the same flush: a ground
      // write emits a Remove for smallGrass / smallStones / debris / debris2 / wallGrass at its
      // own cell, so cover queued in an earlier flush than its ground is deleted by that ground.
      int painted = 0;
      while (job.NextIndex < job.TotalTileCount && painted < MaxTilesPerTick)
      {
        painted += PaintTile(context.Area, job, scatterCover, tileUpdates);
        job.NextIndex++;
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

    /// <summary>
    /// Lays one cell, and returns how many tile writes it queued so the caller's per-tick budget
    /// can count them.
    /// </summary>
    /// <remarks>
    /// Order inside a cell is ground, then either the border wall or the cover — never both, and
    /// never cover first. A wall write strips smallGrass and smallStones at its own cell, so
    /// decorating a cell that is about to take a wall would queue grass that the very next write
    /// deletes.
    /// </remarks>
    private static int PaintTile(
        DimensionArea area,
        GenerationJob job,
        bool scatterCover,
        DynamicBuffer<TileUpdateBuffer> tileUpdates)
    {
      int width = job.Width;
      int localX = job.LocalBounds.Min.x + (job.NextIndex % width);
      int localY = job.LocalBounds.Min.y + (job.NextIndex / width);
      int2 local = new int2(localX, localY);
      int2 absolute = area.AbsoluteBounds.Min + (local - area.LocalBounds.Min);

      // What this cell's biome says its ground and walls are made of. False means no biome covers
      // the cell, and dirt is the answer — which is what this provider did for every cell before
      // the registry existed.
      int groundTileset;
      int wallTileset;
      if (!DimensionTerrainMaterialRegistry.TryResolve(
              job.DimensionId, local, out groundTileset, out wallTileset))
      {
        groundTileset = FallbackTileset;
        wallTileset = FallbackTileset;
      }

      EntityUtility.AddTile(
          groundTileset,
          TileType.ground,
          absolute,
          true,
          tileUpdates);
      int written = 1;

      if (job.AddBorderWalls && IsBorderTile(local, job.LocalBounds))
      {
        EntityUtility.AddTile(
            wallTileset,
            TileType.wall,
            absolute,
            true,
            tileUpdates);
        return written + 1;
      }

      if (!scatterCover)
      {
        return written;
      }

      // Grow this block's own decoration on the ground just laid, in the same flush as that
      // ground. The rules are keyed by the tileset a tile carries, which is why this asks about
      // groundTileset rather than about the dimension.
      IReadOnlyList<DimensionOverlayRule> rules = DimensionOverlayRuleRegistry.For(groundTileset);
      for (int r = 0; r < rules.Count; r++)
      {
        if (!DimensionOverlayScatter.ShouldPlace(job.OverlaySeed, absolute, rules[r]))
        {
          continue;
        }

        EntityUtility.AddTile(
            groundTileset,
            rules[r].TileType,
            absolute,
            true,
            tileUpdates);
        written++;
      }

      return written;
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
      public readonly string DimensionId;

      /// <summary>
      /// The scatter seed, taken from the DIMENSION rather than the save.
      /// </summary>
      /// <remarks>
      /// The same choice the painted-map provider makes, for the same reason: an authored
      /// dimension's decoration is part of its design, and a host and a joining client generate
      /// terrain independently, so a seed either of them could compute differently would grow
      /// different grass on the two screens. Both providers hash the id through the one shared
      /// <see cref="DimensionOverlayScatter.StableHash"/>, so a dimension that has some of its
      /// ground painted and some of it generated grows one continuous carpet.
      /// </remarks>
      public readonly ulong OverlaySeed;
      public readonly DimensionBounds LocalBounds;
      public readonly int Width;
      public readonly int TotalTileCount;
      public readonly bool AddBorderWalls;
      public readonly bool WaitForTileUpdate;
      public int NextIndex;
      public bool WaitingForTileUpdate;
      public int ReadyAfterFrame;

      public GenerationJob(
          string dimensionId,
          DimensionBounds localBounds,
          bool addBorderWalls,
          bool waitForTileUpdate)
      {
        DimensionId = dimensionId ?? string.Empty;
        OverlaySeed = DimensionOverlayScatter.Hash(
            0UL, default(int2), DimensionOverlayScatter.StableHash(DimensionId));
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
