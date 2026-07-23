using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class ChunkLoaderMapUiHost : MonoBehaviour
{
  private const int ClaimsPerFrame = 16;

  private static ChunkLoaderMapUiHost _instance;

  private readonly ChunkLoaderMapOverlayRenderer _overlay =
      new ChunkLoaderMapOverlayRenderer();
  private readonly ChunkLoaderManagerPanel _manager =
      new ChunkLoaderManagerPanel();
  private readonly ChunkLoaderToolkitMapControls _controls =
      new ChunkLoaderToolkitMapControls();
  private readonly ChunkLoaderCursorOverlay _cursorOverlay =
      new ChunkLoaderCursorOverlay();
  private readonly List<ChunkCoordinate> _selectedMapChunks = new();
  private readonly HashSet<long> _selectedMapChunkKeys = new();
  private readonly Queue<ChunkCoordinate> _claimQueue = new();
  private readonly HashSet<long> _pendingClaimKeys = new();
  private readonly Dictionary<uint, long> _claimRequestKeys = new();

  private MapUI _map;
  private bool _overlayVisible;
  private bool _mouseDown;
  private bool _mapPopupOpen;
  private bool _mapControlsArmed;
  private bool _failedUiCreationForCurrentMapOpen;
  private bool _prewarmFailedForCurrentMap;
  private bool _wasShowingBigMap;
  private int _prewarmStage;
  private double _armMapControlsAt;
  private double _nextPrewarmStepAt;
  private double _suppressMapClicksUntil;
  private Vector3 _mouseDownPosition;

  public static ChunkLoaderMapUiHost Instance => _instance;

  public static void EnsureExists()
  {
    if (_instance != null)
    {
      return;
    }

    GameObject host = new GameObject("ChunkLoaderMapUiHost");
    DontDestroyOnLoad(host);
    _instance = host.AddComponent<ChunkLoaderMapUiHost>();
  }

  public void SetOverlayVisible(bool visible)
  {
    _overlayVisible = visible;
    UpdateOverlayButtonText();
  }

  public void ResetForWorldChange()
  {
    DestroyRuntimeUi();
    _overlayVisible = false;
    _mouseDown = false;
    _mapPopupOpen = false;
    _mapControlsArmed = false;
    _failedUiCreationForCurrentMapOpen = false;
    _prewarmFailedForCurrentMap = false;
    _wasShowingBigMap = false;
    _prewarmStage = 0;
    _armMapControlsAt = 0.0d;
    _nextPrewarmStepAt = 0.0d;
    _suppressMapClicksUntil = 0.0d;
    ClearMapSelection();
    ClearClaimQueue();
  }

  private void Update()
  {
    if (!TryEnsureMap())
    {
      _cursorOverlay.Update(false);
      return;
    }

    ProcessClaimQueue();

    bool bigMap = _map.IsShowingBigMap;
    if (!bigMap)
    {
      ClearMapSelection();
      _cursorOverlay.Update(false);
      _failedUiCreationForCurrentMapOpen = false;
      _mapControlsArmed = false;
    }
    else if (!_wasShowingBigMap)
    {
      _mapControlsArmed = false;
      _armMapControlsAt =
          Time.realtimeSinceStartupAsDouble + 0.25d;
    }
    _wasShowingBigMap = bigMap;

    PrewarmBigMapUi(bigMap);

    if (bigMap && !EnsureBigMapUiCreated())
    {
      _cursorOverlay.Update(false);
      _overlay.SetVisible(false);
      return;
    }

    UpdateMapControlArming(bigMap);
    bool controlsVisible = bigMap && !_manager.IsShowing;
    if (bigMap)
    {
      _controls.UpdateMapBounds(_map);
      _manager.UpdateMapBounds(_map);
    }
    _controls.SetVisible(controlsVisible);
    UpdateSelectionControls();

    _manager.Update();
    bool cursorOverlayNeeded =
        bigMap &&
        ((controlsVisible && _controls.IsCursorOverVisuals) ||
         _manager.IsCursorOverVisuals);
    _cursorOverlay.Update(cursorOverlayNeeded);
    if (!bigMap)
    {
      _manager.Hide();
      if (Manager.main == null || Manager.main.player == null)
      {
        _overlay.SetVisible(false);
        return;
      }

      Vector3 playerWorld = Manager.main.player.WorldPosition;
      ChunkCoordinate playerChunk =
          ChunkCoordinate.FromWorldPosition(
              new float2(playerWorld.x, playerWorld.z));
      _overlay.SetPendingSelection(_selectedMapChunks);
      _overlay.Update(
          _map,
          playerChunk,
          _overlayVisible,
          showHover: false);
      _mouseDown = false;
      return;
    }

    float2 cursorWorld = _map.GetCursorWorldPosition();
    ChunkCoordinate hovered =
        ChunkCoordinate.FromWorldPosition(cursorWorld);
    _overlay.SetPendingSelection(_selectedMapChunks);
    _overlay.Update(
        _map,
        hovered,
        _overlayVisible && !_manager.IsShowing,
        showHover: true);

    if (_manager.IsShowing || !_overlayVisible)
    {
      _mouseDown = false;
      return;
    }

    if (!_overlay.IsInteractiveGridVisible(_map))
    {
      _mouseDown = false;
      return;
    }

    HandleMapClick(hovered);
  }

  private bool TryEnsureMap()
  {
    MapUI map = Manager.ui != null ? Manager.ui.mapUI : null;
    if (map == null ||
        Manager.ui.filteringUI == null ||
        Manager.ui.filteringUI.title == null ||
        Manager.ui.filteringUI.title.text == null ||
        map.largeMapBorder == null ||
        map.largeMapBackground == null ||
        map.miniMapBackground == null ||
        map.mapPartsContainer == null)
    {
      return false;
    }

    if (_map == map)
    {
      return true;
    }

    DestroyRuntimeUi();
    _map = map;
    _overlay.EnsureCreated(map);
    _prewarmStage = 0;
    _prewarmFailedForCurrentMap = false;
    _nextPrewarmStepAt =
        Time.realtimeSinceStartupAsDouble + 0.75d;
    return true;
  }

  private void PrewarmBigMapUi(bool bigMap)
  {
    if (bigMap ||
        _map == null ||
        _prewarmFailedForCurrentMap ||
        _prewarmStage >= 3 ||
        Time.realtimeSinceStartupAsDouble < _nextPrewarmStepAt)
    {
      return;
    }

    double now = Time.realtimeSinceStartupAsDouble;
    try
    {
      if (_prewarmStage == 0)
      {
        if (!_manager.EnsureCreated(_map))
        {
          _nextPrewarmStepAt = now + 0.50d;
          return;
        }

        _prewarmStage = 1;
        _nextPrewarmStepAt = now + 0.10d;
        return;
      }

      if (_prewarmStage == 1)
      {
        CreateMapControls();
        _controls.SetArmed(false);
        _prewarmStage = 2;
        _nextPrewarmStepAt = now + 0.10d;
        return;
      }

      if (_prewarmStage == 2)
      {
        _cursorOverlay.Prewarm();
        _prewarmStage = 3;
      }
    }
    catch (System.Exception ex)
    {
      _controls.Destroy();
      _prewarmFailedForCurrentMap = true;
      Debug.LogWarning(
          $"[ChunkLoaderMod] Deferred map UI prewarm failed; " +
          $"the UI will be created on demand instead. {ex.Message}");
    }
  }

  private bool EnsureBigMapUiCreated()
  {
    if (_controls.IsCreated)
    {
      return true;
    }

    if (_failedUiCreationForCurrentMapOpen)
    {
      return false;
    }

    if (!_manager.EnsureCreated(_map))
    {
      _failedUiCreationForCurrentMapOpen = true;
      return false;
    }

    try
    {
      CreateMapControls();
      _mapControlsArmed = false;
      _armMapControlsAt =
          Time.realtimeSinceStartupAsDouble + 0.25d;
      _controls.SetArmed(false);
      return true;
    }
    catch (System.Exception ex)
    {
      _controls.Destroy();
      _failedUiCreationForCurrentMapOpen = true;
      Debug.LogError(
          $"[ChunkLoaderMod] Failed to create the map controls. {ex}");
      return false;
    }
  }

  private void UpdateMapControlArming(bool bigMap)
  {
    if (!bigMap || !_controls.IsCreated)
    {
      return;
    }

    if (!_mapControlsArmed &&
        Time.realtimeSinceStartupAsDouble >= _armMapControlsAt &&
        !Input.GetMouseButton(0))
    {
      _mapControlsArmed = true;
    }

    _controls.SetArmed(_mapControlsArmed);
  }

  private void CreateMapControls()
  {
    _controls.Create(
        ToggleOverlayFromMapButton,
        ShowManagerFromMapButton,
        ClaimSelectedChunks);
    _controls.SetVisible(false);
    _controls.SetGridVisible(_overlayVisible);
    UpdateSelectionControls();
  }

  private void ToggleOverlayFromMapButton()
  {
    if (!_mapControlsArmed ||
        _map == null ||
        !_map.IsShowingBigMap)
    {
      return;
    }

    SetOverlayVisible(!_overlayVisible);
  }

  private void ShowManagerFromMapButton()
  {
    if (!_mapControlsArmed ||
        _map == null ||
        !_map.IsShowingBigMap)
    {
      return;
    }

    _manager.Show();
  }

  private void HandleMapClick(ChunkCoordinate hovered)
  {
    double now = Time.realtimeSinceStartupAsDouble;
    if (_mapPopupOpen || now < _suppressMapClicksUntil)
    {
      _mouseDown = false;
      return;
    }

    if (Input.GetMouseButtonDown(0))
    {
      _mouseDown = true;
      _mouseDownPosition = Input.mousePosition;
    }

    if (!_mouseDown || !Input.GetMouseButtonUp(0))
    {
      return;
    }

    _mouseDown = false;
    if ((Input.mousePosition - _mouseDownPosition).sqrMagnitude > 25.0f)
    {
      return;
    }

    if (_controls.IsPointerOver)
    {
      return;
    }

    if (ChunkLoaderNetworkState.TryGetRecord(
            hovered,
            out ChunkLoaderRegistrationRecord existing))
    {
      _manager.FocusRegistration(existing.registrationId);
      return;
    }

    long coordinateKey = hovered.ToKey();
    if (_selectedMapChunkKeys.Contains(coordinateKey))
    {
      RemoveSelectedChunk(coordinateKey);
      return;
    }

    if (_pendingClaimKeys.Contains(coordinateKey))
    {
      ShowInformation("This chunk is already queued for claiming.");
      return;
    }

    if (!IsChunkRevealed(hovered))
    {
      ShowInformation(
          "This chunk has not been revealed on your map yet. " +
          "Chunk loaders can keep explored areas active, but they cannot generate unknown world regions remotely.");
      return;
    }

    AddSelectedChunk(hovered);
  }

  private bool IsChunkRevealed(ChunkCoordinate coordinate)
  {
    if (_map == null || _map.MapParts == null)
    {
      return false;
    }

    int2[] probes =
    {
      coordinate.Center,
      coordinate.Origin + new int2(1, 1),
      coordinate.Origin + new int2(ChunkLoaderConstants.ChunkSize - 2, 1),
      coordinate.Origin + new int2(1, ChunkLoaderConstants.ChunkSize - 2),
      coordinate.Origin + new int2(
          ChunkLoaderConstants.ChunkSize - 2,
          ChunkLoaderConstants.ChunkSize - 2)
    };

    for (int i = 0; i < probes.Length; i++)
    {
      Vector2Int mapPartIndex =
          MapUI.WorldPositionToMapPartIndex(new float2(probes[i].x, probes[i].y));
      if (!_map.MapParts.TryGetValue(
              mapPartIndex,
              out MapPartSerialized serialized) ||
          serialized.png == null ||
          serialized.png.Length == 0)
      {
        continue;
      }

      Texture2D texture = new Texture2D(
          2,
          2,
          TextureFormat.RGBA32,
          false,
          true);
      try
      {
        if (!ImageConversion.LoadImage(texture, serialized.png, false))
        {
          continue;
        }

        int2 local = MapUI.WorldPositionToMapPartPosition(probes[i]);
        int x = MathMod(local.x, texture.width);
        int y = MathMod(local.y, texture.height);
        if (texture.GetPixel(x, y).a > 0.01f)
        {
          return true;
        }
      }
      finally
      {
        Destroy(texture);
      }
    }

    return false;
  }

  private static int MathMod(int value, int divisor)
  {
    int result = value % divisor;
    return result < 0 ? result + divisor : result;
  }

  private void ShowInformation(string message)
  {
    if (Manager.menu == null || _mapPopupOpen)
    {
      return;
    }

    _mapPopupOpen = true;
    _mouseDown = false;
    Manager.menu.centerPopUpText.StartNewDisplaySequence(
        message,
        null,
        true,
        0.0f,
        2.0f,
        true,
        0.0f,
        1.0f,
        false,
        TextManager.FontFace.boldMedium,
        _ => FinishMapPopup(),
        new List<string> { "ok" },
        10.0f,
        0.95f,
        0,
        18.0f,
        false,
        false);
  }

  private void FinishMapPopup()
  {
    _mapPopupOpen = false;
    _mouseDown = false;
    _suppressMapClicksUntil =
        Time.realtimeSinceStartupAsDouble + 0.25d;
  }

  private void UpdateOverlayButtonText()
  {
    _controls.SetGridVisible(_overlayVisible);
  }

  private void AddSelectedChunk(ChunkCoordinate coordinate)
  {
    long key = coordinate.ToKey();
    if (_selectedMapChunkKeys.Add(key))
    {
      _selectedMapChunks.Add(coordinate);
      UpdateSelectionControls();
    }
  }

  private void RemoveSelectedChunk(long key)
  {
    if (!_selectedMapChunkKeys.Remove(key))
    {
      return;
    }

    for (int i = _selectedMapChunks.Count - 1; i >= 0; i--)
    {
      if (_selectedMapChunks[i].ToKey() == key)
      {
        _selectedMapChunks.RemoveAt(i);
        break;
      }
    }

    UpdateSelectionControls();
  }

  private void ClearMapSelection()
  {
    if (_selectedMapChunks.Count == 0 &&
        _selectedMapChunkKeys.Count == 0)
    {
      return;
    }

    _selectedMapChunks.Clear();
    _selectedMapChunkKeys.Clear();
    if (_overlay != null)
    {
      _overlay.SetPendingSelection(_selectedMapChunks);
    }
    UpdateSelectionControls();
  }

  private void ClaimSelectedChunks()
  {
    if (!_mapControlsArmed ||
        _selectedMapChunks.Count == 0 ||
        _claimQueue.Count > 0)
    {
      return;
    }

    for (int i = 0; i < _selectedMapChunks.Count; i++)
    {
      ChunkCoordinate coordinate = _selectedMapChunks[i];
      long key = coordinate.ToKey();
      if (_pendingClaimKeys.Contains(key) ||
          ChunkLoaderNetworkState.TryGetRecord(coordinate, out _))
      {
        continue;
      }

      _claimQueue.Enqueue(coordinate);
      _pendingClaimKeys.Add(key);
    }

    ClearMapSelection();
    UpdateSelectionControls();
  }

  private void ProcessClaimQueue()
  {
    int sent = 0;
    while (_claimQueue.Count > 0 && sent < ClaimsPerFrame)
    {
      ChunkCoordinate coordinate = _claimQueue.Dequeue();
      long key = coordinate.ToKey();
      if (ChunkLoaderNetworkState.TryGetRecord(coordinate, out _))
      {
        _pendingClaimKeys.Remove(key);
        continue;
      }

      uint requestId = ChunkLoaderNetworkState.Create(coordinate);
      if (requestId == 0)
      {
        _pendingClaimKeys.Remove(key);
        continue;
      }

      _claimRequestKeys[requestId] = key;
      sent++;
    }

    UpdateSelectionControls();
  }

  private void UpdateSelectionControls()
  {
    _controls.SetSelectionState(
        _selectedMapChunks.Count,
        _claimQueue.Count + _claimRequestKeys.Count);
  }

  private void ClearClaimQueue()
  {
    _claimQueue.Clear();
    _pendingClaimKeys.Clear();
    _claimRequestKeys.Clear();
    UpdateSelectionControls();
  }

  private void OnMutationCompleted(
      uint requestId,
      ChunkLoaderMutationResult result)
  {
    if (_claimRequestKeys.TryGetValue(requestId, out long key))
    {
      _claimRequestKeys.Remove(requestId);
      _pendingClaimKeys.Remove(key);
      UpdateSelectionControls();
    }
  }

  private void OnEnable()
  {
    ChunkLoaderNetworkState.MutationCompleted += OnMutationCompleted;
  }

  private void OnDisable()
  {
    ChunkLoaderNetworkState.MutationCompleted -= OnMutationCompleted;
  }

  private void OnDestroy()
  {
    ClearClaimQueue();
    DestroyRuntimeUi();
    if (_instance == this)
    {
      _instance = null;
    }
  }

  private void DestroyRuntimeUi()
  {
    ClearMapSelection();
    _cursorOverlay.Destroy();
    _manager.Destroy();
    _overlay.Destroy();
    _controls.Destroy();
    _map = null;
    _mapPopupOpen = false;
    _mapControlsArmed = false;
    _failedUiCreationForCurrentMapOpen = false;
    _prewarmFailedForCurrentMap = false;
    _wasShowingBigMap = false;
    _prewarmStage = 0;
    _armMapControlsAt = 0.0d;
    _nextPrewarmStepAt = 0.0d;
    _suppressMapClicksUntil = 0.0d;
  }
}
