using System;
using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Portals
{
  public static class DimensionPortalRuntime
  {
    private static uint nextRequestId;
    private static int pendingActivationCount;

    public static bool TryQueueActivation(
        World world,
        Entity portal,
        Entity player,
        Entity sourceConnection,
        uint preferredRequestId,
        string reason,
        out uint requestId)
    {
      requestId = 0;
      if (world == null || !world.IsCreated || portal == Entity.Null || player == Entity.Null)
      {
        return false;
      }

      EntityManager entityManager = world.EntityManager;
      if (!entityManager.Exists(portal) ||
          !entityManager.Exists(player) ||
          !entityManager.HasComponent<DimensionPortalCD>(portal))
      {
        return false;
      }

      if (!entityManager.HasComponent<DimensionPortalActivationBuffer>(portal))
      {
        entityManager.AddBuffer<DimensionPortalActivationBuffer>(portal);
      }

      DynamicBuffer<DimensionPortalActivationBuffer> activations =
          entityManager.GetBuffer<DimensionPortalActivationBuffer>(portal);
      for (int i = 0; i < activations.Length; i++)
      {
        if (activations[i].Player == player)
        {
          DimensionPortalActivationBuffer existing = activations[i];
          if (sourceConnection != Entity.Null)
          {
            existing.SourceConnection = sourceConnection;
          }

          if (preferredRequestId != 0)
          {
            existing.RequestId = preferredRequestId;
          }

          if (!string.IsNullOrEmpty(reason))
          {
            existing.Reason = ToFixed128(reason);
          }

          activations[i] = existing;
          requestId = existing.RequestId;
          return true;
        }
      }

      requestId = preferredRequestId == 0 ? NextRequestId() : preferredRequestId;
      activations.Add(new DimensionPortalActivationBuffer
      {
        Player = player,
        SourceConnection = sourceConnection,
        RequestId = requestId,
        RequestedAt = world.Time.ElapsedTime,
        Reason = ToFixed128(reason)
      });

      pendingActivationCount++;
      return true;
    }

    public static bool HasPendingActivations
    {
      get { return pendingActivationCount > 0; }
    }

    public static void MarkPendingActivationsDrained()
    {
      pendingActivationCount = 0;
    }

    public static void Reset()
    {
      pendingActivationCount = 0;
      nextRequestId = 0;
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

    private static FixedString128Bytes ToFixed128(string value)
    {
      FixedString128Bytes result = default;
      if (string.IsNullOrEmpty(value))
      {
        return result;
      }

      int count = Math.Min(value.Length, 127);
      for (int i = 0; i < count; i++)
      {
        if (!char.IsControl(value[i]))
        {
          result.Append(value[i]);
        }
      }

      return result;
    }
  }
}
