using System.Collections.Generic;
using Pug.Sprite;
using Unity.Entities;
using UnityEngine;

public static class SmartSplitterClientInteractionController
{
  private sealed class InteractionDriver : MonoBehaviour
  {
    private void Update()
    {
      SmartSplitterClientInteractionController.Tick();
    }
  }

  private sealed class Candidate
  {
    public Entity SplitterEntity;
    public World World;
    public GameObject Root;
    public GameObject SmartVisual;
    public Vector3 Center;
    public bool Powered;
    public float LastSeenTime;
    public readonly List<SpriteObject> HighlightSprites = new List<SpriteObject>();
    public bool Highlighted;
  }

  private struct CandidateScore
  {
    public bool Valid;
    public float Distance;
    public float SideDistance;
    public float Alignment;
    public float Score;
  }

  private static readonly Dictionary<Entity, Candidate> Candidates = new Dictionary<Entity, Candidate>();

  private static Entity _selectedEntity = Entity.Null;
  private static Entity _openEntity = Entity.Null;
  private static SmartSplitterPanelController _panelInstance;
  private static InteractionDriver _driver;

  private static int _lastUseFrame = -1;
  private static int _lastTickFrame = -1;

  private static readonly Color HighlightColor = Color.white;
  private static readonly Color TransparentOutline = new Color(0f, 0f, 0f, 0f);

  // Strict RobotArm-like interaction approximation:
  // 1. The splitter must be powered.
  // 2. The player's real distance to the splitter body must be inside the interaction range.
  // 3. The splitter must be in front of the player along the player-to-cursor line.
  // 4. The splitter body must be close to that line.
  //
  // Important: input is sampled every rendered frame by InteractionDriver, not only when the
  // visual swap controller refreshes, so pressing E while highlighted is reliable.
  private const float MaxInteractDistance = 1.35f;
  private const float PanelCloseDistance = 1.60f;
  private const float MaxSideDistance = 0.58f;
  private const float MinAlignment = 0.20f;
  private const float CandidateForgetAfterSeconds = 1.0f;
  private const float InteractionPointOffset = 0.32f;

  public static void RegisterOrUpdate(
      Entity splitterEntity,
      World world,
      GameObject splitterRoot,
      GameObject smartVisual,
      bool powered)
  {
    if (splitterEntity == Entity.Null || splitterRoot == null)
    {
      return;
    }

    EnsureDriver();

    bool created = false;

    if (!Candidates.TryGetValue(splitterEntity, out Candidate candidate))
    {
      candidate = new Candidate
      {
        SplitterEntity = splitterEntity
      };

      Candidates[splitterEntity] = candidate;
      created = true;
    }

    bool visualChanged = candidate.Root != splitterRoot || candidate.SmartVisual != smartVisual;

    candidate.World = world;
    candidate.Root = splitterRoot;
    candidate.SmartVisual = smartVisual;
    candidate.Powered = powered;
    candidate.LastSeenTime = Time.time;
    candidate.Center = splitterRoot.transform.position;

    if (created || visualChanged)
    {
      RefreshHighlightSprites(candidate);
    }

    if (!powered)
    {
      if (candidate.Highlighted)
      {
        SetHighlighted(candidate, false);
      }

      if (_selectedEntity == splitterEntity)
      {
        ClearSelection("unpowered");
      }

      if (_openEntity == splitterEntity)
      {
        ClosePanel("unpowered");
      }
    }

    if (SmartSplitterDebugSettings.EnableVisualSwapLogs && created)
    {
      Debug.Log(
          $"[SmartSplitterClientInteraction] registered splitter={splitterEntity} " +
          $"root={splitterRoot.name} highlightSprites={candidate.HighlightSprites.Count}");
    }
  }

