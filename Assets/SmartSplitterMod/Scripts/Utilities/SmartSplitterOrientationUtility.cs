using Unity.Mathematics;

public static class SmartSplitterOrientationUtility
{
  public static int NormalizeVariation(int variation)
  {
    return ((variation % 4) + 4) % 4;
  }

  public static int GetSmartSpriteVariationForPlacedVariation(int placedVariation)
  {
    switch (NormalizeVariation(placedVariation))
    {
      case 0:
        return 3;
      case 1:
        return 1;
      case 2:
        return 0;
      case 3:
        return 2;
      default:
        return 0;
    }
  }

  public static bool TryGetInputDirectionForPlacedVariation(int placedVariation, out int2 inputDirection)
  {
    int smartSpriteVariation = GetSmartSpriteVariationForPlacedVariation(placedVariation);
    return TryGetInputDirectionForSmartSpriteVariation(smartSpriteVariation, out inputDirection);
  }

  public static bool TryGetInputDirectionForSmartSpriteVariation(
      int smartSpriteVariation,
      out int2 inputDirection)
  {
    switch (NormalizeVariation(smartSpriteVariation))
    {
      case 0:
        inputDirection = new int2(1, 0);
        return true;
      case 1:
        inputDirection = new int2(0, 1);
        return true;
      case 2:
        inputDirection = new int2(0, -1);
        return true;
      case 3:
        inputDirection = new int2(-1, 0);
        return true;
      default:
        inputDirection = default;
        return false;
    }
  }
}
