using Unity.NetCode;

public struct SmartSplitterFilterRequestRpc : IRpcCommand
{
  public int CenterX;
  public int CenterY;
}

public struct SmartSplitterSetLaneFilterRpc : IRpcCommand
{
  public int CenterX;
  public int CenterY;
  public byte Lane;
  public byte Mode;
  public int ObjectID;
  public int Variation;
}

public struct SmartSplitterFilterStateRpc : IRpcCommand
{
  public int CenterX;
  public int CenterY;

  public byte LeftMode;
  public int LeftObjectID;
  public int LeftVariation;

  public byte CenterMode;
  public int CenterObjectID;
  public int CenterVariation;

  public byte RightMode;
  public int RightObjectID;
  public int RightVariation;
}

public struct SmartSplitterPowerStateRpc : IRpcCommand
{
  public int CenterX;
  public int CenterY;
  public byte Powered;
}
