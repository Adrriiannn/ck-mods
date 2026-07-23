using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public sealed class ChunkLoaderTelemetryEvent
{
  public long sequence;
  public long timestampUtcTicks;
  public ulong registrationId;
  public int positionX;
  public int positionY;
  public string category = string.Empty;
  public string message = string.Empty;
}

public static class ChunkLoaderTelemetry
{
  private static readonly List<ChunkLoaderTelemetryEvent> Events = new();
  private static string _worldKey;
  private static long _nextSequence = 1;
  private static bool _loaded;

  public static void Reset()
  {
    Events.Clear();
    _worldKey = null;
    _nextSequence = 1;
    _loaded = false;
  }

  public static void EnsureLoaded()
  {
    if (!ChunkLoaderSettings.Current.telemetryEnabled)
    {
      return;
    }

    string worldKey = ChunkLoaderRegistry.WorldKey ?? string.Empty;
    if (_loaded && _worldKey == worldKey)
    {
      TrimSessionEvents();
      return;
    }

    Events.Clear();
    _worldKey = worldKey;
    _nextSequence = 1;
    _loaded = true;
  }

  public static void Record(
      ulong registrationId,
      string category,
      string message,
      int positionX,
      int positionY)
  {
    if (!ChunkLoaderSettings.Current.telemetryEnabled ||
        registrationId == 0 ||
        IsInventoryNoise(message))
    {
      return;
    }

    EnsureLoaded();
    if (!_loaded)
    {
      return;
    }

    Events.Add(new ChunkLoaderTelemetryEvent
    {
      sequence = _nextSequence++,
      timestampUtcTicks = DateTime.UtcNow.Ticks,
      registrationId = registrationId,
      positionX = positionX,
      positionY = positionY,
      category = Sanitize(category, 24),
      message = Sanitize(message, 160)
    });
    TrimSessionEvents();
  }

  public static void GetRecent(
      ulong registrationId,
      int maximum,
      List<ChunkLoaderTelemetryEvent> destination,
      long newerThanSequence = long.MinValue)
  {
    destination.Clear();
    if (!ChunkLoaderSettings.Current.telemetryEnabled || maximum <= 0)
    {
      return;
    }

    EnsureLoaded();
    for (int eventIndex = Events.Count - 1;
         eventIndex >= 0 && destination.Count < maximum;
         eventIndex--)
    {
      if (Events[eventIndex].registrationId == registrationId &&
          Events[eventIndex].sequence > newerThanSequence &&
          !IsInventoryNoise(Events[eventIndex].message))
      {
        destination.Add(Events[eventIndex]);
      }
    }
  }

  public static void FlushIfDue()
  {
    TrimSessionEvents();
  }

  public static void FlushNow()
  {
    TrimSessionEvents();
  }

  private static void TrimSessionEvents()
  {
    if (Events.Count == 0)
    {
      return;
    }

    int overflow = Events.Count - ChunkLoaderConstants.ActivitySessionEventLimit;
    if (overflow > 0)
    {
      Events.RemoveRange(0, overflow);
    }
  }

  private static bool IsInventoryNoise(string message)
  {
    return !string.IsNullOrEmpty(message) &&
           (message.IndexOf(
                " inventory gained ",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf(
                " inventory lost ",
                StringComparison.OrdinalIgnoreCase) >= 0);
  }

  private static string Sanitize(string value, int maximum)
  {
    if (string.IsNullOrEmpty(value))
    {
      return string.Empty;
    }

    StringBuilder builder = new StringBuilder(Math.Min(value.Length, maximum));
    for (int i = 0; i < value.Length && builder.Length < maximum; i++)
    {
      if (!char.IsControl(value[i]))
      {
        builder.Append(value[i]);
      }
    }
    return builder.ToString();
  }
}
