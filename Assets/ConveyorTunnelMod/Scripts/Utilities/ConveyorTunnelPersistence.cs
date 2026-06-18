using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Pug.ECS.Components;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public readonly struct ConveyorTunnelPersistentPair
{
  public ConveyorTunnelPersistentPair(int2 entranceTile, int2 exitTile, int2 direction)
  {
    EntranceTile = entranceTile;
    ExitTile = exitTile;
    Direction = direction;
  }

  public readonly int2 EntranceTile;
  public readonly int2 ExitTile;
  public readonly int2 Direction;
}

public readonly struct ConveyorTunnelPendingEndpoint
{
  public ConveyorTunnelPendingEndpoint(int2 tile, int2 direction)
  {
    Tile = tile;
    Direction = direction;
  }

  public readonly int2 Tile;
  public readonly int2 Direction;
}

public static class ConveyorTunnelPersistence
{
  private const int CurrentVersion = 2;
  private const string FilePrefix = "ConveyorTunnelMod_world_";
  private const string FileSuffix = "_links.json";
  private const double DeferredFlushDelaySeconds = 0.75d;

  private static PersistedState _state;
  private static string _loadedPath;
  private static string _loadedWorldKey;
  private static bool _isLoaded;
  private static bool _dirty;
  private static bool _warnedUnavailable;
  private static double _nextFlushAt = double.PositiveInfinity;

  [Serializable]
  private sealed class PersistedState
  {
    public int version = CurrentVersion;
    public string worldKey = string.Empty;
    public List<PersistedTunnelPair> pairs = new List<PersistedTunnelPair>();
    public List<PersistedPendingEndpoint> pendingEndpoints =
        new List<PersistedPendingEndpoint>();
    // Kept only so version-one saves can be migrated without losing their endpoint.
    public PersistedPendingEndpoint pending;
    public bool placementModeInitialized;
  }

  [Serializable]
  private sealed class PersistedTunnelPair
  {
    public int entranceX;
    public int entranceY;
    public int exitX;
    public int exitY;
    public int directionX;
    public int directionY;
    public long updatedAtTicks;
  }

  [Serializable]
  private sealed class PersistedPendingEndpoint
  {
    public int tileX;
    public int tileY;
    public int directionX;
    public int directionY;
    public long updatedAtTicks;
  }

  public static void ResetLoadedState()
  {
    FlushNow();
    _state = null;
    _loadedPath = null;
    _loadedWorldKey = null;
    _isLoaded = false;
    _dirty = false;
    _warnedUnavailable = false;
    _nextFlushAt = double.PositiveInfinity;
  }

  public static void EnsureLoadedForCurrentWorld()
  {
    if (!TryGetSavePath(out string path, out string worldKey))
    {
      if (!_warnedUnavailable)
      {
        Debug.LogWarning("[ConveyorTunnelPersistence] Save path unavailable; tunnel links will not persist yet.");
        _warnedUnavailable = true;
      }

      return;
    }

    if (_isLoaded && _loadedPath == path)
    {
      return;
    }

    FlushNow();

    _loadedPath = path;
    _loadedWorldKey = worldKey;
    _state = new PersistedState
    {
      version = CurrentVersion,
      worldKey = worldKey
    };

    byte[] raw = null;
    try
    {
      if (API.ConfigFilesystem.FileExists(path))
      {
        raw = API.ConfigFilesystem.Read(path);
        string json = raw != null ? Encoding.UTF8.GetString(raw) : string.Empty;
        if (!string.IsNullOrWhiteSpace(json))
        {
          PersistedState loadedState = JsonConvert.DeserializeObject<PersistedState>(json);
          if (loadedState != null)
          {
            _state = loadedState;
          }
        }
      }
      else if (TryGetFallbackSavePath(worldKey, out string fallbackPath) &&
               API.ConfigFilesystem.FileExists(fallbackPath))
      {
        raw = API.ConfigFilesystem.Read(fallbackPath);
        string json = raw != null ? Encoding.UTF8.GetString(raw) : string.Empty;
        if (!string.IsNullOrWhiteSpace(json))
        {
          PersistedState loadedState = JsonConvert.DeserializeObject<PersistedState>(json);
          if (loadedState != null)
          {
            _state = loadedState;
          }

          _dirty = true;
        }
      }

      NormalizeState(worldKey);
      _isLoaded = true;
      FlushNow();
      _warnedUnavailable = false;
      Debug.Log($"[ConveyorTunnelPersistence] Loaded save path={path} pairs={_state.pairs.Count}");
    }
    catch (Exception exception)
    {
      Debug.LogWarning($"[ConveyorTunnelPersistence] Failed to load saved tunnel links. A backup will be kept. {exception.Message}");
      TryWriteCorruptBackup(path, raw);
      _state = new PersistedState
      {
        version = CurrentVersion,
        worldKey = worldKey
      };
      NormalizeState(worldKey);
      _isLoaded = true;
      _dirty = true;
      FlushNow();
    }
  }

