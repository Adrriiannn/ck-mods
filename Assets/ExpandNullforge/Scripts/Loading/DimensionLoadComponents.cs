using Unity.Entities;

namespace ExpandNullforge.Loading
{
  public struct DimensionLoadAnchorCD : IComponentData
  {
    public ulong TicketHash;
    public int AbsoluteMinX;
    public int AbsoluteMinY;
    public int AbsoluteMaxExclusiveX;
    public int AbsoluteMaxExclusiveY;
    public double CreatedAt;
    public double SubMapsObservedAt;
    public byte ImmediateLoadEnabled;
    public byte KeepTilesResident;
    public byte EnableSimulation;
  }

  public struct DimensionMergedSimulationRegionCD : IComponentData
  {
    public int AbsoluteMinX;
    public int AbsoluteMinY;
    public int SizeX;
    public int SizeY;
  }
}
