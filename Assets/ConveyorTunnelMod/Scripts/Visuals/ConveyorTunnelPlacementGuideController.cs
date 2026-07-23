using System;
using System.Collections.Generic;
using Pug.UnityExtensions;
using Unity.Mathematics;
using UnityEngine;

public sealed class ConveyorTunnelPlacementGuideController : MonoBehaviour
{
  internal const float HologramMaxAlpha = 0.5f;

  private const int MaxGuideMarkerCount = 2048;
  private const float PreviewStaleSeconds = 0.15f;
  private const float ArrowSpeedTilesPerSecond = 1.8f;
  private const float EndpointArrowInset = 0.68f;
  private const float BackgroundGroundHeightOffset = 0.021f;
  private const float ArrowGroundHeightOffset = 0.026f;
  private const string TransparencyPropertyName = "_transparancy";

  private static ConveyorTunnelPlacementGuideController _instance;

  private readonly List<SpriteRenderer> _backgroundMarkers =
      new List<SpriteRenderer>();
  private readonly List<SpriteRenderer> _arrowMarkers =
      new List<SpriteRenderer>();
  private readonly List<ConveyorTunnelPendingEndpoint> _pendingEndpoints =
      new List<ConveyorTunnelPendingEndpoint>();

  private PlacementIcon _placementIcon;
  private Material _guideMaterial;
  private int2 _targetTile;
  private int2 _candidateDirection;
  private float _lastPreviewAt = float.NegativeInfinity;

  public static void EnsureExists()
  {
    if (_instance != null)
    {
      return;
    }

    GameObject host = new GameObject("ConveyorTunnelPlacementGuide");
    DontDestroyOnLoad(host);
    _instance = host.AddComponent<ConveyorTunnelPlacementGuideController>();
  }

  public static void SetPlacementPreview(
      int2 targetTile,
      int2 candidateDirection,
      PlacementIcon placementIcon)
  {
    EnsureExists();
    if (_instance == null)
    {
      return;
    }

    _instance._targetTile = targetTile;
    _instance._candidateDirection = candidateDirection;
    _instance._placementIcon = placementIcon;
    _instance._lastPreviewAt = Time.unscaledTime;
  }

  public static void Hide()
  {
    if (_instance != null)
    {
      _instance.HideAll();
      _instance._lastPreviewAt = float.NegativeInfinity;
    }
  }

  public static void SyncAppearanceFromPlacementIcon(
      PlacementIcon placementIcon)
  {
    if (_instance == null ||
        placementIcon == null ||
        _instance._placementIcon != placementIcon)
    {
      return;
    }

    _instance.SyncGuideAppearance();
  }

  public static void Clear()
  {
    if (_instance == null)
    {
      return;
    }

    Destroy(_instance.gameObject);
    _instance = null;
  }

  private void LateUpdate()
  {
    if (!ShouldShowGuide())
    {
      HideAll();
      return;
    }

    if (!ConveyorTunnelNetworkState.HasReceivedSnapshot)
    {
      ConveyorTunnelNetworkState.RequestSnapshot();
    }

    ConveyorTunnelNetworkState.GetPendingEndpoints(_pendingEndpoints);
    if (_pendingEndpoints.Count == 0 ||
        !ConveyorTunnelHelperSpriteRegistry.IsReady)
    {
      HideAll();
      return;
    }

    EnsureGuideMaterial();
    SyncGuideAppearance();
    _pendingEndpoints.Sort(ComparePendingEndpoints);

    Vector3 targetRenderPosition = GetPlacementTargetRenderPosition();
    int usedBackgroundMarkers = 0;
    int usedArrowMarkers = 0;

    for (int i = 0; i < _pendingEndpoints.Count; i++)
    {
      ConveyorTunnelPendingEndpoint pending = _pendingEndpoints[i];
      int2 delta = _targetTile - pending.Tile;
      if (ConveyorTunnelDirectionUtility.Cross(delta, pending.Direction) != 0)
      {
        continue;
      }

      int signedDistance =
          ConveyorTunnelDirectionUtility.Dot(delta, pending.Direction);
      if (signedDistance == 0)
      {
        continue;
      }

      int distance = math.abs(signedDistance);
      int intermediateTileCount = distance - 1;
      if (intermediateTileCount <= 0)
      {
        continue;
      }

      bool correctOrientation =
          _candidateDirection.Equals(pending.Direction);
      int2 pathStep = signedDistance > 0
          ? pending.Direction
          : new int2(-pending.Direction.x, -pending.Direction.y);

      if (!ConveyorTunnelHelperSpriteRegistry.TryGetBackground(
              pending.Direction,
              correctOrientation,
              out Sprite backgroundSprite) ||
          !ConveyorTunnelHelperSpriteRegistry.TryGetArrow(
              pending.Direction,
              correctOrientation,
              out Sprite arrowSprite))
      {
        continue;
      }

      usedBackgroundMarkers = AppendBackgroundTiles(
          pending.Tile,
          pathStep,
          intermediateTileCount,
          backgroundSprite,
          targetRenderPosition,
          usedBackgroundMarkers);

      usedArrowMarkers = correctOrientation
          ? AppendAnimatedArrows(
              pending.Tile,
              pathStep,
              distance,
              intermediateTileCount,
              arrowSprite,
              targetRenderPosition,
              usedArrowMarkers)
          : AppendStaticArrows(
              pending.Tile,
              pathStep,
              intermediateTileCount,
              arrowSprite,
              targetRenderPosition,
              usedArrowMarkers);
    }

    HideUnusedMarkers(usedBackgroundMarkers, usedArrowMarkers);
  }

  private bool ShouldShowGuide()
  {
    if (_placementIcon == null ||
        _placementIcon.SR == null ||
        Time.unscaledTime - _lastPreviewAt > PreviewStaleSeconds)
    {
      return false;
    }

    if (Manager.ui != null &&
        (Manager.ui.isShowingMap || Manager.ui.isAnyInventoryShowing))
    {
      return false;
    }

    return _placementIcon.gameObject.activeInHierarchy &&
           _placementIcon.SR.enabled;
  }

  private void EnsureGuideMaterial()
  {
    if (_guideMaterial == null)
    {
      if (_placementIcon != null &&
          _placementIcon.SR != null &&
          _placementIcon.SR.material != null)
      {
        _guideMaterial = Instantiate(_placementIcon.SR.material);
        _guideMaterial.name = "Conveyor Tunnel Placement Helpers";
      }
      else
      {
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
          _guideMaterial = new Material(spriteShader)
          {
            name = "Conveyor Tunnel Placement Helpers"
          };
        }
      }
    }

