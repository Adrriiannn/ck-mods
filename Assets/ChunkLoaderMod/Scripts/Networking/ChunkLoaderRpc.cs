using Unity.Collections;
using Unity.NetCode;

public struct ChunkLoaderRegistryRequestRpc : IRpcCommand
{
  public byte IncludeAll;
}

public struct ChunkLoaderRegistrySnapshotBeginRpc : IRpcCommand
{
  public ulong RegistryRevision;
  public int RecordCount;
  public int PersonalActive;
  public int PersonalActiveLimit;
  public int WorldActive;
  public int WorldActiveLimit;
  public int PersonalSaved;
  public int PersonalSavedLimit;
  public float SnapshotCooldownSeconds;
  public byte SnapshotsEnabled;
  public byte TelemetryEnabled;
  public ulong ViewerPersistentId;
  public byte ViewerIsAdmin;
}

public struct ChunkLoaderQuotaUpdateRpc : IRpcCommand
{
  public int PersonalActive;
  public int PersonalActiveLimit;
  public int WorldActive;
  public int WorldActiveLimit;
  public int PersonalSaved;
  public int PersonalSavedLimit;
}

public struct ChunkLoaderRegistrationRpc : IRpcCommand
{
  public ulong RegistrationId;
  public int ChunkX;
  public int ChunkY;
  public FixedString64Bytes Name;
  public ulong OwnerPersistentId;
  public FixedString64Bytes OwnerName;
  public long CreatedAtUtcTicks;
  public long LastEnabledAtUtcTicks;
  public long LastDisabledAtUtcTicks;
  public long TotalActiveTicks;
  public byte DesiredEnabled;
  public byte ActualState;
  public ushort LastErrorCode;
  public FixedString128Bytes LastErrorText;
  public ulong Revision;
}

public struct ChunkLoaderRegistrationDeletedRpc : IRpcCommand
{
  public ulong RegistrationId;
}

public struct ChunkLoaderRegistrySnapshotEndRpc : IRpcCommand
{
  public ulong RegistryRevision;
}

public struct ChunkLoaderMutationRequestRpc : IRpcCommand
{
  public uint RequestId;
  public byte Action;
  public ulong RegistrationId;
  public ulong ExpectedRevision;
  public int ChunkX;
  public int ChunkY;
  public byte DesiredEnabled;
  public FixedString64Bytes Name;
}

public struct ChunkLoaderMutationResultRpc : IRpcCommand
{
  public uint RequestId;
  public byte Action;
  public byte Success;
  public ushort ErrorCode;
  public FixedString128Bytes Message;
  public byte HasRecord;
  public ChunkLoaderRegistrationRpc Record;
}
