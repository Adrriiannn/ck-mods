using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionPermissionService
    {
        bool TryRegisterPermissionProvider(
            IDimensionPermissionProvider provider,
            out DimensionOperationResult result);

        bool TryRemovePermissionProvider(
            string providerId,
            out DimensionOperationResult result);

        IReadOnlyList<string> GetPermissionProviderIds();

        DimensionPermissionResult EvaluatePermission(
            DimensionPermissionContext context);
    }
}
