using System;
using System.Collections.Generic;
using PugMod;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public sealed class ChunkLoaderSnapshotSample
{
  public byte X;
  public byte Y;
  public int ObjectId;
  public int Variation;
  public int Amount;
  public byte Flags;
}

public sealed class ChunkLoaderDetailsData
{
  public ulong RegistrationId;
  public long CapturedAtUtcTicks;
  public int TotalEntities;
  public int Objects;
  public int Enemies;
  public int Players;
  public bool IncludesSamples;
  public ChunkLoaderErrorCode ErrorCode;
  public string ErrorText = string.Empty;
  public readonly List<ChunkLoaderSnapshotSample> Samples = new();
  public readonly List<ChunkLoaderTelemetryEvent> Logs = new();
}

public static class ChunkLoaderDetailsState
{
  private static readonly Dictionary<ulong, ChunkLoaderDetailsData> Details = new();
  private static readonly Dictionary<ulong, double> LastSnapshotRequestAt = new();
  private static readonly Dictionary<ulong, double> LastLiveRequestAt = new();

  public static event Action<ulong> DetailsChanged;

  public static void Reset()
  {
    Details.Clear();
    LastSnapshotRequestAt.Clear();
    LastLiveRequestAt.Clear();
  }

  public static bool TryGet(ulong registrationId, out ChunkLoaderDetailsData data)
  {
    return Details.TryGetValue(registrationId, out data);
  }

  public static void Begin(ChunkLoaderDetailsBeginRpc rpc)
  {
    List<ChunkLoaderSnapshotSample> preservedSamples = null;
    List<ChunkLoaderTelemetryEvent> preservedLogs = null;
    if (rpc.IncludesSamples == 0 &&
        Details.TryGetValue(
            rpc.RegistrationId,
            out ChunkLoaderDetailsData previous) &&
        previous.Samples.Count > 0)
    {
      preservedSamples =
          new List<ChunkLoaderSnapshotSample>(previous.Samples);
    }
    ChunkLoaderDetailsData previousCounts = null;
    if (rpc.IncludesSamples == 0 &&
        rpc.ErrorCode == 0 &&
        rpc.TotalEntities < 0)
    {
      Details.TryGetValue(rpc.RegistrationId, out previousCounts);
    }
    if (rpc.IncludesSamples == 0 &&
        rpc.ErrorCode == 0 &&
        Details.TryGetValue(
            rpc.RegistrationId,
            out ChunkLoaderDetailsData previousLogs) &&
        previousLogs.Logs.Count > 0)
    {
      preservedLogs =
          new List<ChunkLoaderTelemetryEvent>(previousLogs.Logs);
    }

    ChunkLoaderDetailsData next = new ChunkLoaderDetailsData
    {
      RegistrationId = rpc.RegistrationId,
      CapturedAtUtcTicks = rpc.CapturedAtUtcTicks,
      TotalEntities = previousCounts != null
          ? previousCounts.TotalEntities
          : Math.Max(0, rpc.TotalEntities),
      Objects = previousCounts != null
          ? previousCounts.Objects
          : Math.Max(0, rpc.Objects),
      Enemies = previousCounts != null
          ? previousCounts.Enemies
          : Math.Max(0, rpc.Enemies),
      Players = previousCounts != null
          ? previousCounts.Players
          : Math.Max(0, rpc.Players),
      IncludesSamples = rpc.IncludesSamples != 0,
      ErrorCode = (ChunkLoaderErrorCode)rpc.ErrorCode,
      ErrorText = rpc.ErrorText.ToString()
    };
    if (preservedSamples != null)
    {
      next.Samples.AddRange(preservedSamples);
    }
    if (preservedLogs != null)
    {
      next.Logs.AddRange(preservedLogs);
      TrimLogs(next);
    }

    Details[rpc.RegistrationId] = next;
  }

