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
  public static partial class DimensionWorldRegistry
  {

    private static RegistryPayload _state;
    private static string _worldKey;
    private static string _pathA;
    private static string _pathB;
    private static long _generation;
    private static bool _loaded;
    private static bool _dirty;
    private static bool _warnedUnavailable;
    private static bool _hasNonOverworldFootprint;
    private static double _nextFlushAt = double.PositiveInfinity;
    private static double _nextFlushFailureLogAt;
    private static int _consecutiveFlushFailures;

    public static event Action RegistryReloaded;

    public static bool IsLoaded
    {
      get { return _loaded; }
    }

    public static ulong Revision
    {
      get { return _state != null ? _state.registryRevision : 0UL; }
    }

    public static string WorldKey
    {
      get { return _worldKey ?? string.Empty; }
    }

    public static bool HasPendingFlush
    {
      get { return _dirty && !double.IsPositiveInfinity(_nextFlushAt); }
    }

    public static bool HasNonOverworldFootprint
    {
      get { return _loaded && _hasNonOverworldFootprint; }
    }

    public static int PlayerVisitCount
    {
      get
      {
        EnsureLoadedForCurrentWorld();
        if (!_loaded || _state == null || _state.playerVisits == null)
        {
          return 0;
        }

        return _state.playerVisits.Count;
      }
    }

    public static DimensionPersistenceHealthSnapshot GetPersistenceHealthSnapshot()
    {
      return CreatePersistenceHealthSnapshot(GetSnapshot());
    }

    public static DimensionOperationResult ForceFlushNow(string reason)
    {
      EnsureLoadedForCurrentWorld();
      return FlushNowInternal(reason ?? string.Empty);
    }

    public static DimensionOperationResult PrepareForWorldUnload(string reason)
    {
      DimensionOperationResult flushResult = ForceFlushNow(reason ?? "world-unload");
      if (!flushResult.Success)
      {
        return flushResult;
      }

      DimensionPersistenceHealthSnapshot health = CreatePersistenceHealthSnapshot(GetSnapshot());
      if (!health.ReadyForWorldUnload)
      {
        return DimensionOperationResult.Failed(health.Code, health.Message);
      }

      return new DimensionOperationResult(
          true,
          "persistence-ready-for-unload",
          "Dimension registry persistence is ready for world unload.");
    }

    public static void ResetLoadedState()
    {
      FlushNow();
      _state = null;
      _worldKey = null;
      _pathA = null;
      _pathB = null;
      _generation = 0;
      _loaded = false;
      _dirty = false;
      _warnedUnavailable = false;
      _hasNonOverworldFootprint = false;
      _nextFlushAt = double.PositiveInfinity;
      _nextFlushFailureLogAt = 0.0d;
      _consecutiveFlushFailures = 0;
    }

    public static void EnsureLoadedForCurrentWorld()
    {
      string worldKey;
      string pathA;
      string pathB;
      if (!TryGetPaths(out worldKey, out pathA, out pathB))
      {
        if (!_warnedUnavailable)
        {
          DimensionLog.Problem(DimensionLogChannels.Persist, null, 
              "Dimension registry save path is not available yet. " +
              "Waiting for the stable world GUID before loading or writing dimension state.");
          _warnedUnavailable = true;
        }

        return;
      }

      if (_loaded && _worldKey == worldKey)
      {
        return;
      }

      FlushNow();

      _worldKey = worldKey;
      _pathA = pathA;
      _pathB = pathB;
      _state = NewPayload(worldKey);
      _generation = 0;

      byte[] rawA = null;
      byte[] rawB = null;

      try
      {
        RegistryEnvelope envelopeA = TryReadEnvelope(pathA, out rawA);
        RegistryEnvelope envelopeB = TryReadEnvelope(pathB, out rawB);
        RegistryEnvelope chosen = ChooseNewestValid(envelopeA, envelopeB, worldKey);
        bool hadRegistryBytes =
            (rawA != null && rawA.Length > 0) ||
            (rawB != null && rawB.Length > 0);
        bool loadedFromSlotFallback = false;

        if (chosen != null)
        {
          RegistryPayload loadedPayload;
          long loadedGeneration;
          if (TryReadPayloadFromChosenEnvelope(
                  chosen,
                  envelopeA,
                  envelopeB,
                  pathA,
                  pathB,
                  rawA,
                  rawB,
                  worldKey,
                  out loadedPayload,
                  out loadedGeneration))
          {
            _state = loadedPayload;
            _generation = loadedGeneration;
          }
          else
          {
            DimensionLog.Fatal(DimensionLogChannels.Persist, null, 
                "No readable dimension registry payload was available. " +
                "A clean registry will be created while preserving unreadable inputs.");
            _dirty = true;
          }
        }
        else if (hadRegistryBytes)
        {
          DimensionLog.Fatal(DimensionLogChannels.Persist, null, 
              "Both dimension registry generations were invalid. " +
              "The original bytes are being preserved before a clean registry is created.");
          TryWriteCorruptBackup(pathA, rawA);
          TryWriteCorruptBackup(pathB, rawB);
          _dirty = true;
        }
        else if (!hadRegistryBytes &&
                 TryLoadSlotFallbackForStableWorldKey(worldKey, out RegistryPayload fallbackPayload))
        {
          _state = fallbackPayload;
          _generation = 0L;
          _dirty = true;
          loadedFromSlotFallback = true;
        }

        NormalizeState(worldKey);
        _loaded = true;
        RefreshNonOverworldFootprintCache();
        if (_dirty)
        {
          FlushNow();
        }

        _warnedUnavailable = false;
        Action handler = RegistryReloaded;
        if (handler != null)
        {
          handler();
        }

        DimensionFrameworkLog.Verbose(
            "Dimension registry loaded world=" +
            worldKey +
            " generation=" +
            _generation +
            " dimensions=" +
            _state.dimensions.Count +
            " slots=" +
            _state.dimensionSlots.Count +
            " players=" +
            _state.players.Count +
            " portals=" +
            _state.portals.Count +
            " markers=" +
            _state.markers.Count +
            " anchors=" +
            _state.anchors.Count +
            " scenes=" +
            _state.scenes.Count +
            " progressFlags=" +
            _state.progressFlags.Count +
            " generatedAreas=" +
            _state.generatedAreas.Count +
            " ownershipBindings=" +
            _state.contentOwnership.Count +
            (loadedFromSlotFallback ? " migratedFromSlotFallback=true" : string.Empty) +
            ".");
      }
      catch (Exception ex)
      {
        DimensionLog.Fatal(DimensionLogChannels.Persist, null, "Dimension registry load failed; preserving corrupt inputs. " + ex);
        TryWriteCorruptBackup(pathA, rawA);
        TryWriteCorruptBackup(pathB, rawB);
        _state = NewPayload(worldKey);
        NormalizeState(worldKey);
        _loaded = true;
        _dirty = true;
        FlushNow();

        Action handler = RegistryReloaded;
        if (handler != null)
        {
          handler();
        }
      }
    }

    public static void FlushIfDue()
    {
      if (_dirty && Time.realtimeSinceStartupAsDouble >= _nextFlushAt)
      {
        FlushNow();
      }
    }

    public static void FlushNow()
    {
      FlushNowInternal("scheduled");
    }

    private static DimensionOperationResult FlushNowInternal(string reason)
    {
      if (!_dirty ||
          !_loaded ||
          _state == null ||
          API.ConfigFilesystem == null ||
          string.IsNullOrEmpty(_pathA) ||
          string.IsNullOrEmpty(_pathB))
      {
        if (!_loaded || _state == null)
        {
          return new DimensionOperationResult(
              true,
              "persistence-not-loaded",
              "No dimension registry is loaded for the current world.");
        }

        if (!_dirty)
        {
          return new DimensionOperationResult(
              true,
              "persistence-clean",
              "Dimension registry has no pending changes.");
        }

        return DimensionOperationResult.Failed(
            "persistence-unavailable",
            "Dimension registry cannot be flushed because the save path is not available.");
      }

      try
      {
        NormalizeState(_worldKey);
        string payload = JsonConvert.SerializeObject(_state, Formatting.None);
        long nextGeneration = _generation + 1;
        RegistryEnvelope envelope = new RegistryEnvelope
        {
          generation = nextGeneration,
          schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
          worldKey = _worldKey,
          payload = payload,
          payloadChecksum = ComputeChecksum(payload)
        };

        string path = (nextGeneration & 1L) == 0L ? _pathA : _pathB;
        string json = JsonConvert.SerializeObject(envelope, Formatting.Indented);
        API.ConfigFilesystem.Write(path, Encoding.UTF8.GetBytes(json));

        byte[] verifiedRaw;
        RegistryEnvelope verified = TryReadEnvelope(path, out verifiedRaw);
        if (!IsEnvelopeValid(verified, _worldKey) || verified.generation != nextGeneration)
        {
          throw new InvalidOperationException("Dimension registry verification failed after write.");
        }

        _generation = nextGeneration;
        _dirty = false;
        _nextFlushAt = double.PositiveInfinity;
        _nextFlushFailureLogAt = 0.0d;
        _consecutiveFlushFailures = 0;
        return new DimensionOperationResult(
            true,
            "persistence-flushed",
            "Dimension registry flushed successfully.");
      }
      catch (Exception ex)
      {
        double now = Time.realtimeSinceStartupAsDouble;
        _consecutiveFlushFailures++;
        double retryDelay = Math.Min(
            DimensionRegistryConstants.FlushFailureMaximumRetrySeconds,
            DimensionRegistryConstants.FlushFailureInitialRetrySeconds *
            Math.Pow(2.0d, Math.Min(4, _consecutiveFlushFailures - 1)));
        _nextFlushAt = now + retryDelay;

        if (now >= _nextFlushFailureLogAt)
        {
          DimensionLog.Fatal(DimensionLogChannels.Persist, null, 
              "Dimension registry save failed. Retrying in " +
              retryDelay.ToString("0.#") +
              " seconds. " +
              ex);
          _nextFlushFailureLogAt =
              now + DimensionRegistryConstants.FlushFailureLogIntervalSeconds;
        }

        return DimensionOperationResult.Failed(
            "persistence-flush-failed",
            "Dimension registry save failed: " + ex.Message);
      }
    }

    private static DimensionPersistenceHealthSnapshot CreatePersistenceHealthSnapshot(
        DimensionRegistrySnapshot snapshot)
    {
      bool lastFlushHealthy = snapshot.ConsecutiveFlushFailures <= 0;
      bool readyForWorldUnload =
          !snapshot.IsDirty &&
          !snapshot.HasPendingFlush &&
          lastFlushHealthy;
      string code = "persistence-ready";
      string message = "Dimension registry persistence is clean.";

      if (!snapshot.IsLoaded)
      {
        code = "persistence-not-loaded";
        message = "No dimension registry is loaded for the current world.";
      }
      else if (snapshot.IsDirty)
      {
        code = "persistence-dirty";
        message = "Dimension registry has unsaved changes.";
      }
      else if (snapshot.HasPendingFlush)
      {
        code = "persistence-flush-pending";
        message = "Dimension registry has a scheduled flush pending.";
      }
      else if (!lastFlushHealthy)
      {
        code = "persistence-flush-failed";
        message =
            "Dimension registry has " +
            snapshot.ConsecutiveFlushFailures +
            " consecutive flush failure(s).";
      }

      return new DimensionPersistenceHealthSnapshot(
          snapshot,
          lastFlushHealthy,
          readyForWorldUnload,
          code,
          message);
    }

    private static void Touch()
    {
      Touch(true);
    }

    private static void Touch(bool refreshNonOverworldFootprint)
    {
      if (refreshNonOverworldFootprint)
      {
        RefreshNonOverworldFootprintCache();
      }

      if (_state != null)
      {
        _state.registryRevision++;
      }

      _dirty = true;
      _nextFlushAt = Time.realtimeSinceStartupAsDouble + DimensionRegistryConstants.FlushDelaySeconds;
    }

    private static bool IsNonOverworld(string dimensionId)
    {
      return !string.IsNullOrEmpty(dimensionId) &&
             !string.Equals(dimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
    }

    private static void RefreshNonOverworldFootprintCache()
    {
      if (!_loaded || _state == null)
      {
        _hasNonOverworldFootprint = false;
        return;
      }

      _hasNonOverworldFootprint =
          ContainsNonOverworldDimensionSlot(_state.dimensionSlots) ||
          ContainsNonOverworldGeneratedArea(_state.generatedAreas) ||
          ContainsNonOverworldPlayerState(_state.players) ||
          ContainsNonOverworldPlayerVisit(_state.playerVisits);
    }

    private static bool ContainsNonOverworldDimensionSlot(
        List<DimensionSlotPersistenceRecord> records)
    {
      if (records == null)
      {
        return false;
      }

      for (int i = 0; i < records.Count; i++)
      {
        DimensionSlotPersistenceRecord record = records[i];
        if (record != null && IsNonOverworldDimensionId(record.dimensionId))
        {
          return true;
        }
      }

      return false;
    }

    private static bool ContainsNonOverworldGeneratedArea(
        List<DimensionGeneratedAreaRecord> records)
    {
      if (records == null)
      {
        return false;
      }

      for (int i = 0; i < records.Count; i++)
      {
        DimensionGeneratedAreaRecord record = records[i];
        if (record != null && IsNonOverworldDimensionId(record.dimensionId))
        {
          return true;
        }
      }

      return false;
    }

    private static bool ContainsNonOverworldPlayerState(
        List<DimensionPlayerStateRecord> records)
    {
      if (records == null)
      {
        return false;
      }

      for (int i = 0; i < records.Count; i++)
      {
        DimensionPlayerStateRecord record = records[i];
        if (record != null && IsNonOverworldDimensionId(record.dimensionId))
        {
          return true;
        }
      }

      return false;
    }

    private static bool ContainsNonOverworldPlayerVisit(
        List<DimensionPlayerVisitStateRecord> records)
    {
      if (records == null)
      {
        return false;
      }

      for (int i = 0; i < records.Count; i++)
      {
        DimensionPlayerVisitStateRecord record = records[i];
        if (record != null && IsNonOverworldDimensionId(record.dimensionId))
        {
          return true;
        }
      }

      return false;
    }

    private static bool IsNonOverworldDimensionId(string dimensionId)
    {
      return !string.IsNullOrEmpty(dimensionId) &&
             !string.Equals(dimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
    }
  }
}
