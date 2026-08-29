using System;
using ExpandNullforge.Api;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using ExpandNullforge.Core;

namespace ExpandNullforge.Networking
{
  public readonly struct DimensionPlayerContextNetworkSnapshot
  {
    public readonly uint RequestId;
    public readonly bool IsKnown;
    public readonly bool IsPersistedFallback;
    public readonly string Code;
    public readonly string Message;
    public readonly DimensionContext Context;

    public DimensionPlayerContextNetworkSnapshot(
        uint requestId,
        bool isKnown,
        bool isPersistedFallback,
        string code,
        string message,
        DimensionContext context)
    {
      RequestId = requestId;
      IsKnown = isKnown;
      IsPersistedFallback = isPersistedFallback;
      Code = code ?? string.Empty;
      Message = message ?? string.Empty;
      Context = context;
    }
  }

  public static class DimensionPlayerContextNetworkState
  {
    private const double InitialHydrationRetrySeconds = 0.50d;
    private const double MaximumHydrationRetrySeconds = 8.00d;

    private static uint nextRequestId;
    private static bool hasCurrentSnapshot;
    private static DimensionPlayerContextNetworkSnapshot currentSnapshot;
    private static bool hydrationActive;
    private static bool hydrationIncludePersistedFallback;
    private static string hydrationReason = string.Empty;
    private static double nextHydrationAttemptAt;
    private static int hydrationAttemptCount;
    private static uint lastHydrationRequestId;

    public static event Action<uint> ContextRequestStarted;

    public static event Action<DimensionPlayerContextNetworkSnapshot> ContextSnapshotReceived;

    public static event Action<DimensionPlayerContextNetworkSnapshot> CurrentDimensionChanged;

    public static bool IsCurrentContextHydrationActive
    {
      get { return hydrationActive; }
    }

    public static bool HasCurrentSnapshot
    {
      get { return hasCurrentSnapshot; }
    }

    public static bool TryGetCurrentSnapshot(
        out DimensionPlayerContextNetworkSnapshot snapshot)
    {
      snapshot = currentSnapshot;
      return hasCurrentSnapshot;
    }

    public static uint RequestCurrentContext(
        bool includePersistedFallback,
        string reason)
    {
      if (!TryGetClientEntityManager(out EntityManager entityManager))
      {
        return 0;
      }

      uint requestId = NextRequestId();
      Entity entity = entityManager.CreateEntity(
          typeof(DimensionPlayerContextRequestRpc),
          typeof(SendRpcCommandRequest));
      entityManager.SetComponentData(
          entity,
          new DimensionPlayerContextRequestRpc
          {
            RequestId = requestId,
            IncludePersistedFallback = includePersistedFallback ? (byte)1 : (byte)0,
            Reason = DimensionFixedStrings.ToFixed128(reason)
          });

      Action<uint> startedHandler = ContextRequestStarted;
      if (startedHandler != null)
      {
        startedHandler(requestId);
      }

      return requestId;
    }

    public static void StartCurrentContextHydration(
        bool includePersistedFallback,
        string reason)
    {
      hydrationActive = true;
      hydrationIncludePersistedFallback = includePersistedFallback;
      hydrationReason = reason ?? string.Empty;
      nextHydrationAttemptAt = 0.0d;
      hydrationAttemptCount = 0;
      lastHydrationRequestId = 0;
    }

    public static void StopCurrentContextHydration()
    {
      hydrationActive = false;
      hydrationReason = string.Empty;
      hydrationAttemptCount = 0;
      lastHydrationRequestId = 0;
      nextHydrationAttemptAt = 0.0d;
    }

    public static void UpdateCurrentContextHydration()
    {
      if (!hydrationActive)
      {
        return;
      }

      if (hasCurrentSnapshot && currentSnapshot.IsKnown)
      {
        StopCurrentContextHydration();
        return;
      }

      double now = Time.realtimeSinceStartupAsDouble;
      if (now < nextHydrationAttemptAt)
      {
        return;
      }

      uint requestId =
          RequestCurrentContext(
              hydrationIncludePersistedFallback,
              hydrationReason);
      hydrationAttemptCount++;
      if (requestId != 0)
      {
        lastHydrationRequestId = requestId;
      }

      double retryDelay =
          Math.Min(
              MaximumHydrationRetrySeconds,
              InitialHydrationRetrySeconds * Math.Pow(2.0d, Math.Min(4, hydrationAttemptCount - 1)));
      nextHydrationAttemptAt = now + retryDelay;
    }

    public static void ApplySnapshot(DimensionPlayerContextSnapshotRpc rpc)
    {
      DimensionContext context;
      if (rpc.Known != 0)
      {
        context =
            new DimensionContext(
                true,
                rpc.DimensionId.ToString(),
                new float2(rpc.AbsoluteX, rpc.AbsoluteY),
                new float2(rpc.LocalX, rpc.LocalY));
      }
      else
      {
        context =
            DimensionContext.Unknown(
                new float2(rpc.AbsoluteX, rpc.AbsoluteY));
      }

      DimensionPlayerContextNetworkSnapshot snapshot =
          new DimensionPlayerContextNetworkSnapshot(
              rpc.RequestId,
              rpc.Known != 0,
              rpc.PersistedFallback != 0,
              rpc.Code.ToString(),
              rpc.Message.ToString(),
              context);

      bool dimensionChanged =
          !hasCurrentSnapshot ||
          currentSnapshot.IsKnown != snapshot.IsKnown ||
          !string.Equals(
              currentSnapshot.Context.DimensionId,
              snapshot.Context.DimensionId,
              StringComparison.Ordinal);

      currentSnapshot = snapshot;
      hasCurrentSnapshot = true;

      if (hydrationActive && snapshot.IsKnown)
      {
        StopCurrentContextHydration();
      }

      Action<DimensionPlayerContextNetworkSnapshot> receivedHandler =
          ContextSnapshotReceived;
      if (receivedHandler != null)
      {
        receivedHandler(snapshot);
      }

      if (dimensionChanged)
      {
        Action<DimensionPlayerContextNetworkSnapshot> changedHandler =
            CurrentDimensionChanged;
        if (changedHandler != null)
        {
          changedHandler(snapshot);
        }
      }
    }

    public static void Reset()
    {
      nextRequestId = 0;
      hasCurrentSnapshot = false;
      currentSnapshot = default(DimensionPlayerContextNetworkSnapshot);
      StopCurrentContextHydration();
      ContextRequestStarted = null;
      ContextSnapshotReceived = null;
      CurrentDimensionChanged = null;
    }

    private static uint NextRequestId()
    {
      nextRequestId++;
      if (nextRequestId == 0)
      {
        nextRequestId++;
      }

      return nextRequestId;
    }

    private static bool TryGetClientEntityManager(out EntityManager entityManager)
    {
      entityManager = default;
      return DimensionClientRpcReadiness.TryGetReadyEntityManager(out entityManager);
    }
  }
}
