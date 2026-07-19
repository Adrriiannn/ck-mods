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
  public static class DimensionWorldRegistry
  {
    [Serializable]
    public sealed class DimensionDefinitionRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string dimensionId = string.Empty;
      public string displayName = string.Empty;
      public int absoluteOriginX;
      public int absoluteOriginY;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public int generationVersion;
      public int spaceKind;
      public int capabilities;
      public int lifecycleState;
      public bool builtIn;
    }

    [Serializable]
    public sealed class DimensionSlotPersistenceRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string dimensionId = string.Empty;
      public int absoluteOriginX;
      public int absoluteOriginY;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public int candidateIndex;
      public bool usedFixedOrigin;
      public long assignedUtcTicks;
      public string allocationCode = string.Empty;
      public string allocationMessage = string.Empty;
    }

    [Serializable]
    public sealed class DimensionPlayerStateRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string playerId = string.Empty;
      public string dimensionId = DimensionIds.Overworld;
      public float localX;
      public float localY;
      public float absoluteX;
      public float absoluteY;
      public long savedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionPlayerVisitStateRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string playerId = string.Empty;
      public string dimensionId = DimensionIds.Overworld;
      public float localX;
      public float localY;
      public float absoluteX;
      public float absoluteY;
      public long savedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionPortalRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string portalId = string.Empty;
      public string displayName = string.Empty;
      public string fromDimensionId = string.Empty;
      public float fromLocalX;
      public float fromLocalY;
      public string toDimensionId = string.Empty;
      public float toLocalX;
      public float toLocalY;
      public int state;
    }

    [Serializable]
    public sealed class DimensionMarkerRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string markerId = string.Empty;
      public string dimensionId = string.Empty;
      public float localX;
      public float localY;
      public string label = string.Empty;
      public string kind = string.Empty;
      public bool visible = true;
    }

    [Serializable]
    public sealed class DimensionAnchorRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string anchorId = string.Empty;
      public string displayName = string.Empty;
      public string dimensionId = string.Empty;
      public float localX;
      public float localY;
      public int kind;
      public int priority;
      public bool enabled = true;
    }

    [Serializable]
    public sealed class DimensionSceneRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string sceneId = string.Empty;
      public string displayName = string.Empty;
      public string dimensionId = string.Empty;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public string kind = string.Empty;
      public int priority;
      public int state;
    }

    [Serializable]
    public sealed class DimensionProgressFlagRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string flagId = string.Empty;
      public string dimensionId = string.Empty;
      public string category = string.Empty;
      public bool value;
      public long updatedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionGeneratedAreaRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string dimensionId = string.Empty;
      public int localMinX;
      public int localMinY;
      public int localMaxExclusiveX;
      public int localMaxExclusiveY;
      public int state;
      public float progress01;
      public string message = string.Empty;
      public long updatedUtcTicks;
    }

    [Serializable]
    public sealed class DimensionContentOwnershipRecord
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string contentPackId = string.Empty;
      public int recordKind;
      public string recordId = string.Empty;
      public string displayName = string.Empty;
      public string notes = string.Empty;
    }

    [Serializable]
    private sealed class RegistryPayload
    {
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string worldKey = string.Empty;
      public ulong registryRevision;
      public List<DimensionDefinitionRecord> dimensions = new List<DimensionDefinitionRecord>();
      public List<DimensionSlotPersistenceRecord> dimensionSlots =
          new List<DimensionSlotPersistenceRecord>();
      public List<DimensionPlayerStateRecord> players = new List<DimensionPlayerStateRecord>();
      public List<DimensionPlayerVisitStateRecord> playerVisits = new List<DimensionPlayerVisitStateRecord>();
      public List<DimensionPortalRecord> portals = new List<DimensionPortalRecord>();
      public List<DimensionMarkerRecord> markers = new List<DimensionMarkerRecord>();
      public List<DimensionAnchorRecord> anchors = new List<DimensionAnchorRecord>();
      public List<DimensionSceneRecord> scenes = new List<DimensionSceneRecord>();
      public List<DimensionProgressFlagRecord> progressFlags = new List<DimensionProgressFlagRecord>();
      public List<DimensionGeneratedAreaRecord> generatedAreas = new List<DimensionGeneratedAreaRecord>();
      public List<DimensionContentOwnershipRecord> contentOwnership = new List<DimensionContentOwnershipRecord>();
    }

    [Serializable]
    private sealed class RegistryEnvelope
    {
      public long generation;
      public int schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      public string worldKey = string.Empty;
      public string payload = string.Empty;
      public string payloadChecksum = string.Empty;
    }

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

    public static DimensionRegistrySnapshot GetSnapshot()
    {
      EnsureLoadedForCurrentWorld();
      RegistryPayload state = _state;
      return new DimensionRegistrySnapshot(
          _loaded,
          _dirty,
          _dirty && !double.IsPositiveInfinity(_nextFlushAt),
          state != null ? state.schemaVersion : DimensionRegistryConstants.RegistrySchemaVersion,
          _generation,
          state != null ? state.registryRevision : 0UL,
          _consecutiveFlushFailures,
          _worldKey,
          CountOrZero(state != null ? state.dimensions : null),
          CountOrZero(state != null ? state.dimensionSlots : null),
          CountOrZero(state != null ? state.players : null),
          CountOrZero(state != null ? state.playerVisits : null),
          CountOrZero(state != null ? state.portals : null),
          CountOrZero(state != null ? state.markers : null),
          CountOrZero(state != null ? state.anchors : null),
          CountOrZero(state != null ? state.scenes : null),
          CountOrZero(state != null ? state.progressFlags : null),
          CountOrZero(state != null ? state.generatedAreas : null),
          CountOrZero(state != null ? state.contentOwnership : null));
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
          Debug.LogWarning(
              "[ExpandNullforge] Dimension registry save path is not available yet. " +
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
            Debug.LogError(
                "[ExpandNullforge] No readable dimension registry payload was available. " +
                "A clean registry will be created while preserving unreadable inputs.");
            _dirty = true;
          }
        }
        else if (hadRegistryBytes)
        {
          Debug.LogError(
              "[ExpandNullforge] Both dimension registry generations were invalid. " +
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
            "[ExpandNullforge] Dimension registry loaded world=" +
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
        Debug.LogError("[ExpandNullforge] Dimension registry load failed; preserving corrupt inputs. " + ex);
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
          Debug.LogError(
              "[ExpandNullforge] Dimension registry save failed. Retrying in " +
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

    public static void GetDimensions(List<DimensionDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensions == null)
      {
        return;
      }

      for (int i = 0; i < _state.dimensions.Count; i++)
      {
        DimensionDefinitionRecord record = _state.dimensions[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToDefinition(record));
      }
    }

    public static void GetDimensionSlots(List<DimensionSlotRecord> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensionSlots == null)
      {
        return;
      }

      for (int i = 0; i < _state.dimensionSlots.Count; i++)
      {
        DimensionSlotPersistenceRecord record = _state.dimensionSlots[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToSlotRecord(record));
      }
    }

    public static bool TryGetDimensionSlot(string dimensionId, out DimensionSlotRecord slot)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded ||
          _state == null ||
          _state.dimensionSlots == null ||
          string.IsNullOrEmpty(dimensionId))
      {
        slot = default(DimensionSlotRecord);
        return false;
      }

      int index = FindDimensionSlotRecordIndex(dimensionId);
      if (index < 0)
      {
        slot = default(DimensionSlotRecord);
        return false;
      }

      slot = ToSlotRecord(_state.dimensionSlots[index]);
      return !string.IsNullOrEmpty(slot.DimensionId);
    }

    public static void GetPortals(List<DimensionPortalDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.portals == null)
      {
        return;
      }

      for (int i = 0; i < _state.portals.Count; i++)
      {
        DimensionPortalRecord record = _state.portals[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToPortal(record));
      }
    }

    public static void GetMarkers(List<DimensionMapMarker> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.markers == null)
      {
        return;
      }

      for (int i = 0; i < _state.markers.Count; i++)
      {
        DimensionMarkerRecord record = _state.markers[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToMarker(record));
      }
    }

    public static void GetAnchors(List<DimensionAnchorDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.anchors == null)
      {
        return;
      }

      for (int i = 0; i < _state.anchors.Count; i++)
      {
        DimensionAnchorRecord record = _state.anchors[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToAnchor(record));
      }
    }

    public static void GetScenes(List<DimensionSceneDefinition> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.scenes == null)
      {
        return;
      }

      for (int i = 0; i < _state.scenes.Count; i++)
      {
        DimensionSceneRecord record = _state.scenes[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToScene(record));
      }
    }

    public static void GetProgressFlags(List<DimensionProgressFlag> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.progressFlags == null)
      {
        return;
      }

      for (int i = 0; i < _state.progressFlags.Count; i++)
      {
        DimensionProgressFlagRecord record = _state.progressFlags[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToProgressFlag(record));
      }
    }

    public static void GetGeneratedAreas(List<DimensionGenerationStatus> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.generatedAreas == null)
      {
        return;
      }

      for (int i = 0; i < _state.generatedAreas.Count; i++)
      {
        DimensionGeneratedAreaRecord record = _state.generatedAreas[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToGenerationStatus(record));
      }
    }

    public static void GetContentOwnershipBindings(List<DimensionContentOwnershipBinding> destination)
    {
      destination.Clear();
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.contentOwnership == null)
      {
        return;
      }

      for (int i = 0; i < _state.contentOwnership.Count; i++)
      {
        DimensionContentOwnershipRecord record = _state.contentOwnership[i];
        if (record == null)
        {
          continue;
        }

        destination.Add(ToContentOwnership(record));
      }
    }

    public static void UpsertDimension(DimensionDefinition definition, bool builtIn)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.dimensions = _state.dimensions ?? new List<DimensionDefinitionRecord>();
      DimensionDefinitionRecord record = ToRecord(definition, builtIn);
      int index = FindDimensionRecordIndex(record.dimensionId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.dimensions[index], record))
        {
          return;
        }

        _state.dimensions[index] = record;
      }
      else
      {
        _state.dimensions.Add(record);
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

    public static void UpsertDimensionSlot(DimensionSlotRecord slot)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || string.IsNullOrEmpty(slot.DimensionId))
      {
        return;
      }

      _state.dimensionSlots =
          _state.dimensionSlots ?? new List<DimensionSlotPersistenceRecord>();
      DimensionSlotPersistenceRecord record = ToRecord(slot);
      int index = FindDimensionSlotRecordIndex(record.dimensionId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.dimensionSlots[index], record))
        {
          return;
        }

        _state.dimensionSlots[index] = record;
      }
      else
      {
        _state.dimensionSlots.Add(record);
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

    public static void RemoveDimensionSlot(string dimensionId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensionSlots == null)
      {
        return;
      }

      int index = FindDimensionSlotRecordIndex(dimensionId);
      if (index < 0)
      {
        return;
      }

      _state.dimensionSlots.RemoveAt(index);
      Touch();
    }

    public static void UpsertPortal(DimensionPortalDefinition portal)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.portals = _state.portals ?? new List<DimensionPortalRecord>();
      DimensionPortalRecord record = ToRecord(portal);
      int index = FindPortalRecordIndex(record.portalId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.portals[index], record))
        {
          return;
        }

        _state.portals[index] = record;
      }
      else
      {
        _state.portals.Add(record);
      }

      Touch();
    }

    public static void RemovePortal(string portalId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.portals == null)
      {
        return;
      }

      int index = FindPortalRecordIndex(portalId);
      if (index < 0)
      {
        return;
      }

      _state.portals.RemoveAt(index);
      Touch();
    }

    public static void UpsertMarker(DimensionMapMarker marker)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.markers = _state.markers ?? new List<DimensionMarkerRecord>();
      DimensionMarkerRecord record = ToRecord(marker);
      int index = FindMarkerRecordIndex(record.markerId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.markers[index], record))
        {
          return;
        }

        _state.markers[index] = record;
      }
      else
      {
        _state.markers.Add(record);
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

    public static void RemoveMarker(string markerId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.markers == null)
      {
        return;
      }

      int index = FindMarkerRecordIndex(markerId);
      if (index < 0)
      {
        return;
      }

      _state.markers.RemoveAt(index);
      Touch();
    }

    public static void UpsertAnchor(DimensionAnchorDefinition anchor)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.anchors = _state.anchors ?? new List<DimensionAnchorRecord>();
      DimensionAnchorRecord record = ToRecord(anchor);
      int index = FindAnchorRecordIndex(record.anchorId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.anchors[index], record))
        {
          return;
        }

        _state.anchors[index] = record;
      }
      else
      {
        _state.anchors.Add(record);
      }

      Touch();
    }

    public static void RemoveAnchor(string anchorId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.anchors == null)
      {
        return;
      }

      int index = FindAnchorRecordIndex(anchorId);
      if (index < 0)
      {
        return;
      }

      _state.anchors.RemoveAt(index);
      Touch();
    }

    public static void UpsertScene(DimensionSceneDefinition scene)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.scenes = _state.scenes ?? new List<DimensionSceneRecord>();
      DimensionSceneRecord record = ToRecord(scene);
      int index = FindSceneRecordIndex(record.sceneId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.scenes[index], record))
        {
          return;
        }

        _state.scenes[index] = record;
      }
      else
      {
        _state.scenes.Add(record);
      }

      Touch();
    }

    public static void RemoveScene(string sceneId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.scenes == null)
      {
        return;
      }

      int index = FindSceneRecordIndex(sceneId);
      if (index < 0)
      {
        return;
      }

      _state.scenes.RemoveAt(index);
      Touch();
    }

    public static void UpsertProgressFlag(DimensionProgressFlag flag)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.progressFlags = _state.progressFlags ?? new List<DimensionProgressFlagRecord>();
      DimensionProgressFlagRecord record = ToRecord(flag);
      int index = FindProgressFlagRecordIndex(record.flagId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.progressFlags[index], record))
        {
          return;
        }

        _state.progressFlags[index] = record;
      }
      else
      {
        _state.progressFlags.Add(record);
      }

      Touch();
    }

    public static void RemoveProgressFlag(string flagId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.progressFlags == null)
      {
        return;
      }

      int index = FindProgressFlagRecordIndex(flagId);
      if (index < 0)
      {
        return;
      }

      _state.progressFlags.RemoveAt(index);
      Touch();
    }

    public static void UpsertGeneratedArea(DimensionGenerationStatus status)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.generatedAreas = _state.generatedAreas ?? new List<DimensionGeneratedAreaRecord>();
      DimensionGeneratedAreaRecord record = ToRecord(status);
      int index = FindGeneratedAreaRecordIndex(
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)));
      if (index >= 0)
      {
        if (RecordsEqual(_state.generatedAreas[index], record))
        {
          return;
        }

        _state.generatedAreas[index] = record;
      }
      else
      {
        _state.generatedAreas.Add(record);
      }

      Touch();
    }

    public static void RemoveGeneratedArea(string dimensionId, DimensionBounds localBounds)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.generatedAreas == null)
      {
        return;
      }

      int index = FindGeneratedAreaRecordIndex(dimensionId, localBounds);
      if (index < 0)
      {
        return;
      }

      _state.generatedAreas.RemoveAt(index);
      Touch();
    }

    public static void UpsertContentOwnership(DimensionContentOwnershipBinding binding)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null)
      {
        return;
      }

      _state.contentOwnership =
          _state.contentOwnership ?? new List<DimensionContentOwnershipRecord>();
      DimensionContentOwnershipRecord record = ToRecord(binding);
      int index = FindContentOwnershipRecordIndex(record.recordKind, record.recordId);
      if (index >= 0)
      {
        if (RecordsEqual(_state.contentOwnership[index], record))
        {
          return;
        }

        _state.contentOwnership[index] = record;
      }
      else
      {
        _state.contentOwnership.Add(record);
      }

      Touch();
    }

    public static void RemoveContentOwnership(DimensionContentRecordKind recordKind, string recordId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.contentOwnership == null)
      {
        return;
      }

      int index = FindContentOwnershipRecordIndex((int)recordKind, recordId);
      if (index < 0)
      {
        return;
      }

      _state.contentOwnership.RemoveAt(index);
      Touch();
    }

    private static bool RecordsEqual(DimensionDefinitionRecord a, DimensionDefinitionRecord b)
    {
      return a != null &&
             b != null &&
             a.dimensionId == b.dimensionId &&
             a.displayName == b.displayName &&
             a.absoluteOriginX == b.absoluteOriginX &&
             a.absoluteOriginY == b.absoluteOriginY &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.generationVersion == b.generationVersion &&
             a.spaceKind == b.spaceKind &&
             a.capabilities == b.capabilities &&
             a.lifecycleState == b.lifecycleState &&
             a.builtIn == b.builtIn;
    }

    private static bool RecordsEqual(
        DimensionSlotPersistenceRecord a,
        DimensionSlotPersistenceRecord b)
    {
      return a != null &&
             b != null &&
             a.dimensionId == b.dimensionId &&
             a.absoluteOriginX == b.absoluteOriginX &&
             a.absoluteOriginY == b.absoluteOriginY &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.candidateIndex == b.candidateIndex &&
             a.usedFixedOrigin == b.usedFixedOrigin &&
             a.assignedUtcTicks == b.assignedUtcTicks &&
             a.allocationCode == b.allocationCode &&
             a.allocationMessage == b.allocationMessage;
    }

    private static bool RecordsEqual(DimensionPortalRecord a, DimensionPortalRecord b)
    {
      return a != null &&
             b != null &&
             a.portalId == b.portalId &&
             a.displayName == b.displayName &&
             a.fromDimensionId == b.fromDimensionId &&
             a.fromLocalX == b.fromLocalX &&
             a.fromLocalY == b.fromLocalY &&
             a.toDimensionId == b.toDimensionId &&
             a.toLocalX == b.toLocalX &&
             a.toLocalY == b.toLocalY &&
             a.state == b.state;
    }

    private static bool RecordsEqual(DimensionMarkerRecord a, DimensionMarkerRecord b)
    {
      return a != null &&
             b != null &&
             a.markerId == b.markerId &&
             a.dimensionId == b.dimensionId &&
             a.localX == b.localX &&
             a.localY == b.localY &&
             a.label == b.label &&
             a.kind == b.kind &&
             a.visible == b.visible;
    }

    private static bool RecordsEqual(DimensionAnchorRecord a, DimensionAnchorRecord b)
    {
      return a != null &&
             b != null &&
             a.anchorId == b.anchorId &&
             a.displayName == b.displayName &&
             a.dimensionId == b.dimensionId &&
             a.localX == b.localX &&
             a.localY == b.localY &&
             a.kind == b.kind &&
             a.priority == b.priority &&
             a.enabled == b.enabled;
    }

    private static bool RecordsEqual(DimensionSceneRecord a, DimensionSceneRecord b)
    {
      return a != null &&
             b != null &&
             a.sceneId == b.sceneId &&
             a.displayName == b.displayName &&
             a.dimensionId == b.dimensionId &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.kind == b.kind &&
             a.priority == b.priority &&
             a.state == b.state;
    }

    private static bool RecordsEqual(DimensionProgressFlagRecord a, DimensionProgressFlagRecord b)
    {
      return a != null &&
             b != null &&
             a.flagId == b.flagId &&
             a.dimensionId == b.dimensionId &&
             a.category == b.category &&
             a.value == b.value &&
             a.updatedUtcTicks == b.updatedUtcTicks;
    }

    private static bool RecordsEqual(DimensionGeneratedAreaRecord a, DimensionGeneratedAreaRecord b)
    {
      return a != null &&
             b != null &&
             a.dimensionId == b.dimensionId &&
             a.localMinX == b.localMinX &&
             a.localMinY == b.localMinY &&
             a.localMaxExclusiveX == b.localMaxExclusiveX &&
             a.localMaxExclusiveY == b.localMaxExclusiveY &&
             a.state == b.state &&
             a.progress01 == b.progress01 &&
             a.message == b.message;
    }

    private static bool RecordsEqual(DimensionContentOwnershipRecord a, DimensionContentOwnershipRecord b)
    {
      return a != null &&
             b != null &&
             a.contentPackId == b.contentPackId &&
             a.recordKind == b.recordKind &&
             a.recordId == b.recordId &&
             a.displayName == b.displayName &&
             a.notes == b.notes;
    }

    private static int CountOrZero<T>(List<T> list)
    {
      return list != null ? list.Count : 0;
    }

    public static void RemoveDimension(string dimensionId)
    {
      EnsureLoadedForCurrentWorld();
      if (!_loaded || _state == null || _state.dimensions == null)
      {
        return;
      }

      int index = FindDimensionRecordIndex(dimensionId);
      if (index < 0)
      {
        return;
      }

      _state.dimensions.RemoveAt(index);
      if (_state.dimensionSlots != null)
      {
        int slotIndex = FindDimensionSlotRecordIndex(dimensionId);
        if (slotIndex >= 0)
        {
          _state.dimensionSlots.RemoveAt(slotIndex);
        }
      }

      Touch();
    }

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

    private static int FindDimensionRecordIndex(string dimensionId)
    {
      if (_state == null || _state.dimensions == null || string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.dimensions.Count; i++)
      {
        DimensionDefinitionRecord record = _state.dimensions[i];
        if (record != null && string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindDimensionSlotRecordIndex(string dimensionId)
    {
      if (_state == null || _state.dimensionSlots == null || string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.dimensionSlots.Count; i++)
      {
        DimensionSlotPersistenceRecord record = _state.dimensionSlots[i];
        if (record != null &&
            string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindPortalRecordIndex(string portalId)
    {
      if (_state == null || _state.portals == null || string.IsNullOrEmpty(portalId))
      {
        return -1;
      }

      for (int i = 0; i < _state.portals.Count; i++)
      {
        DimensionPortalRecord record = _state.portals[i];
        if (record != null && string.Equals(record.portalId, portalId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindMarkerRecordIndex(string markerId)
    {
      if (_state == null || _state.markers == null || string.IsNullOrEmpty(markerId))
      {
        return -1;
      }

      for (int i = 0; i < _state.markers.Count; i++)
      {
        DimensionMarkerRecord record = _state.markers[i];
        if (record != null && string.Equals(record.markerId, markerId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindAnchorRecordIndex(string anchorId)
    {
      if (_state == null || _state.anchors == null || string.IsNullOrEmpty(anchorId))
      {
        return -1;
      }

      for (int i = 0; i < _state.anchors.Count; i++)
      {
        DimensionAnchorRecord record = _state.anchors[i];
        if (record != null && string.Equals(record.anchorId, anchorId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindSceneRecordIndex(string sceneId)
    {
      if (_state == null || _state.scenes == null || string.IsNullOrEmpty(sceneId))
      {
        return -1;
      }

      for (int i = 0; i < _state.scenes.Count; i++)
      {
        DimensionSceneRecord record = _state.scenes[i];
        if (record != null && string.Equals(record.sceneId, sceneId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindProgressFlagRecordIndex(string flagId)
    {
      if (_state == null || _state.progressFlags == null || string.IsNullOrEmpty(flagId))
      {
        return -1;
      }

      for (int i = 0; i < _state.progressFlags.Count; i++)
      {
        DimensionProgressFlagRecord record = _state.progressFlags[i];
        if (record != null && string.Equals(record.flagId, flagId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindPlayerRecordIndex(string playerId)
    {
      if (_state == null || _state.players == null || string.IsNullOrEmpty(playerId))
      {
        return -1;
      }

      for (int i = 0; i < _state.players.Count; i++)
      {
        DimensionPlayerStateRecord record = _state.players[i];
        if (record != null && string.Equals(record.playerId, playerId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindPlayerVisitRecordIndex(string playerId, string dimensionId)
    {
      if (_state == null ||
          _state.playerVisits == null ||
          string.IsNullOrEmpty(playerId) ||
          string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.playerVisits.Count; i++)
      {
        DimensionPlayerVisitStateRecord record = _state.playerVisits[i];
        if (record != null &&
            string.Equals(record.playerId, playerId, StringComparison.Ordinal) &&
            string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindGeneratedAreaRecordIndex(string dimensionId, DimensionBounds localBounds)
    {
      if (_state == null || _state.generatedAreas == null || string.IsNullOrEmpty(dimensionId))
      {
        return -1;
      }

      for (int i = 0; i < _state.generatedAreas.Count; i++)
      {
        DimensionGeneratedAreaRecord record = _state.generatedAreas[i];
        if (record != null &&
            string.Equals(record.dimensionId, dimensionId, StringComparison.Ordinal) &&
            record.localMinX == localBounds.Min.x &&
            record.localMinY == localBounds.Min.y &&
            record.localMaxExclusiveX == localBounds.MaxExclusive.x &&
            record.localMaxExclusiveY == localBounds.MaxExclusive.y)
        {
          return i;
        }
      }

      return -1;
    }

    private static int FindContentOwnershipRecordIndex(int recordKind, string recordId)
    {
      if (_state == null ||
          _state.contentOwnership == null ||
          string.IsNullOrEmpty(recordId))
      {
        return -1;
      }

      for (int i = 0; i < _state.contentOwnership.Count; i++)
      {
        DimensionContentOwnershipRecord record = _state.contentOwnership[i];
        if (record != null &&
            record.recordKind == recordKind &&
            string.Equals(record.recordId, recordId, StringComparison.Ordinal))
        {
          return i;
        }
      }

      return -1;
    }

    private static DimensionDefinitionRecord ToRecord(DimensionDefinition definition, bool builtIn)
    {
      return new DimensionDefinitionRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        dimensionId = SanitizeName(definition.Id, 128, string.Empty),
        displayName = SanitizeName(definition.DisplayName, 128, definition.Id),
        absoluteOriginX = definition.AbsoluteOrigin.x,
        absoluteOriginY = definition.AbsoluteOrigin.y,
        localMinX = definition.LocalBounds.Min.x,
        localMinY = definition.LocalBounds.Min.y,
        localMaxExclusiveX = definition.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = definition.LocalBounds.MaxExclusive.y,
        generationVersion = Math.Max(1, definition.GenerationVersion),
        spaceKind = (int)definition.SpaceKind,
        capabilities = (int)definition.Capabilities,
        lifecycleState = (int)definition.LifecycleState,
        builtIn = builtIn
      };
    }

    private static DimensionPortalRecord ToRecord(DimensionPortalDefinition portal)
    {
      return new DimensionPortalRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        portalId = SanitizeName(portal.PortalId, 128, string.Empty),
        displayName = SanitizeName(portal.DisplayName, 128, portal.PortalId),
        fromDimensionId = SanitizeName(portal.FromDimensionId, 128, string.Empty),
        fromLocalX = portal.FromLocalPosition.x,
        fromLocalY = portal.FromLocalPosition.y,
        toDimensionId = SanitizeName(portal.ToDimensionId, 128, string.Empty),
        toLocalX = portal.ToLocalPosition.x,
        toLocalY = portal.ToLocalPosition.y,
        state = (int)portal.State
      };
    }

    private static DimensionSlotPersistenceRecord ToRecord(DimensionSlotRecord slot)
    {
      return new DimensionSlotPersistenceRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        dimensionId = SanitizeName(slot.DimensionId, 128, string.Empty),
        absoluteOriginX = slot.AbsoluteOrigin.x,
        absoluteOriginY = slot.AbsoluteOrigin.y,
        localMinX = slot.LocalBounds.Min.x,
        localMinY = slot.LocalBounds.Min.y,
        localMaxExclusiveX = slot.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = slot.LocalBounds.MaxExclusive.y,
        candidateIndex = slot.CandidateIndex,
        usedFixedOrigin = slot.UsedFixedOrigin,
        assignedUtcTicks = slot.AssignedUtcTicks,
        allocationCode = SanitizeName(slot.AllocationCode, 64, string.Empty),
        allocationMessage = SanitizeName(slot.AllocationMessage, 256, string.Empty)
      };
    }

    private static DimensionMarkerRecord ToRecord(DimensionMapMarker marker)
    {
      return new DimensionMarkerRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        markerId = SanitizeName(marker.MarkerId, 128, string.Empty),
        dimensionId = SanitizeName(marker.DimensionId, 128, string.Empty),
        localX = marker.LocalPosition.x,
        localY = marker.LocalPosition.y,
        label = SanitizeName(marker.Label, 128, marker.MarkerId),
        kind = SanitizeName(marker.Kind, 64, string.Empty),
        visible = marker.Visible
      };
    }

    private static DimensionAnchorRecord ToRecord(DimensionAnchorDefinition anchor)
    {
      return new DimensionAnchorRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        anchorId = SanitizeName(anchor.AnchorId, 128, string.Empty),
        displayName = SanitizeName(anchor.DisplayName, 128, anchor.AnchorId),
        dimensionId = SanitizeName(anchor.DimensionId, 128, string.Empty),
        localX = anchor.LocalPosition.x,
        localY = anchor.LocalPosition.y,
        kind = (int)anchor.Kind,
        priority = anchor.Priority,
        enabled = anchor.Enabled
      };
    }

    private static DimensionSceneRecord ToRecord(DimensionSceneDefinition scene)
    {
      return new DimensionSceneRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        sceneId = SanitizeName(scene.SceneId, 128, string.Empty),
        displayName = SanitizeName(scene.DisplayName, 128, scene.SceneId),
        dimensionId = SanitizeName(scene.DimensionId, 128, string.Empty),
        localMinX = scene.LocalBounds.Min.x,
        localMinY = scene.LocalBounds.Min.y,
        localMaxExclusiveX = scene.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = scene.LocalBounds.MaxExclusive.y,
        kind = SanitizeName(scene.Kind, 64, string.Empty),
        priority = scene.Priority,
        state = (int)scene.State
      };
    }

    private static DimensionProgressFlagRecord ToRecord(DimensionProgressFlag flag)
    {
      return new DimensionProgressFlagRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        flagId = SanitizeName(flag.FlagId, 128, string.Empty),
        dimensionId = SanitizeName(flag.DimensionId, 128, string.Empty),
        category = SanitizeName(flag.Category, 64, string.Empty),
        value = flag.Value,
        updatedUtcTicks = flag.UpdatedUtcTicks
      };
    }

    private static DimensionDefinition ToDefinition(DimensionDefinitionRecord record)
    {
      return new DimensionDefinition(
          record.dimensionId,
          record.displayName,
          new int2(record.absoluteOriginX, record.absoluteOriginY),
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          Math.Max(1, record.generationVersion),
          (DimensionSpaceKind)record.spaceKind,
          (DimensionCapabilityFlags)record.capabilities,
          (DimensionLifecycleState)record.lifecycleState);
    }

    private static DimensionPortalDefinition ToPortal(DimensionPortalRecord record)
    {
      return new DimensionPortalDefinition(
          record.portalId,
          record.displayName,
          record.fromDimensionId,
          new float2(record.fromLocalX, record.fromLocalY),
          record.toDimensionId,
          new float2(record.toLocalX, record.toLocalY),
          (DimensionPortalState)record.state);
    }

    private static DimensionSlotRecord ToSlotRecord(DimensionSlotPersistenceRecord record)
    {
      if (record == null)
      {
        return default(DimensionSlotRecord);
      }

      return new DimensionSlotRecord(
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          new int2(record.absoluteOriginX, record.absoluteOriginY),
          record.candidateIndex,
          record.usedFixedOrigin,
          record.assignedUtcTicks,
          record.allocationCode,
          record.allocationMessage);
    }

    private static DimensionPlayerVisitRecord ToPlayerVisit(DimensionPlayerVisitStateRecord record)
    {
      if (record == null)
      {
        return default(DimensionPlayerVisitRecord);
      }

      return new DimensionPlayerVisitRecord(
          record.playerId,
          record.dimensionId,
          new float2(record.localX, record.localY),
          new float2(record.absoluteX, record.absoluteY),
          record.savedUtcTicks);
    }

    private static int ComparePlayerVisits(
        DimensionPlayerVisitRecord left,
        DimensionPlayerVisitRecord right)
    {
      int saved = right.SavedUtcTicks.CompareTo(left.SavedUtcTicks);
      if (saved != 0)
      {
        return saved;
      }

      int player = string.Compare(left.PlayerId, right.PlayerId, StringComparison.Ordinal);
      if (player != 0)
      {
        return player;
      }

      return string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
    }

    private static DimensionMapMarker ToMarker(DimensionMarkerRecord record)
    {
      return new DimensionMapMarker(
          record.markerId,
          record.dimensionId,
          new float2(record.localX, record.localY),
          record.label,
          record.kind,
          record.visible);
    }

    private static DimensionAnchorDefinition ToAnchor(DimensionAnchorRecord record)
    {
      return new DimensionAnchorDefinition(
          record.anchorId,
          record.displayName,
          record.dimensionId,
          new float2(record.localX, record.localY),
          (DimensionAnchorKind)record.kind,
          record.priority,
          record.enabled);
    }

    private static DimensionSceneDefinition ToScene(DimensionSceneRecord record)
    {
      return new DimensionSceneDefinition(
          record.sceneId,
          record.displayName,
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          record.kind,
          record.priority,
          (DimensionSceneState)record.state);
    }

    private static DimensionProgressFlag ToProgressFlag(DimensionProgressFlagRecord record)
    {
      return new DimensionProgressFlag(
          record.flagId,
          record.dimensionId,
          record.category,
          record.value,
          record.updatedUtcTicks);
    }

    private static DimensionGeneratedAreaRecord ToRecord(DimensionGenerationStatus status)
    {
      return new DimensionGeneratedAreaRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        dimensionId = SanitizeName(status.DimensionId, 128, string.Empty),
        localMinX = status.LocalBounds.Min.x,
        localMinY = status.LocalBounds.Min.y,
        localMaxExclusiveX = status.LocalBounds.MaxExclusive.x,
        localMaxExclusiveY = status.LocalBounds.MaxExclusive.y,
        state = (int)status.State,
        progress01 = Mathf.Clamp01(status.Progress01),
        message = SanitizeName(status.Message, 256, string.Empty),
        updatedUtcTicks = DateTime.UtcNow.Ticks
      };
    }

    private static DimensionContentOwnershipRecord ToRecord(DimensionContentOwnershipBinding binding)
    {
      return new DimensionContentOwnershipRecord
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        contentPackId = SanitizeName(binding.ContentPackId, 128, string.Empty),
        recordKind = (int)binding.RecordKind,
        recordId = SanitizeName(binding.RecordId, 128, string.Empty),
        displayName = SanitizeName(binding.DisplayName, 128, string.Empty),
        notes = SanitizeName(binding.Notes, 256, string.Empty)
      };
    }

    private static DimensionGenerationStatus ToGenerationStatus(DimensionGeneratedAreaRecord record)
    {
      return new DimensionGenerationStatus(
          record.dimensionId,
          new DimensionBounds(
              new int2(record.localMinX, record.localMinY),
              new int2(record.localMaxExclusiveX, record.localMaxExclusiveY)),
          (DimensionGenerationState)record.state,
          Mathf.Clamp01(record.progress01),
          record.message);
    }

    private static DimensionContentOwnershipBinding ToContentOwnership(DimensionContentOwnershipRecord record)
    {
      return new DimensionContentOwnershipBinding(
          record.contentPackId,
          (DimensionContentRecordKind)record.recordKind,
          record.recordId,
          record.displayName,
          record.notes);
    }


    private static RegistryEnvelope ChooseNewestValid(
        RegistryEnvelope a,
        RegistryEnvelope b,
        string worldKey)
    {
      bool validA = IsEnvelopeValid(a, worldKey);
      bool validB = IsEnvelopeValid(b, worldKey);
      if (validA && validB)
      {
        return a.generation >= b.generation ? a : b;
      }

      if (validA)
      {
        return a;
      }

      return validB ? b : null;
    }

    private static RegistryEnvelope TryReadEnvelope(string path, out byte[] raw)
    {
      raw = null;
      if (API.ConfigFilesystem == null || !API.ConfigFilesystem.FileExists(path))
      {
        return null;
      }

      raw = API.ConfigFilesystem.Read(path);
      if (raw == null || raw.Length == 0)
      {
        return null;
      }

      return JsonConvert.DeserializeObject<RegistryEnvelope>(Encoding.UTF8.GetString(raw));
    }

    private static bool IsEnvelopeValid(RegistryEnvelope envelope, string worldKey)
    {
      return envelope != null &&
             IsRegistrySchemaReadable(envelope.schemaVersion) &&
             envelope.schemaVersion <= DimensionRegistryConstants.RegistrySchemaVersion &&
             envelope.worldKey == worldKey &&
             !string.IsNullOrEmpty(envelope.payload) &&
             envelope.payloadChecksum == ComputeChecksum(envelope.payload);
    }

    private static bool TryReadPayloadFromChosenEnvelope(
        RegistryEnvelope chosen,
        RegistryEnvelope envelopeA,
        RegistryEnvelope envelopeB,
        string pathA,
        string pathB,
        byte[] rawA,
        byte[] rawB,
        string worldKey,
        out RegistryPayload payload,
        out long generation)
    {
      if (TryReadPayloadCandidate(
              chosen,
              ReferenceEquals(chosen, envelopeA) ? pathA : pathB,
              ReferenceEquals(chosen, envelopeA) ? rawA : rawB,
              worldKey,
              out payload,
              out generation))
      {
        return true;
      }

      RegistryEnvelope fallback = ReferenceEquals(chosen, envelopeA) ? envelopeB : envelopeA;
      if (fallback == null)
      {
        payload = null;
        generation = 0L;
        return false;
      }

      return TryReadPayloadCandidate(
          fallback,
          ReferenceEquals(fallback, envelopeA) ? pathA : pathB,
          ReferenceEquals(fallback, envelopeA) ? rawA : rawB,
          worldKey,
          out payload,
          out generation);
    }

    private static bool TryReadPayloadCandidate(
        RegistryEnvelope envelope,
        string path,
        byte[] raw,
        string worldKey,
        out RegistryPayload payload,
        out long generation)
    {
      payload = null;
      generation = 0L;
      if (!IsEnvelopeValid(envelope, worldKey))
      {
        return false;
      }

      try
      {
        RegistryPayload candidate =
            JsonConvert.DeserializeObject<RegistryPayload>(envelope.payload);
        if (candidate == null)
        {
          Debug.LogError("[ExpandNullforge] Dimension registry payload was empty after deserialization.");
          TryWriteCorruptBackup(path, raw);
          return false;
        }

        if (!IsRegistrySchemaReadable(candidate.schemaVersion))
        {
          Debug.LogError(
              "[ExpandNullforge] Dimension registry payload schema " +
              candidate.schemaVersion +
              " is not readable by this framework build. Current schema=" +
              DimensionRegistryConstants.RegistrySchemaVersion +
              ". Preserving the unreadable registry before starting clean.");
          TryWriteCorruptBackup(path, raw);
          return false;
        }

        payload = candidate;
        generation = envelope.generation;
        return true;
      }
      catch (Exception ex)
      {
        Debug.LogError("[ExpandNullforge] Dimension registry payload failed to deserialize. " + ex);
        TryWriteCorruptBackup(path, raw);
        return false;
      }
    }

    private static bool IsRegistrySchemaReadable(int schemaVersion)
    {
      return schemaVersion >= DimensionRegistryConstants.RegistryMinimumReadableSchemaVersion &&
             schemaVersion <= DimensionRegistryConstants.RegistrySchemaVersion;
    }

    private static RegistryPayload NewPayload(string worldKey)
    {
      return new RegistryPayload
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        worldKey = worldKey ?? string.Empty,
        registryRevision = 0,
        dimensions = new List<DimensionDefinitionRecord>(),
        dimensionSlots = new List<DimensionSlotPersistenceRecord>(),
        players = new List<DimensionPlayerStateRecord>(),
        playerVisits = new List<DimensionPlayerVisitStateRecord>(),
        portals = new List<DimensionPortalRecord>(),
        markers = new List<DimensionMarkerRecord>(),
        anchors = new List<DimensionAnchorRecord>(),
        scenes = new List<DimensionSceneRecord>(),
        progressFlags = new List<DimensionProgressFlagRecord>(),
        generatedAreas = new List<DimensionGeneratedAreaRecord>(),
        contentOwnership = new List<DimensionContentOwnershipRecord>()
      };
    }

    private static void NormalizeState(string worldKey)
    {
      if (_state == null)
      {
        _state = NewPayload(worldKey);
        _dirty = true;
      }

      NormalizeSchemaVersion(ref _state.schemaVersion);
      string normalizedWorldKey = worldKey ?? string.Empty;
      if (!string.Equals(_state.worldKey, normalizedWorldKey, StringComparison.Ordinal))
      {
        _state.worldKey = normalizedWorldKey;
        _dirty = true;
      }

      EnsureList(ref _state.dimensions);
      EnsureList(ref _state.dimensionSlots);
      EnsureList(ref _state.players);
      EnsureList(ref _state.playerVisits);
      EnsureList(ref _state.portals);
      EnsureList(ref _state.markers);
      EnsureList(ref _state.anchors);
      EnsureList(ref _state.scenes);
      EnsureList(ref _state.progressFlags);
      EnsureList(ref _state.generatedAreas);
      EnsureList(ref _state.contentOwnership);

      NormalizeDimensionRecords();
      NormalizeDimensionSlotRecords();
      NormalizePlayerRecords();
      NormalizePlayerVisitRecords();
      NormalizePortalRecords();
      NormalizeMarkerRecords();
      NormalizeAnchorRecords();
      NormalizeSceneRecords();
      NormalizeProgressFlagRecords();
      NormalizeGeneratedAreaRecords();
      NormalizeContentOwnershipRecords();
    }

    private static void NormalizeSchemaVersion(ref int schemaVersion)
    {
      if (schemaVersion == DimensionRegistryConstants.RegistrySchemaVersion)
      {
        return;
      }

      schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion;
      _dirty = true;
    }

    private static void EnsureList<T>(ref List<T> list)
    {
      if (list != null)
      {
        return;
      }

      list = new List<T>();
      _dirty = true;
    }

    private static void ReplaceNormalizedList<T>(ref List<T> list, List<T> normalized)
    {
      if (list == null || normalized == null || list.Count != normalized.Count)
      {
        _dirty = true;
      }

      list = normalized ?? new List<T>();
    }

    private static void SetSanitizedName(ref string field, int maxCharacters, string fallback)
    {
      string sanitized = SanitizeName(field, maxCharacters, fallback);
      if (!string.Equals(field, sanitized, StringComparison.Ordinal))
      {
        field = sanitized;
        _dirty = true;
      }
    }

    private static void SetIntIfChanged(ref int field, int value)
    {
      if (field == value)
      {
        return;
      }

      field = value;
      _dirty = true;
    }

    private static void SetFloatIfChanged(ref float field, float value)
    {
      if (field == value)
      {
        return;
      }

      field = value;
      _dirty = true;
    }

    private static float NormalizeProgress01(float value)
    {
      if (float.IsNaN(value) || float.IsInfinity(value))
      {
        return 0.0f;
      }

      return Mathf.Clamp01(value);
    }

    private static void NormalizeDimensionRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionDefinitionRecord> normalized = new List<DimensionDefinitionRecord>();
      for (int i = 0; i < _state.dimensions.Count; i++)
      {
        DimensionDefinitionRecord record = _state.dimensions[i];
        if (record == null || string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.dimensionId);
        SetIntIfChanged(ref record.generationVersion, Math.Max(1, record.generationVersion));
        if (record.localMaxExclusiveX <= record.localMinX || record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        if (!ids.Add(record.dimensionId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.dimensions, normalized);
    }

    private static void NormalizeDimensionSlotRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionSlotPersistenceRecord> normalized =
          new List<DimensionSlotPersistenceRecord>();
      for (int i = 0; i < _state.dimensionSlots.Count; i++)
      {
        DimensionSlotPersistenceRecord record = _state.dimensionSlots[i];
        if (record == null || string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.allocationCode, 64, string.Empty);
        SetSanitizedName(ref record.allocationMessage, 256, string.Empty);
        if (record.localMaxExclusiveX <= record.localMinX ||
            record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        if (!ids.Add(record.dimensionId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.dimensionSlots, normalized);
    }

    private static void NormalizePortalRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionPortalRecord> normalized = new List<DimensionPortalRecord>();
      for (int i = 0; i < _state.portals.Count; i++)
      {
        DimensionPortalRecord record = _state.portals[i];
        if (record == null || string.IsNullOrEmpty(record.portalId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.portalId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.portalId);
        SetSanitizedName(ref record.fromDimensionId, 128, string.Empty);
        SetSanitizedName(ref record.toDimensionId, 128, string.Empty);
        if (!ids.Add(record.portalId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.portals, normalized);
    }

    private static void NormalizeMarkerRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionMarkerRecord> normalized = new List<DimensionMarkerRecord>();
      for (int i = 0; i < _state.markers.Count; i++)
      {
        DimensionMarkerRecord record = _state.markers[i];
        if (record == null || string.IsNullOrEmpty(record.markerId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.markerId, 128, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.label, 128, record.markerId);
        SetSanitizedName(ref record.kind, 64, string.Empty);
        if (!ids.Add(record.markerId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.markers, normalized);
    }

    private static void NormalizeAnchorRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionAnchorRecord> normalized = new List<DimensionAnchorRecord>();
      for (int i = 0; i < _state.anchors.Count; i++)
      {
        DimensionAnchorRecord record = _state.anchors[i];
        if (record == null || string.IsNullOrEmpty(record.anchorId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.anchorId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.anchorId);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        if (!IsValidAnchorKind((DimensionAnchorKind)record.kind))
        {
          SetIntIfChanged(ref record.kind, (int)DimensionAnchorKind.Fallback);
        }

        if (!ids.Add(record.anchorId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.anchors, normalized);
    }

    private static void NormalizeSceneRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionSceneRecord> normalized = new List<DimensionSceneRecord>();
      for (int i = 0; i < _state.scenes.Count; i++)
      {
        DimensionSceneRecord record = _state.scenes[i];
        if (record == null || string.IsNullOrEmpty(record.sceneId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.sceneId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, record.sceneId);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.kind, 64, string.Empty);
        if (record.localMaxExclusiveX <= record.localMinX ||
            record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        if (!IsValidSceneState((DimensionSceneState)record.state))
        {
          SetIntIfChanged(ref record.state, (int)DimensionSceneState.Planned);
        }

        if (!ids.Add(record.sceneId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.scenes, normalized);
    }

    private static void NormalizeProgressFlagRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionProgressFlagRecord> normalized = new List<DimensionProgressFlagRecord>();
      for (int i = 0; i < _state.progressFlags.Count; i++)
      {
        DimensionProgressFlagRecord record = _state.progressFlags[i];
        if (record == null || string.IsNullOrEmpty(record.flagId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.flagId, 128, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.category, 64, string.Empty);
        if (!ids.Add(record.flagId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.progressFlags, normalized);
    }

    private static bool IsValidAnchorKind(DimensionAnchorKind kind)
    {
      return kind == DimensionAnchorKind.Entry ||
             kind == DimensionAnchorKind.Return ||
             kind == DimensionAnchorKind.Respawn ||
             kind == DimensionAnchorKind.Fallback ||
             kind == DimensionAnchorKind.Checkpoint ||
             kind == DimensionAnchorKind.Portal ||
             kind == DimensionAnchorKind.Scene ||
             kind == DimensionAnchorKind.Debug;
    }

    private static bool IsValidSceneState(DimensionSceneState state)
    {
      return state == DimensionSceneState.Planned ||
             state == DimensionSceneState.Reserved ||
             state == DimensionSceneState.Generating ||
             state == DimensionSceneState.Ready ||
             state == DimensionSceneState.Disabled ||
             state == DimensionSceneState.Error;
    }

    private static void NormalizePlayerRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionPlayerStateRecord> normalized = new List<DimensionPlayerStateRecord>();
      for (int i = 0; i < _state.players.Count; i++)
      {
        DimensionPlayerStateRecord record = _state.players[i];
        if (record == null || string.IsNullOrEmpty(record.playerId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.playerId, 96, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, DimensionIds.Overworld);
        if (!ids.Add(record.playerId))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.players, normalized);
    }

    private static void NormalizePlayerVisitRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionPlayerVisitStateRecord> normalized =
          new List<DimensionPlayerVisitStateRecord>();
      for (int i = 0; i < _state.playerVisits.Count; i++)
      {
        DimensionPlayerVisitStateRecord record = _state.playerVisits[i];
        if (record == null ||
            string.IsNullOrEmpty(record.playerId) ||
            string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.playerId, 96, string.Empty);
        SetSanitizedName(ref record.dimensionId, 128, DimensionIds.Overworld);

        string key = record.playerId + "|" + record.dimensionId;
        if (!ids.Add(key))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.playerVisits, normalized);
    }

    private static void NormalizeGeneratedAreaRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionGeneratedAreaRecord> normalized = new List<DimensionGeneratedAreaRecord>();
      for (int i = 0; i < _state.generatedAreas.Count; i++)
      {
        DimensionGeneratedAreaRecord record = _state.generatedAreas[i];
        if (record == null || string.IsNullOrEmpty(record.dimensionId))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.dimensionId, 128, string.Empty);
        SetSanitizedName(ref record.message, 256, string.Empty);
        SetFloatIfChanged(ref record.progress01, NormalizeProgress01(record.progress01));
        if (record.localMaxExclusiveX <= record.localMinX || record.localMaxExclusiveY <= record.localMinY)
        {
          continue;
        }

        string key =
            record.dimensionId +
            "|" +
            record.localMinX +
            "," +
            record.localMinY +
            "," +
            record.localMaxExclusiveX +
            "," +
            record.localMaxExclusiveY;
        if (!ids.Add(key))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.generatedAreas, normalized);
    }

    private static void NormalizeContentOwnershipRecords()
    {
      HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
      List<DimensionContentOwnershipRecord> normalized =
          new List<DimensionContentOwnershipRecord>();
      for (int i = 0; i < _state.contentOwnership.Count; i++)
      {
        DimensionContentOwnershipRecord record = _state.contentOwnership[i];
        if (record == null ||
            string.IsNullOrEmpty(record.contentPackId) ||
            string.IsNullOrEmpty(record.recordId) ||
            !IsValidContentRecordKind((DimensionContentRecordKind)record.recordKind))
        {
          continue;
        }

        NormalizeSchemaVersion(ref record.schemaVersion);
        SetSanitizedName(ref record.contentPackId, 128, string.Empty);
        SetSanitizedName(ref record.recordId, 128, string.Empty);
        SetSanitizedName(ref record.displayName, 128, string.Empty);
        SetSanitizedName(ref record.notes, 256, string.Empty);

        string key = record.recordKind + "|" + record.recordId;
        if (!ids.Add(key))
        {
          continue;
        }

        normalized.Add(record);
      }

      ReplaceNormalizedList(ref _state.contentOwnership, normalized);
    }

    private static bool IsValidContentRecordKind(DimensionContentRecordKind kind)
    {
      return kind == DimensionContentRecordKind.Dimension ||
             kind == DimensionContentRecordKind.Portal ||
             kind == DimensionContentRecordKind.PortalPresentation ||
             kind == DimensionContentRecordKind.TravelRequirement ||
             kind == DimensionContentRecordKind.TravelRequirementEvaluator ||
             kind == DimensionContentRecordKind.MapLayer ||
             kind == DimensionContentRecordKind.MapMarker ||
             kind == DimensionContentRecordKind.Anchor ||
             kind == DimensionContentRecordKind.Biome ||
             kind == DimensionContentRecordKind.GenerationTable ||
             kind == DimensionContentRecordKind.GenerationTableEntry ||
             kind == DimensionContentRecordKind.ZoneDefinition ||
             kind == DimensionContentRecordKind.ZoneProvider ||
             kind == DimensionContentRecordKind.EnvironmentProfile ||
             kind == DimensionContentRecordKind.Scene ||
             kind == DimensionContentRecordKind.SceneTemplate ||
             kind == DimensionContentRecordKind.Encounter ||
             kind == DimensionContentRecordKind.ResourceNode ||
             kind == DimensionContentRecordKind.SpawnRule ||
             kind == DimensionContentRecordKind.WorldEvent ||
             kind == DimensionContentRecordKind.ProgressFlag ||
             kind == DimensionContentRecordKind.GenerationProvider ||
             kind == DimensionContentRecordKind.GenerationPass ||
             kind == DimensionContentRecordKind.AccessProvider ||
             kind == DimensionContentRecordKind.AssetReference ||
             kind == DimensionContentRecordKind.Custom;
    }

    private static bool TryGetPaths(out string worldKey, out string pathA, out string pathB)
    {
      worldKey = null;
      pathA = null;
      pathB = null;

      if (API.ConfigFilesystem == null)
      {
        return false;
      }

      worldKey = GetCurrentStableWorldKey();
      if (string.IsNullOrWhiteSpace(worldKey))
      {
        return false;
      }

      GetPathsForWorldKey(worldKey, out pathA, out pathB);
      return true;
    }

    private static bool TryLoadSlotFallbackForStableWorldKey(
        string stableWorldKey,
        out RegistryPayload payload)
    {
      payload = null;
      if (string.IsNullOrWhiteSpace(stableWorldKey) ||
          !stableWorldKey.StartsWith("guid-", StringComparison.Ordinal) ||
          API.ConfigFilesystem == null)
      {
        return false;
      }

      string slotWorldKey = GetSlotWorldKey();
      if (string.IsNullOrWhiteSpace(slotWorldKey) ||
          string.Equals(slotWorldKey, stableWorldKey, StringComparison.Ordinal))
      {
        return false;
      }

      string fallbackPathA;
      string fallbackPathB;
      GetPathsForWorldKey(slotWorldKey, out fallbackPathA, out fallbackPathB);

      byte[] fallbackRawA;
      byte[] fallbackRawB;
      RegistryEnvelope fallbackEnvelopeA = TryReadEnvelope(fallbackPathA, out fallbackRawA);
      RegistryEnvelope fallbackEnvelopeB = TryReadEnvelope(fallbackPathB, out fallbackRawB);
      RegistryEnvelope fallbackChosen =
          ChooseNewestValid(fallbackEnvelopeA, fallbackEnvelopeB, slotWorldKey);
      if (fallbackChosen == null)
      {
        return false;
      }

      long fallbackGeneration;
      if (!TryReadPayloadFromChosenEnvelope(
              fallbackChosen,
              fallbackEnvelopeA,
              fallbackEnvelopeB,
              fallbackPathA,
              fallbackPathB,
              fallbackRawA,
              fallbackRawB,
              slotWorldKey,
              out payload,
              out fallbackGeneration))
      {
        payload = null;
        return false;
      }

      DimensionFrameworkLog.Verbose(
          "[ExpandNullforge] Migrating provisional dimension registry from " +
          slotWorldKey +
          " to stable world key " +
          stableWorldKey +
          ".");
      return true;
    }

    private static void GetPathsForWorldKey(string worldKey, out string pathA, out string pathB)
    {
      string safeWorldKey = SanitizeFilePart(worldKey);
      pathA = DimensionRegistryConstants.FilePrefix + safeWorldKey + DimensionRegistryConstants.FileSuffixA;
      pathB = DimensionRegistryConstants.FilePrefix + safeWorldKey + DimensionRegistryConstants.FileSuffixB;
    }

    private static string GetCurrentStableWorldKey()
    {
      World world = API.Server != null ? API.Server.World : null;
      if ((world == null || !world.IsCreated) && Manager.ecs != null)
      {
        world = Manager.ecs.ServerWorld;
      }

      if (world != null && world.IsCreated)
      {
        try
        {
          EntityManager entityManager = world.EntityManager;
          using (EntityQuery query =
              entityManager.CreateEntityQuery(ComponentType.ReadOnly<ServerGuidCD>()))
          {
            if (query.CalculateEntityCount() > 0)
            {
              ServerGuidCD guid = query.GetSingleton<ServerGuidCD>();
              string value = guid.Value.ToString();
              if (!string.IsNullOrWhiteSpace(value))
              {
                return "guid-" + value;
              }
            }
          }
        }
        catch (Exception ex)
        {
          Debug.LogWarning(
              "[ExpandNullforge] Could not read stable world GUID yet: " +
              ex.Message);
        }
      }

      return null;
    }

    private static string GetSlotWorldKey()
    {
      return Manager.saves != null
          ? "slot-" + Manager.saves.GetWorldId()
          : null;
    }

    private static string ComputeChecksum(string value)
    {
      const ulong offset = 14695981039346656037UL;
      const ulong prime = 1099511628211UL;
      ulong hash = offset;
      byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
      for (int i = 0; i < bytes.Length; i++)
      {
        hash ^= bytes[i];
        hash *= prime;
      }

      return hash.ToString("X16");
    }

    private static string SanitizeFilePart(string value)
    {
      if (string.IsNullOrEmpty(value))
      {
        return string.Empty;
      }

      StringBuilder builder = new StringBuilder(value.Length);
      for (int i = 0; i < value.Length; i++)
      {
        char c = value[i];
        builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
      }

      return builder.ToString();
    }

    private static string SanitizeName(string value, int maxCharacters, string fallback)
    {
      if (string.IsNullOrEmpty(value))
      {
        return fallback ?? string.Empty;
      }

      StringBuilder builder = new StringBuilder(Math.Min(value.Length, maxCharacters));
      for (int i = 0; i < value.Length && builder.Length < maxCharacters; i++)
      {
        char c = value[i];
        if (!char.IsControl(c))
        {
          builder.Append(c);
        }
      }

      string result = builder.ToString().Trim();
      return string.IsNullOrEmpty(result) ? fallback ?? string.Empty : result;
    }

    private static void TryWriteCorruptBackup(string path, byte[] raw)
    {
      if (raw == null || raw.Length == 0 || API.ConfigFilesystem == null)
      {
        return;
      }

      try
      {
        API.ConfigFilesystem.Write(path + ".corrupt-" + DateTime.UtcNow.Ticks, raw);
      }
      catch (Exception ex)
      {
        Debug.LogWarning("[ExpandNullforge] Could not preserve corrupt dimension registry: " + ex.Message);
      }
    }
  }
}
