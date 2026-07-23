using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Pug.ECS.Components;
using PugMod;
using Unity.Entities;
using UnityEngine;

public static class ChunkLoaderRegistry
{
  [Serializable]
  private sealed class RegistryPayload
  {
    public int schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion;
    public string worldKey = string.Empty;
    public ulong registryRevision;
    public ulong nextRegistrationId = 1;
    public long nextDefaultNameNumber = 1;
    public List<ChunkLoaderRegistrationRecord> registrations = new();
  }

  [Serializable]
  private sealed class RegistryEnvelope
  {
    public long generation;
    public int schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion;
    public string worldKey = string.Empty;
    public string payload = string.Empty;
    public string payloadChecksum = string.Empty;
  }

  private const string FilePrefix = "ChunkLoaderMod_world_";
  private const string FileSuffixA = "_registry_A.json";
  private const string FileSuffixB = "_registry_B.json";

  private static RegistryPayload _state;
  private static string _worldKey;
  private static string _pathA;
  private static string _pathB;
  private static long _generation;
  private static bool _loaded;
  private static bool _dirty;
  private static bool _warnedUnavailable;
  private static double _nextFlushAt = double.PositiveInfinity;
  private static double _nextFlushFailureLogAt;
  private static int _consecutiveFlushFailures;

  public static event Action<ChunkLoaderRegistrationRecord> RecordChanged;
  public static event Action<ulong> RecordDeleted;
  public static event Action RegistryReloaded;

  public static bool IsLoaded => _loaded;
  public static ulong Revision => _state != null ? _state.registryRevision : 0;
  public static string WorldKey => _worldKey ?? string.Empty;

  public static void ResetLoadedState()
  {
    FinalizeActivePeriodsForShutdown();
    FlushNow();
    _state = null;
    _worldKey = null;
    _pathA = null;
    _pathB = null;
    _generation = 0;
    _loaded = false;
    _dirty = false;
    _warnedUnavailable = false;
    _nextFlushAt = double.PositiveInfinity;
    _nextFlushFailureLogAt = 0.0d;
    _consecutiveFlushFailures = 0;
  }

