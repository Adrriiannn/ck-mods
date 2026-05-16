using System;
using Inventory;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public sealed class SmartSplitterPanelController : MonoBehaviour
{
  [Serializable]
  public sealed class SlotView
  {
    public Button Button;
    public Image Background;
    public Image Icon;
    public GameObject HoverHighlight;
  }

  [Serializable]
  public sealed class LaneView
  {
    public string LaneName;
    public Button FilterButton;
    public GameObject FilterHighlight;
    public Image FilterSilhouette;
    public SlotView Slot0;
    public SlotView Slot1;
  }

  [Header("Root")]
  [SerializeField] private Canvas canvas;
  [SerializeField] private RectTransform panelRoot;

  [Header("Overlay Canvas")]
  [Tooltip("For the Unity-UI version, keep the Smart Splitter panel in its own Screen Space Overlay canvas. The custom cursor is rendered in this same canvas so it can appear above the panel.")]
  [SerializeField] private bool forceScreenSpaceOverlayCanvas = true;

  [Tooltip("Sorting order for the Smart Splitter overlay canvas. It should be high enough to render above vanilla UI; the custom cursor renders above the panel inside this canvas.")]
  [SerializeField] private int overlayCanvasSortingOrder = 1000;

  [Tooltip("If the prefab Canvas was accidentally saved with zero scale/size, normalize it on open so the panel cannot disappear.")]
  [SerializeField] private bool normalizeOverlayCanvasOnOpen = true;

  [Header("Vanilla UI Integration")]
  [Tooltip("Calls Manager.ui.OnPlayerInventoryOpen() when opening and HideAllInventoryAndCraftingUI(true) when closing.")]
  [SerializeField] private bool useVanillaInventoryMode = true;

  [Tooltip("After opening the bottom inventory, hide vanilla side panels so only the backpack/trash style UI remains alongside this custom panel.")]
  [SerializeField] private bool hideVanillaSidePanelsOnOpen = true;

  [Tooltip("Vanilla filter structures do not open their filter panel over an already-open player inventory. If inventory is already open before interacting with the smart splitter, close it and require a second interact press.")]
  [SerializeField] private bool closeExistingInventoryInsteadOfOpening = true;

  [Header("Cursor Overlay")]
  [Tooltip("The fake cursor object inside this panel's overlay Canvas. If left empty, the controller finds a child named SmartSplitterCursorOverlay.")]
  [SerializeField] private RectTransform cursorOverlay;

  [SerializeField] private Image cursorOverlayImage;

  [Tooltip("Optional image child used for the item currently held by the mouse. If missing, it is created at runtime.")]
  [SerializeField] private Image cursorHeldItemImage;

  [Tooltip("Optional cage/overlay image shown when vanilla says the held item needs one. If missing, it is created at runtime.")]
  [SerializeField] private Image cursorHeldItemOverlayImage;

  [Tooltip("Optional cage/underlay image shown when vanilla says the held item needs one. If missing, it is created at runtime.")]
  [SerializeField] private Image cursorHeldItemUnderlayImage;

  [Tooltip("Optional stack amount text. If missing, a simple Unity UI Text is created at runtime.")]
  [SerializeField] private Text cursorHeldItemAmountText;

  [Tooltip("Moves the overlay cursor relative to Input.mousePosition. Positive X moves right. Positive Y moves up. Use this to line up with the vanilla cursor hotspot.")]
  [SerializeField] private Vector2 cursorScreenOffset = new Vector2(6f, -6f);

  [Tooltip("Snaps the overlay cursor to integer screen/UI pixels, mimicking UIMouse's RoundToPixelPerfectPosition behavior as closely as a Screen Space Overlay cursor can.")]
  [SerializeField] private bool pixelSnapCursor = true;

  [Tooltip("Optional pixel grid size for the overlay cursor. 1 means integer-pixel movement.")]
  [SerializeField] private float cursorPixelSnap = 2f;

  [SerializeField] private Color cursorNormalColor = Color.white;

  [Tooltip("Vanilla UIMouse uses Color.white * 0.7f while either mouse button is pressed.")]
  [SerializeField] private Color cursorMouseDownColor = new Color(0.7f, 0.7f, 0.7f, 1f);

  [SerializeField] private Vector2 heldItemAnchoredPosition = new Vector2(22f, -4f);
  [SerializeField] private Vector2 heldItemSize = new Vector2(32f, 32f);
  [SerializeField] private Vector2 heldItemAmountAnchoredPosition = new Vector2(31f, -24f);
  [SerializeField] private int heldItemAmountFontSize = 14;
  [SerializeField] private bool hideVanillaCursorWhileOpen = true;

  [Header("Power")]
  [SerializeField] private GameObject electricityIconOn;
  [SerializeField] private GameObject electricityIconOff;

  [Header("Lanes")]
  [SerializeField] private LaneView leftLane;
  [SerializeField] private LaneView centerLane;
  [SerializeField] private LaneView rightLane;

  private Entity _splitterEntity;
  private World _world;
  private bool _isOpen;
  private bool _openedVanillaInventoryMode;

  private Transform _originalCanvasParent;
  private int _originalCanvasSiblingIndex;
  private Vector3 _originalCanvasLocalPosition;
  private Quaternion _originalCanvasLocalRotation;
  private Vector3 _originalCanvasLocalScale;
  private RenderMode _originalCanvasRenderMode;
  private Camera _originalCanvasWorldCamera;
  private bool _originalCanvasOverrideSorting;
  private int _originalCanvasSortingOrder;
  private float _originalCanvasPlaneDistance;
  private bool _capturedCanvasDefaults;

  private bool _capturedVanillaCursorState;
  private bool _vanillaPointerWasActive;
  private bool _vanillaPointerSpriteWasEnabled;

  private Sprite _lastHeldSprite;
  private int _lastHeldAmount = int.MinValue;
  private bool _lastHeldCageOverlay;

  public bool IsOpen => _isOpen;
  public Entity SplitterEntity => _splitterEntity;

  private void Update()
  {
    if (!_isOpen)
    {
      return;
    }

    UpdateOverlayCursor();

    if (Input.GetKeyDown(KeyCode.Tab))
    {
      Close("tab");
      return;
    }

    if (Input.GetKeyDown(KeyCode.Escape))
    {
      Close("escape");
    }
  }

  private void LateUpdate()
  {
    if (!_isOpen)
    {
      return;
    }

    // UIMouse also updates in LateUpdate, so suppress and redraw here too.
    // This keeps the vanilla cursor and vanilla grabbed-item visuals from leaking over/under our overlay cursor.
    CaptureAndHideVanillaCursor();
    UpdateOverlayCursor();
  }

  public void Open(Entity splitterEntity, World world, bool powered)
  {
    if (ShouldCloseExistingInventoryInsteadOfOpening())
    {
      Debug.Log($"[SmartSplitterPanel] CLOSE_EXISTING_INVENTORY_INSTEAD_OF_OPEN splitter={splitterEntity}");
      CloseVanillaInventoryMode();
      _splitterEntity = Entity.Null;
      _world = null;
      _isOpen = false;
      gameObject.SetActive(false);
      return;
    }

    _splitterEntity = splitterEntity;
    _world = world;
    _isOpen = true;

    gameObject.SetActive(true);

    CaptureCanvasDefaultsIfNeeded();
    NormalizeOverlayCanvas();

    if (useVanillaInventoryMode)
    {
      OpenVanillaInventoryMode();
    }

    EnsureCursorOverlayReferences();
    SetOverlayCursorVisible(true);
    CaptureAndHideVanillaCursor();

    SetPowered(powered);
    ClearHoverState();
    UpdateOverlayCursor();

    Debug.Log(
      $"[SmartSplitterPanel] OPEN splitter={splitterEntity} " +
      $"world={(world != null ? world.Name : "null")} powered={powered}"
    );
  }

  public void Close(string reason)
  {
    if (!_isOpen)
    {
      return;
    }

    Debug.Log($"[SmartSplitterPanel] CLOSE splitter={_splitterEntity} reason={reason}");

    _isOpen = false;
    _splitterEntity = Entity.Null;
    _world = null;

    ClearHoverState();
    SetOverlayCursorVisible(false);
    RestoreVanillaCursor();

    if (useVanillaInventoryMode && _openedVanillaInventoryMode)
    {
      CloseVanillaInventoryMode();
    }

    _openedVanillaInventoryMode = false;

    RestoreCanvasDefaults();
    gameObject.SetActive(false);
  }

  public void Toggle(Entity splitterEntity, World world, bool powered)
  {
    if (_isOpen && _splitterEntity == splitterEntity)
    {
      Close("toggle");
      return;
    }

    Open(splitterEntity, world, powered);
  }

  public void SetPowered(bool powered)
  {
    if (electricityIconOn != null)
    {
      electricityIconOn.SetActive(powered);
    }

    if (electricityIconOff != null)
    {
      electricityIconOff.SetActive(!powered);
    }
  }

  public void ClearHoverState()
  {
    SetLaneHover(leftLane, false);
    SetLaneHover(centerLane, false);
    SetLaneHover(rightLane, false);

    SetSlotHover(leftLane?.Slot0, false);
    SetSlotHover(leftLane?.Slot1, false);
    SetSlotHover(centerLane?.Slot0, false);
    SetSlotHover(centerLane?.Slot1, false);
    SetSlotHover(rightLane?.Slot0, false);
    SetSlotHover(rightLane?.Slot1, false);
  }

  private void NormalizeOverlayCanvas()
  {
    if (canvas == null)
    {
      return;
    }

    if (forceScreenSpaceOverlayCanvas)
    {
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.worldCamera = null;
      canvas.planeDistance = 100f;
    }

    canvas.overrideSorting = true;
    canvas.sortingOrder = overlayCanvasSortingOrder;

    if (!normalizeOverlayCanvasOnOpen)
    {
      return;
    }

    RectTransform canvasRect = canvas.GetComponent<RectTransform>();
    if (canvasRect != null)
    {
      canvasRect.localPosition = Vector3.zero;
      canvasRect.localRotation = Quaternion.identity;
      canvasRect.localScale = Vector3.one;

      // Screen Space Overlay canvases drive their own visible rect at runtime,
      // but this protects prefab instances accidentally saved with width/height 0.
      if (canvasRect.sizeDelta.x <= 0f || canvasRect.sizeDelta.y <= 0f)
      {
        float width = Screen.width > 0 ? Screen.width : 1920f;
        float height = Screen.height > 0 ? Screen.height : 1080f;
        canvasRect.sizeDelta = new Vector2(width, height);
      }
    }
    else
    {
      canvas.transform.localPosition = Vector3.zero;
      canvas.transform.localRotation = Quaternion.identity;
      canvas.transform.localScale = Vector3.one;
    }

    if (panelRoot != null)
    {
      panelRoot.SetAsFirstSibling();
    }
  }

  private void EnsureCursorOverlayReferences()
  {
    if (canvas == null)
    {
      return;
    }

    if (cursorOverlay == null)
    {
      Transform found = canvas.transform.Find("SmartSplitterCursorOverlay");
      if (found != null)
      {
        cursorOverlay = found as RectTransform;
      }
    }

    if (cursorOverlay == null)
    {
      GameObject cursorGo = new GameObject("SmartSplitterCursorOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
      cursorOverlay = cursorGo.GetComponent<RectTransform>();
      cursorOverlay.SetParent(canvas.transform, false);
      cursorOverlay.sizeDelta = new Vector2(8f, 8f);
      cursorOverlay.pivot = new Vector2(0f, 1f);
      cursorOverlay.anchorMin = new Vector2(0.5f, 0.5f);
      cursorOverlay.anchorMax = new Vector2(0.5f, 0.5f);
      cursorOverlayImage = cursorGo.GetComponent<Image>();
    }

    if (cursorOverlayImage == null)
    {
      cursorOverlayImage = cursorOverlay.GetComponent<Image>();
    }

    cursorOverlay.anchorMin = new Vector2(0.5f, 0.5f);
    cursorOverlay.anchorMax = new Vector2(0.5f, 0.5f);
    cursorOverlay.pivot = new Vector2(0f, 1f);
    if (cursorOverlay.sizeDelta.x <= 0f || cursorOverlay.sizeDelta.y <= 0f)
    {
      cursorOverlay.sizeDelta = new Vector2(8f, 8f);
    }

    if (cursorOverlayImage != null)
    {
      cursorOverlayImage.raycastTarget = false;
      cursorOverlayImage.preserveAspect = true;
    }

    cursorOverlay.SetAsLastSibling();

    EnsureHeldItemOverlayObjects();
  }

  private void EnsureHeldItemOverlayObjects()
  {
    if (cursorOverlay == null)
    {
      return;
    }

    if (cursorHeldItemUnderlayImage == null)
    {
      cursorHeldItemUnderlayImage = FindChildImage(cursorOverlay, "HeldItemUnderlay");
      if (cursorHeldItemUnderlayImage == null)
      {
        cursorHeldItemUnderlayImage = CreateCursorChildImage("HeldItemUnderlay");
      }
    }

    if (cursorHeldItemImage == null)
    {
      cursorHeldItemImage = FindChildImage(cursorOverlay, "HeldItemIcon");
      if (cursorHeldItemImage == null)
      {
        cursorHeldItemImage = CreateCursorChildImage("HeldItemIcon");
      }
    }

    if (cursorHeldItemOverlayImage == null)
    {
      cursorHeldItemOverlayImage = FindChildImage(cursorOverlay, "HeldItemOverlay");
      if (cursorHeldItemOverlayImage == null)
      {
        cursorHeldItemOverlayImage = CreateCursorChildImage("HeldItemOverlay");
      }
    }

    SetupHeldItemImage(cursorHeldItemUnderlayImage);
    SetupHeldItemImage(cursorHeldItemImage);
    SetupHeldItemImage(cursorHeldItemOverlayImage);

    if (cursorHeldItemAmountText == null)
    {
      Transform existing = cursorOverlay.Find("HeldItemAmount");
      if (existing != null)
      {
        cursorHeldItemAmountText = existing.GetComponent<Text>();
      }

      if (cursorHeldItemAmountText == null)
      {
        GameObject amountGo = new GameObject("HeldItemAmount", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform amountRect = amountGo.GetComponent<RectTransform>();
        amountRect.SetParent(cursorOverlay, false);
        amountRect.anchorMin = new Vector2(0f, 1f);
        amountRect.anchorMax = new Vector2(0f, 1f);
        amountRect.pivot = new Vector2(1f, 0f);
        amountRect.sizeDelta = new Vector2(32f, 16f);
        cursorHeldItemAmountText = amountGo.GetComponent<Text>();
        cursorHeldItemAmountText.alignment = TextAnchor.LowerRight;
        cursorHeldItemAmountText.raycastTarget = false;
        cursorHeldItemAmountText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
      }
    }

    if (cursorHeldItemAmountText != null)
    {
      RectTransform amountRect = cursorHeldItemAmountText.rectTransform;
      amountRect.anchoredPosition = heldItemAmountAnchoredPosition;
      cursorHeldItemAmountText.fontSize = heldItemAmountFontSize;
      cursorHeldItemAmountText.color = Color.white;
      cursorHeldItemAmountText.raycastTarget = false;
    }

    // Render order: underlay < item icon < overlay < amount text.
    if (cursorHeldItemUnderlayImage != null)
    {
      cursorHeldItemUnderlayImage.transform.SetSiblingIndex(0);
    }

    if (cursorHeldItemImage != null)
    {
      cursorHeldItemImage.transform.SetSiblingIndex(1);
    }

    if (cursorHeldItemOverlayImage != null)
    {
      cursorHeldItemOverlayImage.transform.SetSiblingIndex(2);
    }

    if (cursorHeldItemAmountText != null)
    {
      cursorHeldItemAmountText.transform.SetAsLastSibling();
    }
  }

  private Image CreateCursorChildImage(string name)
  {
    GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
    RectTransform rect = go.GetComponent<RectTransform>();
    rect.SetParent(cursorOverlay, false);
    Image image = go.GetComponent<Image>();
    image.raycastTarget = false;
    return image;
  }

  private static Image FindChildImage(RectTransform parent, string childName)
  {
    if (parent == null)
    {
      return null;
    }

    Transform child = parent.Find(childName);
    return child != null ? child.GetComponent<Image>() : null;
  }

  private void SetupHeldItemImage(Image image)
  {
    if (image == null)
    {
      return;
    }

    RectTransform rect = image.rectTransform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = heldItemAnchoredPosition;
    rect.sizeDelta = heldItemSize;
    image.raycastTarget = false;
    image.preserveAspect = true;
  }

  private void UpdateOverlayCursor()
  {
    if (!_isOpen || cursorOverlay == null || canvas == null)
    {
      return;
    }

    CaptureAndHideVanillaCursor();

    Vector2 screenPosition = Input.mousePosition;
    if (pixelSnapCursor)
    {
      screenPosition = Snap(screenPosition, Mathf.Max(0.0001f, cursorPixelSnap));
    }

    Vector2 localPosition;
    RectTransform canvasRect = canvas.GetComponent<RectTransform>();
    if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out localPosition))
    {
      localPosition += cursorScreenOffset;
    }
    else
    {
      localPosition = screenPosition + cursorScreenOffset;
    }

    if (pixelSnapCursor)
    {
      localPosition = Snap(localPosition, Mathf.Max(0.0001f, cursorPixelSnap));
    }

    cursorOverlay.anchoredPosition = localPosition;
    cursorOverlay.SetAsLastSibling();

    if (cursorOverlayImage != null)
    {
      bool mouseDown = Input.GetMouseButton(0) || Input.GetMouseButton(1);
      cursorOverlayImage.color = mouseDown ? cursorMouseDownColor : cursorNormalColor;
    }

    UpdateHeldItemCursorOverlay();
  }

  private static Vector2 Snap(Vector2 value, float snap)
  {
    return new Vector2(
      Mathf.Round(value.x / snap) * snap,
      Mathf.Round(value.y / snap) * snap
    );
  }

  private void UpdateHeldItemCursorOverlay()
  {
    if (cursorHeldItemImage == null)
    {
      return;
    }

    InventoryHandler mouseInventory = null;
    if (Manager.ui != null && Manager.ui.mouse != null)
    {
      mouseInventory = Manager.ui.mouse.mouseInventory;
    }

    if (mouseInventory == null || !mouseInventory.HasObject(0) || (Manager.menu != null && Manager.menu.IsAnyMenuActive()))
    {
      ClearHeldItemCursorOverlay();
      return;
    }

    ContainedObjectsBuffer containedObjectData = mouseInventory.GetContainedObjectData(0);

    ObjectInfo objectInfo;
    Sprite itemSprite = null;

    if (PugDatabase.TryGetObjectInfo(containedObjectData.objectID, out objectInfo, containedObjectData.variation))
    {
      if (Manager.ui != null && Manager.ui.itemOverridesTable != null)
      {
        itemSprite = Manager.ui.itemOverridesTable.GetIconOverride(containedObjectData.objectData, false);
      }

      if (itemSprite == null && objectInfo != null)
      {
        itemSprite = objectInfo.icon;
      }
    }
    else if (Manager.ui != null && Manager.ui.mouse != null)
    {
      itemSprite = Manager.ui.mouse.missingItemSprite;
    }

    bool showItem = itemSprite != null;
    cursorHeldItemImage.gameObject.SetActive(showItem);
    cursorHeldItemImage.sprite = itemSprite;
    cursorHeldItemImage.color = Color.white;
    cursorHeldItemImage.preserveAspect = true;
    cursorHeldItemImage.rectTransform.sizeDelta = heldItemSize;
    cursorHeldItemImage.rectTransform.anchoredPosition = heldItemAnchoredPosition;

    bool showCageOverlay = false;
    if (showItem && Manager.ui != null)
    {
      showCageOverlay = Manager.ui.ShouldShowCageOverlay(containedObjectData);
    }

    SetCageOverlayImage(cursorHeldItemUnderlayImage, Manager.ui != null && Manager.ui.mouse != null ? Manager.ui.mouse.grabbedItemUnderlaySR : null, showCageOverlay);
    SetCageOverlayImage(cursorHeldItemOverlayImage, Manager.ui != null && Manager.ui.mouse != null ? Manager.ui.mouse.grabbedItemOverlaySR : null, showCageOverlay);

    string amountText = string.Empty;
    if (objectInfo != null && objectInfo.isStackable && containedObjectData.amount > 1)
    {
      amountText = containedObjectData.amount.ToString();
    }

    if (cursorHeldItemAmountText != null)
    {
      cursorHeldItemAmountText.text = amountText;
      cursorHeldItemAmountText.gameObject.SetActive(!string.IsNullOrEmpty(amountText));
    }

    _lastHeldSprite = itemSprite;
    _lastHeldAmount = containedObjectData.amount;
    _lastHeldCageOverlay = showCageOverlay;
  }

  private static void SetCageOverlayImage(Image image, SpriteRenderer vanillaRenderer, bool visible)
  {
    if (image == null)
    {
      return;
    }

    image.gameObject.SetActive(visible);

    if (!visible)
    {
      return;
    }

    if (vanillaRenderer != null)
    {
      image.sprite = vanillaRenderer.sprite;
      image.color = vanillaRenderer.color;
    }
    else
    {
      image.color = Color.white;
    }
  }

  private void ClearHeldItemCursorOverlay()
  {
    if (cursorHeldItemImage != null)
    {
      cursorHeldItemImage.sprite = null;
      cursorHeldItemImage.gameObject.SetActive(false);
    }

    if (cursorHeldItemOverlayImage != null)
    {
      cursorHeldItemOverlayImage.gameObject.SetActive(false);
    }

    if (cursorHeldItemUnderlayImage != null)
    {
      cursorHeldItemUnderlayImage.gameObject.SetActive(false);
    }

    if (cursorHeldItemAmountText != null)
    {
      cursorHeldItemAmountText.text = string.Empty;
      cursorHeldItemAmountText.gameObject.SetActive(false);
    }

    _lastHeldSprite = null;
    _lastHeldAmount = int.MinValue;
    _lastHeldCageOverlay = false;
  }

  private void SetOverlayCursorVisible(bool visible)
  {
    EnsureCursorOverlayReferences();

    if (cursorOverlay != null)
    {
      cursorOverlay.gameObject.SetActive(visible);
      if (visible)
      {
        cursorOverlay.SetAsLastSibling();
      }
    }

    if (!visible)
    {
      ClearHeldItemCursorOverlay();
    }
  }

  private void CaptureAndHideVanillaCursor()
  {
    if (!hideVanillaCursorWhileOpen || Manager.ui == null || Manager.ui.mouse == null || Manager.ui.mouse.pointer == null)
    {
      return;
    }

    if (!_capturedVanillaCursorState)
    {
      _capturedVanillaCursorState = true;
      _vanillaPointerWasActive = Manager.ui.mouse.pointer.gameObject.activeSelf;
      _vanillaPointerSpriteWasEnabled = Manager.ui.mouse.pointerSR != null && Manager.ui.mouse.pointerSR.enabled;
    }

    // UIMouse updates every frame, so keep suppressing the whole vanilla cursor object while our overlay cursor is active.
    // Disabling only pointerSR was not enough because the pointer GameObject and grabbed-item renderers can be re-enabled by UIMouse.
    if (Manager.ui.mouse.pointer != null)
    {
      Manager.ui.mouse.pointer.gameObject.SetActive(false);
    }

    if (Manager.ui.mouse.pointerSR != null)
    {
      Manager.ui.mouse.pointerSR.enabled = false;
    }

    if (Manager.ui.mouse.controllerMapAimSR != null)
    {
      Manager.ui.mouse.controllerMapAimSR.enabled = false;
    }

    if (Manager.ui.mouse.grabbedItemSR != null)
    {
      Manager.ui.mouse.grabbedItemSR.enabled = false;
    }

    if (Manager.ui.mouse.grabbedItemOverlaySR != null)
    {
      Manager.ui.mouse.grabbedItemOverlaySR.gameObject.SetActive(false);
    }

    if (Manager.ui.mouse.grabbedItemUnderlaySR != null)
    {
      Manager.ui.mouse.grabbedItemUnderlaySR.gameObject.SetActive(false);
    }
  }

  private void RestoreVanillaCursor()
  {
    if (!_capturedVanillaCursorState || Manager.ui == null || Manager.ui.mouse == null)
    {
      _capturedVanillaCursorState = false;
      return;
    }

    if (Manager.ui.mouse.pointer != null)
    {
      Manager.ui.mouse.pointer.gameObject.SetActive(_vanillaPointerWasActive);
    }

    if (Manager.ui.mouse.pointerSR != null)
    {
      Manager.ui.mouse.pointerSR.enabled = _vanillaPointerSpriteWasEnabled;
    }

    _capturedVanillaCursorState = false;
  }

  private void CaptureCanvasDefaultsIfNeeded()
  {
    if (_capturedCanvasDefaults || canvas == null)
    {
      return;
    }

    _capturedCanvasDefaults = true;
    _originalCanvasParent = canvas.transform.parent;
    _originalCanvasSiblingIndex = canvas.transform.GetSiblingIndex();
    _originalCanvasLocalPosition = canvas.transform.localPosition;
    _originalCanvasLocalRotation = canvas.transform.localRotation;
    _originalCanvasLocalScale = canvas.transform.localScale;
    _originalCanvasRenderMode = canvas.renderMode;
    _originalCanvasWorldCamera = canvas.worldCamera;
    _originalCanvasOverrideSorting = canvas.overrideSorting;
    _originalCanvasSortingOrder = canvas.sortingOrder;
    _originalCanvasPlaneDistance = canvas.planeDistance;
  }

  private void RestoreCanvasDefaults()
  {
    if (!_capturedCanvasDefaults || canvas == null)
    {
      return;
    }

    canvas.transform.SetParent(_originalCanvasParent, false);
    canvas.transform.SetSiblingIndex(_originalCanvasSiblingIndex);
    canvas.transform.localPosition = _originalCanvasLocalPosition;
    canvas.transform.localRotation = _originalCanvasLocalRotation;
    canvas.transform.localScale = _originalCanvasLocalScale;
    canvas.renderMode = _originalCanvasRenderMode;
    canvas.worldCamera = _originalCanvasWorldCamera;
    canvas.overrideSorting = _originalCanvasOverrideSorting;
    canvas.sortingOrder = _originalCanvasSortingOrder;
    canvas.planeDistance = _originalCanvasPlaneDistance;
  }

  private bool ShouldCloseExistingInventoryInsteadOfOpening()
  {
    if (!closeExistingInventoryInsteadOfOpening || _isOpen || !useVanillaInventoryMode)
    {
      return false;
    }

    if (Manager.ui == null)
    {
      return false;
    }

    // Match vanilla filter structure behaviour: if the player already has an inventory-style
    // UI open before interacting, the first interact closes that existing UI instead of
    // opening a filter window over it. The next interact opens the filter panel cleanly.
    return Manager.ui.isAnyInventoryShowing
      || Manager.ui.isPlayerEquipmentShowing
      || Manager.ui.isVanitySlotsShowing
      || Manager.ui.isSalvageAndRepairUIShowing
      || Manager.ui.isUpgradeForgeUIShowing;
  }

  private void OpenVanillaInventoryMode()
  {
    if (Manager.ui == null)
    {
      return;
    }

    Manager.ui.OnPlayerInventoryOpen();
    _openedVanillaInventoryMode = true;

    if (!hideVanillaSidePanelsOnOpen)
    {
      return;
    }

    // Keep this deliberately defensive. These methods existed in the tested UIManager path, but UI state can vary.
    if (Manager.ui.characterWindow != null)
    {
      Manager.ui.characterWindow.Hide();
    }

    if (Manager.ui.activeCraftingUI != null)
    {
      Manager.ui.activeCraftingUI.HideCraftingUI();
    }
  }

  private static void CloseVanillaInventoryMode()
  {
    if (Manager.ui == null)
    {
      return;
    }

    Manager.ui.HideAllInventoryAndCraftingUI(true);
  }

  private static void SetLaneHover(LaneView lane, bool hovered)
  {
    if (lane?.FilterHighlight != null)
    {
      lane.FilterHighlight.SetActive(hovered);
    }
  }

  private static void SetSlotHover(SlotView slot, bool hovered)
  {
    if (slot?.HoverHighlight != null)
    {
      slot.HoverHighlight.SetActive(hovered);
    }
  }
}
