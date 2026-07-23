using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public static class ChunkLoaderPlayerResolver
{
  public static ChunkLoaderActor Resolve(World world, Entity sourceConnection)
  {
    if (world == null || !world.IsCreated)
    {
      return default;
    }

    EntityManager entityManager = world.EntityManager;
    EntityQuery query = entityManager.CreateEntityQuery(
        ComponentType.ReadOnly<PlayerGhost>());

    using NativeArray<PlayerGhost> players =
        query.ToComponentDataArray<PlayerGhost>(Allocator.Temp);
    query.Dispose();

    for (int i = 0; i < players.Length; i++)
    {
      PlayerGhost player = players[i];
      if (sourceConnection != Entity.Null && player.connection != sourceConnection)
      {
        continue;
      }

      ulong persistentId = GetPersistentId(player);

      return new ChunkLoaderActor(
          persistentId,
          player.onlineName.ToString(),
          player.adminPrivileges > 0);
    }

    if (sourceConnection != Entity.Null &&
        entityManager.Exists(sourceConnection) &&
        entityManager.HasComponent<NetworkId>(sourceConnection))
    {
      NetworkId networkId = entityManager.GetComponentData<NetworkId>(sourceConnection);
      return new ChunkLoaderActor(
          unchecked(0x8000000000000000UL | (uint)networkId.Value),
          $"Player {networkId.Value}",
          false);
    }

    return default;
  }

  public static ulong GetPersistentId(in PlayerGhost player)
  {
    ulong persistentId = player.onlineId;
    if (persistentId == 0)
    {
      persistentId = HashPlayerGuid(player.playerGuid);
    }

    if (persistentId == 0)
    {
      persistentId = unchecked((ulong)(player.playerIndex + 1));
    }

    return persistentId;
  }

  private static ulong HashPlayerGuid(Hash128 guid)
  {
    string value = guid.ToString();
    if (string.IsNullOrEmpty(value))
    {
      return 0;
    }

    const ulong offset = 14695981039346656037UL;
    const ulong prime = 1099511628211UL;
    ulong hash = offset;
    for (int i = 0; i < value.Length; i++)
    {
      hash ^= value[i];
      hash *= prime;
    }

    return hash == 0 ? 1UL : hash;
  }
}
