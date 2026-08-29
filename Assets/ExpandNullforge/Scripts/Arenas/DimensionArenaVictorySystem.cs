using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Portals;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Arenas
{
    /// <summary>
    /// Notices an arena's boss go down, and arms the way out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE SIGNAL IS HEALTH REACHING ZERO ON A BOSS THAT WAS SEEN ALIVE INSIDE. Entity
    /// disappearance is deliberately not a defeat — a boss despawning because everyone left
    /// is indistinguishable from a kill by absence alone. Bosses hold their entity through
    /// the death sequence with health at or below zero, which is the window this reads.
    /// </para>
    /// <para>
    /// Victory writes two things on purpose: the session arm (what the portal spawner reads)
    /// and a persisted progress flag. A victor who quits before stepping through still finds
    /// the portal on their next visit, because the first tick re-arms from the flag. The
    /// arena reset clears both — the next visitor earns their own exit.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionArenaVictorySystem : SystemBase
    {
        private const double PollIntervalSeconds = 0.5d;

        private readonly Dictionary<string, HashSet<Entity>> seenAliveInside =
            new Dictionary<string, HashSet<Entity>>(System.StringComparer.Ordinal);

        private EntityQuery bossQuery;
        private double nextPollAt;
        private bool rearmedFromFlags;

        protected override void OnCreate()
        {
            bossQuery = GetEntityQuery(
                ComponentType.ReadOnly<BossCD>(),
                ComponentType.ReadOnly<HealthCD>(),
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        protected override void OnUpdate()
        {
            double now = World.Time.ElapsedTime;
            if (now < nextPollAt)
            {
                return;
            }

            nextPollAt = now + PollIntervalSeconds;

            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
            {
                return;
            }

            IReadOnlyList<DimensionDefinition> dimensions = service.GetDimensions();

            if (!rearmedFromFlags)
            {
                RearmFromPersistedFlags(service, dimensions);
                rearmedFromFlags = true;
            }

            if (bossQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using NativeArray<Entity> entities = bossQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<HealthCD> healths =
                bossQuery.ToComponentDataArray<HealthCD>(Allocator.Temp);
            using NativeArray<LocalTransform> transforms =
                bossQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                float2 absolute = new float2(transforms[i].Position.x, transforms[i].Position.z);
                string arenaId = FindOwningArena(dimensions, absolute);
                if (arenaId == null)
                {
                    continue;
                }

                HashSet<Entity> seen;
                if (!seenAliveInside.TryGetValue(arenaId, out seen))
                {
                    seen = new HashSet<Entity>();
                    seenAliveInside[arenaId] = seen;
                }

                if (healths[i].health > 0)
                {
                    seen.Add(entities[i]);
                }
                else if (seen.Remove(entities[i]))
                {
                    OnVictory(service, arenaId);
                }
            }
        }

        private static string FindOwningArena(
            IReadOnlyList<DimensionDefinition> dimensions,
            float2 absolutePosition)
        {
            for (int i = 0; i < dimensions.Count; i++)
            {
                if (DimensionTypePolicy.For(dimensions[i].Type).ReturnPortalArmedByVictory &&
                    dimensions[i].AbsoluteBounds.Contains(absolutePosition))
                {
                    return dimensions[i].Id;
                }
            }

            return null;
        }

        private static void OnVictory(IDimensionService service, string arenaId)
        {
            DimensionOperationResult result;
            service.TrySetProgressFlag(
                DimensionArenaResetSystem.VictoryFlagId(arenaId),
                arenaId,
                "arena",
                true,
                "The arena's boss went down.",
                out result);

            ArmPortals(arenaId);
            DimensionFrameworkLog.Info(
                "Arena '" + arenaId + "' won; its exit portal is armed.");
        }

        private static void RearmFromPersistedFlags(
            IDimensionService service,
            IReadOnlyList<DimensionDefinition> dimensions)
        {
            for (int i = 0; i < dimensions.Count; i++)
            {
                if (!DimensionTypePolicy.For(dimensions[i].Type).ReturnPortalArmedByVictory)
                {
                    continue;
                }

                DimensionProgressFlag flag;
                if (service.TryGetProgressFlag(
                        DimensionArenaResetSystem.VictoryFlagId(dimensions[i].Id),
                        out flag) &&
                    flag.Value)
                {
                    ArmPortals(dimensions[i].Id);
                }
            }
        }

        private static void ArmPortals(string arenaId)
        {
            for (int i = 0; i < DimensionReturnPortalSpawnRegistry.Count; i++)
            {
                DimensionReturnPortalSpawnDefinition definition;
                if (DimensionReturnPortalSpawnRegistry.TryGet(i, out definition) &&
                    definition.ArmedByVictory &&
                    string.Equals(definition.SourceDimensionId, arenaId, System.StringComparison.Ordinal))
                {
                    DimensionReturnPortalSpawnRegistry.Arm(definition.PortalId);
                }
            }
        }
    }
}
