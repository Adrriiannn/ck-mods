using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChunkLoaderManagerPanel
{
  private const int RowsPerPage = 8;
  private const int TabsPerPage = 3;
  private const float RenameFieldFontSize = 22.0f;
  private const float SoftCornerRadius = 7.0f;
  private const float TabControlHeight = 34.0f;
  private const float TabMarqueeDelaySeconds = 1.0f;
  private const float TabMarqueePixelsPerSecond = 34.0f;
  private const float TabMarqueeEndPadding = 18.0f;
  private const float RowNameMarqueePixelsPerSecond = 38.0f;
  private const float RowNameMarqueeEndPadding = 24.0f;
  private const float DetailsScrollVerticalPadding = 24.0f;
  private static readonly Color PanelBackground =
      new Color(0.055f, 0.078f, 0.120f, 0.985f);
  private static readonly Color SectionBackground =
      new Color(0.035f, 0.053f, 0.086f, 0.985f);
  private static readonly Color HeaderBackground =
      new Color(0.070f, 0.115f, 0.155f, 0.985f);
  private static readonly Color RowBackground =
      new Color(0.045f, 0.066f, 0.098f, 0.985f);
  private static readonly Color RowAlternateBackground =
      new Color(0.052f, 0.076f, 0.112f, 0.985f);
  private static readonly Color RowSelectedBackground =
      new Color(0.170f, 0.365f, 0.455f, 0.985f);
  private static readonly Color RowHoverBackground =
      new Color(0.095f, 0.170f, 0.220f, 0.985f);
  private static readonly Color RedButtonBackground =
      new Color(0.330f, 0.070f, 0.060f, 0.980f);
  private static readonly Color GreenButtonBackground =
      new Color(0.065f, 0.215f, 0.095f, 0.980f);
  private static readonly Color ButtonBackground =
      new Color(0.075f, 0.125f, 0.160f, 0.980f);
  private static readonly Color EdgeColor =
      new Color(0.540f, 0.660f, 0.780f, 1.0f);
  private static readonly Color SoftEdgeColor =
      new Color(0.540f, 0.660f, 0.780f, 0.72f);
  private static readonly Color DividerColor =
      new Color(0.540f, 0.660f, 0.780f, 0.24f);
  private static readonly Color CheckboxBackground =
      new Color(0.020f, 0.034f, 0.052f, 0.96f);
  private static readonly Color CheckboxSelectedBackground =
      new Color(0.075f, 0.240f, 0.295f, 0.98f);

  private sealed class RowView
  {
    public ulong Id;
    public Button Root;
    public VisualElement CheckCell;
    public Label Check;
    public VisualElement NameCell;
    public VisualElement NameClip;
    public Label Name;
    public string NameText = string.Empty;
    public float NameMarqueeStartTime;
    public Label Center;
    public Label Status;
    public bool Hovered;
  }

  private sealed class TabView
  {
    public ulong Id;
    public VisualElement Root;
    public Button Focus;
    public VisualElement Clip;
    public Label Label;
    public Button Close;
    public string Title = string.Empty;
    public float MarqueeStartTime;
  }

  private readonly List<ChunkLoaderRegistrationRecord> _records = new();
  private readonly List<RowView> _rows = new();
  private readonly List<TabView> _tabViews = new();
  private readonly HashSet<ulong> _selected = new();
  private readonly ChunkLoaderToolkitSnapshot _snapshot =
      new ChunkLoaderToolkitSnapshot();

  private GameObject _instance;
  private UIDocument _document;
  private VisualElement _root;
  private VisualElement _backdrop;
  private VisualElement _content;
  private VisualElement _listSection;
  private VisualElement _detailsSection;
  private VisualElement _snapshotFrame;
  private ScrollView _detailsScroll;
  private VisualElement _renameRow;
  private VisualElement _rowContainer;
  private VisualElement _tabContainer;
  private VisualElement _overviewRow;
  private VisualElement _summaryPanel;
  private VisualElement _detailsContent;
  private UnityEngine.UIElements.Image _snapshotImage;
  private Label _summaryDetails;
  private VisualElement _selectHeader;
  private Button _selectAllButton;
  private Label _selectAllCheck;
  private Label _selectAllText;
  private Label _nameHeader;
  private Label _positionHeader;
  private Label _statusHeader;
  private Label _quota;
  private Label _pageText;
  private Label _emptyText;
  private Button _viewButton;
  private Button _enableButton;
  private Button _deleteButton;
  private Button _renameButton;
  private Button _addButton;
  private Button _previousButton;
  private Button _nextButton;
  private Button _refreshDetailsButton;
  private Button _tabPreviousButton;
  private Button _tabNextButton;
  private TextField _renameField;
  private MapUI _map;
  private bool _showing;
  private bool _renaming;
  private bool _vanillaTextInputActive;
  private bool _mapViewInputSuspended;
  private bool _detailsScrollDragging;
  private int _detailsScrollWheelFrame = -1;
  private float _detailsScrollLastPointerY;
  private float _detailsLogFontSize = 15.0f;
  private string _detailsText = string.Empty;
  private ulong _focusedId;
  private ulong _lastDetailsRequestId;
  private ulong _liveDetailsFocusedId;
  private double _nextLiveDetailsRequestAt;
  private int _page;
  private int _tabPage;

  public bool IsShowing => _showing;

  public bool IsPointerOver
  {
    get
    {
      return IsMouseNearBackdrop(0.0f);
    }
  }

  public bool IsCursorOverVisuals => IsMouseNearBackdrop(0.0f);

  public bool EnsureCreated(MapUI map)
  {
    if (_instance != null)
    {
      return true;
    }
    if (map == null || !ChunkLoaderToolkitUi.IsReady)
    {
      return false;
    }

    try
    {
      _map = map;
      _instance = ChunkLoaderToolkitUi.CreateToolkitDocument(
          "ChunkLoaderManager",
          ChunkLoaderToolkitUi.ToolkitSortingOrder,
          out _document,
          out _root);
      if (_root == null)
      {
        return false;
      }

      BuildUi();
      SetVisible(false);
      Debug.Log("[ChunkLoaderMod] UI Toolkit manager panel created.");

      ChunkLoaderNetworkState.RegistryChanged += Refresh;
      ChunkLoaderNetworkState.MutationCompleted += OnMutationCompleted;
      ChunkLoaderDetailsState.DetailsChanged += OnDetailsChanged;
      return true;
    }
    catch (Exception ex)
    {
      CleanupFailedCreation();
      Debug.LogError(
          $"[ChunkLoaderMod] Failed to create the UI Toolkit manager panel. {ex}");
      return false;
    }
  }

  public void Show()
  {
    if (_instance == null)
    {
      EnsureCreated(Manager.ui != null ? Manager.ui.mapUI : null);
    }
    if (_instance == null || _map == null || !_map.IsShowingBigMap)
    {
      return;
    }

    ChunkLoaderNetworkState.RequestSnapshot(force: true);
    UpdateMapBounds(_map);
    SetVisible(true);
    SuspendMapViewInput();
    Refresh();
  }

  public void Hide()
  {
    CancelRename();
    ResumeMapViewInput();
    SetVisible(false);
  }

  public void Update()
  {
    if (!_showing)
    {
      return;
    }

    UpdateTabMarquees();
    UpdateRowNameMarquees();
    ClampDetailsScrollToContent();

    if (_renaming)
    {
      if (Input.GetKeyDown(KeyCode.Escape))
      {
        CancelRename();
      }
      else if (Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter))
      {
        CommitRenameField();
      }
      return;
    }

    HandleDetailsScrollInput();

    if (Input.GetKeyDown(KeyCode.Escape) ||
        Input.GetKeyDown(KeyCode.Tab))
    {
      Hide();
      return;
    }

    UpdateLiveDetailsFeed();
  }

  public void UpdateMapBounds(MapUI map)
  {
    if (_root == null ||
        _backdrop == null ||
        !ChunkLoaderToolkitUi.TryGetMapPanelRect(map, _root, out Rect mapRect))
    {
      return;
    }

    float marginX = Mathf.Clamp(mapRect.width * 0.018f, 10.0f, 30.0f);
    float marginY = Mathf.Clamp(mapRect.height * 0.018f, 8.0f, 24.0f);
    float width = Mathf.Max(1.0f, mapRect.width - marginX * 2.0f);
    float height = Mathf.Max(1.0f, mapRect.height - marginY * 2.0f);

    _backdrop.style.left = mapRect.xMin + marginX;
    _backdrop.style.top = mapRect.yMin + marginY;
    _backdrop.style.width = width;
    _backdrop.style.height = height;

    UpdateResponsiveSizing(width, height);
  }

  public void FocusRegistration(ulong registrationId)
  {
    _selected.Clear();
    _selected.Add(registrationId);
    _focusedId = registrationId;
    Show();
  }

  public void Destroy()
  {
    ReleaseVanillaTextInput();
    ResumeMapViewInput();
    ChunkLoaderNetworkState.RegistryChanged -= Refresh;
    ChunkLoaderNetworkState.MutationCompleted -= OnMutationCompleted;
    ChunkLoaderDetailsState.DetailsChanged -= OnDetailsChanged;
    _snapshot.Destroy();
    ChunkLoaderToolkitUi.DestroyToolkitDocument(_instance);

    _instance = null;
    _document = null;
    _root = null;
    _backdrop = null;
    _content = null;
    _listSection = null;
    _detailsSection = null;
    _snapshotFrame = null;
    _detailsScroll = null;
    _renameRow = null;
    _rowContainer = null;
    _tabContainer = null;
    _overviewRow = null;
    _summaryPanel = null;
    _detailsContent = null;
    _snapshotImage = null;
    _summaryDetails = null;
    _selectHeader = null;
    _selectAllButton = null;
    _selectAllCheck = null;
    _selectAllText = null;
    _nameHeader = null;
    _positionHeader = null;
    _statusHeader = null;
    _quota = null;
    _pageText = null;
    _emptyText = null;
    _viewButton = null;
    _enableButton = null;
    _deleteButton = null;
    _renameButton = null;
    _addButton = null;
    _previousButton = null;
    _nextButton = null;
    _refreshDetailsButton = null;
    _tabPreviousButton = null;
    _tabNextButton = null;
    _renameField = null;
    _map = null;
    _rows.Clear();
    _tabViews.Clear();
    _records.Clear();
    _selected.Clear();
    _focusedId = 0;
    _lastDetailsRequestId = 0;
    _liveDetailsFocusedId = 0;
    _nextLiveDetailsRequestAt = 0.0d;
    _page = 0;
    _tabPage = 0;
    _renaming = false;
    _detailsScrollDragging = false;
    _detailsScrollWheelFrame = -1;
    _detailsScrollLastPointerY = 0.0f;
    _detailsText = string.Empty;
    _showing = false;
  }

  private void BuildUi()
  {
    _root.Clear();
    _root.style.display = DisplayStyle.None;

    _backdrop = new VisualElement { name = "ChunkLoaderManagerBackdrop" };
    _backdrop.style.position = Position.Absolute;
    _backdrop.style.left = 0.0f;
    _backdrop.style.top = 0.0f;
    _backdrop.style.width = 1200.0f;
    _backdrop.style.height = 760.0f;
    _backdrop.style.flexDirection = FlexDirection.Column;
    _backdrop.style.backgroundColor = PanelBackground;
    _backdrop.style.paddingLeft = 14.0f;
    _backdrop.style.paddingRight = 14.0f;
    _backdrop.style.paddingTop = 14.0f;
    _backdrop.style.paddingBottom = 14.0f;
    _backdrop.pickingMode = PickingMode.Position;
    ApplyBorder(_backdrop, EdgeColor, 3.0f);
    ApplyCornerRadius(_backdrop, SoftCornerRadius + 2.0f);
    _backdrop.RegisterCallback<WheelEvent>(ConsumeUiEvent);
    _backdrop.RegisterCallback<PointerDownEvent>(ConsumeUiEvent);
    _backdrop.RegisterCallback<PointerMoveEvent>(ConsumeUiEvent);
    _backdrop.RegisterCallback<PointerUpEvent>(ConsumeUiEvent);
    _backdrop.RegisterCallback<PointerCancelEvent>(ConsumeUiEvent);
    _backdrop.RegisterCallback<KeyDownEvent>(ConsumeUiEvent);
    _root.Add(_backdrop);

    VisualElement titleBar = MakeSection();
    titleBar.style.height = 58.0f;
    titleBar.style.flexShrink = 0.0f;
    titleBar.style.flexDirection = FlexDirection.Row;
    titleBar.style.alignItems = Align.Center;
    titleBar.style.justifyContent = Justify.Center;
    titleBar.style.marginBottom = 10.0f;
    Label title = MakeLabel("LOADED CHUNKS", 21.0f, pixel: true);
    titleBar.Add(title);
    Button close = MakeButton("X", Hide, RedButtonBackground, pixel: true);
    close.style.position = Position.Absolute;
    close.style.right = 10.0f;
    close.style.width = 30.0f;
    close.style.height = 26.0f;
    titleBar.Add(close);
    _backdrop.Add(titleBar);

    _content = new VisualElement { name = "Content" };
    _content.style.flexDirection = FlexDirection.Row;
    _content.style.flexGrow = 1.0f;
    _content.style.flexShrink = 1.0f;
    _content.style.minHeight = 0.0f;
    _content.style.overflow = Overflow.Hidden;
    _content.style.marginBottom = 10.0f;
    _backdrop.Add(_content);

    BuildListSection(_content);
    BuildDetailsSection(_content);
    BuildFooter();
  }

  private bool IsMouseNearBackdrop(float padding)
  {
    if (_root == null ||
        _root.panel == null ||
        _backdrop == null ||
        !_showing ||
        _root.resolvedStyle.display == DisplayStyle.None)
    {
      return false;
    }

    Vector2 mousePos = Input.mousePosition;
    Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
        _root.panel,
        new Vector2(mousePos.x, Screen.height - mousePos.y));
    Rect rect = _backdrop.worldBound;
    rect.xMin -= padding;
    rect.xMax += padding;
    rect.yMin -= padding;
    rect.yMax += padding;
    return rect.Contains(panelPos);
  }

  private bool IsMouseOverElement(VisualElement element, float padding = 0.0f)
  {
    if (_root == null ||
        _root.panel == null ||
        element == null ||
        !_showing ||
        _root.resolvedStyle.display == DisplayStyle.None)
    {
      return false;
    }

    Vector2 mousePos = Input.mousePosition;
    Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
        _root.panel,
        new Vector2(mousePos.x, Screen.height - mousePos.y));
    Rect rect = element.worldBound;
    rect.xMin -= padding;
    rect.xMax += padding;
    rect.yMin -= padding;
    rect.yMax += padding;
    return rect.Contains(panelPos);
  }

  private void HandleDetailsScrollInput()
  {
    if (_detailsScroll == null ||
        !IsMouseOverDetailsScrollArea())
    {
      return;
    }

    float wheel = Input.mouseScrollDelta.y;
    if (Mathf.Abs(wheel) < 0.01f ||
        _detailsScrollWheelFrame == Time.frameCount)
    {
      return;
    }

    _detailsScrollWheelFrame = Time.frameCount;
    MoveDetailsScroll(-wheel * 54.0f);
  }

  private void HandleDetailsScrollWheel(WheelEvent evt)
  {
    float delta = evt != null ? evt.delta.y : 0.0f;
    if (Mathf.Abs(delta) > 0.01f &&
        _detailsScrollWheelFrame != Time.frameCount)
    {
      _detailsScrollWheelFrame = Time.frameCount;
      MoveDetailsScroll(delta * 18.0f);
    }
    ConsumeUiEvent(evt);
  }

  private void HandleDetailsPointerDown(PointerDownEvent evt)
  {
    if (IsDetailsScrollerEvent(evt))
    {
      return;
    }

    _detailsScrollDragging = true;
    _detailsScrollLastPointerY =
        evt != null ? evt.position.y : Input.mousePosition.y;
    ConsumeUiEvent(evt);
  }

  private void HandleDetailsPointerMove(PointerMoveEvent evt)
  {
    if (IsDetailsScrollerEvent(evt))
    {
      return;
    }

    if (_detailsScrollDragging)
    {
      float pointerY = evt != null ? evt.position.y : Input.mousePosition.y;
      MoveDetailsScroll(_detailsScrollLastPointerY - pointerY);
      _detailsScrollLastPointerY = pointerY;
    }

    ConsumeUiEvent(evt);
  }

  private void HandleDetailsPointerUp(PointerUpEvent evt)
  {
    if (IsDetailsScrollerEvent(evt))
    {
      return;
    }

    _detailsScrollDragging = false;
    ConsumeUiEvent(evt);
  }

  private void HandleDetailsPointerCancel(PointerCancelEvent evt)
  {
    if (IsDetailsScrollerEvent(evt))
    {
      return;
    }

    _detailsScrollDragging = false;
    ConsumeUiEvent(evt);
  }

  private bool IsDetailsScrollerEvent(EventBase evt)
  {
    if (evt == null ||
        _detailsScroll == null ||
        _detailsScroll.verticalScroller == null)
    {
      return false;
    }

    VisualElement current = evt.target as VisualElement;
    while (current != null)
    {
      if (current == _detailsScroll.verticalScroller)
      {
        return true;
      }

      current = current.parent;
    }

    return false;
  }

  private void MoveDetailsScroll(float delta)
  {
    if (_detailsScroll == null)
    {
      return;
    }

    Vector2 offset = _detailsScroll.scrollOffset;
    _detailsScroll.scrollOffset = new Vector2(
        offset.x,
        Mathf.Clamp(offset.y + delta, 0.0f, GetDetailsMaxScroll()));
  }

  private float GetDetailsMaxScroll()
  {
    if (_detailsScroll == null)
    {
      return 0.0f;
    }

    float viewportHeight = _detailsScroll.resolvedStyle.height;
    VisualElement viewport = _detailsScroll.Q("unity-content-viewport");
    if (viewport != null &&
        !float.IsNaN(viewport.resolvedStyle.height) &&
        viewport.resolvedStyle.height > 0.0f)
    {
      viewportHeight = viewport.resolvedStyle.height;
    }

    float contentHeight = 0.0f;
    VisualElement container = _detailsScroll.Q("unity-content-container") ??
                              _detailsScroll.contentContainer;
    if (container != null &&
        !float.IsNaN(container.resolvedStyle.height) &&
        container.resolvedStyle.height > 0.0f)
    {
      contentHeight = container.resolvedStyle.height;
    }
    if (_detailsContent != null &&
        !float.IsNaN(_detailsContent.resolvedStyle.height) &&
        _detailsContent.resolvedStyle.height > 0.0f)
    {
      contentHeight = Mathf.Max(
          contentHeight,
          _detailsContent.resolvedStyle.height + DetailsScrollVerticalPadding);
    }

    if (float.IsNaN(viewportHeight) ||
        viewportHeight <= 0.0f ||
        float.IsNaN(contentHeight) ||
        contentHeight <= viewportHeight)
    {
      return 0.0f;
    }

    return Mathf.Max(0.0f, contentHeight - viewportHeight);
  }

  private void ClampDetailsScrollToContent()
  {
    if (_detailsScroll == null)
    {
      return;
    }

    Vector2 offset = _detailsScroll.scrollOffset;
    float maxScroll = GetDetailsMaxScroll();
    float y = Mathf.Clamp(offset.y, 0.0f, maxScroll);
    if (Mathf.Abs(y - offset.y) > 0.01f)
    {
      _detailsScroll.scrollOffset = new Vector2(offset.x, y);
    }
  }

  private bool IsMouseOverDetailsScrollArea()
  {
    return IsMouseOverElement(_detailsScroll) ||
           IsMouseOverElement(_detailsContent) ||
           IsMouseOverElement(_detailsSection);
  }

  private static void ConsumeUiEvent(EventBase evt)
  {
    if (evt == null)
    {
      return;
    }

    evt.StopImmediatePropagation();
  }

  private void BuildListSection(VisualElement parent)
  {
    VisualElement list = MakeSection();
    _listSection = list;
    list.name = "ChunkRegistrations";
    list.style.flexGrow = 1.0f;
    list.style.flexShrink = 1.0f;
    list.style.minHeight = 0.0f;
    list.style.marginRight = 14.0f;
    list.style.paddingLeft = 12.0f;
    list.style.paddingRight = 12.0f;
    list.style.paddingTop = 12.0f;
    list.style.paddingBottom = 12.0f;
    parent.Add(list);

    VisualElement headerLine = new VisualElement();
    headerLine.style.flexDirection = FlexDirection.Row;
    headerLine.style.alignItems = Align.Center;
    headerLine.style.justifyContent = Justify.FlexEnd;
    headerLine.style.marginBottom = 10.0f;
    list.Add(headerLine);

    _viewButton = MakeButton("View", ViewOnMap, ButtonBackground);
    _enableButton = MakeButton("Disable", ToggleSelectedEnabled, GreenButtonBackground);
    _renameButton = MakeButton("Rename", BeginRename, ButtonBackground);
    _deleteButton = MakeButton("Delete", DeleteSelected, RedButtonBackground);
    AddActionButton(headerLine, _viewButton);
    AddActionButton(headerLine, _enableButton);
    AddActionButton(headerLine, _renameButton);
    AddActionButton(headerLine, _deleteButton);

    _renameRow = new VisualElement { name = "RenameRow" };
    _renameRow.style.display = DisplayStyle.None;
    _renameRow.style.flexDirection = FlexDirection.Row;
    _renameRow.style.alignItems = Align.Center;
    _renameRow.style.marginBottom = 8.0f;
    _renameRow.style.paddingLeft = 8.0f;
    _renameRow.style.paddingRight = 8.0f;
    _renameRow.style.paddingTop = 3.0f;
    _renameRow.style.paddingBottom = 3.0f;
    _renameRow.style.backgroundColor = new Color(0.07f, 0.20f, 0.23f, 0.96f);
    ApplyBorder(_renameRow, SoftEdgeColor, 2.0f);
    _renameField = new TextField();
    _renameField.style.flexGrow = 1.0f;
    _renameField.style.marginRight = 8.0f;
    _renameField.style.height = 38.0f;
    StyleRenameTextField(_renameField);
    _renameField.RegisterCallback<AttachToPanelEvent>(
        _ => StyleRenameTextField(_renameField));
    _renameField.RegisterCallback<KeyDownEvent>(evt =>
    {
      if (evt.keyCode == KeyCode.Return ||
          evt.keyCode == KeyCode.KeypadEnter)
      {
        CommitRenameField();
        ConsumeUiEvent(evt);
      }
      else if (evt.keyCode == KeyCode.Escape)
      {
        CancelRename();
        ConsumeUiEvent(evt);
      }
      else
      {
        ConsumeUiEvent(evt);
      }
    });
    Button renameOk = MakeButton("Apply", CommitRenameField, GreenButtonBackground);
    renameOk.style.width = 96.0f;
    Button renameCancel = MakeButton("Cancel", CancelRename, ButtonBackground);
    renameCancel.style.width = 104.0f;
    renameCancel.style.marginLeft = 8.0f;
    _renameRow.Add(_renameField);
    _renameRow.Add(renameOk);
    _renameRow.Add(renameCancel);
    list.Add(_renameRow);

    VisualElement columns = MakeRowShell(HeaderBackground);
    columns.style.height = 50.0f;
    columns.style.marginBottom = 8.0f;
    _selectHeader = MakeSelectAllHeader();
    _nameHeader = MakeColumnLabel("Chunk Name", 0.0f, flex: 1.0f, header: true);
    _positionHeader = MakeColumnLabel("Chunk Position", 270.0f, center: true, header: true);
    _statusHeader = MakeColumnLabel("Chunk Status", 370.0f, center: true, header: true, last: true);
    columns.Add(_selectHeader);
    columns.Add(_nameHeader);
    columns.Add(_positionHeader);
    columns.Add(_statusHeader);
    list.Add(columns);

    _rowContainer = new VisualElement { name = "Rows" };
    _rowContainer.style.flexGrow = 1.0f;
    _rowContainer.style.flexShrink = 1.0f;
    _rowContainer.style.minHeight = 0.0f;
    _rowContainer.style.overflow = Overflow.Hidden;
    _rowContainer.style.flexDirection = FlexDirection.Column;
    list.Add(_rowContainer);

    for (int i = 0; i < RowsPerPage; i++)
    {
      RowView row = CreateRow(i);
      _rows.Add(row);
      _rowContainer.Add(row.Root);
    }

    _emptyText = MakeLabel(
        "No chunks have been registered yet.",
        14.0f,
        pixel: true,
        color: ChunkLoaderToolkitUi.MutedTextColor);
    _emptyText.style.unityTextAlign = TextAnchor.MiddleCenter;
    _emptyText.style.flexGrow = 1.0f;
    _emptyText.style.display = DisplayStyle.None;
    _rowContainer.Add(_emptyText);

    VisualElement pager = new VisualElement();
    pager.style.flexDirection = FlexDirection.Row;
    pager.style.alignItems = Align.Center;
    pager.style.marginTop = 10.0f;
    list.Add(pager);
    _previousButton = MakeButton("<", PreviousPage, ButtonBackground, pixel: true);
    _nextButton = MakeButton(">", NextPage, ButtonBackground, pixel: true);
    _previousButton.style.width = 62.0f;
    _nextButton.style.width = 62.0f;
    _pageText = MakeLabel("1 / 1", 13.0f, pixel: true);
    _pageText.style.width = 120.0f;
    _pageText.style.unityTextAlign = TextAnchor.MiddleCenter;
    pager.Add(_previousButton);
    pager.Add(_pageText);
    pager.Add(_nextButton);
  }

  private void BuildDetailsSection(VisualElement parent)
  {
    VisualElement details = MakeSection();
    _detailsSection = details;
    details.name = "SelectedChunk";
    details.style.width = 720.0f;
    details.style.flexDirection = FlexDirection.Column;
    details.style.flexShrink = 0.0f;
    details.style.minHeight = 0.0f;
    details.style.overflow = Overflow.Hidden;
    details.style.paddingLeft = 18.0f;
    details.style.paddingRight = 18.0f;
    details.style.paddingTop = 18.0f;
    details.style.paddingBottom = 18.0f;
    parent.Add(details);

    Label heading = MakeLabel("SELECTED CHUNK", 18.0f, pixel: true);
    heading.style.marginBottom = 12.0f;
    heading.style.flexShrink = 0.0f;
    details.Add(heading);

    _tabContainer = new VisualElement { name = "Tabs" };
    _tabContainer.style.flexDirection = FlexDirection.Row;
    _tabContainer.style.height = TabControlHeight;
    _tabContainer.style.minHeight = TabControlHeight;
    _tabContainer.style.marginBottom = 10.0f;
    _tabContainer.style.flexShrink = 0.0f;
    details.Add(_tabContainer);
    _tabPreviousButton = MakeButton("<", () =>
    {
      if (_tabPage > 0)
      {
        _tabPage--;
        RefreshTabs();
      }
    }, ButtonBackground, pixel: true);
    _tabNextButton = MakeButton(">", () =>
    {
      if ((_tabPage + 1) * TabsPerPage < _selected.Count)
      {
        _tabPage++;
        RefreshTabs();
      }
    }, ButtonBackground, pixel: true);
    _tabPreviousButton.style.width = 42.0f;
    _tabNextButton.style.width = 42.0f;
    _tabPreviousButton.style.marginRight = 6.0f;
    _tabNextButton.style.marginLeft = 6.0f;
    _tabContainer.Add(_tabPreviousButton);
    for (int i = 0; i < TabsPerPage; i++)
    {
      TabView tab = CreateTab();
      _tabViews.Add(tab);
      _tabContainer.Add(tab.Root);
    }
    _tabContainer.Add(_tabNextButton);

    VisualElement overviewRow = new VisualElement { name = "SelectedChunkOverview" };
    _overviewRow = overviewRow;
    overviewRow.style.flexDirection = FlexDirection.Row;
    overviewRow.style.alignItems = Align.FlexStart;
    overviewRow.style.flexShrink = 0.0f;
    overviewRow.style.marginBottom = 14.0f;
    overviewRow.style.overflow = Overflow.Hidden;
    details.Add(overviewRow);

    VisualElement snapshotColumn = new VisualElement { name = "SnapshotColumn" };
    snapshotColumn.style.flexDirection = FlexDirection.Column;
    snapshotColumn.style.flexShrink = 0.0f;
    snapshotColumn.style.marginRight = 16.0f;
    overviewRow.Add(snapshotColumn);

    _snapshotFrame = new VisualElement();
    _snapshotFrame.style.alignSelf = Align.FlexStart;
    _snapshotFrame.style.width = 280.0f;
    _snapshotFrame.style.height = 280.0f;
    _snapshotFrame.style.flexShrink = 0.0f;
    _snapshotFrame.style.backgroundColor = new Color(0.01f, 0.035f, 0.045f, 1.0f);
    ApplyBorder(_snapshotFrame, EdgeColor, 3.0f);
    _snapshotImage = new UnityEngine.UIElements.Image();
    _snapshotImage.scaleMode = ScaleMode.ScaleToFit;
    _snapshotImage.style.flexGrow = 1.0f;
    _snapshotImage.style.width = Length.Percent(100);
    _snapshotImage.style.height = Length.Percent(100);
    _snapshotFrame.Add(_snapshotImage);
    snapshotColumn.Add(_snapshotFrame);

    _refreshDetailsButton = MakeButton(
        "Refresh snapshot",
        RefreshFocusedDetails,
        ButtonBackground);
    _refreshDetailsButton.style.alignSelf = Align.FlexStart;
    _refreshDetailsButton.style.width = 280.0f;
    _refreshDetailsButton.style.marginTop = 6.0f;
    _refreshDetailsButton.style.flexShrink = 0.0f;
    snapshotColumn.Add(_refreshDetailsButton);

    VisualElement summaryPanel = new VisualElement { name = "ChunkSummary" };
    _summaryPanel = summaryPanel;
    summaryPanel.style.flexGrow = 1.0f;
    summaryPanel.style.flexShrink = 1.0f;
    summaryPanel.style.minWidth = 150.0f;
    summaryPanel.style.backgroundColor = Color.clear;
    summaryPanel.style.paddingLeft = 14.0f;
    summaryPanel.style.paddingRight = 14.0f;
    summaryPanel.style.paddingTop = 12.0f;
    summaryPanel.style.paddingBottom = 12.0f;
    summaryPanel.style.overflow = Overflow.Hidden;
    ApplyCornerRadius(summaryPanel, SoftCornerRadius);
    overviewRow.Add(summaryPanel);

    _summaryDetails = MakeLabel(
        "Select a chunk from the list to inspect its live state.",
        16.0f,
        pixel: false);
    _summaryDetails.style.whiteSpace = WhiteSpace.Normal;
    _summaryDetails.style.unityTextAlign = TextAnchor.UpperLeft;
    summaryPanel.Add(_summaryDetails);

    _detailsScroll = new ScrollView(ScrollViewMode.Vertical);
    _detailsScroll.name = "RecentActivityScroll";
    _detailsScroll.pickingMode = PickingMode.Position;
    _detailsScroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
    _detailsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
    _detailsScroll.style.flexGrow = 1.0f;
    _detailsScroll.style.flexShrink = 1.0f;
    _detailsScroll.style.minHeight = 120.0f;
    _detailsScroll.style.backgroundColor = Color.clear;
    _detailsScroll.style.overflow = Overflow.Hidden;
    ApplyCornerRadius(_detailsScroll, SoftCornerRadius);
    _detailsScroll.contentContainer.pickingMode = PickingMode.Position;
    _detailsScroll.contentContainer.style.backgroundColor = Color.clear;
    _detailsScroll.contentContainer.style.paddingLeft = 14.0f;
    _detailsScroll.contentContainer.style.paddingRight = 14.0f;
    _detailsScroll.contentContainer.style.paddingTop =
        DetailsScrollVerticalPadding * 0.5f;
    _detailsScroll.contentContainer.style.paddingBottom =
        DetailsScrollVerticalPadding * 0.5f;
    _detailsScroll.contentContainer.style.flexDirection = FlexDirection.Column;
    _detailsScroll.contentContainer.style.flexGrow = 0.0f;
    _detailsScroll.contentContainer.style.flexShrink = 0.0f;
    _detailsScroll.contentContainer.style.overflow = Overflow.Visible;

    _detailsContent = new VisualElement { name = "RecentActivityContent" };
    _detailsContent.pickingMode = PickingMode.Position;
    _detailsContent.style.flexDirection = FlexDirection.Column;
    _detailsContent.style.flexGrow = 0.0f;
    _detailsContent.style.flexShrink = 0.0f;
    _detailsContent.style.width = Length.Percent(100.0f);
    _detailsContent.style.backgroundColor = Color.clear;
    _detailsContent.style.overflow = Overflow.Visible;
    _detailsScroll.contentContainer.RegisterCallback<WheelEvent>(
        HandleDetailsScrollWheel,
        TrickleDown.TrickleDown);
    _detailsScroll.contentContainer.RegisterCallback<PointerDownEvent>(
        HandleDetailsPointerDown);
    _detailsScroll.contentContainer.RegisterCallback<PointerMoveEvent>(
        HandleDetailsPointerMove);
    _detailsScroll.contentContainer.RegisterCallback<PointerUpEvent>(
        HandleDetailsPointerUp);
    _detailsScroll.contentContainer.RegisterCallback<PointerCancelEvent>(
        HandleDetailsPointerCancel);
    _detailsScroll.RegisterCallback<WheelEvent>(
        HandleDetailsScrollWheel,
        TrickleDown.TrickleDown);
    _detailsScroll.RegisterCallback<PointerDownEvent>(HandleDetailsPointerDown);
    _detailsScroll.RegisterCallback<PointerMoveEvent>(HandleDetailsPointerMove);
    _detailsScroll.RegisterCallback<PointerUpEvent>(HandleDetailsPointerUp);
    _detailsScroll.RegisterCallback<PointerCancelEvent>(
        HandleDetailsPointerCancel);
    _detailsScroll.RegisterCallback<AttachToPanelEvent>(
        _ => StyleDetailsScrollTransparency());
    _detailsContent.RegisterCallback<WheelEvent>(
        HandleDetailsScrollWheel,
        TrickleDown.TrickleDown);
    _detailsContent.RegisterCallback<PointerDownEvent>(HandleDetailsPointerDown);
    _detailsContent.RegisterCallback<PointerMoveEvent>(HandleDetailsPointerMove);
    _detailsContent.RegisterCallback<PointerUpEvent>(HandleDetailsPointerUp);
    _detailsContent.RegisterCallback<PointerCancelEvent>(
        HandleDetailsPointerCancel);
    _detailsScroll.Add(_detailsContent);
    details.Add(_detailsScroll);
    SetDetailsText("Select a chunk from the list to inspect recent activity.");
  }

  private void StyleDetailsScrollTransparency()
  {
    if (_detailsScroll == null)
    {
      return;
    }

    _detailsScroll.style.backgroundColor = Color.clear;
    _detailsScroll.contentContainer.style.backgroundColor = Color.clear;

    VisualElement viewport = _detailsScroll.Q("unity-content-viewport");
    if (viewport != null)
    {
      viewport.pickingMode = PickingMode.Position;
      viewport.style.backgroundColor = Color.clear;
      viewport.RegisterCallback<WheelEvent>(
          HandleDetailsScrollWheel,
          TrickleDown.TrickleDown);
      viewport.RegisterCallback<PointerDownEvent>(HandleDetailsPointerDown);
      viewport.RegisterCallback<PointerMoveEvent>(HandleDetailsPointerMove);
      viewport.RegisterCallback<PointerUpEvent>(HandleDetailsPointerUp);
      viewport.RegisterCallback<PointerCancelEvent>(HandleDetailsPointerCancel);
    }

    VisualElement container = _detailsScroll.Q("unity-content-container");
    if (container != null)
    {
      container.pickingMode = PickingMode.Position;
      container.style.backgroundColor = Color.clear;
    }

    Scroller scroller = _detailsScroll.verticalScroller;
    if (scroller != null)
    {
      scroller.pickingMode = PickingMode.Position;
      scroller.style.width = 12.0f;
      scroller.style.backgroundColor =
          new Color(0.020f, 0.035f, 0.052f, 0.72f);
      ApplyCornerRadius(scroller, SoftCornerRadius);
    }
  }

  private void SetDetailsText(string text)
  {
    if (_detailsContent == null)
    {
      return;
    }

    text ??= string.Empty;
    if (string.Equals(_detailsText, text, StringComparison.Ordinal))
    {
      ScheduleDetailsScrollClamp(_detailsScroll != null
          ? _detailsScroll.scrollOffset.y
          : 0.0f);
      return;
    }

    float previousOffset = _detailsScroll != null
        ? _detailsScroll.scrollOffset.y
        : 0.0f;
    _detailsText = text;
    _detailsContent.Clear();

    string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
    string[] lines = normalized.Split('\n');
    if (lines.Length == 0)
    {
      lines = new[] { string.Empty };
    }

    for (int i = 0; i < lines.Length; i++)
    {
      Label line = MakeLabel(
          string.IsNullOrEmpty(lines[i]) ? " " : lines[i],
          _detailsLogFontSize,
          pixel: false);
      line.name = "RecentActivityLine";
      line.pickingMode = PickingMode.Ignore;
      line.style.flexGrow = 0.0f;
      line.style.flexShrink = 0.0f;
      line.style.width = Length.Percent(100.0f);
      line.style.whiteSpace = WhiteSpace.Normal;
      line.style.overflow = Overflow.Visible;
      line.style.unityTextAlign = TextAnchor.UpperLeft;
      line.style.marginBottom = 1.0f;
      _detailsContent.Add(line);
    }

    if (_detailsScroll != null)
    {
      _detailsScroll.contentContainer.style.minHeight = 0.0f;
      _detailsScroll.contentContainer.style.height = StyleKeyword.Auto;
      ScheduleDetailsScrollClamp(previousOffset);
    }
  }

  private void ScheduleDetailsScrollClamp(float requestedOffset)
  {
    if (_detailsScroll == null)
    {
      return;
    }

    _detailsScroll.schedule.Execute(() =>
    {
      if (_detailsScroll == null)
      {
        return;
      }

      _detailsScroll.scrollOffset = new Vector2(
          _detailsScroll.scrollOffset.x,
          Mathf.Clamp(requestedOffset, 0.0f, GetDetailsMaxScroll()));
    }).ExecuteLater(0);
  }

  private void BuildFooter()
  {
    VisualElement footer = MakeSection();
    footer.style.height = 52.0f;
    footer.style.flexShrink = 0.0f;
    footer.style.flexDirection = FlexDirection.Row;
    footer.style.alignItems = Align.Center;
    footer.style.paddingLeft = 14.0f;
    footer.style.paddingRight = 14.0f;
    _backdrop.Add(footer);

    _quota = MakeLabel("Personal: 0/0     World: 0/0", 12.0f, pixel: true);
    _quota.style.flexGrow = 1.0f;
    footer.Add(_quota);

    _addButton = MakeButton("+ Load a new chunk", LoadNewChunk, new Color(0.04f, 0.26f, 0.24f, 0.98f));
    _addButton.style.width = 430.0f;
    _addButton.style.height = 30.0f;
    footer.Add(_addButton);
  }

  private void UpdateResponsiveSizing(
      float panelWidth,
      float panelHeight)
  {
    if (_detailsSection == null)
    {
      return;
    }

    float contentWidth = Mathf.Max(1.0f, panelWidth - 28.0f);
    float detailsWidth;
    if (contentWidth >= 1500.0f)
    {
      detailsWidth = Mathf.Clamp(contentWidth * 0.34f, 520.0f, 720.0f);
    }
    else if (contentWidth >= 1100.0f)
    {
      detailsWidth = Mathf.Clamp(contentWidth * 0.34f, 390.0f, 560.0f);
    }
    else
    {
      float minDetailsWidth = contentWidth < 820.0f ? 240.0f : 320.0f;
      detailsWidth = Mathf.Clamp(contentWidth * 0.36f, minDetailsWidth, 430.0f);
    }

    float listWidth = Mathf.Max(240.0f, contentWidth - detailsWidth - 14.0f);
    _detailsSection.style.width = detailsWidth;

    float contentHeight = Mathf.Max(1.0f, panelHeight - 152.0f);
    float snapshotHeightBudget = Mathf.Max(96.0f, contentHeight * 0.30f);
    float snapshotSize = Mathf.Clamp(
        Mathf.Min((detailsWidth - 56.0f) * 0.42f, snapshotHeightBudget),
        104.0f,
        220.0f);
    if (_snapshotFrame != null)
    {
      _snapshotFrame.style.width = snapshotSize;
      _snapshotFrame.style.height = snapshotSize;
    }
    if (_refreshDetailsButton != null)
    {
      _refreshDetailsButton.style.width = snapshotSize;
      _refreshDetailsButton.style.height = 34.0f;
    }
    if (_overviewRow != null)
    {
      float overviewHeight = snapshotSize + 42.0f;
      _overviewRow.style.height = overviewHeight;
      _overviewRow.style.minHeight = overviewHeight;
      _overviewRow.style.maxHeight = overviewHeight;
    }
    if (_summaryPanel != null)
    {
      _summaryPanel.style.height = snapshotSize + 42.0f;
    }
    if (_detailsScroll != null)
    {
      _detailsScroll.style.minHeight =
          Mathf.Clamp(contentHeight * 0.28f, 120.0f, 280.0f);
    }
    if (_summaryDetails != null)
    {
      _summaryDetails.style.fontSize =
          Mathf.Clamp(detailsWidth * 0.026f, 13.0f, 16.0f);
    }
    if (_detailsContent != null)
    {
      _detailsLogFontSize = Mathf.Clamp(detailsWidth * 0.026f, 13.0f, 16.0f);
      for (int i = 0; i < _detailsContent.childCount; i++)
      {
        Label line = _detailsContent[i] as Label;
        if (line != null)
        {
          line.style.fontSize = _detailsLogFontSize;
        }
      }
      ScheduleDetailsScrollClamp(_detailsScroll != null
          ? _detailsScroll.scrollOffset.y
          : 0.0f);
    }
    if (_addButton != null)
    {
      _addButton.style.width = Mathf.Clamp(panelWidth * 0.22f, 240.0f, 430.0f);
    }

    float selectWidth = Mathf.Clamp(listWidth * 0.10f, 72.0f, 112.0f);
    float positionWidth = Mathf.Clamp(listWidth * 0.22f, 200.0f, 300.0f);
    float statusWidth = Mathf.Clamp(listWidth * 0.32f, 270.0f, 430.0f);
    SetColumnWidths(selectWidth, positionWidth, statusWidth);

    float actionWidth = Mathf.Clamp((listWidth - 310.0f) / 4.0f, 82.0f, 132.0f);
    SetActionButtonWidth(actionWidth);
  }

  private void SetColumnWidths(
      float selectWidth,
      float positionWidth,
      float statusWidth)
  {
    SetOptionalWidth(_selectHeader, selectWidth);
    SetOptionalWidth(_positionHeader, positionWidth);
    SetOptionalWidth(_statusHeader, statusWidth);
    for (int i = 0; i < _rows.Count; i++)
    {
      SetOptionalWidth(_rows[i].CheckCell, selectWidth);
      SetOptionalWidth(_rows[i].Center, positionWidth);
      SetOptionalWidth(_rows[i].Status, statusWidth);
    }
  }

  private void SetActionButtonWidth(float width)
  {
    SetOptionalWidth(_viewButton, width);
    SetOptionalWidth(_enableButton, width);
    SetOptionalWidth(_renameButton, width);
    SetOptionalWidth(_deleteButton, width);
  }

  private static void SetOptionalWidth(
      VisualElement element,
      float width)
  {
    if (element != null)
    {
      element.style.width = width;
    }
  }

  private RowView CreateRow(int rowIndex)
  {
    RowView view = new RowView();
    Button root = new Button(() => ToggleRow(view));
    root.name = $"Row{rowIndex}";
    root.focusable = false;
    root.style.height = 56.0f;
    root.style.flexDirection = FlexDirection.Row;
    root.style.alignItems = Align.Stretch;
    root.style.marginBottom = 6.0f;
    root.style.paddingLeft = 0.0f;
    root.style.paddingRight = 0.0f;
    root.style.paddingTop = 0.0f;
    root.style.paddingBottom = 0.0f;
    root.style.backgroundColor = RowBackground;
    ApplyBorder(root, new Color(0.0f, 0.0f, 0.0f, 0.0f), 0.0f);
    root.RegisterCallback<PointerEnterEvent>(_ =>
    {
      view.Hovered = true;
      RefreshRowVisual(view, rowIndex);
    });
    root.RegisterCallback<PointerLeaveEvent>(_ =>
    {
      view.Hovered = false;
      RefreshRowVisual(view, rowIndex);
    });

    view.Root = root;
    view.CheckCell = MakeCheckboxCell(112.0f);
    view.Check = MakeCheckbox();
    view.NameCell = MakeMarqueeColumn(
        out view.NameClip,
        out view.Name,
        0.0f,
        flex: 1.0f);
    view.Center = MakeColumnLabel(string.Empty, 270.0f, center: true);
    view.Status = MakeColumnLabel(string.Empty, 370.0f, center: true, last: true);
    view.CheckCell.Add(view.Check);
    root.Add(view.CheckCell);
    root.Add(view.NameCell);
    root.Add(view.Center);
    root.Add(view.Status);
    return view;
  }

  private static VisualElement MakeCheckboxCell(float width)
  {
    VisualElement cell = new VisualElement();
    cell.style.width = width;
    cell.style.height = Length.Percent(100);
    cell.style.alignItems = Align.Center;
    cell.style.justifyContent = Justify.Center;
    cell.style.borderRightWidth = 1.0f;
    cell.style.borderRightColor = DividerColor;
    return cell;
  }

  private static Label MakeCheckbox()
  {
    Label label = MakeLabel(string.Empty, 13.0f, pixel: true);
    label.style.width = 24.0f;
    label.style.height = 24.0f;
    label.style.alignSelf = Align.Center;
    label.style.unityTextAlign = TextAnchor.MiddleCenter;
    label.style.marginLeft = 0.0f;
    label.style.marginRight = 0.0f;
    label.style.marginTop = 0.0f;
    label.style.marginBottom = 0.0f;
    label.style.paddingLeft = 1.0f;
    label.style.paddingRight = 0.0f;
    label.style.paddingTop = 2.0f;
    label.style.paddingBottom = 0.0f;
    label.style.backgroundColor = CheckboxBackground;
    ApplyBorder(label, SoftEdgeColor, 2.0f);
    ApplyCornerRadius(label, 4.0f);
    return label;
  }

  private VisualElement MakeSelectAllHeader()
  {
    VisualElement cell = MakeCheckboxCell(112.0f);
    Button button = new Button(ToggleSelectAll);
    button.name = "SelectAll";
    button.focusable = false;
    button.style.width = Length.Percent(100.0f);
    button.style.height = Length.Percent(100.0f);
    button.style.flexDirection = FlexDirection.Row;
    button.style.alignItems = Align.Center;
    button.style.justifyContent = Justify.Center;
    button.style.paddingLeft = 0.0f;
    button.style.paddingRight = 0.0f;
    button.style.paddingTop = 0.0f;
    button.style.paddingBottom = 0.0f;
    button.style.backgroundColor = Color.clear;
    ApplyBorder(button, Color.clear, 0.0f);

    _selectAllCheck = MakeCheckbox();
    _selectAllCheck.style.width = 22.0f;
    _selectAllCheck.style.height = 22.0f;
    _selectAllText = MakeLabel(
        "All",
        12.0f,
        pixel: true,
        color: new Color(0.70f, 0.78f, 0.84f, 1.0f));
    _selectAllText.style.marginLeft = 6.0f;
    _selectAllText.style.unityTextAlign = TextAnchor.MiddleLeft;
    _selectAllText.style.whiteSpace = WhiteSpace.NoWrap;

    button.Add(_selectAllCheck);
    button.Add(_selectAllText);
    cell.Add(button);
    _selectAllButton = button;
    return cell;
  }

  private TabView CreateTab()
  {
    TabView view = new TabView();
    VisualElement root = new VisualElement();
    root.style.position = Position.Relative;
    root.style.flexDirection = FlexDirection.Row;
    root.style.height = TabControlHeight;
    root.style.flexGrow = 1.0f;
    root.style.marginRight = 6.0f;
    root.style.backgroundColor = HeaderBackground;
    root.style.overflow = Overflow.Hidden;
    ApplyBorder(root, SoftEdgeColor, 2.0f);
    ApplyCornerRadius(root, SoftCornerRadius);

    Button focus = new Button(() =>
    {
      _focusedId = view.Id;
      Refresh();
    });
    focus.focusable = false;
    focus.style.flexGrow = 1.0f;
    focus.style.height = Length.Percent(100.0f);
    focus.style.backgroundColor = Color.clear;
    focus.style.flexDirection = FlexDirection.Row;
    focus.style.paddingLeft = 10.0f;
    focus.style.paddingRight = 22.0f;
    focus.style.paddingTop = 0.0f;
    focus.style.paddingBottom = 0.0f;
    focus.style.overflow = Overflow.Hidden;
    ApplyBorder(focus, Color.clear, 0.0f);

    VisualElement clip = new VisualElement();
    clip.style.flexGrow = 1.0f;
    clip.style.flexShrink = 1.0f;
    clip.style.minWidth = 0.0f;
    clip.style.height = Length.Percent(100.0f);
    clip.style.overflow = Overflow.Hidden;
    clip.style.justifyContent = Justify.Center;

    Label label = MakeLabel(string.Empty, 12.0f, pixel: true);
    label.style.position = Position.Absolute;
    label.style.left = 0.0f;
    label.style.top = 0.0f;
    label.style.bottom = 0.0f;
    label.style.flexShrink = 0.0f;
    label.style.height = Length.Percent(100.0f);
    label.style.unityTextAlign = TextAnchor.MiddleLeft;
    label.style.whiteSpace = WhiteSpace.NoWrap;
    label.style.overflow = Overflow.Visible;
    clip.Add(label);
    focus.Add(clip);

    Button close = MakeInlineCloseButton(() =>
    {
      _selected.Remove(view.Id);
      if (_focusedId == view.Id)
      {
        _focusedId = FirstSelectedId();
      }
      Refresh();
    });
    close.style.position = Position.Absolute;
    close.style.right = 5.0f;
    close.style.top = 4.0f;
    close.style.width = 16.0f;
    close.style.height = 16.0f;

    root.Add(focus);
    root.Add(close);
    view.Root = root;
    view.Focus = focus;
    view.Clip = clip;
    view.Label = label;
    view.Close = close;
    return view;
  }

  private static VisualElement MakeSection()
  {
    VisualElement element = new VisualElement();
    element.style.backgroundColor = SectionBackground;
    ApplyBorder(element, SoftEdgeColor, 2.0f);
    ApplyCornerRadius(element, SoftCornerRadius);
    return element;
  }

  private static VisualElement MakeRowShell(Color background)
  {
    VisualElement element = new VisualElement();
    element.style.flexDirection = FlexDirection.Row;
    element.style.backgroundColor = background;
    ApplyCornerRadius(element, SoftCornerRadius);
    return element;
  }

  private static Label MakeColumnLabel(
      string text,
      float width,
      float flex = 0.0f,
      bool center = false,
      bool header = false,
      bool last = false)
  {
    Label label = MakeLabel(
        text,
        header ? 13.0f : 12.0f,
        pixel: true,
        color: header
            ? new Color(0.70f, 0.78f, 0.84f, 1.0f)
            : ChunkLoaderToolkitUi.TextColor);
    if (width > 0.0f)
    {
      label.style.width = width;
    }
    if (flex > 0.0f)
    {
      label.style.flexGrow = flex;
    }
    label.style.height = Length.Percent(100);
    label.style.unityTextAlign = center
        ? TextAnchor.MiddleCenter
        : TextAnchor.MiddleLeft;
    label.style.paddingLeft = center ? 0.0f : 16.0f;
    label.style.paddingRight = center ? 0.0f : 16.0f;
    label.style.whiteSpace = WhiteSpace.NoWrap;
    label.style.overflow = Overflow.Hidden;
    if (!last)
    {
      label.style.borderRightWidth = 1.0f;
      label.style.borderRightColor = DividerColor;
    }
    return label;
  }

  private static VisualElement MakeMarqueeColumn(
      out VisualElement clip,
      out Label label,
      float width,
      float flex = 0.0f,
      bool last = false)
  {
    VisualElement cell = new VisualElement();
    cell.style.height = Length.Percent(100);
    cell.style.flexDirection = FlexDirection.Row;
    cell.style.alignItems = Align.Center;
    cell.style.overflow = Overflow.Hidden;
    cell.style.minWidth = 0.0f;
    cell.style.flexShrink = 1.0f;
    if (width > 0.0f)
    {
      cell.style.width = width;
    }
    else if (flex > 0.0f)
    {
      cell.style.width = 0.0f;
    }
    if (flex > 0.0f)
    {
      cell.style.flexGrow = flex;
    }
    if (!last)
    {
      cell.style.borderRightWidth = 1.0f;
      cell.style.borderRightColor = DividerColor;
    }

    clip = new VisualElement { name = "MarqueeClip" };
    clip.style.flexGrow = 1.0f;
    clip.style.flexShrink = 1.0f;
    clip.style.minWidth = 0.0f;
    clip.style.height = Length.Percent(100);
    clip.style.marginLeft = 16.0f;
    clip.style.marginRight = 16.0f;
    clip.style.overflow = Overflow.Hidden;
    clip.style.justifyContent = Justify.Center;
    clip.pickingMode = PickingMode.Ignore;

    label = MakeLabel(string.Empty, 12.0f, pixel: true);
    label.style.position = Position.Absolute;
    label.style.left = 0.0f;
    label.style.top = 0.0f;
    label.style.bottom = 0.0f;
    label.style.flexShrink = 0.0f;
    label.style.height = Length.Percent(100);
    label.style.unityTextAlign = TextAnchor.MiddleLeft;
    label.style.whiteSpace = WhiteSpace.NoWrap;
    label.style.overflow = Overflow.Visible;

    clip.Add(label);
    cell.Add(clip);
    return cell;
  }

  private static Label MakeLabel(
      string text,
      float size,
      bool pixel,
      Color? color = null)
  {
    Label label = new Label(text);
    label.style.color = color ?? ChunkLoaderToolkitUi.TextColor;
    label.style.fontSize = size;
    label.style.unityFont = pixel
        ? ChunkLoaderToolkitUi.PixelFont
        : ChunkLoaderToolkitUi.BodyFont;
    label.style.unityTextAlign = TextAnchor.MiddleLeft;
    return label;
  }

  private static void StyleRenameTextField(TextField field)
  {
    if (field == null)
    {
      return;
    }

    field.style.unityFont = ChunkLoaderToolkitUi.PixelFont;
    field.style.fontSize = RenameFieldFontSize;
    field.style.color = ChunkLoaderToolkitUi.TextColor;
    field.style.unityTextAlign = TextAnchor.MiddleLeft;
    field.style.backgroundColor = new Color(0.035f, 0.145f, 0.155f, 0.98f);
    field.style.paddingLeft = 0.0f;
    field.style.paddingRight = 0.0f;
    field.style.paddingTop = 0.0f;
    field.style.paddingBottom = 0.0f;

    VisualElement input = field.Q("unity-text-input");
    if (input == null)
    {
      return;
    }

    input.style.height = Length.Percent(100.0f);
    input.style.unityFont = ChunkLoaderToolkitUi.PixelFont;
    input.style.fontSize = RenameFieldFontSize;
    input.style.color = ChunkLoaderToolkitUi.TextColor;
    input.style.unityTextAlign = TextAnchor.MiddleLeft;
    input.style.paddingLeft = 12.0f;
    input.style.paddingRight = 12.0f;
    input.style.paddingTop = 0.0f;
    input.style.paddingBottom = 0.0f;
    input.style.backgroundColor = new Color(0.035f, 0.145f, 0.155f, 0.98f);
  }

  private static Button MakeButton(
      string text,
      Action clicked,
      Color background,
      bool pixel = true)
  {
    Button button = new Button(clicked) { text = text };
    button.focusable = false;
    button.style.height = 34.0f;
    button.style.backgroundColor = background;
    button.style.color = ChunkLoaderToolkitUi.TextColor;
    button.style.fontSize = pixel ? 12.0f : 16.0f;
    button.style.unityFont = pixel
        ? ChunkLoaderToolkitUi.PixelFont
        : ChunkLoaderToolkitUi.BodyFont;
    button.style.unityTextAlign = TextAnchor.MiddleCenter;
    ApplyBorder(button, SoftEdgeColor, 2.0f);
    ApplyCornerRadius(button, SoftCornerRadius);
    RegisterButtonStateColors(button, background);
    return button;
  }

  private static Button MakeInlineCloseButton(Action clicked)
  {
    Button button = new Button(clicked) { text = "X" };
    button.focusable = false;
    button.style.backgroundColor = Color.clear;
    button.style.color = ChunkLoaderToolkitUi.TextColor;
    button.style.fontSize = 10.0f;
    button.style.unityFont = ChunkLoaderToolkitUi.PixelFont;
    button.style.unityTextAlign = TextAnchor.MiddleCenter;
    button.style.paddingLeft = 0.0f;
    button.style.paddingRight = 0.0f;
    button.style.paddingTop = 0.0f;
    button.style.paddingBottom = 0.0f;
    ApplyBorder(button, Color.clear, 0.0f);
    button.RegisterCallback<PointerEnterEvent>(_ =>
    {
      button.style.color = ChunkLoaderToolkitUi.AccentColor;
    });
    button.RegisterCallback<PointerLeaveEvent>(_ =>
    {
      button.style.color = ChunkLoaderToolkitUi.TextColor;
    });
    button.RegisterCallback<PointerDownEvent>(_ =>
    {
      button.style.color = new Color(0.70f, 0.78f, 0.84f, 1.0f);
    });
    button.RegisterCallback<PointerUpEvent>(_ =>
    {
      button.style.color = ChunkLoaderToolkitUi.AccentColor;
    });
    return button;
  }

  private static void RegisterButtonStateColors(
      Button button,
      Color idle)
  {
    Color hover = LerpColor(idle, Color.white, 0.12f);
    Color pressed = LerpColor(idle, Color.black, 0.18f);
    button.RegisterCallback<PointerEnterEvent>(_ =>
    {
      if (button.enabledSelf)
      {
        button.style.backgroundColor = hover;
      }
    });
    button.RegisterCallback<PointerLeaveEvent>(_ =>
    {
      button.style.backgroundColor = idle;
    });
    button.RegisterCallback<PointerDownEvent>(_ =>
    {
      if (button.enabledSelf)
      {
        button.style.backgroundColor = pressed;
      }
    });
    button.RegisterCallback<PointerUpEvent>(_ =>
    {
      if (button.enabledSelf)
      {
        button.style.backgroundColor = hover;
      }
    });
  }

  private static Color LerpColor(
      Color from,
      Color to,
      float t)
  {
    return new Color(
        Mathf.Lerp(from.r, to.r, t),
        Mathf.Lerp(from.g, to.g, t),
        Mathf.Lerp(from.b, to.b, t),
        from.a);
  }

  private static void AddActionButton(
      VisualElement parent,
      Button button)
  {
    button.style.width = 142.0f;
    button.style.height = 34.0f;
    button.style.marginLeft = 8.0f;
    parent.Add(button);
  }

  private void Refresh()
  {
    if (_instance == null)
    {
      return;
    }

    ChunkLoaderNetworkState.GetRecords(_records);
    _selected.RemoveWhere(
        id => !_records.Exists(record => record.registrationId == id));
    if (_focusedId != 0 &&
        !_records.Exists(record => record.registrationId == _focusedId))
    {
      _focusedId = 0;
    }

    int maxPage = Math.Max(0, (_records.Count - 1) / RowsPerPage);
    _page = Mathf.Clamp(_page, 0, maxPage);
    _tabPage = Mathf.Clamp(
        _tabPage,
        0,
        Math.Max(0, (_selected.Count - 1) / TabsPerPage));

    for (int i = 0; i < _rows.Count; i++)
    {
      int recordIndex = _page * RowsPerPage + i;
      RowView row = _rows[i];
      if (recordIndex >= _records.Count)
      {
        row.Root.style.display = DisplayStyle.None;
        row.Id = 0;
        SetRowName(row, string.Empty);
        continue;
      }

      ChunkLoaderRegistrationRecord record = _records[recordIndex];
      bool selected = _selected.Contains(record.registrationId);
      row.Id = record.registrationId;
      row.Root.style.display = DisplayStyle.Flex;
      RefreshRowVisual(row, i);
      SetRowName(row, record.customName);
      row.Center.text =
          $"({record.Coordinate.Center.x}, {record.Coordinate.Center.y})";
      row.Status.text = FormatStatus(record);
    }

    if (_emptyText != null)
    {
      _emptyText.style.display =
          _records.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }
    if (_pageText != null)
    {
      _pageText.text =
          $"{_page + 1} / {Math.Max(1, maxPage + 1)}";
    }

    ChunkLoaderQuotaSummary quota = ChunkLoaderNetworkState.Quota;
    if (_quota != null)
    {
      string personalLimit =
          ChunkLoaderSettings.FormatLimit(quota.PersonalActiveLimit);
      string worldLimit =
          ChunkLoaderSettings.FormatLimit(quota.WorldActiveLimit);
      _quota.text =
          $"Personal: {quota.PersonalActive}/{personalLimit}     " +
          $"World: {quota.WorldActive}/{worldLimit}";
    }

    RefreshActions();
    RefreshSelectAllHeader();
    RefreshTabs();
    RefreshDetails();
  }

  private void SetRowName(
      RowView row,
      string name)
  {
    if (row == null || row.Name == null)
    {
      return;
    }

    string safeName = name ?? string.Empty;
    if (row.NameText == safeName)
    {
      return;
    }

    row.NameText = safeName;
    row.NameMarqueeStartTime = Time.unscaledTime;
    row.Name.text = safeName;
    row.Name.style.left = 0.0f;
    row.Name.style.width = StyleKeyword.Auto;
    row.Name.style.unityTextAlign = TextAnchor.MiddleLeft;
  }

  private static string FormatStatus(
      ChunkLoaderRegistrationRecord record)
  {
    if (record.actualState == ChunkLoaderRuntimeState.Disabled)
    {
      if (record.lastDisabledAtUtcTicks <= 0)
      {
        return "Unloaded";
      }

      DateTime disabled = new DateTime(
          record.lastDisabledAtUtcTicks,
          DateTimeKind.Utc);
      return $"Unloaded since {disabled.ToLocalTime():dd.MM.yyyy}";
    }
    if (record.lastEnabledAtUtcTicks <= 0)
    {
      return record.actualState.ToString();
    }

    DateTime enabled = new DateTime(
        record.lastEnabledAtUtcTicks,
        DateTimeKind.Utc);
    return $"Loaded since {enabled.ToLocalTime():dd.MM.yyyy}";
  }

  private void RefreshRowVisual(
      RowView row,
      int pageRowIndex)
  {
    if (row == null || row.Root == null)
    {
      return;
    }

    bool selected = row.Id != 0 && _selected.Contains(row.Id);
    row.Root.style.backgroundColor = selected
        ? RowSelectedBackground
        : row.Hovered
            ? RowHoverBackground
            : pageRowIndex % 2 == 0
                ? RowBackground
                : RowAlternateBackground;
    if (row.Check != null)
    {
      row.Check.text = selected ? "X" : string.Empty;
      row.Check.style.backgroundColor = selected
          ? CheckboxSelectedBackground
          : CheckboxBackground;
      row.Check.style.borderTopColor = selected
          ? ChunkLoaderToolkitUi.AccentColor
          : SoftEdgeColor;
      row.Check.style.borderRightColor = selected
          ? ChunkLoaderToolkitUi.AccentColor
          : SoftEdgeColor;
      row.Check.style.borderBottomColor = selected
          ? ChunkLoaderToolkitUi.AccentColor
          : SoftEdgeColor;
      row.Check.style.borderLeftColor = selected
          ? ChunkLoaderToolkitUi.AccentColor
          : SoftEdgeColor;
    }
  }

  private void ToggleRow(RowView row)
  {
    if (row == null || row.Id == 0)
    {
      return;
    }

    _focusedId = row.Id;
    if (!_selected.Add(row.Id))
    {
      _selected.Remove(row.Id);
      if (_focusedId == row.Id)
      {
        _focusedId = _selected.Count > 0 ? FirstSelectedId() : 0;
      }
    }
    Refresh();
  }

  private void ToggleSelectAll()
  {
    bool selectAll = false;
    for (int i = 0; i < _records.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _records[i];
      if (CanManage(record) &&
          !_selected.Contains(record.registrationId))
      {
        selectAll = true;
        break;
      }
    }

    for (int i = 0; i < _records.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _records[i];
      if (!CanManage(record))
      {
        continue;
      }

      if (selectAll)
      {
        _selected.Add(record.registrationId);
      }
      else
      {
        _selected.Remove(record.registrationId);
      }
    }

    if (_focusedId == 0 || !_selected.Contains(_focusedId))
    {
      _focusedId = FirstSelectedId();
    }
    Refresh();
  }

  private void RefreshActions()
  {
    int selectedCount = _selected.Count;
    bool canManageSelection = selectedCount > 0;
    foreach (ulong id in _selected)
    {
      if (!TryFind(id, out ChunkLoaderRegistrationRecord record) ||
          !CanManage(record))
      {
        canManageSelection = false;
        break;
      }
    }

    SetButtonEnabled(_viewButton, selectedCount == 1);
    SetButtonEnabled(_renameButton, selectedCount == 1 && canManageSelection);
    SetButtonEnabled(_deleteButton, canManageSelection);
    SetButtonEnabled(_enableButton, canManageSelection);

    bool shouldEnable = false;
    foreach (ulong id in _selected)
    {
      if (TryFind(id, out ChunkLoaderRegistrationRecord record) &&
          !record.desiredEnabled)
      {
        shouldEnable = true;
        break;
      }
    }
    if (_enableButton != null)
    {
      _enableButton.text = shouldEnable ? "Enable" : "Disable";
    }

    SetButtonEnabled(_previousButton, _page > 0);
    SetButtonEnabled(
        _nextButton,
        (_page + 1) * RowsPerPage < _records.Count);
    SetButtonEnabled(
        _refreshDetailsButton,
        selectedCount > 0);
    if (_refreshDetailsButton != null)
    {
      _refreshDetailsButton.text = ChunkLoaderNetworkState.SnapshotsEnabled
          ? "Refresh snapshot"
          : "Refresh details";
    }
  }

  private void RefreshSelectAllHeader()
  {
    int selectable = 0;
    int selected = 0;
    for (int i = 0; i < _records.Count; i++)
    {
      ChunkLoaderRegistrationRecord record = _records[i];
      if (!CanManage(record))
      {
        continue;
      }

      selectable++;
      if (_selected.Contains(record.registrationId))
      {
        selected++;
      }
    }

    bool allSelected = selectable > 0 && selected == selectable;
    bool partlySelected = selected > 0 && !allSelected;
    SetButtonEnabled(_selectAllButton, selectable > 0);
    if (_selectAllCheck == null)
    {
      return;
    }

    _selectAllCheck.text = allSelected ? "X" : partlySelected ? "-" : string.Empty;
    _selectAllCheck.style.backgroundColor =
        allSelected || partlySelected
            ? CheckboxSelectedBackground
            : CheckboxBackground;
    Color border =
        allSelected || partlySelected
            ? ChunkLoaderToolkitUi.AccentColor
            : SoftEdgeColor;
    _selectAllCheck.style.borderTopColor = border;
    _selectAllCheck.style.borderRightColor = border;
    _selectAllCheck.style.borderBottomColor = border;
    _selectAllCheck.style.borderLeftColor = border;
  }

  private void RefreshTabs()
  {
    List<ulong> selectedIds = new List<ulong>();
    for (int i = 0; i < _records.Count; i++)
    {
      if (_selected.Contains(_records[i].registrationId))
      {
        selectedIds.Add(_records[i].registrationId);
      }
    }

    bool paged = selectedIds.Count > TabsPerPage;
    SetDisplay(_tabPreviousButton, paged);
    SetDisplay(_tabNextButton, paged);
    SetButtonEnabled(_tabPreviousButton, _tabPage > 0);
    SetButtonEnabled(
        _tabNextButton,
        (_tabPage + 1) * TabsPerPage < selectedIds.Count);

    int first = _tabPage * TabsPerPage;
    for (int slot = 0; slot < _tabViews.Count; slot++)
    {
      int index = first + slot;
      TabView tab = _tabViews[slot];
      if (index >= selectedIds.Count ||
          !TryFind(
              selectedIds[index],
              out ChunkLoaderRegistrationRecord record))
      {
        tab.Root.style.display = DisplayStyle.None;
        tab.Id = 0;
        ResetTabMarquee(tab, string.Empty);
        continue;
      }

      bool changedTab =
          tab.Id != record.registrationId ||
          tab.Title != record.customName;
      tab.Id = record.registrationId;
      tab.Root.style.display = DisplayStyle.Flex;
      tab.Root.style.backgroundColor = tab.Id == _focusedId
          ? RowSelectedBackground
          : HeaderBackground;
      if (changedTab)
      {
        ResetTabMarquee(tab, record.customName);
      }
    }
  }

  private void ResetTabMarquee(
      TabView tab,
      string title)
  {
    if (tab == null)
    {
      return;
    }

    tab.Title = title ?? string.Empty;
    tab.MarqueeStartTime = Time.unscaledTime;
    if (tab.Label != null)
    {
      tab.Label.text = tab.Title;
      tab.Label.style.left = 0.0f;
      tab.Label.style.unityTextAlign = TextAnchor.MiddleLeft;
    }
  }

  private void UpdateTabMarquees()
  {
    float now = Time.unscaledTime;
    for (int i = 0; i < _tabViews.Count; i++)
    {
      TabView tab = _tabViews[i];
      if (tab == null ||
          tab.Id == 0 ||
          tab.Clip == null ||
          tab.Label == null ||
          string.IsNullOrEmpty(tab.Title))
      {
        continue;
      }

      tab.MarqueeStartTime = UpdateMeasuredMarquee(
          tab.Clip,
          tab.Label,
          tab.Title,
          tab.MarqueeStartTime,
          now,
          centerWhenFits: true,
          TabMarqueePixelsPerSecond,
          TabMarqueeEndPadding);
    }
  }

  private void UpdateRowNameMarquees()
  {
    float now = Time.unscaledTime;
    for (int i = 0; i < _rows.Count; i++)
    {
      RowView row = _rows[i];
      if (row == null ||
          row.Id == 0 ||
          row.NameClip == null ||
          row.Name == null ||
          string.IsNullOrEmpty(row.NameText))
      {
        continue;
      }

      row.NameMarqueeStartTime = UpdateMeasuredMarquee(
          row.NameClip,
          row.Name,
          row.NameText,
          row.NameMarqueeStartTime,
          now,
          centerWhenFits: false,
          RowNameMarqueePixelsPerSecond,
          RowNameMarqueeEndPadding);
    }
  }

  private static float UpdateMeasuredMarquee(
      VisualElement clip,
      Label label,
      string text,
      float startTime,
      float now,
      bool centerWhenFits,
      float pixelsPerSecond,
      float endPadding)
  {
    if (clip == null || label == null || string.IsNullOrEmpty(text))
    {
      SetLabelOffset(label, 0.0f);
      return now;
    }

    float clipWidth = clip.resolvedStyle.width;
    if (float.IsNaN(clipWidth) || clipWidth <= 1.0f)
    {
      SetLabelOffset(label, 0.0f);
      return startTime;
    }

    float fontSize = label.resolvedStyle.fontSize;
    if (float.IsNaN(fontSize) || fontSize <= 1.0f)
    {
      fontSize = 12.0f;
    }

    float textWidth = MeasureTextWidth(label, text, fontSize);
    float overflow = textWidth - clipWidth;
    if (overflow <= 2.0f)
    {
      label.style.width = clipWidth;
      label.style.unityTextAlign = centerWhenFits
          ? TextAnchor.MiddleCenter
          : TextAnchor.MiddleLeft;
      SetLabelOffset(label, 0.0f);
      return now;
    }

    float maxOffset = overflow + endPadding;
    label.style.width = textWidth + endPadding;
    label.style.unityTextAlign = TextAnchor.MiddleLeft;

    float elapsed = now - startTime;
    if (elapsed < TabMarqueeDelaySeconds)
    {
      SetLabelOffset(label, 0.0f);
      return startTime;
    }

    float offset = (elapsed - TabMarqueeDelaySeconds) * pixelsPerSecond;
    if (offset > maxOffset)
    {
      SetLabelOffset(label, 0.0f);
      return now;
    }

    SetLabelOffset(label, -offset);
    return startTime;
  }

  private static void SetLabelOffset(
      Label label,
      float offset)
  {
    if (label != null)
    {
      label.style.left = offset;
    }
  }

  private static float MeasureTextWidth(
      Label label,
      string text,
      float fontSize)
  {
    return EstimateTabTextWidth(text, fontSize);
  }

  private static float EstimateTabTextWidth(
      string text,
      float fontSize)
  {
    if (string.IsNullOrEmpty(text))
    {
      return 0.0f;
    }

    float width = fontSize * 0.35f;
    for (int i = 0; i < text.Length; i++)
    {
      char c = text[i];
      if (c == ' ')
      {
        width += fontSize * 0.55f;
      }
      else if (c == '.' ||
               c == ',' ||
               c == ':' ||
               c == ';' ||
               c == '!' ||
               c == '|' ||
               c == '\'' ||
               c == 'i' ||
               c == 'l')
      {
        width += fontSize * 0.62f;
      }
      else
      {
        width += fontSize * 0.98f;
      }
    }

    return width + fontSize * 1.20f;
  }

  private void RefreshDetails()
  {
    ulong id = _focusedId != 0 ? _focusedId : FirstSelectedId();
    if (!TryFind(id, out ChunkLoaderRegistrationRecord record))
    {
      _liveDetailsFocusedId = 0;
      _nextLiveDetailsRequestAt = 0.0d;
      if (_summaryDetails != null)
      {
        _summaryDetails.text =
            "Select a chunk from the list to inspect its live state.";
      }
      SetDetailsText(
          "Select a chunk from the list to inspect recent activity.");
      _snapshot.Render(_snapshotImage, null, null);
      return;
    }

    if (_lastDetailsRequestId != id)
    {
      _lastDetailsRequestId = id;
      _liveDetailsFocusedId = id;
      _nextLiveDetailsRequestAt =
          Time.realtimeSinceStartupAsDouble +
          ChunkLoaderConstants.LiveDetailsClientIntervalSeconds;
      if (_detailsScroll != null)
      {
        _detailsScroll.scrollOffset = Vector2.zero;
      }
      ChunkLoaderDetailsState.Request(id);
    }

    string error = record.lastErrorCode == ChunkLoaderErrorCode.None
        ? "None"
        : record.lastErrorText;
    string summary =
        $"{Truncate(record.customName, 42)}\n" +
        $"Center: ({record.Coordinate.Center.x}, " +
        $"{record.Coordinate.Center.y})\n" +
        $"State: {record.actualState}\n" +
        $"Error: {error}\n\n" +
        "Live details pending...";
    string activity = "Live details pending...";
    ChunkLoaderDetailsData data = null;
    if (ChunkLoaderDetailsState.TryGet(id, out data))
    {
      if (data.ErrorCode != ChunkLoaderErrorCode.None)
      {
        summary =
            $"{Truncate(record.customName, 42)}\n" +
            $"Center: ({record.Coordinate.Center.x}, " +
            $"{record.Coordinate.Center.y})\n" +
            $"State: {record.actualState}\n" +
            $"Error: {data.ErrorText}";
        activity = "No activity is available while details are in an error state.";
      }
      else
      {
        summary =
            $"{Truncate(record.customName, 42)}\n" +
            $"Center: ({record.Coordinate.Center.x}, " +
            $"{record.Coordinate.Center.y})\n" +
            $"State: {record.actualState}\n" +
            $"Error: {error}\n\n" +
            $"Entities: {data.TotalEntities}\n" +
            $"Objects: {data.Objects}\n" +
            $"Mobs: {data.Enemies}\n" +
            $"Players: {data.Players}";
        if (!ChunkLoaderNetworkState.SnapshotsEnabled)
        {
          summary += "\n\nLive visual snapshots are disabled by the server.";
        }

        activity = string.Empty;
        int visibleLogs = 0;
        for (int i = 0; i < data.Logs.Count; i++)
        {
          ChunkLoaderTelemetryEvent log = data.Logs[i];
          string timestamp = log.timestampUtcTicks > 0
              ? new DateTime(log.timestampUtcTicks, DateTimeKind.Utc)
                  .ToLocalTime()
                  .ToString("HH:mm:ss")
              : "--:--:--";
          if (activity.Length > 0)
          {
            activity += "\n";
          }
          activity += $"- {timestamp} {log.message}";
          visibleLogs++;
        }
        if (visibleLogs == 0)
        {
          activity = "No session activity recorded yet.";
        }
      }
    }

    _snapshot.Render(
        _snapshotImage,
        data,
        record.Coordinate);
    if (_summaryDetails != null)
    {
      _summaryDetails.text = summary;
    }
    SetDetailsText(activity);
  }

  private void ViewOnMap()
  {
    if (_selected.Count != 1 ||
        !TryFind(FirstSelectedId(), out ChunkLoaderRegistrationRecord record))
    {
      return;
    }

    Hide();
    _map.ShowBigMap();
    _map.CenterAtPosition(new float2(
        record.Coordinate.Center.x,
        record.Coordinate.Center.y));
    ChunkLoaderMapUiHost.Instance?.SetOverlayVisible(true);
  }

  private void ToggleSelectedEnabled()
  {
    bool shouldEnable = false;
    foreach (ulong id in _selected)
    {
      if (TryFind(id, out ChunkLoaderRegistrationRecord record) &&
          !record.desiredEnabled)
      {
        shouldEnable = true;
        break;
      }
    }

    foreach (ulong id in new List<ulong>(_selected))
    {
      if (TryFind(id, out ChunkLoaderRegistrationRecord record) &&
          record.desiredEnabled != shouldEnable)
      {
        ChunkLoaderNetworkState.SetEnabled(
            record.registrationId,
            record.revision,
            shouldEnable);
      }
    }
  }

  private void DeleteSelected()
  {
    if (_selected.Count == 0 || Manager.menu == null)
    {
      return;
    }

    int count = _selected.Count;
    string names = string.Empty;
    int displayed = 0;
    foreach (ulong id in _selected)
    {
      if (!TryFind(id, out ChunkLoaderRegistrationRecord record))
      {
        continue;
      }
      if (displayed < 6)
      {
        names +=
            $"\n- {record.customName} " +
            $"({record.Coordinate.Center.x}, {record.Coordinate.Center.y})";
      }
      displayed++;
    }
    if (displayed > 6)
    {
      names += $"\n- and {displayed - 6} more";
    }

    SuspendForVanillaPopup();
    Manager.menu.centerPopUpText.StartNewDisplaySequence(
        $"Delete {count} chunk registration" +
        $"{(count == 1 ? "" : "s")}?{names}\n\n" +
        "The world contents and unrelated telemetry will not be deleted.",
        null,
        true,
        0.0f,
        1.5f,
        true,
        0.0f,
        1.0f,
        false,
        TextManager.FontFace.boldMedium,
        response =>
        {
          ResumeAfterVanillaPopup();
          if (!response.IsConfirm)
          {
            return;
          }
          foreach (ulong id in new List<ulong>(_selected))
          {
            if (TryFind(id, out ChunkLoaderRegistrationRecord record))
            {
              ChunkLoaderNetworkState.Delete(
                  record.registrationId,
                  record.revision);
            }
          }
        },
        new List<string> { "cancelDialogue", "delete" },
        10.0f,
        0.95f,
        0,
        18.0f,
        false,
        false,
        true,
        false);
  }

  private void BeginRename()
  {
    if (_selected.Count != 1 ||
        !TryFind(FirstSelectedId(), out ChunkLoaderRegistrationRecord record))
    {
      return;
    }

    _focusedId = record.registrationId;
    _renaming = true;
    _renameField.SetValueWithoutNotify(record.customName);
    _renameRow.style.display = DisplayStyle.Flex;
    _renameField.Focus();
    ActivateVanillaTextInput();
  }

  private void CommitRenameField()
  {
    if (!_renaming)
    {
      return;
    }

    string value = _renameField.value?.Trim();
    _renaming = false;
    ReleaseVanillaTextInput();
    _renameRow.style.display = DisplayStyle.None;
    if (_selected.Count != 1 || string.IsNullOrWhiteSpace(value))
    {
      return;
    }

    ulong id = FirstSelectedId();
    if (TryFind(id, out ChunkLoaderRegistrationRecord record))
    {
      ChunkLoaderNetworkState.Rename(
          record.registrationId,
          record.revision,
          value);
    }
  }

  private void CancelRename()
  {
    _renaming = false;
    ReleaseVanillaTextInput();
    if (_renameRow != null)
    {
      _renameRow.style.display = DisplayStyle.None;
    }
  }

  private void ActivateVanillaTextInput()
  {
    if (_vanillaTextInputActive)
    {
      return;
    }

    // UI Toolkit's TextField already owns text entry. Registering a vanilla
    // TextInputInterface as well makes each typed character arrive twice:
    // once through UI Toolkit and once through InputManager.AppendString.
    // We only need to suppress gameplay/map bindings while the field has focus.
    if (!_mapViewInputSuspended && Manager.input != null)
    {
      Manager.input.DisableInput(-1.0f);
    }
    _vanillaTextInputActive = true;
  }

  private void ReleaseVanillaTextInput()
  {
    if (!_vanillaTextInputActive)
    {
      _vanillaTextInputActive = false;
      return;
    }

    _vanillaTextInputActive = false;
    if (!_mapViewInputSuspended && Manager.input != null)
    {
      Manager.input.EnableInput();
    }
  }

  private void SuspendMapViewInput()
  {
    if (_mapViewInputSuspended || Manager.input == null)
    {
      return;
    }

    Manager.input.DisableInput(-1.0f);
    _mapViewInputSuspended = true;
  }

  private void ResumeMapViewInput()
  {
    if (!_mapViewInputSuspended || Manager.input == null)
    {
      _mapViewInputSuspended = false;
      return;
    }

    if (!_vanillaTextInputActive)
    {
      Manager.input.EnableInput();
    }
    _mapViewInputSuspended = false;
  }

  private void LoadNewChunk()
  {
    Hide();
    ChunkLoaderMapUiHost.Instance?.SetOverlayVisible(true);
  }

  private void RefreshFocusedDetails()
  {
    ulong id = _focusedId != 0 ? _focusedId : FirstSelectedId();
    if (id != 0)
    {
      if (TryFind(id, out ChunkLoaderRegistrationRecord record))
      {
        _snapshot.ForceRefreshMap(_snapshotImage, record.Coordinate);
      }
      ChunkLoaderDetailsState.Request(id, force: true, includeSamples: true);
      _liveDetailsFocusedId = id;
      _nextLiveDetailsRequestAt =
          Time.realtimeSinceStartupAsDouble +
          ChunkLoaderConstants.LiveDetailsClientIntervalSeconds;
    }
  }

  private void UpdateLiveDetailsFeed()
  {
    ulong id = _focusedId != 0 ? _focusedId : FirstSelectedId();
    if (id == 0 ||
        !_selected.Contains(id) ||
        !TryFind(id, out ChunkLoaderRegistrationRecord _))
    {
      _liveDetailsFocusedId = 0;
      _nextLiveDetailsRequestAt = 0.0d;
      return;
    }

    double now = Time.realtimeSinceStartupAsDouble;
    if (_liveDetailsFocusedId != id)
    {
      _liveDetailsFocusedId = id;
      _nextLiveDetailsRequestAt =
          now + ChunkLoaderConstants.LiveDetailsClientIntervalSeconds;
      return;
    }

    if (now < _nextLiveDetailsRequestAt)
    {
      return;
    }

    _nextLiveDetailsRequestAt =
        now + ChunkLoaderConstants.LiveDetailsClientIntervalSeconds;
    ChunkLoaderDetailsState.Request(
        id,
        includeSamples: false,
        live: true);
  }

  private void OnDetailsChanged(ulong registrationId)
  {
    if (registrationId == _focusedId ||
        _selected.Contains(registrationId))
    {
      RefreshDetails();
    }
  }

  private void PreviousPage()
  {
    if (_page > 0)
    {
      _page--;
      Refresh();
    }
  }

  private void NextPage()
  {
    if ((_page + 1) * RowsPerPage < _records.Count)
    {
      _page++;
      Refresh();
    }
  }

  private void OnMutationCompleted(
      uint requestId,
      ChunkLoaderMutationResult result)
  {
    if (!result.Success && Manager.menu != null)
    {
      SuspendForVanillaPopup();
      Manager.menu.centerPopUpText.StartNewDisplaySequence(
          string.IsNullOrEmpty(result.Message)
              ? result.ErrorCode.ToString()
              : result.Message,
          null,
          true,
          0.0f,
          2.0f,
          true,
          0.0f,
          1.0f,
          false,
          TextManager.FontFace.boldMedium,
          _ => ResumeAfterVanillaPopup(),
          new List<string> { "ok" },
          10.0f,
          0.95f,
          0,
          18.0f,
          false,
          false);
      ChunkLoaderNetworkState.RequestSnapshot();
    }
  }

  private void SetVisible(bool visible)
  {
    _showing = visible;
    if (_root != null)
    {
      _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
      _root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }
    if (_backdrop != null)
    {
      _backdrop.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
      _backdrop.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }
  }

  private void SuspendForVanillaPopup()
  {
    if (_backdrop != null)
    {
      _backdrop.style.display = DisplayStyle.None;
    }
  }

  private void ResumeAfterVanillaPopup()
  {
    if (_showing &&
        _backdrop != null &&
        _map != null &&
        _map.IsShowingBigMap)
    {
      _backdrop.style.display = DisplayStyle.Flex;
    }
  }

  private void CleanupFailedCreation()
  {
    _snapshot.Destroy();
    ChunkLoaderToolkitUi.DestroyToolkitDocument(_instance);
    _instance = null;
    _document = null;
    _root = null;
    _backdrop = null;
    _overviewRow = null;
    _summaryPanel = null;
    _rows.Clear();
    _tabViews.Clear();
    _showing = false;
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
    ApplyCornerRadius(element, SoftCornerRadius);
  }

  private static void ApplyCornerRadius(
      VisualElement element,
      float radius)
  {
    if (element == null)
    {
      return;
    }

    element.style.borderTopLeftRadius = radius;
    element.style.borderTopRightRadius = radius;
    element.style.borderBottomRightRadius = radius;
    element.style.borderBottomLeftRadius = radius;
  }

  private static void SetButtonEnabled(
      Button button,
      bool enabled)
  {
    if (button == null)
    {
      return;
    }

    button.SetEnabled(enabled);
    button.style.opacity = enabled ? 1.0f : 0.42f;
  }

  private static void SetDisplay(
      VisualElement element,
      bool visible)
  {
    if (element != null)
    {
      element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
  }

  private bool TryFind(
      ulong id,
      out ChunkLoaderRegistrationRecord record)
  {
    for (int i = 0; i < _records.Count; i++)
    {
      if (_records[i].registrationId == id)
      {
        record = _records[i];
        return true;
      }
    }
    record = null;
    return false;
  }

  private static bool CanManage(ChunkLoaderRegistrationRecord record)
  {
    return record != null &&
           (ChunkLoaderNetworkState.ViewerIsAdmin ||
            record.ownerPersistentId ==
                ChunkLoaderNetworkState.ViewerPersistentId);
  }

  private ulong FirstSelectedId()
  {
    foreach (ulong id in _selected)
    {
      return id;
    }
    return 0;
  }

  private static string Truncate(string value, int maxLength)
  {
    if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
    {
      return value ?? string.Empty;
    }
    return value.Substring(0, Math.Max(1, maxLength - 3)) + "...";
  }
}
