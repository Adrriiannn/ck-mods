using Pug.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

public enum ConveyorTunnelEndpointRole : byte
{
  None = 0,
  Entrance = 1,
  Exit = 2
}

public enum ConveyorTunnelPayloadPhase : byte
{
  IntakeAnimation = 0,
  UndergroundTravel = 1,
  ExitAnimation = 2
}

public enum ConveyorTunnelItemVisualEffectKind : byte
{
  Intake = 0,
  Exit = 1
}

public struct ConveyorTunnelEndpointTag : IComponentData
{
}

public struct ConveyorTunnelEndpointStateCD : IComponentData
{
  public int PairId;
  public ConveyorTunnelEndpointRole Role;
  public byte Linked;
  public byte Pending;
  public byte HasPayloads;
  public int2 Tile;
  public int2 Direction;
  public int2 PairedTile;
  public float2 EntranceHandoffPoint;
  public float2 ExitHandoffPoint;
  public Entity PairedEndpoint;
}

public struct ConveyorTunnelPayloadCD : IComponentData
{
  public int PairId;
  public Entity EntranceEndpoint;
  public Entity ExitEndpoint;
  public Entity MoveeEntity;
  public ObjectDataCD ObjectData;
  public int AuxDataIndex;
  public float2 VisualPoint;
  public float2 HoldPoint;
  public float2 ReleasePoint;
  public float2 ReleaseTarget;
  public float OriginalHeight;
  public float VisualTimer;
  public float TravelTimer;
  public float TravelDuration;
  public ConveyorTunnelPayloadPhase Phase;
  public byte MoveeHadEnabledComponent;
  public byte MoveeWasEnabled;
}

public struct ConveyorTunnelStateRequestRpc : IRpcCommand
{
}

public struct ConveyorTunnelSnapshotBeginRpc : IRpcCommand
{
}

public struct ConveyorTunnelEndpointStateRpc : IRpcCommand
{
  public int TileX;
  public int TileY;
  public int PairId;
  public byte Role;
  public byte Linked;
  public byte Pending;
  public byte HasPayloads;
  public int DirectionX;
  public int DirectionY;
  public int PairedTileX;
  public int PairedTileY;
}

public struct ConveyorTunnelItemVisualEffectRpc : IRpcCommand
{
  public int EventId;
  public byte Kind;
  public float PointX;
  public float PointY;
}

public struct ConveyorTunnelSnapshotEndRpc : IRpcCommand
{
}
