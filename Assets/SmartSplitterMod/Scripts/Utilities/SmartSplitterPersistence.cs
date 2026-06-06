using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Pug.ECS.Components;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class SmartSplitterPersistence
{
  private const int CurrentVersion = 1;
  private const string FilePrefix = "SmartSplitterMod_world_";
  private const string FileSuffix = "_filters.json";
  private const double DeferredFlushDelaySeconds = 0.75d;

  private static PersistedState _state;
  private static string _loadedPath;
  private static string _loadedWorldKey;
  private static bool _isLoaded;
  private static bool _dirty;
  private static bool _warnedUnavailable;
  private static double _nextFlushAt = double.PositiveInfinity;

  [Serializable]
  public readonly struct HistoryItem
  {
    public HistoryItem(ObjectID objectID, int variation)
    {
      ObjectID = objectID;
      Variation = variation;
    }

    public readonly ObjectID ObjectID;
    public readonly int Variation;
  }

  [Serializable]
  private sealed class PersistedState
  {
    public int version = CurrentVersion;
    public string worldKey = string.Empty;
    public List<PersistedSplitterFilters> splitters = new();
    public List<PersistedHistoryItem> history = new();
  }

  [Serializable]
  private sealed class PersistedSplitterFilters
  {
    public int centerX;
    public int centerY;
    public PersistedLaneFilter left = new();
    public PersistedLaneFilter center = new();
    public PersistedLaneFilter right = new();
    public long updatedAtTicks;
  }

  [Serializable]
  private sealed class PersistedLaneFilter
  {
    public int mode;
    public int objectID;
    public int variation;
  }

  [Serializable]
  private sealed class PersistedHistoryItem
  {
    public int objectID;
    public int variation;
    public long firstSeenAtTicks;
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

  public static bool TryGetFilters(int2 center, out SmartSplitterLaneFiltersCD filters)
  {
    filters = default;
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null)
    {
      return false;
    }

    PersistedSplitterFilters persisted = FindSplitter(center);
    if (persisted == null)
    {
      return false;
    }

    filters = CreateFiltersFromPersisted(persisted);

    RememberFilterHistory(filters.Left);
    RememberFilterHistory(filters.Center);
    RememberFilterHistory(filters.Right);
    return true;
  }

  public static bool TrySaveFilters(
      EntityManager entityManager,
      Entity splitter,
      SmartSplitterLaneFiltersCD filters)
  {
    if (!entityManager.Exists(splitter) ||
        !entityManager.HasComponent<SmartSplitterOriginalOutputsCD>(splitter))
    {
      return false;
    }

    SmartSplitterOriginalOutputsCD originals =
        entityManager.GetComponentData<SmartSplitterOriginalOutputsCD>(splitter);

    List<int2> centers = new();
    AddUniqueCenterFromCachedOutputs(originals, centers);
    AddUniqueCenterFromCurrentMovers(entityManager, originals, centers);

    if (centers.Count == 0)
    {
      Debug.LogWarning("[SmartSplitterPersistence] Could not save filters because no stable splitter center could be resolved.");
      return false;
    }

    for (int i = 0; i < centers.Count; i++)
    {
      SaveFilters(centers[i], filters);
    }

    return true;
  }

  public static void SaveFilters(int2 center, SmartSplitterLaneFiltersCD filters)
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null)
    {
      return;
    }

    PersistedSplitterFilters persisted = FindSplitter(center);
    if (persisted == null)
    {
      persisted = new PersistedSplitterFilters
      {
        centerX = center.x,
        centerY = center.y
      };
      _state.splitters.Add(persisted);
    }

    persisted.left = ToPersistedFilter(filters.Left);
    persisted.center = ToPersistedFilter(filters.Center);
    persisted.right = ToPersistedFilter(filters.Right);
    persisted.updatedAtTicks = DateTime.UtcNow.Ticks;

    RememberFilterHistory(filters.Left);
    RememberFilterHistory(filters.Center);
    RememberFilterHistory(filters.Right);

    MarkDirty();
    Debug.Log(
        $"[SmartSplitterPersistence] Saved filters center=({center.x},{center.y}) left={SmartSplitterLaneFilterUtility.FormatFilter(filters.Left)} centerLane={SmartSplitterLaneFilterUtility.FormatFilter(filters.Center)} right={SmartSplitterLaneFilterUtility.FormatFilter(filters.Right)} splitters={_state.splitters.Count} history={_state.history.Count}");
  }

  public static bool DeleteFilters(int2 center)
  {
    EnsureLoadedForCurrentWorld();

    if (!_isLoaded || _state == null || _state.splitters == null)
    {
      return false;
    }

    for (int i = _state.splitters.Count - 1; i >= 0; i--)
    {
      PersistedSplitterFilters splitter = _state.splitters[i];
      if (splitter == null ||
          splitter.centerX != center.x ||
          splitter.centerY != center.y)
      {
        continue;
      }

      _state.splitters.RemoveAt(i);
      MarkDirty();
      Debug.Log(
          $"[SmartSplitterPersistence] Deleted filters center=({center.x},{center.y}) splitters={_state.splitters.Count} history={_state.history.Count}");
      return true;
    }

    return false;
  }

  public static void RememberHistoryItem(ObjectID objectID, int variation)
  {
    if (objectID == ObjectID.None)
    {
      return;
    }

    EnsureLoadedForCurrentWorld();
    if (!_isLoaded || _state == null)
    {
      return;
    }

    if (RememberHistoryItemInternal(objectID, variation))
    {
      MarkDirty();
    }
  }

  public static List<HistoryItem> GetHistoryItems()
  {
    EnsureLoadedForCurrentWorld();

    List<HistoryItem> items = new();
    if (!_isLoaded || _state == null || _state.history == null)
    {
      return items;
    }

    for (int i = 0; i < _state.history.Count; i++)
    {
      PersistedHistoryItem item = _state.history[i];
      if (item == null || item.objectID == (int)ObjectID.None)
      {
        continue;
      }

      items.Add(new HistoryItem((ObjectID)item.objectID, item.variation));
    }

    return items;
  }

  public static void EnsureLoadedForCurrentWorld()
  {
    if (!TryGetSavePath(out string path, out string worldKey))
    {
      if (!_warnedUnavailable)
      {
        Debug.LogWarning("[SmartSplitterPersistence] Save path unavailable; filters will not persist yet.");
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
      Debug.Log(
          $"[SmartSplitterPersistence] Loaded save path={path} splitters={_state.splitters.Count} history={_state.history.Count}");
    }
    catch (Exception ex)
    {
      Debug.LogWarning($"[SmartSplitterPersistence] Failed to load saved filters. A backup will be kept. {ex}");
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
    catch (Exception ex)
    {
      Debug.LogError($"[SmartSplitterPersistence] Failed to save splitter filters. {ex}");
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
          if (!string.IsNullOrWhiteSpace(guid.Value.ToString()))
          {
            return "guid-" + guid.Value;
          }
        }
      }
      catch (Exception ex)
      {
        Debug.LogWarning($"[SmartSplitterPersistence] Could not read world GUID; falling back to save slot. {ex.Message}");
      }
    }

    try
    {
      string slotKey = GetSlotWorldKey();
      return string.IsNullOrWhiteSpace(slotKey) ? null : slotKey;
    }
    catch (Exception ex)
    {
      Debug.LogWarning($"[SmartSplitterPersistence] Could not read world save slot. {ex.Message}");
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

  private static PersistedSplitterFilters FindSplitter(int2 center)
  {
    if (_state == null || _state.splitters == null)
    {
      return null;
    }

    for (int i = 0; i < _state.splitters.Count; i++)
    {
      PersistedSplitterFilters splitter = _state.splitters[i];
      if (splitter != null &&
          splitter.centerX == center.x &&
          splitter.centerY == center.y)
      {
        return splitter;
      }
    }

    return null;
  }

  private static SmartSplitterLaneFiltersCD CreateFiltersFromPersisted(PersistedSplitterFilters persisted)
  {
    return new SmartSplitterLaneFiltersCD
    {
      Left = FromPersistedFilter(persisted.left),
      Center = FromPersistedFilter(persisted.center),
      Right = FromPersistedFilter(persisted.right)
    };
  }

  private static PersistedLaneFilter ToPersistedFilter(SmartSplitterLaneFilter filter)
  {
    return new PersistedLaneFilter
    {
      mode = (int)filter.Mode,
      objectID = (int)filter.FilterObject,
      variation = filter.FilterVariation
    };
  }

  private static SmartSplitterLaneFilter FromPersistedFilter(PersistedLaneFilter filter)
  {
    if (filter == null)
    {
      return SmartSplitterLaneFilterUtility.CreateAnyFilter();
    }

    SmartSplitterLaneFilterMode mode = (SmartSplitterLaneFilterMode)filter.mode;
    switch (mode)
    {
      case SmartSplitterLaneFilterMode.Item:
        return filter.objectID == (int)ObjectID.None
            ? SmartSplitterLaneFilterUtility.CreateAnyFilter()
            : SmartSplitterLaneFilterUtility.CreateItemFilter((ObjectID)filter.objectID, filter.variation);

      case SmartSplitterLaneFilterMode.None:
        return SmartSplitterLaneFilterUtility.CreateNoneFilter();

      case SmartSplitterLaneFilterMode.Any:
      default:
        return SmartSplitterLaneFilterUtility.CreateAnyFilter();
    }
  }

  private static void RememberFilterHistory(SmartSplitterLaneFilter filter)
  {
    if (filter.Mode == SmartSplitterLaneFilterMode.Item)
    {
      RememberHistoryItemInternal(filter.FilterObject, filter.FilterVariation);
      MarkDirty();
    }
  }

  private static bool RememberHistoryItemInternal(ObjectID objectID, int variation)
  {
    if (objectID == ObjectID.None)
    {
      return false;
    }

    _state.history ??= new List<PersistedHistoryItem>();
    for (int i = 0; i < _state.history.Count; i++)
    {
      PersistedHistoryItem item = _state.history[i];
      if (item != null &&
          item.objectID == (int)objectID &&
          item.variation == variation)
      {
        return false;
      }
    }

    _state.history.Add(new PersistedHistoryItem
    {
      objectID = (int)objectID,
      variation = variation,
      firstSeenAtTicks = DateTime.UtcNow.Ticks
    });
    return true;
  }

  private static void NormalizeState(string worldKey)
  {
    _state ??= new PersistedState();
    _state.version = CurrentVersion;
    _state.worldKey = worldKey ?? string.Empty;
    _state.splitters ??= new List<PersistedSplitterFilters>();
    _state.history ??= new List<PersistedHistoryItem>();

    NormalizeSplitterList();
    NormalizeHistoryList();
  }

  private static void NormalizeSplitterList()
  {
    HashSet<long> seen = new();
    List<PersistedSplitterFilters> normalized = new();

    for (int i = _state.splitters.Count - 1; i >= 0; i--)
    {
      PersistedSplitterFilters splitter = _state.splitters[i];
      if (splitter == null)
      {
        continue;
      }

      long key = GetTileKey(splitter.centerX, splitter.centerY);
      if (!seen.Add(key))
      {
        continue;
      }

      splitter.left = NormalizeFilter(splitter.left);
      splitter.center = NormalizeFilter(splitter.center);
      splitter.right = NormalizeFilter(splitter.right);
      normalized.Add(splitter);
    }

    normalized.Reverse();
    _state.splitters = normalized;
  }

  private static PersistedLaneFilter NormalizeFilter(PersistedLaneFilter filter)
  {
    filter ??= new PersistedLaneFilter();
    SmartSplitterLaneFilterMode mode = (SmartSplitterLaneFilterMode)filter.mode;
    if (mode != SmartSplitterLaneFilterMode.Any &&
        mode != SmartSplitterLaneFilterMode.Item &&
        mode != SmartSplitterLaneFilterMode.None)
    {
      filter.mode = (int)SmartSplitterLaneFilterMode.Any;
      filter.objectID = (int)ObjectID.None;
      filter.variation = 0;
    }

    if ((SmartSplitterLaneFilterMode)filter.mode != SmartSplitterLaneFilterMode.Item)
    {
      filter.objectID = (int)ObjectID.None;
      filter.variation = 0;
    }

    return filter;
  }

  private static void NormalizeHistoryList()
  {
    HashSet<long> seen = new();
    List<PersistedHistoryItem> normalized = new();

    for (int i = 0; i < _state.history.Count; i++)
    {
      PersistedHistoryItem item = _state.history[i];
      if (item == null || item.objectID == (int)ObjectID.None)
      {
        continue;
      }

      long key = GetItemKey(item.objectID, item.variation);
      if (!seen.Add(key))
      {
        continue;
      }

      if (item.firstSeenAtTicks <= 0)
      {
        item.firstSeenAtTicks = DateTime.UtcNow.Ticks;
      }

      normalized.Add(item);
    }

    _state.history = normalized;
  }

  private static long GetTileKey(int x, int y)
  {
    return ((long)x << 32) ^ (uint)y;
  }

  private static long GetItemKey(int objectID, int variation)
  {
    return ((long)objectID << 32) ^ (uint)variation;
  }

  private static void AddUniqueCenterFromCachedOutputs(
      SmartSplitterOriginalOutputsCD originals,
      List<int2> centers)
  {
    if (SmartSplitterLaneFilterUtility.TryGetSplitterCenterFromCachedOutputs(originals, out int2 center))
    {
      AddUniqueCenter(center, centers);
    }
  }

  private static void AddUniqueCenterFromCurrentMovers(
      EntityManager entityManager,
      SmartSplitterOriginalOutputsCD originals,
      List<int2> centers)
  {
    if (SmartSplitterLaneFilterUtility.TryGetSplitterCenterFromCurrentMovers(entityManager, originals, out int2 center))
    {
      AddUniqueCenter(center, centers);
    }
  }

  private static void AddUniqueCenter(int2 center, List<int2> centers)
  {
    for (int i = 0; i < centers.Count; i++)
    {
      if (centers[i].x == center.x && centers[i].y == center.y)
      {
        return;
      }
    }

    centers.Add(center);
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
    catch (Exception backupEx)
    {
      Debug.LogWarning($"[SmartSplitterPersistence] Could not write corrupt-save backup. {backupEx.Message}");
    }
  }
}
