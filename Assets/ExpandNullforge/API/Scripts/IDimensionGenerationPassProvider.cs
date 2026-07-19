namespace ExpandNullforge.Api
{
    public interface IDimensionGenerationPassProvider
    {
        DimensionGenerationProviderResult TickGenerationPass(DimensionGenerationPassContext context);
    }
}
