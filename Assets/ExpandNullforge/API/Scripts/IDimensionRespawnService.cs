namespace ExpandNullforge.Api
{
    public interface IDimensionRespawnService
    {
        DimensionRespawnTarget ResolveRespawnTarget(DimensionRespawnRequest request);
    }
}
