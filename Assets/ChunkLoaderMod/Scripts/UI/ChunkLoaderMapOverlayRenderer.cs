using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class ChunkLoaderMapOverlayRenderer
{
  private const int BaseSortingOrder = 410;
  private const int MaximumLinesPerAxis = 256;
  private const int MaximumGridLineRenderers = 16384;
  private static readonly Color FineGridColor =
      new Color(0.50f, 0.54f, 0.56f, 0.30f);
  private static readonly Color SelectionFillColor =
      new Color(1.0f, 0.83f, 0.18f, 0.28f);

  private readonly Dictionary<ulong, SpriteRenderer> _recordFills = new();
  private readonly List<ChunkLoaderRegistrationRecord> _records = new();
  private readonly List<ChunkCoordinate> _pendingSelection = new();
  private readonly List<SpriteRenderer> _selectionFills = new();
  private readonly SpriteRenderer[] _hoverLines = new SpriteRenderer[4];
  private readonly List<SpriteRenderer> _gridLines = new();

  private GameObject _root;
  private GameObject _gridObject;
  private SpriteRenderer _background;
  private Material _material;
  private Sprite _pixelSprite;
  private int _activeGridLineCount;
  private int _activeSelectionFillCount;
  private int _renderedRecordsVersion = int.MinValue;

  public bool IsCreated => _root != null;

  public bool IsInteractiveGridVisible(MapUI map)
  {
    return ShouldShowFineGrid(map);
  }

  public void EnsureCreated(MapUI map)
  {
    if (_root != null || map == null || map.mapPartsContainer == null)
    {
      return;
    }

    _pixelSprite = Sprite.Create(
        Texture2D.whiteTexture,
        new Rect(0, 0, 1, 1),
        new Vector2(0.5f, 0.5f),
        1.0f,
        0,
        SpriteMeshType.FullRect);
    _pixelSprite.name = "ChunkLoaderPixel";

    _material = map.mapContentMaterial != null
        ? Object.Instantiate(map.mapContentMaterial)
        : null;
    if (_material != null)
    {
      _material.name = "ChunkLoader Map Overlay Material";
    }

    _root = new GameObject("ChunkLoaderMapOverlay");
    _root.layer = ObjectLayerID.UI;
    _root.transform.SetParent(map.mapPartsContainer.transform, false);
    _root.transform.localPosition = Vector3.zero;

    _background = CreateRenderer(
        "InactiveChunkTint",
        new Color(0.08f, 0.09f, 0.10f, 0.12f),
        BaseSortingOrder);
    CreateGridRenderer();

    for (int i = 0; i < _hoverLines.Length; i++)
    {
      _hoverLines[i] = CreateRenderer(
          "HoveredChunkBorder",
          new Color(0.74f, 0.93f, 1.0f, 0.85f),
          BaseSortingOrder + 8);
    }

    _root.SetActive(false);
  }

  public void SetVisible(bool visible)
  {
    if (_root != null)
    {
      _root.SetActive(visible);
    }
  }

  public void SetPendingSelection(IReadOnlyList<ChunkCoordinate> selection)
  {
    _pendingSelection.Clear();
    if (selection == null)
    {
      return;
    }

    for (int i = 0; i < selection.Count; i++)
    {
      _pendingSelection.Add(selection[i]);
    }
  }

  public void Update(
      MapUI map,
      ChunkCoordinate focusCoordinate,
      bool visible,
      bool showHover)
  {
    EnsureCreated(map);
    if (_root == null)
    {
      return;
    }

    _root.SetActive(visible);
    if (!visible)
    {
      return;
    }

    bool bigMap = map != null && map.IsShowingBigMap;
    UpdateMaterialMask(map, bigMap);
    PositionGrid(map, bigMap);

    bool hoverVisible =
        showHover && ShouldShowFineGrid(map);
    for (int i = 0; i < _hoverLines.Length; i++)
    {
      _hoverLines[i].gameObject.SetActive(hoverVisible);
    }
    if (hoverVisible)
    {
      PositionHover(focusCoordinate, GetPixelThickness(map));
    }
    RefreshRecordFills();
    RefreshSelectionFills();
  }

  public void Destroy()
  {
    if (_root != null)
    {
      Object.Destroy(_root);
    }

    if (_material != null)
    {
      Object.Destroy(_material);
    }
    if (_pixelSprite != null)
    {
      Object.Destroy(_pixelSprite);
    }

    _root = null;
    _gridObject = null;
    _material = null;
    _pixelSprite = null;
    _gridLines.Clear();
    _activeGridLineCount = 0;
    _recordFills.Clear();
    _pendingSelection.Clear();
    _selectionFills.Clear();
    _activeSelectionFillCount = 0;
    _renderedRecordsVersion = int.MinValue;
  }

  private void PositionGrid(MapUI map, bool bigMap)
  {
    if (!TryGetVisibleLocalBounds(
            map,
            bigMap,
            out float minX,
            out float minY,
            out float maxX,
            out float maxY))
    {
      return;
    }

    float width = maxX - minX;
    float height = maxY - minY;
    float centerX = (minX + maxX) * 0.5f;
    float centerY = (minY + maxY) * 0.5f;
    _background.transform.localPosition =
        new Vector3(centerX, centerY, -0.02f);
    _background.transform.localScale =
        new Vector3(width, height, 1.0f);

    bool showGrid = ShouldShowFineGrid(map);
    if (_gridObject != null)
    {
      _gridObject.SetActive(showGrid);
    }
    if (!showGrid)
    {
      HideGridLines();
      return;
    }

    const float chunkSize = ChunkLoaderConstants.ChunkSize;
    int firstX = Mathf.FloorToInt(minX / chunkSize);
    int lastX = Mathf.CeilToInt(maxX / chunkSize);
    int firstY = Mathf.FloorToInt(minY / chunkSize);
    int lastY = Mathf.CeilToInt(maxY / chunkSize);
    int verticalCount =
        Mathf.Min(MaximumLinesPerAxis, lastX - firstX + 1);
    int horizontalCount =
        Mathf.Min(MaximumLinesPerAxis, lastY - firstY + 1);
    RebuildGridLines(
        minX,
        minY,
        maxX,
        maxY,
        firstX,
        firstY,
        verticalCount,
        horizontalCount,
        chunkSize,
        GetGridThickness(map),
        FineGridColor);
  }

  private void PositionHover(
      ChunkCoordinate coordinate,
      float thickness)
  {
    int2 origin = coordinate.Origin;
    float centerX = origin.x + ChunkLoaderConstants.ChunkSize * 0.5f;
    float centerY = origin.y + ChunkLoaderConstants.ChunkSize * 0.5f;
    float size = ChunkLoaderConstants.ChunkSize;

    PositionLine(_hoverLines[0], centerX, origin.y, size, thickness);
    PositionLine(_hoverLines[1], centerX, origin.y + size, size, thickness);
    PositionLine(_hoverLines[2], origin.x, centerY, thickness, size);
    PositionLine(_hoverLines[3], origin.x + size, centerY, thickness, size);
  }

  private static void PositionLine(
      SpriteRenderer line,
      float x,
      float y,
      float width,
      float height)
  {
    line.transform.localPosition = new Vector3(x, y, -0.05f);
    line.transform.localScale = new Vector3(width, height, 1.0f);
  }

  private void RefreshRecordFills()
  {
    int recordsVersion = ChunkLoaderNetworkState.RecordsVersion;
    if (_renderedRecordsVersion == recordsVersion)
    {
      return;
    }

    _renderedRecordsVersion = recordsVersion;
    ChunkLoaderNetworkState.GetRecordsUnsorted(_records);
    HashSet<ulong> seen = new HashSet<ulong>();
    for (int i = 0; i < _records.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _records[i];
      seen.Add(record.registrationId);
      if (!_recordFills.TryGetValue(record.registrationId, out SpriteRenderer fill))
      {
        fill = CreateRenderer(
            "RegisteredChunkFill",
            Color.white,
            BaseSortingOrder + 4);
        _recordFills.Add(record.registrationId, fill);
      }

      int2 center = record.Coordinate.Center;
      fill.transform.localPosition =
          new Vector3(center.x, center.y, -0.04f);
      fill.transform.localScale = new Vector3(
          ChunkLoaderConstants.ChunkSize,
          ChunkLoaderConstants.ChunkSize,
          1.0f);
      fill.color = GetRecordColor(record);
      fill.gameObject.SetActive(true);
    }

    List<ulong> removed = new List<ulong>();
    foreach (KeyValuePair<ulong, SpriteRenderer> entry in _recordFills)
    {
      if (!seen.Contains(entry.Key))
      {
        Object.Destroy(entry.Value.gameObject);
        removed.Add(entry.Key);
      }
    }

    for (int i = 0; i < removed.Count; i++)
    {
      _recordFills.Remove(removed[i]);
    }
  }

  private void RefreshSelectionFills()
  {
    for (int i = 0; i < _pendingSelection.Count; i++)
    {
      SpriteRenderer fill = GetSelectionFill(i);
      int2 center = _pendingSelection[i].Center;
      fill.transform.localPosition =
          new Vector3(center.x, center.y, -0.045f);
      fill.transform.localScale = new Vector3(
          ChunkLoaderConstants.ChunkSize,
          ChunkLoaderConstants.ChunkSize,
          1.0f);
      fill.color = SelectionFillColor;
      fill.gameObject.SetActive(true);
    }

    for (int i = _pendingSelection.Count; i < _activeSelectionFillCount; i++)
    {
      _selectionFills[i].gameObject.SetActive(false);
    }

    _activeSelectionFillCount = _pendingSelection.Count;
  }

  private SpriteRenderer GetSelectionFill(int index)
  {
    while (_selectionFills.Count <= index)
    {
      SpriteRenderer fill = CreateRenderer(
          "SelectedChunkFill",
          SelectionFillColor,
          BaseSortingOrder + 5);
      fill.gameObject.SetActive(false);
      _selectionFills.Add(fill);
    }

    return _selectionFills[index];
  }

  private static Color GetRecordColor(ChunkLoaderRegistrationRecord record)
  {
    switch (record.actualState)
    {
      case ChunkLoaderRuntimeState.Loaded:
        return new Color(0.30f, 0.90f, 0.43f, 0.19f);

      case ChunkLoaderRuntimeState.Loading:
      case ChunkLoaderRuntimeState.QueuedForLoad:
      case ChunkLoaderRuntimeState.QueuedForUnload:
        return new Color(0.95f, 0.72f, 0.25f, 0.18f);

      case ChunkLoaderRuntimeState.Error:
        return new Color(0.92f, 0.28f, 0.28f, 0.20f);

      default:
        return new Color(0.40f, 0.52f, 0.62f, 0.15f);
    }
  }

  private SpriteRenderer CreateRenderer(
      string name,
      Color color,
      int sortingOrder)
  {
    GameObject gameObject = new GameObject(name);
    gameObject.layer = ObjectLayerID.UI;
    gameObject.transform.SetParent(_root.transform, false);
    SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
    renderer.sprite = _pixelSprite;
    renderer.color = color;
    renderer.sortingLayerID = SortingLayerID.GUI;
    renderer.sortingOrder = sortingOrder;
    if (_material != null)
    {
      renderer.sharedMaterial = _material;
    }

    return renderer;
  }

  private void CreateGridRenderer()
  {
    _gridObject = new GameObject("FineChunkGrid");
    _gridObject.layer = ObjectLayerID.UI;
    _gridObject.transform.SetParent(_root.transform, false);
    _gridObject.transform.localPosition = Vector3.zero;
    _gridObject.SetActive(false);
  }

  private void RebuildGridLines(
      float minX,
      float minY,
      float maxX,
      float maxY,
      int firstX,
      int firstY,
      int verticalCount,
      int horizontalCount,
      float chunkSize,
      float thickness,
      Color color)
  {
    if (_gridObject == null)
    {
      return;
    }

    float half = thickness * 0.5f;
    const float z = -0.03f;
    int estimatedSegments =
        verticalCount +
        horizontalCount * (verticalCount + 1);
    if (estimatedSegments > MaximumGridLineRenderers)
    {
      _gridObject.SetActive(false);
      HideGridLines();
      return;
    }

    _gridObject.SetActive(true);
    int lineIndex = 0;
    for (int i = 0; i < verticalCount; i++)
    {
      float x = (firstX + i) * chunkSize;
      float xMin = Mathf.Max(minX, x - half);
      float xMax = Mathf.Min(maxX, x + half);
      lineIndex = AddGridLine(
          lineIndex,
          (xMin + xMax) * 0.5f,
          (minY + maxY) * 0.5f,
          xMax - xMin,
          maxY - minY,
          z,
          color);
    }

    for (int yIndex = 0; yIndex < horizontalCount; yIndex++)
    {
      float y = (firstY + yIndex) * chunkSize;
      float yMin = Mathf.Max(minY, y - half);
      float yMax = Mathf.Min(maxY, y + half);
      if (yMax <= yMin)
      {
        continue;
      }

      float segmentStart = minX;
      for (int xIndex = 0; xIndex < verticalCount; xIndex++)
      {
        float x = (firstX + xIndex) * chunkSize;
        float skipStart = Mathf.Max(minX, x - half);
        float skipEnd = Mathf.Min(maxX, x + half);
        lineIndex = AddGridLine(
            lineIndex,
            (segmentStart + skipStart) * 0.5f,
            (yMin + yMax) * 0.5f,
            skipStart - segmentStart,
            yMax - yMin,
            z,
            color);
        segmentStart = Mathf.Max(segmentStart, skipEnd);
      }

      lineIndex = AddGridLine(
          lineIndex,
          (segmentStart + maxX) * 0.5f,
          (yMin + yMax) * 0.5f,
          maxX - segmentStart,
          yMax - yMin,
          z,
          color);
    }

    for (int i = lineIndex; i < _activeGridLineCount; i++)
    {
      _gridLines[i].gameObject.SetActive(false);
    }
    _activeGridLineCount = lineIndex;
  }

  private int AddGridLine(
      int index,
      float x,
      float y,
      float width,
      float height,
      float z,
      Color color)
  {
    if (width <= 0.0f || height <= 0.0f)
    {
      return index;
    }

    SpriteRenderer line = GetGridLine(index);
    line.color = color;
    line.transform.localPosition = new Vector3(x, y, z);
    line.transform.localScale = new Vector3(width, height, 1.0f);
    line.gameObject.SetActive(true);
    return index + 1;
  }

  private SpriteRenderer GetGridLine(int index)
  {
    while (_gridLines.Count <= index)
    {
      GameObject lineObject = new GameObject("FineChunkGridLine");
      lineObject.layer = ObjectLayerID.UI;
      lineObject.transform.SetParent(_gridObject.transform, false);
      SpriteRenderer line = lineObject.AddComponent<SpriteRenderer>();
      line.sprite = _pixelSprite;
      line.sortingLayerID = SortingLayerID.GUI;
      line.sortingOrder = BaseSortingOrder + 6;
      if (_material != null)
      {
        line.sharedMaterial = _material;
      }
      lineObject.SetActive(false);
      _gridLines.Add(line);
    }

    return _gridLines[index];
  }

  private void HideGridLines()
  {
    for (int i = 0; i < _activeGridLineCount; i++)
    {
      _gridLines[i].gameObject.SetActive(false);
    }
    _activeGridLineCount = 0;
  }

  private bool TryGetVisibleLocalBounds(
      MapUI map,
      bool bigMap,
      out float minX,
      out float minY,
      out float maxX,
      out float maxY)
  {
    SpriteRenderer mask = map == null
        ? null
        : bigMap
            ? map.largeMapBackground
            : map.miniMapBackground;
    if (mask == null || _root == null)
    {
      minX = minY = maxX = maxY = 0.0f;
      return false;
    }

    Bounds bounds = mask.bounds;
    Vector3 localMin = _root.transform.InverseTransformPoint(
        new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
    Vector3 localMax = _root.transform.InverseTransformPoint(
        new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
    minX = Mathf.Min(localMin.x, localMax.x);
    minY = Mathf.Min(localMin.y, localMax.y);
    maxX = Mathf.Max(localMin.x, localMax.x);
    maxY = Mathf.Max(localMin.y, localMax.y);
    return maxX > minX && maxY > minY;
  }

  private static float GetPixelThickness(MapUI map)
  {
    float zoom = map?.zoomTransform != null
        ? Mathf.Abs(map.zoomTransform.localScale.x)
        : 1.0f;
    return 0.0625f / Mathf.Max(zoom, 0.0001f);
  }

  private static float GetGridThickness(MapUI map)
  {
    return GetPixelThickness(map) * 1.05f;
  }

  private static bool ShouldShowFineGrid(MapUI map)
  {
    return GetProjectedGridPixels(
        map,
        ChunkLoaderConstants.ChunkSize) >= 10.0f;
  }

  private static float GetProjectedGridPixels(
      MapUI map,
      float gridSize)
  {
    float zoom = map?.zoomTransform != null
        ? Mathf.Abs(map.zoomTransform.localScale.x)
        : 1.0f;
    return gridSize * zoom * 16.0f;
  }

  private void UpdateMaterialMask(MapUI map, bool bigMap)
  {
    SpriteRenderer mask = map == null
        ? null
        : bigMap
            ? map.largeMapBackground
            : map.miniMapBackground;
    if (_material == null || mask == null)
    {
      return;
    }

    Bounds bounds = mask.bounds;
    Vector3 min = bounds.min;
    Vector3 max = bounds.max;
    float width = max.x - min.x;
    float height = max.y - min.y;
    if (width <= 0.0f || height <= 0.0f)
    {
      return;
    }

    _material.SetVector(
        Shader.PropertyToID("_MaskRect"),
        new Vector4(min.x, min.y, 1.0f / width, 1.0f / height));
  }
}