  public static void AddSample(ChunkLoaderSnapshotSampleRpc rpc)
  {
    if (Details.TryGetValue(rpc.RegistrationId, out ChunkLoaderDetailsData data))
    {
      data.Samples.Add(new ChunkLoaderSnapshotSample
      {
        X = rpc.RelativeX,
        Y = rpc.RelativeY,
        ObjectId = rpc.ObjectId,
        Variation = rpc.Variation,
        Amount = rpc.Amount,
        Flags = rpc.Flags
      });
    }
  }

  public static void AddLog(ChunkLoaderLogLineRpc rpc)
  {
    if (Details.TryGetValue(rpc.RegistrationId, out ChunkLoaderDetailsData data))
    {
      InsertLog(data, new ChunkLoaderTelemetryEvent
      {
        sequence = rpc.Sequence,
        timestampUtcTicks = rpc.TimestampUtcTicks,
        registrationId = rpc.RegistrationId,
        category = rpc.Category.ToString(),
        message = rpc.Message.ToString()
      });
    }
  }

  public static void Complete(ulong registrationId)
  {
    DetailsChanged?.Invoke(registrationId);
  }

  public static bool Request(
      ulong registrationId,
      bool force = false,
      bool includeSamples = true,
      bool live = false)
  {
    double now = Time.realtimeSinceStartupAsDouble;
    float cooldown = includeSamples
        ? ChunkLoaderNetworkState.SnapshotCooldownSeconds
        : ChunkLoaderConstants.LiveDetailsClientIntervalSeconds * 0.75f;
    Dictionary<ulong, double> lastRequestAt = includeSamples
        ? LastSnapshotRequestAt
        : LastLiveRequestAt;
    if (!force &&
        lastRequestAt.TryGetValue(registrationId, out double last) &&
        now - last < cooldown)
    {
      return false;
    }

    if (!TryGetClientEntityManager(out EntityManager entityManager))
    {
      return false;
    }

    lastRequestAt[registrationId] = now;
    Entity entity = entityManager.CreateEntity(
        typeof(ChunkLoaderDetailsRequestRpc),
        typeof(SendRpcCommandRequest));
    entityManager.SetComponentData(entity, new ChunkLoaderDetailsRequestRpc
    {
      RegistrationId = registrationId,
      LastKnownLogSequence = includeSamples
          ? long.MinValue
          : GetNewestKnownLogSequence(registrationId),
      IncludeSamples = includeSamples ? (byte)1 : (byte)0,
      IsLive = live ? (byte)1 : (byte)0
    });
    return true;
  }

  private static long GetNewestKnownLogSequence(ulong registrationId)
  {
    if (!Details.TryGetValue(registrationId, out ChunkLoaderDetailsData data) ||
        data.Logs.Count == 0)
    {
      return long.MinValue;
    }

    long newest = long.MinValue;
    for (int i = 0; i < data.Logs.Count; i++)
    {
      if (data.Logs[i].sequence > newest)
      {
        newest = data.Logs[i].sequence;
      }
    }
    return newest;
  }

  private static void InsertLog(
      ChunkLoaderDetailsData data,
      ChunkLoaderTelemetryEvent log)
  {
    for (int i = 0; i < data.Logs.Count; i++)
    {
      if (data.Logs[i].sequence == log.sequence)
      {
        return;
      }
    }

    int index = 0;
    while (index < data.Logs.Count &&
           data.Logs[index].sequence > log.sequence)
    {
      index++;
    }
    data.Logs.Insert(index, log);
    TrimLogs(data);
  }

  private static void TrimLogs(ChunkLoaderDetailsData data)
  {
    int overflow =
        data.Logs.Count - ChunkLoaderConstants.ActivityDetailsLineLimit;
    if (overflow > 0)
    {
      data.Logs.RemoveRange(
          ChunkLoaderConstants.ActivityDetailsLineLimit,
          overflow);
    }
  }

  private static bool TryGetClientEntityManager(out EntityManager entityManager)
  {
    entityManager = default;
    World world = API.Client != null ? API.Client.World : null;
    if ((world == null || !world.IsCreated) && Manager.ecs != null)
    {
      world = Manager.ecs.ClientWorld;
    }

    if (world == null || !world.IsCreated)
    {
      return false;
    }

    entityManager = world.EntityManager;
    return true;
  }
}
