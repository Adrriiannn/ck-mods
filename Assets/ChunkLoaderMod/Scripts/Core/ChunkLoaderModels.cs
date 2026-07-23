using System;
using Newtonsoft.Json;

public enum ChunkLoaderRuntimeState : byte
{
  Disabled = 0,
  QueuedForLoad = 1,
  Loading = 2,
  Loaded = 3,
  QueuedForUnload = 4,
  Error = 5
}

public enum ChunkLoaderErrorCode : ushort
{
  None = 0,
  CompatibilityUnavailable = 1,
  RegistryUnavailable = 2,
  DuplicateChunk = 3,
  PersonalActiveLimitReached = 4,
  WorldActiveLimitReached = 5,
  PersonalSavedLimitReached = 6,
  PermissionDenied = 7,
  RegistrationNotFound = 8,
  StaleRevision = 9,
  InvalidName = 10,
  InvalidCoordinate = 11,
  ChunkNotGenerated = 12,
  LoadTimedOut = 13,
  RuntimeAnchorFailed = 14,
  InternalError = 15
}

public enum ChunkLoaderMutationAction : byte
{
  Create = 1,
  Rename = 2,
  SetEnabled = 3,
  Delete = 4
}

[Serializable]
[JsonObject(MemberSerialization.Fields)]
public sealed class ChunkLoaderRegistrationRecord
{
  public ulong registrationId;
  public int schemaVersion = ChunkLoaderConstants.RegistrySchemaVersion;
  public string worldNamespaceId = ChunkLoaderConstants.WorldNamespaceVanilla;
  public int chunkX;
  public int chunkY;
  public string customName = string.Empty;
  public ulong ownerPersistentId;
  public string ownerDisplayNameAtCreation = string.Empty;
  public long createdAtUtcTicks;
  public long lastEnabledAtUtcTicks;
  public long lastDisabledAtUtcTicks;
  public bool desiredEnabled;
  public ChunkLoaderRuntimeState actualState;
  public ChunkLoaderErrorCode lastErrorCode;
  public string lastErrorText = string.Empty;
  public ulong revision;
  public long totalActiveTicks;
  public long activePeriodStartedUtcTicks;

  [JsonIgnore]
  public ChunkCoordinate Coordinate =>
      new ChunkCoordinate(chunkX, chunkY);

  [JsonIgnore]
  public bool CountsAgainstActiveQuota =>
      desiredEnabled ||
      actualState == ChunkLoaderRuntimeState.QueuedForLoad ||
      actualState == ChunkLoaderRuntimeState.Loading ||
      actualState == ChunkLoaderRuntimeState.Loaded;

  public ChunkLoaderRegistrationRecord Clone()
  {
    return (ChunkLoaderRegistrationRecord)MemberwiseClone();
  }
}

public readonly struct ChunkLoaderActor
{
  public ChunkLoaderActor(ulong persistentId, string displayName, bool isAdmin)
  {
    PersistentId = persistentId;
    DisplayName = displayName ?? string.Empty;
    IsAdmin = isAdmin;
  }

  public ulong PersistentId { get; }
  public string DisplayName { get; }
  public bool IsAdmin { get; }
  public bool IsValid => PersistentId != 0;
}

public readonly struct ChunkLoaderQuotaSummary
{
  public ChunkLoaderQuotaSummary(
      int personalActive,
      int personalActiveLimit,
      int worldActive,
      int worldActiveLimit,
      int personalSaved,
      int personalSavedLimit)
  {
    PersonalActive = personalActive;
    PersonalActiveLimit = personalActiveLimit;
    WorldActive = worldActive;
    WorldActiveLimit = worldActiveLimit;
    PersonalSaved = personalSaved;
    PersonalSavedLimit = personalSavedLimit;
  }

  public int PersonalActive { get; }
  public int PersonalActiveLimit { get; }
  public int WorldActive { get; }
  public int WorldActiveLimit { get; }
  public int PersonalSaved { get; }
  public int PersonalSavedLimit { get; }
}

public readonly struct ChunkLoaderMutationResult
{
  public ChunkLoaderMutationResult(
      bool success,
      ChunkLoaderErrorCode errorCode,
      string message,
      ChunkLoaderRegistrationRecord record = null)
  {
    Success = success;
    ErrorCode = errorCode;
    Message = message ?? string.Empty;
    Record = record;
  }

  public bool Success { get; }
  public ChunkLoaderErrorCode ErrorCode { get; }
  public string Message { get; }
  public ChunkLoaderRegistrationRecord Record { get; }

  public static ChunkLoaderMutationResult Ok(ChunkLoaderRegistrationRecord record)
  {
    return new ChunkLoaderMutationResult(true, ChunkLoaderErrorCode.None, string.Empty, record);
  }

  public static ChunkLoaderMutationResult Fail(ChunkLoaderErrorCode code, string message)
  {
    return new ChunkLoaderMutationResult(false, code, message);
  }
}