    RefreshMarkerRenderSettings();
  }

  private void SyncGuideAppearance()
  {
    if (_guideMaterial == null ||
        _placementIcon == null ||
        _placementIcon.SR == null)
    {
      return;
    }

    Material sourceMaterial = _placementIcon.SR.material;
    if (sourceMaterial != null &&
        sourceMaterial.HasProperty(TransparencyPropertyName) &&
        _guideMaterial.HasProperty(TransparencyPropertyName))
    {
      _guideMaterial.SetFloat(
          TransparencyPropertyName,
          sourceMaterial.GetFloat(TransparencyPropertyName));
    }
  }

  private Color GetSourceColor()
  {
    return _placementIcon != null && _placementIcon.SR != null
        ? _placementIcon.SR.color
        : Color.white;
  }

  private int AppendBackgroundTiles(
      int2 startTile,
      int2 pathStep,
      int tileCount,
      Sprite sprite,
      Vector3 targetRenderPosition,
      int markerStartIndex)
  {
    int markersAvailable = MaxGuideMarkerCount - markerStartIndex;
    int markerCount = Mathf.Min(tileCount, markersAvailable);

    for (int i = 0; i < markerCount; i++)
    {
      SpriteRenderer marker = GetBackgroundMarker(markerStartIndex + i);
      if (marker == null)
      {
        return markerStartIndex + i;
      }

      int2 tile = startTile + pathStep * (i + 1);
      marker.sprite = sprite;
      marker.color = GetSourceColor();
      marker.transform.position =
          GetRenderPositionForTile(tile, targetRenderPosition) +
          new Vector3(0.0f, BackgroundGroundHeightOffset, 0.0f);
      marker.transform.rotation = GetSourceRotation();
      marker.transform.localScale = Vector3.one;
      SetRendererActive(marker, true);
    }

    return markerStartIndex + markerCount;
  }

  private int AppendAnimatedArrows(
      int2 startTile,
      int2 pathStep,
      int endpointDistance,
      int desiredArrowCount,
      Sprite sprite,
      Vector3 targetRenderPosition,
      int markerStartIndex)
  {
    int markersAvailable = MaxGuideMarkerCount - markerStartIndex;
    int arrowCount = Mathf.Min(desiredArrowCount, markersAvailable);
    if (arrowCount <= 0)
    {
      return markerStartIndex;
    }

    float usableLength = Mathf.Max(
        0.05f,
        endpointDistance - EndpointArrowInset * 2.0f);
    float spacing = usableLength / arrowCount;
    float travelOffset = Mathf.Repeat(
        Time.unscaledTime * ArrowSpeedTilesPerSecond,
        spacing);
    Vector3 startPosition =
        GetRenderPositionForTile(startTile, targetRenderPosition);
    Vector3 renderPathDirection =
        ToRenderOffset(new float2(pathStep.x, pathStep.y));

    for (int i = 0; i < arrowCount; i++)
    {
      SpriteRenderer marker = GetArrowMarker(markerStartIndex + i);
      if (marker == null)
      {
        return markerStartIndex + i;
      }

      float distanceFromStart =
          EndpointArrowInset +
          Mathf.Repeat(i * spacing + travelOffset, usableLength);
      marker.sprite = sprite;
      marker.color = GetSourceColor();
      marker.transform.position =
          startPosition +
          renderPathDirection * distanceFromStart +
          new Vector3(0.0f, ArrowGroundHeightOffset, 0.0f);
      marker.transform.rotation = GetSourceRotation();
      marker.transform.localScale = Vector3.one;
      SetRendererActive(marker, true);
    }

    return markerStartIndex + arrowCount;
  }

  private int AppendStaticArrows(
      int2 startTile,
      int2 pathStep,
      int tileCount,
      Sprite sprite,
      Vector3 targetRenderPosition,
      int markerStartIndex)
  {
    int markersAvailable = MaxGuideMarkerCount - markerStartIndex;
    int markerCount = Mathf.Min(tileCount, markersAvailable);

    for (int i = 0; i < markerCount; i++)
    {
      SpriteRenderer marker = GetArrowMarker(markerStartIndex + i);
      if (marker == null)
      {
        return markerStartIndex + i;
      }

      int2 tile = startTile + pathStep * (i + 1);
      marker.sprite = sprite;
      marker.color = GetSourceColor();
      marker.transform.position =
          GetRenderPositionForTile(tile, targetRenderPosition) +
          new Vector3(0.0f, ArrowGroundHeightOffset, 0.0f);
      marker.transform.rotation = GetSourceRotation();
      marker.transform.localScale = Vector3.one;
      SetRendererActive(marker, true);
    }

    return markerStartIndex + markerCount;
  }

  private SpriteRenderer GetBackgroundMarker(int index)
  {
    while (_backgroundMarkers.Count <= index &&
           _backgroundMarkers.Count < MaxGuideMarkerCount)
    {
      _backgroundMarkers.Add(
          CreateMarker(
              "HelperBackground_" + _backgroundMarkers.Count,
              isArrow: false));
    }

    return index >= 0 && index < _backgroundMarkers.Count
        ? _backgroundMarkers[index]
        : null;
  }

  private SpriteRenderer GetArrowMarker(int index)
  {
    while (_arrowMarkers.Count <= index &&
           _arrowMarkers.Count < MaxGuideMarkerCount)
    {
      _arrowMarkers.Add(
          CreateMarker("HelperArrow_" + _arrowMarkers.Count, isArrow: true));
    }

    return index >= 0 && index < _arrowMarkers.Count
        ? _arrowMarkers[index]
        : null;
  }

  private SpriteRenderer CreateMarker(string markerName, bool isArrow)
  {
    GameObject marker = new GameObject(markerName);
    marker.transform.SetParent(transform, false);
    SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
    ConfigureRenderer(renderer, isArrow);
    marker.SetActive(false);
    return renderer;
  }

  private void RefreshMarkerRenderSettings()
  {
    for (int i = 0; i < _backgroundMarkers.Count; i++)
    {
      ConfigureRenderer(_backgroundMarkers[i], isArrow: false);
    }

    for (int i = 0; i < _arrowMarkers.Count; i++)
    {
      ConfigureRenderer(_arrowMarkers[i], isArrow: true);
    }
  }

  private void ConfigureRenderer(SpriteRenderer renderer, bool isArrow)
  {
    if (renderer == null || _placementIcon == null || _placementIcon.SR == null)
    {
      return;
    }

    SpriteRenderer source = _placementIcon.SR;
    renderer.sharedMaterial = _guideMaterial != null
        ? _guideMaterial
        : source.sharedMaterial;
    renderer.sortingLayerID = source.sortingLayerID;
    renderer.sortingOrder = source.sortingOrder + (isArrow ? 3 : 2);
    renderer.drawMode = SpriteDrawMode.Simple;
    renderer.maskInteraction = source.maskInteraction;
    renderer.gameObject.layer = source.gameObject.layer;
  }

  private void HideUnusedMarkers(
      int usedBackgroundMarkers,
      int usedArrowMarkers)
  {
    for (int i = usedBackgroundMarkers; i < _backgroundMarkers.Count; i++)
    {
      SetRendererActive(_backgroundMarkers[i], false);
    }

    for (int i = usedArrowMarkers; i < _arrowMarkers.Count; i++)
    {
      SetRendererActive(_arrowMarkers[i], false);
    }
  }

  private Vector3 GetRenderPositionForTile(
      int2 tile,
      Vector3 targetRenderPosition)
  {
    return targetRenderPosition + ToRenderOffset(tile - _targetTile);
  }

  private Vector3 GetPlacementTargetRenderPosition()
  {
    if (_placementIcon != null && _placementIcon.targetPosition != null)
    {
      return _placementIcon.targetPosition.position;
    }

    return _placementIcon != null
        ? _placementIcon.transform.position
        : Vector3.zero;
  }

  private Quaternion GetSourceRotation()
  {
    return _placementIcon != null && _placementIcon.SR != null
        ? _placementIcon.SR.transform.rotation
        : Quaternion.identity;
  }

  private static int ComparePendingEndpoints(
      ConveyorTunnelPendingEndpoint a,
      ConveyorTunnelPendingEndpoint b)
  {
    int y = a.Tile.y.CompareTo(b.Tile.y);
    return y != 0 ? y : a.Tile.x.CompareTo(b.Tile.x);
  }

  private static Vector3 ToRenderOffset(int2 tileOffset)
  {
    return new Vector3(tileOffset.x, 0.0f, tileOffset.y);
  }

  private static Vector3 ToRenderOffset(float2 tileOffset)
  {
    return new Vector3(tileOffset.x, 0.0f, tileOffset.y);
  }

  private void HideAll()
  {
    HideUnusedMarkers(0, 0);
  }

  private static void SetRendererActive(SpriteRenderer renderer, bool active)
  {
    if (renderer != null && renderer.gameObject.activeSelf != active)
    {
      renderer.gameObject.SetActive(active);
    }
  }

  private void OnDestroy()
  {
    if (_guideMaterial != null)
    {
      Destroy(_guideMaterial);
      _guideMaterial = null;
    }

    if (_instance == this)
    {
      _instance = null;
    }
  }
}

