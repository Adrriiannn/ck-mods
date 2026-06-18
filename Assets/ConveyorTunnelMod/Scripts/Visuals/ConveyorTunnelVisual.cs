using System.Collections.Generic;
using PugMod;
using Pug.Sprite;
using Unity.Mathematics;
using UnityEngine;

public sealed class ConveyorTunnelVisual : EntityMonoBehaviour
{
  private const int DirectionCount = 4;
  private const int ConnectedEntranceVariantIndex = 1;
  private const int ConnectedExitVariantIndex = 2;
  private const int TerminatedEntranceVariantIndex = 3;
  private const int TerminatedExitVariantIndex = 4;
  private const string UgcLitMaterialName = "UGC SpriteObject Lit";
  private const string VanillaNormalLitMaterialName =
      "SpriteObject (Lit Opaque, Use Normal)";
  private const string UseNormalPropertyName = "_UseNormal";
  private const string UseNormalKeyword = "USE_NORMAL";

  private static readonly Dictionary<long, int2> TunnelDirectionsByTile =
      new Dictionary<long, int2>();
  private static readonly Dictionary<long, ConveyorTunnelVisual> TunnelVisualsByTile =
      new Dictionary<long, ConveyorTunnelVisual>();
  private static Material _runtimeTunnelMaterial;
  private static bool _runtimeTunnelMaterialResolved;
  private static bool _runtimeTunnelMaterialLogged;
  private static bool _prefabSpriteMaterialLogged;
  private SpriteObject _spriteObject;
  private int _lastAnimationIndex = -1;
  private int _lastVariantIndex = -1;
  private bool _runtimeMaterialApplied;
  private bool _hasRegisteredTile;
  private int2 _registeredTile;
  private int2 _registeredDirection;

  protected override void Awake()
  {
    base.Awake();
    ApplyRuntimeMaterial(ResolveSpriteObject());
  }

  private void OnEnable()
  {
    ConveyorTunnelNetworkState.EndpointStateChanged -= HandleEndpointStateChanged;
    ConveyorTunnelNetworkState.EndpointStateChanged += HandleEndpointStateChanged;
    _lastAnimationIndex = -1;
    _lastVariantIndex = -1;
  }

  private void OnDisable()
  {
    ConveyorTunnelNetworkState.EndpointStateChanged -= HandleEndpointStateChanged;
    SetInteractionOutline(false);
    UnregisterTunnelTile();
  }

  public override void OnOccupied()
  {
    base.OnOccupied();
    RefreshTunnelRegistry(force: true);
    ApplyVisual(force: true);
    SyncConveyorAnimationTime();
  }

  public override void ManagedLateUpdate()
  {
    base.ManagedLateUpdate();
    RefreshTunnelRegistry(force: false);
    ApplyVisual(force: false);
    SyncConveyorAnimationTime();
  }

  public override void OnFree()
  {
    SetInteractionOutline(false);
    UnregisterTunnelTile();
    base.OnFree();
  }

  public static bool TryGetTunnelDirectionAtTile(int2 tile, out int2 direction)
  {
    return TunnelDirectionsByTile.TryGetValue(GetTileKey(tile), out direction);
  }

  public static void ClearRegisteredTunnels()
  {
    foreach (ConveyorTunnelVisual visual in TunnelVisualsByTile.Values)
    {
      if (visual != null)
      {
        visual.SetInteractionOutline(false);
      }
    }

    TunnelDirectionsByTile.Clear();
    TunnelVisualsByTile.Clear();
  }

  public static void GetRegisteredTunnelTiles(List<int2> tiles)
  {
    tiles.Clear();
    foreach (long key in TunnelVisualsByTile.Keys)
    {
      tiles.Add(TileFromKey(key));
    }
  }

  public static void SetInteractionOutlineAtTile(int2 tile, bool show)
  {
    if (TunnelVisualsByTile.TryGetValue(
            GetTileKey(tile),
            out ConveyorTunnelVisual visual) &&
        visual != null)
    {
      visual.SetInteractionOutline(show);
    }
  }

  public static void RegisterLoadedObject(Object obj)
  {
    // Reserved for visual assets that need runtime registration.
  }

