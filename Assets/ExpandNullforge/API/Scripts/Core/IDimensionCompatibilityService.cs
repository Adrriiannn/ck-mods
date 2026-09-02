namespace ExpandNullforge.Api
{
    public interface IDimensionCompatibilityService
    {
        DimensionCoordinateCompatibilityResult ResolveCoordinateForDimensionAwareOperation(
            DimensionCoordinateCompatibilityRequest request);
    }
}
