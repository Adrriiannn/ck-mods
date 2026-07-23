using System;
using System.Collections.Generic;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public static class ChunkLoaderNetworkState
{
  private static readonly Dictionary<ulong, ChunkLoaderRegistrationRecord> Records = new();
  private static readonly Dictionary<long, ulong> RecordIdsByCoordinate = new();
  private static uint _nextRequestId;
  private static int _recordsVersion;
  private static double _lastSnapshotRequestAt = double.NegativeInfinity;
  private static bool _snapshotInProgress;

  public static event Action RegistryChanged;
  public static event Action<uint, ChunkLoaderMutationResult> MutationCompleted;

  public static ulong RegistryRevision { get; private set; }
  public static ChunkLoaderQuotaSummary Quota { get; private set; }
  public static bool HasSnapshot { get; private set; }
  public static bool SnapshotsEnabled { get; private set; } = true;
  public static bool TelemetryEnabled { get; private set; } = true;
  public static float SnapshotCooldownSeconds { get; private set; } = 5.0f;
  public static ulong ViewerPersistentId { get; private set; }
  public static bool ViewerIsAdmin { get; private set; }
  public static int RecordsVersion => _recordsVersion;

  public static void Reset()
  {
    Records.Clear();
    RecordIdsByCoordinate.Clear();
    BumpRecordsVersion();
    RegistryRevision = 0;
    Quota = default;
    HasSnapshot = false;
    SnapshotsEnabled = true;
    TelemetryEnabled = true;
    SnapshotCooldownSeconds = 5.0f;
    ViewerPersistentId = 0;
    ViewerIsAdmin = false;
    _snapshotInProgress = false;
    _lastSnapshotRequestAt = double.NegativeInfinity;
  }

  public static void BeginSnapshot(
      ulong registryRevision,
      ChunkLoaderQuotaSummary quota,
      bool snapshotsEnabled,
      bool telemetryEnabled,
      float snapshotCooldownSeconds,
      ulong viewerPersistentId,
      bool viewerIsAdmin)
  {
    Records.Clear();
    RecordIdsByCoordinate.Clear();
    BumpRecordsVersion();
    RegistryRevision = registryRevision;
    Quota = quota;
    SnapshotsEnabled = snapshotsEnabled;
    TelemetryEnabled = telemetryEnabled;
    SnapshotCooldownSeconds = Mathf.Clamp(snapshotCooldownSeconds, 1.0f, 60.0f);
    ViewerPersistentId = viewerPersistentId;
    ViewerIsAdmin = viewerIsAdmin;
    HasSnapshot = false;
    _snapshotInProgress = true;
  }

  public static void CompleteSnapshot(ulong registryRevision)
  {
    RegistryRevision = registryRevision;
    HasSnapshot = true;
    _snapshotInProgress = false;
    RegistryChanged?.Invoke();
  }

  public static void UpdateQuota(ChunkLoaderQuotaSummary quota)
  {
    Quota = quota;
    RegistryChanged?.Invoke();
  }

  public static void Remember(ChunkLoaderRegistrationRecord record)
  {
    if (record == null)
    {
      return;
    }

    if (Records.TryGetValue(
            record.registrationId,
            out ChunkLoaderRegistrationRecord previous))
    {
      RecordIdsByCoordinate.Remove(previous.Coordinate.ToKey());
    }

    ChunkLoaderRegistrationRecord clone = record.Clone();
    Records[record.registrationId] = clone;
    RecordIdsByCoordinate[clone.Coordinate.ToKey()] = clone.registrationId;
    BumpRecordsVersion();
    if (!_snapshotInProgress)
    {
      RegistryChanged?.Invoke();
    }
  }

  public static void Forget(ulong registrationId)
  {
    if (Records.TryGetValue(
            registrationId,
            out ChunkLoaderRegistrationRecord record))
    {
      RecordIdsByCoordinate.Remove(record.Coordinate.ToKey());
    }

    if (!Records.Remove(registrationId))
    {
      return;
    }

    BumpRecordsVersion();
    if (!_snapshotInProgress)
    {
      RegistryChanged?.Invoke();
    }
  }

  public static void GetRecords(List<ChunkLoaderRegistrationRecord> destination)
  {
    destination.Clear();
    foreach (ChunkLoaderRegistrationRecord record in Records.Values)
    {
      destination.Add(record.Clone());
    }

    destination.Sort((a, b) =>
    {
      int created = a.createdAtUtcTicks.CompareTo(b.createdAtUtcTicks);
      return created != 0 ? created : a.registrationId.CompareTo(b.registrationId);
    });
  }

  public static void GetRecordsUnsorted(
      List<ChunkLoaderRegistrationRecord> destination)
  {
    destination.Clear();
    foreach (ChunkLoaderRegistrationRecord record in Records.Values)
    {
      destination.Add(record.Clone());
    }
  }

  public static bool TryGetRecord(
      ulong registrationId,
      out ChunkLoaderRegistrationRecord record)
  {
    if (Records.TryGetValue(registrationId, out ChunkLoaderRegistrationRecord found))
    {
      record = found.Clone();
      return true;
    }

    record = null;
    return false;
  }

  public static bool TryGetRecord(
      ChunkCoordinate coordinate,
      out ChunkLoaderRegistrationRecord record)
  {
    if (RecordIdsByCoordinate.TryGetValue(
            coordinate.ToKey(),
            out ulong registrationId))
    {
      return TryGetRecord(registrationId, out record);
    }

    record = null;
    return false;
  }

  public static void RequestSnapshot(bool includeAll = false, bool force = false)
  {
    if (!TryGetClientEntityManager(out EntityManager entityManager))
    {
      return;
    }

    double now = Time.realtimeSinceStartupAsDouble;
    if (!force && now - _lastSnapshotRequestAt < 0.50d)
    {
      return;
    }

    _lastSnapshotRequestAt = now;
    Entity entity = entityManager.CreateEntity(
        typeof(ChunkLoaderRegistryRequestRpc),
        typeof(SendRpcCommandRequest));
    entityManager.SetComponentData(entity, new ChunkLoaderRegistryRequestRpc
    {
      IncludeAll = includeAll ? (byte)1 : (byte)0
    });
  }

  public static uint Create(ChunkCoordinate coordinate)
  {
    return SendMutation(new ChunkLoaderMutationRequestRpc
    {
      RequestId = NextRequestId(),
      Action = (byte)ChunkLoaderMutationAction.Create,
      ChunkX = coordinate.X,
      ChunkY = coordinate.Y
    });
  }

  public static uint Rename(
      ulong registrationId,
      ulong expectedRevision,
      string name)
  {
    return SendMutation(new ChunkLoaderMutationRequestRpc
    {
      RequestId = NextRequestId(),
      Action = (byte)ChunkLoaderMutationAction.Rename,
      RegistrationId = registrationId,
      ExpectedRevision = expectedRevision,
      Name = ToFixed64(name)
    });
  }

  public static uint SetEnabled(
      ulong registrationId,
      ulong expectedRevision,
      bool enabled)
  {
    return SendMutation(new ChunkLoaderMutationRequestRpc
    {
      RequestId = NextRequestId(),
      Action = (byte)ChunkLoaderMutationAction.SetEnabled,
      RegistrationId = registrationId,
      ExpectedRevision = expectedRevision,
      DesiredEnabled = enabled ? (byte)1 : (byte)0
    });
  }

  public static uint Delete(ulong registrationId, ulong expectedRevision)
  {
    return SendMutation(new ChunkLoaderMutationRequestRpc
    {
      RequestId = NextRequestId(),
      Action = (byte)ChunkLoaderMutationAction.Delete,
      RegistrationId = registrationId,
      ExpectedRevision = expectedRevision
    });
  }

  public static void CompleteMutation(
      uint requestId,
      ChunkLoaderMutationAction action,
      ChunkLoaderMutationResult result)
  {
    if (result.Success && result.Record != null)
    {
      if (action == ChunkLoaderMutationAction.Delete)
      {
        Forget(result.Record.registrationId);
      }
      else
      {
        Remember(result.Record);
      }
    }

    MutationCompleted?.Invoke(requestId, result);
  }

  private static uint SendMutation(ChunkLoaderMutationRequestRpc request)
  {
    if (!TryGetClientEntityManager(out EntityManager entityManager))
    {
      return 0;
    }

    Entity entity = entityManager.CreateEntity(
        typeof(ChunkLoaderMutationRequestRpc),
        typeof(SendRpcCommandRequest));
    entityManager.SetComponentData(entity, request);
    return request.RequestId;
  }

  private static uint NextRequestId()
  {
    _nextRequestId++;
    if (_nextRequestId == 0)
    {
      _nextRequestId++;
    }

    return _nextRequestId;
  }

  private static void BumpRecordsVersion()
  {
    unchecked
    {
      _recordsVersion++;
    }
  }

  private static FixedString64Bytes ToFixed64(string value)
  {
    FixedString64Bytes result = default;
    if (!string.IsNullOrEmpty(value))
    {
      int count = Math.Min(value.Length, ChunkLoaderConstants.MaxNameCharacters);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }
    }

    return result;
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
