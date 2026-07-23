using Unity.Collections;
using Unity.NetCode;

public struct ChunkLoaderDetailsRequestRpc : IRpcCommand
{
  public ulong RegistrationId;
  public long LastKnownLogSequence;
  public byte IncludeSamples;
  public byte IsLive;
}

public struct ChunkLoaderDetailsBeginRpc : IRpcCommand
{
  public ulong RegistrationId;
  public long CapturedAtUtcTicks;
  public int TotalEntities;
  public int Objects;
  public int Enemies;
  public int Players;
  public int Samples;
  public int LogLines;
  public byte IncludesSamples;
  public ushort ErrorCode;
  public FixedString128Bytes ErrorText;
}

public struct ChunkLoaderSnapshotSampleRpc : IRpcCommand
{
  public ulong RegistrationId;
  public byte RelativeX;
  public byte RelativeY;
  public int ObjectId;
  public int Variation;
  public int Amount;
  public byte Flags;
}

public struct ChunkLoaderLogLineRpc : IRpcCommand
{
  public ulong RegistrationId;
  public long Sequence;
  public long TimestampUtcTicks;
  public FixedString32Bytes Category;
  public FixedString128Bytes Message;
}

public struct ChunkLoaderDetailsEndRpc : IRpcCommand
{
  public ulong RegistrationId;
}
