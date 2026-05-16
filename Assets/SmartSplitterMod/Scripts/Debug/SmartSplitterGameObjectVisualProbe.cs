using Pug.Sprite;
using UnityEngine;

/// <summary>
/// GameObject-side visual probe for SmartSplitterMod.
///
/// Purpose:
/// - Checks whether live ConveyorBeltSplitter MonoBehaviour instances exist at runtime.
/// - Checks whether their XScaler/SpriteObject child hierarchy exists at runtime.
/// - Checks whether the Pug.Sprite.SpriteObject component and SpriteAsset reference can be read.
///
/// SDK notes:
/// - Does not use System.Reflection.
/// - Does not call GetType(), FullName, Name, FieldInfo, PropertyInfo, etc.
/// - Uses the confirmed namespace/type: Pug.Sprite.SpriteObject.
/// </summary>
public sealed class SmartSplitterGameObjectVisualProbe : MonoBehaviour
{
  private static SmartSplitterGameObjectVisualProbe _instance;

  private float _nextLogAt;

  public static void EnsureExists()
  {
    if (_instance != null)
    {
      return;
    }

    GameObject probeObject = new GameObject("SmartSplitterGameObjectVisualProbe");
    DontDestroyOnLoad(probeObject);
    _instance = probeObject.AddComponent<SmartSplitterGameObjectVisualProbe>();
  }

  private void Update()
  {
    if (!SmartSplitterDebugSettings.EnableGameObjectVisualProbe)
    {
      return;
    }

    if (Time.time < _nextLogAt)
    {
      return;
    }

    _nextLogAt = Time.time + SmartSplitterDebugSettings.GameObjectVisualProbeIntervalSeconds;

    RunProbe();
  }

  private void RunProbe()
  {
    ConveyorBeltSplitter[] splitters = Object.FindObjectsByType<ConveyorBeltSplitter>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None);

    Debug.Log(
        $"[SmartSplitterGameObjectVisualProbe] splitterCount={splitters.Length}");

    for (int i = 0; i < splitters.Length; i++)
    {
      ConveyorBeltSplitter splitter = splitters[i];

      if (splitter == null)
      {
        continue;
      }

      GameObject root = splitter.gameObject;
      Transform rootTransform = root.transform;

      Transform xScaler = rootTransform.Find("XScaler");
      Transform spriteObjectTransform = xScaler != null
          ? xScaler.Find("SpriteObject")
          : rootTransform.Find("SpriteObject");

      SpriteObject spriteObject = spriteObjectTransform != null
          ? spriteObjectTransform.GetComponent<SpriteObject>()
          : null;

      string spriteAssetText = "none";
      string materialText = "none";
      string spriteObjectGameObjectName = "none";
      bool spriteObjectActive = false;
      int spriteObjectComponentCount = 0;

      if (spriteObjectTransform != null)
      {
        spriteObjectGameObjectName = spriteObjectTransform.name;
        spriteObjectActive = spriteObjectTransform.gameObject.activeInHierarchy;
        spriteObjectComponentCount = spriteObjectTransform.GetComponents<Component>().Length;
      }

      if (spriteObject != null)
      {
        spriteAssetText = spriteObject.asset != null
            ? spriteObject.asset.name
            : "null";

        materialText = spriteObject.material != null
            ? spriteObject.material.name
            : "null";
      }

      Debug.Log(
          $"[SmartSplitterGameObjectVisualProbe] splitter[{i}] " +
          $"rootName={root.name} active={root.activeInHierarchy} " +
          $"pos=({rootTransform.position.x:0.00},{rootTransform.position.y:0.00},{rootTransform.position.z:0.00}) " +
          $"hasXScaler={xScaler != null} " +
          $"hasSpriteObjectTransform={spriteObjectTransform != null} " +
          $"spriteObjectName={spriteObjectGameObjectName} " +
          $"spriteObjectActive={spriteObjectActive} " +
          $"spriteObjectComponentCount={spriteObjectComponentCount} " +
          $"hasSpriteObjectComponent={spriteObject != null} " +
          $"spriteAsset={spriteAssetText} " +
          $"material={materialText}");

      if (!SmartSplitterDebugSettings.EnableGameObjectVisualProbeVerboseLogs)
      {
        continue;
      }

      LogChildren(rootTransform, 0, SmartSplitterDebugSettings.GameObjectVisualProbeMaxChildDepth);
    }
  }

  private void LogChildren(Transform parent, int depth, int maxDepth)
  {
    if (parent == null || depth > maxDepth)
    {
      return;
    }

    string indent = new string(' ', depth * 2);
    int componentCount = parent.GetComponents<Component>().Length;

    Debug.Log(
        $"[SmartSplitterGameObjectVisualProbe] {indent}child depth={depth} " +
        $"name={parent.name} active={parent.gameObject.activeInHierarchy} " +
        $"childCount={parent.childCount} componentCount={componentCount}");

    for (int i = 0; i < parent.childCount; i++)
    {
      LogChildren(parent.GetChild(i), depth + 1, maxDepth);
    }
  }
}
