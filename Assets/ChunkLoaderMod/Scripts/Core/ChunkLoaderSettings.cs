using System;
using System.Text;
using Newtonsoft.Json;
using PugMod;
using UnityEngine;

[Serializable]
public sealed class ChunkLoaderServerSettings
{
  public int schemaVersion = 3;
  public int personalActiveLimit = ChunkLoaderConstants.DefaultPersonalActiveLimit;
  public int worldActiveLimit = ChunkLoaderConstants.DefaultWorldActiveLimit;
  public int personalSavedLimit = ChunkLoaderConstants.DefaultPersonalSavedLimit;
  public bool snapshotsEnabled = true;
  public float snapshotCooldownSeconds = 5.0f;
  public float liveDetailsCooldownSeconds = 1.0f;
  public int snapshotCapturesPerSecond = 2;
  public int snapshotMaximumSamples = 256;
  public bool telemetryEnabled = true;
}

public static class ChunkLoaderSettings
{
  private const string SettingsPath = "ChunkLoaderMod_server_settings.json";
  private static ChunkLoaderServerSettings _current = new();
  private static bool _loaded;

  public static ChunkLoaderServerSettings Current
  {
    get
    {
      EnsureLoaded();
      return _current;
    }
  }

  public static void EnsureLoaded()
  {
    if (_loaded)
    {
      return;
    }

    if (API.ConfigFilesystem == null)
    {
      return;
    }

    _loaded = true;
    _current = new ChunkLoaderServerSettings();
    try
    {
      if (API.ConfigFilesystem != null &&
          API.ConfigFilesystem.FileExists(SettingsPath))
      {
        byte[] bytes = API.ConfigFilesystem.Read(SettingsPath);
        if (bytes != null && bytes.Length > 0)
        {
          ChunkLoaderServerSettings loaded =
              JsonConvert.DeserializeObject<ChunkLoaderServerSettings>(
                  Encoding.UTF8.GetString(bytes));
          if (loaded != null)
          {
            _current = loaded;
          }
        }
      }

      Normalize();
      Save();
      LogConfiguration();
    }
    catch (Exception ex)
    {
      Debug.LogError(
          $"[ChunkLoaderMod] Server settings could not be loaded; safe defaults are active. {ex}");
      _current = new ChunkLoaderServerSettings();
      Normalize();
    }
  }

  public static void Reload()
  {
    _loaded = false;
    EnsureLoaded();
  }

  public static void Reset()
  {
    _loaded = false;
    _current = new ChunkLoaderServerSettings();
  }

  public static bool IsUnlimitedLimit(int limit)
  {
    return limit <= 0 || limit >= ChunkLoaderConstants.UnlimitedLimit;
  }

  public static bool IsLimitReached(int count, int limit)
  {
    return !IsUnlimitedLimit(limit) && count >= limit;
  }

  public static string FormatLimit(int limit)
  {
    return IsUnlimitedLimit(limit) ? "no limit" : limit.ToString();
  }

  private static void Normalize()
  {
    int loadedSchema = _current.schemaVersion;
    if (loadedSchema < 3)
    {
      _current.personalActiveLimit =
          ChunkLoaderConstants.DefaultPersonalActiveLimit;
      _current.worldActiveLimit =
          ChunkLoaderConstants.DefaultWorldActiveLimit;
      _current.personalSavedLimit =
          ChunkLoaderConstants.DefaultPersonalSavedLimit;
    }

    _current.schemaVersion = 3;
    _current.personalActiveLimit =
        NormalizeLimit(_current.personalActiveLimit);
    _current.worldActiveLimit =
        NormalizeLimit(_current.worldActiveLimit);
    _current.personalSavedLimit =
        NormalizeLimit(_current.personalSavedLimit);
    _current.snapshotCooldownSeconds = Mathf.Clamp(
        _current.snapshotCooldownSeconds,
        1.0f,
        60.0f);
    _current.liveDetailsCooldownSeconds = Mathf.Clamp(
        _current.liveDetailsCooldownSeconds,
        0.25f,
        10.0f);
    _current.snapshotCapturesPerSecond = Mathf.Clamp(
        _current.snapshotCapturesPerSecond,
        1,
        8);
    _current.snapshotMaximumSamples = Mathf.Clamp(
        _current.snapshotMaximumSamples,
        32,
        1024);
  }

  private static void Save()
  {
    if (API.ConfigFilesystem == null)
    {
      return;
    }

    string json = JsonConvert.SerializeObject(_current, Formatting.Indented);
    API.ConfigFilesystem.Write(SettingsPath, Encoding.UTF8.GetBytes(json));
  }

  private static void LogConfiguration()
  {
    Debug.Log(
        $"[ChunkLoaderMod] Settings: active={FormatLimit(_current.personalActiveLimit)}/player, " +
        $"{FormatLimit(_current.worldActiveLimit)}/world; " +
        $"saved={FormatLimit(_current.personalSavedLimit)}/player; " +
        $"snapshots={_current.snapshotsEnabled}; telemetry={_current.telemetryEnabled}.");
  }

  private static int NormalizeLimit(int limit)
  {
    if (IsUnlimitedLimit(limit))
    {
      return ChunkLoaderConstants.UnlimitedLimit;
    }

    return Math.Max(1, limit);
  }
}