public sealed class ConveyorTunnelConnectionOverlayController : MonoBehaviour
{
  private enum OverlayMode : byte
  {
    Hidden = 0,
    Single = 1,
    All = 2
  }

  private sealed class DisplayedConnection
  {
    public long Key;
    public ConveyorTunnelConnectionVisual Connection;
    public Color Color;
    public bool UseOriginalSpriteColors;
    public float IntroducedAt;
    public float FadeStartAlpha;
  }

  private readonly struct Bounds2D
  {
    public Bounds2D(float minX, float minY, float maxX, float maxY)
    {
      MinX = minX;
      MinY = minY;
      MaxX = maxX;
      MaxY = maxY;
    }

    public readonly float MinX;
    public readonly float MinY;
    public readonly float MaxX;
    public readonly float MaxY;
  }

  private const int MaxMarkersPerLayer = 2048;
  private const float InteractionRadius = 2.25f;
  private const float AimLineRadius = 0.85f;
  private const float TargetPollIntervalSeconds = 0.06f;
  private const float FadeInDurationSeconds = 0.35f;
  private const float HoldDurationSeconds = 5.0f;
  private const float FadeOutDurationSeconds = FadeInDurationSeconds;
  private const float ArrowSpeedTilesPerSecond = 1.8f;
  private const float EndpointArrowInset = 0.68f;
  private const float BackgroundGroundHeightOffset = 0.031f;
  private const float ArrowGroundHeightOffset = 0.037f;
  private const float BackgroundAlphaMultiplier =
      ConveyorTunnelPlacementGuideController.HologramMaxAlpha;
  private const float ArrowAlphaMultiplier =
      ConveyorTunnelPlacementGuideController.HologramMaxAlpha;
  private const float VisiblePathRadius = 28.0f;

  private static ConveyorTunnelConnectionOverlayController _instance;

  private readonly List<int2> _registeredTunnelTiles = new List<int2>();
  private readonly List<ConveyorTunnelConnectionVisual> _networkConnections =
      new List<ConveyorTunnelConnectionVisual>();
  private readonly List<DisplayedConnection> _displayedConnections =
      new List<DisplayedConnection>();
  private readonly List<SpriteRenderer> _backgroundMarkers =
      new List<SpriteRenderer>();
  private readonly List<SpriteRenderer> _arrowMarkers =
      new List<SpriteRenderer>();
  private readonly Dictionary<Sprite, Sprite> _tintableSprites =
      new Dictionary<Sprite, Sprite>();
  private readonly List<Texture2D> _generatedTextures = new List<Texture2D>();

  private OverlayMode _mode;
  private Material _overlayMaterial;
  private int2 _highlightedTile;
  private int2 _cachedLookTargetTile;
  private int2 _selectedEndpointTile;
  private bool _hasHighlightedTile;
  private bool _hasCachedLookTarget;
  private bool _networkStateDirty;
  private float _nextTargetPollAt;
  private float _displayExpiresAt = float.PositiveInfinity;
  private float _fadeOutStartedAt = float.PositiveInfinity;
  private float _colorPhase;

  public static bool IsShowing =>
      _instance != null && _instance._mode != OverlayMode.Hidden;

  public static void EnsureExists()
  {
    if (_instance != null)
    {
      return;
    }

    GameObject host = new GameObject("ConveyorTunnelConnectionOverlay");
    DontDestroyOnLoad(host);
    _instance = host.AddComponent<ConveyorTunnelConnectionOverlayController>();
  }

