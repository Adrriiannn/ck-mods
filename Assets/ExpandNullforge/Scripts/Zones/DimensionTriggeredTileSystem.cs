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
    /// Fires this mod's triggered tiles when something stands on them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper has traps, but each is an object with its own hardcoded behaviour rather than a
    /// mechanism an author can configure — so the trigger side is structure the framework supplies.
    /// The action side is not: summoning, conditions and damage are all the game's own, which is what
    /// keeps a custom trap feeling like part of Core Keeper rather than a script running over it.
    /// </para>
    /// <para>
    /// THE COOLDOWN IS NOT OPTIONAL. Standing on a tile is a state, not an event: without it a trap
    /// fires every simulation tick a player stands there, which is not a trap but instant death and a
    /// wall of notifications. Even a "once only" trap needs the record, because "already fired" has to
    /// survive the player still being on the tile.
    /// </para>
    /// <para>
    /// Server only. Everything a trigger does is authoritative state.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionTriggeredTileSystem : SystemBase
    {
        /// <summary>When each trigger last fired, so a cooldown can be honoured.</summary>
        private readonly Dictionary<string, double> lastFired =
            new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Triggers that have fired and will not fire again.</summary>
        private readonly HashSet<string> spent = new HashSet<string>(StringComparer.Ordinal);

        private EntityQuery steppableQuery;

        protected override void OnCreate()
        {
            // Anything that can carry a condition and has a position can set one off; whether it
            // actually does is the trigger's own choice, checked per definition.
            steppableQuery = GetEntityQuery(
                ComponentType.ReadWrite<ConditionsBuffer>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<Simulate>(),
                ComponentType.Exclude<ProjectileCD>());

            RequireForUpdate<ConditionsTableCD>();
        }

        protected override void OnUpdate()
        {
            if (!DimensionTriggeredTileRegistry.HasAny)
            {
                return;
            }

            ConditionsTableCD conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>();
            NetworkTick currentTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            uint tickRate = (uint)SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;
            double now = SystemAPI.Time.ElapsedTime;

            EntityManager entityManager = EntityManager;

            using (NativeArray<Entity> entities = steppableQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    float3 position = entityManager.GetComponentData<LocalTransform>(entity).Position;

                    // The same rounding the rest of the framework uses to decide which tile something
                    // is standing on, so a trap and a hazard agree about where a tile is.
                    int2 cell = new int2(
                        (int)math.round(position.x),
                        (int)math.round(position.z));

                    IReadOnlyList<DimensionTriggeredTileDefinition> triggers =
                        DimensionTriggeredTileRegistry.GetAt(cell);
                    if (triggers == null)
                    {
                        continue;
                    }

                    bool isPlayer = entityManager.HasComponent<PlayerGhost>(entity);

                    for (int t = 0; t < triggers.Count; t++)
                    {
                        DimensionTriggeredTileDefinition trigger = triggers[t];
                        if (!ShouldFire(trigger, entity, isPlayer, now))
                        {
                            continue;
                        }

                        lastFired[trigger.TriggerId] = now;
                        if (trigger.OnceOnly)
                        {
                            spent.Add(trigger.TriggerId);
                        }

                        Fire(trigger, entity, cell, conditionsTable, currentTick, tickRate);
                    }
                }
            }
        }

        private bool ShouldFire(
            DimensionTriggeredTileDefinition trigger,
            Entity entity,
            bool isPlayer,
            double now)
        {
            if (spent.Contains(trigger.TriggerId))
            {
                return false;
            }

            double last;
            if (lastFired.TryGetValue(trigger.TriggerId, out last) &&
                now - last < math.max(trigger.CooldownSeconds, 0.25))
            {
                // A floor under the authored cooldown, because zero means "every tick" and nobody
                // means that. A quarter second is still instant to a player and survivable in a log.
                return false;
            }

            switch (trigger.Trigger)
            {
                case DimensionTileTrigger.PlayerSteps:
                    return isPlayer;

                case DimensionTileTrigger.PlayerStepsCarrying:
                    return isPlayer && IsCarrying(entity, trigger.TriggerTarget);

                case DimensionTileTrigger.AnythingSteps:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether an entity is holding a particular item.
        /// </summary>
        /// <remarks>
        /// Fails CLOSED, unlike the progression check: a door that opens for anyone when the inventory
        /// cannot be read is worse than one that stays shut, because the shut door is noticed and the
        /// open one silently undoes the puzzle.
        /// </remarks>
        private bool IsCarrying(Entity entity, string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return false;
            }

            ObjectID wanted = API.Authoring.GetObjectID(itemName);
            if (wanted == ObjectID.None || !EntityManager.HasBuffer<ContainedObjectsBuffer>(entity))
            {
                return false;
            }

            DynamicBuffer<ContainedObjectsBuffer> contents =
                EntityManager.GetBuffer<ContainedObjectsBuffer>(entity, true);

            for (int i = 0; i < contents.Length; i++)
            {
                if (contents[i].objectData.objectID == wanted && contents[i].objectData.amount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Fire(
            DimensionTriggeredTileDefinition trigger,
            Entity victim,
            int2 cell,
            ConditionsTableCD conditionsTable,
            NetworkTick currentTick,
            uint tickRate)
        {
            switch (trigger.Action)
            {
                case DimensionTileAction.SummonCreatures:
                    Summon(trigger, cell);
                    break;

                case DimensionTileAction.ApplyCondition:
                    ApplyCondition(trigger, victim, conditionsTable, currentTick, tickRate);
                    break;

                case DimensionTileAction.Damage:
                    Damage(victim, trigger.ActionAmount);
                    break;
            }
        }

        private void Summon(DimensionTriggeredTileDefinition trigger, int2 cell)
        {
            ObjectID summonId = API.Authoring.GetObjectID(trigger.ActionTarget);
            if (summonId == ObjectID.None)
            {
                // Rate limited: this runs per trigger step while the condition lasts, so saying it
                // once would hide that it is still happening and saying it every time buries
                // everything else.
                DimensionLog.ProblemEvery(
                    DimensionLogChannels.Zone,
                    World,
                    "summon-" + trigger.TriggerId,
                    30f,
                    "triggered tile '" + trigger.TriggerId + "' wants to summon '" +
                    trigger.ActionTarget + "', which is not a registered object.");
                return;
            }

            PugDatabase.DatabaseBankCD databaseBank;
            if (!SystemAPI.TryGetSingleton(out databaseBank))
            {
                return;
            }

            int count = trigger.ActionAmount < 1 ? 1 : trigger.ActionAmount;
            float radius = trigger.Radius <= 0f ? 2f : trigger.Radius;
            float3 origin = new float3(cell.x, 0f, cell.y);

            for (int i = 0; i < count; i++)
            {
                float angle = (math.PI2 / count) * i;
                float3 position = origin + new float3(math.cos(angle), 0f, math.sin(angle)) * radius;
                EntityUtility.CreateEntity(World, position, summonId, 1, databaseBank.databaseBankBlob);
            }
        }

        private void ApplyCondition(
            DimensionTriggeredTileDefinition trigger,
            Entity victim,
            ConditionsTableCD conditionsTable,
            NetworkTick currentTick,
            uint tickRate)
        {
            ConditionID condition;
            if (!Enum.TryParse(trigger.ActionTarget, false, out condition))
            {
                DimensionLog.ProblemEvery(
                    DimensionLogChannels.Zone,
                    World,
                    "condition-" + trigger.TriggerId,
                    30f,
                    "triggered tile '" + trigger.TriggerId + "' names condition '" +
                    trigger.ActionTarget + "', which this version of the game does not have.");
                return;
            }

            EntityUtility.AddOrRefreshCondition(
                victim,
                World,
                condition,
                trigger.ActionAmount < 1 ? 1 : trigger.ActionAmount,
                trigger.ActionDuration,
                conditionsTable,
                currentTick,
                tickRate);
        }

        private void Damage(Entity victim, int amount)
        {
            if (amount <= 0 || !EntityManager.HasComponent<HealthCD>(victim))
            {
                return;
            }

            HealthCD health = EntityManager.GetComponentData<HealthCD>(victim);

            // Floored at zero rather than allowed negative: the game's own death handling reads health
            // as a count, and a negative one has produced odd corpses before.
            health.health = math.max(0, health.health - amount);
            EntityManager.SetComponentData(victim, health);
        }

        /// <summary>Forgets which triggers have fired. The runtime does not need it; tests do.</summary>
        public void ResetForNewWorld()
        {
            lastFired.Clear();
            spent.Clear();
        }
    }
}
