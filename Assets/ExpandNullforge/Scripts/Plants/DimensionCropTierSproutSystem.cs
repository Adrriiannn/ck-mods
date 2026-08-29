using System.Collections.Generic;
using ExpandNullforge.Foundation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// Makes a rare seed sprout into the matching rare plant, for every version past the one the
    /// game promotes by itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THE GAME DOES, AND WHERE IT STOPS. When a seed finishes growing, <c>PlantsGrowJob</c>
    /// reads the seed's baked <c>Seed/rareSeedVariation</c>, compares it against the seed's own
    /// variation, and spawns the plant on <c>Seed/rarePlantVariation</c> when they match — otherwise
    /// on variation 0. One pair, baked into a shared property blob that cannot be written per plant,
    /// inside a Burst-compiled job that cannot be patched. So a seed on a third version's variation
    /// grows a perfectly ordinary plant, and every difference the version was supposed to have —
    /// its produce, its extras — is lost at the moment of sprouting.
    /// </para>
    /// <para>
    /// SO THE PLANT IS REPLACED, NOT REBRANDED. The obvious repair is to write the right number into
    /// the new plant's <c>ObjectDataCD.variation</c>, and it does not work: the entity was built from
    /// the variation-0 prefab, so its <c>PlantCD</c> still names the ordinary produce and its
    /// give-back buffer is still the ordinary one. Changing the label changes nothing behind it.
    /// The plant is destroyed and created again from the version's own prefab instead — which is
    /// what the game itself does for a golden crop, one frame later, at stage zero, so nothing has
    /// been grown yet and nothing is lost.
    /// </para>
    /// <para>
    /// HOW A PLANT IS RECOGNISED AS THAT SEED'S. The seed is gone by the time the plant exists, so
    /// the tile is remembered while the seed is still growing: its position, the plant it says it
    /// turns into, and the version it came up as. A plant is only replaced when it stands on a
    /// remembered tile, is the object that seed named, is still at stage zero, and is on variation 0
    /// — which is exactly the case the game got wrong. Entries are re-recorded every update from the
    /// seeds still in the ground, so a world that was saved and loaded rebuilds them from what is
    /// planted rather than losing them.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// AHEAD OF THE GROWING SYSTEM ON PURPOSE. <c>PlantsGrowingSystem</c> hands every new plant a
    /// pair of timer entities through a command buffer that plays back next frame. Replacing a plant
    /// after it had been queued for timers would leave those commands pointing at an entity that no
    /// longer exists, which fails at playback rather than where the mistake was made. Running first
    /// means the plant is already gone before that job builds its query.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PlantsGrowingSystem))]
    [UpdateAfter(typeof(DimensionCropTierRollSystem))]
    public partial class DimensionCropTierSproutSystem : SystemBase
    {
        /// <summary>
        /// How long a remembered tile survives after the seed on it was last seen.
        /// </summary>
        /// <remarks>
        /// The plant appears the frame after the seed is destroyed, so this only has to outlive one
        /// frame. It is thirty seconds because a server that stalls should not cost a player their
        /// rare crop, and because nothing else reads the entry — a stale one is a few bytes, not a
        /// wrong result.
        /// </remarks>
        private const double RememberSeconds = 30d;

        private struct PendingSprout
        {
            public ObjectID PlantObjectID;

            public int PlantVariation;

            public double ExpiresAt;
        }

        private EntityQuery seedQuery;
        private EntityQuery plantQuery;
        private readonly Dictionary<int2, PendingSprout> pending = new Dictionary<int2, PendingSprout>();
        private readonly List<DimensionCropTier> tiers = new List<DimensionCropTier>();
        private readonly List<int2> expired = new List<int2>();

        protected override void OnCreate()
        {
            seedQuery = GetEntityQuery(
                ComponentType.ReadOnly<DimensionCropTierSeedCD>(),
                ComponentType.ReadOnly<DimensionCropTierBuffer>(),
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<Pug.Properties.ObjectPropertiesCD>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<GrowingCD>());
            plantQuery = GetEntityQuery(
                ComponentType.ReadOnly<DimensionCropTierPlantCD>(),
                ComponentType.ReadOnly<PlantCD>(),
                ComponentType.ReadOnly<GrowingCD>(),
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        protected override void OnUpdate()
        {
            double now = World.Time.ElapsedTime;
            RememberPlantedSeeds(now);

            if (pending.Count == 0)
            {
                return;
            }

            ReplaceSproutedPlants();
            Forget(now);
        }

        private void RememberPlantedSeeds(double now)
        {
            if (seedQuery.IsEmpty)
            {
                return;
            }

            using NativeArray<Entity> entities = seedQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                ObjectDataCD objectData = EntityManager.GetComponentData<ObjectDataCD>(entity);
                if (objectData.variation == 0)
                {
                    continue;
                }

                ReadTiers(entity);
                int plantVariation =
                    DimensionCropTierRoll.PlantVariationForSeedVariation(tiers, objectData.variation);
                if (plantVariation <= 0)
                {
                    continue;
                }

                Pug.Properties.ObjectPropertiesCD properties =
                    EntityManager.GetComponentData<Pug.Properties.ObjectPropertiesCD>(entity);
                ObjectID plantObjectID;
                if (!properties.TryGet(DimensionPlantNames.TurnsIntoPlantPropertyId, out plantObjectID) ||
                    plantObjectID == ObjectID.None)
                {
                    continue;
                }

                LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
                pending[ToTile(transform.Position)] = new PendingSprout
                {
                    PlantObjectID = plantObjectID,
                    PlantVariation = plantVariation,
                    ExpiresAt = now + RememberSeconds
                };
            }
        }

        private void ReplaceSproutedPlants()
        {
            if (plantQuery.IsEmpty)
            {
                return;
            }

            PugDatabase.DatabaseBankCD databaseBank;
            if (!SystemAPI.TryGetSingleton(out databaseBank))
            {
                return;
            }

            using NativeArray<Entity> entities = plantQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                ObjectDataCD objectData = EntityManager.GetComponentData<ObjectDataCD>(entity);
                if (objectData.variation != 0)
                {
                    continue;
                }

                GrowingCD growing = EntityManager.GetComponentData<GrowingCD>(entity);
                if (growing.currentStage != 0)
                {
                    // Past its first stage this is not a plant that just sprouted, and swapping it
                    // would throw away growth the player has already waited through.
                    continue;
                }

                LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
                int2 tile = ToTile(transform.Position);
                PendingSprout sprout;
                if (!pending.TryGetValue(tile, out sprout) ||
                    sprout.PlantObjectID != objectData.objectID)
                {
                    continue;
                }

                pending.Remove(tile);
                EntityManager.DestroyEntity(entity);

                Entity replacement = EntityUtility.CreateEntity(
                    World,
                    transform.Position,
                    objectData.objectID,
                    1,
                    databaseBank.databaseBankBlob,
                    sprout.PlantVariation);

                if (replacement == Entity.Null)
                {
                    // The only way this happens is a version whose plant prefab never registered.
                    // The tile is now empty, which is worse than an ordinary crop, so it is worth
                    // saying rather than leaving the player to notice a vanished plant.
                    DimensionFrameworkLog.Warning(
                        "A crop rolled a version whose plant is not registered at " +
                        "variation " + sprout.PlantVariation + ", so nothing grew on that tile. " +
                        "Generate the mod again so the version's plant prefab exists.");
                }
            }
        }

        private void Forget(double now)
        {
            expired.Clear();
            foreach (KeyValuePair<int2, PendingSprout> entry in pending)
            {
                if (entry.Value.ExpiresAt <= now)
                {
                    expired.Add(entry.Key);
                }
            }

            for (int i = 0; i < expired.Count; i++)
            {
                pending.Remove(expired[i]);
            }
        }

        private void ReadTiers(Entity entity)
        {
            tiers.Clear();
            DynamicBuffer<DimensionCropTierBuffer> buffer =
                EntityManager.GetBuffer<DimensionCropTierBuffer>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                tiers.Add(new DimensionCropTier
                {
                    SeedVariation = buffer[i].SeedVariation,
                    PlantVariation = buffer[i].PlantVariation,
                    ChancePercent = buffer[i].ChancePercent,
                    UsesTheGamesGoldenRoll = buffer[i].UsesTheGamesGoldenRoll != 0
                });
            }
        }

        /// <summary>
        /// The tile a position sits on, rounded the way the game's own tile lookups round.
        /// </summary>
        private static int2 ToTile(float3 position)
        {
            return new int2((int)math.round(position.x), (int)math.round(position.z));
        }
    }
}
