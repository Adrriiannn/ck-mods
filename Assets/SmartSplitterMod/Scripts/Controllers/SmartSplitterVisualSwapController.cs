using System.Collections.Generic;
using Pug.Automation;
using Pug.ECS.Components;
using Pug.ECS.Hybrid;
using Pug.Sprite;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Prefab-authored powered visual swap for placed ConveyorBeltSplitter graphical objects.
///
/// This version auto-centers the instantiated SmartSplitterVisual prefab by comparing
/// the rendered bounds center of the vanilla SpriteObject and the smart SpriteObject.
/// It preserves the material authored on SmartSplitterVisual.prefab instead of forcing
/// the registry material at runtime.
/// That fixes SpriteAsset pivot/atlas/frame-origin differences without hardcoding tile offsets.
/// </summary>
public sealed class SmartSplitterVisualSwapController : MonoBehaviour
{
  private sealed class CachedSpriteState
  {
    public SpriteObject VanillaSpriteObject;
    public GameObject SmartVisualObject;
    public SpriteObject SmartSpriteObject;
    public bool AppliedSmartVisual;
    public bool HasLastPowered;
    public bool LastPowered;
    public string LastAssetName;
    public string LastMaterialName;
    public bool HasAlignedSmartVisual;
  }

  private static SmartSplitterVisualSwapController _instance;

  private readonly Dictionary<SpriteObject, CachedSpriteState> _spriteStates = new();

  private World _world;
  private EntityQuery _splitterQuery;
  private EntityQuery _electricityQuery;
  private CreateGraphicalObjectSystem _graphicalObjectSystem;

  private bool _queriesCreated;
  private bool _loggedWaitingForGraphicalSystem;
  private bool _loggedWaitingForSmartPrefab;
  private float _nextUpdateAt;

  public static void EnsureExists()
  {
    if (_instance != null)
    {
      return;
    }

    GameObject controllerObject = new GameObject("SmartSplitterVisualSwapController");
    DontDestroyOnLoad(controllerObject);
    _instance = controllerObject.AddComponent<SmartSplitterVisualSwapController>();
  }

  private void Update()
  {
    if (!SmartSplitterDebugSettings.EnableVisualSwap)
    {
      return;
    }

    if (Time.time < _nextUpdateAt)
    {
      return;
    }

    _nextUpdateAt = Time.time + SmartSplitterDebugSettings.VisualSwapIntervalSeconds;
    RunSwap();
  }

