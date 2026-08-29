using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Portals;
using HarmonyLib;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Keeps vanilla's ambient wildlife out of every dimension whose type says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The threat is real and verified: the game's periodic environment spawner round-robins
    /// EVERY loaded submap and enqueues respawn work wherever a player stands — our dimensions
    /// included. Any spawn-table row whose biome or tileset matches a dungeon's tiles would
    /// quietly repopulate it.
    /// </para>
    /// <para>
    /// Two complementary blocks. PRIMARY: the game's own <c>BlockedSpawnAreaCD</c> circles,
    /// which its Burst spawn job already honors with no patch. THE TRAP: a vanilla janitor
    /// system deletes every such circle after thirty seconds — so this system re-zeroes each
    /// circle's age clock every five, defeating the janitor forever rather than racing
    /// recreation. SECONDARY: rows marked "can spawn in blocked areas" ignore circles, so a
    /// postfix on the periodic producer destroys any work order aimed inside a gated
    /// dimension before the consumer can ever see it — the postfix runs synchronously inside
    /// the producer's own update slot, which makes the window airtight.
    /// </para>
    /// <para>
    /// The circle entities deliberately carry NO transform and NO object data: that keeps
    /// them invisible to the arena wipe, to serialization, and to anything that walks
    /// "real" objects.
    /// </para>
    /// <para>
    /// This system is also where the type bundles' deferred diagnostics live: registration
    /// order puts dimensions before their portals, so "a Dungeon needs a return portal" can
    /// only be judged once everything is up — here, once, on the first maintained pass.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionAmbientSpawnGateSystem : SystemBase
    {
        /// <summary>Well under the janitor's 30-second deletion window.</summary>
        private const double MaintainIntervalSeconds = 5.0d;

        /// <summary>One circle covers a square this big; larger dimensions get a grid of them.</summary>
        private const float MaxCircleRadius = 512f;

        private readonly Dictionary<string, List<Entity>> circlesByDimension =
            new Dictionary<string, List<Entity>>(System.StringComparer.Ordinal);

        private readonly HashSet<string> diagnosed = new HashSet<string>(System.StringComparer.Ordinal);

        private double nextMaintainAt;

        protected override void OnUpdate()
        {
            double now = World.Time.ElapsedTime;
            if (now < nextMaintainAt)
            {
                return;
            }

            nextMaintainAt = now + MaintainIntervalSeconds;

            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
            {
                return;
            }

            DimensionAmbientSpawnPolicyCache.Refresh(service);

            IReadOnlyList<DimensionDefinition> dimensions = service.GetDimensions();
            for (int i = 0; i < dimensions.Count; i++)
            {
                DimensionDefinition dimension = dimensions[i];
                DimensionTypePolicy policy = DimensionTypePolicy.For(dimension.Type);
                if (policy.BlocksAmbientSpawns)
                {
                    MaintainCircles(dimension);
                }

                EmitDeferredDiagnosticsOnce(dimension, policy);
            }
        }

        private void MaintainCircles(DimensionDefinition dimension)
        {
            List<Entity> circles;
            if (!circlesByDimension.TryGetValue(dimension.Id, out circles))
            {
                circles = new List<Entity>();
                circlesByDimension[dimension.Id] = circles;
            }

            List<float3> wanted = ComputeCovering(dimension.AbsoluteBounds);

            for (int i = 0; i < wanted.Count; i++)
            {
                float2 center = new float2(wanted[i].x, wanted[i].y);
                float radius = wanted[i].z;

                if (i < circles.Count && EntityManager.Exists(circles[i]))
                {
                    // Defeat the janitor: re-zero the age clock rather than letting the
                    // circle lapse and racing to recreate it.
                    BlockedSpawnAreaCD existing =
                        EntityManager.GetComponentData<BlockedSpawnAreaCD>(circles[i]);
                    existing.Value.ElapsedTicks = 0;
                    existing.Value.Center = center;
                    existing.Value.Radius = radius;
                    EntityManager.SetComponentData(circles[i], existing);
                    continue;
                }

                Entity circle = EntityManager.CreateEntity(typeof(BlockedSpawnAreaCD));
                EntityManager.SetComponentData(circle, new BlockedSpawnAreaCD(center, radius));
                if (i < circles.Count)
                {
                    circles[i] = circle;
                }
                else
                {
                    circles.Add(circle);
                }
            }
        }

        /// <summary>Circle centers (xy) and radii (z) covering the bounds with margin.</summary>
        private static List<float3> ComputeCovering(DimensionBounds bounds)
        {
            List<float3> circles = new List<float3>();
            float2 size = new float2(bounds.Size.x, bounds.Size.y);
            float2 min = new float2(bounds.Min.x, bounds.Min.y);

            float singleRadius = 0.5f * math.length(size) + 8f;
            if (singleRadius <= MaxCircleRadius)
            {
                float2 center = min + size * 0.5f;
                circles.Add(new float3(center.x, center.y, singleRadius));
                return circles;
            }

            // Tile with a grid of circles whose spacing guarantees overlap: a circle of
            // radius r covers a square of side r*sqrt(2).
            float radius = math.min(MaxCircleRadius, 0.5f * math.min(size.x, size.y) + 8f);
            float spacing = radius * 1.41421f;
            for (float y = min.y + spacing * 0.5f; y < min.y + size.y + spacing * 0.5f; y += spacing)
            {
                for (float x = min.x + spacing * 0.5f; x < min.x + size.x + spacing * 0.5f; x += spacing)
                {
                    circles.Add(new float3(x, y, radius));
                }
            }

            return circles;
        }

        private void EmitDeferredDiagnosticsOnce(
            DimensionDefinition dimension,
            DimensionTypePolicy policy)
        {
            if (!policy.RequiresReturnPortal || !diagnosed.Add(dimension.Id))
            {
                return;
            }

            if (!DimensionReturnPortalSpawnRegistry.HasDefinitionForSource(dimension.Id))
            {
                DimensionFrameworkLog.Warning(
                    dimension.Type + " '" + dimension.Id + "' has no " +
                    "return portal. Players can only leave it by dying. Add a return portal " +
                    "for this dimension, or make it a World if one-way is intended.");
            }
        }
    }

    /// <summary>
    /// Which absolute areas block ambient spawning — the lookup the producer postfix uses.
    /// </summary>
    public static class DimensionAmbientSpawnPolicyCache
    {
        private static readonly List<DimensionBounds> BlockedBounds = new List<DimensionBounds>();

        internal static void Refresh(IDimensionService service)
        {
            BlockedBounds.Clear();
            IReadOnlyList<DimensionDefinition> dimensions = service.GetDimensions();
            for (int i = 0; i < dimensions.Count; i++)
            {
                if (DimensionTypePolicy.For(dimensions[i].Type).BlocksAmbientSpawns)
                {
                    BlockedBounds.Add(dimensions[i].AbsoluteBounds);
                }
            }
        }

        /// <summary>Whether the [position, position+size) square touches any gated dimension.</summary>
        public static bool BlocksArea(int2 position, int size)
        {
            for (int i = 0; i < BlockedBounds.Count; i++)
            {
                DimensionBounds bounds = BlockedBounds[i];
                if (position.x < bounds.MaxExclusive.x &&
                    position.x + size > bounds.Min.x &&
                    position.y < bounds.MaxExclusive.y &&
                    position.y + size > bounds.Min.y)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Clear()
        {
            BlockedBounds.Clear();
        }
    }

    /// <summary>
    /// Destroys ambient-spawn work orders aimed inside a gated dimension.
    /// </summary>
    /// <remarks>
    /// The half the circles cannot cover: spawn-table rows flagged to ignore blocked areas.
    /// The producer is fully managed, and staging into the consumer happens in a DIFFERENT
    /// system, so an order destroyed here was never observable by anything.
    /// </remarks>
    [HarmonyPatch(typeof(SpawnEnvironmentObjectsPeriodicallySystem), "OnUpdate")]
    public static class DimensionAmbientSpawnOrderHook
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        private static void Postfix(SpawnEnvironmentObjectsPeriodicallySystem __instance)
        {
            Fired++;

            EntityManager entityManager = __instance.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpawnEnvironmentObjectsCD>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            using NativeArray<SpawnEnvironmentObjectsCD> orders =
                query.ToComponentDataArray<SpawnEnvironmentObjectsCD>(Allocator.Temp);
            for (int i = 0; i < orders.Length; i++)
            {
                if (DimensionAmbientSpawnPolicyCache.BlocksArea(orders[i].position, 16))
                {
                    entityManager.DestroyEntity(entities[i]);
                }
            }
        }
    }
}
