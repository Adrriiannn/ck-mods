using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// Decides which version of a crop a freshly planted seed came up as.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE FRAMEWORK OWNS THIS ROLL. Core Keeper rolls exactly once, for exactly one variation,
    /// at a chance written into the code: <c>SeederSlot</c> and <c>PlaceObjectSlot</c> both do
    /// "three percent plus the player's gardening bonuses, and if it wins place the seed on
    /// <c>rareSeedVariation</c>". There is no table, so a crop with a third version has nowhere to
    /// say how rare it is. This rolls the authored versions instead.
    /// </para>
    /// <para>
    /// IT DOES NOT FIGHT THE GAME'S OWN ROLL. A version that opted into the golden roll is skipped
    /// here, and the game has already placed the seed on that version's variation by the time this
    /// runs — a seed that arrives already on a non-zero variation is left exactly as it is. The
    /// order does mean the game's roll happens first, so a version rolled here competes for what is
    /// left; with vanilla's three percent that is a difference of three parts in a hundred.
    /// </para>
    /// <para>
    /// ZERO IS THE ONLY MARK FOR "NOT ROLLED YET", so an ordinary planting is written onto its own
    /// plain variation rather than left on zero. Without that, every seed already in the ground
    /// would be rolled again on the next world load, and a player could reload until a rare version
    /// came up. The variation is saved with the placed object, so one roll is one roll forever.
    /// </para>
    /// <para>
    /// Server-only, and it runs ahead of the growing system so that even a crop authored to be ready
    /// the instant it is planted has been rolled before it sprouts.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PlantsGrowingSystem))]
    public partial class DimensionCropTierRollSystem : SystemBase
    {
        private EntityQuery seedQuery;
        private readonly List<DimensionCropTier> tiers = new List<DimensionCropTier>();

        protected override void OnCreate()
        {
            seedQuery = GetEntityQuery(
                ComponentType.ReadOnly<DimensionCropTierSeedCD>(),
                ComponentType.ReadOnly<DimensionCropTierBuffer>(),
                ComponentType.ReadWrite<ObjectDataCD>(),
                ComponentType.ReadWrite<RandomCD>(),
                ComponentType.ReadOnly<GrowingCD>());
        }

        protected override void OnUpdate()
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
                if (objectData.variation != 0)
                {
                    continue;
                }

                DimensionCropTierSeedCD seed =
                    EntityManager.GetComponentData<DimensionCropTierSeedCD>(entity);
                if (seed.PlainSeedVariation <= 0)
                {
                    // Nothing to write the "already rolled" mark onto, so rolling would repeat on
                    // every load. Generated seeds always carry one; a hand-built prefab that does
                    // not is left alone rather than rolled unfairly.
                    continue;
                }

                ReadTiers(entity);

                RandomCD random = EntityManager.GetComponentData<RandomCD>(entity);
                float roll = random.Value.NextFloat();
                EntityManager.SetComponentData(entity, random);

                int chosen = DimensionCropTierRoll.Choose(tiers, roll);
                objectData.variation = chosen == DimensionCropTierRoll.NoTier
                    ? seed.PlainSeedVariation
                    : tiers[chosen].SeedVariation;

                // The count is how a view knows its variation moved under it. Without the bump the
                // client keeps whatever look it built when the seed was placed.
                objectData.variationUpdateCount++;
                EntityManager.SetComponentData(entity, objectData);
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
    }
}
