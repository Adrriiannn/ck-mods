using PugMod;
using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Turns baked boss NAMES into the ObjectIDs the game's summoning actually reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Server-only on purpose: <c>BossSummoningSystem</c> is a server system and reads both the
    /// item buffer and the area component server-side; the client has nothing to look at.
    /// </para>
    /// <para>
    /// The timing window is generous by construction. The game's summoning check runs on a
    /// 0.2-second throttle over items already lying near a circle; hydration lands on the
    /// entity's first simulated tick — sub-frame against sub-second. A name that never resolves
    /// (the object failed to register) leaves the circle idling and the item inert, with one
    /// warning naming the name, once.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionSummonHydrationSystem : SystemBase
    {
        private EntityQuery itemQuery;
        private EntityQuery areaQuery;
        private EntityQuery shopQuery;
        private EntityQuery hatchQuery;
        private readonly System.Collections.Generic.HashSet<string> warnedNames =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        protected override void OnCreate()
        {
            itemQuery = GetEntityQuery(
                ComponentType.ReadWrite<DimensionSummonHydrateCD>(),
                ComponentType.ReadWrite<SummoningItemBuffer>(),
                ComponentType.ReadOnly<DimensionSummonBossNameBuffer>());
            areaQuery = GetEntityQuery(
                ComponentType.ReadWrite<DimensionSummonAreaNameCD>(),
                ComponentType.ReadWrite<SummonAreaCD>());
            shopQuery = GetEntityQuery(
                ComponentType.ReadWrite<DimensionShopHydrateCD>(),
                ComponentType.ReadWrite<VendingMachineItemBuffer>(),
                ComponentType.ReadOnly<DimensionShopStockNameBuffer>());
            hatchQuery = GetEntityQuery(
                ComponentType.ReadWrite<DimensionHatchTargetCD>(),
                ComponentType.ReadWrite<HatchWhenPlayerNearbyStateCD>());
        }

        protected override void OnUpdate()
        {
            HydrateItems();
            HydrateAreas();
            HydrateShops();
            HydrateHatchers();
        }

        /// <summary>Shop stock: names become the vending buffer the buy window prices from.</summary>
        private void HydrateShops()
        {
            using NativeArray<Entity> entities = shopQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                DimensionShopHydrateCD state =
                    EntityManager.GetComponentData<DimensionShopHydrateCD>(entity);
                if (state.Hydrated != 0)
                {
                    continue;
                }

                DynamicBuffer<DimensionShopStockNameBuffer> names =
                    EntityManager.GetBuffer<DimensionShopStockNameBuffer>(entity);
                bool allResolved = true;
                for (int n = 0; n < names.Length; n++)
                {
                    string itemName = names[n].ItemName.ToString();
                    if (string.IsNullOrEmpty(itemName))
                    {
                        continue;
                    }

                    ObjectID itemId = API.Authoring.GetObjectID(itemName);
                    if (itemId == ObjectID.None)
                    {
                        allResolved = false;
                        WarnOnce(itemName, "shop");
                        continue;
                    }

                    DynamicBuffer<VendingMachineItemBuffer> stock =
                        EntityManager.GetBuffer<VendingMachineItemBuffer>(entity);
                    bool present = false;
                    for (int s = 0; s < stock.Length && !present; s++)
                    {
                        present = stock[s].objectID == itemId;
                    }

                    if (!present)
                    {
                        stock.Add(new VendingMachineItemBuffer { objectID = itemId });
                    }
                }

                if (allResolved)
                {
                    state.Hydrated = 1;
                    EntityManager.SetComponentData(entity, state);
                }
            }
        }

        /// <summary>Hatchers: the name becomes what actually crawls out.</summary>
        private void HydrateHatchers()
        {
            using NativeArray<Entity> entities = hatchQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                DimensionHatchTargetCD target =
                    EntityManager.GetComponentData<DimensionHatchTargetCD>(entity);
                if (target.Hydrated != 0)
                {
                    continue;
                }

                string spawnName = target.SpawnName.ToString();
                if (string.IsNullOrEmpty(spawnName))
                {
                    target.Hydrated = 1;
                    EntityManager.SetComponentData(entity, target);
                    continue;
                }

                ObjectID spawnId = API.Authoring.GetObjectID(spawnName);
                if (spawnId == ObjectID.None)
                {
                    WarnOnce(spawnName, "hatching egg");
                    continue;
                }

                HatchWhenPlayerNearbyStateCD hatch =
                    EntityManager.GetComponentData<HatchWhenPlayerNearbyStateCD>(entity);
                hatch.objectToSpawn = spawnId;
                EntityManager.SetComponentData(entity, hatch);

                target.Hydrated = 1;
                EntityManager.SetComponentData(entity, target);
            }
        }

        private void HydrateItems()
        {
            using NativeArray<Entity> entities = itemQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                DimensionSummonHydrateCD state =
                    EntityManager.GetComponentData<DimensionSummonHydrateCD>(entity);
                if (state.Hydrated != 0)
                {
                    continue;
                }

                DynamicBuffer<DimensionSummonBossNameBuffer> names =
                    EntityManager.GetBuffer<DimensionSummonBossNameBuffer>(entity);
                bool allResolved = true;
                for (int n = 0; n < names.Length; n++)
                {
                    string bossName = names[n].BossName.ToString();
                    if (string.IsNullOrEmpty(bossName))
                    {
                        continue;
                    }

                    ObjectID bossId = API.Authoring.GetObjectID(bossName);
                    if (bossId == ObjectID.None)
                    {
                        allResolved = false;
                        WarnOnce(bossName, "summoning item");
                        continue;
                    }

                    DynamicBuffer<SummoningItemBuffer> vanilla =
                        EntityManager.GetBuffer<SummoningItemBuffer>(entity);
                    if (!Contains(vanilla, bossId))
                    {
                        vanilla.Add(new SummoningItemBuffer { bossToSummon = bossId });
                    }
                }

                if (allResolved)
                {
                    state.Hydrated = 1;
                    EntityManager.SetComponentData(entity, state);
                }
            }
        }

        private void HydrateAreas()
        {
            using NativeArray<Entity> entities = areaQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                DimensionSummonAreaNameCD names =
                    EntityManager.GetComponentData<DimensionSummonAreaNameCD>(entity);
                if (names.Hydrated != 0)
                {
                    continue;
                }

                string bossName = names.Boss.ToString();
                if (string.IsNullOrEmpty(bossName))
                {
                    names.Hydrated = 1;
                    EntityManager.SetComponentData(entity, names);
                    continue;
                }

                ObjectID bossId = API.Authoring.GetObjectID(bossName);
                if (bossId == ObjectID.None)
                {
                    WarnOnce(bossName, "summoning circle");
                    continue;
                }

                SummonAreaCD area = EntityManager.GetComponentData<SummonAreaCD>(entity);
                area.bossToSummon = bossId;

                string optionalName = names.Optional.ToString();
                if (!string.IsNullOrEmpty(optionalName))
                {
                    // The optional boss failing to resolve does not hold the circle hostage —
                    // the main boss is the point; the alternate is flavor.
                    area.optionalBossToSummon = API.Authoring.GetObjectID(optionalName);
                }

                EntityManager.SetComponentData(entity, area);
                names.Hydrated = 1;
                EntityManager.SetComponentData(entity, names);
            }
        }

        private static bool Contains(DynamicBuffer<SummoningItemBuffer> buffer, ObjectID bossId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].bossToSummon == bossId)
                {
                    return true;
                }
            }

            return false;
        }

        private void WarnOnce(string bossName, string where)
        {
            if (warnedNames.Add(bossName))
            {
                Foundation.DimensionFrameworkLog.Warning(
                    "A " + where + " names boss '" + bossName + "', which " +
                    "resolves to no object. It will stay inert until the boss registers.");
            }
        }
    }
}