  public static void FlushNow()
  {
    if (!_dirty ||
        !_isLoaded ||
        _state == null ||
        string.IsNullOrEmpty(_loadedPath) ||
        API.ConfigFilesystem == null)
    {
      return;
    }

    try
    {
      _state.version = CurrentVersion;
      _state.worldKey = _loadedWorldKey ?? string.Empty;
      NormalizeState(_state.worldKey);

      string json = JsonConvert.SerializeObject(_state, Formatting.Indented);
      API.ConfigFilesystem.Write(_loadedPath, Encoding.UTF8.GetBytes(json));
      _dirty = false;
      _nextFlushAt = double.PositiveInfinity;
    }
    catch (Exception exception)
    {
      Debug.LogError($"[ConveyorTunnelPersistence] Failed to save tunnel links. {exception.Message}");
    }
  }

  public static void FlushIfDue()
  {
    if (!_dirty || Time.realtimeSinceStartup < _nextFlushAt)
    {
      return;
    }

    FlushNow();
  }

  public static void GetPairs(List<ConveyorTunnelPersistentPair> pairs)
  {
    pairs.Clear();
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null || _state.pairs == null)
    {
      return;
    }

    for (int i = 0; i < _state.pairs.Count; i++)
    {
      PersistedTunnelPair pair = _state.pairs[i];
      if (pair == null)
      {
        continue;
      }

      int2 direction = new int2(pair.directionX, pair.directionY);
      if (direction.Equals(int2.zero))
      {
        continue;
      }

      pairs.Add(new ConveyorTunnelPersistentPair(
          new int2(pair.entranceX, pair.entranceY),
          new int2(pair.exitX, pair.exitY),
          direction));
    }
  }

  public static void GetPendingEndpoints(
      List<ConveyorTunnelPendingEndpoint> pendingEndpoints)
  {
    pendingEndpoints.Clear();
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null || _state.pendingEndpoints == null)
    {
      return;
    }

    for (int i = 0; i < _state.pendingEndpoints.Count; i++)
    {
      PersistedPendingEndpoint pending = _state.pendingEndpoints[i];
      if (pending == null)
      {
        continue;
      }

      int2 direction = new int2(pending.directionX, pending.directionY);
      if (direction.Equals(int2.zero))
      {
        continue;
      }

      pendingEndpoints.Add(new ConveyorTunnelPendingEndpoint(
          new int2(pending.tileX, pending.tileY),
          direction));
    }
  }

  public static void SetPending(int2 tile, int2 direction)
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null || direction.Equals(int2.zero))
    {
      return;
    }

    _state.pendingEndpoints ??= new List<PersistedPendingEndpoint>();
    for (int i = 0; i < _state.pendingEndpoints.Count; i++)
    {
      PersistedPendingEndpoint pending = _state.pendingEndpoints[i];
      if (pending == null || pending.tileX != tile.x || pending.tileY != tile.y)
      {
        continue;
      }

      if (pending.directionX == direction.x &&
          pending.directionY == direction.y)
      {
        return;
      }

      pending.directionX = direction.x;
      pending.directionY = direction.y;
      pending.updatedAtTicks = DateTime.UtcNow.Ticks;
      MarkDirty();
      return;
    }

    _state.pendingEndpoints.Add(new PersistedPendingEndpoint
    {
      tileX = tile.x,
      tileY = tile.y,
      directionX = direction.x,
      directionY = direction.y,
      updatedAtTicks = DateTime.UtcNow.Ticks
    });
    MarkDirty();
  }

  public static bool ClearPending()
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded ||
        _state == null ||
        _state.pendingEndpoints == null ||
        _state.pendingEndpoints.Count == 0)
    {
      return false;
    }

    _state.pendingEndpoints.Clear();
    MarkDirty();
    return true;
  }

  public static bool ClearPendingIfTile(int2 tile)
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null || _state.pendingEndpoints == null)
    {
      return false;
    }

    bool removed = false;
    for (int i = _state.pendingEndpoints.Count - 1; i >= 0; i--)
    {
      PersistedPendingEndpoint pending = _state.pendingEndpoints[i];
      if (pending == null ||
          (pending.tileX == tile.x && pending.tileY == tile.y))
      {
        _state.pendingEndpoints.RemoveAt(i);
        removed = true;
      }
    }

    if (removed)
    {
      MarkDirty();
    }

    return removed;
  }

  public static bool SavePair(int2 entranceTile, int2 exitTile, int2 direction)
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded ||
        _state == null ||
        direction.Equals(int2.zero) ||
        entranceTile.Equals(exitTile))
    {
      return false;
    }

    _state.pairs ??= new List<PersistedTunnelPair>();
    long entranceKey = GetTileKey(entranceTile.x, entranceTile.y);
    long exitKey = GetTileKey(exitTile.x, exitTile.y);
    bool changed =
        RemovePendingTileInternal(entranceTile) |
        RemovePendingTileInternal(exitTile);

    for (int i = _state.pairs.Count - 1; i >= 0; i--)
    {
      PersistedTunnelPair pair = _state.pairs[i];
      if (pair == null)
      {
        _state.pairs.RemoveAt(i);
        changed = true;
        continue;
      }

      long existingEntranceKey = GetTileKey(pair.entranceX, pair.entranceY);
      long existingExitKey = GetTileKey(pair.exitX, pair.exitY);
      bool samePair =
          existingEntranceKey == entranceKey &&
          existingExitKey == exitKey &&
          pair.directionX == direction.x &&
          pair.directionY == direction.y;
      bool endpointReused =
          existingEntranceKey == entranceKey ||
          existingEntranceKey == exitKey ||
          existingExitKey == entranceKey ||
          existingExitKey == exitKey;

      if (!samePair && endpointReused)
      {
        _state.pairs.RemoveAt(i);
        changed = true;
      }
      else if (samePair)
      {
        pair.updatedAtTicks = DateTime.UtcNow.Ticks;
        if (changed)
        {
          MarkDirty();
        }

        return changed;
      }
    }

    _state.pairs.Add(new PersistedTunnelPair
    {
      entranceX = entranceTile.x,
      entranceY = entranceTile.y,
      exitX = exitTile.x,
      exitY = exitTile.y,
      directionX = direction.x,
      directionY = direction.y,
      updatedAtTicks = DateTime.UtcNow.Ticks
    });

    MarkDirty();
    return true;
  }

  public static bool DeletePairByEndpoint(int2 tile)
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null || _state.pairs == null)
    {
      return false;
    }

    long key = GetTileKey(tile.x, tile.y);
    bool removed = false;
    for (int i = _state.pairs.Count - 1; i >= 0; i--)
    {
      PersistedTunnelPair pair = _state.pairs[i];
      if (pair == null ||
          GetTileKey(pair.entranceX, pair.entranceY) == key ||
          GetTileKey(pair.exitX, pair.exitY) == key)
      {
        _state.pairs.RemoveAt(i);
        removed = true;
      }
    }

    if (removed)
    {
      MarkDirty();
    }

    return removed;
  }

  public static bool HasAnySavedPair()
  {
    EnsureLoadedForCurrentWorld();
    return _isLoaded && _state != null && _state.pairs != null && _state.pairs.Count > 0;
  }

  public static bool HasPending()
  {
    EnsureLoadedForCurrentWorld();
    return _isLoaded &&
           _state != null &&
           _state.pendingEndpoints != null &&
           _state.pendingEndpoints.Count > 0;
  }

  public static bool HasPlacementModeInitialized()
  {
    EnsureLoadedForCurrentWorld();
    return _isLoaded && _state != null && _state.placementModeInitialized;
  }

  public static void MarkPlacementModeInitialized()
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null || _state.placementModeInitialized)
    {
      return;
    }

    _state.placementModeInitialized = true;
    MarkDirty();
  }

  private static void MarkDirty()
  {
    _dirty = true;
    double flushAt = Time.realtimeSinceStartup + DeferredFlushDelaySeconds;
    if (flushAt < _nextFlushAt)
    {
      _nextFlushAt = flushAt;
    }
  }

  private static bool TryGetSavePath(out string path, out string worldKey)
  {
    path = null;
    worldKey = null;

    if (API.ConfigFilesystem == null)
    {
      return false;
    }

    worldKey = GetCurrentWorldKey();
    if (string.IsNullOrWhiteSpace(worldKey))
    {
      return false;
    }

    path = GetSavePathForWorldKey(worldKey);
    return true;
  }

  private static bool TryGetFallbackSavePath(string primaryWorldKey, out string fallbackPath)
  {
    fallbackPath = null;

    if (string.IsNullOrWhiteSpace(primaryWorldKey) ||
        !primaryWorldKey.StartsWith("guid-", StringComparison.Ordinal))
    {
      return false;
    }

    string slotWorldKey = GetSlotWorldKey();
    if (string.IsNullOrWhiteSpace(slotWorldKey) || slotWorldKey == primaryWorldKey)
    {
      return false;
    }

    fallbackPath = GetSavePathForWorldKey(slotWorldKey);
    return true;
  }

  private static string GetSavePathForWorldKey(string worldKey)
  {
    return FilePrefix + SanitizeFilePart(worldKey) + FileSuffix;
  }

  private static string GetCurrentWorldKey()
  {
    World world = API.Server != null ? API.Server.World : null;
    if ((world == null || !world.IsCreated) && Manager.ecs != null)
    {
      world = Manager.ecs.ServerWorld;
    }

    if (world != null && world.IsCreated)
    {
      try
      {
        EntityManager entityManager = world.EntityManager;
        using EntityQuery guidQuery =
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<ServerGuidCD>());
        if (guidQuery.CalculateEntityCount() > 0)
        {
          ServerGuidCD guid = guidQuery.GetSingleton<ServerGuidCD>();
          string guidText = guid.Value.ToString();
          if (!string.IsNullOrWhiteSpace(guidText))
          {
            return "guid-" + guidText;
          }
        }
      }
      catch (Exception exception)
      {
        Debug.LogWarning($"[ConveyorTunnelPersistence] Could not read world GUID; falling back to save slot. {exception.Message}");
      }
    }

    try
    {
      string slotKey = GetSlotWorldKey();
      return string.IsNullOrWhiteSpace(slotKey) ? null : slotKey;
    }
    catch (Exception exception)
    {
      Debug.LogWarning($"[ConveyorTunnelPersistence] Could not read world save slot. {exception.Message}");
    }

    return null;
  }

  private static string GetSlotWorldKey()
  {
    return Manager.saves != null
        ? "slot-" + Manager.saves.GetWorldId()
        : null;
  }

  private static string SanitizeFilePart(string value)
  {
    StringBuilder builder = new StringBuilder(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
      char c = value[i];
      if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
      {
        builder.Append(c);
      }
      else
      {
        builder.Append('_');
      }
    }

    return builder.ToString();
  }

  private static void NormalizeState(string worldKey)
  {
    _state ??= new PersistedState();
    bool needsVersionUpgrade = _state.version != CurrentVersion;
    _state.version = CurrentVersion;
    _state.worldKey = worldKey ?? string.Empty;
    _state.pairs ??= new List<PersistedTunnelPair>();
    _state.pendingEndpoints ??= new List<PersistedPendingEndpoint>();

    NormalizePairs();
    NormalizePendingEndpoints();

    if (needsVersionUpgrade)
    {
      _dirty = true;
    }
  }

  private static void NormalizePairs()
  {
    HashSet<long> usedTiles = new HashSet<long>();
    List<PersistedTunnelPair> normalized = new List<PersistedTunnelPair>();

    for (int i = 0; i < _state.pairs.Count; i++)
    {
      PersistedTunnelPair pair = _state.pairs[i];
      if (pair == null)
      {
        continue;
      }

      int2 entrance = new int2(pair.entranceX, pair.entranceY);
      int2 exit = new int2(pair.exitX, pair.exitY);
      int2 direction = new int2(pair.directionX, pair.directionY);
      if (entrance.Equals(exit) ||
          direction.Equals(int2.zero) ||
          ConveyorTunnelDirectionUtility.Cross(exit - entrance, direction) != 0 ||
          ConveyorTunnelDirectionUtility.Dot(exit - entrance, direction) <= 0)
      {
        continue;
      }

      long entranceKey = GetTileKey(pair.entranceX, pair.entranceY);
      long exitKey = GetTileKey(pair.exitX, pair.exitY);
      if (!usedTiles.Add(entranceKey) || !usedTiles.Add(exitKey))
      {
        continue;
      }

      if (pair.updatedAtTicks <= 0)
      {
        pair.updatedAtTicks = DateTime.UtcNow.Ticks;
      }

      normalized.Add(pair);
    }

    _state.pairs = normalized;
  }

  private static void NormalizePendingEndpoints()
  {
    if (_state.pending != null)
    {
      _state.pendingEndpoints.Add(_state.pending);
      _state.pending = null;
      _dirty = true;
    }

    HashSet<long> pairedTiles = new HashSet<long>();
    for (int i = 0; i < _state.pairs.Count; i++)
    {
      PersistedTunnelPair pair = _state.pairs[i];
      if (pair == null)
      {
        continue;
      }

      pairedTiles.Add(GetTileKey(pair.entranceX, pair.entranceY));
      pairedTiles.Add(GetTileKey(pair.exitX, pair.exitY));
    }

    HashSet<long> usedTiles = new HashSet<long>();
    List<PersistedPendingEndpoint> normalized =
        new List<PersistedPendingEndpoint>();
    for (int i = 0; i < _state.pendingEndpoints.Count; i++)
    {
      PersistedPendingEndpoint pending = _state.pendingEndpoints[i];
      if (pending == null ||
          new int2(pending.directionX, pending.directionY).Equals(int2.zero))
      {
        continue;
      }

      long key = GetTileKey(pending.tileX, pending.tileY);
      if (pairedTiles.Contains(key) || !usedTiles.Add(key))
      {
        continue;
      }

      if (pending.updatedAtTicks <= 0)
      {
        pending.updatedAtTicks = DateTime.UtcNow.Ticks;
      }

      normalized.Add(pending);
    }

    _state.pendingEndpoints = normalized;
  }

  private static bool RemovePendingTileInternal(int2 tile)
  {
    if (_state == null || _state.pendingEndpoints == null)
    {
      return false;
    }

    bool removed = false;
    for (int i = _state.pendingEndpoints.Count - 1; i >= 0; i--)
    {
      PersistedPendingEndpoint pending = _state.pendingEndpoints[i];
      if (pending == null ||
          (pending.tileX == tile.x && pending.tileY == tile.y))
      {
        _state.pendingEndpoints.RemoveAt(i);
        removed = true;
      }
    }

    return removed;
  }

  private static void TryWriteCorruptBackup(string originalPath, byte[] raw)
  {
    if (raw == null || raw.Length == 0 || API.ConfigFilesystem == null)
    {
      return;
    }

    try
    {
      string backupPath = originalPath + ".corrupt-" + DateTime.UtcNow.Ticks;
      API.ConfigFilesystem.Write(backupPath, raw);
    }
    catch (Exception exception)
    {
      Debug.LogWarning($"[ConveyorTunnelPersistence] Could not write corrupt-save backup. {exception.Message}");
    }
  }

  private static long GetTileKey(int x, int y)
  {
    return ((long)x << 32) ^ (uint)y;
  }
}
