using Unity.Entities;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationContext
    {
        public readonly World ServerWorld;
        public readonly DimensionDefinition Dimension;
        public readonly DimensionArea Area;
        public readonly DimensionGenerationStatus CurrentStatus;
        public readonly double ElapsedSeconds;

        public DimensionGenerationContext(
            World serverWorld,
            DimensionDefinition dimension,
            DimensionArea area,
            DimensionGenerationStatus currentStatus,
            double elapsedSeconds)
        {
            ServerWorld = serverWorld;
            Dimension = dimension;
            Area = area;
            CurrentStatus = currentStatus;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}
