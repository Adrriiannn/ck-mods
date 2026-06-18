using Unity.Mathematics;

public static class SmartSplitterOrientationUtility
{
  public static int NormalizeVariation(int variation)
  {
    return ((variation % 4) + 4) % 4;
  }

  public static int GetSmartForwardVariationForPlacementVariation(int placementVariation)
  {
    // Vanilla splitter placement variation 0/1/2/3 visibly points
    // right/down/left/up. Convert that stored vanilla value back into the
    // smart splitter's forward-lane basis: up/right/down/left.
    return NormalizeVariation(placementVariation + 1);
  }

  public static int GetPlacementVariationForSmartForwardVariation(int forwardVariation)
  {
    // Inverse of GetSmartForwardVariationForPlacementVariation. Used only at
    // the placement moment so the actual spawned vanilla splitter matches the
    // hologram direction the player selected.
    return NormalizeVariation(forwardVariation - 1);
  }

  public static int GetTCircuitPreviewVariationForPlacementVariation(int placementVariation)
  {
    // PlacementIcon indexes TCircuit frames directly. This map keeps the
    // hologram cycling in Core Keeper's normal up/right/down/left order
    // without changing the placed splitter's runtime direction.
    switch (NormalizeVariation(placementVariation))
    {
      case 0:
        return 2;
      case 1:
        return 3;
      case 2:
        return 0;
      case 3:
        return 1;
      default:
        return 0;
    }
  }

  public static int GetSmartSpriteVariationForForwardVariation(int forwardVariation)
  {
    // The smartsplitter asset stores its frames as:
    // base=left, variant1=down, variant2=up, variant3=right.
    // Keep this explicit so the smart visual follows the logical
    // up/right/down/left forward-lane basis used by placement and routing.
    switch (NormalizeVariation(forwardVariation))
    {
      case 0:
        return 2;
      case 1:
        return 3;
      case 2:
        return 1;
      case 3:
        return 0;
      default:
        return 0;
    }
  }

  public static bool TryGetForwardVariationFromDirection(int2 direction, out int forwardVariation)
  {
    if (direction.x == 0 && direction.y > 0)
    {
      forwardVariation = 0;
      return true;
    }

    if (direction.x > 0 && direction.y == 0)
    {
      forwardVariation = 1;
      return true;
    }

    if (direction.x == 0 && direction.y < 0)
    {
      forwardVariation = 2;
      return true;
    }

    if (direction.x < 0 && direction.y == 0)
    {
      forwardVariation = 3;
      return true;
    }

    forwardVariation = 0;
    return false;
  }

  public static bool TryGetInputDirectionForForwardVariation(
      int forwardVariation,
      out int2 inputDirection)
  {
    switch (NormalizeVariation(forwardVariation))
    {
      case 0:
        inputDirection = new int2(0, -1);
        return true;
      case 1:
        inputDirection = new int2(-1, 0);
        return true;
      case 2:
        inputDirection = new int2(0, 1);
        return true;
      case 3:
        inputDirection = new int2(1, 0);
        return true;
      default:
        inputDirection = default;
        return false;
    }
  }
}
