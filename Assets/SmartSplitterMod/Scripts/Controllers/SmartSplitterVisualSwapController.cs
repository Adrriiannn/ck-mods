using System.Collections.Generic;
using Pug.Automation;
using Pug.ECS.Components;
using Pug.ECS.Hybrid;
using PugMod;
using Pug.Sprite;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
  private const string UgcLitMaterialName = "UGC SpriteObject Lit";
  private const string VanillaNormalLitMaterialName =
      "SpriteObject (Lit Opaque, Use Normal)";
  private const string UseNormalPropertyName = "_UseNormal";
  private const string UseNormalKeyword = "USE_NORMAL";

  private static Material _runtimeSmartSplitterMaterial;
  private static bool _runtimeSmartSplitterMaterialResolved;
  private static bool _runtimeSmartSplitterMaterialLogged;

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
  private int _lastSpawnPowerFallbackFrame = -1;

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

  public static void HandleObjectSpawnedOnClient(
      Entity entity,
      EntityManager entityManager,
      GameObject graphicalObject)
  {
    if (!SmartSplitterDebugSettings.EnableVisualSwap)
    {
      return;
    }

    EnsureExists();
    _instance.ApplySpawnedGraphicalObject(entity, entityManager, graphicalObject);
  }

  public static void HandleObjectDespawnedOnClient(
      Entity entity,
      EntityManager entityManager,
      GameObject graphicalObject)
  {
    _instance?.CleanupDespawnedGraphicalObject(entity, graphicalObject);
  }

  private void OnEnable()
  {
    SmartSplitterNetworkState.PowerStateChanged -= HandlePowerStateChanged;
    SmartSplitterNetworkState.PowerStateChanged += HandlePowerStateChanged;
  }

  private void OnDisable()
  {
    SmartSplitterNetworkState.PowerStateChanged -= HandlePowerStateChanged;
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

    bool needsPowerFallback = NeedsPowerFallback();
    if (needsPowerFallback)
    {
      RebuildPoweredElectricityTileSet();
    }

    for (int i = _visibleSplitterStates.Count - 1; i >= 0; i--)
    {
      CachedSpriteState state = _visibleSplitterStates[i];
      if (!IsVisualStateUsable(state))
      {
        RemoveVisualStateAt(i);
        continue;
      }

      RefreshStateFromEntity(state);

      int2 center = new int2(state.SplitterX, state.SplitterY);
      bool powered = SmartSplitterNetworkState.TryGetPower(center, out bool cachedPowered)
          ? cachedPowered
          : IsSplitterPoweredByAdjacentElectricity(
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

  private void HandlePowerStateChanged(int2 center, bool powered)
  {
    if (!SmartSplitterDebugSettings.EnableVisualSwap)
    {
      return;
    }

    ApplyPowerStateNow(center, powered, allowDiscoveryRefresh: true);
  }

  private bool ApplyPowerStateNow(int2 center, bool powered, bool allowDiscoveryRefresh)
  {
    if (!TryEnsureQueries() ||
        !SmartSplitterAssetRegistry.TryGetSmartVisualPrefabAndFallbacks(
            out GameObject smartPrefab,
            out SpriteAsset smartAsset,
            out Material smartMaterial))
    {
      return false;
    }

    bool applied = false;

    for (int i = _visibleSplitterStates.Count - 1; i >= 0; i--)
    {
      CachedSpriteState state = _visibleSplitterStates[i];
      if (!IsVisualStateUsable(state))
      {
        RemoveVisualStateAt(i);
        continue;
      }

      RefreshStateFromEntity(state);
      if (state.SplitterX != center.x || state.SplitterY != center.y)
      {
        continue;
      }

      ApplyDesiredVisual(
          state,
          smartPrefab,
          smartAsset,
          smartMaterial,
          powered,
          state.SpriteVariation);

      state.HasLastPowered = true;
      state.LastPowered = powered;
      state.LastVariation = state.SpriteVariation;
      applied = true;
    }

    if (applied || !allowDiscoveryRefresh)
    {
      return applied;
    }

    RefreshSplitterVisualCache();
    return ApplyPowerStateNow(center, powered, allowDiscoveryRefresh: false);
  }

  private void ApplySpawnedGraphicalObject(
      Entity splitterEntity,
      EntityManager entityManager,
      GameObject graphicalObject)
  {
    if (graphicalObject == null ||
        splitterEntity == Entity.Null ||
        !entityManager.Exists(splitterEntity) ||
        !entityManager.HasComponent<ObjectDataCD>(splitterEntity) ||
        !entityManager.HasComponent<LocalTransform>(splitterEntity))
    {
      return;
    }

    ObjectDataCD objectData = entityManager.GetComponentData<ObjectDataCD>(splitterEntity);
    if (objectData.objectID != ObjectID.ConveyorBeltSplitter)
    {
      return;
    }

    SpriteObject vanillaSpriteObject = FindPrimarySpriteObject(graphicalObject);
    if (vanillaSpriteObject == null)
    {
      return;
    }

    LocalTransform splitterTransform = entityManager.GetComponentData<LocalTransform>(splitterEntity);
    int splitterX = Mathf.RoundToInt(splitterTransform.Position.x);
    int splitterY = Mathf.RoundToInt(splitterTransform.Position.z);
    int spriteVariation = ResolveSplitterSpriteVariation(objectData.variation);

    bool hasVisualAssets = SmartSplitterAssetRegistry.TryGetSmartVisualPrefabAndFallbacks(
        out GameObject smartPrefab,
        out SpriteAsset smartAsset,
        out Material smartMaterial);

    int2 center = new int2(splitterX, splitterY);
    bool powered = TryResolvePowerForSpawn(center, out bool resolvedPowered) && resolvedPowered;

    CachedSpriteState state = RegisterVisibleSplitterState(
        vanillaSpriteObject,
        splitterEntity,
        splitterX,
        splitterY,
        spriteVariation);

    ApplyDesiredVisual(
        state,
        smartPrefab,
        smartAsset,
        smartMaterial,
        powered && hasVisualAssets,
        spriteVariation);

    state.HasLastPowered = true;
    state.LastPowered = powered;
    state.LastVariation = spriteVariation;
  }

  private void CleanupDespawnedGraphicalObject(Entity splitterEntity, GameObject graphicalObject)
  {
    if (graphicalObject == null)
    {
      return;
    }

    SpriteObject vanillaSpriteObject = FindPrimarySpriteObject(graphicalObject);
    if (vanillaSpriteObject == null)
    {
      return;
    }

    if (_spriteStates.TryGetValue(vanillaSpriteObject, out CachedSpriteState state))
    {
      for (int i = _visibleSplitterStates.Count - 1; i >= 0; i--)
      {
        if (_visibleSplitterStates[i] == state)
        {
          RemoveVisualStateAt(i);
          return;
        }
      }

      ResetVisualState(state);
      _spriteStates.Remove(vanillaSpriteObject);
      return;
    }

    HideUntrackedSmartVisual(vanillaSpriteObject);
    if (!vanillaSpriteObject.gameObject.activeSelf)
    {
      vanillaSpriteObject.gameObject.SetActive(true);
    }
  }

  private bool NeedsPowerFallback()
  {
    for (int i = 0; i < _visibleSplitterStates.Count; i++)
    {
      CachedSpriteState state = _visibleSplitterStates[i];
      if (state == null)
      {
        continue;
      }

      if (!SmartSplitterNetworkState.TryGetPower(
              new int2(state.SplitterX, state.SplitterY),
              out _))
      {
        return true;
      }
    }

    return false;
  }

  private bool TryResolvePowerForSpawn(int2 center, out bool powered)
  {
    if (SmartSplitterNetworkState.TryGetPower(center, out powered))
    {
      return true;
    }

    if (!TryEnsureQueries())
    {
      powered = false;
      return false;
    }

    if (_lastSpawnPowerFallbackFrame != Time.frameCount)
    {
      RebuildPoweredElectricityTileSet();
      _lastSpawnPowerFallbackFrame = Time.frameCount;
    }

    powered = IsSplitterPoweredByAdjacentElectricity(
        center.x,
        center.y,
        _poweredElectricityTiles);
    return true;
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

        LocalTransform splitterTransform = splitterTransforms[i];
        int splitterX = Mathf.RoundToInt(splitterTransform.Position.x);
        int splitterY = Mathf.RoundToInt(splitterTransform.Position.z);
        int spriteVariation = ResolveSplitterSpriteVariation(splitterObjectData[i].variation);

        RegisterVisibleSplitterState(
            vanillaSpriteObject,
            splitterEntity,
            splitterX,
            splitterY,
            spriteVariation);
      }
    }
  }

  private CachedSpriteState RegisterVisibleSplitterState(
      SpriteObject vanillaSpriteObject,
      Entity splitterEntity,
      int splitterX,
      int splitterY,
      int spriteVariation)
  {
    CachedSpriteState state = GetOrCreateState(vanillaSpriteObject);
    bool targetChanged = state.Registered &&
        (state.SplitterEntity != splitterEntity ||
         state.SplitterX != splitterX ||
         state.SplitterY != splitterY);

    state.SplitterEntity = splitterEntity;
    state.SplitterX = splitterX;
    state.SplitterY = splitterY;
    state.SpriteVariation = spriteVariation;

    if (!state.Registered || targetChanged)
    {
      state.HasLastPowered = false;
    }

    if (!_visibleSplitterStates.Contains(state))
    {
      _visibleSplitterStates.Add(state);
    }

    state.Registered = true;
    TryAttachExistingSmartVisual(state);
    SmartSplitterNetworkState.RequestFilters(new int2(state.SplitterX, state.SplitterY));
    return state;
  }

  private void RefreshStateFromEntity(CachedSpriteState state)
  {
    if (state == null ||
        state.SplitterEntity == Entity.Null ||
        _world == null ||
        !_world.IsCreated ||
        !_world.EntityManager.Exists(state.SplitterEntity) ||
        !_world.EntityManager.HasComponent<ObjectDataCD>(state.SplitterEntity) ||
        !_world.EntityManager.HasComponent<LocalTransform>(state.SplitterEntity))
    {
      return;
    }

    ObjectDataCD objectData = _world.EntityManager.GetComponentData<ObjectDataCD>(state.SplitterEntity);
    LocalTransform transform = _world.EntityManager.GetComponentData<LocalTransform>(state.SplitterEntity);
    state.SplitterX = Mathf.RoundToInt(transform.Position.x);
    state.SplitterY = Mathf.RoundToInt(transform.Position.z);
    state.SpriteVariation = ResolveSplitterSpriteVariation(objectData.variation);
  }

  private static int ResolveSplitterSpriteVariation(int splitterVariation)
  {
    return SmartSplitterOrientationUtility.GetSmartSpriteVariationForForwardVariation(
        SmartSplitterOrientationUtility.GetSmartForwardVariationForPlacementVariation(
            splitterVariation));
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
      ResetVisualState(state);

      if (state.VanillaSpriteObject != null)
      {
        _spriteStates.Remove(state.VanillaSpriteObject);
      }
    }

    _visibleSplitterStates.RemoveAt(index);
  }

  private void ResetVisualState(CachedSpriteState state)
  {
    state.Registered = false;
    state.HasLastPowered = false;
    state.LastPowered = false;
    state.LastVariation = -1;

    RemoveSmartVisual(state);
    if (state.VanillaSpriteObject != null &&
        !state.VanillaSpriteObject.gameObject.activeSelf)
    {
      state.VanillaSpriteObject.gameObject.SetActive(true);
    }
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
        ResetVisualState(state);
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

    TryAttachExistingSmartVisual(state);

    if (powered)
    {
      bool changed = EnsureSmartVisual(state, smartPrefab, smartAsset, smartMaterial);
      bool forceVariantRefresh = changed;

      if (state.SmartSpriteObject != null && !state.SmartSpriteObject.gameObject.activeSelf)
      {
        state.SmartSpriteObject.gameObject.SetActive(true);
        changed = true;
        forceVariantRefresh = true;
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
        forceVariantRefresh = true;
      }

      changed |= ApplyDirectionalVariant(
          state.SmartSpriteObject,
          variation,
          forceVariantRefresh);

      state.AppliedSmartVisual = true;
      return changed;
    }

    bool restored = false;
    restored |= RemoveSmartVisual(state);

    if (!state.VanillaSpriteObject.gameObject.activeSelf)
    {
      state.VanillaSpriteObject.gameObject.SetActive(true);
      restored = true;
    }

    restored |= ApplyDirectionalVariant(
        state.VanillaSpriteObject,
        variation);

    state.AppliedSmartVisual = false;
    return restored;
  }

  private bool TryAttachExistingSmartVisual(CachedSpriteState state)
  {
    if (state == null || state.VanillaSpriteObject == null)
    {
      return false;
    }

    if (state.SmartVisualObject != null)
    {
      if (state.SmartSpriteObject == null)
      {
        state.SmartSpriteObject = state.SmartVisualObject.GetComponentInChildren<SpriteObject>(true);
      }

      return true;
    }

    Transform parent = state.VanillaSpriteObject.transform.parent;
    Transform existingSmartVisual = parent != null
        ? parent.Find("SmartSplitterVisual")
        : null;

    if (existingSmartVisual == null)
    {
      return false;
    }

    state.SmartVisualObject = existingSmartVisual.gameObject;
    state.SmartSpriteObject = state.SmartVisualObject.GetComponentInChildren<SpriteObject>(true);
    return true;
  }

  private static bool HideUntrackedSmartVisual(SpriteObject vanillaSpriteObject)
  {
    if (vanillaSpriteObject == null)
    {
      return false;
    }

    Transform parent = vanillaSpriteObject.transform.parent;
    Transform existingSmartVisual = parent != null
        ? parent.Find("SmartSplitterVisual")
        : null;

    if (existingSmartVisual == null)
    {
      return false;
    }

    GameObject smartVisualObject = existingSmartVisual.gameObject;
    bool changed = smartVisualObject.activeSelf;
    if (changed)
    {
      smartVisualObject.SetActive(false);
    }

    return changed;
  }

  private bool RemoveSmartVisual(CachedSpriteState state)
  {
    if (state == null || state.SmartVisualObject == null)
    {
      return false;
    }

    bool changed = state.SmartVisualObject.activeSelf;
    if (changed)
    {
      state.SmartVisualObject.SetActive(false);
    }

    return changed;
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
      if (!TryAttachExistingSmartVisual(state))
      {
        Transform parent = state.VanillaSpriteObject.transform.parent;
        state.SmartVisualObject = Instantiate(
            smartPrefab,
            parent);

        state.SmartVisualObject.name = "SmartSplitterVisual";

        // Parent is vanilla XScaler. Keep root identity-local. The prefab child transform stays authored.
        state.SmartVisualObject.transform.localPosition = Vector3.zero;
        state.SmartVisualObject.transform.localRotation = Quaternion.identity;
        state.SmartVisualObject.transform.localScale = Vector3.one;
      }

      if (state.SmartVisualObject != null && state.SmartSpriteObject == null)
      {
        state.SmartSpriteObject = state.SmartVisualObject.GetComponentInChildren<SpriteObject>(true);
      }

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

    Material runtimeMaterial = ResolveRuntimeSmartSplitterMaterial();
    if (runtimeMaterial != null && state.SmartSpriteObject.material != runtimeMaterial)
    {
      state.SmartSpriteObject.material = runtimeMaterial;
      changed = true;
    }

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

  private static Material ResolveRuntimeSmartSplitterMaterial()
  {
    if (_runtimeSmartSplitterMaterialResolved)
    {
      return _runtimeSmartSplitterMaterial;
    }

    if (API.Rendering == null)
    {
      return null;
    }

    _runtimeSmartSplitterMaterial =
        API.Rendering.GetMaterial(VanillaNormalLitMaterialName);

    if (_runtimeSmartSplitterMaterial == null)
    {
      _runtimeSmartSplitterMaterial =
          CreateNormalLitMaterial(API.Rendering.GetMaterial(UgcLitMaterialName));
    }

    if (_runtimeSmartSplitterMaterial == null)
    {
      return null;
    }

    _runtimeSmartSplitterMaterialResolved = true;
    if (!_runtimeSmartSplitterMaterialLogged)
    {
      string shaderName = _runtimeSmartSplitterMaterial.shader != null
          ? _runtimeSmartSplitterMaterial.shader.name
          : "null";
      Debug.Log(
          $"[SmartSplitterVisualSwap] Runtime material resolved " +
          $"requested={VanillaNormalLitMaterialName} " +
          $"resolved={_runtimeSmartSplitterMaterial.name} shader={shaderName}");
      _runtimeSmartSplitterMaterialLogged = true;
    }

    return _runtimeSmartSplitterMaterial;
  }

  private static Material CreateNormalLitMaterial(Material baseMaterial)
  {
    if (baseMaterial == null)
    {
      return null;
    }

    Material material = Object.Instantiate(baseMaterial);
    material.name = "SmartSplitter SpriteObject (Lit Opaque, Use Normal)";
    if (material.HasProperty(UseNormalPropertyName))
    {
      material.SetFloat(UseNormalPropertyName, 1.0f);
    }

    material.EnableKeyword(UseNormalKeyword);
    return material;
  }

  private static bool ApplyDirectionalVariant(
      SpriteObject spriteObject,
      int variation,
      bool force = false)
  {
    if (spriteObject == null)
    {
      return false;
    }

    variation = SmartSplitterOrientationUtility.NormalizeVariation(variation);

    if (variation == 0)
    {
      if (!force && spriteObject.currentVariantHash == 0)
      {
        return false;
      }

      spriteObject.ResetVariant();
      if (force)
      {
        spriteObject.ApplyVisualChange();
      }

      return true;
    }

    int desiredVariantIndex = variation - 1;
    if (!force && spriteObject.currentVariantIndex == desiredVariantIndex)
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
