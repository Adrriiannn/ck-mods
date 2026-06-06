using System.Collections.Generic;
using I2.Loc;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public sealed class SmartSplitterFilterPanelController : MonoBehaviour
{
  private const int PanelSortingOrderBase = 20;
  private const int RuntimeIconSortingOrder = 500;
  private const int DropdownRuntimeSortingOrder = 620;
  private const float UiPixelsPerUnit = 16.0f;
  private const float PanelLayoutScale = 1.0f;
  private const float DropdownHeaderIconSize = 0.25f;
  private const float FilterIconSize = 0.5f;
  private const float DropdownOptionIconSize = 0.5f;
  private const float DropdownRowHeight = 0.5f;
  private const float DropdownTextScale = 0.5f;
  private const float NativeButtonInputZ = -2.25f;
  private const float PanelBlockerInputZ = -2.0f;
  private const float DropdownOptionInputZ = -2.4f;
  private const float DropdownBlockerInputZ = -2.3f;
  private const float DropdownArrowVerticalNudge = 1.5f / UiPixelsPerUnit;
  private const float DropdownContentRightNudge = 4.0f / UiPixelsPerUnit;
  private const float PanelInventoryGap = 2.0f / UiPixelsPerUnit;
  private const int DropdownHistoryColumns = 3;
  private const int MaxVisibleDropdownHistoryRows = 3;
  private const int DropdownHeaderLabelMaxCharacters = 7;
  private const int DropdownOptionLabelMaxCharacters = 8;
  private const float PanelRefreshIntervalSeconds = 0.08f;

  private sealed class LaneWidgets
  {
    public SmartSplitterLane Lane;
    public SmartSplitterNativeButton FilterButton;
    public SmartSplitterNativeButton PickButton;
    public SmartSplitterNativeButton ClearButton;
    public SmartSplitterNativeButton DropdownButton;
    public SmartSplitterNativeButton DropdownArrowButton;
    public GameObject Silhouette;
    public GameObject Highlight;
    public GameObject NoneIcon;
    public GameObject DropdownPanel;
    public GameObject DropdownArrow;
    public SpriteRenderer RuntimeIcon;
    public SpriteRenderer DropdownHeaderIcon;
    public TinyPixelText DropdownHeaderText;
    public int DropdownScrollOffset;
    public readonly List<GameObject> DropdownOptionObjects = new();
  }

  private readonly struct DropdownOptionData
  {
    public DropdownOptionData(DropdownOptionKind kind, ObjectID objectID, int variation)
    {
      Kind = kind;
      ObjectID = objectID;
      Variation = variation;
    }

    public readonly DropdownOptionKind Kind;
    public readonly ObjectID ObjectID;
    public readonly int Variation;
  }

  private enum DropdownOptionKind
  {
    Any,
    None,
    Item
  }

  private struct RememberedFilterItem
  {
    public ObjectID ObjectID;
    public int Variation;
  }

  private static readonly List<RememberedFilterItem> RememberedFilterItems = new();

  private readonly LaneWidgets[] _lanes =
  {
    new LaneWidgets { Lane = SmartSplitterLane.Left },
    new LaneWidgets { Lane = SmartSplitterLane.Center },
    new LaneWidgets { Lane = SmartSplitterLane.Right }
  };

  private readonly Dictionary<string, GameObject> _nativeObjectsByName = new();
  private readonly Dictionary<Sprite, Sprite> _uiPixelSprites = new();
  private static Sprite _dropdownArrowDownSprite;
  private static Sprite _dropdownArrowUpSprite;
  private static Sprite _dropdownPixelSprite;

  private World _world;
  private Entity _splitter;
  private int2 _targetCenter;
  private bool _hasTargetCenter;
  private bool _useDirectEcsTarget;
  private GameObject _panelRoot;
  private GameObject _nativeRoot;
  private SmartSplitterLane? _pendingPickLane;
  private bool _endPendingPickAfterClick;
  private bool _ownsHiddenVanillaFilterUi;
  private bool _hasVanillaFilteringUiOriginalPosition;
  private Transform _vanillaFilteringUiRootTransform;
  private Vector3 _vanillaFilteringUiOriginalPosition;
  private bool _vanillaPickerSessionActive;
  private int _nativeDrawOrder;
  private bool _isBound;
  private bool _loggedFirstShow;
  private SmartSplitterLane? _openDropdownLane;
  private float _nextPanelRefreshAt;

  public bool IsShowing => _nativeRoot != null && _nativeRoot.activeSelf;

  public void Show(World world, Entity splitter)
  {
    int2 center = default;
    bool hasCenter = world != null &&
                     world.IsCreated &&
                     splitter != Entity.Null &&
                     world.EntityManager.Exists(splitter) &&
                     world.EntityManager.HasComponent<SmartSplitterOriginalOutputsCD>(splitter) &&
                     SmartSplitterLaneFilterUtility.TryGetSplitterCenter(
                         world.EntityManager,
                         world.EntityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(splitter),
                         out center);

    if (!hasCenter)
    {
      Hide();
      return;
    }

    Show(world, splitter, center, hasCenter);
  }

  public void Show(World world, Entity splitter, int2 center, bool useDirectEcsTarget)
  {
    EnsureBound();

    _world = world;
    _splitter = splitter;
    _targetCenter = center;
    _hasTargetCenter = true;
    _useDirectEcsTarget = useDirectEcsTarget;
    _nextPanelRefreshAt = 0.0f;

    if (!_useDirectEcsTarget)
    {
      SmartSplitterNetworkState.RequestFilters(center);
    }

    if (_nativeRoot != null)
    {
      _nativeRoot.SetActive(true);
      ConfigureNativeRoot();
    }

    ApplyVanillaUiSprites();
    Refresh();

    if (!_loggedFirstShow)
    {
      _loggedFirstShow = true;
      Debug.Log(
          "[SmartSplitterFilterPanelController] Show requested " +
          $"nativeRoot={(_nativeRoot != null ? _nativeRoot.name : "null")} " +
          $"active={(_nativeRoot != null && _nativeRoot.activeInHierarchy)} " +
          $"parent={(_nativeRoot != null && _nativeRoot.transform.parent != null ? _nativeRoot.transform.parent.name : "null")} " +
          $"renderers={(_nativeRoot != null ? _nativeRoot.GetComponentsInChildren<SpriteRenderer>(true).Length : 0)}");
    }
  }

  public void Hide()
  {
    _world = null;
    _splitter = Entity.Null;
    _targetCenter = default;
    _hasTargetCenter = false;
    _useDirectEcsTarget = false;
    CloseDropdowns();

    if (_nativeRoot != null)
    {
      _nativeRoot.SetActive(false);
    }

    EndPendingPick();
  }

  public void Refresh()
  {
    EnsureBound();

    if (!TryGetCurrentFilters(out SmartSplitterLaneFiltersCD filters))
    {
      filters = SmartSplitterLaneFilterUtility.CreateDefaultLaneFilters();
    }

    for (int i = 0; i < _lanes.Length; i++)
    {
      LaneWidgets lane = _lanes[i];
      SmartSplitterLaneFilter filter = SmartSplitterLaneFilterUtility.GetLaneFilter(filters, lane.Lane);
      RefreshLane(lane, filter);
    }
  }

  private void Awake()
  {
    EnsureBound();
    Hide();
  }

  private void Update()
  {
    if (IsShowing)
    {
      if (_pendingPickLane.HasValue)
      {
        TryApplyHoveredInventoryItemToPendingLane(deferPickerEnd: true);
      }

      if (Time.unscaledTime >= _nextPanelRefreshAt)
      {
        _nextPanelRefreshAt = Time.unscaledTime + PanelRefreshIntervalSeconds;
        ConfigureNativeRoot();
        ApplyVanillaUiSprites();
        Refresh();
      }

      HandleDropdownScroll();
      MaintainFilterPickMouseMode();
    }
  }

  private void LateUpdate()
  {
    if (IsShowing)
    {
      UpdatePendingPick();
    }
  }

  private void EnsureBound()
  {
    if (_isBound)
    {
      return;
    }

    _panelRoot = FindGameObjectInSource("PanelRoot");
    if (_panelRoot == null)
    {
      _panelRoot = gameObject;
    }

    Canvas canvas = GetComponentInChildren<Canvas>(true);
    if (canvas != null)
    {
      canvas.gameObject.SetActive(false);
    }

    BuildNativePanel();

    BindLane(_lanes[0], "Left");
    BindLane(_lanes[1], "Center");
    BindLane(_lanes[2], "Right");
    BindElectricityTooltip();
    NormalizeStaticState();

    _isBound = true;
  }

  private void BuildNativePanel()
  {
    if (_nativeRoot != null)
    {
      return;
    }

    _nativeRoot = new GameObject("SmartSplitterNativePanelRoot");
    SetNativeLayer(_nativeRoot);
    DontDestroyOnLoad(_nativeRoot);
    ConfigureNativeRoot();

    _nativeObjectsByName.Clear();
    _nativeDrawOrder = PanelSortingOrderBase;
    CloneRectTransformTree(_panelRoot.transform, _nativeRoot.transform, true);
    AddPanelInputBlocker();
  }

  private void ConfigureNativeRoot()
  {
    if (_nativeRoot == null)
    {
      return;
    }

    Transform uiRoot = TryGetVanillaUIRoot();
    if (uiRoot != null)
    {
      if (_nativeRoot.transform.parent != uiRoot)
      {
        _nativeRoot.transform.SetParent(uiRoot, false);
      }
    }

    float scale = SmartSplitterDebugSettings.SmartSplitterPanelScale;
    _nativeRoot.transform.localScale = Vector3.one * scale;

    _nativeRoot.transform.localPosition = new Vector3(
        0.0f,
        GetPanelRootY(uiRoot, scale),
        10.0f);
    _nativeRoot.transform.localRotation = Quaternion.identity;
  }

  private static float GetPanelRootY(Transform uiRoot, float rootScale)
  {
    if (TryGetInventoryTopInRootSpace(uiRoot, out float inventoryTop))
    {
      float panelHalfHeight = ToUiUnits(GetAuthoredSize("PanelRoot", null).y) * rootScale * 0.5f;
      return SnapUiUnits(inventoryTop + PanelInventoryGap + panelHalfHeight);
    }

    return SnapUiUnits(2.45f);
  }

  private static bool TryGetInventoryTopInRootSpace(Transform uiRoot, out float topY)
  {
    topY = 0.0f;
    if (uiRoot == null || Manager.ui == null || Manager.ui.playerInventoryUI == null)
    {
      return false;
    }

    ItemSlotsUIContainer inventory = Manager.ui.playerInventoryUI;
    if (!inventory.isShowing)
    {
      return false;
    }

    Bounds bounds = default;
    bool hasBounds = false;
    EncapsulateRendererBounds(inventory.backgroundSR, ref bounds, ref hasBounds);
    if (inventory.additionalBackgroundSRs != null)
    {
      for (int i = 0; i < inventory.additionalBackgroundSRs.Length; i++)
      {
        EncapsulateRendererBounds(inventory.additionalBackgroundSRs[i], ref bounds, ref hasBounds);
      }
    }

    if (inventory.itemSlots != null)
    {
      for (int i = 0; i < inventory.itemSlots.Count; i++)
      {
        SlotUIBase slot = inventory.itemSlots[i];
        if (slot == null || !slot.gameObject.activeInHierarchy)
        {
          continue;
        }

        Renderer[] renderers = slot.GetComponentsInChildren<Renderer>(true);
        for (int j = 0; j < renderers.Length; j++)
        {
          EncapsulateRendererBounds(renderers[j], ref bounds, ref hasBounds);
        }
      }
    }

    if (!hasBounds)
    {
      return false;
    }

    topY = uiRoot.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)).y;
    return true;
  }

  private static void EncapsulateRendererBounds(Renderer renderer, ref Bounds bounds, ref bool hasBounds)
  {
    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
    {
      return;
    }

    if (hasBounds)
    {
      bounds.Encapsulate(renderer.bounds);
      return;
    }

    bounds = renderer.bounds;
    hasBounds = true;
  }

  private static Transform TryGetVanillaUIRoot()
  {
    if (Manager.ui != null &&
        Manager.ui.chestInventoryUI != null &&
        Manager.ui.chestInventoryUI.transform.parent != null)
    {
      return Manager.ui.chestInventoryUI.transform.parent;
    }

    if (Manager.camera != null && Manager.camera.uiCamera != null)
    {
      return Manager.camera.uiCamera.transform;
    }

    return null;
  }

  private void CloneRectTransformTree(Transform source, Transform nativeParent, bool sourceIsPanelRoot)
  {
    RectTransform sourceRect = source as RectTransform;
    GameObject nativeObject = new GameObject(source.name);
    SetNativeLayer(nativeObject);
    nativeObject.transform.SetParent(nativeParent, false);
    nativeObject.transform.localRotation = Quaternion.identity;
    nativeObject.transform.localScale = Vector3.one;

    if (!sourceIsPanelRoot && sourceRect != null)
    {
      nativeObject.transform.localPosition =
          GetNativeLocalPosition(source.name, sourceRect);
    }
    else
    {
      nativeObject.transform.localPosition = Vector3.zero;
    }

    _nativeObjectsByName[source.name] = nativeObject;

    CreateNativeSprite(source, sourceRect, nativeObject);

    for (int i = 0; i < source.childCount; i++)
    {
      CloneRectTransformTree(source.GetChild(i), nativeObject.transform, false);
    }
  }

  private void CreateNativeSprite(Transform source, RectTransform sourceRect, GameObject nativeObject)
  {
    Image image = source.GetComponent<Image>();
    if (image == null || image.sprite == null)
    {
      return;
    }

    GameObject spriteObject = new GameObject(source.name + "_Sprite");
    SetNativeLayer(spriteObject);
    spriteObject.transform.SetParent(nativeObject.transform, false);
    spriteObject.transform.localPosition = Vector3.zero;
    spriteObject.transform.localRotation = Quaternion.identity;
    spriteObject.transform.localScale = Vector3.one;

    SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
    spriteRenderer.sprite = GetUiPixelSprite(image.sprite);
    spriteRenderer.color = image.color;
    spriteRenderer.sortingLayerID = SortingLayerID.GUI;
    spriteRenderer.sortingOrder = _nativeDrawOrder++;
    spriteRenderer.sprite.texture.filterMode = FilterMode.Point;
    spriteRenderer.sprite.texture.wrapMode = TextureWrapMode.Clamp;

    if (sourceRect == null)
    {
      return;
    }

    Vector2 authoredSize = GetAuthoredSize(source.name, sourceRect);
    Vector2 targetSize = new Vector2(
        ToUiUnits(authoredSize.x),
        ToUiUnits(authoredSize.y));

    if (ShouldRenderAtAuthoredPixelSize(source.name, spriteRenderer.sprite, sourceRect, authoredSize))
    {
      spriteRenderer.drawMode = SpriteDrawMode.Simple;
      spriteObject.transform.localScale = Vector3.one;
      return;
    }

    if (ShouldRenderDropdownAsPixelSlices(source.name, image, image.sprite))
    {
      Destroy(spriteObject);
      CreatePixelSlicedNativeSprite(source.name, image.sprite, image.color, sourceRect, nativeObject);
      return;
    }

    if ((image.type == Image.Type.Sliced || image.type == Image.Type.Tiled) &&
        HasSpriteBorder(spriteRenderer.sprite))
    {
      spriteRenderer.drawMode = image.type == Image.Type.Tiled
          ? SpriteDrawMode.Tiled
          : SpriteDrawMode.Sliced;
      spriteRenderer.size = targetSize;
      return;
    }

    Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
    if (spriteSize.x <= 0.0f || spriteSize.y <= 0.0f)
    {
      return;
    }

    float scaleX = targetSize.x / spriteSize.x;
    float scaleY = targetSize.y / spriteSize.y;
    if (image.preserveAspect)
    {
      float scale = Mathf.Min(scaleX, scaleY);
      scaleX = scale;
      scaleY = scale;
    }

    spriteObject.transform.localScale = new Vector3(scaleX, scaleY, 1.0f);
  }

  private static float ToUiUnits(float pixels)
  {
    return SnapUiPixels(pixels * PanelLayoutScale) / UiPixelsPerUnit;
  }

  private static float PixelsToUiUnits(float pixels)
  {
    return SnapUiPixels(pixels * PanelLayoutScale) / UiPixelsPerUnit;
  }

  private static Vector3 GetNativeLocalPosition(string objectName, RectTransform sourceRect)
  {
    Vector2 authoredSize = GetAuthoredSize(objectName, sourceRect);
    Vector2 anchoredPosition = GetAuthoredAnchoredPosition(objectName, sourceRect);
    if (IsDropdownFrameObject(objectName))
    {
      anchoredPosition.x = AlignCenterToPixelGrid(
          anchoredPosition.x * PanelLayoutScale,
          authoredSize.x * PanelLayoutScale) / PanelLayoutScale;
      anchoredPosition.y = AlignCenterToPixelGrid(
          anchoredPosition.y * PanelLayoutScale,
          authoredSize.y * PanelLayoutScale) / PanelLayoutScale;
    }

    return new Vector3(
        PixelsToUiUnits(anchoredPosition.x),
        ShouldPreserveHalfPixelPosition(objectName)
            ? PixelsToUiUnitsUnsnapped(anchoredPosition.y)
            : PixelsToUiUnits(anchoredPosition.y),
        0.0f);
  }

  private static bool ShouldPreserveHalfPixelPosition(string objectName)
  {
    return objectName == "Header" || objectName == "FilteringText";
  }

  private static float PixelsToUiUnitsUnsnapped(float pixels)
  {
    return pixels * PanelLayoutScale / UiPixelsPerUnit;
  }

  private static Vector2 GetAuthoredSize(string objectName, RectTransform sourceRect)
  {
    Vector2 fallback = sourceRect != null ? sourceRect.sizeDelta : Vector2.zero;
    switch (objectName)
    {
      case "SmartSplitterPanel":
      case "PanelRoot":
        return new Vector2(112.0f, 60.0f);
      case "PanelBody":
        return new Vector2(112.0f, 48.0f);
      case "Header":
        return new Vector2(30.0f, 7.0f);
      case "FilteringText":
        return GetHalfAuthoredSize(fallback);
      case "LeftDropdown":
      case "CenterDropdown":
      case "RightDropdown":
        return new Vector2(32.0f, 6.0f);
      case "LeftDropdownPanel":
      case "CenterDropdownPanel":
      case "RightDropdownPanel":
        return new Vector2(32.0f, 48.0f);
      case "DividerLeft":
      case "DividerRight":
        return new Vector2(1.0f, 40.0f);
      case "Icon":
      case "HoverHighlight":
        return GetHalfAuthoredSize(fallback);
      default:
        break;
    }

    if (TryGetLaneLayout(objectName, out _, out string part))
    {
      switch (part)
      {
        case "Text":
        case "FilterButton":
        case "FilterSilhouette":
        case "FilterHighlight":
        case "FilterNone":
        case "Slot0":
        case "Slot1":
          return GetHalfAuthoredSize(fallback);
        case "DropdownArrow":
          return GetHalfAuthoredSize(fallback);
      }
    }

    if (objectName == "ElectricityIconOn" || objectName == "ElectricityIconOff")
    {
      return GetHalfAuthoredSize(fallback);
    }

    return fallback;
  }

  private static Vector2 GetHalfAuthoredSize(Vector2 size)
  {
    return new Vector2(
        Mathf.Max(1.0f, Mathf.Round(size.x * 0.5f)),
        Mathf.Max(1.0f, Mathf.Round(size.y * 0.5f)));
  }

  private static Vector2 GetAuthoredAnchoredPosition(string objectName, RectTransform sourceRect)
  {
    Vector2 fallback = sourceRect != null ? sourceRect.anchoredPosition : Vector2.zero;
    if (string.IsNullOrEmpty(objectName))
    {
      return fallback;
    }

    switch (objectName)
    {
      case "PanelBody":
        return new Vector2(0.0f, -6.0f);
      case "Header":
      case "FilteringText":
        return new Vector2(0.0f, 21.5f);
      case "DividerLeft":
        return new Vector2(-18.0f, -6.0f);
      case "DividerRight":
        return new Vector2(18.0f, -6.0f);
    }

    if (TryGetLaneLayout(objectName, out float laneX, out string part))
    {
      switch (part)
      {
        case "Text":
          return new Vector2(laneX, 11.5f);
        case "FilterButton":
        case "FilterSilhouette":
        case "FilterHighlight":
        case "FilterNone":
          return new Vector2(laneX, 2.5f);
        case "Dropdown":
          return new Vector2(laneX, -11.0f);
        case "DropdownArrow":
          return new Vector2(laneX + 13.5f, -11.5f);
        case "DropdownPanel":
          return new Vector2(laneX, -37.5f);
        case "Slot0":
          return new Vector2(laneX - 6.0f, -20.0f);
        case "Slot1":
          return new Vector2(laneX + 6.0f, -20.0f);
      }
    }

    if (objectName == "ElectricityIconOn" || objectName == "ElectricityIconOff")
    {
      return new Vector2(50.0f, 12.0f);
    }

    return fallback;
  }

  private static bool TryGetLaneLayout(string objectName, out float laneX, out string part)
  {
    laneX = 0.0f;
    part = null;
    if (objectName.StartsWith("Left"))
    {
      laneX = -36.0f;
      part = objectName.Substring("Left".Length);
      return true;
    }

    if (objectName.StartsWith("Center"))
    {
      laneX = 0.0f;
      part = objectName.Substring("Center".Length);
      return true;
    }

    if (objectName.StartsWith("Right"))
    {
      laneX = 36.0f;
      part = objectName.Substring("Right".Length);
      return true;
    }

    return false;
  }

  private static bool IsDropdownFrameObject(string objectName)
  {
    return !string.IsNullOrEmpty(objectName) &&
           (objectName.EndsWith("Dropdown") || objectName.EndsWith("DropdownPanel"));
  }

  private static float AlignCenterToPixelGrid(float centerPixels, float sizePixels)
  {
    int roundedSize = Mathf.RoundToInt(sizePixels);
    if ((roundedSize & 1) == 0)
    {
      return Mathf.Round(centerPixels);
    }

    float floor = Mathf.Floor(centerPixels);
    return floor + 0.5f;
  }

  private static bool ShouldRenderDropdownAsPixelSlices(string objectName, Image image, Sprite sprite)
  {
    return image != null &&
           sprite != null &&
           !string.IsNullOrEmpty(objectName) &&
           objectName.Contains("Dropdown") &&
           image.type == Image.Type.Sliced &&
           HasSpriteBorder(sprite);
  }

  private void CreatePixelSlicedNativeSprite(
      string objectName,
      Sprite sourceSprite,
      Color color,
      RectTransform sourceRect,
      GameObject nativeObject)
  {
    if (sourceSprite == null || sourceRect == null || nativeObject == null)
    {
      return;
    }

    Vector2 authoredSize = GetAuthoredSize(objectName, sourceRect);
    int targetWidth = Mathf.RoundToInt(SnapUiPixels(authoredSize.x * PanelLayoutScale));
    int targetHeight = Mathf.RoundToInt(SnapUiPixels(authoredSize.y * PanelLayoutScale));
    int sourceWidth = Mathf.RoundToInt(sourceSprite.rect.width);
    int sourceHeight = Mathf.RoundToInt(sourceSprite.rect.height);
    if (targetWidth <= 0 || targetHeight <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
    {
      return;
    }

    Vector4 border = sourceSprite.border;
    int left = Mathf.Clamp(Mathf.RoundToInt(border.x), 0, sourceWidth);
    int right = Mathf.Clamp(Mathf.RoundToInt(border.z), 0, sourceWidth - left);
    int bottom = Mathf.Clamp(Mathf.RoundToInt(border.y), 0, sourceHeight);
    int top = Mathf.Clamp(Mathf.RoundToInt(border.w), 0, sourceHeight - bottom);
    int sourceCenterWidth = Mathf.Max(0, sourceWidth - left - right);
    int sourceCenterHeight = Mathf.Max(0, sourceHeight - bottom - top);
    int targetCenterWidth = Mathf.Max(0, targetWidth - left - right);
    int targetCenterHeight = Mathf.Max(0, targetHeight - bottom - top);

    int[] sourceXs = { 0, left, left + sourceCenterWidth };
    int[] sourceYs = { 0, bottom, bottom + sourceCenterHeight };
    int[] sourceWidths = { left, sourceCenterWidth, right };
    int[] sourceHeights = { bottom, sourceCenterHeight, top };
    int[] targetXs = { 0, left, left + targetCenterWidth };
    int[] targetYs = { 0, bottom, bottom + targetCenterHeight };
    int[] targetWidths = { left, targetCenterWidth, right };
    int[] targetHeights = { bottom, targetCenterHeight, top };

    int sortingOrder = _nativeDrawOrder++;
    for (int y = 0; y < 3; y++)
    {
      for (int x = 0; x < 3; x++)
      {
        CreatePixelSlice(
            objectName,
            sourceSprite,
            nativeObject.transform,
            color,
            sortingOrder,
            sourceXs[x],
            sourceYs[y],
            sourceWidths[x],
            sourceHeights[y],
            targetXs[x],
            targetYs[y],
            targetWidths[x],
            targetHeights[y],
            targetWidth,
            targetHeight);
      }
    }
  }

  private void CreatePixelSlice(
      string objectName,
      Sprite sourceSprite,
      Transform parent,
      Color color,
      int sortingOrder,
      int sourceX,
      int sourceY,
      int sourceWidth,
      int sourceHeight,
      int targetX,
      int targetY,
      int targetWidth,
      int targetHeight,
      int totalTargetWidth,
      int totalTargetHeight)
  {
    if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
    {
      return;
    }

    Rect sourceRect = sourceSprite.rect;
    Rect sliceRect = new Rect(
        sourceRect.x + sourceX,
        sourceRect.y + sourceY,
        sourceWidth,
        sourceHeight);
    Sprite sliceSprite = Sprite.Create(
        sourceSprite.texture,
        sliceRect,
        new Vector2(0.5f, 0.5f),
        UiPixelsPerUnit,
        0,
        SpriteMeshType.FullRect);
    sliceSprite.name = $"{sourceSprite.name}_{sourceX}_{sourceY}_SmartSplitterSlice";
    sliceSprite.texture.filterMode = FilterMode.Point;
    sliceSprite.texture.wrapMode = TextureWrapMode.Clamp;

    GameObject sliceObject = new GameObject($"{objectName}_Slice");
    SetNativeLayer(sliceObject);
    sliceObject.transform.SetParent(parent, false);
    sliceObject.transform.localRotation = Quaternion.identity;
    sliceObject.transform.localPosition = new Vector3(
        ((targetX + (targetWidth * 0.5f)) - (totalTargetWidth * 0.5f)) / UiPixelsPerUnit,
        ((targetY + (targetHeight * 0.5f)) - (totalTargetHeight * 0.5f)) / UiPixelsPerUnit,
        0.0f);
    sliceObject.transform.localScale = new Vector3(
        (float)targetWidth / sourceWidth,
        (float)targetHeight / sourceHeight,
        1.0f);

    SpriteRenderer renderer = sliceObject.AddComponent<SpriteRenderer>();
    renderer.sprite = sliceSprite;
    renderer.color = color;
    renderer.sortingLayerID = SortingLayerID.GUI;
    renderer.sortingOrder = sortingOrder;
  }

  private static bool ShouldRenderAtAuthoredPixelSize(
      string objectName,
      Sprite sprite,
      RectTransform sourceRect,
      Vector2 authoredSize)
  {
    if (sprite == null || sourceRect == null || string.IsNullOrEmpty(objectName))
    {
      return false;
    }

    if (!objectName.Contains("Dropdown"))
    {
      return false;
    }

    return Mathf.Approximately(SnapUiPixels(authoredSize.x), sprite.rect.width) &&
           Mathf.Approximately(SnapUiPixels(authoredSize.y), sprite.rect.height);
  }

  private Sprite GetUiPixelSprite(Sprite source)
  {
    if (source == null)
    {
      return null;
    }

    if (_uiPixelSprites.TryGetValue(source, out Sprite normalized) && normalized != null)
    {
      return normalized;
    }

    Rect rect = source.rect;
    Vector2 pivot = rect.width > 0.0f && rect.height > 0.0f
        ? new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height)
        : new Vector2(0.5f, 0.5f);
    normalized = Sprite.Create(
        source.texture,
        rect,
        pivot,
        UiPixelsPerUnit,
        0,
        SpriteMeshType.FullRect,
        source.border);
    normalized.name = source.name + "_SmartSplitterUi16";
    normalized.texture.filterMode = FilterMode.Point;
    normalized.texture.wrapMode = TextureWrapMode.Clamp;
    _uiPixelSprites[source] = normalized;
    return normalized;
  }

  private static float SnapUiUnits(float units)
  {
    return Mathf.Round(units * UiPixelsPerUnit) / UiPixelsPerUnit;
  }

  private static float ScaleLayoutUnits(float units)
  {
    return SnapUiUnits(units * PanelLayoutScale);
  }

  private static float SnapUiPixels(float value)
  {
    return Mathf.Round(value);
  }

  private static bool HasSpriteBorder(Sprite sprite)
  {
    if (sprite == null)
    {
      return false;
    }

    Vector4 border = sprite.border;
    return border.x > 0.0f ||
           border.y > 0.0f ||
           border.z > 0.0f ||
           border.w > 0.0f;
  }

  private void BindLane(LaneWidgets lane, string prefix)
  {
    lane.FilterButton = BindButton(prefix + "FilterButton", null);
    lane.PickButton = BindButton(prefix + "Slot0", () => BeginPickLane(lane.Lane));
    lane.ClearButton = BindButton(prefix + "Slot1", () => SetLaneToAny(lane.Lane));
    lane.DropdownButton = BindButton(prefix + "Dropdown", () => ToggleDropdown(lane.Lane));
    lane.DropdownArrowButton = BindButton(prefix + "DropdownArrow", () => ToggleDropdown(lane.Lane));
    lane.Silhouette = FindNativeObject(prefix + "FilterSilhouette");
    lane.Highlight = FindNativeObject(prefix + "FilterHighlight");
    lane.NoneIcon = FindNativeObject(prefix + "FilterNone");
    lane.DropdownPanel = FindNativeObject(prefix + "DropdownPanel");
    lane.DropdownArrow = FindNativeObject(prefix + "DropdownArrow");
    if (lane.FilterButton != null)
    {
      lane.FilterButton.HoverHighlight = lane.Highlight;
      lane.FilterButton.HoverTitleProvider = () => GetFilterSlotHoverTitle(lane);
      lane.FilterButton.HoverDescriptionProvider = () => GetFilterSlotHoverDescription(lane);
      lane.FilterButton.InitializeVisuals();
    }

    if (lane.PickButton != null)
    {
      lane.PickButton.HoverTitle = "Toggle filter picker";
      lane.PickButton.HoverDescription = "Select an item from your inventory to use as this lane's filter.";
    }

    if (lane.ClearButton != null)
    {
      lane.ClearButton.HoverTitle = "Clear filter";
      lane.ClearButton.HoverDescription = "Resets this lane to accept any item.";
    }

    if (lane.DropdownButton != null)
    {
      lane.DropdownButton.HoverTitle = "Filter mode";
      lane.DropdownButton.HoverDescription = "Choose Any, None, or a remembered item filter.";
      lane.DropdownHeaderIcon = CreateRuntimeIcon(lane.DropdownButton.gameObject, prefix + "DropdownHeaderIcon");
      if (lane.DropdownHeaderIcon != null)
      {
        lane.DropdownHeaderIcon.sortingOrder = DropdownRuntimeSortingOrder;
      }

      lane.DropdownHeaderText = CreateTinyPixelText(
          lane.DropdownButton.gameObject,
          prefix + "DropdownHeaderText",
          Vector3.zero,
          DropdownRuntimeSortingOrder + 1,
          TinyPixelText.Alignment.Center);
    }

    if (lane.DropdownPanel != null)
    {
      AddDropdownInputBlocker(lane.DropdownPanel);
      RaiseDropdownPanelRenderers(lane.DropdownPanel);
      lane.DropdownPanel.SetActive(false);
    }

    if (lane.DropdownArrow != null)
    {
      AlignDropdownArrow(lane.DropdownArrow);
      SetDropdownArrowSprite(lane, false);
    }

    if (lane.NoneIcon != null)
    {
      lane.NoneIcon.SetActive(false);
    }

    lane.RuntimeIcon = CreateRuntimeIcon(lane.FilterButton != null ? lane.FilterButton.gameObject : lane.Silhouette, prefix + "FilterIcon");
  }

  private void NormalizeStaticState()
  {
    SetActiveIfFound("ElectricityIconOn", true);
    SetActiveIfFound("ElectricityIconOff", false);
    ApplyVanillaUiSprites();
  }

  private void BindElectricityTooltip()
  {
    SmartSplitterNativeButton electricityButton = BindButton("ElectricityIconOn", null);
    if (electricityButton != null)
    {
      electricityButton.HoverDescription = "This building requires electricity.";
    }
  }

  private SmartSplitterNativeButton BindButton(string objectName, System.Action action)
  {
    GameObject nativeObject = FindNativeObject(objectName);
    if (nativeObject == null)
    {
      return null;
    }

    SmartSplitterNativeButton button = nativeObject.GetComponent<SmartSplitterNativeButton>();
    if (button == null)
    {
      button = nativeObject.AddComponent<SmartSplitterNativeButton>();
    }

    button.Clicked = action;
    button.HoverHighlight = FindDirectChild(nativeObject, "HoverHighlight");
    button.InitializeVisuals();

    BoxCollider collider = nativeObject.GetComponent<BoxCollider>();
    if (collider == null)
    {
      collider = nativeObject.AddComponent<BoxCollider>();
    }

    RectTransform sourceRect = FindGameObjectInSource(objectName)?.GetComponent<RectTransform>();
    Vector2 authoredSize = GetAuthoredSize(objectName, sourceRect);
    Vector2 size = sourceRect != null
        ? new Vector2(ToUiUnits(authoredSize.x), ToUiUnits(authoredSize.y))
        : Vector2.one;
    collider.size = new Vector3(size.x, size.y, 0.1f);
    collider.center = new Vector3(0.0f, 0.0f, NativeButtonInputZ);

    return button;
  }

  private void AddPanelInputBlocker()
  {
    if (_nativeRoot == null)
    {
      return;
    }

    RectTransform sourceRect = FindSourceRect("PanelRoot") ?? FindSourceRect("SmartSplitterPanel");
    Vector2 authoredSize = GetAuthoredSize(sourceRect != null ? sourceRect.name : "PanelRoot", sourceRect);
    Vector2 size = sourceRect != null
        ? new Vector2(ToUiUnits(authoredSize.x), ToUiUnits(authoredSize.y))
        : new Vector2(9.375f, 5.0f);

    GameObject blockerObject = FindDirectChild(_nativeRoot, "PanelInputBlocker");
    if (blockerObject == null)
    {
      blockerObject = new GameObject("PanelInputBlocker");
      SetNativeLayer(blockerObject);
      blockerObject.transform.SetParent(_nativeRoot.transform, false);
    }

    blockerObject.transform.localPosition = Vector3.zero;
    blockerObject.transform.localRotation = Quaternion.identity;
    blockerObject.transform.localScale = Vector3.one;

    BlockingUIElement blocker = blockerObject.GetComponent<BlockingUIElement>();
    if (blocker == null)
    {
      blocker = blockerObject.AddComponent<BlockingUIElement>();
    }

    InitializeUIElementLists(blocker);

    BoxCollider collider = blockerObject.GetComponent<BoxCollider>();
    if (collider == null)
    {
      collider = blockerObject.AddComponent<BoxCollider>();
    }

    collider.size = new Vector3(size.x, size.y, 0.1f);
    collider.center = new Vector3(0.0f, 0.0f, PanelBlockerInputZ);
  }

  private void ToggleDropdown(SmartSplitterLane lane)
  {
    if (_openDropdownLane.HasValue && _openDropdownLane.Value == lane)
    {
      CloseDropdowns();
      return;
    }

    OpenDropdown(lane);
  }

  private void OpenDropdown(SmartSplitterLane lane)
  {
    _openDropdownLane = lane;

    for (int i = 0; i < _lanes.Length; i++)
    {
      bool isOpen = _lanes[i].Lane == lane;
      SetDropdownOpenState(_lanes[i], isOpen);
      if (isOpen)
      {
        _lanes[i].DropdownScrollOffset = 0;
        RebuildDropdownOptions(_lanes[i]);
      }
    }
  }

  private void CloseDropdowns()
  {
    _openDropdownLane = null;
    for (int i = 0; i < _lanes.Length; i++)
    {
      SetDropdownOpenState(_lanes[i], false);
    }
  }

  private void SetDropdownOpenState(LaneWidgets lane, bool open)
  {
    if (lane.DropdownPanel != null)
    {
      lane.DropdownPanel.SetActive(open);
    }

    if (lane.DropdownArrow != null)
    {
      lane.DropdownArrow.transform.localRotation = Quaternion.identity;
      SetDropdownArrowSprite(lane, open);
    }

    if (!open)
    {
      ClearDropdownOptions(lane);
    }
  }

  private void ClearDropdownOptions(LaneWidgets lane)
  {
    for (int i = 0; i < lane.DropdownOptionObjects.Count; i++)
    {
      if (lane.DropdownOptionObjects[i] != null)
      {
        Destroy(lane.DropdownOptionObjects[i]);
      }
    }

    lane.DropdownOptionObjects.Clear();
  }

  private void RebuildDropdownOptions(LaneWidgets lane)
  {
    if (lane.DropdownPanel == null)
    {
      return;
    }

    ClearDropdownOptions(lane);

    List<DropdownOptionData> options = BuildDropdownHistoryOptions();
    int totalHistoryRows = Mathf.CeilToInt(options.Count / (float)DropdownHistoryColumns);
    int maxScrollOffset = Mathf.Max(0, totalHistoryRows - MaxVisibleDropdownHistoryRows);
    lane.DropdownScrollOffset = Mathf.Clamp(lane.DropdownScrollOffset, 0, maxScrollOffset);

    AddDropdownTextOption(lane, DropdownOptionKind.Any, ObjectID.None, 0, 1.0f);
    AddDropdownTextOption(lane, DropdownOptionKind.None, ObjectID.None, 0, 0.55f);

    int firstHistoryIndex = lane.DropdownScrollOffset * DropdownHistoryColumns;
    int visibleCount = Mathf.Min(
        MaxVisibleDropdownHistoryRows * DropdownHistoryColumns,
        options.Count - firstHistoryIndex);
    for (int i = 0; i < visibleCount; i++)
    {
      DropdownOptionData option = options[firstHistoryIndex + i];
      int column = i % DropdownHistoryColumns;
      int row = i / DropdownHistoryColumns;
      AddDropdownHistoryOption(
          lane,
          option.ObjectID,
          option.Variation,
          -0.5f + column * 0.5f,
          -0.03125f - row * 0.5f);
    }
  }

  private static List<DropdownOptionData> BuildDropdownHistoryOptions()
  {
    MergePersistedHistoryItems();

    List<DropdownOptionData> options = new();

    for (int i = 0; i < RememberedFilterItems.Count; i++)
    {
      options.Add(new DropdownOptionData(
          DropdownOptionKind.Item,
          RememberedFilterItems[i].ObjectID,
          RememberedFilterItems[i].Variation));
    }

    return options;
  }

  private void HandleDropdownScroll()
  {
    if (!_openDropdownLane.HasValue)
    {
      return;
    }

    float scroll = Input.mouseScrollDelta.y;
    if (Mathf.Approximately(scroll, 0.0f))
    {
      return;
    }

    LaneWidgets lane = GetLaneWidgets(_openDropdownLane.Value);
    if (lane == null)
    {
      return;
    }

    int optionCount = BuildDropdownHistoryOptions().Count;
    int totalHistoryRows = Mathf.CeilToInt(optionCount / (float)DropdownHistoryColumns);
    int maxScrollOffset = Mathf.Max(0, totalHistoryRows - MaxVisibleDropdownHistoryRows);
    int direction = scroll < 0.0f ? 1 : -1;
    int nextOffset = Mathf.Clamp(lane.DropdownScrollOffset + direction, 0, maxScrollOffset);
    if (nextOffset == lane.DropdownScrollOffset)
    {
      return;
    }

    lane.DropdownScrollOffset = nextOffset;
    RebuildDropdownOptions(lane);
  }

  private LaneWidgets GetLaneWidgets(SmartSplitterLane lane)
  {
    for (int i = 0; i < _lanes.Length; i++)
    {
      if (_lanes[i].Lane == lane)
      {
        return _lanes[i];
      }
    }

    return null;
  }

  private void AddDropdownTextOption(
      LaneWidgets lane,
      DropdownOptionKind kind,
      ObjectID objectID,
      int variation,
      float rowY)
  {
    GameObject row = new GameObject($"{lane.Lane}DropdownOption_{kind}");
    SetNativeLayer(row);
    row.transform.SetParent(lane.DropdownPanel.transform, false);
    row.transform.localPosition = new Vector3(0.0f, ScaleLayoutUnits(rowY), 0.0f);
    row.transform.localRotation = Quaternion.identity;
    row.transform.localScale = Vector3.one;

    SmartSplitterNativeButton button = row.AddComponent<SmartSplitterNativeButton>();
    button.Clicked = () => SelectDropdownOption(lane.Lane, kind, objectID, variation);
    button.HoverTitleProvider = () => GetDropdownOptionHoverTitle(kind, objectID, variation);
    button.HoverDescriptionProvider = () => GetDropdownOptionHoverDescription(kind);
    button.InitializeVisuals();

    BoxCollider collider = row.AddComponent<BoxCollider>();
    collider.size = new Vector3(ScaleLayoutUnits(1.55f), ScaleLayoutUnits(DropdownRowHeight), 0.1f);
    collider.center = new Vector3(0.0f, 0.0f, DropdownOptionInputZ);

    SpriteRenderer selectedBackground = CreateFlatSelectionBackground(
        row,
        new Vector2(
            ScaleLayoutUnits(1.45f),
            ScaleLayoutUnits(DropdownRowHeight - 0.0625f)));
    selectedBackground.gameObject.SetActive(IsDropdownOptionSelected(lane.Lane, kind, objectID));

    SpriteRenderer hoverBackground = CreateFlatSelectionBackground(
        row,
        new Vector2(
            ScaleLayoutUnits(1.45f),
            ScaleLayoutUnits(DropdownRowHeight - 0.0625f)));
    hoverBackground.color = new Color(0.75f, 0.86f, 1.0f, 0.22f);
    hoverBackground.gameObject.SetActive(false);
    button.HoverHighlight = hoverBackground.gameObject;

    TinyPixelText label = CreateTinyPixelText(
        row,
        "Text",
        new Vector3(ScaleLayoutUnits(-0.62f), 0.0f, 0.0f),
        DropdownRuntimeSortingOrder + 3,
        TinyPixelText.Alignment.Left);
    label.Render(FormatDropdownSingleLineLabel(
        GetDropdownOptionLabel(kind, objectID, variation),
        DropdownOptionLabelMaxCharacters));

    lane.DropdownOptionObjects.Add(row);
  }

  private void AddDropdownHistoryOption(
      LaneWidgets lane,
      ObjectID objectID,
      int variation,
      float x,
      float y)
  {
    GameObject option = new GameObject($"{lane.Lane}DropdownHistoryOption_{objectID}");
    SetNativeLayer(option);
    option.transform.SetParent(lane.DropdownPanel.transform, false);
    option.transform.localPosition = new Vector3(ScaleLayoutUnits(x), ScaleLayoutUnits(y), 0.0f);
    option.transform.localRotation = Quaternion.identity;
    option.transform.localScale = Vector3.one;

    SmartSplitterNativeButton button = option.AddComponent<SmartSplitterNativeButton>();
    button.Clicked = () => SelectDropdownOption(
        lane.Lane,
        DropdownOptionKind.Item,
        objectID,
        variation);
    button.HoverTitleProvider = () => new TextAndFormatFields
    {
      text = GetItemDisplayName(objectID, variation),
      dontLocalize = true
    };
    button.InitializeVisuals();

    BoxCollider collider = option.AddComponent<BoxCollider>();
    collider.size = new Vector3(ScaleLayoutUnits(0.5f), ScaleLayoutUnits(0.5f), 0.1f);
    collider.center = new Vector3(0.0f, 0.0f, DropdownOptionInputZ);

    SpriteRenderer selectedBackground = CreateFlatSelectionBackground(
        option,
        new Vector2(ScaleLayoutUnits(0.5f), ScaleLayoutUnits(0.5f)));
    selectedBackground.gameObject.SetActive(IsDropdownOptionSelected(
        lane.Lane,
        DropdownOptionKind.Item,
        objectID));
    button.HoverHighlight = selectedBackground.gameObject;

    SpriteRenderer icon = CreateRuntimeIcon(option, "Icon");
    if (icon != null)
    {
      icon.sortingOrder = DropdownRuntimeSortingOrder + 2;
      icon.transform.localPosition = Vector3.zero;
      ApplyItemIcon(icon, objectID, variation, ScaleLayoutUnits(DropdownOptionIconSize));
    }

    lane.DropdownOptionObjects.Add(option);
  }

  private void AddDropdownInputBlocker(GameObject dropdownPanel)
  {
    if (dropdownPanel == null)
    {
      return;
    }

    RectTransform sourceRect = FindGameObjectInSource(dropdownPanel.name)?.GetComponent<RectTransform>();
    Vector2 authoredSize = GetAuthoredSize(dropdownPanel.name, sourceRect);
    Vector2 blockerSize = authoredSize + new Vector2(2.0f, 2.0f);
    Vector2 size = sourceRect != null
        ? new Vector2(ToUiUnits(blockerSize.x), ToUiUnits(blockerSize.y))
        : new Vector2(2.6f, 4.0f);

    GameObject blockerObject = FindDirectChild(dropdownPanel, "DropdownInputBlocker");
    if (blockerObject == null)
    {
      blockerObject = new GameObject("DropdownInputBlocker");
      SetNativeLayer(blockerObject);
      blockerObject.transform.SetParent(dropdownPanel.transform, false);
    }

    blockerObject.transform.localPosition = Vector3.zero;
    blockerObject.transform.localRotation = Quaternion.identity;
    blockerObject.transform.localScale = Vector3.one;

    BlockingUIElement blocker = blockerObject.GetComponent<BlockingUIElement>();
    if (blocker == null)
    {
      blocker = blockerObject.AddComponent<BlockingUIElement>();
    }

    InitializeUIElementLists(blocker);

    BoxCollider collider = blockerObject.GetComponent<BoxCollider>();
    if (collider == null)
    {
      collider = blockerObject.AddComponent<BoxCollider>();
    }

    collider.size = new Vector3(size.x, size.y, 0.1f);
    collider.center = new Vector3(0.0f, 0.0f, DropdownBlockerInputZ);
  }

  private static void InitializeUIElementLists(UIelement element)
  {
    if (element == null)
    {
      return;
    }

    element.topUIElements ??= new List<UIelement>();
    element.bottomUIElements ??= new List<UIelement>();
    element.leftUIElements ??= new List<UIelement>();
    element.rightUIElements ??= new List<UIelement>();
    element.childElements ??= new List<UIelement>();
  }

  private void RaiseDropdownPanelRenderers(GameObject dropdownPanel)
  {
    if (dropdownPanel == null)
    {
      return;
    }

    SpriteRenderer[] renderers = dropdownPanel.GetComponentsInChildren<SpriteRenderer>(true);
    for (int i = 0; i < renderers.Length; i++)
    {
      if (renderers[i] == null)
      {
        continue;
      }

      renderers[i].sortingLayerID = SortingLayerID.GUI;
      renderers[i].sortingOrder = DropdownRuntimeSortingOrder - 4;
    }
  }

  private void RaiseDropdownArrowRenderer(GameObject dropdownArrow)
  {
    if (dropdownArrow == null)
    {
      return;
    }

    SpriteRenderer renderer = dropdownArrow.GetComponentInChildren<SpriteRenderer>(true);
    if (renderer == null)
    {
      return;
    }

    renderer.sortingLayerID = SortingLayerID.GUI;
    renderer.sortingOrder = DropdownRuntimeSortingOrder + 4;
  }

  private void SetDropdownArrowSprite(LaneWidgets lane, bool open)
  {
    if (lane == null || lane.DropdownArrow == null)
    {
      return;
    }

    SpriteRenderer[] renderers = lane.DropdownArrow.GetComponentsInChildren<SpriteRenderer>(true);
    for (int i = 0; i < renderers.Length; i++)
    {
      if (renderers[i] != null)
      {
        renderers[i].enabled = false;
      }
    }

    GameObject existingPixels = FindDirectChild(lane.DropdownArrow, "RuntimeDropdownArrowPixels");
    if (existingPixels != null)
    {
      Destroy(existingPixels);
    }

    CreateDropdownArrowPixels(lane.DropdownArrow.transform, open);
    RaiseDropdownArrowRenderer(lane.DropdownArrow);
  }

  private static void CreateDropdownArrowPixels(Transform parent, bool up)
  {
    if (parent == null)
    {
      return;
    }

    GameObject root = new GameObject("RuntimeDropdownArrowPixels");
    SetNativeLayer(root);
    root.transform.SetParent(parent, false);
    root.transform.localPosition = Vector3.zero;
    root.transform.localRotation = Quaternion.identity;
    root.transform.localScale = Vector3.one * 0.5f;

    Vector2Int[] pixels = up
        ? new[]
        {
          new Vector2Int(-1, -1),
          new Vector2Int(0, -1),
          new Vector2Int(1, -1),
          new Vector2Int(0, 0)
        }
        : new[]
        {
          new Vector2Int(-1, 0),
          new Vector2Int(0, 0),
          new Vector2Int(1, 0),
          new Vector2Int(0, -1)
        };

    Color arrow = new Color(0.95f, 0.78f, 0.72f, 1.0f);
    for (int i = 0; i < pixels.Length; i++)
    {
      GameObject pixel = new GameObject("Pixel");
      SetNativeLayer(pixel);
      pixel.transform.SetParent(root.transform, false);
      pixel.transform.localPosition = new Vector3(
          pixels[i].x / UiPixelsPerUnit,
          pixels[i].y / UiPixelsPerUnit,
          0.0f);
      pixel.transform.localRotation = Quaternion.identity;
      pixel.transform.localScale = Vector3.one;

      SpriteRenderer renderer = pixel.AddComponent<SpriteRenderer>();
      renderer.sprite = GetDropdownPixelSprite();
      renderer.color = arrow;
      renderer.sortingLayerID = SortingLayerID.GUI;
      renderer.sortingOrder = DropdownRuntimeSortingOrder + 4;
    }
  }

  private static Sprite GetDropdownPixelSprite()
  {
    if (_dropdownPixelSprite == null)
    {
      _dropdownPixelSprite = Sprite.Create(
          Texture2D.whiteTexture,
          new Rect(0, 0, 1, 1),
          new Vector2(0.5f, 0.5f),
          UiPixelsPerUnit,
          0,
          SpriteMeshType.FullRect);
    }

    return _dropdownPixelSprite;
  }

  private static Sprite GetDropdownArrowDownSprite()
  {
    if (_dropdownArrowDownSprite == null)
    {
      _dropdownArrowDownSprite = CreateDropdownArrowSprite(false);
    }

    return _dropdownArrowDownSprite;
  }

  private static Sprite GetDropdownArrowUpSprite()
  {
    if (_dropdownArrowUpSprite == null)
    {
      _dropdownArrowUpSprite = CreateDropdownArrowSprite(true);
    }

    return _dropdownArrowUpSprite;
  }

  private static Sprite CreateDropdownArrowSprite(bool up)
  {
    Texture2D texture = new Texture2D(3, 2, TextureFormat.RGBA32, false)
    {
      filterMode = FilterMode.Point,
      wrapMode = TextureWrapMode.Clamp
    };
    Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);
    Color arrow = new Color(0.95f, 0.78f, 0.72f, 1.0f);
    texture.SetPixels(up
        ? new[] { arrow, arrow, arrow, clear, arrow, clear }
        : new[] { clear, arrow, clear, arrow, arrow, arrow });
    texture.Apply(false, true);

    return Sprite.Create(
        texture,
        new Rect(0, 0, 3, 2),
        new Vector2(0.5f, 0.5f),
        UiPixelsPerUnit,
        0,
        SpriteMeshType.FullRect);
  }

  private void AlignDropdownArrow(GameObject dropdownArrow)
  {
    if (dropdownArrow == null)
    {
      return;
    }

    RectTransform sourceRect = FindSourceRect(dropdownArrow.name);
    if (sourceRect != null)
    {
      Vector2 authoredPosition = GetAuthoredAnchoredPosition(dropdownArrow.name, sourceRect);
      dropdownArrow.transform.localPosition = new Vector3(
          PixelsToUiUnits(authoredPosition.x),
          PixelsToUiUnits(authoredPosition.y) + DropdownArrowVerticalNudge,
          dropdownArrow.transform.localPosition.z);
    }

    RaiseDropdownArrowRenderer(dropdownArrow);
  }

  private SpriteRenderer CreateFlatSelectionBackground(GameObject parent, Vector2 size)
  {
    GameObject selectedObject = new GameObject("Selected");
    SetNativeLayer(selectedObject);
    selectedObject.transform.SetParent(parent.transform, false);
    selectedObject.transform.localPosition = Vector3.zero;
    selectedObject.transform.localRotation = Quaternion.identity;
    selectedObject.transform.localScale = Vector3.one;

    SpriteRenderer renderer = selectedObject.AddComponent<SpriteRenderer>();
    renderer.sortingLayerID = SortingLayerID.GUI;
    renderer.sortingOrder = DropdownRuntimeSortingOrder - 1;
    renderer.sprite = Texture2D.whiteTexture != null
        ? Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), UiPixelsPerUnit)
        : null;
    renderer.color = new Color(0.75f, 0.86f, 1.0f, 0.35f);
    renderer.drawMode = SpriteDrawMode.Sliced;
    renderer.size = size;
    return renderer;
  }

  private void SelectDropdownOption(
      SmartSplitterLane lane,
      DropdownOptionKind kind,
      ObjectID objectID,
      int variation)
  {
    switch (kind)
    {
      case DropdownOptionKind.Any:
        SetLaneToAny(lane);
        break;
      case DropdownOptionKind.None:
        SetLaneToNone(lane);
        break;
      case DropdownOptionKind.Item:
        SetLaneToItem(lane, objectID, variation);
        break;
    }

    CloseDropdowns();
  }

  private bool IsDropdownOptionSelected(SmartSplitterLane lane, DropdownOptionKind kind, ObjectID objectID)
  {
    SmartSplitterLaneFilter filter = GetCurrentLaneFilter(lane);
    return kind switch
    {
      DropdownOptionKind.Any => filter.Mode == SmartSplitterLaneFilterMode.Any,
      DropdownOptionKind.None => filter.Mode == SmartSplitterLaneFilterMode.None,
      DropdownOptionKind.Item => filter.Mode == SmartSplitterLaneFilterMode.Item &&
                                 filter.FilterObject == objectID,
      _ => false
    };
  }

  private string GetDropdownOptionLabel(DropdownOptionKind kind, ObjectID objectID, int variation)
  {
    return kind switch
    {
      DropdownOptionKind.Any => "Any",
      DropdownOptionKind.None => "None",
      DropdownOptionKind.Item => GetItemDisplayName(objectID, variation),
      _ => string.Empty
    };
  }

  private TextAndFormatFields GetDropdownOptionHoverTitle(
      DropdownOptionKind kind,
      ObjectID objectID,
      int variation)
  {
    return new TextAndFormatFields
    {
      text = kind switch
      {
        DropdownOptionKind.Any => "Any item",
        DropdownOptionKind.None => "Blocked lane",
        DropdownOptionKind.Item => GetItemDisplayName(objectID, variation),
        _ => string.Empty
      },
      dontLocalize = true
    };
  }

  private List<TextAndFormatFields> GetDropdownOptionHoverDescription(DropdownOptionKind kind)
  {
    string description = kind switch
    {
      DropdownOptionKind.Any => "Allows this lane to pass any item.",
      DropdownOptionKind.None => "Prevents items from using this lane.",
      _ => string.Empty
    };

    if (string.IsNullOrEmpty(description))
    {
      return null;
    }

    return new List<TextAndFormatFields>
    {
      new TextAndFormatFields
      {
        text = description,
        color = Color.white * 0.99f,
        dontLocalize = true
      }
    };
  }

  private void CycleLane(SmartSplitterLane lane)
  {
    SmartSplitterLaneFiltersCD filters = TryGetCurrentFilters(out SmartSplitterLaneFiltersCD currentFilters)
        ? currentFilters
        : SmartSplitterLaneFilterUtility.CreateDefaultLaneFilters();
    SmartSplitterLaneFilter current = SmartSplitterLaneFilterUtility.GetLaneFilter(filters, lane);
    SmartSplitterLaneFilter next = SmartSplitterLaneFilterUtility.GetNextProofFilter(current);
    ApplyLaneFilter(lane, next);
    Refresh();
  }

  private void BeginPickLane(SmartSplitterLane lane)
  {
    if (!HasValidTarget())
    {
      return;
    }

    if (_pendingPickLane == lane)
    {
      EndPendingPick();
      return;
    }

    _pendingPickLane = lane;
    BeginFilterPickCursorMode();
    AudioManager.SfxUI(SfxID.FIXME_menu_select, 1.0f, true, 0.5f, 0.15f, false, true, 0.0f);
  }

  private void UpdatePendingPick()
  {
    if (_endPendingPickAfterClick)
    {
      EndPendingPick();
      return;
    }

    if (!_pendingPickLane.HasValue ||
        Manager.input == null ||
        Manager.input.singleplayerInputModule == null)
    {
      return;
    }

    PlayerInput input = Manager.input.singleplayerInputModule;
    if (input.WasButtonPressedDownThisFrame(PlayerInput.InputType.CANCEL, false) ||
        input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_SECOND_INTERACT, false))
    {
      EndPendingPick();
      return;
    }

    if (!input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_INTERACT, false))
    {
      return;
    }

    TryApplyHoveredInventoryItemToPendingLane(deferPickerEnd: false);
  }

  private bool TryApplyHoveredInventoryItemToPendingLane(bool deferPickerEnd)
  {
    if (!_pendingPickLane.HasValue ||
        Manager.input == null ||
        Manager.input.singleplayerInputModule == null ||
        !Manager.input.singleplayerInputModule.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_INTERACT, false))
    {
      return false;
    }

    InventorySlotUI slot = Manager.ui != null
        ? Manager.ui.currentSelectedUIElement as InventorySlotUI
        : null;
    if (slot == null)
    {
      return false;
    }

    ObjectDataCD objectData = slot.GetObjectData();
    if (objectData.objectID == ObjectID.None)
    {
      return false;
    }

    SetLaneToItem(_pendingPickLane.Value, objectData.objectID, objectData.variation);
    if (deferPickerEnd)
    {
      _endPendingPickAfterClick = true;
    }
    else
    {
      EndPendingPick();
    }

    AudioManager.SfxUI(SfxID.uiPickup, 1.1f, true, 1.0f, 0.2f, false, true, 0.0f);
    return true;
  }

  private void MaintainFilterPickMouseMode()
  {
    if (!_pendingPickLane.HasValue ||
        Manager.ui == null ||
        Manager.ui.mouse == null)
    {
      return;
    }

    if (!_vanillaPickerSessionActive)
    {
      BeginFilterPickCursorMode();
    }

    KeepVanillaFilteringUiAliveOffscreen();

    if (Manager.ui.mouse.mouseMode != UIMouse.MouseMode.FilterPicking)
    {
      Manager.ui.mouse.SetMouseMode(UIMouse.MouseMode.FilterPicking, -1);
    }
  }

  private void BeginFilterPickCursorMode()
  {
    if (!_pendingPickLane.HasValue ||
        Manager.ui == null ||
        Manager.ui.mouse == null)
    {
      return;
    }

    KeepVanillaFilteringUiAliveOffscreen();

    if (Manager.ui.mouse.mouseMode != UIMouse.MouseMode.FilterPicking)
    {
      Manager.ui.mouse.SetMouseMode(UIMouse.MouseMode.FilterPicking, -1);
    }

    _vanillaPickerSessionActive = true;
  }

  private void EndPendingPick()
  {
    if (_pendingPickLane.HasValue &&
        Manager.ui != null &&
        Manager.ui.mouse != null &&
        Manager.ui.mouse.mouseMode == UIMouse.MouseMode.FilterPicking)
    {
      Manager.ui.mouse.SetMouseMode(UIMouse.MouseMode.Normal, -1);
    }

    HideOwnedVanillaFilteringUi();
    _vanillaPickerSessionActive = false;
    _endPendingPickAfterClick = false;
    _pendingPickLane = null;
  }

  private void SetLaneToItem(SmartSplitterLane lane, ObjectID objectID, int variation)
  {
    if (!HasValidTarget())
    {
      return;
    }

    RememberFilterItem(objectID, variation);
    ApplyLaneFilter(lane, SmartSplitterLaneFilterUtility.CreateItemFilter(objectID, variation));
    Refresh();
  }

  private void SetLaneToAny(SmartSplitterLane lane)
  {
    if (!HasValidTarget())
    {
      return;
    }

    ApplyLaneFilter(lane, SmartSplitterLaneFilterUtility.CreateAnyFilter());
    if (_pendingPickLane == lane)
    {
      EndPendingPick();
    }

    Refresh();
  }

  private void SetLaneToNone(SmartSplitterLane lane)
  {
    if (!HasValidTarget())
    {
      return;
    }

    ApplyLaneFilter(lane, SmartSplitterLaneFilterUtility.CreateNoneFilter());
    if (_pendingPickLane == lane)
    {
      EndPendingPick();
    }

    Refresh();
  }

  private bool TryGetTargetEntityManager(out EntityManager entityManager)
  {
    entityManager = default;

    if (_world == null || !_world.IsCreated || _splitter == Entity.Null)
    {
      return false;
    }

    entityManager = _world.EntityManager;
    return entityManager.Exists(_splitter) &&
           entityManager.HasComponent<SmartSplitterLaneFiltersCD>(_splitter);
  }

  private bool HasValidTarget()
  {
    return TryGetTargetEntityManager(out _) ||
           (_hasTargetCenter && _targetCenter.x != int.MinValue);
  }

  private bool TryGetCurrentFilters(out SmartSplitterLaneFiltersCD filters)
  {
    if (TryGetTargetEntityManager(out EntityManager entityManager))
    {
      filters = entityManager.GetComponentData<SmartSplitterLaneFiltersCD>(_splitter);
      if (_hasTargetCenter)
      {
        SmartSplitterNetworkState.RememberFilters(_targetCenter, filters);
      }

      return true;
    }

    if (_hasTargetCenter && SmartSplitterNetworkState.TryGetFilters(_targetCenter, out filters))
    {
      return true;
    }

    filters = default;
    if (_hasTargetCenter)
    {
      SmartSplitterNetworkState.RequestFilters(_targetCenter);
    }

    return false;
  }

  private void ApplyLaneFilter(SmartSplitterLane lane, SmartSplitterLaneFilter filter)
  {
    if (TryGetTargetEntityManager(out EntityManager entityManager))
    {
      SmartSplitterLaneFilterUtility.SetLaneFilter(entityManager, _splitter, lane, filter);

      if (_hasTargetCenter &&
          SmartSplitterNetworkState.TryGetFilters(_targetCenter, out SmartSplitterLaneFiltersCD cachedFilters))
      {
        SmartSplitterLaneFilterUtility.SetLaneFilterValue(ref cachedFilters, lane, filter);
        SmartSplitterNetworkState.RememberFilters(_targetCenter, cachedFilters);
      }

      return;
    }

    if (_hasTargetCenter)
    {
      SmartSplitterNetworkState.SendLaneFilter(_targetCenter, lane, filter);
    }
  }

  private void RefreshLane(LaneWidgets lane, SmartSplitterLaneFilter filter)
  {
    bool hasItem = filter.Mode == SmartSplitterLaneFilterMode.Item &&
                   filter.FilterObject != ObjectID.None;
    bool isNone = filter.Mode == SmartSplitterLaneFilterMode.None;

    if (lane.Silhouette != null)
    {
      lane.Silhouette.SetActive(!hasItem && !isNone);
    }

    if (lane.NoneIcon != null)
    {
      lane.NoneIcon.SetActive(isNone);
    }

    if (lane.Highlight != null)
    {
      lane.Highlight.SetActive(lane.FilterButton != null && lane.FilterButton.IsHovered);
    }

    RefreshDropdownHeader(lane, filter);

    if (lane.RuntimeIcon == null)
    {
      return;
    }

    if (!hasItem ||
        !PugDatabase.TryGetObjectInfo(filter.FilterObject, out ObjectInfo objectInfo, filter.FilterVariation) ||
        objectInfo.icon == null)
    {
      lane.RuntimeIcon.sprite = null;
      lane.RuntimeIcon.gameObject.SetActive(false);
      return;
    }

    ContainedObjectsBuffer containedObject = new ContainedObjectsBuffer
    {
      objectData = new ObjectDataCD
      {
        objectID = filter.FilterObject,
        variation = filter.FilterVariation
      }
    };
    Sprite iconOverride = Manager.ui != null
        ? Manager.ui.itemOverridesTable.GetIconOverride(containedObject.objectData, false)
        : null;
    lane.RuntimeIcon.sprite = iconOverride != null ? iconOverride : objectInfo.icon;
    lane.RuntimeIcon.sprite.texture.filterMode = FilterMode.Point;
    lane.RuntimeIcon.sprite.texture.wrapMode = TextureWrapMode.Clamp;
    if (Manager.ui != null)
    {
      Manager.ui.ApplyAnyIconGradientMap(containedObject, lane.RuntimeIcon);
    }

    lane.RuntimeIcon.transform.localPosition = GetFilterIconLocalPosition(lane, objectInfo.iconOffset);
    FitSpriteRenderer(lane.RuntimeIcon, Vector2.one * FilterIconSize);
    lane.RuntimeIcon.gameObject.SetActive(true);
    RememberFilterItem(filter.FilterObject, filter.FilterVariation);
  }

  private static Vector3 GetFilterIconLocalPosition(LaneWidgets lane, Vector3 iconOffset)
  {
    if (lane == null ||
        lane.RuntimeIcon == null ||
        lane.RuntimeIcon.transform.parent == null ||
        lane.Silhouette == null)
    {
      return iconOffset * FilterIconSize;
    }

    Vector3 slotCenter = lane.RuntimeIcon.transform.parent.InverseTransformPoint(lane.Silhouette.transform.position);
    return slotCenter + (iconOffset * FilterIconSize);
  }

  private void RefreshDropdownHeader(LaneWidgets lane, SmartSplitterLaneFilter filter)
  {
    if (lane.DropdownHeaderIcon != null)
    {
      lane.DropdownHeaderIcon.sprite = null;
      lane.DropdownHeaderIcon.gameObject.SetActive(false);
    }

    if (lane.DropdownHeaderText != null)
    {
      lane.DropdownHeaderText.Root.localPosition = new Vector3(0.0f, 0.5f / UiPixelsPerUnit, 0.0f);
      lane.DropdownHeaderText.Render(FormatDropdownSingleLineLabel("History", DropdownHeaderLabelMaxCharacters));
    }
  }

  private static string FormatDropdownSingleLineLabel(string value, int maxCharacters)
  {
    if (string.IsNullOrEmpty(value) || maxCharacters <= 0)
    {
      return string.Empty;
    }

    string compact = CollapseWhitespace(value);
    if (compact.Length <= maxCharacters)
    {
      return compact;
    }

    if (maxCharacters <= 3)
    {
      return compact.Substring(0, maxCharacters);
    }

    return compact.Substring(0, maxCharacters - 3) + "...";
  }

  private static string CollapseWhitespace(string value)
  {
    bool previousWasSpace = false;
    System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
      char character = value[i];
      if (char.IsWhiteSpace(character))
      {
        if (builder.Length > 0 && !previousWasSpace)
        {
          builder.Append(' ');
          previousWasSpace = true;
        }

        continue;
      }

      builder.Append(character);
      previousWasSpace = false;
    }

    if (builder.Length > 0 && builder[builder.Length - 1] == ' ')
    {
      builder.Length--;
    }

    return builder.ToString();
  }

  private static void RememberFilterItem(ObjectID objectID, int variation)
  {
    if (objectID == ObjectID.None)
    {
      return;
    }

    AddRememberedFilterItem(objectID, variation);
    SmartSplitterPersistence.RememberHistoryItem(objectID, variation);
  }

  private static void MergePersistedHistoryItems()
  {
    List<SmartSplitterPersistence.HistoryItem> persistedItems =
        SmartSplitterPersistence.GetHistoryItems();

    for (int i = 0; i < persistedItems.Count; i++)
    {
      AddRememberedFilterItem(persistedItems[i].ObjectID, persistedItems[i].Variation);
    }
  }

  private static bool AddRememberedFilterItem(ObjectID objectID, int variation)
  {
    if (objectID == ObjectID.None)
    {
      return false;
    }

    for (int i = 0; i < RememberedFilterItems.Count; i++)
    {
      if (RememberedFilterItems[i].ObjectID == objectID &&
          RememberedFilterItems[i].Variation == variation)
      {
        return false;
      }
    }

    RememberedFilterItems.Add(new RememberedFilterItem
    {
      ObjectID = objectID,
      Variation = variation
    });

    return true;
  }

  private void ApplyDropdownOptionIcon(
      SpriteRenderer renderer,
      DropdownOptionKind kind,
      ObjectID objectID,
      int variation,
      float targetSize)
  {
    switch (kind)
    {
      case DropdownOptionKind.Any:
        renderer.sprite = null;
        renderer.gameObject.SetActive(false);
        break;
      case DropdownOptionKind.None:
        ApplyNoneIcon(renderer, targetSize);
        break;
      case DropdownOptionKind.Item:
        ApplyItemIcon(renderer, objectID, variation, targetSize);
        break;
    }
  }

  private void ApplyNoneIcon(SpriteRenderer renderer, float targetSize)
  {
    if (renderer == null)
    {
      return;
    }

    Sprite noneSprite = FindFirstIconSprite(FindNativeObject("LeftFilterNone")?.transform);
    if (noneSprite == null)
    {
      renderer.sprite = null;
      renderer.gameObject.SetActive(false);
      return;
    }

    renderer.sprite = noneSprite;
    renderer.color = Color.white;
    renderer.sprite.texture.filterMode = FilterMode.Point;
    renderer.sprite.texture.wrapMode = TextureWrapMode.Clamp;
    FitSpriteRenderer(renderer, Vector2.one * targetSize);
    renderer.gameObject.SetActive(true);
  }

  private void ApplyItemIcon(SpriteRenderer renderer, ObjectID objectID, int variation, float targetSize)
  {
    if (renderer == null ||
        !PugDatabase.TryGetObjectInfo(objectID, out ObjectInfo objectInfo, variation) ||
        objectInfo.icon == null)
    {
      if (renderer != null)
      {
        renderer.sprite = null;
        renderer.gameObject.SetActive(false);
      }

      return;
    }

    ContainedObjectsBuffer containedObject = new ContainedObjectsBuffer
    {
      objectData = new ObjectDataCD
      {
        objectID = objectID,
        variation = variation
      }
    };
    Sprite iconOverride = Manager.ui != null
        ? Manager.ui.itemOverridesTable.GetIconOverride(containedObject.objectData, false)
        : null;
    renderer.sprite = iconOverride != null ? iconOverride : objectInfo.icon;
    renderer.color = Color.white;
    renderer.sprite.texture.filterMode = FilterMode.Point;
    renderer.sprite.texture.wrapMode = TextureWrapMode.Clamp;
    if (Manager.ui != null)
    {
      Manager.ui.ApplyAnyIconGradientMap(containedObject, renderer);
    }

    FitSpriteRenderer(renderer, Vector2.one * targetSize);
    renderer.gameObject.SetActive(true);
  }

  private static string GetItemDisplayName(ObjectID objectID, int variation)
  {
    ObjectID displayObjectID = PlayerController.GetAnyObjectIDReplaceForNameAndDesc(objectID);
    if (API.Authoring != null &&
        API.Authoring.ObjectProperties.TryGetPropertyString(displayObjectID, "name", out string propertyName) &&
        !string.IsNullOrEmpty(propertyName))
    {
      string term = "Items/" + propertyName;
      string translated = LocalizationManager.GetTranslation(
          term,
          true,
          0,
          true,
          false,
          null,
          null,
          true);
      if (!string.IsNullOrEmpty(translated) && translated != term)
      {
        return translated;
      }

      return propertyName;
    }

    string objectIDName = displayObjectID.ToString();
    return string.IsNullOrEmpty(objectIDName) ? "Item" : objectIDName;
  }

  private TextAndFormatFields GetFilterSlotHoverTitle(LaneWidgets lane)
  {
    SmartSplitterLaneFilter filter = GetCurrentLaneFilter(lane.Lane);
    bool hasItem = filter.Mode == SmartSplitterLaneFilterMode.Item &&
                   filter.FilterObject != ObjectID.None;

    return new TextAndFormatFields
    {
      text = filter.Mode == SmartSplitterLaneFilterMode.None
          ? "Blocked lane"
          : hasItem
              ? "Filtering enabled"
              : "No item filter",
      dontLocalize = true
    };
  }

  private List<TextAndFormatFields> GetFilterSlotHoverDescription(LaneWidgets lane)
  {
    SmartSplitterLaneFilter filter = GetCurrentLaneFilter(lane.Lane);
    bool hasItem = filter.Mode == SmartSplitterLaneFilterMode.Item &&
                   filter.FilterObject != ObjectID.None;

    if (filter.Mode == SmartSplitterLaneFilterMode.None)
    {
      return new List<TextAndFormatFields>
      {
        new TextAndFormatFields
        {
          text = "This lane is blocked and will not let items through.",
          color = Color.white * 0.99f,
          dontLocalize = true
        }
      };
    }

    if (!hasItem)
    {
      return new List<TextAndFormatFields>
      {
        new TextAndFormatFields
        {
          text = "This lane is not restricted to a specific item.",
          color = Color.white * 0.99f,
          dontLocalize = true
        }
      };
    }

    ContainedObjectsBuffer containedObject = new ContainedObjectsBuffer
    {
      objectData = new ObjectDataCD
      {
        objectID = filter.FilterObject,
        variation = filter.FilterVariation
      }
    };
    TextAndFormatFields objectName = PlayerController.GetObjectName(containedObject, false);

    return new List<TextAndFormatFields>
    {
      new TextAndFormatFields
      {
        text = "Only handles items matching the filter.",
        color = Color.white * 0.99f,
        dontLocalize = true,
        paddingBeneath = 0.125f
      },
      new TextAndFormatFields
      {
        text = "Filtering:",
        color = Color.white * 0.99f,
        dontLocalize = true
      },
      objectName
    };
  }

  private SmartSplitterLaneFilter GetCurrentLaneFilter(SmartSplitterLane lane)
  {
    if (!TryGetCurrentFilters(out SmartSplitterLaneFiltersCD filters))
    {
      return SmartSplitterLaneFilterUtility.CreateAnyFilter();
    }

    return SmartSplitterLaneFilterUtility.GetLaneFilter(filters, lane);
  }

  private SpriteRenderer CreateRuntimeIcon(GameObject parent, string name)
  {
    if (parent == null)
    {
      return null;
    }

    GameObject iconObject = new GameObject(name);
    SetNativeLayer(iconObject);
    iconObject.transform.SetParent(parent.transform, false);
    iconObject.transform.localPosition = Vector3.zero;
    iconObject.transform.localRotation = Quaternion.identity;

    SpriteRenderer spriteRenderer = iconObject.AddComponent<SpriteRenderer>();
    spriteRenderer.sortingLayerID = SortingLayerID.GUI;
    spriteRenderer.sortingOrder = RuntimeIconSortingOrder;
    iconObject.SetActive(false);

    return spriteRenderer;
  }

  private TinyPixelText CreateTinyPixelText(
      GameObject parent,
      string name,
      Vector3 localPosition,
      int sortingOrder,
      TinyPixelText.Alignment alignment)
  {
    if (parent == null)
    {
      return null;
    }

    GameObject textObject = new GameObject(name);
    SetNativeLayer(textObject);
    textObject.transform.SetParent(parent.transform, false);
    textObject.transform.localPosition = localPosition;
    textObject.transform.localRotation = Quaternion.identity;
    textObject.transform.localScale = Vector3.one * DropdownTextScale;

    return new TinyPixelText(
        textObject.transform,
        sortingOrder,
        Color.white,
        alignment);
  }

  private void ApplyVanillaUiSprites()
  {
    if (Manager.ui == null || Manager.ui.filteringUI == null)
    {
      return;
    }

    Sprite electricitySprite = Manager.ui.filteringUI.electricityIcon != null
        ? Manager.ui.filteringUI.electricityIcon.sprite
        : null;
    if (electricitySprite != null)
    {
      Color electricityColor = Manager.ui.filteringUI.electricityColorOn;
      SetNativeSprite("ElectricityIconOn", electricitySprite, electricityColor);
      SetNativeSprite("ElectricityIconOff", electricitySprite, electricityColor);
    }
  }

  private static Sprite FindFirstIconSprite(Component root)
  {
    if (root == null)
    {
      return null;
    }

    SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
    for (int i = 0; i < renderers.Length; i++)
    {
      if (renderers[i] != null &&
          renderers[i].sprite != null &&
          renderers[i].name == "Icon")
      {
        return renderers[i].sprite;
      }
    }

    for (int i = 0; i < renderers.Length; i++)
    {
      if (renderers[i] != null && renderers[i].sprite != null)
      {
        return renderers[i].sprite;
      }
    }

    return null;
  }

  private void SetNativeSprite(string objectName, Sprite sprite, Color color)
  {
    GameObject nativeObject = FindNativeObject(objectName);
    if (nativeObject == null || sprite == null)
    {
      return;
    }

    SpriteRenderer renderer = nativeObject.GetComponentInChildren<SpriteRenderer>(true);
    if (renderer == null)
    {
      return;
    }

    renderer.sprite = sprite;
    renderer.color = color;
    renderer.sprite.texture.filterMode = FilterMode.Point;
    renderer.sprite.texture.wrapMode = TextureWrapMode.Clamp;
    FitSpriteRendererToSourceRect(renderer, FindSourceRect(objectName), true);

    SmartSplitterNativeButton button = nativeObject.GetComponent<SmartSplitterNativeButton>();
    if (button != null)
    {
      button.InitializeVisuals();
    }
  }

  private void FitSpriteRendererToSourceRect(SpriteRenderer spriteRenderer, RectTransform sourceRect, bool preserveAspect)
  {
    if (spriteRenderer == null ||
        spriteRenderer.sprite == null ||
        sourceRect == null)
    {
      return;
    }

    spriteRenderer.drawMode = SpriteDrawMode.Simple;
    spriteRenderer.transform.localScale = Vector3.one;

    Vector2 authoredSize = GetAuthoredSize(sourceRect.name, sourceRect);
    Vector2 targetSize = new Vector2(
        ToUiUnits(authoredSize.x),
        ToUiUnits(authoredSize.y));
    Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
    if (targetSize.x <= 0.0f ||
        targetSize.y <= 0.0f ||
        spriteSize.x <= 0.0f ||
        spriteSize.y <= 0.0f)
    {
      return;
    }

    float scaleX = targetSize.x / spriteSize.x;
    float scaleY = targetSize.y / spriteSize.y;
    if (preserveAspect)
    {
      float scale = Mathf.Min(scaleX, scaleY);
      scaleX = scale;
      scaleY = scale;
    }

    spriteRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1.0f);
  }

  private void KeepVanillaFilteringUiAliveOffscreen()
  {
    if (Manager.ui == null ||
        Manager.ui.filteringUI == null ||
        Manager.ui.filteringUI.root == null)
    {
      return;
    }

    GameObject root = Manager.ui.filteringUI.root;
    Transform rootTransform = root.transform;
    if (!_ownsHiddenVanillaFilterUi || _vanillaFilteringUiRootTransform != rootTransform)
    {
      _vanillaFilteringUiRootTransform = rootTransform;
      _vanillaFilteringUiOriginalPosition = rootTransform.localPosition;
      _hasVanillaFilteringUiOriginalPosition = true;
    }

    root.SetActive(true);
    rootTransform.localPosition = new Vector3(10000.0f, 10000.0f, 0.0f);
    _ownsHiddenVanillaFilterUi = true;
  }

  private void HideOwnedVanillaFilteringUi()
  {
    if (!_ownsHiddenVanillaFilterUi ||
        Manager.ui == null ||
        Manager.ui.filteringUI == null ||
        Manager.ui.filteringUI.root == null)
    {
      RestoreOwnedVanillaFilteringUiPosition();
      _ownsHiddenVanillaFilterUi = false;
      return;
    }

    RestoreOwnedVanillaFilteringUiPosition();

    if (Manager.main == null ||
        Manager.main.player == null ||
        Manager.main.player.GetActiveFilteringBuilding() == null)
    {
      Manager.ui.filteringUI.HideUI();
    }

    _ownsHiddenVanillaFilterUi = false;
  }

  private void RestoreOwnedVanillaFilteringUiPosition()
  {
    if (!_ownsHiddenVanillaFilterUi)
    {
      return;
    }

    if (_hasVanillaFilteringUiOriginalPosition && _vanillaFilteringUiRootTransform != null)
    {
      _vanillaFilteringUiRootTransform.localPosition = _vanillaFilteringUiOriginalPosition;
    }

    _hasVanillaFilteringUiOriginalPosition = false;
    _vanillaFilteringUiRootTransform = null;
  }

  private void FitSpriteRenderer(SpriteRenderer spriteRenderer, Vector2 targetSize)
  {
    if (spriteRenderer == null || spriteRenderer.sprite == null)
    {
      return;
    }

    Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
    float maxAxis = Mathf.Max(spriteSize.x, spriteSize.y);
    if (maxAxis <= 0.0f)
    {
      return;
    }

    float targetAxis = Mathf.Min(targetSize.x, targetSize.y);
    float scale = targetAxis / maxAxis;
    spriteRenderer.transform.localScale = new Vector3(scale, scale, 1.0f);
  }

  private GameObject FindNativeObject(string objectName)
  {
    return _nativeObjectsByName.TryGetValue(objectName, out GameObject nativeObject)
        ? nativeObject
        : null;
  }

  private void SetActiveIfFound(string objectName, bool active)
  {
    GameObject nativeObject = FindNativeObject(objectName);
    if (nativeObject != null)
    {
      nativeObject.SetActive(active);
    }
  }

  private GameObject FindGameObjectInSource(string objectName)
  {
    Transform[] transforms = GetComponentsInChildren<Transform>(true);
    for (int i = 0; i < transforms.Length; i++)
    {
      if (transforms[i].name == objectName)
      {
        return transforms[i].gameObject;
      }
    }

    return null;
  }

  private RectTransform FindSourceRect(string objectName)
  {
    GameObject sourceObject = FindGameObjectInSource(objectName);
    return sourceObject != null ? sourceObject.GetComponent<RectTransform>() : null;
  }

  private static GameObject FindDirectChild(GameObject parent, string childName)
  {
    if (parent == null)
    {
      return null;
    }

    Transform child = parent.transform.Find(childName);
    return child != null ? child.gameObject : null;
  }

  private static void SetNativeLayer(GameObject target)
  {
    if (target != null && ObjectLayerID.UI >= 0)
    {
      target.layer = ObjectLayerID.UI;
    }
  }

  private void OnDisable()
  {
    EndPendingPick();
  }

  private void OnDestroy()
  {
    EndPendingPick();
  }
}

