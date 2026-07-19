using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private readonly Dictionary<Entity, string> playerPersistentIdCache =
        new Dictionary<Entity, string>();

    public bool HasTrackedPlayerInDimension(string dimensionId)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        return false;
      }

      foreach (TrackedPlayerContextRecord tracked in trackedPlayerContexts.Values)
      {
        if (tracked != null &&
            string.Equals(tracked.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }

    private double GetPlayerContextTrackIntervalSeconds()
    {
      if (pendingTravelByPlayerId.Count > 0 ||
          runtimeLoadRecords.Count > 0 ||
          runtimeGenerationRecords.Count > 0 ||
          HasTrackedNonOverworldPlayer())
      {
        return PlayerContextTrackIntervalSeconds;
      }

      return IdleOverworldPlayerContextTrackIntervalSeconds;
    }

    private bool HasTrackedNonOverworldPlayer()
    {
      foreach (TrackedPlayerContextRecord tracked in trackedPlayerContexts.Values)
      {
        if (tracked != null &&
            !string.IsNullOrEmpty(tracked.DimensionId) &&
            !string.Equals(tracked.DimensionId, DimensionIds.Overworld, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }

    private void TrackServerPlayerContexts()
    {
      if (!IsServerWorldAvailable())
      {
        trackedPlayerContexts.Clear();
        observedPlayerIds.Clear();
        trackedPlayerKeys.Clear();
        return;
      }

      if (!serverQueriesCreated)
      {
        CreateServerQueries();
      }

      observedPlayerIds.Clear();
      using NativeArray<Entity> playerEntities =
          serverPlayerQuery.ToEntityArray(Allocator.Temp);

      EntityManager entityManager = serverWorld.EntityManager;
      for (int i = 0; i < playerEntities.Length; i++)
      {
        Entity player = playerEntities[i];
        if (player == Entity.Null ||
            !entityManager.Exists(player) ||
            !entityManager.HasComponent<LocalTransform>(player))
        {
          continue;
        }

        string playerId;
        if (!TryGetCachedPlayerPersistentId(player, out playerId))
        {
          continue;
        }

        observedPlayerIds.Add(playerId);
        LocalTransform transform = entityManager.GetComponentData<LocalTransform>(player);
        float2 absolutePosition =
            new float2(transform.Position.x, transform.Position.z);
        DimensionContext context = GetContextForAbsolute(absolutePosition);
        if (!context.IsKnown)
        {
          continue;
        }

        UpsertTrackedPlayerContext(playerId, context, absolutePosition);
      }

      RemoveUnobservedTrackedPlayers();
    }

    private void UpsertTrackedPlayerContext(
        string playerId,
        DimensionContext context,
        float2 absolutePosition)
    {
      int2 localTile = ToTile(context.LocalPosition);
      int2 absoluteTile = ToTile(absolutePosition);

      TrackedPlayerContextRecord tracked;
      bool wasTracked = trackedPlayerContexts.TryGetValue(playerId, out tracked);
      string previousDimensionId = wasTracked && tracked != null
          ? tracked.DimensionId
          : string.Empty;
      bool dimensionChanged =
          !wasTracked ||
          tracked == null ||
          !string.Equals(previousDimensionId, context.DimensionId, StringComparison.Ordinal);

      if (wasTracked &&
          tracked != null &&
          !dimensionChanged &&
          math.all(tracked.LocalTile == localTile) &&
          math.all(tracked.AbsoluteTile == absoluteTile))
      {
        return;
      }

      if (tracked == null)
      {
        tracked = new TrackedPlayerContextRecord();
        trackedPlayerContexts[playerId] = tracked;
      }

      tracked.DimensionId = context.DimensionId;
      tracked.LocalTile = localTile;
      tracked.AbsoluteTile = absoluteTile;
      tracked.LocalPosition = context.LocalPosition;
      tracked.AbsolutePosition = absolutePosition;

      bool isOverworld =
          string.Equals(context.DimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
      bool shouldPersistCheckpoint =
          dimensionChanged ||
          !tracked.HasPersistedCheckpoint ||
          !string.Equals(
              tracked.PersistedDimensionId,
              context.DimensionId,
              StringComparison.Ordinal) ||
          IsPersistenceCheckpointDistanceExceeded(tracked.PersistedLocalTile, localTile) ||
          IsPersistenceCheckpointDistanceExceeded(tracked.PersistedAbsoluteTile, absoluteTile);

      if ((!isOverworld || dimensionChanged) && shouldPersistCheckpoint)
      {
        DimensionWorldRegistry.UpsertPlayerState(
            playerId,
            context.DimensionId,
            context.LocalPosition,
            absolutePosition);
      }

      if (!isOverworld && shouldPersistCheckpoint)
      {
        DimensionWorldRegistry.UpsertPlayerVisit(
            playerId,
            context.DimensionId,
            context.LocalPosition,
            absolutePosition);
      }

      if (shouldPersistCheckpoint)
      {
        tracked.HasPersistedCheckpoint = true;
        tracked.PersistedDimensionId = context.DimensionId;
        tracked.PersistedLocalTile = localTile;
        tracked.PersistedAbsoluteTile = absoluteTile;
      }
    }

    private void RemoveUnobservedTrackedPlayers()
    {
      trackedPlayerKeys.Clear();
      foreach (string playerId in trackedPlayerContexts.Keys)
      {
        if (!observedPlayerIds.Contains(playerId))
        {
          trackedPlayerKeys.Add(playerId);
        }
      }

      for (int i = 0; i < trackedPlayerKeys.Count; i++)
      {
        trackedPlayerContexts.Remove(trackedPlayerKeys[i]);
      }

      trackedPlayerKeys.Clear();
    }

    private int2 ToTile(float2 position)
    {
      return new int2((int)math.floor(position.x), (int)math.floor(position.y));
    }

    private bool TryGetCachedPlayerPersistentId(Entity player, out string playerId)
    {
      if (playerPersistentIdCache.TryGetValue(player, out playerId) &&
          !string.IsNullOrEmpty(playerId))
      {
        return true;
      }

      if (!TryGetPlayerPersistentId(player, out playerId))
      {
        return false;
      }

      playerPersistentIdCache[player] = playerId;
      return true;
    }

    private bool IsPersistenceCheckpointDistanceExceeded(int2 previous, int2 current)
    {
      return math.abs(current.x - previous.x) >= PlayerContextPersistenceTileThreshold ||
          math.abs(current.y - previous.y) >= PlayerContextPersistenceTileThreshold;
    }
  }
}