  public static void Tick()
  {

    SmartSplitterUIHierarchyProbe.LogOnce();

    // Tick can be called by both the driver and the visual swap controller. Input must be
    // sampled every frame, but duplicate work in the same frame causes double-open/close.
    if (Time.frameCount == _lastTickFrame)
    {
      return;
    }

    _lastTickFrame = Time.frameCount;

    PruneDeadCandidates();

    PlayerController player = Manager.main != null ? Manager.main.player : null;

    bool interactPressed = WasInteractPressed(player);
    bool escapePressed = Input.GetKeyDown(KeyCode.Escape);

    // Closing must be reliable and independent of current highlight/selection.
    if (escapePressed && IsPanelOpen())
    {
      ClosePanel("escape");
      return;
    }

    if (interactPressed && IsPanelOpen())
    {
      ClosePanel("toggle");
      return;
    }

    if (player == null)
    {
      ClosePanel("no-player");
      ClearSelection("no-player");
      return;
    }

    CloseOpenPanelIfInvalidOrOutOfRange(player);

    // When the Smart Splitter panel is open, vanilla inventory/UI state owns the cursor and
    // gameplay input. Keep the currently-open context alive, but do not keep re-running
    // aim-line selection under the UI.
    if (IsPanelOpen())
    {
      return;
    }

    Candidate best = FindBestCandidate(player, out float bestDistance, out float bestSideDistance, out float bestAlignment);

    if (best == null)
    {
      ClearSelection("no-candidate");
      return;
    }

    if (_selectedEntity != best.SplitterEntity)
    {
      ClearSelection("selection-changed");
      _selectedEntity = best.SplitterEntity;
      SetHighlighted(best, true);

      Debug.Log(
          $"[SmartSplitterClientInteraction] selected splitter={best.SplitterEntity} " +
          $"distance={bestDistance:0.000} sideDistance={bestSideDistance:0.000} alignment={bestAlignment:0.000}");
    }
    else if (!best.Highlighted)
    {
      SetHighlighted(best, true);
    }

    if (interactPressed && _selectedEntity != Entity.Null)
    {
      // Authoritative rule: highlighted == selected == usable.
      // Open the panel using the same selected entity that owns the white outline.
      if (Candidates.TryGetValue(_selectedEntity, out Candidate selectedCandidate))
      {
        OpenPanel(selectedCandidate);
      }
    }
  }

  private static void EnsureDriver()
  {
    if (_driver != null)
    {
      return;
    }

    GameObject driverObject = new GameObject("SmartSplitterClientInteractionController");
    Object.DontDestroyOnLoad(driverObject);
    _driver = driverObject.AddComponent<InteractionDriver>();
  }

  private static bool WasInteractPressed(PlayerController player)
  {
    // Raw E is the primary path for the custom Smart Splitter UI. The game's own
    // interact input is only a fallback, because vanilla interaction can compete for it.
    bool pressed = Input.GetKeyDown(KeyCode.E);

    if (player != null && player.inputModule != null)
    {
      pressed |= player.inputModule.WasButtonPressedDownThisFrame(PlayerInput.InputType.INTERACT_WITH_OBJECT, false);
    }

    if (!pressed)
    {
      return false;
    }

    // Prevent duplicate handling if both raw E and the game input module fire in the same frame.
    if (Time.frameCount == _lastUseFrame)
    {
      return false;
    }

    _lastUseFrame = Time.frameCount;
    return true;
  }

  private static Candidate FindBestCandidate(
      PlayerController player,
      out float bestDistance,
      out float bestSideDistance,
      out float bestAlignment)
  {
    bestDistance = float.MaxValue;
    bestSideDistance = float.MaxValue;
    bestAlignment = -1f;

    Vector3 playerPos = player.RenderPosition;
    Vector3 aimDir = GetMouseWorldAimDirection(playerPos, player);

    if (aimDir.sqrMagnitude < 0.0001f)
    {
      return null;
    }

    Candidate best = null;
    float bestScore = float.MaxValue;

    foreach (Candidate candidate in Candidates.Values)
    {
      if (candidate == null || candidate.Root == null || !candidate.Powered)
      {
        continue;
      }

      CandidateScore score = ScoreCandidate(candidate, playerPos, aimDir);

      if (!score.Valid)
      {
        continue;
      }

      if (score.Score < bestScore)
      {
        bestScore = score.Score;
        best = candidate;
        bestDistance = score.Distance;
        bestSideDistance = score.SideDistance;
        bestAlignment = score.Alignment;
      }
    }

    return best;
  }

