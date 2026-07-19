namespace ExpandNullforge.Api
{
    public interface IDimensionAccessProvider
    {
        string ProviderId { get; }

        int Priority { get; }

        bool TryEvaluateTravelAccess(DimensionAccessContext context, out DimensionAccessResult result);
    }
}
