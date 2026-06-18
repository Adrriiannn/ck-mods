using Unity.Mathematics;
using UnityEngine;

public static class ConveyorTunnelHelperSpriteRegistry
{
  private const string HelperArrowUpName = "HelperArrowUp";
  private const string HelperArrowDownName = "HelperArrowDown";
  private const string HelperArrowLeftName = "HelperArrowLeft";
  private const string HelperArrowRightName = "HelperArrowRight";
  private const string HelperBackgroundHorizontalName = "HelperBGHorizontal";
  private const string HelperBackgroundVerticalName = "HelperBGVertical";
  private const string HelperNoArrowUpName = "HelperNoArrowUp";
  private const string HelperNoArrowDownName = "HelperNoArrowDown";
  private const string HelperNoArrowLeftName = "HelperNoArrowLeft";
  private const string HelperNoArrowRightName = "HelperNoArrowRight";
  private const string HelperNoBackgroundHorizontalName = "HelperNoBGHorizontal";
  private const string HelperNoBackgroundVerticalName = "HelperNoBGVertical";

  private static Sprite _arrowUp;
  private static Sprite _arrowDown;
  private static Sprite _arrowLeft;
  private static Sprite _arrowRight;
  private static Sprite _backgroundHorizontal;
  private static Sprite _backgroundVertical;
  private static Sprite _noArrowUp;
  private static Sprite _noArrowDown;
  private static Sprite _noArrowLeft;
  private static Sprite _noArrowRight;
  private static Sprite _noBackgroundHorizontal;
  private static Sprite _noBackgroundVertical;
  private static bool _loggedReady;

  public static bool IsReady =>
      _arrowUp != null &&
      _arrowDown != null &&
      _arrowLeft != null &&
      _arrowRight != null &&
      _backgroundHorizontal != null &&
      _backgroundVertical != null &&
      _noArrowUp != null &&
      _noArrowDown != null &&
      _noArrowLeft != null &&
      _noArrowRight != null &&
      _noBackgroundHorizontal != null &&
      _noBackgroundVertical != null;

  public static void RegisterLoadedObject(Object obj)
  {
    if (obj is ConveyorTunnelHelperSpriteLibrary library)
    {
      CaptureLibrary(library);
      return;
    }

    if (!(obj is Sprite sprite))
    {
      return;
    }

    switch (sprite.name)
    {
      case HelperArrowUpName:
        _arrowUp = sprite;
        break;
      case HelperArrowDownName:
        _arrowDown = sprite;
        break;
      case HelperArrowLeftName:
        _arrowLeft = sprite;
        break;
      case HelperArrowRightName:
        _arrowRight = sprite;
        break;
      case HelperBackgroundHorizontalName:
        _backgroundHorizontal = sprite;
        break;
      case HelperBackgroundVerticalName:
        _backgroundVertical = sprite;
        break;
      case HelperNoArrowUpName:
        _noArrowUp = sprite;
        break;
      case HelperNoArrowDownName:
        _noArrowDown = sprite;
        break;
      case HelperNoArrowLeftName:
        _noArrowLeft = sprite;
        break;
      case HelperNoArrowRightName:
        _noArrowRight = sprite;
        break;
      case HelperNoBackgroundHorizontalName:
        _noBackgroundHorizontal = sprite;
        break;
      case HelperNoBackgroundVerticalName:
        _noBackgroundVertical = sprite;
        break;
      default:
        return;
    }

    if (IsReady && !_loggedReady)
    {
      Debug.Log("[ConveyorTunnelHelperSpriteRegistry] Captured all placement helper sprites.");
      _loggedReady = true;
    }
  }

  private static void CaptureLibrary(ConveyorTunnelHelperSpriteLibrary library)
  {
    if (library == null)
    {
      return;
    }

    _arrowUp = library.ArrowUp;
    _arrowDown = library.ArrowDown;
    _arrowLeft = library.ArrowLeft;
    _arrowRight = library.ArrowRight;
    _backgroundHorizontal = library.BackgroundHorizontal;
    _backgroundVertical = library.BackgroundVertical;
    _noArrowUp = library.NoArrowUp;
    _noArrowDown = library.NoArrowDown;
    _noArrowLeft = library.NoArrowLeft;
    _noArrowRight = library.NoArrowRight;
    _noBackgroundHorizontal = library.NoBackgroundHorizontal;
    _noBackgroundVertical = library.NoBackgroundVertical;

    if (IsReady && !_loggedReady)
    {
      Debug.Log(
          "[ConveyorTunnelHelperSpriteRegistry] Captured serialized placement helper library.");
      _loggedReady = true;
    }
  }

  public static bool TryGetBackground(
      int2 direction,
      bool correctOrientation,
      out Sprite sprite)
  {
    sprite = direction.x != 0
        ? correctOrientation
            ? _backgroundHorizontal
            : _noBackgroundHorizontal
        : correctOrientation
            ? _backgroundVertical
            : _noBackgroundVertical;
    return sprite != null;
  }

  public static bool TryGetArrow(
      int2 direction,
      bool correctOrientation,
      out Sprite sprite)
  {
    if (direction.Equals(new int2(0, 1)))
    {
      sprite = correctOrientation ? _arrowUp : _noArrowUp;
    }
    else if (direction.Equals(new int2(0, -1)))
    {
      sprite = correctOrientation ? _arrowDown : _noArrowDown;
    }
    else if (direction.Equals(new int2(-1, 0)))
    {
      sprite = correctOrientation ? _arrowLeft : _noArrowLeft;
    }
    else if (direction.Equals(new int2(1, 0)))
    {
      sprite = correctOrientation ? _arrowRight : _noArrowRight;
    }
    else
    {
      sprite = null;
    }

    return sprite != null;
  }

  public static void Clear()
  {
    _arrowUp = null;
    _arrowDown = null;
    _arrowLeft = null;
    _arrowRight = null;
    _backgroundHorizontal = null;
    _backgroundVertical = null;
    _noArrowUp = null;
    _noArrowDown = null;
    _noArrowLeft = null;
    _noArrowRight = null;
    _noBackgroundHorizontal = null;
    _noBackgroundVertical = null;
    _loggedReady = false;
  }
}