  private static Vector3 GetMouseWorldAimDirection(Vector3 playerPos, PlayerController player)
  {
    // Raycast the cursor through the game camera onto the ground plane, then aim from
    // player to that point. This is what fixed the stale/random axis selection.
    if (Manager.ui != null && Manager.ui.mouse != null && Manager.camera != null && Manager.camera.gameCamera != null)
    {
      Vector3 mouseGameViewPosition = Manager.ui.mouse.GetMouseGameViewPosition();
      Camera gameCamera = Manager.camera.gameCamera;
      Ray ray = gameCamera.ViewportPointToRay(gameCamera.WorldToViewportPoint(mouseGameViewPosition));
      Plane plane = new Plane(Vector3.up, 0f);

      if (plane.Raycast(ray, out float enter))
      {
        Vector3 mouseWorld = ray.origin + ray.direction * enter;
        mouseWorld -= Vector3.forward * 0.5f;

        Vector3 aim = mouseWorld - playerPos;
        aim.y = 0f;

        if (aim.sqrMagnitude > 0.0001f)
        {
          return aim.normalized;
        }
      }
    }

    // Fallback only to the player's real current aim/facing, never to artificial/cardinal directions.
    Vector3 fallback = player != null ? player.aimDirection : Vector3.zero;
    fallback.y = 0f;

    if (fallback.sqrMagnitude < 0.0001f && player != null)
    {
      fallback = player.targetingDirection;
      fallback.y = 0f;
    }

    if (fallback.sqrMagnitude < 0.0001f && player != null)
    {
      fallback = player.facingDirection.vec3;
      fallback.y = 0f;
    }

    return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.zero;
  }

  private static CandidateScore ScoreCandidate(Candidate candidate, Vector3 playerPos, Vector3 aimDir)
  {
    CandidateScore best = new CandidateScore
    {
      Valid = false,
      Distance = float.MaxValue,
      SideDistance = float.MaxValue,
      Alignment = -1f,
      Score = float.MaxValue
    };

    Vector3 center = candidate.Center;

    // Hard center range gate first. This prevents highlight/open from far away even if an
    // edge point happens to score well against the aim line.
    float centerDistance = DistanceXZ(playerPos, center);

    if (centerDistance > MaxInteractDistance || centerDistance < 0.05f)
    {
      return best;
    }

    Vector3[] points =
    {
            center,
            center + new Vector3(InteractionPointOffset, 0f, 0f),
            center + new Vector3(-InteractionPointOffset, 0f, 0f),
            center + new Vector3(0f, 0f, InteractionPointOffset),
            center + new Vector3(0f, 0f, -InteractionPointOffset)
        };

    for (int i = 0; i < points.Length; i++)
    {
      TryScorePointXZ(playerPos, points[i], aimDir, centerDistance, ref best);
    }

    return best;
  }