  public static void Hide()
  {
    if (_instance != null)
    {
      _instance.HideImmediately();
    }
  }

  public static void Clear()
  {
    if (_instance == null)
    {
      return;
    }

    Destroy(_instance.gameObject);
    _instance = null;
  }

  private void OnEnable()
  {
    ConveyorTunnelNetworkState.EndpointStateChanged -= HandleEndpointStateChanged;
    ConveyorTunnelNetworkState.EndpointStateChanged += HandleEndpointStateChanged;
  }

  private void OnDisable()
  {
    ConveyorTunnelNetworkState.EndpointStateChanged -= HandleEndpointStateChanged;
    UpdateHighlight(false, default);
    HideAllMarkers();
  }

  private void Update()
  {
    UpdateDisplayLifecycle();

    bool canInteract = CanInteractWithWorld();
    int2 targetTile = default;
    bool hasTarget = canInteract && TryGetLookTarget(out targetTile);
    UpdateHighlight(hasTarget, targetTile);

    if (hasTarget && WasInteractPressed())
    {
      HandleInteraction(targetTile);
    }
  }

  private void LateUpdate()
  {
    ShowInteractPrompt();

    if (_mode == OverlayMode.Hidden || !CanShowWorldOverlay())
    {
      HideAllMarkers();
      return;
    }

    RenderDisplayedConnections();
  }

  private void HandleInteraction(int2 targetTile)
  {
    float now = Time.unscaledTime;
    bool isFading = !float.IsPositiveInfinity(_fadeOutStartedAt);

    if (_mode == OverlayMode.Hidden || isFading)
    {
      if (ConveyorTunnelNetworkState.TryGetConnectionForEndpoint(
              targetTile,
              out ConveyorTunnelConnectionVisual selectedConnection))
      {
        StartSingleConnection(selectedConnection, targetTile, now);
      }

      return;
    }

    if (_mode == OverlayMode.Single)
    {
      ShowAllConnections(now);
      return;
    }

    BeginFadeOut(now);
  }

  private void StartSingleConnection(
      ConveyorTunnelConnectionVisual connection,
      int2 selectedEndpointTile,
      float now)
  {
    _displayedConnections.Clear();
    _displayedConnections.Add(new DisplayedConnection
    {
      Key = GetConnectionKey(connection),
      Connection = connection,
      Color = Color.white,
      UseOriginalSpriteColors = true,
      IntroducedAt = now,
      FadeStartAlpha = 1.0f
    });

    _selectedEndpointTile = selectedEndpointTile;
    _mode = OverlayMode.Single;
    _displayExpiresAt = now + HoldDurationSeconds;
    _fadeOutStartedAt = float.PositiveInfinity;
    _networkStateDirty = false;
  }

  private void ShowAllConnections(float now)
  {
    ConveyorTunnelNetworkState.GetConnections(_networkConnections);
    if (_networkConnections.Count == 0)
    {
      BeginFadeOut(now);
      return;
    }

    SortConnectionsByPlayerDistance(_networkConnections);
    _colorPhase = CreateRandomColorPhase();
    _mode = OverlayMode.All;
    _displayExpiresAt = now + HoldDurationSeconds;
    _fadeOutStartedAt = float.PositiveInfinity;
    ReconcileAllConnections(now, addNewConnections: true);
    _networkStateDirty = false;
  }

  private void UpdateDisplayLifecycle()
  {
    if (_mode == OverlayMode.Hidden)
    {
      return;
    }

    float now = Time.unscaledTime;

    if (_networkStateDirty)
    {
      ReconcileDisplayedConnections(now);
      _networkStateDirty = false;
    }

    if (float.IsPositiveInfinity(_fadeOutStartedAt) &&
        now >= _displayExpiresAt)
    {
      BeginFadeOut(now);
    }

    if (!float.IsPositiveInfinity(_fadeOutStartedAt) &&
        now - _fadeOutStartedAt >= FadeOutDurationSeconds)
    {
      HideImmediately();
    }
  }

  private void ReconcileDisplayedConnections(float now)
  {
    if (_mode == OverlayMode.Single)
    {
      if (!ConveyorTunnelNetworkState.TryGetConnectionForEndpoint(
              _selectedEndpointTile,
              out ConveyorTunnelConnectionVisual connection) ||
          _displayedConnections.Count == 0 ||
          GetConnectionKey(connection) != _displayedConnections[0].Key)
      {
        BeginFadeOut(now);
        return;
      }

      _displayedConnections[0].Connection = connection;
      return;
    }

    if (_mode == OverlayMode.All)
    {
      ConveyorTunnelNetworkState.GetConnections(_networkConnections);
      SortConnectionsByPlayerDistance(_networkConnections);
      ReconcileAllConnections(now, addNewConnections: true);

      if (_displayedConnections.Count == 0)
      {
        BeginFadeOut(now);
      }
    }
  }

  private void ReconcileAllConnections(
      float now,
      bool addNewConnections)
  {
    for (int i = _displayedConnections.Count - 1; i >= 0; i--)
    {
      DisplayedConnection displayed = _displayedConnections[i];
      int networkIndex = FindConnectionIndex(displayed.Key);
      if (networkIndex < 0)
      {
        _displayedConnections.RemoveAt(i);
        continue;
      }

      displayed.Connection = _networkConnections[networkIndex];
    }

    if (!addNewConnections)
    {
      return;
    }

    int addedColorIndex = _displayedConnections.Count;
    for (int i = 0; i < _networkConnections.Count; i++)
    {
      ConveyorTunnelConnectionVisual connection = _networkConnections[i];
      long key = GetConnectionKey(connection);
      if (FindDisplayedConnectionIndex(key) >= 0)
      {
        continue;
      }

      _displayedConnections.Add(new DisplayedConnection
      {
        Key = key,
        Connection = connection,
        Color = GetRandomNonRedColor(addedColorIndex++),
        UseOriginalSpriteColors = false,
        IntroducedAt = now,
        FadeStartAlpha = 1.0f
      });
    }
  }

  private void BeginFadeOut(float now)
  {
    if (_mode == OverlayMode.Hidden ||
        !float.IsPositiveInfinity(_fadeOutStartedAt))
    {
      return;
    }

    for (int i = 0; i < _displayedConnections.Count; i++)
    {
      _displayedConnections[i].FadeStartAlpha =
          GetFadeInAlpha(_displayedConnections[i], now);
    }

    _fadeOutStartedAt = now;
  }