public sealed class TinyPixelText
{
  public enum Alignment
  {
    Left,
    Center
  }

  private const float PixelSize = 1.0f / 16.0f;
  private const int GlyphHeight = 5;
  private const int GlyphSpacing = 1;

  private static Sprite _pixelSprite;

  private readonly List<GameObject> _pixels = new();
  private readonly int _sortingOrder;
  private readonly Color _color;
  private readonly Alignment _alignment;

  public TinyPixelText(Transform root, int sortingOrder, Color color, Alignment alignment)
  {
    Root = root;
    _sortingOrder = sortingOrder;
    _color = color;
    _alignment = alignment;
  }

  public Transform Root { get; }

  public void Render(string value)
  {
    Clear();

    if (Root == null || string.IsNullOrEmpty(value))
    {
      return;
    }

    string text = value;
    int width = Measure(text);
    float startX = _alignment == Alignment.Center
        ? -width * PixelSize * 0.5f
        : 0.0f;

    int cursor = 0;
    for (int i = 0; i < text.Length; i++)
    {
      string[] glyph = GetGlyph(text[i]);
      int glyphWidth = glyph[0].Length;
      for (int row = 0; row < GlyphHeight; row++)
      {
        for (int column = 0; column < glyphWidth; column++)
        {
          if (glyph[row][column] != '1')
          {
            continue;
          }

          AddPixel(
              startX + (cursor + column) * PixelSize,
              ((GlyphHeight - 1) * 0.5f - row) * PixelSize);
        }
      }

      cursor += glyphWidth + GlyphSpacing;
    }
  }

