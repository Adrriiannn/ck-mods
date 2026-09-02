namespace ExpandNullforge.Api
{
    public interface IDimensionPermissionProvider
    {
        string ProviderId { get; }

        int Priority { get; }

        bool TryEvaluatePermission(
            DimensionPermissionContext context,
            out DimensionPermissionResult result);
    }
}