  private void HandleEndpointStateChanged(int2 tile, ConveyorTunnelEndpointVisualState state)
  {
    if (TryGetCurrentTile(out int2 currentTile) && currentTile.Equals(tile))
    {
      ApplyVisual(force: true);
    }
  }

  private void ApplyVisual(bool force)
  {
    SpriteObject spriteObject = ResolveSpriteObject();
    if (spriteObject == null || spriteObject.asset == null)
    {
      return;
    }

    int animationIndex = GetAnimationIndex();
    int variantIndex = GetVariantIndex();
    bool changed = false;

    if (force || _lastAnimationIndex != animationIndex ||
        _lastVariantIndex != variantIndex ||
        spriteObject.currentAnimationIndex != animationIndex ||
        spriteObject.currentVariantIndex != GetSpriteObjectVariantIndex(variantIndex))
    {
      changed |= TryPlayAnimation(spriteObject, animationIndex, variantIndex);
      _lastAnimationIndex = animationIndex;
      _lastVariantIndex = variantIndex;
    }

    if (changed)
    {
      spriteObject.ApplyVisualChange();
    }
  }

  private SpriteObject ResolveSpriteObject()
  {
    if (_spriteObject != null)
    {
      ApplyRuntimeMaterial(_spriteObject);
      return _spriteObject;
    }

    if (spriteObjects != null)
    {
      for (int i = 0; i < spriteObjects.Count; i++)
      {
        if (spriteObjects[i] != null)
        {
          _spriteObject = spriteObjects[i];
          ApplyRuntimeMaterial(_spriteObject);
          return _spriteObject;
        }
      }
    }

    Transform xScaler = transform.Find("XScaler");
    Transform spriteTransform = xScaler != null
        ? xScaler.Find("SpriteObject")
        : transform.Find("SpriteObject");

    _spriteObject = spriteTransform != null
        ? spriteTransform.GetComponent<SpriteObject>()
        : GetComponentInChildren<SpriteObject>(true);

    ApplyRuntimeMaterial(_spriteObject);
    return _spriteObject;
  }

  private void ApplyRuntimeMaterial(SpriteObject spriteObject)
  {
    if (_runtimeMaterialApplied || spriteObject == null)
    {
      return;
    }

    Material material = ResolveRuntimeTunnelMaterial();
    if (material != null)
    {
      bool changed = false;
      if (spriteObject.material != material)
      {
        spriteObject.material = material;
        changed = true;
      }

      if (spriteObject.color != Color.white)
      {
        spriteObject.color = Color.white;
        changed = true;
      }

      if (changed)
      {
        spriteObject.ApplyVisualChange();
      }

      _runtimeMaterialApplied = true;
      return;
    }

    if (spriteObject.material != null)
    {
      if (!_prefabSpriteMaterialLogged)
      {
        string shaderName = spriteObject.material.shader != null
            ? spriteObject.material.shader.name
            : "null";
        Debug.Log(
            $"[ConveyorTunnelVisual] Using prefab material " +
            $"material={spriteObject.material.name} shader={shaderName}");
        _prefabSpriteMaterialLogged = true;
      }

      return;
    }

    material = ResolveFallbackUgcLitMaterial();
    if (material == null)
    {
      return;
    }

    if (spriteObject.material != material)
    {
      spriteObject.material = material;
      spriteObject.ApplyVisualChange();
    }

    _runtimeMaterialApplied = true;
  }

  private static Material ResolveRuntimeTunnelMaterial()
  {
    if (_runtimeTunnelMaterialResolved)
    {
      return _runtimeTunnelMaterial;
    }

    if (API.Rendering == null)
    {
      return null;
    }

    _runtimeTunnelMaterial =
        API.Rendering.GetMaterial(VanillaNormalLitMaterialName);

    if (_runtimeTunnelMaterial == null)
    {
      _runtimeTunnelMaterial =
          CreateNormalLitMaterial(API.Rendering.GetMaterial(UgcLitMaterialName));
    }

    if (_runtimeTunnelMaterial == null)
    {
      return null;
    }

    _runtimeTunnelMaterialResolved = true;
    if (!_runtimeTunnelMaterialLogged)
    {
      string resolvedName =
          _runtimeTunnelMaterial != null
              ? _runtimeTunnelMaterial.name
              : "null";
      string shaderName =
          _runtimeTunnelMaterial != null && _runtimeTunnelMaterial.shader != null
              ? _runtimeTunnelMaterial.shader.name
              : "null";
      Debug.Log(
          $"[ConveyorTunnelVisual] Runtime material resolved requested={VanillaNormalLitMaterialName} resolved={resolvedName} shader={shaderName}");
      _runtimeTunnelMaterialLogged = true;
    }

    return _runtimeTunnelMaterial;
  }

