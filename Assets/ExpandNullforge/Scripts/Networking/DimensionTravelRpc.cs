using Unity.Collections;
using Unity.NetCode;

namespace ExpandNullforge.Networking
{
  public struct DimensionTravelRequestRpc : IRpcCommand
  {
    public uint RequestId;
    public FixedString64Bytes TargetDimensionId;
    public float TargetLocalX;
    public float TargetLocalY;
    public byte RequireGeneratedArea;
    public byte AllowFallbackPosition;
    public FixedString128Bytes Reason;
  }

  public struct DimensionPortalTravelRequestRpc : IRpcCommand
  {
    public uint RequestId;
    public FixedString64Bytes PortalId;
    public byte RequireGeneratedArea;
    public byte AllowFallbackPosition;
    public FixedString128Bytes Reason;
  }

  public struct DimensionTravelResultRpc : IRpcCommand
  {
    public uint RequestId;
    public byte Accepted;
    public byte Final;
    public FixedString64Bytes Code;
    public FixedString128Bytes Message;
    public FixedString64Bytes TravelId;
    public FixedString64Bytes LoadTicketId;
    public FixedString64Bytes TargetDimensionId;
    public float TargetLocalX;
    public float TargetLocalY;
    public float TargetAbsoluteX;
    public float TargetAbsoluteY;
  }

  public struct DimensionTravelCancelRequestRpc : IRpcCommand
  {
    public uint RequestId;
    public FixedString64Bytes TravelId;
    public FixedString128Bytes Reason;
  }

  public struct DimensionTravelCancelResultRpc : IRpcCommand
  {
    public uint RequestId;
    public byte Accepted;
    public FixedString64Bytes Code;
    public FixedString128Bytes Message;
    public FixedString64Bytes TravelId;
  }
}
