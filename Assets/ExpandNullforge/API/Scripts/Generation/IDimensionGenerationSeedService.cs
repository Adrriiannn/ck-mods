namespace ExpandNullforge.Api
{
    public interface IDimensionGenerationSeedService
    {
        uint ResolveGenerationSeed(DimensionGenerationSeedRequest request);

        int ResolveGenerationRange(
            DimensionGenerationSeedRequest request,
            int maxExclusive);

        float ResolveGenerationUnitFloat(DimensionGenerationSeedRequest request);
    }
}
