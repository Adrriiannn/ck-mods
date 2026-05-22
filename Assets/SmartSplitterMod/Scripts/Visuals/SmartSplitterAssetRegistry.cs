using Pug.Sprite;
using UnityEngine;

/// <summary>
/// Static registry for Smart Splitter visual assets.
///
/// Important:
/// Do not create an empty runtime SmartSplitterAssetRegistry GameObject.
/// AddComponent() on an empty runtime object calls Awake(), and an empty Awake() can overwrite
/// the valid prefab data captured from ModObjectLoaded().
/// </summary>
public sealed class SmartSplitterAssetRegistry : MonoBehaviour
{
  private static bool _hasLoadedRegistryData;
  private static DataBlockRef<SpriteAsset> _loadedSmartSplitterAsset;
  private static Material _loadedSmartSplitterMaterial;
  private static GameObject _loadedSmartSplitterVisualPrefab;
  private static GameObject _loadedSmartSplitterPanelPrefab;

  public static SmartSplitterAssetRegistry Instance { get; private set; }

  [Header("Sprite Assets")]
  public DataBlockRef<SpriteAsset> SmartSplitterAsset;

  [Header("Materials")]
  [Tooltip("Assign UGC SpriteObject Lit here. Leave empty to keep the prefab/default material.")]
  public Material SmartSplitterMaterial;

  [Header("Visual Prefabs")]
  [Tooltip("Assign Assets/SmartSplitterMod/Prefabs/SmartSplitterVisual.prefab here.")]
  public GameObject SmartSplitterVisualPrefab;

  [Tooltip("Assign Assets/SmartSplitterMod/Prefabs/SmartSplitterPanel.prefab here.")]
  public GameObject SmartSplitterPanelPrefab;

  public static void EnsureExists()
  {
    // This method intentionally does not create a new empty registry object.
    // The registry data must come from the real mod prefab through ModObjectLoaded()
    // or from an already-loaded scene/prefab instance with assigned fields.
    if (Instance != null)
    {
      return;
    }

    SmartSplitterAssetRegistry existing =
        FindFirstObjectByType<SmartSplitterAssetRegistry>(FindObjectsInactive.Include);

    if (existing != null)
    {
      Instance = existing;

      if (HasUsefulRegistryData(existing))
      {
        CaptureRegistryData(existing, "EnsureExists");
      }
    }
  }

  public static void RegisterLoadedObject(Object obj)
  {
    SmartSplitterAssetRegistry source = null;

    if (obj is SmartSplitterAssetRegistry directRegistry)
    {
      source = directRegistry;
    }
    else if (obj is GameObject gameObject)
    {
      if (LooksLikePanelPrefab(gameObject))
      {
        _loadedSmartSplitterPanelPrefab = gameObject;
        Debug.Log($"[SmartSplitterAssetRegistry] Captured SmartSplitterPanel prefab name={gameObject.name}");
      }

      source = gameObject.GetComponentInChildren<SmartSplitterAssetRegistry>(true);
    }

    if (source == null)
    {
      return;
    }

    if (!HasUsefulRegistryData(source))
    {
      Debug.Log(
          "[SmartSplitterAssetRegistry] Ignored empty registry object " +
          $"name={(obj != null ? obj.name : "null")} " +
          $"assetAddress={(source.SmartSplitterAsset.hasAddress ? source.SmartSplitterAsset.address.ToString() : "no-address")} " +
          $"material={(source.SmartSplitterMaterial != null ? source.SmartSplitterMaterial.name : "null")} " +
          $"visualPrefab={(source.SmartSplitterVisualPrefab != null ? source.SmartSplitterVisualPrefab.name : "null")}");
      return;
    }

    Instance = source;
    CaptureRegistryData(source, "ModObjectLoaded");
  }

  public static bool TryGetSmartVisual(out SpriteAsset asset, out Material material)
  {
    asset = null;
    material = _loadedSmartSplitterMaterial;

    if (!_hasLoadedRegistryData)
    {
      return false;
    }

    if (!_loadedSmartSplitterAsset.TryGet(out asset) || asset == null)
    {
      return false;
    }

    return true;
  }

