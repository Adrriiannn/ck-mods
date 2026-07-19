namespace ExpandNullforge.Api
{
    public interface IDimensionGenerationProvider
    {
        string ProviderId { get; }

        bool CanGenerate(DimensionDefinition dimension, DimensionBounds localBounds);

        DimensionGenerationProviderResult TickGeneration(DimensionGenerationContext context);
    }
}
