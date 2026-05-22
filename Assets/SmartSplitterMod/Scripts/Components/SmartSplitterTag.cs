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
  CenterOnly = 2,
  LeftCenter = 3,
  RightOnly = 4,
  Both = 5,
  CenterRight = 6,
  All = 7,
  Blocked = 8
}

public enum SmartSplitterLane : byte
{
  Left = 0,
  Center = 1,
  Right = 2
}

public enum SmartSplitterLaneFilterMode : byte
{
  Any = 0,
  Item = 1,
  None = 2
}

public struct SmartSplitterLaneFilter
{
  public SmartSplitterLaneFilterMode Mode;
  public ObjectID FilterObject;
  public int FilterVariation;
}

public struct SmartSplitterLaneFiltersCD : IComponentData
{
  public SmartSplitterLaneFilter Left;
  public SmartSplitterLaneFilter Center;
  public SmartSplitterLaneFilter Right;
}

public struct SmartSplitterConfigCD : IComponentData
{
  public bool Enabled;
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

  public Entity ArmedEntity;

  public SmartSplitterDecision Decision;

  public ObjectID ItemObject;
  public int ItemVariation;
  public int ItemAmount;
  public int RouteStartLaneIndex;
  public int NextRouteStartLaneIndex;

  public double ArmedAt;
  public double ExpiresAt;

  public double RouteAppliedAt;
}
