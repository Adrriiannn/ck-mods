using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Tells the game which custom biome a player is standing in, so everything keyed on biome —
    /// the title card, discovery, the gamepad light — works for custom biomes without being rebuilt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY WRITE THE COMPONENT RATHER THAN PATCH THE TRACKER. <c>BiomeTrackerSystem</c> decides the
    /// player's biome, and it carries struct-level <c>[BurstCompile]</c>, so a Harmony patch on it
    /// would never take effect. But it is only writing a component — and a component can be written
    /// again. Running after it in the same group and correcting the answer for tiles it has never
    /// heard of costs nothing and leaves vanilla's own logic entirely intact for vanilla ground.
    /// </para>
    /// <para>
    /// WHY THE TILE UNDER THE PLAYER, NOT THE ZONE THEY ARE IN. Because that is the question the rest
    /// of the game asks. Ambient sound, music and the title gate all count tilesets in a box around
    /// the player; keying the biome off the same thing means the title, the music and the ambience
    /// agree about where the player is. A zone rectangle would be a second, differently-shaped answer,
    /// and the two would disagree exactly at the edges — which is where players notice.
    /// </para>
    /// <para>
    /// COST WHEN UNUSED IS ZERO: with no custom biome registered the system returns immediately.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(BeforePredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(BiomeTrackerSystem))]
    public partial class DimensionCurrentBiomeSystem : SystemBase
    {
        private EntityQuery playerQuery;

        protected override void OnCreate()
        {
            playerQuery = GetEntityQuery(
                ComponentType.ReadWrite<CurrentBiomeCD>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PlayerGhost>());
        }

        protected override void OnUpdate()
        {
            if (!DimensionRegionTitleRegistry.HasAny)
            {
                return;
            }

            PugQuerySystem querySystem = World.GetExistingSystemManaged<PugQuerySystem>();
            if (querySystem == null)
            {
                return;
            }

            TileAccessor tiles = new TileAccessor(querySystem);
            EntityManager entityManager = EntityManager;

            using (Unity.Collections.NativeArray<Entity> players =
                   playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                for (int i = 0; i < players.Length; i++)
                {
                    Entity player = players[i];
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(player);

                    int2 cell = new int2(
                        (int)math.round(transform.Position.x),
                        (int)math.round(transform.Position.z));

                    if (!tiles.IsInitialized(cell))
                    {
                        continue;
                    }

                    TileCD top = tiles.GetTop(cell);
                    string biomeId;
                    if (!DimensionRegionTitleRegistry.TryGetBiomeIdForTileset(top.tileset, out biomeId))
                    {
                        // Standing on vanilla ground. Vanilla's own answer is already correct and must
                        // not be overwritten — a player walking out of a custom biome has to get their
                        // real biome back, or the title for it will never fire again.
                        continue;
                    }

                    Biome biome = DimensionBiomeIdentity.GetOrAssign(biomeId);
                    CurrentBiomeCD current = entityManager.GetComponentData<CurrentBiomeCD>(player);
                    if (current.biome == biome)
                    {
                        continue;
                    }

                    current.biome = biome;
                    entityManager.SetComponentData(player, current);
                }
            }
        }
    }
}