  private void HideImmediately()
  {
    _mode = OverlayMode.Hidden;
    _displayedConnections.Clear();
    _displayExpiresAt = float.PositiveInfinity;
    _fadeOutStartedAt = float.PositiveInfinity;
    _networkStateDirty = false;
    HideAllMarkers();
  }

  private void HandleEndpointStateChanged(
      int2 tile,
      ConveyorTunnelEndpointVisualState state)
  {
    if (_mode != OverlayMode.Hidden)
    {
      _networkStateDirty = true;
    }
  }

  private bool TryGetLookTarget(out int2 targetTile)
  {
    targetTile = default;
    if (Time.unscaledTime < _nextTargetPollAt)
    {
      targetTile = _cachedLookTargetTile;
      return _hasCachedLookTarget;
    }

    _nextTargetPollAt = Time.unscaledTime + TargetPollIntervalSeconds;
    _hasCachedLookTarget =
        TryGetLookTargetUncached(out _cachedLookTargetTile);
    targetTile = _cachedLookTargetTile;
    return _hasCachedLookTarget;
  }

  private bool TryGetLookTargetUncached(out int2 targetTile)
  {
    targetTile = default;
    if (Manager.main == null ||
        Manager.main.player == null ||
        Manager.ui == null ||
        Manager.ui.mouse == null)
    {
      return false;
    }

    ConveyorTunnelVisual.GetRegisteredTunnelTiles(_registeredTunnelTiles);
    if (_registeredTunnelTiles.Count == 0)
    {
      return false;
    }

    Vector3 playerWorld = Manager.main.player.WorldPosition;
    Vector2 playerPosition = new Vector2(playerWorld.x, playerWorld.z);
    Vector3 mouseWorld3 = EntityMonoBehaviour.ToWorldFromRender(
        Manager.ui.mouse.GetMouseGameViewPosition());
    Vector2 mousePosition = new Vector2(mouseWorld3.x, mouseWorld3.z);

    float targetRadiusSq = InteractionRadius * InteractionRadius;
    float bestScore = float.MaxValue;
    float bestPlayerDistanceSq = targetRadiusSq;
    float tileHalfExtent = Mathf.Max(0.55f, AimLineRadius * 0.5f);
    const float sameAxisTolerance = 0.72f;
    bool found = false;

    for (int i = 0; i < _registeredTunnelTiles.Count; i++)
    {
      int2 tile = _registeredTunnelTiles[i];
      Vector2 tileCenter = new Vector2(tile.x, tile.y);
      Vector2 playerOffset = tileCenter - playerPosition;
      float playerDistanceSq = playerOffset.sqrMagnitude;
      if (playerDistanceSq > targetRadiusSq)
      {
        continue;
      }

      bool isCardinalNeighbor =
          (Mathf.Abs(playerOffset.x) <= sameAxisTolerance &&
           Mathf.Abs(playerOffset.y) <= InteractionRadius) ||
          (Mathf.Abs(playerOffset.y) <= sameAxisTolerance &&
           Mathf.Abs(playerOffset.x) <= InteractionRadius);
      if (!isCardinalNeighbor)
      {
        continue;
      }

      Bounds2D hitbox = new Bounds2D(
          tileCenter.x - tileHalfExtent,
          tileCenter.y - tileHalfExtent,
          tileCenter.x + tileHalfExtent,
          tileCenter.y + tileHalfExtent);
      if (!SegmentIntersectsBounds(
              playerPosition,
              mousePosition,
              hitbox,
              out float hitT))
      {
        continue;
      }

      float score = hitT + playerDistanceSq * 0.01f;
      if (score > bestScore ||
          (Mathf.Approximately(score, bestScore) &&
           playerDistanceSq >= bestPlayerDistanceSq))
      {
        continue;
      }

      found = true;
      bestScore = score;
      bestPlayerDistanceSq = playerDistanceSq;
      targetTile = tile;
    }

    return found;
  }

  private void UpdateHighlight(bool hasTarget, int2 targetTile)
  {
    if (_hasHighlightedTile &&
        (!hasTarget || !_highlightedTile.Equals(targetTile)))
    {
      ConveyorTunnelVisual.SetInteractionOutlineAtTile(
          _highlightedTile,
          false);
      _hasHighlightedTile = false;
    }

    if (hasTarget &&
        (!_hasHighlightedTile || !_highlightedTile.Equals(targetTile)))
    {
      _highlightedTile = targetTile;
      _hasHighlightedTile = true;
      ConveyorTunnelVisual.SetInteractionOutlineAtTile(
          _highlightedTile,
          true);
    }
  }

  private static bool CanInteractWithWorld()
  {
    return Manager.ui != null &&
           !Manager.ui.isAnyInventoryShowing &&
           !Manager.ui.isShowingMap &&
           Manager.main != null &&
           Manager.main.player != null &&
           !Manager.main.player.instrumentHandler.IsPlayingInstrument &&
           !Manager.main.player.guestMode;
  }

  private static bool CanShowWorldOverlay()
  {
    return Manager.ui != null &&
           !Manager.ui.isAnyInventoryShowing &&
           !Manager.ui.isShowingMap &&
           Manager.main != null &&
           Manager.main.player != null;
  }

  private static bool WasInteractPressed()
  {
    if (Manager.main == null ||
        Manager.main.player == null ||
        Manager.main.player.inputModule == null ||
        Manager.input == null ||
        Manager.input.textInputWasActiveThisFrame)
    {
      return false;
    }

    return Manager.main.player.inputModule.WasButtonPressedDownThisFrame(
        PlayerInput.InputType.INTERACT_WITH_OBJECT,
        false);
  }

  private void ShowInteractPrompt()
  {
    if (!_hasHighlightedTile ||
        !CanInteractWithWorld() ||
        Manager.ui.interactHintButton == null)
    {
      return;
    }

    InteractButton interactButton = Manager.ui.interactHintButton;
    interactButton.icon.enabled = true;
    interactButton.icon.SetAlpha(1.0f);
    interactButton.textContainer.SetActive(true);
  }

