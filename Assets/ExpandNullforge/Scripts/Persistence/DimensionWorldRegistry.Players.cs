using System;
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Newtonsoft.Json;
using Pug.ECS.Components;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Persistence
{
  /// <summary>
  /// What each player has visited, and where they were when they left.
  /// </summary>
  public static partial class DimensionWorldRegistry
  {
    public static void UpsertPlayerState(
        string playerId,
        string dimensionId,
        float2 localPosition,
        float2 absolutePosition)
    {
      if (string.IsNullOrEmpty(playerId))
      {
        return;
      }

      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.players = _state.players ?? new List<DimensionPlayerStateRecord>();
      int index = FindPlayerRecordIndex(playerId);
      DimensionPlayerStateRecord record = new DimensionPlayerStateRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        playerId = SanitizeName(playerId, 96, string.Empty),
        dimensionId = string.IsNullOrEmpty(dimensionId) ? DimensionIds.Overworld : dimensionId,
        localX = localPosition.x,
        localY = localPosition.y,
        absoluteX = absolutePosition.x,
        absoluteY = absolutePosition.y,
        savedUtcTicks = DateTime.UtcNow.Ticks
      };

      if (index >= 0)
      {
        DimensionPlayerStateRecord existing = _state.players[index];
        if (IsSamePlayerState(existing, record))
        {
          return;
        }

        _state.players[index] = record;
      }
      else
      {
        _state.players.Add(record);
      }

      if (IsNonOverworld(record.dimensionId))
      {
        _hasNonOverworldFootprint = true;
        Touch(false);
      }
      else
      {
        Touch(true);
      }
    }

    public static bool TryGetPlayerState(string playerId, out DimensionPlayerStateRecord record)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.players == null || string.IsNullOrEmpty(playerId))
      {
        record = null;
        return false;
      }

      int index = FindPlayerRecordIndex(playerId);
      if (index < 0)
      {
        record = null;
        return false;
      }

      record = _state.players[index];
      return record != null;
    }

    public static void UpsertPlayerVisit(
        string playerId,
        string dimensionId,
        float2 localPosition,
        float2 absolutePosition)
    {
      if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(dimensionId))
      {
        return;
      }

      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.playerVisits =
          _state.playerVisits ?? new List<DimensionPlayerVisitStateRecord>();
      int index = FindPlayerVisitRecordIndex(playerId, dimensionId);
      DimensionPlayerVisitStateRecord record = new DimensionPlayerVisitStateRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        playerId = SanitizeName(playerId, 96, string.Empty),
        dimensionId = SanitizeName(dimensionId, 128, DimensionIds.Overworld),
        localX = localPosition.x,
        localY = localPosition.y,
        absoluteX = absolutePosition.x,
        absoluteY = absolutePosition.y,
        savedUtcTicks = DateTime.UtcNow.Ticks
      };

      if (index >= 0)
      {
        DimensionPlayerVisitStateRecord existing = _state.playerVisits[index];
        if (IsSamePlayerVisit(existing, record))
        {
          return;
        }

        _state.playerVisits[index] = record;
      }
      else
      {
        _state.playerVisits.Add(record);
      }

      if (IsNonOverworld(record.dimensionId))
      {
        _hasNonOverworldFootprint = true;
        Touch(false);
      }
      else
      {
        Touch(true);
      }
    }

    public static bool TryGetPlayerVisit(
        string playerId,
        string dimensionId,
        out DimensionPlayerVisitRecord visit)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded ||
          _state == null ||
          _state.playerVisits == null ||
          string.IsNullOrEmpty(playerId) ||
          string.IsNullOrEmpty(dimensionId))
      {
        visit = default(DimensionPlayerVisitRecord);
        return false;
      }

      int index = FindPlayerVisitRecordIndex(playerId, dimensionId);
      if (index < 0)
      {
        visit = default(DimensionPlayerVisitRecord);
        return false;
      }

      visit = ToPlayerVisit(_state.playerVisits[index]);
      return !string.IsNullOrEmpty(visit.PlayerId);
    }

    public static void GetPlayerVisits(
        string playerId,
        List<DimensionPlayerVisitRecord> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded ||
          _state == null ||
          _state.playerVisits == null ||
          string.IsNullOrEmpty(playerId))
      {
        return;
      }

      for (int i = 0; i < _state.playerVisits.Count; i++)
      {
        DimensionPlayerVisitStateRecord record = _state.playerVisits[i];
        if (record == null ||
            !string.Equals(record.playerId, playerId, StringComparison.Ordinal))
        {
          continue;
        }

        destination.Add(ToPlayerVisit(record));
      }

      destination.Sort(ComparePlayerVisits);
    }

    public static void RemovePlayerVisit(string playerId, string dimensionId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded ||
          _state == null ||
          _state.playerVisits == null ||
          string.IsNullOrEmpty(playerId) ||
          string.IsNullOrEmpty(dimensionId))
      {
        return;
      }

      int index = FindPlayerVisitRecordIndex(playerId, dimensionId);
      if (index < 0)
      {
        return;
      }

      _state.playerVisits.RemoveAt(index);
      Touch();
    }

    public static void RemovePlayerVisitsForDimension(string dimensionId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded ||
          _state == null ||
          _state.playerVisits == null ||
          string.IsNullOrEmpty(dimensionId))
      {
        return;
      }

      bool changed = false;
      for (int i = _state.playerVisits.Count - 1; i >= 0; i--)
      {
        DimensionPlayerVisitStateRecord record = _state.playerVisits[i];
        if (record != null &&
            string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal))
        {
          _state.playerVisits.RemoveAt(i);
          changed = true;
        }
      }

      if (changed)
      {
        Touch();
      }
    }

    private static bool IsSamePlayerState(
        DimensionPlayerStateRecord a,
        DimensionPlayerStateRecord b)
    {
      return a != null &&
             b != null &&
             a.playerId == b.playerId &&
             a.dimensionId == b.dimensionId &&
             Mathf.Approximately(a.localX, b.localX) &&
             Mathf.Approximately(a.localY, b.localY) &&
             Mathf.Approximately(a.absoluteX, b.absoluteX) &&
             Mathf.Approximately(a.absoluteY, b.absoluteY);
    }

    private static bool IsSamePlayerVisit(
        DimensionPlayerVisitStateRecord a,
        DimensionPlayerVisitStateRecord b)
    {
      return a != null &&
             b != null &&
             a.playerId == b.playerId &&
             a.dimensionId == b.dimensionId &&
             Mathf.Approximately(a.localX, b.localX) &&
             Mathf.Approximately(a.localY, b.localY) &&
             Mathf.Approximately(a.absoluteX, b.absoluteX) &&
             Mathf.Approximately(a.absoluteY, b.absoluteY);
    }
  }
}