  private static void TryScorePointXZ(
      Vector3 playerPos,
      Vector3 target,
      Vector3 aimDir,
      float centerDistance,
      ref CandidateScore best)
  {
    Vector2 player2 = new Vector2(playerPos.x, playerPos.z);
    Vector2 target2 = new Vector2(target.x, target.z);
    Vector2 aim2 = new Vector2(aimDir.x, aimDir.z);

    if (aim2.sqrMagnitude < 0.0001f)
    {
      return;
    }

    aim2.Normalize();

    Vector2 toTarget = target2 - player2;
    float pointDistance = toTarget.magnitude;

    if (pointDistance < 0.05f)
    {
      return;
    }

    Vector2 toTargetDir = toTarget / pointDistance;
    float alignment = Vector2.Dot(aim2, toTargetDir);

    if (alignment < MinAlignment)
    {
      return;
    }

    Vector2 closestPointOnAimLine = player2 + aim2 * Vector2.Dot(toTarget, aim2);
    float sideDistance = (target2 - closestPointOnAimLine).magnitude;

    if (sideDistance > MaxSideDistance)
    {
      return;
    }

    float score =
        sideDistance * 2.0f +
        centerDistance * 0.35f +
        (1f - Mathf.Clamp01(alignment)) * 0.45f;

    if (score >= best.Score)
    {
      return;
    }

    best.Valid = true;
    best.Distance = centerDistance;
    best.SideDistance = sideDistance;
    best.Alignment = alignment;
    best.Score = score;
  }

