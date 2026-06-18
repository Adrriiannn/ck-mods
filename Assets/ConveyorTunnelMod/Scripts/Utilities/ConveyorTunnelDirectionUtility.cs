using Pug.ECS.Components;
using Unity.Mathematics;

public enum ConveyorTunnelPairingPreviewStatus : byte
{
  DifferentLine = 0,
  SameTile = 1,
  WrongOrientation = 2,
  Valid = 3
}

public readonly struct ConveyorTunnelPairingEvaluation
{
  public ConveyorTunnelPairingEvaluation(
      ConveyorTunnelPairingPreviewStatus status,
      int2 entranceTile,
      int2 exitTile,
      int2 flowDirection)
  {
    Status = status;
    EntranceTile = entranceTile;
    ExitTile = exitTile;
    FlowDirection = flowDirection;
  }

  public readonly ConveyorTunnelPairingPreviewStatus Status;
  public readonly int2 EntranceTile;
  public readonly int2 ExitTile;
  public readonly int2 FlowDirection;

  public bool IsOnPairingLine =>
      Status != ConveyorTunnelPairingPreviewStatus.DifferentLine;

  public bool IsValid =>
      Status == ConveyorTunnelPairingPreviewStatus.Valid;
}

public static class ConveyorTunnelDirectionUtility
{
  // The tunnel art mouth sits about 65% from the incoming edge: -0.5 + 0.65 = +0.15.
  private const float TunnelMouthOffset = 0.15f;

  public static int2 GetDirectionFromVariation(int variation)
  {
    int2 direction = DirectionBasedOnVariationCD.GetDirectionFromVariation(
        NormalizeVariation(variation),
        false);

    if (!direction.Equals(int2.zero))
    {
      return direction;
    }

    switch (NormalizeVariation(variation))
    {
      case 0:
        return new int2(0, 1);
      case 1:
        return new int2(1, 0);
      case 2:
        return new int2(0, -1);
      case 3:
        return new int2(-1, 0);
      default:
        return int2.zero;
    }
  }

  public static int NormalizeVariation(int variation)
  {
    int normalized = variation % 4;
    return normalized < 0 ? normalized + 4 : normalized;
  }

  public static int Dot(int2 a, int2 b)
  {
    return a.x * b.x + a.y * b.y;
  }

  public static int Cross(int2 a, int2 b)
  {
    return a.x * b.y - a.y * b.x;
  }

  public static ConveyorTunnelPairingEvaluation EvaluatePairing(
      int2 pendingTile,
      int2 pendingDirection,
      int2 candidateTile,
      int2 candidateDirection)
  {
    if (pendingDirection.Equals(int2.zero))
    {
      return new ConveyorTunnelPairingEvaluation(
          ConveyorTunnelPairingPreviewStatus.DifferentLine,
          default,
          default,
          int2.zero);
    }

    int2 delta = candidateTile - pendingTile;
    if (Cross(delta, pendingDirection) != 0)
    {
      return new ConveyorTunnelPairingEvaluation(
          ConveyorTunnelPairingPreviewStatus.DifferentLine,
          default,
          default,
          pendingDirection);
    }

    int signedDistance = Dot(delta, pendingDirection);
    if (signedDistance == 0)
    {
      return new ConveyorTunnelPairingEvaluation(
          ConveyorTunnelPairingPreviewStatus.SameTile,
          default,
          default,
          pendingDirection);
    }

    if (!candidateDirection.Equals(pendingDirection))
    {
      return new ConveyorTunnelPairingEvaluation(
          ConveyorTunnelPairingPreviewStatus.WrongOrientation,
          default,
          default,
          pendingDirection);
    }

    return signedDistance > 0
        ? new ConveyorTunnelPairingEvaluation(
            ConveyorTunnelPairingPreviewStatus.Valid,
            pendingTile,
            candidateTile,
            pendingDirection)
        : new ConveyorTunnelPairingEvaluation(
            ConveyorTunnelPairingPreviewStatus.Valid,
            candidateTile,
            pendingTile,
            pendingDirection);
  }

  public static bool IsOrderedPair(
      int2 entranceTile,
      int2 entranceDirection,
      int2 exitTile,
      int2 exitDirection,
      int2 requiredDirection)
  {
    if (requiredDirection.Equals(int2.zero) ||
        !entranceDirection.Equals(requiredDirection) ||
        !exitDirection.Equals(requiredDirection))
    {
      return false;
    }

    ConveyorTunnelPairingEvaluation evaluation = EvaluatePairing(
        entranceTile,
        requiredDirection,
        exitTile,
        requiredDirection);

    return evaluation.IsValid &&
           evaluation.EntranceTile.Equals(entranceTile) &&
           evaluation.ExitTile.Equals(exitTile);
  }

  public static float2 GetEntranceHandoffPoint(int2 tile, int2 direction)
  {
    return new float2(
        tile.x + direction.x * TunnelMouthOffset,
        tile.y + direction.y * TunnelMouthOffset);
  }

  public static float2 GetExitHandoffPoint(int2 tile, int2 direction)
  {
    return new float2(
        tile.x - direction.x * TunnelMouthOffset,
        tile.y - direction.y * TunnelMouthOffset);
  }
}
