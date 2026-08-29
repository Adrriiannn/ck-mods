using System;
using System.Collections.Generic;
using ExpandNullforge.Foundation;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Fires a boss's authored phases as its health falls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS A NEW SYSTEM WHEN ALMOST NOTHING ELSE IS. Core Keeper's bosses do have phases, but
    /// each is its own hardcoded state machine keyed to that specific boss — there is no reusable
    /// mechanism to borrow, and no data table to append to. The structure genuinely has to come from
    /// somewhere. What still does not come from here is the EFFECTS: summoning, conditions and healing
    /// are all the game's own, so an authored phase behaves like something the game did.
    /// </para>
    /// <para>
    /// EACH PHASE FIRES ONCE PER BOSS. Health in a fight is not monotonic — a boss that heals, or one
    /// whose health ticks back up between hits, would otherwise re-trigger its phase every frame it
    /// spent near the threshold, which reads in game as an endless stream of adds. The crossed set is
    /// keyed on the boss entity so two of the same boss fight independently.
    /// </para>
    /// <para>
    /// Server only. Phases spawn creatures and apply conditions, both of which are authoritative
    /// state; running this on a client would either desync or duplicate.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionBossPhaseSystem : SystemBase
    {
        /// <summary>
        /// Which phases each living boss has already passed.
        /// </summary>
        /// <remarks>
        /// Keyed on the entity rather than the boss type so two copies of the same boss — a
        /// double-summon, or two arenas open at once — each run their own fight.
        /// </remarks>
        private readonly Dictionary<Entity, HashSet<string>> crossed =
            new Dictionary<Entity, HashSet<string>>();

        /// <summary>
        /// Which registered boss each object id is, resolved once the object database exists.
        /// </summary>
        /// <remarks>
        /// Phases are registered by NAME at mod load, long before the database has turned names into
        /// ids. Resolving lazily — and caching — keeps the per-frame path to a dictionary lookup on an
        /// id we already have, rather than a string comparison per boss per tick.
        /// </remarks>
        private readonly Dictionary<ObjectID, IReadOnlyList<DimensionBossPhaseDefinition>> phasesByObject =
            new Dictionary<ObjectID, IReadOnlyList<DimensionBossPhaseDefinition>>();

        private bool resolvedPhaseObjects;

        private EntityQuery bossQuery;

        protected override void OnCreate()
        {
            bossQuery = GetEntityQuery(
                ComponentType.ReadOnly<BossCD>(),
                ComponentType.ReadOnly<HealthCD>(),
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<Simulate>());

            RequireForUpdate<ConditionsTableCD>();
        }

        protected override void OnUpdate()
        {
            if (!DimensionBossPhaseRegistry.HasAny)
            {
                return;
            }

            ResolvePhaseObjects();
            if (phasesByObject.Count == 0)
            {
                return;
            }

            ConditionsTableCD conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>();
            NetworkTick currentTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            uint tickRate = (uint)SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;

            EntityManager entityManager = EntityManager;

            using (NativeArray<Entity> bosses = bossQuery.ToEntityArray(Allocator.Temp))
            {
                ForgetDeadBosses(bosses);

                for (int i = 0; i < bosses.Length; i++)
                {
                    Entity boss = bosses[i];
                    HealthCD health = entityManager.GetComponentData<HealthCD>(boss);
                    if (health.maxHealth <= 0)
                    {
                        continue;
                    }

                    ObjectDataCD objectData = entityManager.GetComponentData<ObjectDataCD>(boss);
                    IReadOnlyList<DimensionBossPhaseDefinition> phases;
                    if (!phasesByObject.TryGetValue(objectData.objectID, out phases) || phases.Count == 0)
                    {
                        continue;
                    }

                    float fraction = health.health / (float)health.maxHealth;
                    HashSet<string> alreadyCrossed = GetCrossedSet(boss);

                    for (int p = 0; p < phases.Count; p++)
                    {
                        DimensionBossPhaseDefinition phase = phases[p];
                        if (fraction > phase.HealthThreshold || alreadyCrossed.Contains(phase.PhaseId))
                        {
                            continue;
                        }

                        alreadyCrossed.Add(phase.PhaseId);
                        Fire(boss, phase, conditionsTable, currentTick, tickRate);
                    }
                }
            }
        }

        /// <summary>
        /// Turns each registered boss's name into the object id the world reports.
        /// </summary>
        /// <remarks>
        /// Runs once, on the first update after the object database exists. A boss whose name resolves
        /// to nothing is reported rather than skipped silently — "my boss has no phases" is a symptom
        /// with no other explanation anywhere.
        /// </remarks>
        private void ResolvePhaseObjects()
        {
            if (resolvedPhaseObjects)
            {
                return;
            }

            IReadOnlyList<string> bossNames = DimensionBossPhaseRegistry.BossNames;
            for (int i = 0; i < bossNames.Count; i++)
            {
                ObjectID objectID = API.Authoring.GetObjectID(bossNames[i]);
                if (objectID == ObjectID.None)
                {
                    DimensionLog.ProblemEvery(
                        DimensionLogChannels.Boss,
                        World,
                        "unregistered-" + bossNames[i],
                        30f,
                        "boss '" + bossNames[i] + "' has phases but is not a " +
                        "registered object, so none of them will ever fire.");
                    continue;
                }

                phasesByObject[objectID] = DimensionBossPhaseRegistry.GetPhases(bossNames[i]);
            }

            resolvedPhaseObjects = true;
        }

        /// <summary>
        /// Drops remembered state for bosses that no longer exist.
        /// </summary>
        /// <remarks>
        /// Without this the dictionary grows for the life of the world, and — worse — a recycled
        /// entity index could inherit a dead boss's crossed phases and skip its own opening.
        /// </remarks>
        private void ForgetDeadBosses(NativeArray<Entity> alive)
        {
            if (crossed.Count == 0)
            {
                return;
            }

            HashSet<Entity> living = new HashSet<Entity>();
            for (int i = 0; i < alive.Length; i++)
            {
                living.Add(alive[i]);
            }

            List<Entity> gone = null;
            foreach (KeyValuePair<Entity, HashSet<string>> pair in crossed)
            {
                if (!living.Contains(pair.Key))
                {
                    gone = gone ?? new List<Entity>();
                    gone.Add(pair.Key);
                }
            }

            if (gone == null)
            {
                return;
            }

            // Any tracked boss leaving the world ends its phase music with it.
            DimensionMusicOverrideRegistry.ClearPhaseOverride();

            for (int i = 0; i < gone.Count; i++)
            {
                crossed.Remove(gone[i]);
            }
        }

        private HashSet<string> GetCrossedSet(Entity boss)
        {
            HashSet<string> set;
            if (!crossed.TryGetValue(boss, out set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                crossed.Add(boss, set);
            }

            return set;
        }

        private void Fire(
            Entity boss,
            DimensionBossPhaseDefinition phase,
            ConditionsTableCD conditionsTable,
            NetworkTick currentTick,
            uint tickRate)
        {
            // The phase carries its own music: it wins immediately and holds until the
            // boss dies or despawns. Host-side only — a remote client keeps the fight cue.
            if (!string.IsNullOrEmpty(phase.MusicCueId))
            {
                DimensionMusicOverrideRegistry.SetPhaseOverride(phase.MusicCueId);
            }

            switch (phase.Action)
            {
                case DimensionBossPhaseAction.SummonAdds:
                    SummonAdds(boss, phase);
                    break;

                case DimensionBossPhaseAction.ApplyConditionToSelf:
                    ApplyCondition(boss, phase, conditionsTable, currentTick, tickRate);
                    break;

                case DimensionBossPhaseAction.ApplyConditionToPlayers:
                    ApplyConditionToPlayers(boss, phase, conditionsTable, currentTick, tickRate);
                    break;

                case DimensionBossPhaseAction.Heal:
                    Heal(boss, phase);
                    break;
            }
        }

        private void SummonAdds(Entity boss, DimensionBossPhaseDefinition phase)
        {
            ObjectID addId = API.Authoring.GetObjectID(phase.ActionTarget);
            if (addId == ObjectID.None)
            {
                // Rate limited: a phase re-checks on every tick it is active.
                DimensionLog.ProblemEvery(
                    DimensionLogChannels.Boss,
                    World,
                    "summon-" + phase.PhaseId,
                    30f,
                    "boss phase '" + phase.PhaseId + "' wants to summon '" +
                    phase.ActionTarget + "', which is not a registered object. Nothing was summoned.");
                return;
            }

            PugDatabase.DatabaseBankCD databaseBank;
            if (!SystemAPI.TryGetSingleton(out databaseBank))
            {
                return;
            }

            float3 origin = EntityManager.GetComponentData<LocalTransform>(boss).Position;
            int count = phase.ActionAmount < 1 ? 1 : phase.ActionAmount;
            float radius = phase.Radius <= 0f ? 3f : phase.Radius;

            for (int i = 0; i < count; i++)
            {
                // Spread evenly around the boss rather than randomly: a summon that happens to stack
                // all its adds on one tile reads as a bug, and an even ring is what the game's own
                // summons look like.
                float angle = (math.PI2 / count) * i;
                float3 position = origin + new float3(math.cos(angle), 0f, math.sin(angle)) * radius;

                EntityUtility.CreateEntity(World, position, addId, 1, databaseBank.databaseBankBlob);
            }
        }

        private void ApplyCondition(
            Entity target,
            DimensionBossPhaseDefinition phase,
            ConditionsTableCD conditionsTable,
            NetworkTick currentTick,
            uint tickRate)
        {
            ConditionID condition;
            if (!Enum.TryParse(phase.ActionTarget, false, out condition))
            {
                DimensionLog.ProblemEvery(
                    DimensionLogChannels.Boss,
                    World,
                    "condition-" + phase.PhaseId,
                    30f,
                    "boss phase '" + phase.PhaseId + "' names condition '" +
                    phase.ActionTarget + "', which this version of the game does not have.");
                return;
            }

            EntityUtility.AddOrRefreshCondition(
                target,
                World,
                condition,
                phase.ActionAmount < 1 ? 1 : phase.ActionAmount,
                phase.ActionDuration,
                conditionsTable,
                currentTick,
                tickRate);
        }

        /// <summary>
        /// Applies a condition to every player close enough to be in the fight.
        /// </summary>
        /// <remarks>
        /// Range-limited on purpose. A raid mechanic that reaches someone standing in their base on
        /// the other side of the world is not a mechanic, it is a bug report.
        /// </remarks>
        private void ApplyConditionToPlayers(
            Entity boss,
            DimensionBossPhaseDefinition phase,
            ConditionsTableCD conditionsTable,
            NetworkTick currentTick,
            uint tickRate)
        {
            float3 origin = EntityManager.GetComponentData<LocalTransform>(boss).Position;
            float radius = phase.Radius <= 0f ? 12f : phase.Radius;
            float radiusSquared = radius * radius;

            EntityQuery players = GetEntityQuery(
                ComponentType.ReadOnly<PlayerGhost>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<ConditionsBuffer>());

            using (NativeArray<Entity> entities = players.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    float3 position = EntityManager.GetComponentData<LocalTransform>(entities[i]).Position;
                    if (math.distancesq(position, origin) > radiusSquared)
                    {
                        continue;
                    }

                    ApplyCondition(entities[i], phase, conditionsTable, currentTick, tickRate);
                }
            }
        }

        private void Heal(Entity boss, DimensionBossPhaseDefinition phase)
        {
            HealthCD health = EntityManager.GetComponentData<HealthCD>(boss);

            // Clamped to maximum: a phase that heals past full would leave the boss above 100%, and
            // every later threshold would then fire again on the way back down.
            health.health = math.min(health.maxHealth, health.health + phase.ActionAmount);
            EntityManager.SetComponentData(boss, health);
        }
    }
}
