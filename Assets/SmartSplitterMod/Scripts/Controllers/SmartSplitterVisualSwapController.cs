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
/// It preserves the material authored on SmartSplitterVisual.prefab instead of forcing
/// the registry material at runtime, and applies the placed splitter variation to the
/// Smart Splitter sprite asset using the same index-based convention as the vanilla
/// SpriteVariationFromEntityVariation component.
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
    public int LastVariation = -1;
    public string LastAssetName;
    public string LastMaterialName;
    public Entity SplitterEntity;
    public bool Registered;
    public int SplitterX;
    public int SplitterY;
    public int SpriteVariation;
  }

  private static SmartSplitterVisualSwapController _instance;

  private const float SplitterDiscoveryIntervalSeconds = 1.0f;

  private readonly Dictionary<SpriteObject, CachedSpriteState> _spriteStates = new();
  private readonly List<CachedSpriteState> _visibleSplitterStates = new();
  private readonly HashSet<long> _poweredElectricityTiles = new();

  private World _world;
  private EntityQuery _splitterQuery;
  private EntityQuery _electricityQuery;
  private CreateGraphicalObjectSystem _graphicalObjectSystem;

  private bool _queriesCreated;
  private bool _loggedWaitingForGraphicalSystem;
  private bool _loggedWaitingForSmartPrefab;
  private float _nextUpdateAt;
  private float _nextDiscoveryAt;

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

    float intervalSeconds = SmartSplitterDebugSettings.VisualSwapIntervalSeconds;
    if (intervalSeconds > 0.0f && Time.time < _nextUpdateAt)
    {
      return;
    }

    _nextUpdateAt = Time.time + intervalSeconds;
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
        Debug.Log("[SmartSplitterVisualSwap:PrefabVariant] waiting reason=smart visual prefab not resolved yet");
        _loggedWaitingForSmartPrefab = true;
      }

      return;
    }

    _loggedWaitingForSmartPrefab = false;

    if (_visibleSplitterStates.Count == 0 || Time.time >= _nextDiscoveryAt)
    {
      RefreshSplitterVisualCache();
      _nextDiscoveryAt = Time.time + SplitterDiscoveryIntervalSeconds;
    }

    RebuildPoweredElectricityTileSet();

    for (int i = _visibleSplitterStates.Count - 1; i >= 0; i--)
    {
      CachedSpriteState state = _visibleSplitterStates[i];
      if (!IsVisualStateUsable(state))
      {
        RemoveVisualStateAt(i);
        continue;
      }

      bool powered = IsSplitterPoweredByAdjacentElectricity(
          state.SplitterX,
          state.SplitterY,
          _poweredElectricityTiles);

      bool changed = ApplyDesiredVisual(
          state,
          smartPrefab,
          smartAsset,
          smartMaterial,
          powered,
          state.SpriteVariation);

      if (SmartSplitterDebugSettings.EnableVisualSwapLogs)
      {
        SpriteObject visibleSprite = powered && state.SmartSpriteObject != null
            ? state.SmartSpriteObject
            : state.VanillaSpriteObject;

        string currentAssetName = visibleSprite != null && visibleSprite.asset != null
            ? visibleSprite.asset.name
            : "null";
        string currentMaterialName = visibleSprite != null && visibleSprite.material != null
            ? visibleSprite.material.name
            : "null";

        if (changed ||
            !state.HasLastPowered ||
            state.LastPowered != powered ||
            state.LastVariation != state.SpriteVariation ||
            state.LastAssetName != currentAssetName ||
            state.LastMaterialName != currentMaterialName)
        {
          Transform smartRoot = state.SmartVisualObject != null ? state.SmartVisualObject.transform : null;
          Transform smartSpriteTransform = state.SmartSpriteObject != null ? state.SmartSpriteObject.transform : null;

          Debug.Log(
              $"[SmartSplitterVisualSwap:PrefabVariant] entity={state.SplitterEntity} root={state.VanillaSpriteObject.gameObject.transform.root.name} " +
              $"tile=({state.SplitterX},{state.SplitterY}) spriteVariation={state.SpriteVariation} " +
              $"powered={powered} changed={changed} appliedSmart={state.AppliedSmartVisual} " +
              $"visibleAsset={currentAssetName} visibleMaterial={currentMaterialName} " +
              $"vanillaActive={(state.VanillaSpriteObject != null && state.VanillaSpriteObject.gameObject.activeSelf)} " +
              $"smartActive={(state.SmartVisualObject != null && state.SmartVisualObject.activeSelf)} " +
              $"smartRootLocal={(smartRoot != null ? smartRoot.localPosition.ToString() + "/" + smartRoot.localEulerAngles.ToString() : "null")} " +
              $"smartSpriteLocal={(smartSpriteTransform != null ? smartSpriteTransform.localPosition.ToString() + "/" + smartSpriteTransform.localEulerAngles.ToString() : "null")}");
        }

        state.LastAssetName = currentAssetName;
        state.LastMaterialName = currentMaterialName;
      }

      state.HasLastPowered = true;
      state.LastPowered = powered;
      state.LastVariation = state.SpriteVariation;
    }
  }

  private void RefreshSplitterVisualCache()
  {
    if (_world == null || !_world.IsCreated)
    {
      return;
    }

    EntityManager entityManager = _world.EntityManager;
    EntityTypeHandle entityType = entityManager.GetEntityTypeHandle();
    ComponentTypeHandle<ObjectDataCD> objectDataType =
        entityManager.GetComponentTypeHandle<ObjectDataCD>(true);
    ComponentTypeHandle<LocalTransform> transformType =
        entityManager.GetComponentTypeHandle<LocalTransform>(true);

    using NativeArray<ArchetypeChunk> chunks = _splitterQuery.ToArchetypeChunkArray(Allocator.Temp);

    for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
    {
      ArchetypeChunk chunk = chunks[chunkIndex];
      NativeArray<Entity> splitterEntities = chunk.GetNativeArray(entityType);
      NativeArray<ObjectDataCD> splitterObjectData = chunk.GetNativeArray(ref objectDataType);
      NativeArray<LocalTransform> splitterTransforms = chunk.GetNativeArray(ref transformType);

      for (int i = 0; i < chunk.Count; i++)
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
        int placedVariation = SmartSplitterOrientationUtility.NormalizeVariation(splitterObjectData[i].variation);

        state.SplitterEntity = splitterEntity;
        state.SplitterX = Mathf.RoundToInt(splitterTransform.Position.x);
        state.SplitterY = Mathf.RoundToInt(splitterTransform.Position.z);
        state.SpriteVariation = SmartSplitterOrientationUtility.GetSmartSpriteVariationForPlacedVariation(placedVariation);

        if (state.Registered)
        {
          continue;
        }

        state.Registered = true;
        _visibleSplitterStates.Add(state);
      }
    }
  }

  private bool IsVisualStateUsable(CachedSpriteState state)
  {
    if (state == null ||
        state.VanillaSpriteObject == null ||
        state.SplitterEntity == Entity.Null ||
        _world == null ||
        !_world.IsCreated)
    {
      return false;
    }

    return _world.EntityManager.Exists(state.SplitterEntity);
  }

  private void RemoveVisualStateAt(int index)
  {
    CachedSpriteState state = _visibleSplitterStates[index];
    if (state != null)
    {
      state.Registered = false;
      if (state.VanillaSpriteObject != null)
      {
        _spriteStates.Remove(state.VanillaSpriteObject);
      }
    }

    _visibleSplitterStates.RemoveAt(index);
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
        Debug.Log("[SmartSplitterVisualSwap:PrefabVariant] waiting reason=no CreateGraphicalObjectSystem in any active world");
        _loggedWaitingForGraphicalSystem = true;
      }

      _queriesCreated = false;
      _world = null;
      _graphicalObjectSystem = null;
      ClearVisualStateCache();
      return false;
    }

    _loggedWaitingForGraphicalSystem = false;
    bool worldChanged = _world != world;
    _world = world;
    _graphicalObjectSystem = graphicalSystem;
    if (worldChanged)
    {
      ClearVisualStateCache();
      _nextDiscoveryAt = 0.0f;
    }

    EntityManager entityManager = _world.EntityManager;

    _splitterQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
    {
      All = new[]
        {
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>()
            },
      None = new[]
        {
                ComponentType.ReadOnly<EntityDestroyedCD>()
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
      None = new[]
        {
                ComponentType.ReadOnly<EntityDestroyedCD>()
            },
      Options = EntityQueryOptions.IncludeDisabledEntities
    });

    _queriesCreated = true;

    if (SmartSplitterDebugSettings.EnableVisualSwapLogs)
    {
      Debug.Log($"[SmartSplitterVisualSwap:PrefabVariant] initialized graphical lookup world={_world.Name}");
    }

    return true;
  }

  private void ClearVisualStateCache()
  {
    for (int i = 0; i < _visibleSplitterStates.Count; i++)
    {
      CachedSpriteState state = _visibleSplitterStates[i];
      if (state != null)
      {
        state.Registered = false;
      }
    }

    _visibleSplitterStates.Clear();
    _spriteStates.Clear();
    _poweredElectricityTiles.Clear();
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
      LastVariation = -1,
      LastAssetName = vanillaSpriteObject.asset != null ? vanillaSpriteObject.asset.name : "null",
      LastMaterialName = vanillaSpriteObject.material != null ? vanillaSpriteObject.material.name : "null"
    };

    _spriteStates[vanillaSpriteObject] = state;
    return state;
  }

  private bool ApplyDesiredVisual(
      CachedSpriteState state,
      GameObject smartPrefab,
      SpriteAsset smartAsset,
      Material smartMaterial,
      bool powered,
      int variation)
  {
    if (state.VanillaSpriteObject == null)
    {
      return false;
    }

    if (powered)
    {
      bool changed = EnsureSmartVisual(state, smartPrefab, smartAsset, smartMaterial);
      changed |= ApplyDirectionalVariant(state.SmartSpriteObject, variation);

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
      Transform parent = state.VanillaSpriteObject.transform.parent;
      Transform existingSmartVisual = parent != null
          ? parent.Find("SmartSplitterVisual")
          : null;

      if (existingSmartVisual != null)
      {
        state.SmartVisualObject = existingSmartVisual.gameObject;
      }
      else
      {
        state.SmartVisualObject = Instantiate(
            smartPrefab,
            parent);

        state.SmartVisualObject.name = "SmartSplitterVisual";

        // Parent is vanilla XScaler. Keep root identity-local. The prefab child transform stays authored.
        state.SmartVisualObject.transform.localPosition = Vector3.zero;
        state.SmartVisualObject.transform.localRotation = Quaternion.identity;
        state.SmartVisualObject.transform.localScale = Vector3.one;
      }

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

  private static bool ApplyDirectionalVariant(SpriteObject spriteObject, int variation)
  {
    if (spriteObject == null)
    {
      return false;
    }

    variation = SmartSplitterOrientationUtility.NormalizeVariation(variation);

    if (variation == 0)
    {
      if (spriteObject.currentVariantHash == 0)
      {
        return false;
      }

      spriteObject.ResetVariant();
      return true;
    }

    int desiredVariantIndex = variation - 1;
    if (spriteObject.currentVariantIndex == desiredVariantIndex)
    {
      return false;
    }

    spriteObject.SetVariantByIndex(variation);
    return true;
  }

  private bool IsSplitterPoweredByAdjacentElectricity(
      int splitterX,
      int splitterY,
      HashSet<long> poweredElectricityTiles)
  {
    return poweredElectricityTiles.Contains(GetTileKey(splitterX, splitterY)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX + 1, splitterY)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX - 1, splitterY)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX, splitterY + 1)) ||
           poweredElectricityTiles.Contains(GetTileKey(splitterX, splitterY - 1));
  }

  private void RebuildPoweredElectricityTileSet()
  {
    _poweredElectricityTiles.Clear();

    if (_world == null || !_world.IsCreated)
    {
      return;
    }

    EntityManager entityManager = _world.EntityManager;
    ComponentTypeHandle<ElectricityCD> electricityType =
        entityManager.GetComponentTypeHandle<ElectricityCD>(true);
    ComponentTypeHandle<LocalTransform> transformType =
        entityManager.GetComponentTypeHandle<LocalTransform>(true);

    using NativeArray<ArchetypeChunk> chunks = _electricityQuery.ToArchetypeChunkArray(Allocator.Temp);

    for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
    {
      ArchetypeChunk chunk = chunks[chunkIndex];
      NativeArray<ElectricityCD> electricityData = chunk.GetNativeArray(ref electricityType);
      NativeArray<LocalTransform> electricityTransforms = chunk.GetNativeArray(ref transformType);

      for (int i = 0; i < chunk.Count; i++)
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

        _poweredElectricityTiles.Add(GetTileKey(powerX, powerY));
      }
    }
  }

  private static long GetTileKey(int x, int y)
  {
    return ((long)x << 32) ^ (uint)y;
  }
}
