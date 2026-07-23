using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChunkLoaderToolkitMapControls
{
  private const float SoftCornerRadius = 7.0f;
  private const float PanelHeight = 174.0f;
  private static readonly Color EdgeColor =
      new Color(0.540f, 0.660f, 0.780f, 1.0f);
  private static readonly Color PanelBackground =
      new Color(0.055f, 0.078f, 0.120f, 0.965f);
  private static readonly Color ButtonBackground =
      new Color(0.075f, 0.125f, 0.160f, 0.980f);

  private GameObject _instance;
  private UIDocument _document;
  private VisualElement _root;
  private VisualElement _panel;
  private Button _claimButton;
  private Button _gridButton;
  private Button _managerButton;
  private bool _armed;
  private int _selectedCount;
  private int _queuedClaimCount;

  public bool IsCreated => _instance != null;

  public bool IsPointerOver
  {
    get
    {
      if (_document == null ||
          _root == null ||
          _root.panel == null ||
          _panel == null ||
          _root.resolvedStyle.display == DisplayStyle.None)
      {
        return false;
      }

      Vector2 mousePos = Input.mousePosition;
      Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
          _root.panel,
          new Vector2(mousePos.x, Screen.height - mousePos.y));
      VisualElement picked = _root.panel.Pick(panelPos);
      return picked != null &&
             picked != _root &&
             picked.pickingMode != PickingMode.Ignore;
    }
  }

  public bool IsCursorOverVisuals => IsMouseNearPanel(0.0f);

  public void Create(
      Action toggleGrid,
      Action showManager,
      Action claimSelection)
  {
    if (_instance != null)
    {
      return;
    }

    _instance = ChunkLoaderToolkitUi.CreateToolkitDocument(
        "ChunkLoaderMapControls",
        ChunkLoaderToolkitUi.ToolkitSortingOrder,
        out _document,
        out _root);
    if (_root == null)
    {
      throw new InvalidOperationException(
          "Chunk Loader map-controls UIDocument did not expose a root visual element.");
    }

    Build(toggleGrid, showManager, claimSelection);
    SetVisible(false);
    Debug.Log("[ChunkLoaderMod] UI Toolkit map controls created.");
  }

  public void SetVisible(bool visible)
  {
    if (_root == null)
    {
      return;
    }

    _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    _root.pickingMode = PickingMode.Ignore;
    if (_panel != null)
    {
      _panel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }

  }

  public void UpdateMapBounds(MapUI map)
  {
    if (_root == null ||
        _panel == null ||
        !ChunkLoaderToolkitUi.TryGetMapPanelRect(map, _root, out Rect mapRect))
    {
      return;
    }

    float padding = Mathf.Clamp(mapRect.width * 0.012f, 8.0f, 18.0f);
    float panelWidth = Mathf.Clamp(mapRect.width * 0.18f, 250.0f, 360.0f);
    float panelHeight = PanelHeight;

    _panel.style.left = mapRect.xMax - panelWidth - padding;
    _panel.style.top = mapRect.yMax - panelHeight - padding;
    _panel.style.width = panelWidth;
    _panel.style.height = panelHeight;
  }

  public void SetArmed(bool armed)
  {
    _armed = armed;
    SetButtonEnabled(_gridButton, armed);
    SetButtonEnabled(_managerButton, armed);
    RefreshClaimButton();
  }

  public void SetGridVisible(bool visible)
  {
    if (_gridButton != null)
    {
      _gridButton.text = visible
          ? "Chunk grid: On"
          : "Chunk grid: Off";
    }
  }

  public void SetSelectionState(
      int selectedCount,
      int queuedClaimCount)
  {
    _selectedCount = Mathf.Max(0, selectedCount);
    _queuedClaimCount = Mathf.Max(0, queuedClaimCount);
    RefreshClaimButton();
  }

  public void Destroy()
  {
    ChunkLoaderToolkitUi.DestroyToolkitDocument(_instance);
    _instance = null;
    _document = null;
    _root = null;
    _panel = null;
    _claimButton = null;
    _gridButton = null;
    _managerButton = null;
    _armed = false;
    _selectedCount = 0;
    _queuedClaimCount = 0;
  }

  private void Build(
      Action toggleGrid,
      Action showManager,
      Action claimSelection)
  {
    _root.Clear();
    _root.style.display = DisplayStyle.None;

    _panel = new VisualElement { name = "ChunkLoaderMapControlsPanel" };
    _panel.style.position = Position.Absolute;
    _panel.style.width = 330.0f;
    _panel.style.height = PanelHeight;
    _panel.style.paddingLeft = 10.0f;
    _panel.style.paddingRight = 10.0f;
    _panel.style.paddingTop = 10.0f;
    _panel.style.paddingBottom = 10.0f;
    _panel.style.flexDirection = FlexDirection.Column;
    _panel.style.backgroundColor = PanelBackground;
    ApplyBorder(_panel, EdgeColor, 3.0f);
    _root.Add(_panel);

    _claimButton = MakeButton(
        "Claim selection",
        () => claimSelection?.Invoke());
    _gridButton = MakeButton("Chunk grid: Off", () => toggleGrid?.Invoke());
    _managerButton = MakeButton("Loaded chunks", () => showManager?.Invoke());
    _claimButton.style.marginBottom = 10.0f;
    _gridButton.style.marginBottom = 8.0f;
    _panel.Add(_claimButton);
    _panel.Add(_gridButton);
    _panel.Add(_managerButton);
    RefreshClaimButton();
  }

  private void RefreshClaimButton()
  {
    if (_claimButton == null)
    {
      return;
    }

    if (_queuedClaimCount > 0)
    {
      _claimButton.text = $"Claiming {_queuedClaimCount}...";
    }
    else if (_selectedCount > 0)
    {
      _claimButton.text = $"Claim selection ({_selectedCount})";
    }
    else
    {
      _claimButton.text = "Claim selection";
    }

    SetButtonEnabled(
        _claimButton,
        _armed && _selectedCount > 0 && _queuedClaimCount == 0);
  }

  private bool IsMouseNearPanel(float padding)
  {
    if (_root == null ||
        _root.panel == null ||
        _panel == null ||
        _root.resolvedStyle.display == DisplayStyle.None)
    {
      return false;
    }

    Vector2 mousePos = Input.mousePosition;
    Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
        _root.panel,
        new Vector2(mousePos.x, Screen.height - mousePos.y));
    Rect rect = _panel.worldBound;
    rect.xMin -= padding;
    rect.xMax += padding;
    rect.yMin -= padding;
    rect.yMax += padding;
    return rect.Contains(panelPos);
  }

  private static Button MakeButton(string text, Action clicked)
  {
    Button button = new Button(clicked) { text = text };
    button.focusable = false;
    button.style.height = 42.0f;
    button.style.backgroundColor = ButtonBackground;
    button.style.color = ChunkLoaderToolkitUi.TextColor;
    button.style.fontSize = 13.0f;
    button.style.unityTextAlign = TextAnchor.MiddleCenter;
    button.style.unityFont = ChunkLoaderToolkitUi.PixelFont;
    button.style.borderTopWidth = 2.0f;
    button.style.borderRightWidth = 2.0f;
    button.style.borderBottomWidth = 2.0f;
    button.style.borderLeftWidth = 2.0f;
    Color edge = EdgeColor;
    button.style.borderTopColor = edge;
    button.style.borderRightColor = edge;
    button.style.borderBottomColor = edge;
    button.style.borderLeftColor = edge;
    button.style.borderTopLeftRadius = SoftCornerRadius;
    button.style.borderTopRightRadius = SoftCornerRadius;
    button.style.borderBottomRightRadius = SoftCornerRadius;
    button.style.borderBottomLeftRadius = SoftCornerRadius;
    Color hover = new Color(
        Mathf.Min(1.0f, ButtonBackground.r + 0.04f),
        Mathf.Min(1.0f, ButtonBackground.g + 0.06f),
        Mathf.Min(1.0f, ButtonBackground.b + 0.07f),
        ButtonBackground.a);
    button.RegisterCallback<PointerEnterEvent>(_ =>
    {
      if (button.enabledSelf)
      {
        button.style.backgroundColor = hover;
      }
    });
    button.RegisterCallback<PointerLeaveEvent>(_ =>
    {
      button.style.backgroundColor = ButtonBackground;
    });
    return button;
  }

  private static void SetButtonEnabled(Button button, bool enabled)
  {
    if (button == null)
    {
      return;
    }

    button.SetEnabled(enabled);
    button.style.opacity = enabled ? 1.0f : 0.42f;
  }

  private static void ApplyBorder(
      VisualElement element,
      Color color,
      float width)
  {
    element.style.borderTopColor = color;
    element.style.borderRightColor = color;
    element.style.borderBottomColor = color;
    element.style.borderLeftColor = color;
    element.style.borderTopWidth = width;
    element.style.borderRightWidth = width;
    element.style.borderBottomWidth = width;
    element.style.borderLeftWidth = width;
    element.style.borderTopLeftRadius = SoftCornerRadius;
    element.style.borderTopRightRadius = SoftCornerRadius;
    element.style.borderBottomRightRadius = SoftCornerRadius;
    element.style.borderBottomLeftRadius = SoftCornerRadius;
  }

}