  private void Clear()
  {
    for (int i = 0; i < _pixels.Count; i++)
    {
      if (_pixels[i] != null)
      {
        Object.Destroy(_pixels[i]);
      }
    }

    _pixels.Clear();
  }

  private void AddPixel(float x, float y)
  {
    GameObject pixel = new GameObject("Pixel");
    pixel.layer = Root.gameObject.layer;
    pixel.transform.SetParent(Root, false);
    pixel.transform.localPosition = new Vector3(x, y, 0.0f);
    pixel.transform.localRotation = Quaternion.identity;
    pixel.transform.localScale = Vector3.one;

    SpriteRenderer renderer = pixel.AddComponent<SpriteRenderer>();
    renderer.sprite = GetPixelSprite();
    renderer.color = _color;
    renderer.sortingLayerID = SortingLayerID.GUI;
    renderer.sortingOrder = _sortingOrder;
    _pixels.Add(pixel);
  }

  private static Sprite GetPixelSprite()
  {
    if (_pixelSprite == null)
    {
      _pixelSprite = Sprite.Create(
          Texture2D.whiteTexture,
          new Rect(0, 0, 1, 1),
          new Vector2(0.5f, 0.5f),
          16.0f,
          0,
          SpriteMeshType.FullRect);
    }

    return _pixelSprite;
  }