  private void RunSwap()
  {
    if (!TryEnsureQueries())
    {
      return;
    }

    if (!SmartSplitterAssetRegistry.TryGetSmartVisualPrefabAndFallbacks(
            out GameObject smartPrefab,
            out SpriteAsset smartAsset,
            out Material smartMaterial))
    {
      if (SmartSplitterDebugSettings.EnableVisualSwapLogs && !_loggedWaitingForSmartPrefab)
      {
        Debug.Log("[SmartSplitterVisualSwap:PrefabAutoCenterPrefabMaterial] waiting reason=smart visual prefab not resolved yet");
        _loggedWaitingForSmartPrefab = true;
      }

      return;
    }

    _loggedWaitingForSmartPrefab = false;

    using NativeArray<Entity> splitterEntities =
        _splitterQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ObjectDataCD> splitterObjectData =
        _splitterQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
    using NativeArray<LocalTransform> splitterTransforms =
        _splitterQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    using NativeArray<Entity> electricityEntities =
        _electricityQuery.ToEntityArray(Allocator.Temp);
    using NativeArray<ElectricityCD> electricityData =
        _electricityQuery.ToComponentDataArray<ElectricityCD>(Allocator.Temp);
    using NativeArray<LocalTransform> electricityTransforms =
        _electricityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    for (int i = 0; i < splitterEntities.Length; i++)
    {
      Entity splitterEntity = splitterEntities[i];

      if (splitterObjectData[i].objectID != ObjectID.ConveyorBeltSplitter)
      {
        continue;
      }

      if (!_graphicalObjectSystem.GameObjectLookup.TryGetValue(splitterEntity, out GameObject graphicalObject) ||
          graphicalObject == null ||
          !graphicalObject.activeInHierarchy)
      {
        continue;
      }

      SpriteObject vanillaSpriteObject = FindPrimarySpriteObject(graphicalObject);

      if (vanillaSpriteObject == null)
      {
        continue;
      }

      CachedSpriteState state = GetOrCreateState(vanillaSpriteObject);

      LocalTransform splitterTransform = splitterTransforms[i];
      int splitterX = Mathf.RoundToInt(splitterTransform.Position.x);
      int splitterY = Mathf.RoundToInt(splitterTransform.Position.z);

      bool powered = IsSplitterPoweredByAdjacentElectricity(
          splitterX,
          splitterY,
          electricityEntities,
          electricityData,
          electricityTransforms);

      bool changed = ApplyDesiredVisual(state, smartPrefab, smartAsset, smartMaterial, powered);

      SmartSplitterClientInteractionController.RegisterOrUpdate(
          splitterEntity,
          _world,
          graphicalObject,
          state.SmartVisualObject,
          powered);

      SpriteObject visibleSprite = powered && state.SmartSpriteObject != null
          ? state.SmartSpriteObject
          : state.VanillaSpriteObject;

      string currentAssetName = visibleSprite != null && visibleSprite.asset != null
          ? visibleSprite.asset.name
          : "null";
      string currentMaterialName = visibleSprite != null && visibleSprite.material != null
          ? visibleSprite.material.name
          : "null";

      if (SmartSplitterDebugSettings.EnableVisualSwapLogs &&
          (changed ||
           !state.HasLastPowered ||
           state.LastPowered != powered ||
           state.LastAssetName != currentAssetName ||
           state.LastMaterialName != currentMaterialName))
      {
        Transform smartRoot = state.SmartVisualObject != null ? state.SmartVisualObject.transform : null;
        Transform smartSpriteTransform = state.SmartSpriteObject != null ? state.SmartSpriteObject.transform : null;

        Debug.Log(
            $"[SmartSplitterVisualSwap:PrefabAutoCenterPrefabMaterial] entity={splitterEntity} root={graphicalObject.name} " +
            $"tile=({splitterX},{splitterY}) powered={powered} changed={changed} appliedSmart={state.AppliedSmartVisual} " +
            $"visibleAsset={currentAssetName} visibleMaterial={currentMaterialName} " +
            $"vanillaActive={(state.VanillaSpriteObject != null && state.VanillaSpriteObject.gameObject.activeSelf)} " +
            $"smartActive={(state.SmartVisualObject != null && state.SmartVisualObject.activeSelf)} " +
            $"smartRootLocal={(smartRoot != null ? smartRoot.localPosition.ToString() + "/" + smartRoot.localEulerAngles.ToString() : "null")} " +
            $"smartSpriteLocal={(smartSpriteTransform != null ? smartSpriteTransform.localPosition.ToString() + "/" + smartSpriteTransform.localEulerAngles.ToString() : "null")} " +
            $"aligned={state.HasAlignedSmartVisual}");
      }

      state.HasLastPowered = true;
      state.LastPowered = powered;
      state.LastAssetName = currentAssetName;
      state.LastMaterialName = currentMaterialName;
    }

    SmartSplitterClientInteractionController.Tick();
  }

  private bool TryEnsureQueries()
  {
    if (_queriesCreated &&
        _world != null &&
        _world.IsCreated &&
        _graphicalObjectSystem != null)
    {
      return true;
    }

    if (!TryFindGraphicalWorld(out World world, out CreateGraphicalObjectSystem graphicalSystem))
    {
      if (SmartSplitterDebugSettings.EnableVisualSwapLogs && !_loggedWaitingForGraphicalSystem)
      {
        Debug.Log("[SmartSplitterVisualSwap:PrefabAutoCenterPrefabMaterial] waiting reason=no CreateGraphicalObjectSystem in any active world");
        _loggedWaitingForGraphicalSystem = true;
      }

      _queriesCreated = false;
      _world = null;
      _graphicalObjectSystem = null;
      return false;
    }

    _loggedWaitingForGraphicalSystem = false;
    _world = world;
    _graphicalObjectSystem = graphicalSystem;

    EntityManager entityManager = _world.EntityManager;

    _splitterQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[]
        {
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>()
            },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _electricityQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[]
        {
                ComponentType.ReadOnly<ElectricityCD>(),
                ComponentType.ReadOnly<LocalTransform>()
            },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _queriesCreated = true;

    if (SmartSplitterDebugSettings.EnableVisualSwapLogs)
    {
      Debug.Log($"[SmartSplitterVisualSwap:PrefabAutoCenterPrefabMaterial] initialized graphical lookup world={_world.Name}");
    }

    return true;
  }

