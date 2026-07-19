using ExpandNullforge.Api;
using PugMod;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Networking
{
  internal static class DimensionClientRpcReadiness
  {
    public static bool TryGetReadyEntityManager(out EntityManager entityManager)
    {
      if (!TryGetClientEntityManager(out entityManager))
      {
        return false;
      }

      return HasInGameClientConnection(entityManager) &&
          HasReadyLocalPlayer(entityManager);
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

    private static bool HasInGameClientConnection(EntityManager entityManager)
    {
      EntityQuery query =
          entityManager.CreateEntityQuery(
              ComponentType.ReadOnly<NetworkStreamConnection>(),
              ComponentType.ReadOnly<NetworkStreamInGame>(),
              ComponentType.ReadOnly<NetworkId>(),
              ComponentType.ReadOnly<OutgoingRpcDataStreamBuffer>());
      bool ready = !query.IsEmptyIgnoreFilter;
      query.Dispose();
      return ready;
    }

    private static bool HasReadyLocalPlayer(EntityManager entityManager)
    {
      GameObject localPlayer = API.Client != null ? API.Client.LocalPlayer : null;
      EntityMonoBehaviour localEntityMonoBehaviour =
          localPlayer != null ? localPlayer.GetComponent<EntityMonoBehaviour>() : null;
      if (localEntityMonoBehaviour != null && localEntityMonoBehaviour.entityExist)
      {
        return true;
      }

      EntityQuery query =
          entityManager.CreateEntityQuery(
              ComponentType.ReadOnly<PlayerGhost>(),
              ComponentType.ReadOnly<GhostOwnerIsLocal>(),
              ComponentType.ReadOnly<LocalTransform>(),
              ComponentType.ReadOnly<UIActionBuffer>());
      bool ready = !query.IsEmptyIgnoreFilter;
      query.Dispose();
      return ready;
    }
  }
}
