namespace ExpandNullforge.Api
{
    public interface IDimensionReturnTargetService
    {
        DimensionReturnTargetResult ResolveReturnTarget(DimensionReturnTargetRequest request);
    }
}
