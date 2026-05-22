using Unity.Entities;
using UnityEngine;

/// <summary>
/// Temporary vertical-slice controller for proving Smart Splitter lane filter state.
/// This is not the production UI; it only gives us a safe way to write/read the ECS state
/// before building the Robot Arm-style panel.
/// </summary>
public sealed class SmartSplitterLaneFilterVerticalSliceController : MonoBehaviour
{
  private static SmartSplitterLaneFilterVerticalSliceController _instance;

  private Rect _windowRect = new Rect(24.0f, 160.0f, 380.0f, 260.0f);

  public static void EnsureExists()
  {
    if (_instance != null)
    {
      return;
    }

    GameObject controllerObject = new GameObject("SmartSplitterLaneFilterVerticalSliceController");
    DontDestroyOnLoad(controllerObject);
    _instance = controllerObject.AddComponent<SmartSplitterLaneFilterVerticalSliceController>();
  }

  private void Update()
  {
    if (!SmartSplitterDebugSettings.EnableLaneFilterVerticalSlice)
    {
      return;
    }

    if (Input.GetKeyDown(SmartSplitterDebugSettings.LaneFilterVerticalSliceCycleLeftKey) &&
        SmartSplitterLaneFilterUtility.TryFindNearestSmartSplitter(out World world, out Entity splitter, out float distance))
    {
      CycleLane(world.EntityManager, splitter, SmartSplitterLane.Left, distance);
    }
  }

  private void OnGUI()
  {
    if (!SmartSplitterDebugSettings.EnableLaneFilterVerticalSlice)
    {
      return;
    }

    _windowRect = GUILayout.Window(
        9301,
        _windowRect,
        DrawWindow,
        "Smart Splitter Filter Slice");
  }

  private void DrawWindow(int windowId)
  {
    if (!SmartSplitterLaneFilterUtility.TryFindNearestSmartSplitter(out World world, out Entity splitter, out float distance))
    {
      GUILayout.Label("No Smart Splitter within range.");
      GUILayout.Label($"Stand within {SmartSplitterLaneFilterUtility.DefaultTargetRadius:0.0} tiles of a powered or known splitter.");
      GUI.DragWindow();
      return;
    }

    EntityManager entityManager = world.EntityManager;
    SmartSplitterLaneFiltersCD filters = entityManager.GetComponentData<SmartSplitterLaneFiltersCD>(splitter);

    GUILayout.Label($"Target: {splitter} ({distance:0.00} tiles)");
    GUILayout.Label($"Left: {SmartSplitterLaneFilterUtility.FormatFilter(filters.Left)}");
    GUILayout.Label($"Center: {SmartSplitterLaneFilterUtility.FormatFilter(filters.Center)}");
    GUILayout.Label($"Right: {SmartSplitterLaneFilterUtility.FormatFilter(filters.Right)}");

    if (GUILayout.Button("Cycle Left: Any -> None -> Dirt -> Any"))
    {
      CycleLane(entityManager, splitter, SmartSplitterLane.Left, distance);
    }

    if (GUILayout.Button("Cycle Center: Any -> None -> Dirt -> Any"))
    {
      CycleLane(entityManager, splitter, SmartSplitterLane.Center, distance);
    }

    if (GUILayout.Button("Cycle Right: Any -> None -> Dirt -> Any"))
    {
      CycleLane(entityManager, splitter, SmartSplitterLane.Right, distance);
    }

    GUILayout.Label($"Hotkey: {SmartSplitterDebugSettings.LaneFilterVerticalSliceCycleLeftKey} cycles Left");
    GUI.DragWindow();
  }

  private static void CycleLane(
      EntityManager entityManager,
      Entity splitter,
      SmartSplitterLane lane,
      float distance)
  {
    SmartSplitterLaneFiltersCD filters = entityManager.GetComponentData<SmartSplitterLaneFiltersCD>(splitter);
    SmartSplitterLaneFilter current = SmartSplitterLaneFilterUtility.GetLaneFilter(filters, lane);
    SmartSplitterLaneFilter next = SmartSplitterLaneFilterUtility.GetNextProofFilter(current);
    SmartSplitterLaneFilterUtility.SetLaneFilter(entityManager, splitter, lane, next);

    filters = entityManager.GetComponentData<SmartSplitterLaneFiltersCD>(splitter);

    Debug.Log(
        $"[SmartSplitterFilterSlice] target={splitter} lane={lane} distance={distance:0.00} " +
        $"left={SmartSplitterLaneFilterUtility.FormatFilter(filters.Left)} " +
        $"center={SmartSplitterLaneFilterUtility.FormatFilter(filters.Center)} " +
        $"right={SmartSplitterLaneFilterUtility.FormatFilter(filters.Right)}");
  }
}