  private void RenderDisplayedConnections()
  {
    if (!ConveyorTunnelHelperSpriteRegistry.IsReady)
    {
      HideAllMarkers();
      return;
    }

    EnsureRenderResources();
    if (_overlayMaterial == null)
    {
      HideAllMarkers();
      return;
    }

    int usedBackgroundMarkers = 0;
    int usedArrowMarkers = 0;
    float now = Time.unscaledTime;

    for (int i = 0; i < _displayedConnections.Count; i++)
    {
      DisplayedConnection displayed = _displayedConnections[i];
      float alpha = GetConnectionAlpha(displayed, now);
      if (alpha <= 0.001f)
      {
        continue;
      }

      AppendConnectionMarkers(
          displayed,
          alpha,
          ref usedBackgroundMarkers,
          ref usedArrowMarkers);

      if (usedBackgroundMarkers >= MaxMarkersPerLayer &&
          usedArrowMarkers >= MaxMarkersPerLayer)
      {
        break;
      }
    }

    HideUnusedMarkers(usedBackgroundMarkers, usedArrowMarkers);
  }

  private void AppendConnectionMarkers(
      DisplayedConnection displayed,
      float alpha,
      ref int usedBackgroundMarkers,
      ref int usedArrowMarkers)
  {
    ConveyorTunnelConnectionVisual connection = displayed.Connection;
    int2 delta = connection.ExitTile - connection.EntranceTile;
    int signedDistance =
        ConveyorTunnelDirectionUtility.Dot(delta, connection.Direction);
    if (signedDistance == 0 ||
        ConveyorTunnelDirectionUtility.Cross(delta, connection.Direction) != 0)
    {
      return;
    }

    int distance = math.abs(signedDistance);
    int2 pathStep = signedDistance > 0
        ? connection.Direction
        : new int2(-connection.Direction.x, -connection.Direction.y);

    if (!ConveyorTunnelHelperSpriteRegistry.TryGetBackground(
            pathStep,
            correctOrientation: true,
            out Sprite backgroundSource) ||
        !ConveyorTunnelHelperSpriteRegistry.TryGetArrow(
            pathStep,
            correctOrientation: true,
            out Sprite arrowSource))
    {
      return;
    }

    Sprite backgroundSprite = displayed.UseOriginalSpriteColors
        ? backgroundSource
        : GetTintableSprite(backgroundSource);
    Sprite arrowSprite = displayed.UseOriginalSpriteColors
        ? arrowSource
        : GetTintableSprite(arrowSource);
    int intermediateCount = math.max(0, distance - 1);
    if (!TryGetVisibleDistanceRange(
            connection.EntranceTile,
            pathStep,
            distance,
            out float visibleMinDistance,
            out float visibleMaxDistance))
    {
      return;
    }

    Color backgroundColor = WithAlpha(
        displayed.Color,
        alpha * BackgroundAlphaMultiplier);
    Color arrowColor = WithAlpha(
        displayed.Color,
        alpha * ArrowAlphaMultiplier);

    int firstBackgroundDistance = Mathf.Max(
        1,
        Mathf.CeilToInt(visibleMinDistance));
    int lastBackgroundDistance = Mathf.Min(
        intermediateCount,
        Mathf.FloorToInt(visibleMaxDistance));

    for (int tileDistance = firstBackgroundDistance;
         tileDistance <= lastBackgroundDistance &&
         usedBackgroundMarkers < MaxMarkersPerLayer;
         tileDistance++)
    {
      float2 worldPosition = new float2(
          connection.EntranceTile.x + pathStep.x * tileDistance,
          connection.EntranceTile.y + pathStep.y * tileDistance);
      SpriteRenderer marker =
          GetBackgroundMarker(usedBackgroundMarkers++);
      ConfigureMarker(
          marker,
          backgroundSprite,
          backgroundColor,
          worldPosition,
          BackgroundGroundHeightOffset);
    }

    int globalArrowCount = math.max(1, intermediateCount);
    if (MaxMarkersPerLayer - usedArrowMarkers <= 0)
    {
      return;
    }

    if (distance == 1)
    {
      if (visibleMinDistance <= 0.5f && visibleMaxDistance >= 0.5f)
      {
        float2 midpoint =
            new float2(connection.EntranceTile.x, connection.EntranceTile.y) +
            new float2(pathStep.x, pathStep.y) * 0.5f;
        SpriteRenderer adjacentMarker =
            GetArrowMarker(usedArrowMarkers++);
        ConfigureMarker(
            adjacentMarker,
            arrowSprite,
            arrowColor,
            midpoint,
            ArrowGroundHeightOffset);
      }

      return;
    }

    float usableLength = Mathf.Max(
        0.05f,
        distance - EndpointArrowInset * 2.0f);
    float spacing = usableLength / globalArrowCount;
    float travelOffset = Mathf.Repeat(
        t: Time.unscaledTime * ArrowSpeedTilesPerSecond,
        length: spacing);
    float2 entrance = new float2(
        connection.EntranceTile.x,
        connection.EntranceTile.y);
    float2 renderDirection = new float2(pathStep.x, pathStep.y);

    int firstArrowIndex = Mathf.Max(
        0,
        Mathf.FloorToInt(
            (visibleMinDistance - EndpointArrowInset - travelOffset) /
            spacing) - 1);
    int lastArrowIndex = Mathf.Min(
        globalArrowCount - 1,
        Mathf.CeilToInt(
            (visibleMaxDistance - EndpointArrowInset - travelOffset) /
            spacing) + 1);

    for (int i = firstArrowIndex;
         i <= lastArrowIndex &&
         usedArrowMarkers < MaxMarkersPerLayer;
         i++)
    {
      float distanceFromEntrance =
          EndpointArrowInset +
          Mathf.Repeat(i * spacing + travelOffset, usableLength);
      if (distanceFromEntrance < visibleMinDistance ||
          distanceFromEntrance > visibleMaxDistance)
      {
        continue;
      }

      float2 worldPosition =
          entrance +
          renderDirection * distanceFromEntrance;
      SpriteRenderer marker = GetArrowMarker(usedArrowMarkers++);
      ConfigureMarker(
          marker,
          arrowSprite,
          arrowColor,
          worldPosition,
          ArrowGroundHeightOffset);
    }
  }