  public static bool TryGetSmartVisualPrefab(out GameObject prefab)
  {
    prefab = null;

    if (!_hasLoadedRegistryData)
    {
      return false;
    }

    prefab = _loadedSmartSplitterVisualPrefab;
    return prefab != null;
  }

  public static bool TryGetSmartVisualPrefabAndFallbacks(
      out GameObject prefab,
      out SpriteAsset asset,
      out Material material)
  {
    prefab = null;
    asset = null;
    material = _loadedSmartSplitterMaterial;

    if (!_hasLoadedRegistryData)
    {
      return false;
    }

    prefab = _loadedSmartSplitterVisualPrefab;

    if (_loadedSmartSplitterAsset.TryGet(out SpriteAsset resolvedAsset))
    {
      asset = resolvedAsset;
    }

    return prefab != null;
  }

  public static bool TryGetSmartSplitterPanelPrefab(out GameObject prefab)
  {
    prefab = _loadedSmartSplitterPanelPrefab;
    return prefab != null;
  }

  private static bool HasUsefulRegistryData(SmartSplitterAssetRegistry source)
  {
    if (source == null)
    {
      return false;
    }

    return source.SmartSplitterAsset.hasAddress ||
           source.SmartSplitterMaterial != null ||
           source.SmartSplitterVisualPrefab != null ||
           source.SmartSplitterPanelPrefab != null;
  }

  private static void CaptureRegistryData(SmartSplitterAssetRegistry source, string origin)
  {
    _loadedSmartSplitterAsset = source.SmartSplitterAsset;
    _loadedSmartSplitterMaterial = source.SmartSplitterMaterial;
    _loadedSmartSplitterVisualPrefab = source.SmartSplitterVisualPrefab;
    _loadedSmartSplitterPanelPrefab = source.SmartSplitterPanelPrefab != null
        ? source.SmartSplitterPanelPrefab
        : _loadedSmartSplitterPanelPrefab;
    _hasLoadedRegistryData = true;

    SpriteAsset resolvedAsset = null;
    source.SmartSplitterAsset.TryGet(out resolvedAsset);

    Debug.Log(
        $"[SmartSplitterAssetRegistry] Captured registry data origin={origin} " +
        $"assetAddress={(source.SmartSplitterAsset.hasAddress ? source.SmartSplitterAsset.address.ToString() : "no-address")} " +
        $"resolvedAsset={(resolvedAsset != null ? resolvedAsset.name : "null")} " +
        $"material={(source.SmartSplitterMaterial != null ? source.SmartSplitterMaterial.name : "null")} " +
        $"visualPrefab={(source.SmartSplitterVisualPrefab != null ? source.SmartSplitterVisualPrefab.name : "null")} " +
        $"panelPrefab={(_loadedSmartSplitterPanelPrefab != null ? _loadedSmartSplitterPanelPrefab.name : "null")}");
  }

  private static bool LooksLikePanelPrefab(GameObject gameObject)
  {
    if (gameObject == null)
    {
      return false;
    }

    return gameObject.name.StartsWith("SmartSplitterPanel") ||
           gameObject.GetComponentInChildren<SmartSplitterFilterPanelController>(true) != null ||
           FindChildByName(gameObject.transform, "PanelRoot") != null;
  }

  private static Transform FindChildByName(Transform root, string childName)
  {
    if (root == null)
    {
      return null;
    }

    if (root.name == childName)
    {
      return root;
    }

    for (int i = 0; i < root.childCount; i++)
    {
      Transform found = FindChildByName(root.GetChild(i), childName);
      if (found != null)
      {
        return found;
      }
    }

    return null;
  }

  private void Awake()
  {
    Instance = this;

    if (HasUsefulRegistryData(this))
    {
      CaptureRegistryData(this, "Awake");
    }
    else
    {
      Debug.Log("[SmartSplitterAssetRegistry] Awake ignored empty registry instance");
    }
  }

  private void OnDestroy()
  {
    if (Instance == this)
    {
      Instance = null;
    }
  }
}