  private static int Measure(string text)
  {
    int width = 0;
    for (int i = 0; i < text.Length; i++)
    {
      width += GetGlyph(text[i])[0].Length;
      if (i < text.Length - 1)
      {
        width += GlyphSpacing;
      }
    }

    return width;
  }

  private static string[] GetGlyph(char c)
  {
    switch (c)
    {
      case 'A': return new[] { "010", "101", "111", "101", "101" };
      case 'B': return new[] { "110", "101", "110", "101", "110" };
      case 'C': return new[] { "011", "100", "100", "100", "011" };
      case 'D': return new[] { "110", "101", "101", "101", "110" };
      case 'E': return new[] { "111", "100", "110", "100", "111" };
      case 'F': return new[] { "111", "100", "110", "100", "100" };
      case 'G': return new[] { "011", "100", "101", "101", "011" };
      case 'H': return new[] { "101", "101", "111", "101", "101" };
      case 'I': return new[] { "111", "010", "010", "010", "111" };
      case 'J': return new[] { "001", "001", "001", "101", "010" };
      case 'K': return new[] { "101", "101", "110", "101", "101" };
      case 'L': return new[] { "100", "100", "100", "100", "111" };
      case 'M': return new[] { "1001", "1111", "1001", "1001", "1001" };
      case 'N': return new[] { "1001", "1101", "1011", "1001", "1001" };
      case 'O': return new[] { "010", "101", "101", "101", "010" };
      case 'P': return new[] { "110", "101", "110", "100", "100" };
      case 'Q': return new[] { "010", "101", "101", "011", "001" };
      case 'R': return new[] { "110", "101", "110", "101", "101" };
      case 'S': return new[] { "011", "100", "010", "001", "110" };
      case 'T': return new[] { "111", "010", "010", "010", "010" };
      case 'U': return new[] { "101", "101", "101", "101", "111" };
      case 'V': return new[] { "101", "101", "101", "101", "010" };
      case 'W': return new[] { "1001", "1001", "1001", "1111", "1001" };
      case 'X': return new[] { "101", "101", "010", "101", "101" };
      case 'Y': return new[] { "101", "101", "010", "010", "010" };
      case 'Z': return new[] { "111", "001", "010", "100", "111" };
      case 'a': return new[] { "000", "011", "101", "111", "101" };
      case 'b': return new[] { "100", "110", "101", "101", "110" };
      case 'c': return new[] { "000", "011", "100", "100", "011" };
      case 'd': return new[] { "001", "011", "101", "101", "011" };
      case 'e': return new[] { "000", "111", "110", "100", "111" };
      case 'f': return new[] { "011", "100", "110", "100", "100" };
      case 'g': return new[] { "000", "011", "101", "011", "001" };
      case 'h': return new[] { "100", "110", "101", "101", "101" };
      case 'i': return new[] { "0", "1", "1", "1", "1" };
      case 'j': return new[] { "001", "000", "001", "101", "010" };
      case 'k': return new[] { "100", "101", "110", "101", "101" };
      case 'l': return new[] { "1", "1", "1", "1", "1" };
      case 'm': return new[] { "0000", "1110", "1011", "1011", "1011" };
      case 'n': return new[] { "000", "110", "101", "101", "101" };
      case 'o': return new[] { "000", "010", "101", "101", "010" };
      case 'p': return new[] { "000", "110", "101", "110", "100" };
      case 'q': return new[] { "000", "011", "101", "011", "001" };
      case 'r': return new[] { "000", "101", "110", "100", "100" };
      case 's': return new[] { "000", "011", "110", "001", "110" };
      case 't': return new[] { "010", "111", "010", "010", "011" };
      case 'u': return new[] { "000", "101", "101", "101", "111" };
      case 'v': return new[] { "000", "101", "101", "101", "010" };
      case 'w': return new[] { "0000", "1001", "1001", "1111", "0110" };
      case 'x': return new[] { "000", "101", "010", "010", "101" };
      case 'y': return new[] { "000", "101", "101", "011", "001" };
      case 'z': return new[] { "000", "111", "001", "010", "111" };
      case '0': return new[] { "111", "101", "101", "101", "111" };
      case '1': return new[] { "010", "110", "010", "010", "111" };
      case '2': return new[] { "110", "001", "010", "100", "111" };
      case '3': return new[] { "110", "001", "010", "001", "110" };
      case '4': return new[] { "101", "101", "111", "001", "001" };
      case '5': return new[] { "111", "100", "110", "001", "110" };
      case '6': return new[] { "011", "100", "110", "101", "010" };
      case '7': return new[] { "111", "001", "010", "010", "010" };
      case '8': return new[] { "010", "101", "010", "101", "010" };
      case '9': return new[] { "010", "101", "011", "001", "110" };
      case '.': return new[] { "0", "0", "0", "0", "1" };
      case '-': return new[] { "000", "000", "111", "000", "000" };
      case '_': return new[] { "000", "000", "000", "000", "111" };
      case '/': return new[] { "001", "001", "010", "100", "100" };
      case ' ': return new[] { "0", "0", "0", "0", "0" };
      default: return new[] { "111", "001", "010", "000", "010" };
    }
  }
}

