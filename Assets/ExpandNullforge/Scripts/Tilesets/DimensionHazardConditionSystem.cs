using ExpandNullforge.Foundation;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Makes a custom block's slime actually hurt, using Core Keeper's own conditions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS RATHER THAN A PATCH. Vanilla already does this job, in
    /// <c>EnvironmentalConditionsSystem</c>, and does it well — but its worker job carries
    /// struct-level <c>[BurstCompile]</c>, so a Harmony patch on it never takes effect. The framework
    /// does own a workaround for that (disable Burst for the system, then force its dependency to
    /// complete), and it is deliberately <b>not</b> used here: that job runs over <em>every entity
    /// carrying a conditions buffer, every tick</em>, so de-Bursting it would impose a permanent cost
    /// on every player of the game, including those who never open a dimension. Trading everyone's
    /// framerate for one mod's feature is the wrong deal.
    /// </para>
    /// <para>
    /// WHAT THIS IS INSTEAD. A narrow companion that applies <b>the game's own conditions</b> —
    /// nothing invented, no new vocabulary, no new effect — to entities standing on tilesets vanilla
    /// has never heard of. Vanilla's system keeps running, Bursted and untouched, and keeps full
    /// authority over vanilla ground. The two never look at the same tile: vanilla's checks resolve
    /// on vanilla tileset ids, and this one only ever fires on ids in the custom range.
    /// </para>
    /// <para>
    /// COST WHEN UNUSED IS ZERO. The whole system requires a registration to exist before it will
    /// run at all, and that registry is empty unless a mod actually ships a hazardous block. A
    /// player with no such mod pays for one boolean check at system-update time.
    /// </para>
    /// <para>
    /// The values below are vanilla's, read from its own applications so a custom acid burns for
    /// exactly what acid burns for. They are not balance decisions of ours to make.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionHazardConditionSystem : SystemBase
    {
        private EntityQuery affectedQuery;

        protected override void OnCreate()
        {
            // Mirrors vanilla's own selection: anything that can carry conditions and is simulated,
            // minus the two kinds it explicitly excludes. Projectiles and destructibles pass through
            // hazards without being affected by them, and copying that exclusion keeps a custom acid
            // pool from setting fire to arrows flying over it.
            affectedQuery = GetEntityQuery(
                ComponentType.ReadWrite<ConditionsBuffer>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<Simulate>(),
                ComponentType.Exclude<ProjectileCD>(),
                ComponentType.Exclude<DestructibleObjectCD>());

            RequireForUpdate<ConditionsTableCD>();
        }

        protected override void OnUpdate()
        {
            if (!DimensionTilesetBehaviourRegistry.HasAny)
            {
                return;
            }

            PugQuerySystem querySystem = World.GetExistingSystemManaged<PugQuerySystem>();
            if (querySystem == null)
            {
                return;
            }

            ConditionsTableCD conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>();
            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();
            NetworkTick currentTick = networkTime.ServerTick;
            uint tickRate = (uint)SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;

            TileAccessor tiles = new TileAccessor(querySystem);
            EntityManager entityManager = EntityManager;

            using (Unity.Collections.NativeArray<Entity> entities =
                   affectedQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);

                    // The same rounding vanilla uses to decide which tile something is standing on.
                    int2 cell = new int2(
                        (int)math.round(transform.Position.x),
                        (int)math.round(transform.Position.z));

                    if (!tiles.IsInitialized(cell))
                    {
                        continue;
                    }

                    TileCD top = tiles.GetTop(cell);
                    DimensionTilesetGroundBehaviour behaviour =
                        DimensionTilesetBehaviourRegistry.GetGroundBehaviour(top.tileType, top.tileset);
                    if (behaviour == DimensionTilesetGroundBehaviour.None)
                    {
                        continue;
                    }

                    Apply(entity, behaviour, conditionsTable, currentTick, tickRate);
                }
            }
        }

        /// <summary>
        /// Applies the condition vanilla applies for this behaviour, with vanilla's own numbers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Nothing is removed when the entity steps off. That is deliberate and matches the game:
        /// every condition here either expires on its own timer or is a refresh-while-standing effect
        /// the condition system already ages out. Actively clearing them would cut short a poison the
        /// player is meant to carry away from the pool.
        /// </para>
        /// <para>
        /// Plain slime is intentionally absent. Vanilla's generic-slime handling is bound up with a
        /// physics query against the objects sitting on the tile, which is not something worth
        /// reproducing for what amounts to a movement nudge — the visual and audio side already lands
        /// through the player-controller patch, which is what sells it.
        /// </para>
        /// </remarks>
        private void Apply(
            Entity entity,
            DimensionTilesetGroundBehaviour behaviour,
            ConditionsTableCD conditionsTable,
            NetworkTick currentTick,
            uint tickRate)
        {
            switch (behaviour)
            {
                case DimensionTilesetGroundBehaviour.Acid:
                    EntityUtility.AddOrRefreshCondition(
                        entity, World, ConditionID.AcidDamage, 12, 0f, conditionsTable, currentTick, tickRate);
                    break;

                case DimensionTilesetGroundBehaviour.PoisonSlime:
                    EntityUtility.AddOrRefreshCondition(
                        entity, World, ConditionID.Poisoned, 1, 15f, conditionsTable, currentTick, tickRate);
                    break;

                case DimensionTilesetGroundBehaviour.SlipperySlime:
                    EntityUtility.AddOrRefreshCondition(
                        entity, World, ConditionID.SlipperyMovementFromGround, 1, 0f,
                        conditionsTable, currentTick, tickRate);
                    break;

                case DimensionTilesetGroundBehaviour.Oil:
                    EntityUtility.AddOrRefreshCondition(
                        entity, World, ConditionID.DrenchedInOil, 100, 10f,
                        conditionsTable, currentTick, tickRate);
                    break;
            }
        }
    }
}
