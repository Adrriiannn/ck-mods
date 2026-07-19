using Unity.Collections;
using Unity.NetCode;

namespace ExpandNullforge.Networking
{
  public struct DimensionPlayerContextRequestRpc : IRpcCommand
  {
    public uint RequestId;
    public byte IncludePersistedFallback;
    public FixedString128Bytes Reason;
  }

  public struct DimensionPlayerContextSnapshotRpc : IRpcCommand
  {
    public uint RequestId;
    public byte Known;
    public byte PersistedFallback;
    public FixedString64Bytes Code;
    public FixedString128Bytes Message;
    public FixedString64Bytes DimensionId;
    public float AbsoluteX;
    public float AbsoluteY;
    public float LocalX;
    public float LocalY;
  }
}
