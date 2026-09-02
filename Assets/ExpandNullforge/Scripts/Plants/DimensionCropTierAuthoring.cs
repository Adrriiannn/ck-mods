using Pug.Conversion;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Plants
{

    /// <summary>One authored crop version, as the roll and the sprout systems read it.</summary>
    [InternalBufferCapacity(4)]
    public struct DimensionCropTierBuffer : IBufferElementData
    {
        public int SeedVariation;

        public int PlantVariation;

        public float ChancePercent;

        public byte UsesTheGamesGoldenRoll;
    }

    /// <summary>A planted seed that still has to be told which version it came up as.</summary>
    public struct DimensionCropTierSeedCD : IComponentData
    {
        public int PlainSeedVariation;
    }

    /// <summary>A plant that belongs to a crop with versions, on any of its variations.</summary>
    /// <remarks>
    /// Only a marker. It keeps the sprout system's query down to the handful of plants that could
    /// possibly need replacing, rather than every growing thing in the world.
    /// </remarks>
    public struct DimensionCropTierPlantCD : IComponentData
    {
    }

    /// <summary>Puts the version table on the seed.</summary>
    [Preserve]
    public sealed class DimensionCropTierSeedConverter :
        SingleAuthoringComponentConverter<DimensionCropTierSeedAuthoring>
    {
        protected override void Convert(DimensionCropTierSeedAuthoring authoring)
        {
            if (authoring == null || authoring.seedVariations == null)
            {
                return;
            }

            AddComponentData(new DimensionCropTierSeedCD
            {
                PlainSeedVariation = authoring.plainSeedVariation
            });
            EnsureHasBuffer<DimensionCropTierBuffer>();

            for (int i = 0; i < authoring.seedVariations.Length; i++)
            {
                AddToBuffer(new DimensionCropTierBuffer
                {
                    SeedVariation = authoring.seedVariations[i],
                    PlantVariation = At(authoring.plantVariations, i),
                    ChancePercent = At(authoring.chancePercents, i),
                    UsesTheGamesGoldenRoll = At(authoring.usesTheGamesGoldenRoll, i) ? (byte)1 : (byte)0
                });
            }
        }

        private static int At(int[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : 0;
        }

        private static float At(float[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : 0f;
        }

        private static bool At(bool[] values, int index)
        {
            return values != null && index < values.Length && values[index];
        }
    }

    [Preserve]
    public sealed class DimensionCropTierPlantConverter :
        SingleAuthoringComponentConverter<DimensionCropTierPlantAuthoring>
    {
        protected override void Convert(DimensionCropTierPlantAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new DimensionCropTierPlantCD());
        }
    }
}