  private static bool TryGetVisibleDistanceRange(
      int2 entranceTile,
      int2 pathStep,
      int distance,
      out float minDistance,
      out float maxDistance)
  {
    minDistance = 0.0f;
    maxDistance = distance;

    if (Manager.main == null || Manager.main.player == null)
    {
      return true;
    }

    Vector3 playerWorld = Manager.main.player.WorldPosition;
    float2 entrance = new float2(entranceTile.x, entranceTile.y);
    float2 player = new float2(playerWorld.x, playerWorld.z);
    float2 direction = new float2(pathStep.x, pathStep.y);
    float2 delta = player - entrance;
    float alongPath = math.dot(delta, direction);
    float lateralDistance =
        math.abs(delta.x * direction.y - delta.y * direction.x);
    if (lateralDistance > VisiblePathRadius)
    {
      return false;
    }

    minDistance = math.max(0.0f, alongPath - VisiblePathRadius);
    maxDistance = math.min(distance, alongPath + VisiblePathRadius);
    return minDistance <= maxDistance;
  }

  private void EnsureRenderResources()
  {
    if (_overlayMaterial != null)
    {
      return;
    }

    Shader shader = Shader.Find("Sprites/Default");
    if (shader != null)
    {
      _overlayMaterial = new Material(shader)
      {
        name = "Conveyor Tunnel Connection Overlay"
      };
    }
  }

  private Sprite GetTintableSprite(Sprite source)
  {
    if (source == null)
    {
      return null;
    }

    if (_tintableSprites.TryGetValue(source, out Sprite tintable))
    {
      return tintable;
    }

    tintable = CreateTintableSprite(source);
    _tintableSprites[source] = tintable != null ? tintable : source;
    return _tintableSprites[source];
  }

  private Sprite CreateTintableSprite(Sprite source)
  {
    Texture2D sourceTexture = source.texture;
    if (sourceTexture == null)
    {
      return source;
    }

    RenderTexture temporary = null;
    Texture2D readable = null;
    RenderTexture previous = RenderTexture.active;

    try
    {
      temporary = RenderTexture.GetTemporary(
          sourceTexture.width,
          sourceTexture.height,
          0,
          RenderTextureFormat.ARGB32);
      Graphics.Blit(sourceTexture, temporary);
      RenderTexture.active = temporary;

      readable = new Texture2D(
          sourceTexture.width,
          sourceTexture.height,
          TextureFormat.RGBA32,
          mipChain: false);
      readable.ReadPixels(
          new Rect(0, 0, sourceTexture.width, sourceTexture.height),
          0,
          0);
      readable.Apply(updateMipmaps: false, makeNoLongerReadable: false);

      Rect sourceRect = source.textureRect;
      int width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
      int height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
      Color[] pixels = readable.GetPixels(
          Mathf.RoundToInt(sourceRect.x),
          Mathf.RoundToInt(sourceRect.y),
          width,
          height);

      for (int i = 0; i < pixels.Length; i++)
      {
        Color pixel = pixels[i];
        float brightness = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
        pixels[i] = new Color(
            brightness,
            brightness,
            brightness,
            pixel.a);
      }

      Texture2D texture = new Texture2D(
          width,
          height,
          TextureFormat.RGBA32,
          mipChain: false)
      {
        name = source.name + "_Tintable",
        filterMode = FilterMode.Point,
        wrapMode = TextureWrapMode.Clamp
      };
      texture.SetPixels(pixels);
      texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
      _generatedTextures.Add(texture);

      Vector2 pivot = new Vector2(
          source.pivot.x / source.rect.width,
          source.pivot.y / source.rect.height);
      Sprite sprite = Sprite.Create(
          texture,
          new Rect(0, 0, width, height),
          pivot,
          source.pixelsPerUnit,
          extrude: 0,
          SpriteMeshType.FullRect,
          source.border);
      sprite.name = source.name + "_Tintable";
      return sprite;
    }
    catch (Exception exception)
    {
      Debug.LogWarning(
          $"[ConveyorTunnelConnectionOverlay] Could not create tintable helper sprite {source.name}: {exception.Message}");
      return source;
    }
    finally
    {
      RenderTexture.active = previous;
      if (temporary != null)
      {
        RenderTexture.ReleaseTemporary(temporary);
      }

      if (readable != null)
      {
        Destroy(readable);
      }
    }
  }

  private SpriteRenderer GetBackgroundMarker(int index)
  {
    while (_backgroundMarkers.Count <= index &&
           _backgroundMarkers.Count < MaxMarkersPerLayer)
    {
      _backgroundMarkers.Add(CreateMarker(
          "ConnectionBackground_" + _backgroundMarkers.Count,
          isArrow: false));
    }

    return index >= 0 && index < _backgroundMarkers.Count
        ? _backgroundMarkers[index]
        : null;
  }

  private SpriteRenderer GetArrowMarker(int index)
  {
    while (_arrowMarkers.Count <= index &&
           _arrowMarkers.Count < MaxMarkersPerLayer)
    {
      _arrowMarkers.Add(CreateMarker(
          "ConnectionArrow_" + _arrowMarkers.Count,
          isArrow: true));
    }

    return index >= 0 && index < _arrowMarkers.Count
        ? _arrowMarkers[index]
        : null;
  }

  private SpriteRenderer CreateMarker(string markerName, bool isArrow)
  {
    GameObject markerObject = new GameObject(markerName);
    markerObject.transform.SetParent(transform, false);
    SpriteRenderer renderer = markerObject.AddComponent<SpriteRenderer>();
    renderer.sharedMaterial = _overlayMaterial;
    renderer.sortingOrder = isArrow ? 102 : 101;
    renderer.drawMode = SpriteDrawMode.Simple;
    markerObject.SetActive(false);
    return renderer;
  }

  private static void ConfigureMarker(
      SpriteRenderer marker,
      Sprite sprite,
      Color color,
      float2 worldPosition,
      float heightOffset)
  {
    if (marker == null)
    {
      return;
    }

    marker.sprite = sprite;
    marker.color = color;
    marker.transform.position =
        EntityMonoBehaviour.ToRenderFromWorld(
            new Vector3(worldPosition.x, 0.0f, worldPosition.y)) +
        new Vector3(0.0f, heightOffset, 0.0f);
    marker.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
    marker.transform.localScale = Vector3.one;
    SetRendererActive(marker, true);
  }

  private void HideUnusedMarkers(
      int usedBackgroundMarkers,
      int usedArrowMarkers)
  {
    for (int i = usedBackgroundMarkers; i < _backgroundMarkers.Count; i++)
    {
      SetRendererActive(_backgroundMarkers[i], false);
    }

    for (int i = usedArrowMarkers; i < _arrowMarkers.Count; i++)
    {
      SetRendererActive(_arrowMarkers[i], false);
    }
  }