  private bool TryFindGraphicalWorld(out World world, out CreateGraphicalObjectSystem graphicalSystem)
  {
    world = null;
    graphicalSystem = null;

    if (Manager.ecs != null)
    {
      World clientWorld = Manager.ecs.ClientWorld;

      if (TryGetGraphicalSystemFromWorld(clientWorld, out graphicalSystem))
      {
        world = clientWorld;
        return true;
      }
    }

    World defaultWorld = World.DefaultGameObjectInjectionWorld;

    if (TryGetGraphicalSystemFromWorld(defaultWorld, out graphicalSystem))
    {
      world = defaultWorld;
      return true;
    }

    foreach (World candidateWorld in World.All)
    {
      if (TryGetGraphicalSystemFromWorld(candidateWorld, out graphicalSystem))
      {
        world = candidateWorld;
        return true;
      }
    }

    return false;
  }

  private bool TryGetGraphicalSystemFromWorld(World world, out CreateGraphicalObjectSystem graphicalSystem)
  {
    graphicalSystem = null;

    if (world == null || !world.IsCreated)
    {
      return false;
    }

    graphicalSystem = world.GetExistingSystemManaged<CreateGraphicalObjectSystem>();
    return graphicalSystem != null;
  }

  private SpriteObject FindPrimarySpriteObject(GameObject graphicalObject)
  {
    Transform rootTransform = graphicalObject.transform;

    Transform xScaler = rootTransform.Find("XScaler");
    Transform spriteObjectTransform = xScaler != null
        ? xScaler.Find("SpriteObject")
        : rootTransform.Find("SpriteObject");

    if (spriteObjectTransform != null)
    {
      SpriteObject directSpriteObject = spriteObjectTransform.GetComponent<SpriteObject>();

      if (directSpriteObject != null && !directSpriteObject.gameObject.name.Contains("SmartSplitterVisual"))
      {
        return directSpriteObject;
      }
    }

    SpriteObject[] spriteObjects = graphicalObject.GetComponentsInChildren<SpriteObject>(true);

    if (spriteObjects == null || spriteObjects.Length == 0)
    {
      return null;
    }

    for (int i = 0; i < spriteObjects.Length; i++)
    {
      SpriteObject spriteObject = spriteObjects[i];

      if (spriteObject != null && !spriteObject.gameObject.name.Contains("SmartSplitterVisual"))
      {
        return spriteObject;
      }
    }

    return null;
  }

  private CachedSpriteState GetOrCreateState(SpriteObject vanillaSpriteObject)
  {
    if (_spriteStates.TryGetValue(vanillaSpriteObject, out CachedSpriteState state))
    {
      return state;
    }

    state = new CachedSpriteState
    {
      VanillaSpriteObject = vanillaSpriteObject,
      AppliedSmartVisual = false,
      HasLastPowered = false,
      LastPowered = false,
      LastAssetName = vanillaSpriteObject.asset != null ? vanillaSpriteObject.asset.name : "null",
      LastMaterialName = vanillaSpriteObject.material != null ? vanillaSpriteObject.material.name : "null",
      HasAlignedSmartVisual = false
    };

    _spriteStates[vanillaSpriteObject] = state;
    return state;
  }

  private bool ApplyDesiredVisual(
      CachedSpriteState state,
      GameObject smartPrefab,
      SpriteAsset smartAsset,
      Material smartMaterial,
      bool powered)
  {
    if (state.VanillaSpriteObject == null)
    {
      return false;
    }

    if (powered)
    {
      bool changed = EnsureSmartVisual(state, smartPrefab, smartAsset, smartMaterial);

      // Align before hiding vanilla, so vanilla renderer bounds are still available.
      if (!state.HasAlignedSmartVisual)
      {
        if (TryAutoCenterSmartVisual(state, out Vector3 worldDelta))
        {
          state.SmartVisualObject.transform.position += worldDelta;
          state.HasAlignedSmartVisual = true;
          changed = true;

          if (SmartSplitterDebugSettings.EnableVisualSwapLogs)
          {
            Debug.Log($"[SmartSplitterVisualSwap:PrefabAutoCenterPrefabMaterial] auto-centered delta={worldDelta}");
          }
        }
      }

      if (state.VanillaSpriteObject.gameObject.activeSelf)
      {
        state.VanillaSpriteObject.gameObject.SetActive(false);
        changed = true;
      }

      if (state.SmartVisualObject != null && !state.SmartVisualObject.activeSelf)
      {
        state.SmartVisualObject.SetActive(true);
        changed = true;
      }

      state.AppliedSmartVisual = true;
      return changed;
    }

    bool restored = false;

    if (state.SmartVisualObject != null && state.SmartVisualObject.activeSelf)
    {
      state.SmartVisualObject.SetActive(false);
      restored = true;
    }

    if (!state.VanillaSpriteObject.gameObject.activeSelf)
    {
      state.VanillaSpriteObject.gameObject.SetActive(true);
      restored = true;
    }

    state.AppliedSmartVisual = false;
    return restored;
  }

