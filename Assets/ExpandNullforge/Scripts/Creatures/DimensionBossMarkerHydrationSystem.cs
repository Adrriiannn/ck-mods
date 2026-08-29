using PugMod;
using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Writes each boss marker's real ObjectID into the components the game reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BOTH WORLDS, AND IT HAS TO BE. <c>MapMarkerCD</c> has no ghost fields — the client's copy
    /// comes from its own baked prefab, not from the server — so a server-only write would leave
    /// every client pin with an id of None forever, rendering as the broken icon-less marker.
    /// Name-to-id resolution is deterministic (the object database is identical in both worlds),
    /// so double-running is safe by construction.
    /// </para>
    /// <para>
    /// The window is generous: hydration lands within the entity's first ticks, and the pin is
    /// only read when a player opens the map. Even a missed tick degrades to one icon-less
    /// frame and a log line, self-healed on the next map refresh.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionBossMarkerHydrationSystem : SystemBase
    {
        private EntityQuery markerQuery;

        protected override void OnCreate()
        {
            markerQuery = GetEntityQuery(
                ComponentType.ReadWrite<DimensionBossMarkerCD>(),
                ComponentType.ReadWrite<MapMarkerCD>());
            RequireForUpdate(markerQuery);
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> entities = markerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                DimensionBossMarkerCD marker = EntityManager.GetComponentData<DimensionBossMarkerCD>(entity);
                if (marker.Hydrated != 0)
                {
                    continue;
                }

                string bossName = marker.BossName.ToString();
                if (string.IsNullOrEmpty(bossName))
                {
                    marker.Hydrated = 1;
                    EntityManager.SetComponentData(entity, marker);
                    continue;
                }

                ObjectID bossId = API.Authoring.GetObjectID(bossName);
                if (bossId == ObjectID.None)
                {
                    // The object database may simply not be ready yet; try again next tick.
                    // A permanently unresolvable name keeps the pin generic, never crashes.
                    continue;
                }

                MapMarkerCD mapMarker = EntityManager.GetComponentData<MapMarkerCD>(entity);
                mapMarker.uniqueMarkerId = bossId;
                EntityManager.SetComponentData(entity, mapMarker);

                // The scan linkage is what makes the pin die with its boss: the death system
                // walks every scannable entity and disables the ones naming the dead object.
                if (EntityManager.HasComponent<CanBeScannedCD>(entity))
                {
                    CanBeScannedCD scan = EntityManager.GetComponentData<CanBeScannedCD>(entity);
                    scan.objectData.objectID = bossId;
                    EntityManager.SetComponentData(entity, scan);
                }

                marker.Hydrated = 1;
                EntityManager.SetComponentData(entity, marker);
            }
        }
    }
}