  private static Material CreateNormalLitMaterial(Material baseMaterial)
  {
    if (baseMaterial == null)
    {
      return null;
    }

    Material material = Object.Instantiate(baseMaterial);
    material.name = "ConveyorTunnel SpriteObject (Lit Opaque, Use Normal)";
    if (material.HasProperty(UseNormalPropertyName))
    {
      material.SetFloat(UseNormalPropertyName, 1.0f);
    }

    material.EnableKeyword(UseNormalKeyword);
    return material;
  }

  private static Material ResolveFallbackUgcLitMaterial()
  {
    if (API.Rendering == null)
    {
      return null;
    }

    return API.Rendering.GetMaterial(UgcLitMaterialName);
  }

  private static bool TryPlayAnimation(
      SpriteObject spriteObject,
      int animationIndex,
      int variantIndex)
  {
    SpriteAsset asset = spriteObject.asset;
    if (asset == null ||
        !asset.hasAnimations ||
        animationIndex < 0 ||
        animationIndex >= asset.animationCount)
    {
      return false;
    }

    int animationHash = asset.GetAnimationHash(animationIndex);
    if (!spriteObject.HasAnimation(animationHash))
    {
      return false;
    }

    int variantHash = GetAnimationVariantHash(asset, animationIndex, variantIndex);
    spriteObject.PlayAnimation(
        animationHash,
        variantHash,
        forceResetTime: false,
        skipTransition: true);
    return true;
  }

  private static int GetAnimationVariantHash(
      SpriteAsset asset,
      int animationIndex,
      int variantIndex)
  {
    if (asset == null ||
        animationIndex < 0 ||
        animationIndex >= asset.animationCount)
    {
      return 0;
    }

    FrameAnimation animation = asset.GetAnimationAt(animationIndex);
    int spriteObjectVariantIndex = GetSpriteObjectVariantIndex(variantIndex);
    if (animation == null ||
        spriteObjectVariantIndex < 0 ||
        spriteObjectVariantIndex >= animation.variantCount)
    {
      return 0;
    }

    return animation.GetVariantHash(spriteObjectVariantIndex);
  }

  private static int GetSpriteObjectVariantIndex(int variantIndex)
  {
    return variantIndex - 1;
  }

  private int GetAnimationIndex()
  {
    int rawVariation = entityExist ? variation : 0;
    if (rawVariation < 0)
    {
      return 0;
    }

    return rawVariation % DirectionCount;
  }

  private int GetVariantIndex()
  {
    ConveyorTunnelEndpointRole role = ConveyorTunnelEndpointRole.Entrance;
    int2 direction = ConveyorTunnelDirectionUtility.GetDirectionFromVariation(GetAnimationIndex());
    bool connectedToBelt = false;

    if (TryGetCurrentTile(out int2 tile))
    {
      if (ConveyorTunnelNetworkState.TryGetEndpointState(
              tile,
              out ConveyorTunnelEndpointVisualState endpointState))
      {
        if (endpointState.Role != ConveyorTunnelEndpointRole.None)
        {
          role = endpointState.Role;
        }

        if (!endpointState.Direction.Equals(int2.zero))
        {
          direction = endpointState.Direction;
        }
      }

      connectedToBelt = IsConnectedToConveyorBelt(tile, direction, role);
    }

    if (role == ConveyorTunnelEndpointRole.Exit)
    {
      return connectedToBelt
          ? ConnectedExitVariantIndex
          : TerminatedExitVariantIndex;
    }

    return connectedToBelt
        ? ConnectedEntranceVariantIndex
        : TerminatedEntranceVariantIndex;
  }