  private static float DistanceXZ(Vector3 a, Vector3 b)
  {
    float dx = a.x - b.x;
    float dz = a.z - b.z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  private static bool IsPanelOpen()
  {
    return _panelInstance != null && _panelInstance.IsOpen && _openEntity != Entity.Null;
  }

  private static void OpenPanel(Candidate candidate)
  {
    if (candidate == null)
    {
      return;
    }

    if (!EnsurePanelInstance())
    {
      Debug.LogWarning("[SmartSplitterClientInteraction] cannot open panel reason=panel prefab unavailable");
      return;
    }

    bool exists =
        candidate.World != null &&
        candidate.World.IsCreated &&
        candidate.World.EntityManager.Exists(candidate.SplitterEntity);

    if (!exists)
    {
      Debug.LogWarning($"[SmartSplitterClientInteraction] cannot open panel reason=entity-missing splitter={candidate.SplitterEntity}");
      return;
    }

    _openEntity = candidate.SplitterEntity;
    _panelInstance.Open(candidate.SplitterEntity, candidate.World, candidate.Powered);

    Debug.Log(
        $"[SmartSplitterClientInteraction] USE_OPEN splitter={candidate.SplitterEntity} " +
        $"world={(candidate.World != null ? candidate.World.Name : "null")} exists={exists}");
  }

  private static void ClosePanel(string reason)
  {
    if (_panelInstance != null && _panelInstance.IsOpen)
    {
      _panelInstance.Close(reason);
    }

    if (_openEntity != Entity.Null)
    {
      Debug.Log($"[SmartSplitterClientInteraction] USE_CLOSE splitter={_openEntity} reason={reason}");
    }

    _openEntity = Entity.Null;
  }

  private static bool EnsurePanelInstance()
  {
    if (_panelInstance != null)
    {
      return true;
    }

    if (!SmartSplitterAssetRegistry.TryGetSmartPanelPrefab(out GameObject prefab) || prefab == null)
    {
      return false;
    }

    GameObject instance = Object.Instantiate(prefab);
    instance.name = "SmartSplitterPanel(Runtime)";
    Object.DontDestroyOnLoad(instance);

    _panelInstance = instance.GetComponent<SmartSplitterPanelController>();

    if (_panelInstance == null)
    {
      Debug.LogError("[SmartSplitterClientInteraction] SmartSplitterPanel prefab has no SmartSplitterPanelController");
      Object.Destroy(instance);
      return false;
    }

    instance.SetActive(false);
    return true;
  }

  private static void CloseOpenPanelIfInvalidOrOutOfRange(PlayerController player)
  {
    if (!IsPanelOpen())
    {
      return;
    }

    if (!Candidates.TryGetValue(_openEntity, out Candidate openCandidate) ||
        openCandidate == null ||
        openCandidate.Root == null ||
        !openCandidate.Powered)
    {
      ClosePanel("invalid-or-unpowered");
      return;
    }

    if (player == null)
    {
      ClosePanel("no-player");
      return;
    }

    // Panel-open close behavior is physical only. Losing aim/highlight does not close the UI.
    float distance = DistanceXZ(player.RenderPosition, openCandidate.Center);

    if (distance > PanelCloseDistance)
    {
      ClosePanel("out-of-range");
    }
  }

  private static void ClearSelection(string reason)
  {
    if (_selectedEntity == Entity.Null)
    {
      return;
    }

    if (Candidates.TryGetValue(_selectedEntity, out Candidate selected))
    {
      SetHighlighted(selected, false);
    }

    Debug.Log($"[SmartSplitterClientInteraction] cleared selection splitter={_selectedEntity} reason={reason}");
    _selectedEntity = Entity.Null;
  }

  private static void RefreshHighlightSprites(Candidate candidate)
  {
    candidate.HighlightSprites.Clear();

    if (candidate.SmartVisual != null)
    {
      AddSpriteObjects(candidate.HighlightSprites, candidate.SmartVisual);
    }

    // Fallback for early frames before the smart visual instance exists.
    if (candidate.HighlightSprites.Count == 0 && candidate.Root != null)
    {
      AddSpriteObjects(candidate.HighlightSprites, candidate.Root);
    }

    if (candidate.Highlighted)
    {
      ApplyOutline(candidate, HighlightColor);
    }
  }

  private static void AddSpriteObjects(List<SpriteObject> target, GameObject root)
  {
    if (target == null || root == null)
    {
      return;
    }

    SpriteObject[] spriteObjects = root.GetComponentsInChildren<SpriteObject>(true);

    if (spriteObjects == null)
    {
      return;
    }

    for (int i = 0; i < spriteObjects.Length; i++)
    {
      SpriteObject spriteObject = spriteObjects[i];

      if (spriteObject == null || target.Contains(spriteObject))
      {
        continue;
      }

      target.Add(spriteObject);
    }
  }

  private static void SetHighlighted(Candidate candidate, bool highlighted)
  {
    if (candidate == null || candidate.Highlighted == highlighted)
    {
      return;
    }

    candidate.Highlighted = highlighted;
    ApplyOutline(candidate, highlighted ? HighlightColor : TransparentOutline);
  }

  private static void ApplyOutline(Candidate candidate, Color color)
  {
    if (candidate == null)
    {
      return;
    }

    for (int i = candidate.HighlightSprites.Count - 1; i >= 0; i--)
    {
      SpriteObject spriteObject = candidate.HighlightSprites[i];

      if (spriteObject == null)
      {
        candidate.HighlightSprites.RemoveAt(i);
        continue;
      }

      spriteObject.outlineColor = color;
      spriteObject.ApplyVisualChange();
    }
  }

  private static void PruneDeadCandidates()
  {
    if (Candidates.Count == 0)
    {
      return;
    }

    List<Entity> toRemove = null;

    foreach (KeyValuePair<Entity, Candidate> pair in Candidates)
    {
      Candidate candidate = pair.Value;

      bool stale = candidate == null ||
                   candidate.Root == null ||
                   Time.time - candidate.LastSeenTime > CandidateForgetAfterSeconds;

      if (!stale && candidate.World != null && candidate.World.IsCreated)
      {
        stale = !candidate.World.EntityManager.Exists(pair.Key);
      }

      if (stale)
      {
        if (toRemove == null)
        {
          toRemove = new List<Entity>();
        }

        toRemove.Add(pair.Key);
      }
    }

    if (toRemove == null)
    {
      return;
    }

    for (int i = 0; i < toRemove.Count; i++)
    {
      Entity entity = toRemove[i];
      Candidates.Remove(entity);

      if (_selectedEntity == entity)
      {
        ClearSelection("candidate-removed");
      }

      if (_openEntity == entity)
      {
        ClosePanel("candidate-removed");
      }
    }
  }
}
