using UnityEngine;

/// <summary>
/// SDK-safe name/path based GameObject visual hierarchy probe.
///
/// Purpose:
/// - The ConveyorBeltSplitter MonoBehaviour probe only found inactive pooled clones.
/// - This probe scans live Transform hierarchy by object names only, without reflection.
/// - It tries to discover whether active placed splitter visuals exist as GameObjects under a different root/name.
///
/// Restrictions:
/// - Does not use System.Reflection.
/// - Does not call GetType().
/// - Does not reference SpriteObject type directly.
/// - Uses only Transform/GameObject names, active state, position, and hierarchy path.
/// </summary>
public sealed class SmartSplitterGameObjectHierarchyProbe : MonoBehaviour
{
    private static SmartSplitterGameObjectHierarchyProbe _instance;

    private float _nextLogAt;

    public static void EnsureExists()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject probeObject = new GameObject("SmartSplitterGameObjectHierarchyProbe");
        DontDestroyOnLoad(probeObject);
        _instance = probeObject.AddComponent<SmartSplitterGameObjectHierarchyProbe>();
    }

    private void Update()
    {
        if (!SmartSplitterDebugSettings.EnableGameObjectHierarchyProbe)
        {
            return;
        }

        if (Time.time < _nextLogAt)
        {
            return;
        }

        _nextLogAt = Time.time + SmartSplitterDebugSettings.GameObjectHierarchyProbeIntervalSeconds;

        RunProbe();
    }

    private void RunProbe()
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int conveyorNameCount = 0;
        int activeConveyorNameCount = 0;
        int xScalerCount = 0;
        int activeXScalerCount = 0;
        int spriteObjectCount = 0;
        int activeSpriteObjectCount = 0;
        int logged = 0;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];

            if (t == null)
            {
                continue;
            }

            bool nameMatches =
                t.name == "ConveyorBeltSplitter" ||
                t.name == "ConveyorBeltSplitter(Clone)" ||
                t.name == "XScaler" ||
                t.name == "SpriteObject";

            if (!nameMatches)
            {
                continue;
            }

            bool active = t.gameObject.activeInHierarchy;

            if (t.name == "ConveyorBeltSplitter" || t.name == "ConveyorBeltSplitter(Clone)")
            {
                conveyorNameCount++;
                if (active)
                {
                    activeConveyorNameCount++;
                }
            }
            else if (t.name == "XScaler")
            {
                xScalerCount++;
                if (active)
                {
                    activeXScalerCount++;
                }
            }
            else if (t.name == "SpriteObject")
            {
                spriteObjectCount++;
                if (active)
                {
                    activeSpriteObjectCount++;
                }
            }

            if (logged >= SmartSplitterDebugSettings.GameObjectHierarchyProbeMaxLogs)
            {
                continue;
            }

            if (!SmartSplitterDebugSettings.EnableGameObjectHierarchyProbeVerboseLogs && !active)
            {
                continue;
            }

            logged++;

            Debug.Log(
                $"[SmartSplitterGameObjectHierarchyProbe] match name={t.name} " +
                $"active={active} pos=({t.position.x:0.00},{t.position.y:0.00},{t.position.z:0.00}) " +
                $"childCount={t.childCount} path={BuildPath(t)}");
        }

        Debug.Log(
            $"[SmartSplitterGameObjectHierarchyProbe] summary " +
            $"totalTransforms={transforms.Length} " +
            $"conveyorNames={conveyorNameCount} activeConveyorNames={activeConveyorNameCount} " +
            $"xScalers={xScalerCount} activeXScalers={activeXScalerCount} " +
            $"spriteObjects={spriteObjectCount} activeSpriteObjects={activeSpriteObjectCount} " +
            $"logged={logged}");
    }

    private string BuildPath(Transform transform)
    {
        if (transform == null)
        {
            return "none";
        }

        string path = transform.name;
        Transform current = transform.parent;
        int guard = 0;

        while (current != null && guard < 12)
        {
            path = current.name + "/" + path;
            current = current.parent;
            guard++;
        }

        return path;
    }
}