  private static bool IsConnectedToConveyorBelt(
      int2 tile,
      int2 direction,
      ConveyorTunnelEndpointRole role)
  {
    if (direction.Equals(int2.zero))
    {
      return false;
    }

    int side = role == ConveyorTunnelEndpointRole.Exit ? 1 : -1;
    int2 beltTile = new int2(
        tile.x + direction.x * side,
        tile.y + direction.y * side);

    ConveyorBelt belt = ConveyorBelt.GetBeltAtPosition(beltTile);
    if (belt == null || belt.isHidden || !belt.entityExist)
    {
      return false;
    }

    int2 beltDirection =
        ConveyorTunnelDirectionUtility.GetDirectionFromVariation(belt.variation);
    return beltDirection.Equals(direction);
  }

  private void SyncConveyorAnimationTime()
  {
    SpriteObject spriteObject = ResolveSpriteObject();
    if (spriteObject != null)
    {
      spriteObject.animationTime = Time.time;
    }
  }

  private bool TryGetCurrentTile(out int2 tile)
  {
    tile = default;
    if (!entityExist)
    {
      return false;
    }

    Vector3 position = WorldPosition;
    tile = new int2(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
    return true;
  }

  private void RefreshTunnelRegistry(bool force)
  {
    if (!TryGetCurrentTile(out int2 tile))
    {
      UnregisterTunnelTile();
      return;
    }

    int2 direction = ConveyorTunnelDirectionUtility.GetDirectionFromVariation(GetAnimationIndex());
    if (direction.Equals(int2.zero))
    {
      UnregisterTunnelTile();
      return;
    }

    if (!force &&
        _hasRegisteredTile &&
        _registeredTile.Equals(tile) &&
        _registeredDirection.Equals(direction))
    {
      return;
    }

    UnregisterTunnelTile();
    long key = GetTileKey(tile);
    TunnelDirectionsByTile[key] = direction;
    TunnelVisualsByTile[key] = this;
    _registeredTile = tile;
    _registeredDirection = direction;
    _hasRegisteredTile = true;
    RefreshAdjacentConveyorBelts(tile);
  }

  private void UnregisterTunnelTile()
  {
    if (!_hasRegisteredTile)
    {
      return;
    }

    long key = GetTileKey(_registeredTile);
    if (TunnelDirectionsByTile.TryGetValue(key, out int2 currentDirection) &&
        currentDirection.Equals(_registeredDirection))
    {
      TunnelDirectionsByTile.Remove(key);
    }

    if (TunnelVisualsByTile.TryGetValue(
            key,
            out ConveyorTunnelVisual currentVisual) &&
        currentVisual == this)
    {
      TunnelVisualsByTile.Remove(key);
    }

    int2 tile = _registeredTile;
    _hasRegisteredTile = false;
    RefreshAdjacentConveyorBelts(tile);
  }

  private static void RefreshAdjacentConveyorBelts(int2 tile)
  {
    RefreshConveyorBeltVisual(tile + new int2(0, 1));
    RefreshConveyorBeltVisual(tile + new int2(1, 0));
    RefreshConveyorBeltVisual(tile + new int2(0, -1));
    RefreshConveyorBeltVisual(tile + new int2(-1, 0));
  }

  private static void RefreshConveyorBeltVisual(int2 tile)
  {
    ConveyorBelt belt = ConveyorBelt.GetBeltAtPosition(tile);
    if (belt != null)
    {
      belt.UpdateVisuals(false);
    }
  }

  private static long GetTileKey(int2 tile)
  {
    return ((long)tile.x << 32) ^ (uint)tile.y;
  }

  private static int2 TileFromKey(long key)
  {
    return new int2((int)(key >> 32), unchecked((int)(uint)key));
  }

  private void SetInteractionOutline(bool show)
  {
    SpriteObject spriteObject = ResolveSpriteObject();
    if (spriteObject == null)
    {
      return;
    }

    spriteObject.outlineColor = show && Manager.effects != null
        ? Manager.effects.outlineColor
        : new Color(0.0f, 0.0f, 0.0f, 0.0f);
  }

}