  public static void EnsureLoadedForCurrentWorld()
  {
    if (!TryGetPaths(out string worldKey, out string pathA, out string pathB))
    {
      if (!_warnedUnavailable)
      {
        Debug.LogWarning("[ChunkLoaderMod] Registry save path is not available yet.");
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

      if (chosen != null)
      {
        RegistryPayload loadedPayload =
            JsonConvert.DeserializeObject<RegistryPayload>(chosen.payload);
        if (loadedPayload != null)
        {
          _state = loadedPayload;
          _generation = chosen.generation;
        }
      }
      else if (hadRegistryBytes)
      {
        Debug.LogError(
            "[ChunkLoaderMod] Both registry generations were invalid. " +
            "The original bytes are being preserved before a clean registry is created.");
        TryWriteCorruptBackup(pathA, rawA);
        TryWriteCorruptBackup(pathB, rawB);
        _dirty = true;
      }

      NormalizeState(worldKey, resetRuntimeState: true);
      _loaded = true;
      if (_dirty)
      {
        FlushNow();
      }
      _warnedUnavailable = false;
      RegistryReloaded?.Invoke();
      Debug.Log(
          $"[ChunkLoaderMod] Registry loaded world={worldKey} generation={_generation} records={_state.registrations.Count}.");
    }
    catch (Exception ex)
    {
      Debug.LogError($"[ChunkLoaderMod] Registry load failed; preserving corrupt inputs. {ex}");
      TryWriteCorruptBackup(pathA, rawA);
      TryWriteCorruptBackup(pathB, rawB);
      _state = NewPayload(worldKey);
      NormalizeState(worldKey, resetRuntimeState: true);
      _loaded = true;
      _dirty = true;
      FlushNow();
      RegistryReloaded?.Invoke();
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
    if (!_dirty ||
        !_loaded ||
        _state == null ||
        API.ConfigFilesystem == null ||
        string.IsNullOrEmpty(_pathA) ||
        string.IsNullOrEmpty(_pathB))
    {
      return;
    }

    try
    {
      NormalizeState(_worldKey, resetRuntimeState: false);
      string payload = JsonConvert.SerializeObject(_state, Formatting.None);
      long nextGeneration = _generation + 1;
      RegistryEnvelope envelope = new RegistryEnvelope
      {
        generation = nextGeneration,
        schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion,
        worldKey = _worldKey,
        payload = payload,
        payloadChecksum = ComputeChecksum(payload)
      };

      string path = (nextGeneration & 1L) == 0L ? _pathA : _pathB;
      string json = JsonConvert.SerializeObject(envelope, Formatting.Indented);
      byte[] bytes = Encoding.UTF8.GetBytes(json);
      API.ConfigFilesystem.Write(path, bytes);

      RegistryEnvelope verified = TryReadEnvelope(path, out _);
      if (!IsEnvelopeValid(verified, _worldKey) ||
          verified.generation != nextGeneration)
      {
        throw new InvalidOperationException("Registry verification failed after write.");
      }

      _generation = nextGeneration;
      _dirty = false;
      _nextFlushAt = double.PositiveInfinity;
      _nextFlushFailureLogAt = 0.0d;
      _consecutiveFlushFailures = 0;
    }
    catch (Exception ex)
    {
      double now = Time.realtimeSinceStartupAsDouble;
      _consecutiveFlushFailures++;
      double retryDelay = Math.Min(
          ChunkLoaderConstants.RegistryFlushFailureMaximumRetrySeconds,
          ChunkLoaderConstants.RegistryFlushFailureInitialRetrySeconds *
          Math.Pow(2.0d, Math.Min(4, _consecutiveFlushFailures - 1)));
      _nextFlushAt = now + retryDelay;

      if (now >= _nextFlushFailureLogAt)
      {
        Debug.LogError(
            $"[ChunkLoaderMod] Registry save failed. " +
            $"Retrying in {retryDelay:0.#} seconds. {ex}");
        _nextFlushFailureLogAt =
            now +
            ChunkLoaderConstants.RegistryFlushFailureLogIntervalSeconds;
      }
    }
  }

  public static void GetRecords(List<ChunkLoaderRegistrationRecord> destination)
  {
    destination.Clear();
    EnsureLoadedForCurrentWorld();
    if (!_loaded || _state == null)
    {
      return;
    }

    for (int i = 0; i < _state.registrations.Count; i++)
    {
      destination.Add(_state.registrations[i].Clone());
    }
  }

  public static bool TryGetRecord(ulong registrationId, out ChunkLoaderRegistrationRecord record)
  {
    EnsureLoadedForCurrentWorld();
    ChunkLoaderRegistrationRecord found = FindById(registrationId);
    record = found != null ? found.Clone() : null;
    return found != null;
  }

  public static bool TryGetRecord(
      ChunkCoordinate coordinate,
      out ChunkLoaderRegistrationRecord record)
  {
    EnsureLoadedForCurrentWorld();
    ChunkLoaderRegistrationRecord found = FindByCoordinate(coordinate);
    record = found != null ? found.Clone() : null;
    return found != null;
  }

  public static ChunkLoaderQuotaSummary GetQuotaSummary(ulong ownerId)
  {
    EnsureLoadedForCurrentWorld();

    int personalActive = 0;
    int worldActive = 0;
    int personalSaved = 0;
    if (_state != null)
    {
      for (int i = 0; i < _state.registrations.Count; i++)
      {
        ChunkLoaderRegistrationRecord record = _state.registrations[i];
        if (record.CountsAgainstActiveQuota)
        {
          worldActive++;
        }

        if (record.ownerPersistentId != ownerId)
        {
          continue;
        }

        personalSaved++;
        if (record.CountsAgainstActiveQuota)
        {
          personalActive++;
        }
      }
    }

    return new ChunkLoaderQuotaSummary(
        personalActive,
        ChunkLoaderSettings.Current.personalActiveLimit,
        worldActive,
        ChunkLoaderSettings.Current.worldActiveLimit,
        personalSaved,
        ChunkLoaderSettings.Current.personalSavedLimit);
  }

  public static ChunkLoaderMutationResult Create(
      ChunkLoaderActor actor,
      ChunkCoordinate coordinate)
  {
    EnsureLoadedForCurrentWorld();
    if (!CanMutate(actor, out ChunkLoaderMutationResult unavailable))
    {
      return unavailable;
    }

    int maximumChunkIndex =
        (int.MaxValue - ChunkLoaderConstants.ChunkSize) /
        ChunkLoaderConstants.ChunkSize;
    if (coordinate.X < -maximumChunkIndex ||
        coordinate.X > maximumChunkIndex ||
        coordinate.Y < -maximumChunkIndex ||
        coordinate.Y > maximumChunkIndex)
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.InvalidCoordinate,
          "The requested chunk coordinate is outside the supported world range.");
    }

    if (FindByCoordinate(coordinate) != null)
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.DuplicateChunk,
          "This chunk is already registered.");
    }

    ChunkLoaderQuotaSummary quota = GetQuotaSummary(actor.PersistentId);
    if (ChunkLoaderSettings.IsLimitReached(
            quota.PersonalSaved,
            quota.PersonalSavedLimit))
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.PersonalSavedLimitReached,
          "The saved chunk registration limit has been reached.");
    }

    if (ChunkLoaderSettings.IsLimitReached(
            quota.PersonalActive,
            quota.PersonalActiveLimit))
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.PersonalActiveLimitReached,
          "Your active chunk limit has been reached.");
    }

    if (ChunkLoaderSettings.IsLimitReached(
            quota.WorldActive,
            quota.WorldActiveLimit))
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.WorldActiveLimitReached,
          "The server active chunk limit has been reached.");
    }

    DateTime now = DateTime.UtcNow;
    ChunkLoaderRegistrationRecord record = new ChunkLoaderRegistrationRecord
    {
      registrationId = NextRegistrationId(),
      schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion,
      worldNamespaceId = ChunkLoaderConstants.WorldNamespaceVanilla,
      chunkX = coordinate.X,
      chunkY = coordinate.Y,
      customName = ChunkLoaderConstants.DefaultNamePrefix + _state.nextDefaultNameNumber++,
      ownerPersistentId = actor.PersistentId,
      ownerDisplayNameAtCreation = SanitizeName(actor.DisplayName, 32, "Player"),
      createdAtUtcTicks = now.Ticks,
      lastEnabledAtUtcTicks = now.Ticks,
      desiredEnabled = true,
      actualState = ChunkLoaderRuntimeState.QueuedForLoad,
      revision = 1,
      activePeriodStartedUtcTicks = now.Ticks
    };

    _state.registrations.Add(record);
    TouchRegistry();
    PublishChanged(record);
    return ChunkLoaderMutationResult.Ok(record.Clone());
  }

  public static ChunkLoaderMutationResult Rename(
      ChunkLoaderActor actor,
      ulong registrationId,
      ulong expectedRevision,
      string requestedName)
  {
    EnsureLoadedForCurrentWorld();
    ChunkLoaderRegistrationRecord record = FindById(registrationId);
    ChunkLoaderMutationResult validation = ValidateOwnedMutation(actor, record, expectedRevision);
    if (!validation.Success)
    {
      return validation;
    }

    string name = SanitizeName(requestedName, ChunkLoaderConstants.MaxNameCharacters, string.Empty);
    if (string.IsNullOrWhiteSpace(name))
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.InvalidName,
          "Chunk names cannot be empty.");
    }

    if (record.customName == name)
    {
      return ChunkLoaderMutationResult.Ok(record.Clone());
    }

    record.customName = name;
    record.revision++;
    TouchRegistry();
    PublishChanged(record);
    return ChunkLoaderMutationResult.Ok(record.Clone());
  }

  public static ChunkLoaderMutationResult SetEnabled(
      ChunkLoaderActor actor,
      ulong registrationId,
      ulong expectedRevision,
      bool enabled)
  {
    EnsureLoadedForCurrentWorld();
    ChunkLoaderRegistrationRecord record = FindById(registrationId);
    ChunkLoaderMutationResult validation = ValidateOwnedMutation(actor, record, expectedRevision);
    if (!validation.Success)
    {
      return validation;
    }

    if (record.desiredEnabled == enabled)
    {
      return ChunkLoaderMutationResult.Ok(record.Clone());
    }

    if (enabled)
    {
      ChunkLoaderQuotaSummary quota = GetQuotaSummary(record.ownerPersistentId);
      if (ChunkLoaderSettings.IsLimitReached(
              quota.PersonalActive,
              quota.PersonalActiveLimit))
      {
        return ChunkLoaderMutationResult.Fail(
            ChunkLoaderErrorCode.PersonalActiveLimitReached,
            "The owner active chunk limit has been reached.");
      }

      if (ChunkLoaderSettings.IsLimitReached(
              quota.WorldActive,
              quota.WorldActiveLimit))
      {
        return ChunkLoaderMutationResult.Fail(
            ChunkLoaderErrorCode.WorldActiveLimitReached,
            "The server active chunk limit has been reached.");
      }
    }

    DateTime now = DateTime.UtcNow;
    record.desiredEnabled = enabled;
    record.actualState = enabled
        ? ChunkLoaderRuntimeState.QueuedForLoad
        : ChunkLoaderRuntimeState.Disabled;
    record.lastErrorCode = ChunkLoaderErrorCode.None;
    record.lastErrorText = string.Empty;

    if (enabled)
    {
      record.lastEnabledAtUtcTicks = now.Ticks;
      record.activePeriodStartedUtcTicks = now.Ticks;
    }
    else
    {
      record.lastDisabledAtUtcTicks = now.Ticks;
      AccumulateActiveTime(record, now.Ticks);
    }

    record.revision++;
    TouchRegistry();
    PublishChanged(record);
    return ChunkLoaderMutationResult.Ok(record.Clone());
  }

  public static ChunkLoaderMutationResult Delete(
      ChunkLoaderActor actor,
      ulong registrationId,
      ulong expectedRevision)
  {
    EnsureLoadedForCurrentWorld();
    ChunkLoaderRegistrationRecord record = FindById(registrationId);
    ChunkLoaderMutationResult validation = ValidateOwnedMutation(actor, record, expectedRevision);
    if (!validation.Success)
    {
      return validation;
    }

    _state.registrations.Remove(record);
    TouchRegistry();
    RecordDeleted?.Invoke(registrationId);
    return ChunkLoaderMutationResult.Ok(record.Clone());
  }

  public static bool SetRuntimeState(
      ulong registrationId,
      ChunkLoaderRuntimeState state,
      ChunkLoaderErrorCode errorCode = ChunkLoaderErrorCode.None,
      string errorText = "")
  {
    EnsureLoadedForCurrentWorld();
    ChunkLoaderRegistrationRecord record = FindById(registrationId);
    if (record == null)
    {
      return false;
    }

    string normalizedError = errorCode == ChunkLoaderErrorCode.None
        ? string.Empty
        : SanitizeName(errorText, 120, errorCode.ToString());

    if (record.actualState == state &&
        record.lastErrorCode == errorCode &&
        record.lastErrorText == normalizedError)
    {
      return false;
    }

    record.actualState = state;
    record.lastErrorCode = errorCode;
    record.lastErrorText = normalizedError;
    record.revision++;
    TouchRegistry();
    PublishChanged(record);
    return true;
  }

  private static ChunkLoaderMutationResult ValidateOwnedMutation(
      ChunkLoaderActor actor,
      ChunkLoaderRegistrationRecord record,
      ulong expectedRevision)
  {
    if (!CanMutate(actor, out ChunkLoaderMutationResult unavailable))
    {
      return unavailable;
    }

    if (record == null)
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.RegistrationNotFound,
          "The chunk registration no longer exists.");
    }

    if (!actor.IsAdmin && record.ownerPersistentId != actor.PersistentId)
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.PermissionDenied,
          "Only the owner or a server administrator may change this chunk.");
    }

    if (expectedRevision != 0 && record.revision != expectedRevision)
    {
      return ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.StaleRevision,
          "The chunk changed on the server. Refresh and try again.");
    }

    return ChunkLoaderMutationResult.Ok(record.Clone());
  }

  private static bool CanMutate(
      ChunkLoaderActor actor,
      out ChunkLoaderMutationResult failure)
  {
    if (!_loaded || _state == null)
    {
      failure = ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.RegistryUnavailable,
          "The world chunk registry is unavailable.");
      return false;
    }

    if (!actor.IsValid)
    {
      failure = ChunkLoaderMutationResult.Fail(
          ChunkLoaderErrorCode.PermissionDenied,
          "The requesting player could not be identified.");
      return false;
    }

    failure = default;
    return true;
  }

  private static void PublishChanged(ChunkLoaderRegistrationRecord record)
  {
    RecordChanged?.Invoke(record.Clone());
  }

  private static void TouchRegistry()
  {
    _state.registryRevision++;
    _dirty = true;
    _nextFlushAt = Time.realtimeSinceStartupAsDouble +
                   ChunkLoaderConstants.RegistryFlushDelaySeconds;
  }

  private static void AccumulateActiveTime(
      ChunkLoaderRegistrationRecord record,
      long stoppedAtTicks)
  {
    if (record.activePeriodStartedUtcTicks <= 0 ||
        stoppedAtTicks <= record.activePeriodStartedUtcTicks)
    {
      record.activePeriodStartedUtcTicks = 0;
      return;
    }

    record.totalActiveTicks += stoppedAtTicks - record.activePeriodStartedUtcTicks;
    record.activePeriodStartedUtcTicks = 0;
  }

  private static ulong NextRegistrationId()
  {
    ulong value = _state.nextRegistrationId++;
    if (value == 0)
    {
      value = _state.nextRegistrationId++;
    }

    return value;
  }

  private static ChunkLoaderRegistrationRecord FindById(ulong registrationId)
  {
    if (_state == null || _state.registrations == null)
    {
      return null;
    }

    for (int i = 0; i < _state.registrations.Count; i++)
    {
      if (_state.registrations[i].registrationId == registrationId)
      {
        return _state.registrations[i];
      }
    }

    return null;
  }

  private static ChunkLoaderRegistrationRecord FindByCoordinate(ChunkCoordinate coordinate)
  {
    if (_state == null || _state.registrations == null)
    {
      return null;
    }

    for (int i = 0; i < _state.registrations.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _state.registrations[i];
      if (record.chunkX == coordinate.X &&
          record.chunkY == coordinate.Y &&
          record.worldNamespaceId == ChunkLoaderConstants.WorldNamespaceVanilla)
      {
        return record;
      }
    }

    return null;
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
    if (API.ConfigFilesystem == null ||
        !API.ConfigFilesystem.FileExists(path))
    {
      return null;
    }

    raw = API.ConfigFilesystem.Read(path);
    if (raw == null || raw.Length == 0)
    {
      return null;
    }

    return JsonConvert.DeserializeObject<RegistryEnvelope>(
        Encoding.UTF8.GetString(raw));
  }

  private static bool IsEnvelopeValid(RegistryEnvelope envelope, string worldKey)
  {
    return envelope != null &&
           envelope.schemaVersion > 0 &&
           envelope.schemaVersion <= ChunkLoaderConstants.RegistrySchemaVersion &&
           envelope.worldKey == worldKey &&
           !string.IsNullOrEmpty(envelope.payload) &&
           envelope.payloadChecksum == ComputeChecksum(envelope.payload);
  }

  private static RegistryPayload NewPayload(string worldKey)
  {
    return new RegistryPayload
    {
      schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion,
      worldKey = worldKey,
      nextRegistrationId = 1,
      nextDefaultNameNumber = 1,
      registrations = new List<ChunkLoaderRegistrationRecord>()
    };
  }

  private static void NormalizeState(string worldKey, bool resetRuntimeState)
  {
    _state ??= NewPayload(worldKey);
    _state.schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion;
    _state.worldKey = worldKey ?? string.Empty;
    _state.registrations ??= new List<ChunkLoaderRegistrationRecord>();
    if (_state.nextRegistrationId == 0)
    {
      _state.nextRegistrationId = 1;
    }

    if (_state.nextDefaultNameNumber <= 0)
    {
      _state.nextDefaultNameNumber = 1;
    }

    HashSet<ulong> ids = new HashSet<ulong>();
    HashSet<long> coordinates = new HashSet<long>();
    List<ChunkLoaderRegistrationRecord> normalized = new();
    ulong largestId = 0;
    for (int i = 0; i < _state.registrations.Count; i++)
    {
      ChunkLoaderRegistrationRecord existing = _state.registrations[i];
      if (existing != null)
      {
        largestId = Math.Max(largestId, existing.registrationId);
      }
    }
    ulong nextAvailableId = Math.Max(_state.nextRegistrationId, largestId + 1);
    long normalizedAtTicks = DateTime.UtcNow.Ticks;

    for (int i = 0; i < _state.registrations.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _state.registrations[i];
      if (record == null)
      {
        continue;
      }

      if (record.registrationId == 0 || !ids.Add(record.registrationId))
      {
        while (nextAvailableId == 0 || ids.Contains(nextAvailableId))
        {
          nextAvailableId++;
        }
        record.registrationId = nextAvailableId++;
        ids.Add(record.registrationId);
      }

      if (record.schemaVersion > 0 &&
          record.schemaVersion < 2)
      {
        ChunkCoordinate migrated =
            ChunkCoordinate.FromLegacy64Coordinate(
                record.chunkX,
                record.chunkY);
        record.chunkX = migrated.X;
        record.chunkY = migrated.Y;
      }

      long coordinateKey = record.Coordinate.ToKey();
      if (!coordinates.Add(coordinateKey))
      {
        continue;
      }

      largestId = Math.Max(largestId, record.registrationId);
      record.schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion;
      record.worldNamespaceId = ChunkLoaderConstants.WorldNamespaceVanilla;
      if (string.IsNullOrWhiteSpace(record.customName))
      {
        record.customName =
            ChunkLoaderConstants.DefaultNamePrefix + _state.nextDefaultNameNumber++;
      }
      else
      {
        record.customName = SanitizeName(
            record.customName,
            ChunkLoaderConstants.MaxNameCharacters,
            string.Empty);
      }
      record.ownerDisplayNameAtCreation = SanitizeName(
          record.ownerDisplayNameAtCreation,
          32,
          "Player");

      if (resetRuntimeState && record.desiredEnabled)
      {
        record.actualState = ChunkLoaderRuntimeState.QueuedForLoad;
        record.lastErrorCode = ChunkLoaderErrorCode.None;
        record.lastErrorText = string.Empty;
        record.activePeriodStartedUtcTicks = normalizedAtTicks;
      }
      else if (resetRuntimeState)
      {
        record.actualState = ChunkLoaderRuntimeState.Disabled;
        record.activePeriodStartedUtcTicks = 0;
      }

      if (record.revision == 0)
      {
        record.revision = 1;
      }

      normalized.Add(record);
    }

    _state.registrations = normalized;
    if (_state.nextRegistrationId <= largestId)
    {
      _state.nextRegistrationId = largestId + 1;
    }
    if (_state.nextRegistrationId < nextAvailableId)
    {
      _state.nextRegistrationId = nextAvailableId;
    }
  }

  private static void FinalizeActivePeriodsForShutdown()
  {
    if (!_loaded || _state?.registrations == null)
    {
      return;
    }

    long now = DateTime.UtcNow.Ticks;
    bool changed = false;
    for (int i = 0; i < _state.registrations.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _state.registrations[i];
      if (record.activePeriodStartedUtcTicks <= 0)
      {
        continue;
      }

      AccumulateActiveTime(record, now);
      changed = true;
    }

    if (changed)
    {
      _state.registryRevision++;
      _dirty = true;
      _nextFlushAt = 0.0d;
    }
  }

  private static bool TryGetPaths(
      out string worldKey,
      out string pathA,
      out string pathB)
  {
    worldKey = null;
    pathA = null;
    pathB = null;

    if (API.ConfigFilesystem == null)
    {
      return false;
    }

    worldKey = GetCurrentWorldKey();
    if (string.IsNullOrWhiteSpace(worldKey))
    {
      return false;
    }

    string safeWorldKey = SanitizeFilePart(worldKey);
    pathA = FilePrefix + safeWorldKey + FileSuffixA;
    pathB = FilePrefix + safeWorldKey + FileSuffixB;
    return true;
  }

  private static string GetCurrentWorldKey()
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
        using EntityQuery query =
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<ServerGuidCD>());
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
      catch (Exception ex)
      {
        Debug.LogWarning($"[ChunkLoaderMod] Could not read world GUID: {ex.Message}");
      }
    }

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
    StringBuilder builder = new StringBuilder(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
      char c = value[i];
      builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
    }

    return builder.ToString();
  }

  private static string SanitizeName(
      string value,
      int maxCharacters,
      string fallback)
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
      API.ConfigFilesystem.Write(
          path + ".corrupt-" + DateTime.UtcNow.Ticks,
          raw);
    }
    catch (Exception ex)
    {
      Debug.LogWarning($"[ChunkLoaderMod] Could not preserve corrupt registry: {ex.Message}");
    }
  }
}