  private bool EnsureSmartVisual(
      CachedSpriteState state,
      GameObject smartPrefab,
      SpriteAsset smartAsset,
      Material smartMaterial)
  {
    bool changed = false;

    if (state.SmartVisualObject == null || state.SmartSpriteObject == null)
    {
      state.SmartVisualObject = Instantiate(
          smartPrefab,
          state.VanillaSpriteObject.transform.parent);

      state.SmartVisualObject.name = "SmartSplitterVisual";

      // Parent is vanilla XScaler. Keep root identity-local. The prefab child transform stays authored.
      state.SmartVisualObject.transform.localPosition = Vector3.zero;
      state.SmartVisualObject.transform.localRotation = Quaternion.identity;
      state.SmartVisualObject.transform.localScale = Vector3.one;

      state.SmartSpriteObject = state.SmartVisualObject.GetComponentInChildren<SpriteObject>(true);
      changed = true;
    }

    if (state.SmartSpriteObject == null)
    {
      return changed;
    }

    // Defensive fallbacks only. The prefab should already have these assigned.
    if (state.SmartSpriteObject.currentSkin != null)
    {
      state.SmartSpriteObject.currentSkin = null;
      changed = true;
    }

    // Do NOT override the material here.
    // The SmartSplitterVisual prefab is now the source of truth for lighting/material.
    // Overriding this from the registry was forcing UGC SpriteObject Lit even when
    // the prefab's SpriteObject used SpriteObject (Lit Opaque, Use Normal).
    if (smartAsset != null && state.SmartSpriteObject.asset != smartAsset)
    {
      state.SmartSpriteObject.asset = smartAsset;
      changed = true;
    }

    if (changed)
    {
      state.SmartSpriteObject.ApplyVisualChange();
    }

    return changed;
  }

  private bool TryAutoCenterSmartVisual(CachedSpriteState state, out Vector3 worldDelta)
  {
    worldDelta = Vector3.zero;

    if (state.VanillaSpriteObject == null ||
        state.SmartVisualObject == null ||
        state.SmartSpriteObject == null)
    {
      return false;
    }

    if (!TryGetRendererBounds(state.VanillaSpriteObject.gameObject, out Bounds vanillaBounds) ||
        !TryGetRendererBounds(state.SmartVisualObject, out Bounds smartBounds))
    {
      return false;
    }

    worldDelta = vanillaBounds.center - smartBounds.center;

    // Avoid tiny jitter adjustments.
    return worldDelta.sqrMagnitude > 0.000001f;
  }

  private bool TryGetRendererBounds(GameObject root, out Bounds bounds)
  {
    bounds = default;

    if (root == null)
    {
      return false;
    }

    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

    bool found = false;

    for (int i = 0; i < renderers.Length; i++)
    {
      Renderer renderer = renderers[i];

      if (renderer == null)
      {
        continue;
      }

      if (!found)
      {
        bounds = renderer.bounds;
        found = true;
      }
      else
      {
        bounds.Encapsulate(renderer.bounds);
      }
    }

    return found;
  }

  private bool IsSplitterPoweredByAdjacentElectricity(
      int splitterX,
      int splitterY,
      NativeArray<Entity> electricityEntities,
      NativeArray<ElectricityCD> electricityData,
      NativeArray<LocalTransform> electricityTransforms)
  {
    for (int i = 0; i < electricityEntities.Length; i++)
    {
      ElectricityCD electricity = electricityData[i];

      bool canPowerSmartSplitter =
          electricity.hasEnoughElectricityToPowerStuff ||
          electricity.sourceEnergy > 0;

      if (!canPowerSmartSplitter)
      {
        continue;
      }

      LocalTransform transform = electricityTransforms[i];

      int powerX = Mathf.RoundToInt(transform.Position.x);
      int powerY = Mathf.RoundToInt(transform.Position.z);

      int dx = Mathf.Abs(powerX - splitterX);
      int dy = Mathf.Abs(powerY - splitterY);

      bool sameTile = dx == 0 && dy == 0;
      bool orthogonallyAdjacent = (dx == 1 && dy == 0) || (dx == 0 && dy == 1);

      if (sameTile || orthogonallyAdjacent)
      {
        return true;
      }
    }

    return false;
  }
}
