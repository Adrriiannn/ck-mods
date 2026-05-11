using Pug.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;

public struct SmartSplitterTag : IComponentData
{
}

public enum SmartSplitterDecision : byte
{
  None = 0,
  LeftOnly = 1,
  RightOnly = 2,
  Both = 3,
  Blocked = 4
}

public struct SmartSplitterConfigCD : IComponentData
{
  public bool Enabled;

  public ObjectID LeftFilterObject;
  public int LeftFilterVariation;

  public ObjectID RightFilterObject;
  public int RightFilterVariation;
}

public struct SmartSplitterOriginalOutputsCD : IComponentData
{
  public bool HasOriginalOutputs;

  public Entity LeftMoverEntity;
  public int LeftMoverIndex;
  public int2 LeftCachedDirection;
  public int2 LeftCachedStart;

  public Entity RightMoverEntity;
  public int RightMoverIndex;
  public int2 RightCachedDirection;
  public int2 RightCachedStart;
}

public struct SmartSplitterArmedRouteCD : IComponentData
{
  public bool HasArmedRoute;
  public bool AppliedOnce;
  public bool VerifiedHoldState;

  public Entity ArmedEntity;

  public SmartSplitterDecision Decision;

  public ObjectID ItemObject;
  public int ItemVariation;
  public int ItemAmount;

  public double ArmedAt;
  public double ExpiresAt;

  public double RouteAppliedAt;
  public double HoldUntil;
}
