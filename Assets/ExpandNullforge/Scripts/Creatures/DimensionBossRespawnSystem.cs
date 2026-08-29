using System.Collections.Generic;
using ExpandNullforge.Persistence;
using PugMod;
using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Creatures
{
    /// <summary>Each boss's authored respawn cooldown, fed by the generated bootstrap.</summary>
    /// <remarks>
    /// Only bosses with a positive cooldown register here. Vanilla's respawn logic is purely
    /// "is the boss entity gone" — the instant it dies it is summonable again — so the authored
    /// minutes had no consumer at all until this registry and its system existed.
    /// </remarks>
    public static class DimensionBossRespawnRegistry
    {
        private static readonly Dictionary<string, float> CooldownMinutesByBoss =
            new Dictionary<string, float>(System.StringComparer.Ordinal);

        public static bool HasAny
        {
            get { return CooldownMinutesByBoss.Count > 0; }
        }

        public static IReadOnlyDictionary<string, float> All
        {
            get { return CooldownMinutesByBoss; }
        }

        public static void Register(string bossObjectName, float cooldownMinutes)
        {
            if (string.IsNullOrEmpty(bossObjectName) || cooldownMinutes <= 0f)
            {
                return;
            }

            CooldownMinutesByBoss[bossObjectName] = cooldownMinutes;
        }

        public static bool TryGetCooldownMinutes(string bossObjectName, out float minutes)
        {
            return CooldownMinutesByBoss.TryGetValue(bossObjectName, out minutes);
        }

        public static void Clear()
        {
            CooldownMinutesByBoss.Clear();
        }
    }

    /// <summary>A summoning circle's real targets, stashed while its boss cools down.</summary>
    public struct DimensionSummonGateCD : IComponentData
    {
        public ObjectID OriginalBoss;

        public ObjectID OriginalOptional;

        public byte Gated;
    }

    /// <summary>
    /// Makes a defeated boss stay defeated for its authored cooldown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO JOBS, AND THE ORDER WITHIN A TICK IS THE CORRECTNESS. Death detection runs every
    /// tick and, on the first sighting of a registered boss's corpse, records the defeat AND
    /// immediately runs the gate sweep in the same update — the game's summoning check fires
    /// every 0.2 seconds, so a gate that waited for the next sweep interval would let a player
    /// re-drop an idol in the gap after the kill.
    /// </para>
    /// <para>
    /// The gate empties the circle's <c>SummonAreaCD</c> targets and stashes the originals in
    /// <see cref="DimensionSummonGateCD"/>. It also resets the circle's internal state and
    /// timer: a summon already mid-anticipation has LATCHED its target and would complete
    /// regardless of the emptied fields. When the cooldown elapses the stash restores and the
    /// defeat record is removed. Real time, deliberately — the clock keeps running while the
    /// server is down, which is how "come back in thirty minutes" should feel.
    /// </para>
    /// <para>
    /// The writes are plain component writes from a managed server system; the game's summoning
    /// job runs synchronously inside its own update, so the two can interleave at 0.2-second
    /// granularity but never race mid-write.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionBossRespawnSystem : SystemBase
    {
        private const double SweepIntervalSeconds = 1.0d;

        private EntityQuery deadBossQuery;
        private EntityQuery summonAreaQuery;
        private double nextSweepAt;

        /// <summary>Corpses already recorded, so one death is one record.</summary>
        private readonly HashSet<Entity> recordedDeaths = new HashSet<Entity>();

        /// <summary>Resolved id-to-name map, rebuilt lazily like the phase system's.</summary>
        private readonly Dictionary<ObjectID, string> bossNamesById =
            new Dictionary<ObjectID, string>();

        private bool resolvedBossIds;

        protected override void OnCreate()
        {
            // EntityDestroyedCD is enableable and the query only matches while it is ON — the
            // game's own death mark, the exact seam its map-pin removal uses.
            deadBossQuery = GetEntityQuery(
                ComponentType.ReadOnly<BossCD>(),
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<EntityDestroyedCD>());
            summonAreaQuery = GetEntityQuery(ComponentType.ReadWrite<SummonAreaCD>());
        }

        protected override void OnUpdate()
        {
            if (!DimensionBossRespawnRegistry.HasAny)
            {
                return;
            }

            bool defeatLanded = DetectDeaths();

            double now = World.Time.ElapsedTime;
            if (defeatLanded || now >= nextSweepAt)
            {
                nextSweepAt = now + SweepIntervalSeconds;
                SweepGates();
            }
        }

        private bool DetectDeaths()
        {
            if (deadBossQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            EnsureBossIdsResolved();

            bool recorded = false;
            using NativeArray<Entity> entities = deadBossQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<ObjectDataCD> data =
                deadBossQuery.ToComponentDataArray<ObjectDataCD>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (!recordedDeaths.Add(entities[i]))
                {
                    continue;
                }

                string bossName;
                if (!bossNamesById.TryGetValue(data[i].objectID, out bossName))
                {
                    continue;
                }

                DimensionWorldRegistry.UpsertBossDefeat(bossName, System.DateTime.UtcNow.Ticks);
                recorded = true;
            }

            // Forget corpses that are gone, so the set does not grow for the whole session.
            if (recordedDeaths.Count > entities.Length * 2 + 16)
            {
                recordedDeaths.RemoveWhere(entity => !EntityManager.Exists(entity));
            }

            return recorded;
        }

        private void SweepGates()
        {
            if (summonAreaQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            EnsureBossIdsResolved();

            long nowTicks = System.DateTime.UtcNow.Ticks;
            using NativeArray<Entity> entities = summonAreaQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                SummonAreaCD area = EntityManager.GetComponentData<SummonAreaCD>(entity);

                bool hasGate = EntityManager.HasComponent<DimensionSummonGateCD>(entity);
                DimensionSummonGateCD gate = hasGate
                    ? EntityManager.GetComponentData<DimensionSummonGateCD>(entity)
                    : default;

                ObjectID boss = gate.Gated != 0 ? gate.OriginalBoss : area.bossToSummon;
                string bossName;
                if (boss == ObjectID.None || !bossNamesById.TryGetValue(boss, out bossName))
                {
                    continue;
                }

                float cooldownMinutes;
                if (!DimensionBossRespawnRegistry.TryGetCooldownMinutes(bossName, out cooldownMinutes))
                {
                    continue;
                }

                long defeatedTicks;
                bool coolingDown =
                    DimensionWorldRegistry.TryGetBossDefeat(bossName, out defeatedTicks) &&
                    (nowTicks - defeatedTicks) < (long)(cooldownMinutes * System.TimeSpan.TicksPerMinute);

                if (coolingDown && gate.Gated == 0)
                {
                    gate = new DimensionSummonGateCD
                    {
                        OriginalBoss = area.bossToSummon,
                        OriginalOptional = area.optionalBossToSummon,
                        Gated = 1
                    };
                    if (hasGate)
                    {
                        EntityManager.SetComponentData(entity, gate);
                    }
                    else
                    {
                        EntityManager.AddComponentData(entity, gate);
                    }

                    area.bossToSummon = ObjectID.None;
                    area.optionalBossToSummon = ObjectID.None;
                    // The latch: a summon mid-anticipation spawns currentBossToSummon no matter
                    // what the target fields say, so state and timer reset with them.
                    area.currentBossToSummon = ObjectID.None;
                    area.internalState = 0;
                    area.internalTimer = default;
                    EntityManager.SetComponentData(entity, area);
                }
                else if (!coolingDown && gate.Gated != 0)
                {
                    area.bossToSummon = gate.OriginalBoss;
                    area.optionalBossToSummon = gate.OriginalOptional;
                    EntityManager.SetComponentData(entity, area);

                    gate.Gated = 0;
                    EntityManager.SetComponentData(entity, gate);
                    DimensionWorldRegistry.RemoveBossDefeat(bossName);
                }
            }
        }

        private void EnsureBossIdsResolved()
        {
            if (resolvedBossIds && bossNamesById.Count > 0)
            {
                return;
            }

            foreach (KeyValuePair<string, float> entry in DimensionBossRespawnRegistry.All)
            {
                ObjectID id = API.Authoring.GetObjectID(entry.Key);
                if (id != ObjectID.None)
                {
                    bossNamesById[id] = entry.Key;
                    resolvedBossIds = true;
                }
            }
        }
    }
}