  private void HideAllMarkers()
  {
    HideUnusedMarkers(0, 0);
  }

  private float GetConnectionAlpha(
      DisplayedConnection displayed,
      float now)
  {
    if (float.IsPositiveInfinity(_fadeOutStartedAt))
    {
      return GetFadeInAlpha(displayed, now);
    }

    float fadeRatio = Mathf.Clamp01(
        (now - _fadeOutStartedAt) / FadeOutDurationSeconds);
    return displayed.FadeStartAlpha * (1.0f - fadeRatio);
  }

  private static float GetFadeInAlpha(
      DisplayedConnection displayed,
      float now)
  {
    return Mathf.Clamp01(
        (now - displayed.IntroducedAt) / FadeInDurationSeconds);
  }

  private void SortConnectionsByPlayerDistance(
      List<ConveyorTunnelConnectionVisual> connections)
  {
    Vector3 playerPosition =
        Manager.main != null && Manager.main.player != null
            ? Manager.main.player.WorldPosition
            : Vector3.zero;

    connections.Sort((a, b) =>
    {
      float aDistance = GetConnectionDistanceSq(a, playerPosition);
      float bDistance = GetConnectionDistanceSq(b, playerPosition);
      int distanceComparison = aDistance.CompareTo(bDistance);
      return distanceComparison != 0
          ? distanceComparison
          : GetConnectionKey(a).CompareTo(GetConnectionKey(b));
    });
  }

  private static float GetConnectionDistanceSq(
      ConveyorTunnelConnectionVisual connection,
      Vector3 playerPosition)
  {
    float centerX =
        (connection.EntranceTile.x + connection.ExitTile.x) * 0.5f;
    float centerY =
        (connection.EntranceTile.y + connection.ExitTile.y) * 0.5f;
    float dx = centerX - playerPosition.x;
    float dy = centerY - playerPosition.z;
    return dx * dx + dy * dy;
  }

  private int FindConnectionIndex(long key)
  {
    for (int i = 0; i < _networkConnections.Count; i++)
    {
      if (GetConnectionKey(_networkConnections[i]) == key)
      {
        return i;
      }
    }

    return -1;
  }

  private int FindDisplayedConnectionIndex(long key)
  {
    for (int i = 0; i < _displayedConnections.Count; i++)
    {
      if (_displayedConnections[i].Key == key)
      {
        return i;
      }
    }

    return -1;
  }

  private static float CreateRandomColorPhase()
  {
    System.Random random = new System.Random(
        Environment.TickCount ^ Time.frameCount);
    return (float)random.NextDouble();
  }

  private Color GetRandomNonRedColor(int index)
  {
    float sequence = Mathf.Repeat(
        _colorPhase + index * 0.61803398875f,
        1.0f);
    float hue = 0.13f + sequence * 0.67f;

    if (Mathf.Abs(hue - 0.5f) < 0.055f)
    {
      hue = Mathf.Repeat(hue + 0.11f, 0.67f) + 0.13f;
    }

    Color color = Color.HSVToRGB(
        hue,
        0.72f + Mathf.Repeat(sequence * 1.7f, 1.0f) * 0.18f,
        1.0f);
    color.a = 1.0f;
    return color;
  }

  private static Color WithAlpha(Color color, float alpha)
  {
    color.a = Mathf.Clamp01(alpha);
    return color;
  }

  private static long GetConnectionKey(
      ConveyorTunnelConnectionVisual connection)
  {
    unchecked
    {
      long hash = 1469598103934665603L;
      hash = (hash ^ connection.EntranceTile.x) * 1099511628211L;
      hash = (hash ^ connection.EntranceTile.y) * 1099511628211L;
      hash = (hash ^ connection.ExitTile.x) * 1099511628211L;
      hash = (hash ^ connection.ExitTile.y) * 1099511628211L;
      return hash;
    }
  }

  private static bool SegmentIntersectsBounds(
      Vector2 segmentStart,
      Vector2 segmentEnd,
      Bounds2D bounds,
      out float hitT)
  {
    hitT = 0.0f;
    Vector2 direction = segmentEnd - segmentStart;
    float tMin = 0.0f;
    float tMax = 1.0f;

    if (!ClipSegmentAxis(
            segmentStart.x,
            direction.x,
            bounds.MinX,
            bounds.MaxX,
            ref tMin,
            ref tMax) ||
        !ClipSegmentAxis(
            segmentStart.y,
            direction.y,
            bounds.MinY,
            bounds.MaxY,
            ref tMin,
            ref tMax))
    {
      return false;
    }

    hitT = tMin;
    return true;
  }

  private static bool ClipSegmentAxis(
      float start,
      float direction,
      float min,
      float max,
      ref float tMin,
      ref float tMax)
  {
    if (Mathf.Abs(direction) < 0.0001f)
    {
      return start >= min && start <= max;
    }

    float inverse = 1.0f / direction;
    float t1 = (min - start) * inverse;
    float t2 = (max - start) * inverse;
    if (t1 > t2)
    {
      float temporary = t1;
      t1 = t2;
      t2 = temporary;
    }

    tMin = Mathf.Max(tMin, t1);
    tMax = Mathf.Min(tMax, t2);
    return tMin <= tMax;
  }

  private static void SetRendererActive(
      SpriteRenderer renderer,
      bool active)
  {
    if (renderer != null && renderer.gameObject.activeSelf != active)
    {
      renderer.gameObject.SetActive(active);
    }
  }

  private void OnDestroy()
  {
    ConveyorTunnelNetworkState.EndpointStateChanged -= HandleEndpointStateChanged;
    UpdateHighlight(false, default);

    if (_overlayMaterial != null)
    {
      Destroy(_overlayMaterial);
      _overlayMaterial = null;
    }

    foreach (Sprite sprite in _tintableSprites.Values)
    {
      if (sprite != null &&
          sprite.name.EndsWith("_Tintable", StringComparison.Ordinal))
      {
        Destroy(sprite);
      }
    }

    for (int i = 0; i < _generatedTextures.Count; i++)
    {
      if (_generatedTextures[i] != null)
      {
        Destroy(_generatedTextures[i]);
      }
    }

    _tintableSprites.Clear();
    _generatedTextures.Clear();

    if (_instance == this)
    {
      _instance = null;
    }
  }
}