public sealed class SmartSplitterNativeButton : UIelement
{
  public System.Action Clicked;
  public GameObject HoverHighlight;
  public bool IsHovered { get; private set; }
  public string HoverTitle;
  public string HoverDescription;
  public System.Func<TextAndFormatFields> HoverTitleProvider;
  public System.Func<List<TextAndFormatFields>> HoverDescriptionProvider;

  private readonly List<SpriteRenderer> _visuals = new();
  private readonly List<Color> _defaultColors = new();

  public void InitializeVisuals()
  {
    _visuals.Clear();
    _defaultColors.Clear();

    SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
    for (int i = 0; i < renderers.Length; i++)
    {
      if (renderers[i] == null)
      {
        continue;
      }

      _visuals.Add(renderers[i]);
      _defaultColors.Add(renderers[i].color);
    }
  }

  private void Awake()
  {
    topUIElements ??= new List<UIelement>();
    bottomUIElements ??= new List<UIelement>();
    leftUIElements ??= new List<UIelement>();
    rightUIElements ??= new List<UIelement>();
    childElements ??= new List<UIelement>();
  }

  public override void OnSelected()
  {
    base.OnSelected();
    SetHover(true);
  }

  public override void OnDeselected(bool playEffect = true)
  {
    base.OnDeselected(playEffect);
    SetHover(false);
  }

