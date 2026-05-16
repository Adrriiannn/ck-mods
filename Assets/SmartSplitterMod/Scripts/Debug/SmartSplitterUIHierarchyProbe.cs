using UnityEngine;

public static class SmartSplitterUIHierarchyProbe
{
  private static bool _logged;

  public static void LogOnce()
  {
    if (_logged)
    {
      return;
    }

    if (Manager.ui == null || Manager.ui.mouse == null || Manager.ui.playerInventoryUI == null)
    {
      return;
    }

    _logged = true;

    Debug.Log("[SmartSplitterUIProbe] ===== UI HIERARCHY PROBE START =====");

    LogObject("Manager.ui", Manager.ui.gameObject);
    LogObject("mouse", Manager.ui.mouse.gameObject);
    LogObject("mouse.pointer", Manager.ui.mouse.pointer != null ? Manager.ui.mouse.pointer.gameObject : null);
    LogObject("mouse.pointerSR", Manager.ui.mouse.pointerSR != null ? Manager.ui.mouse.pointerSR.gameObject : null);
    LogObject("playerInventoryUI", Manager.ui.playerInventoryUI != null ? Manager.ui.playerInventoryUI.gameObject : null);

    if (Manager.ui.filteringUI != null)
    {
      LogObject("filteringUI", Manager.ui.filteringUI.gameObject);
    }

    Debug.Log("[SmartSplitterUIProbe] ===== UI HIERARCHY PROBE END =====");
  }

  private static void LogObject(string label, GameObject go)
  {
    if (go == null)
    {
      Debug.Log($"[SmartSplitterUIProbe] {label}: null");
      return;
    }

    Transform t = go.transform;
    RectTransform rt = go.GetComponent<RectTransform>();
    Canvas canvas = go.GetComponentInParent<Canvas>();
    Canvas ownCanvas = go.GetComponent<Canvas>();

    Debug.Log(
        $"[SmartSplitterUIProbe] {label}: name={go.name} " +
        $"activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} " +
        $"type={(rt != null ? "RectTransform" : "Transform")} " +
        $"pos={t.position} local={t.localPosition} " +
        $"parent={(t.parent != null ? t.parent.name : "null")} " +
        $"sibling={t.GetSiblingIndex()} " +
        $"ownCanvas={(ownCanvas != null ? ownCanvas.name : "null")} " +
        $"parentCanvas={(canvas != null ? canvas.name : "null")}"
    );

    Transform p = t.parent;
    int depth = 0;
    while (p != null && depth < 8)
    {
      Debug.Log(
          $"[SmartSplitterUIProbe] {label}.parent[{depth}] " +
          $"name={p.name} pos={p.position} local={p.localPosition} sibling={p.GetSiblingIndex()} " +
          $"hasRect={(p.GetComponent<RectTransform>() != null)} " +
          $"hasCanvas={(p.GetComponent<Canvas>() != null)}"
      );

      p = p.parent;
      depth++;
    }
  }
}
