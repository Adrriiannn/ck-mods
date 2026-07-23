using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChunkLoaderCursorOverlay
{
  private const int CursorSortingOffset = 1;
  // Vanilla cursor_inv is an 8x8 sprite presented at roughly 4x UI scale.
  // Keep this in UI Toolkit panel units so the PanelSettings scaler still
  // handles different player resolutions for us.
  private const float CursorPanelSize = 32.0f;

  private GameObject _instance;
  private VisualElement _root;
  private UnityEngine.UIElements.Image _cursorImage;
  private bool _loggedMissingTexture;

  public void Update(bool visible)
  {
    if (!visible)
    {
      SetVisible(false);
      return;
    }

    EnsureCreated();
    if (_root == null || _root.panel == null || _cursorImage == null)
    {
      SetVisible(false);
      return;
    }

    Texture2D texture = ChunkLoaderToolkitUi.CursorTexture;
    if (texture == null)
    {
      if (!_loggedMissingTexture)
      {
        _loggedMissingTexture = true;
        Debug.LogWarning(
            "[ChunkLoaderMod] Vanilla cursor texture is unavailable; " +
            "the cursor overlay will stay hidden above Chunk Loader UI.");
      }
      SetVisible(false);
      return;
    }

    ApplyTextureCursor(texture);
    SetVisible(true);
  }

  public void Prewarm()
  {
    EnsureCreated();
    SetVisible(false);
  }

  public void Destroy()
  {
    if (_instance != null)
    {
      ChunkLoaderToolkitUi.DestroyToolkitDocument(_instance);
    }

    _instance = null;
    _root = null;
    _cursorImage = null;
    _loggedMissingTexture = false;
  }

  private void EnsureCreated()
  {
    if (_instance != null)
    {
      return;
    }

    _instance = ChunkLoaderToolkitUi.CreateToolkitDocument(
        "ChunkLoaderCursorOverlay",
        ChunkLoaderToolkitUi.ToolkitSortingOrder + CursorSortingOffset,
        out UIDocument _,
        out _root);
    if (_root == null)
    {
      return;
    }

    _root.pickingMode = PickingMode.Ignore;
    _root.style.position = Position.Absolute;
    _root.style.left = 0.0f;
    _root.style.top = 0.0f;
    _root.style.right = 0.0f;
    _root.style.bottom = 0.0f;

    _cursorImage = new UnityEngine.UIElements.Image
    {
      name = "ChunkLoaderCursorOverlayImage",
      pickingMode = PickingMode.Ignore,
      scaleMode = ScaleMode.ScaleToFit
    };
    _cursorImage.style.position = Position.Absolute;
    _cursorImage.style.display = DisplayStyle.None;
    _root.Add(_cursorImage);

    Debug.Log(
        "[ChunkLoaderMod] Created UI Toolkit cursor overlay above Chunk Loader panels.");
  }

  private void ApplyTextureCursor(Texture2D texture)
  {
    if (_root == null || _root.panel == null)
    {
      return;
    }

    Vector2 mouse = Input.mousePosition;
    Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(
        _root.panel,
        new Vector2(mouse.x, Screen.height - mouse.y));

    _cursorImage.image = texture;
    _cursorImage.sprite = null;
    _cursorImage.tintColor = Color.white;
    _cursorImage.style.left = panelPosition.x;
    _cursorImage.style.top = panelPosition.y;
    _cursorImage.style.width = CursorPanelSize;
    _cursorImage.style.height = CursorPanelSize;
  }

  private void SetVisible(bool visible)
  {
    if (_cursorImage != null)
    {
      _cursorImage.style.display =
          visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
  }
}
