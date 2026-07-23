using PugMod;
using UnityEngine;
using UnityEngine.UIElements;

public static class ChunkLoaderToolkitUi
{
  public const int ToolkitSortingOrder = 12;

  public static readonly Color TextColor =
      new Color(0.92f, 0.95f, 0.97f, 1.0f);
  public static readonly Color MutedTextColor =
      new Color(0.61f, 0.70f, 0.76f, 1.0f);
  public static readonly Color AccentColor =
      new Color(0.28f, 0.92f, 1.0f, 1.0f);
  public static readonly Color SelectionColor =
      new Color(0.10f, 0.43f, 0.58f, 1.0f);

  private const string PixelFontPath =
      "Assets/ChunkLoaderMod/UI/Fonts/PressStart2P-Regular.ttf";
  private const string BodyFontPath =
      "Assets/ChunkLoaderMod/UI/Fonts/Roboto-Variable.ttf";
  private const string CursorTexturePath =
      "Assets/ChunkLoaderMod/UI/Vanilla/cursor_inv.png";

  private static bool _initialized;

  public static Font PixelFont { get; private set; }
  public static Font BodyFont { get; private set; }
  public static Texture2D CursorTexture { get; private set; }

  public static bool IsReady => _initialized;

  public static void Initialize(IMod handler)
  {
    AssetBundle bundle = null;
    if (API.ModLoader?.LoadedMods != null)
    {
      LoadedMod loaded = FindLoadedModForHandler(handler);
      if (loaded?.AssetBundles != null && loaded.AssetBundles.Count > 0)
      {
        bundle = loaded.AssetBundles[0];
      }
    }

    if (bundle != null)
    {
      PixelFont = bundle.LoadAsset<Font>(PixelFontPath);
      BodyFont = bundle.LoadAsset<Font>(BodyFontPath);
      CursorTexture = bundle.LoadAsset<Texture2D>(CursorTexturePath);
    }

    if (PixelFont == null)
    {
      PixelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
    if (BodyFont == null)
    {
      BodyFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    _initialized = true;
    Debug.Log(
        "[ChunkLoaderMod] UI Toolkit interface initialized. " +
        $"pixelFont={(PixelFont != null ? PixelFont.name : "null")}, " +
        $"bodyFont={(BodyFont != null ? BodyFont.name : "null")}, " +
        $"cursorTexture={(CursorTexture != null ? CursorTexture.name : "null")}.");
  }

  public static void Shutdown()
  {
    PixelFont = null;
    BodyFont = null;
    CursorTexture = null;
    _initialized = false;
  }

  public static GameObject CreateToolkitDocument(
      string instanceName,
      int sortingOrder,
      out UIDocument document,
      out VisualElement root,
      bool logCreation = true)
  {
    Transform parent = GetUiParent();
    GameObject instance = new GameObject(instanceName);
    instance.SetActive(false);
    if (parent != null)
    {
      instance.transform.SetParent(parent, false);
    }
    else
    {
      Object.DontDestroyOnLoad(instance);
    }
    SetLayerRecursive(instance, ObjectLayerID.UI);

    PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
    settings.name = instanceName + " PanelSettings";
    settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
    settings.referenceResolution = new Vector2Int(1920, 1080);
    settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
    settings.match = 0.5f;
    settings.sortingOrder = sortingOrder;
    settings.targetDisplay = 0;

    document = instance.AddComponent<UIDocument>();
    document.panelSettings = settings;
    instance.SetActive(true);
    root = document.rootVisualElement;
    if (root != null)
    {
      root.name = instanceName + "Root";
      root.pickingMode = PickingMode.Ignore;
      root.style.flexGrow = 1.0f;
      root.style.width = Length.Percent(100);
      root.style.height = Length.Percent(100);
      root.style.position = Position.Relative;
    }

    if (logCreation)
    {
      Debug.Log(
          $"[ChunkLoaderMod] Created UI Toolkit document {instanceName} " +
          $"sortingOrder={sortingOrder}, parent={(parent != null ? parent.name : "null")}.");
    }
    return instance;
  }

  public static void DestroyToolkitDocument(GameObject instance)
  {
    if (instance == null)
    {
      return;
    }

    UIDocument document = instance.GetComponent<UIDocument>();
    PanelSettings settings = document != null ? document.panelSettings : null;
    if (settings != null)
    {
      Object.Destroy(settings);
    }
    Object.Destroy(instance);
  }

  public static bool TryGetMapPanelRect(
      MapUI map,
      VisualElement root,
      out Rect panelRect)
  {
    panelRect = default;
    if (map == null ||
        root == null ||
        root.panel == null ||
        map.largeMapBorder == null)
    {
      return false;
    }

    Camera uiCamera = GetUiCamera();
    if (uiCamera == null)
    {
      return false;
    }

    return TryGetRendererPanelRect(map.largeMapBorder, root, out panelRect) &&
           panelRect.width >= 100.0f &&
           panelRect.height >= 100.0f;
  }

  public static bool TryGetRendererPanelRect(
      Renderer renderer,
      VisualElement root,
      out Rect panelRect)
  {
    panelRect = default;
    if (renderer == null || root == null || root.panel == null)
    {
      return false;
    }

    Camera uiCamera = GetUiCamera();
    if (uiCamera == null ||
        !TryGetRendererScreenRect(renderer, uiCamera, out Rect screenRect))
    {
      return false;
    }

    Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(
        root.panel,
        new Vector2(screenRect.xMin, Screen.height - screenRect.yMax));
    Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(
        root.panel,
        new Vector2(screenRect.xMax, Screen.height - screenRect.yMin));

    float xMin = Mathf.Min(topLeft.x, bottomRight.x);
    float xMax = Mathf.Max(topLeft.x, bottomRight.x);
    float yMin = Mathf.Min(topLeft.y, bottomRight.y);
    float yMax = Mathf.Max(topLeft.y, bottomRight.y);
    if (xMax - xMin < 1.0f || yMax - yMin < 1.0f)
    {
      return false;
    }

    panelRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    return true;
  }

  private static LoadedMod FindLoadedModForHandler(IMod handler)
  {
    if (handler == null || API.ModLoader?.LoadedMods == null)
    {
      return null;
    }

    foreach (LoadedMod mod in API.ModLoader.LoadedMods)
    {
      if (mod?.Handlers != null && mod.Handlers.Contains(handler))
      {
        return mod;
      }
    }

    return null;
  }

  private static Transform GetUiParent()
  {
    if (Manager.camera != null && Manager.camera.uiCamera != null)
    {
      return Manager.camera.uiCamera.transform;
    }

    return API.Rendering.UICamera != null
        ? API.Rendering.UICamera.transform
        : null;
  }

  private static Camera GetUiCamera()
  {
    if (Manager.camera != null && Manager.camera.uiCamera != null)
    {
      return Manager.camera.uiCamera;
    }

    return API.Rendering.UICamera != null
        ? API.Rendering.UICamera.GetComponent<Camera>()
        : null;
  }

  private static bool TryGetRendererScreenRect(
      Renderer renderer,
      Camera camera,
      out Rect screenRect)
  {
    screenRect = default;
    if (renderer == null || camera == null)
    {
      return false;
    }

    Bounds bounds = renderer.bounds;
    Vector3 min = bounds.min;
    Vector3 max = bounds.max;
    Vector3[] corners =
    {
      new Vector3(min.x, min.y, min.z),
      new Vector3(min.x, min.y, max.z),
      new Vector3(min.x, max.y, min.z),
      new Vector3(min.x, max.y, max.z),
      new Vector3(max.x, min.y, min.z),
      new Vector3(max.x, min.y, max.z),
      new Vector3(max.x, max.y, min.z),
      new Vector3(max.x, max.y, max.z)
    };

    float xMin = float.PositiveInfinity;
    float xMax = float.NegativeInfinity;
    float yMin = float.PositiveInfinity;
    float yMax = float.NegativeInfinity;
    for (int i = 0; i < corners.Length; i++)
    {
      Vector3 screen = camera.WorldToScreenPoint(corners[i]);
      if (float.IsNaN(screen.x) || float.IsNaN(screen.y))
      {
        continue;
      }

      xMin = Mathf.Min(xMin, screen.x);
      xMax = Mathf.Max(xMax, screen.x);
      yMin = Mathf.Min(yMin, screen.y);
      yMax = Mathf.Max(yMax, screen.y);
    }

    if (float.IsInfinity(xMin) ||
        float.IsInfinity(xMax) ||
        float.IsInfinity(yMin) ||
        float.IsInfinity(yMax) ||
        float.IsNaN(xMin) ||
        float.IsNaN(xMax) ||
        float.IsNaN(yMin) ||
        float.IsNaN(yMax) ||
        xMax - xMin < 1.0f ||
        yMax - yMin < 1.0f)
    {
      return false;
    }

    screenRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    return true;
  }

  private static void SetLayerRecursive(GameObject root, int layer)
  {
    if (root == null || layer < 0)
    {
      return;
    }

    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
    for (int i = 0; i < transforms.Length; i++)
    {
      transforms[i].gameObject.layer = layer;
    }
  }
}