  public override void OnLeftClicked(bool mod1, bool mod2)
  {
    Clicked?.Invoke();
  }

  public override TextAndFormatFields GetHoverTitle()
  {
    if (HoverTitleProvider != null)
    {
      return HoverTitleProvider();
    }

    if (string.IsNullOrEmpty(HoverTitle))
    {
      return base.GetHoverTitle();
    }

    return new TextAndFormatFields
    {
      text = HoverTitle,
      dontLocalize = true
    };
  }

  public override List<TextAndFormatFields> GetHoverDescription()
  {
    if (HoverDescriptionProvider != null)
    {
      return HoverDescriptionProvider();
    }

    if (string.IsNullOrEmpty(HoverDescription))
    {
      return base.GetHoverDescription();
    }

    return new List<TextAndFormatFields>
    {
      new TextAndFormatFields
      {
        text = HoverDescription,
        color = Color.white * 0.99f,
        dontLocalize = true
      }
    };
  }

  protected override void LateUpdate()
  {
    base.LateUpdate();

    float multiplier = leftClickIsHeldDown ? 0.58f : 1.0f;
    for (int i = 0; i < _visuals.Count; i++)
    {
      if (_visuals[i] != null)
      {
        _visuals[i].color = _defaultColors[i] * multiplier;
      }
    }
  }

  protected override void OnDisable()
  {
    base.OnDisable();
    SetHover(false);
  }

  private void SetHover(bool show)
  {
    IsHovered = show;
    if (HoverHighlight != null)
    {
      HoverHighlight.SetActive(show);
    }
  }
}
